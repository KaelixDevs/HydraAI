# Linux and Android — 0.2.0 preview

Both clients include SmolLM2-135M-Instruct / bartowski Q4_K_M and llama.cpp b11429. Starter setup works offline. This small model gets inference running; its answers and generated code often need correction.

## Install on Arch / EndeavourOS / Manjaro

Extract `HydraAI-0.2.0-Arch-Setup.zip`. Keep its `HydraAI-Arch-Setup.sh` and `hydra-ai-0.2.0-1-x86_64.pkg.tar.zst` together. Run `bash HydraAI-Arch-Setup.sh` in that folder. Setup verifies the checksum and opens pacman with its usual confirmation prompt. Alternatively, run `sudo pacman -U ./hydra-ai-0.2.0-1-x86_64.pkg.tar.zst`.

Open Hydra AI from the application menu. Choose **Models → Set up included starter → Load locally**, then **New chat**.

## Install the RPM / portable Linux build

Run `sudo dnf install ./HydraAI-0.2.0-1.x86_64.rpm`. This targets modern Fedora x86_64 with OpenSSL 3 and glibc 2.38 or later, not EL8/EL9. Dependencies are declared; the RPM is not repository-signed. Activate the starter in **Models**.

Alternatively, extract `HydraAI-0.2.0-linux-x64.tar.gz` and run `HydraAI/hydra-ai`. Install the system dependencies listed in the package recipes. .NET is bundled. The client uses X11; Wayland desktops need XWayland. ARM Linux is not included.

## Install on Android

Install `HydraAI-0.2.0-arm64.apk` on an **ARM64 Android 8.0 or newer** phone or tablet. Allow installation from the app you used to open it when Android asks. It is a signed direct-install APK, not a Play Store release.

Choose **Settings → Set up included starter → Load model → Open chat**. No computer, model server or internet connection is needed. Type in the single prompt bar; **⋯** opens profiles, model settings and attachments. Native controls, touch-sized buttons, scrolling pages, keyboard insets and saved rotation drafts support smaller screens.

The engine uses up to four CPU threads, a 2,048-token context and up to 384 output tokens. Generation stops when the app leaves the foreground. Start a new chat if the context fills. Import other instruct/chat GGUF files with compatible templates through Settings. Imports are copied to private storage and limited to 2 GB; RAM may impose a lower practical limit. The starter is about 100 MB; installation and setup need space for both the APK and a model copy. GPU acceleration and specific device-speed claims are not included.

Save code blocks beneath responses, then use **Files** to export a copy. Settings can connect an accessible Obsidian vault or backup folder using Android's folder picker. Uninstalling removes private app data, so export files you want to keep. Android saves source and HTML; it does not run desktop binary exporters or rename text into binary formats.

## Feature differences

| Capability | Windows 0.1.1 | Linux 0.2.0 | Android 0.2.0 |
| --- | --- | --- | --- |
| Local inference | CPU process | CPU process | CPU/JNI on phone |
| Compact prompt / options | Yes | Yes | Yes |
| Chats / text attachments | SQLite | SQLite | JSON |
| File library | Source and configured exporters | Source and user-imported files | Source / HTML |
| Vault / folder backups | Yes | Yes | Android folder picker |
| Local Git commits | Yes | Yes, installed Git | No |
| Workspace edit / undo | Yes | Not yet exposed | No |
| Auto profile | Keyword routing | Keyword routing | General profile |

## Build Linux

Install .NET SDK 10, Python 3.11+ and Python's `zstandard` package in your build environment. Run `bash scripts/build-linux.sh`. It publishes the client, downloads hash-pinned runtime/model assets, retains notices and creates packages in `artifacts/releases`. This is an explicit release-builder download step. The installed app does not download models automatically. `scripts/package-linux.py` can also package a prepared Linux publish directory on Windows.

## Build Android

Use JDK 17, Gradle 8.13, SDK platform 36, build-tools 35.0.0 and 36.0.0, NDK 28.2.13676358 and CMake 3.22.1. Accept SDK licenses. Prepare inputs with `python scripts/fetch-platform-assets.py --android-assets artifacts/android-starter --llama-source artifacts/llama.cpp-b11429`.

On Windows, run `scripts/build-android.ps1` with `-AndroidSdk`, `-JavaHome`, `-GradleExecutable`, `-LlamaSource` and `-StarterDirectory artifacts/android-starter`. Output is `artifacts/HydraAI-0.2.0-arm64.apk`. Optional `-DirectoryPackaging -PythonExecutable <python>` works around AAPT2 ZIP-writer crashes using directory output, Gradle-compiled classes, D8, ZIP alignment and official signing tools. Android lint still runs.

On Linux, run `gradle -p apps/android -PllamaDir=<absolute-source-path> assembleRelease lintRelease` after staging `starter.gguf` and license notices in `app/build/generated/starter`. CI demonstrates this. Unsigned CI APKs must be signed before installation.

**Back up the private APK signing key and password.** The helper creates them under ignored `artifacts/private-android-signing`, or accepts `HYDRA_ANDROID_KEYSTORE`, `HYDRA_ANDROID_STORE_PASSWORD` and `HYDRA_ANDROID_KEY_PASSWORD`. Future updates must use the same key. Never publish this directory. Public downloads include only the signed APK and public certificate information.

See [validation](VALIDATION.md) for build checks versus actual device and distribution testing.
