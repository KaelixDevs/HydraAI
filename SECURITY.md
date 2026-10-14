# Security

This is a development release, not a security-audited execution sandbox. Report vulnerabilities privately to the repository maintainer through the hosting platform's private advisory mechanism once a public repository is established. Do not include private prompts or credentials.

- Never execute model text or commands automatically. The first version exposes no autonomous terminal tool.
- Workspace access is rooted, rejects traversal/reparse points and common secret paths, and requires approval before writing. Root selection grants read access. Undo refuses to overwrite intervening edits.
- A hostile process running as the same OS user can race filesystem checks. Use separate OS accounts/VMs for hostile projects. Future agent execution requires OS-level isolation.
- The inference executable is trusted native code explicitly selected by the user. Only run verified distributions. It binds to loopback and uses a random ephemeral API key.
- Untrusted context is labelled as data; this is defense in depth, not a proof against prompt injection. It cannot grant permissions.
- Checksums establish artifact integrity relative to a trusted manifest, not publisher authenticity. Signed update manifests additionally require a separately trusted public key.
- Accounts are an interface/design only; no credentials are collected by the UI. Password hashing utilities use salted PBKDF2-SHA256 and constant-time verification; a deployed service must add rate limits, TLS, reset-token expiry/replay prevention, session revocation and independent review.
- No private text, passwords, access tokens or inference stdout are written to diagnostic logs.

Do not claim secure cloud authentication, encrypted sync, arbitrary command sandboxing, or automatic prompt-injection prevention before implementing and auditing them.

File exports use explicitly selected local tools (.NET SDK, Rojo, Blender, Git). Those tools run with your OS permissions; they are not sandboxed by Hydra. Builds require a user action and never execute the generated program. Review exported code before opening/running it. Mesh JSON is bounded and parsed as data by a fixed exporter. Backup commits disable hooks, signing, fsmonitor and content filters; only newly created backup paths are committed. Linked metadata directories and worktree repositories are not supported. Same-user filesystem races remain outside this protection.
