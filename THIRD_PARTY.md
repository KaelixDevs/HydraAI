# Third-party software

| Component | License / distribution |
| --- | --- |
| .NET / WPF | MIT; runtime notices retained in packaged distribution |
| Windows SQLite (`winsqlite3.dll`) | OS component, not redistributed by this project; SQLite public domain upstream |
| llama.cpp b11429 | MIT; packaged CPU development inference engine, distribution notices included in `runtime` |
| SmolLM2-135M-Instruct / bartowski Q4_K_M | Apache-2.0; packaged third-party development weights, upstream identity retained |
| Inno Setup | Its own license; build-time installer compiler, not original Hydra code |
| PyTorch | BSD-style; optional training dependency, not bundled |
| Microsoft Visual C++ release runtime | Microsoft redistributable license; application-local DLLs for the native backend, not relicensed as MIT |

No model or dataset is included in the source repository. Development binary distributions may include the above starter assets under their original licenses. Users must record upstream model identity, license and source on import. Hydra's MIT license does not relicense models or datasets. No claim of permissive licensing is inferred from file format.

Starter model: https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF (upstream https://huggingface.co/HuggingFaceTB/SmolLM2-135M-Instruct), SHA-256 `2e8040ceae7815abe0dcb3540b9995eaa1fa0d2ca9e797d0a635ae4433c68c2d`.

Runtime archive: https://github.com/ggml-org/llama.cpp/releases/tag/b11429 , SHA-256 `1283323272b04cd07905816a597a0da810918102de958f4ff6f7bbaa70ed2efe`. Upstream binary dependencies retain the accompanying licenses; the packaged runtime is CPU-only. Model quality is not a Hydra benchmark.

The Hydra source is open source. Windows platform components and Microsoft C++ redistributable binaries have their own terms. Release builders must be entitled to redistribute their Visual Studio runtime files; see https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files . No debug/non-redistributable DLLs are included. The .NET runtime license and third-party notices are in `licenses` in the binary distribution. Local runtime dependencies must be serviced by future application updates.

Reference documentation: https://learn.microsoft.com/dotnet/core/deploying/ and https://github.com/ggml-org/llama.cpp/tree/master/tools/server .
