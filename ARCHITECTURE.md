# Hydra AI architecture

Version: 0.2.0 platform expansion. Model weights are distributed separately from source.

`apps/linux` is a native Avalonia client sharing the .NET domain library. SQLite resolves to `libsqlite3.so.0` on Linux and `winsqlite3.dll` on Windows; workspace boundaries use OS-appropriate path case rules. Linux packages contain a self-contained .NET runtime, CPU llama-server and verified starter GGUF. X11 is the default display backend.

`apps/android` is a native Java client with a C++ JNI wrapper around statically linked llama.cpp. There is no HTTP server or internet permission. One background executor owns model imports, load and generation. A process-scoped Application object retains state across activity recreation; the main thread owns visible conversations. Native cancellation uses an atomic flag and a decode abort callback. UTF-8 bytes cross JNI to preserve emoji. Context is limited before decoding and output is bounded. Android's system file picker provides import, export and backup access without broad storage permission.

Desktop and mobile share upstream model/runtime versions but have separate storage and presentation implementations. Platform feature differences are listed in `docs/PLATFORMS.md`; Windows-only workspace and binary export controls are not claimed on the new clients.

## Boundaries

`apps/desktop` is a native Windows WPF shell. `core` is a UI-independent .NET library. `hydra-ml` contains Python dataset, training, and evaluation tools. WPF provides native accessibility, asynchronous I/O, and a runtime-bundled Windows distribution. Other clients can use the same domain boundaries.

The application owns conversation storage, model identity, routing, context assembly, permissions, and inference lifecycle. The initial inference adapter runs the open-source llama.cpp engine on loopback, using its streaming chat protocol. Protocol compatibility does not call or depend on a proprietary AI service. Model weights and the native inference runtime are separately installed assets, never silently downloaded. Third-party weights retain their original names and license metadata. Hydra profiles are behavior configurations, not independently trained models.

## Data and control flow

User → desktop → profile router → bounded local context → inference interface → local process → token stream → SQLite messages.

SQLite uses the OS-provided Windows SQLite library, prepared statements, WAL, foreign keys, migrations, and indexed chat queries. Data lives under `%LOCALAPPDATA%/HydraAI`, or `HYDRA_DATA_DIR` for test/portable use. The single desktop process serializes database access. Chat text is not logged. Data is local plaintext protected by OS user permissions; encryption at rest is not claimed.

Model import verifies a GGUF header and records SHA-256, original identity, license, path, and context configuration. Removal unregisters the backend without deleting the user's source weights. One runtime is loaded at a time. Streaming uses cancellation tokens; stopping terminates the owned inference process to release resources. A random per-process API key protects loopback requests. Runtime logs are discarded because third-party logs can contain prompts.

## Trust boundaries

Model output, attachments, repositories, research pages, manifests, and model metadata are untrusted. No model output is executed. Workspace tools constrain paths to a selected root, reject reparse points and sensitive files, and require explicit write approval. This is an application policy boundary, not an OS sandbox. Arbitrary terminal execution remains unavailable until an OS-isolated executor is implemented. Reading web content cannot grant tool permissions.

Networking is off by default except an explicitly started loopback inference process. Research and download interfaces require opt-in. Download artifacts require an expected hash and size; updates additionally require an offline-trusted signing key. No production signing key or cloud endpoint is provisioned.

## Extension contracts

- `IInferenceBackend`: cancellable streaming text from an explicitly identified backend.
- `HydraModel`: identity, provenance, capabilities, hardware and generation context metadata.
- `SearchProvider`, `PageFetcher`, `DocumentParser`: independent research adapters.
- `CitationManager`: references may only be registered from retrieved documents.
- `INyraBackend`: native 3D research boundary; no trained native 3D implementation. A separate validated mesh-data exporter creates ordinary OBJ/BLEND/FBX files.
- Account service contracts: future remote accounts, device/session revocation, reset and MFA; local mode never depends on them.

## Release boundary

The Windows installer packages the desktop and .NET runtime. The development distribution also includes an explicitly labelled SmolLM2 starter GGUF and llama.cpp CPU runtime. They remain separate assets and require user activation in Model Manager. Native inference binaries execute with the user's permissions and must come from a trusted distribution. CPU/RAM detection is available; full vendor/VRAM probing and automatic GPU selection are subsequent work.

## File outputs and local backups

`core/artifacts` extracts completed source fences into a persistent artifact store. Every output has a separate files directory, SHA-256, size and originating backend. Export copies verify integrity. `FileBuilders` accepts source or bounded mesh data and invokes only explicit trusted tool executables with argument lists, cancellation and bounded output capture. Generated programs are never executed. Blender runs a fixed application-owned exporter; model content is parsed only as data. C# uses a fixed project with offline package sources and no imported directory build hooks.

The desktop owns Save/build actions, the library and settings. Automatic backups require an explicit setting. `BackupService` uses unique snapshot directories beneath selected local roots. Git commits build a separate index with only snapshot files, bypass filters/hooks and preserve unrelated staged entries. Branch updates use compare-and-swap. There is no remote push or cloud API.
