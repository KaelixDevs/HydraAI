#include <jni.h>
#include <llama.h>
#include <atomic>
#include <algorithm>
#include <memory>
#include <string>
#include <vector>

struct Session {
    llama_model * model = nullptr;
    llama_context * context = nullptr;
    std::atomic<bool> stopped{false};
    ~Session() { if (context) llama_free(context); if (model) llama_model_free(model); }
};
static void error(JNIEnv * env, const char * message) { env->ThrowNew(env->FindClass("java/lang/IllegalStateException"), message); }
static std::string utf8(JNIEnv * env, jstring value) {
    // JNI modified UTF-8 corrupts emoji. Use Java's standard UTF-8 encoder.
    jclass stringClass = env->FindClass("java/lang/String");
    jmethodID getBytes = env->GetMethodID(stringClass, "getBytes", "(Ljava/lang/String;)[B");
    jstring encoding = env->NewStringUTF("UTF-8");
    auto bytes = static_cast<jbyteArray>(env->CallObjectMethod(value, getBytes, encoding));
    std::string result(env->GetArrayLength(bytes), '\0');
    env->GetByteArrayRegion(bytes, 0, result.size(), reinterpret_cast<jbyte *>(result.data()));
    env->DeleteLocalRef(bytes); env->DeleteLocalRef(encoding); env->DeleteLocalRef(stringClass); return result;
}
extern "C" JNIEXPORT jlong JNICALL Java_dev_kaelix_hydra_LocalEngine_load(JNIEnv * env, jclass, jstring path, jint threads) {
    llama_log_set([](enum ggml_log_level, const char *, void *) {}, nullptr);
    llama_backend_init();
    auto session = std::make_unique<Session>();
    auto modelParams = llama_model_default_params(); modelParams.n_gpu_layers = 0;
    session->model = llama_model_load_from_file(utf8(env, path).c_str(), modelParams);
    if (!session->model) { error(env, "Cannot load this GGUF. Check format and available RAM."); return 0; }
    auto params = llama_context_default_params(); params.n_ctx = 2048; params.n_batch = 256; params.n_ubatch = 128;
    params.n_threads = std::max(1, std::min(threads, 8)); params.n_threads_batch = params.n_threads;
    params.abort_callback = [](void * data) { return static_cast<Session *>(data)->stopped.load(); }; params.abort_callback_data = session.get();
    session->context = llama_init_from_model(session->model, params);
    if (!session->context) { error(env, "Not enough memory for the model context. Try a smaller GGUF."); return 0; }
    return reinterpret_cast<jlong>(session.release());
}
extern "C" JNIEXPORT void JNICALL Java_dev_kaelix_hydra_LocalEngine_cancel(JNIEnv *, jclass, jlong handle) {
    if (handle) reinterpret_cast<Session *>(handle)->stopped.store(true);
}
extern "C" JNIEXPORT void JNICALL Java_dev_kaelix_hydra_LocalEngine_close(JNIEnv *, jclass, jlong handle) { delete reinterpret_cast<Session *>(handle); }
extern "C" JNIEXPORT void JNICALL Java_dev_kaelix_hydra_LocalEngine_reset(JNIEnv *, jclass, jlong handle) { if (handle) reinterpret_cast<Session *>(handle)->stopped.store(false); }
extern "C" JNIEXPORT void JNICALL Java_dev_kaelix_hydra_LocalEngine_generate(JNIEnv * env, jclass, jlong handle, jobjectArray roles, jobjectArray contents, jobject callback) {
    auto * s = reinterpret_cast<Session *>(handle); if (!s) { error(env, "Load a model first."); return; }
    if (s->stopped.load()) return;
    llama_memory_clear(llama_get_memory(s->context), true);
    const auto count = env->GetArrayLength(roles);
    if (count != env->GetArrayLength(contents) || count > 64) { error(env, "Invalid conversation."); return; }
    std::vector<std::string> r, c; r.reserve(count); c.reserve(count);
    for (int i=0; i<count; ++i) {
        auto role = static_cast<jstring>(env->GetObjectArrayElement(roles, i));
        auto content = static_cast<jstring>(env->GetObjectArrayElement(contents, i));
        r.push_back(utf8(env, role)); c.push_back(utf8(env, content)); env->DeleteLocalRef(role); env->DeleteLocalRef(content);
    }
    std::vector<llama_chat_message> messages;
    for (int i=0; i<count; ++i) messages.push_back({r[i].c_str(), c[i].c_str()});
    const char * tmpl = llama_model_chat_template(s->model, nullptr);
    if (!tmpl) { error(env, "This GGUF has no supported chat template. Choose an instruct/chat model."); return; }
    int length = llama_chat_apply_template(tmpl, messages.data(), messages.size(), true, nullptr, 0);
    if (length <= 0 || length > 1024*1024) { error(env, "Unsupported model chat template or oversized prompt."); return; }
    std::vector<char> prompt(length + 1);
    llama_chat_apply_template(tmpl, messages.data(), messages.size(), true, prompt.data(), prompt.size());
    const auto * vocab = llama_model_get_vocab(s->model);
    int n = -llama_tokenize(vocab, prompt.data(), length, nullptr, 0, true, true);
    if (n < 1 || n > static_cast<int>(llama_n_ctx(s->context)) - 64) { error(env, "Conversation exceeds the phone context limit. Start a new chat or shorten the prompt."); return; }
    std::vector<llama_token> tokens(n); llama_tokenize(vocab, prompt.data(), length, tokens.data(), n, true, true);
    for (int offset=0; offset<n; offset+=256) {
        if (s->stopped.load()) return;
        auto batch = llama_batch_get_one(tokens.data()+offset, std::min(256, n-offset));
        if (llama_decode(s->context, batch) != 0) { if (!s->stopped.load()) error(env, "Inference failed while processing the prompt."); return; }
    }
    auto * sampler = llama_sampler_chain_init(llama_sampler_chain_default_params());
    llama_sampler_chain_add(sampler, llama_sampler_init_top_k(40));
    llama_sampler_chain_add(sampler, llama_sampler_init_top_p(0.95f, 1));
    llama_sampler_chain_add(sampler, llama_sampler_init_temp(0.7f));
    llama_sampler_chain_add(sampler, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
    auto callbackClass = env->GetObjectClass(callback); auto onToken = env->GetMethodID(callbackClass, "onToken", "([B)V");
    const int limit = std::min(384, static_cast<int>(llama_n_ctx(s->context)) - n - 1);
    for (int i=0; i<limit && !s->stopped.load(); ++i) {
        auto token = llama_sampler_sample(sampler, s->context, -1);
        if (llama_vocab_is_eog(vocab, token)) break;
        std::vector<char> piece(64); int size = llama_token_to_piece(vocab, token, piece.data(), piece.size(), 0, true);
        if (size < 0) { piece.resize(-size); size = llama_token_to_piece(vocab, token, piece.data(), piece.size(), 0, true); }
        if (size > 0) {
            jbyteArray bytes = env->NewByteArray(size); env->SetByteArrayRegion(bytes, 0, size, reinterpret_cast<jbyte *>(piece.data()));
            env->CallVoidMethod(callback, onToken, bytes); env->DeleteLocalRef(bytes); if (env->ExceptionCheck()) break;
        }
        auto batch = llama_batch_get_one(&token, 1);
        if (llama_decode(s->context, batch) != 0) { if (!s->stopped.load()) error(env, "Inference could not continue."); break; }
    }
    env->DeleteLocalRef(callbackClass); llama_sampler_free(sampler);
}
