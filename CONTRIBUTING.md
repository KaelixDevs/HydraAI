# Contributing

Use .NET 10 on Windows and Python 3.11+ for ML utilities. Run `scripts/test.ps1` before proposing a change. Keep UI and inference infrastructure separate, use cancellable asynchronous I/O, and add meaningful tests for persistence, parsing, security and backend changes.

Preserve backward-compatible migrations. Never place secrets, downloaded model weights, private conversations, licensed datasets or build outputs in Git. Document every dependency and its license. New network features must be opt-in and disclose endpoints. New execution tools require an explicit permission design and security tests.

Use original model names. Clearly mark untrained Hydra profiles and experimental checkpoints. Publish benchmark methodology, dataset version, model hash, hardware, actual results and limitations. An unrun benchmark is not a score of zero or a claimed success.

No contribution grants permission to train on private user data. Dataset rights and distribution rights must be reviewed independently of code licensing.
