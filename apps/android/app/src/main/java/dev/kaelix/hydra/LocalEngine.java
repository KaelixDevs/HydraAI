package dev.kaelix.hydra;

final class LocalEngine {
    static { System.loadLibrary("hydra_llama"); }
    interface TokenSink { void onToken(byte[] utf8); }
    static native long load(String path, int threads);
    static native void generate(long handle, String[] roles, String[] contents, TokenSink callback);
    static native void cancel(long handle);
    static native void reset(long handle);
    static native void close(long handle);
}
