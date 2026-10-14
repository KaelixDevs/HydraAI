# Feature status — 0.1.1 development

| Area | Available | Remaining |
| --- | --- | --- |
| Windows desktop | Native WPF navigation, original vector identity, local mode | Qt client, advanced animations, accessibility audit |
| Conversation storage | SQLite migrations, CRUD, pin/search-title/rename/branch, response identity/state | Full-text chat search, encrypted storage |
| Chat UI | Compact expanding composer, three-dot options menu, streaming, Stop, copy, edit-as-new, regenerate into branch, continue, basic headings/bold/code fences | Syntax colors, tables, in-place message editing |
| Models | Six specialist profiles, rule-based Auto, GGUF registration, integrity checks, remove registration, starter asset setup | Hydra-trained weights, learned/multi-agent routing, managed catalogue |
| Inference | Actual CPU llama.cpp streaming, bounded context, process health/start/stop, error handling | GPU configuration, multimodal, structured tools, exact token budgeting |
| Attachments | UTF-8 text/source, drag-drop, stored excerpts, overlapping chunks, lexical retrieval | PDF/Office/image parsers, large-file/vector indexing |
| Kairo workspace | Root selection, file listing/read, approved before/after write review, undo protection | Autonomous agent, terminal, Git project editing, line-level diff, OS sandbox |
| Files / exports | Persistent downloadable source/HTML, RBXLX, validated OBJ; C# EXE packages and BLEND/FBX through configured tools; Rojo RBXL adapter | RBXL integration validation blocked by local tool initialization permissions; arbitrary complex scene generation |
| Backups | Obsidian vault and local Git snapshots; opt-in automatic copies and isolated local backup commits | Remote push, worktree repositories, backup retention UI |
| Research | Provider/fetch/parser/planner/ranker interfaces, citation content hashes, gated session framework | Actual live providers, cross-checking, deep research, source scoring |
| Accounts | Separate service contracts, tested salted password utility, account-free local mode | Deployed service, login/reset/MFA/device management/sync |
| Privacy | Internet off by default, explicit preferences, local storage disclosure, redacted diagnostic export | Encrypted sync, user-memory UI |
| Downloads/updates | Opt-in resumable downloader primitive, size/range/hash verification, signature verification primitive | Catalogue/UI integration, signed production update feeds/rollback |
| Hardware | OS, CPU core count, physical RAM, rough capacity guidance | GPU/VRAM vendor detection and measured model recommendations |
| ML | Licensed provenance pipeline, deterministic splits/dedup/PII heuristics, contamination checks, tiny causal Transformer source, checkpoint/resume source | Training execution validation, SFT/DPO/RL/MoE/multimodal/distributed training |
| Evaluation | Exact-match/coverage CLI with input/model hashes; specialist suite specifications; dashboard showing pending evaluations | Specialist runners, human rubrics, benchmark report import into UI |
| Distribution | Self-contained build, Inno Setup recipe, portable release, CI definition | Production signing, clean-VM installer matrix, update service |

See VALIDATION.md for observed test results. Compiling an interface is not an implementation of the associated intelligence capability.
