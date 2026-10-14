# Local inference

Hydra implements its own application, identity registry, context assembly, permissions, persistence and lifecycle. The initial native inference adapter uses the independent, open-source llama.cpp engine. It communicates only with its owned process at `127.0.0.1` on a dynamically selected port using an ephemeral API key. The streaming protocol's familiar route names do not contact OpenAI or any other hosted model service.

1. Obtain a trusted Windows x64 CPU build from https://github.com/ggml-org/llama.cpp/releases and extract the entire distribution.
2. Obtain a GGUF whose upstream license permits your use. Record its original name, source and license. The installer contains no Hydra-trained model.
3. Open **Model Manager**, select `llama-server.exe`, import the GGUF, then click **Load locally**. The file is hashed on import and again before loading.
4. Choose a Hydra behavior profile and chat. Every response retains the actual backend's name. Auto routing is a keyword heuristic.

The first adapter explicitly uses CPU mode (`--n-gpu-layers 0`) for repeatability. GPU support, model-specific chat template settings, structured tool calls and multimodal inputs need additional integration. A small CPU model is suitable for integration tests, not a flagship-quality substitute.

The supported runtime flags are `-m`, `--host`, `--port`, `--ctx-size`, `--api-key` and `--n-gpu-layers`. A successful health probe precedes generation. Incompatible runtime/model versions, missing DLLs, exhausted RAM, or invalid chat templates produce an error rather than an artificial answer. No runtime output is persisted because it may include sensitive prompts.

Generation reserves 1,024 output tokens and uses a conservative UTF-8-byte estimate to select recent messages. Old context may be omitted. Exact tokenizer budgeting and summarization are planned. Selected attachment excerpts are included as explicitly untrusted reference text. Press **Stop** to cancel and terminate the owned process; reload it before generating again.

Reference: https://github.com/ggml-org/llama.cpp/tree/master/tools/server .
