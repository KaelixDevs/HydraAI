# Privacy

Hydra starts in local mode with internet features off. No telemetry or advertising SDK is included. There is no usage quota and no subscription system.

Chats, messages, imported file excerpts, model metadata, projects and settings are stored locally in SQLite under `%LOCALAPPDATA%/HydraAI`. `HYDRA_DATA_DIR` can select a different directory. SQLite backups/WAL files may retain deleted content until storage is reclaimed. Deletion is not a forensic erasure guarantee.

Your prompts are sent only to the explicitly started loopback inference process. No cloud account is required. Imported models retain their original identities. Native inference runtimes are separate trusted software with their own behavior and licenses.

Text attachments are read on explicit selection and bounded excerpts can be included in inference. Workspace files are accessed only after choosing a root. Private conversations are never automatically added to datasets or uploaded for training.

Research web access and model downloads are separate opt-in settings. The current UI does not implement automatic web research or model downloads. Cloud sync, remote accounts and telemetry remain unavailable; enabling future features must explain their endpoints and data flow.

The local database is plaintext. OS account permissions and disk encryption provide protection at rest. Clipboard copying is explicit. Diagnostics must omit message text, file contents, credentials and user paths.

Saved outputs, metadata and build intermediates also remain in the local Hydra data directory. Optional Obsidian/Git backups copy saved files into destinations you select, without chat transcripts. Your vault or repository sync software may upload these copies independently of Hydra. Hydra makes local commits only and never pushes. Disconnecting a destination or undoing a workspace edit does not remove existing backup snapshots.
