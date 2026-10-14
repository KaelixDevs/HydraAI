# Hydra AI 0.2.0 — Linux and Android preview

New native Linux client with local streaming chat, a compact prompt and options menu, GGUF imports, a file library and Obsidian/local Git backups. Downloads include an Arch setup script with a checksum-verified pacman package, an RPM and a portable Linux archive.

New native Android app for ARM64 phones/tablets running Android 8+. The APK runs llama.cpp directly on the device and includes the starter model. It has no internet permission and needs no remote server. Mobile controls support smaller screens, the keyboard, text attachments, saved conversations, source exports and folder backups.

After installing, activate the included starter in Models (Linux) or Settings (Android), then load it. SmolLM2-135M-Instruct / bartowski Q4_K_M is a small third-party development model with limited answer quality. It is not Hydra-trained intelligence.

See PLATFORMS.md for installation, system requirements and feature differences. Android does not run Git commits or desktop binary exporters. Linux uses X11/XWayland. These packages are previews; compilation and package validation do not establish a clean-install or physical-device test matrix. Windows users can continue using the 0.1.1 installer.
