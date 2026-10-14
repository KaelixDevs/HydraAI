# Files and backups in 0.1.1

Ask for source in a fenced code block, optionally with a filename: `html filename=index.html`, `csharp filename=Program.cs`, or `luau filename=main.server.luau`. Save buttons appear below completed responses. A saved output persists in the Files library before you choose an external destination. You can also add existing files of any type to the library and back them up.

| Output | Input and requirement |
| --- | --- |
| HTML, text, JSON, CSS, JS, Python, C++, other source | Completed code block; no extra tool |
| RBXLX | Lua/Luau block; Hydra packages a Workspace and a ServerScriptService script in a genuine XML place |
| RBXL | Lua/Luau block; configured trusted Rojo executable builds a binary place |
| Windows EXE package | C# console source; configured .NET 10 SDK compiles it; ZIP includes EXE, DLL and runtime configuration; .NET 10 required to run |
| OBJ | Valid `hydra-mesh` JSON; no extra tool |
| BLEND / FBX | Same mesh data; configured trusted Blender; fixed exporter creates a mesh object |

Executable output is currently C# only. These exporters do not turn an arbitrary description into a production game or detailed textured scene. The backend must produce valid source/data. Binary extensions on plain text are rejected; binaries are emitted only by real tools. Generated code is never run automatically.

For a mesh, ask for a `hydra-mesh` code block containing vertices and zero-based face indices, for example:

```hydra-mesh
{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}
```

The export buttons support this format only; arbitrary generated Python never runs in Blender. Mesh topology is bounded and checked for valid indices, but artistic quality, manifoldness and game readiness are not guaranteed.

Configure tool paths in Settings → File export tools. Rojo's RBXL command is implemented, but its 7.7.1 executable returned access denied during filesystem-watcher initialization in this restricted validation environment. A real RBXL build could not be confirmed here. RBXLX works without Rojo and can be opened and saved as RBXL in Roblox Studio. .NET 10 and Blender 4.5.9 real output tests passed.

In Settings → Backups · Obsidian & Git, choose an existing vault/repository. Enable automatic backups to snapshot newly saved outputs and approved workspace writes. Alternatively, use Files → Back up now. All files, including binary attachments, use separate directories under `HydraAI-Backups/date/id/files`. A Markdown note links to each file. Existing content is never overwritten. Repeated backups create separate snapshots.

Optional Git commits include only the new backup files and preserve unrelated staged changes. A regular repository and checked-out local branch are required; linked worktrees are not supported. No remote push occurs. Configure remote sync separately with your normal Git or Obsidian tooling. A failed commit leaves copied files available and reports the failure. Backups are not removed by undo, disconnection or chat deletion.
