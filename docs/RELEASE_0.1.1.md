# Hydra AI 0.1.1

Close Hydra and run the new installer to update the same installation. User conversations, model registrations and settings remain in the existing local data directory. Clean-VM upgrade testing has not been performed.

- Compact expanding message bar with a three-dot options menu. Enter sends; Shift+Enter inserts a new line. Profiles, backend selection, attachments and conversation actions live in the menu.
- Settings connect a local Obsidian vault or regular Git repository for unique backup snapshots. Automatic copies and local Git commits are opt-in. Hydra does not push to a remote or include unrelated staged changes in backup commits.
- Saved response files persist in the Files library. HTML/source, Roblox XML places and OBJ work directly. Configured local tools build C# EXE packages, binary Roblox places and Blender/FBX meshes. See FILE_EXPORTS.md for exact inputs and requirements.
- Verified real Git, C# compiler and Blender exports. The Rojo binary-place check remains blocked by tool initialization permissions in the test environment; the XML-place export passed.

Optional exporter tools are not bundled or downloaded automatically. The included inference starter is unchanged, so output quality remains limited by the selected upstream model. No new Hydra-trained weights are included.
