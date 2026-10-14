# Update and distribution design

The Inno Setup recipe installs per user without administrator privileges, creates Start Menu/optional desktop shortcuts, registers an uninstaller and includes version information. Re-running the installer repairs application files. Local conversation data lives separately and survives uninstall.

`scripts/package.ps1` bundles the .NET runtime; normal users do not need Python, Node, Visual Studio, Git or the .NET SDK. The initial installer is unsigned. Production signing requires the maintainer's own trusted signing identity; none is fabricated here.

Application, inference runtime, extensions and model weights are distinct update channels. A future channel manifest must contain channel, monotonic version, minimum app version, URL, length, SHA-256 and signature. Verify an ECDSA signature over the exact manifest bytes against a separately pinned key before trusting artifact hashes. Stage and hash artifacts, stop relevant processes, atomically activate the reviewed version, retain rollback metadata, and reject downgrade/replay. Model changes require explicit consent and preserve original identities.

`UpdateVerifier` implements signature/hash primitives. `ModelDownloader` implements opt-in HTTPS, resumable range transfers, declared-size bounds and hash verification before activation. It is not yet connected to a catalogue or UI. There is no automatic updater, production key, remote catalogue or silent replacement. ETag optimization, trusted catalogue management, interrupted-install recovery and signed update rollout remain release work.

Release builders can explicitly run `scripts/fetch-starter.ps1 -VisualCppRedist <licensed-x64-CRT-directory>` before packaging to reproduce the included development backend assets. Downloads are verified against fixed hashes. The default source-only packaging script does not download model weights.
