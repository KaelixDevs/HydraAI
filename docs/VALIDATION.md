# Validation — 0.1.1 development

Development validation performed on this Windows x64 machine. These results do not constitute a security audit, cross-hardware certification, or AI benchmark.

## Observed results

- Native WPF desktop and .NET core compiled successfully with warnings treated as errors: **zero build warnings/errors**.
- Core test executable: **27/27 passed** in the final configured suite, including SQLite migration/reopen/Unicode/parameter binding, transaction rollback, branching/cascades, workspace confinement/approval/undo conflicts, model tampering, streamed event parsing, truncated stream errors, bounded context, routing, retrieval, citation integrity, password hashing, signed manifests, real RAM detection, verified downloads and range resumption.
- New output checks passed: completed code-fence extraction and filename handling; persistent artifact metadata and tamper rejection; repeated Obsidian snapshots and binary preservation; mesh bounds and actual OBJ; escaped RBXLX source. Real Git backup commits preserved unrelated staged changes. The .NET 10 SDK produced a Windows EXE package without executing it. Blender **4.5.9** produced actual BLEND and FBX files from a validated triangle fixture.
- Python standard-library pipeline suite: **10/10 passed**, covering deduplication, provenance/license rules, private-data consent, PII filtering, contamination, stable splits, invalid IDs, missing predictions and Unicode tokenizer round trips.
- UI smoke run: **10 screens rendered**, missing-backend generation blocked, compact composer height and three-dot profile menu checked, generated-file library persistence checked, and an actual model response streamed and persisted with its upstream identity. Screenshots in `docs/screenshots` were captured from WPF rendering and visually inspected.
- Real CPU inference: llama.cpp **b11429**, **SmolLM2-135M-Instruct / bartowski Q4_K_M**, with model SHA-256 `2e8040ceae7815abe0dcb3540b9995eaa1fa0d2ca9e797d0a635ae4433c68c2d`. Import, checksum verification, process startup, streamed content and unload succeeded.
- Self-contained Windows publish succeeded using .NET **10.0.12**, SDK **10.0.401**. Inno Setup **7.1.0** compiled a Windows setup executable.
- Packaging checks verified the local runtime files, self-contained runtime configuration, Windows executable header and installer product metadata. Version strings are normalized for padding introduced by the installer compiler.

## Model-quality limitation observed

The tiny starter backend failed a simple arithmetic prompt during integration testing. One actual output was: “Two plus two is a combination of two and two, resulting in two.” The integration test only checks that real inference executes and streams; it does not score the answer as correct. A separate UI test produced an actual forest-description response. No capability parity, mathematical reliability, or Hydra benchmark score is claimed. Import stronger appropriately licensed weights for useful work and verify important results.

## Not validated or not implemented

- The optional Rojo **7.7.1** RBXL integration test failed with `Access is denied (os error 5)` while initializing its in-memory filesystem/file watcher, before project loading. A direct invocation reproduced it. This test is not included in the 27 passing tests above. The RBXL adapter remains unverified in this environment; it reports exporter failures without fabricating a file. RBXLX does not need Rojo and passed its XML structure/source tests. Opening exported places in Roblox Studio was not tested.
- Clean Windows 10/11 VM installation, repair, upgrade, shortcuts and uninstall. The installer is compiled but not claimed to have passed that matrix.
- Production Authenticode signing: no signing certificate is configured; installer is unsigned.
- GPU inference, VRAM probing, out-of-memory behavior across hardware, multi-gigabyte models, and adversarial local-process filesystem races.
- Full application security review and accessibility/user testing.
- Training execution: PyTorch was not installed here. Tiny Transformer/trainer source is included but no checkpoint was trained and no training success is claimed.
- Live account service, cloud sync, real web search, native 3D, multimodal ingestion, autonomous terminal agents, and production update service.
- Remote CI was not run: the workflow definition is included for future repository pushes.

## Reproduce

Run `scripts/build.ps1`, then `scripts/test.ps1 -Python <python-executable>`. For the optional real inference test, set `HYDRA_LLAMA_RUNTIME` to the trusted server executable and `HYDRA_TEST_MODEL` to the above GGUF, then run `dotnet run --project tests/Hydra.Tests.csproj -c Release`. Test fixtures are explicitly labelled and never used as application responses.

For optional real output tests, set `HYDRA_TEST_GIT`, `HYDRA_TEST_DOTNET`, `HYDRA_TEST_BLENDER`, and/or `HYDRA_TEST_ROJO` to trusted tool executables. `HYDRA_TEST_ROOT` selects a disposable test parent directory; each run creates and cleans up its own unique child directory.

For the binary UI test, set `HYDRA_DATA_DIR` to a fresh disposable directory, then run `HydraAI.exe --smoke --with-starter --output <directory>`. This exits after rendering, generating with the included upstream model, and validating persistence. Run `installer/test-package.ps1` to check packaged runtime dependencies and installer metadata. These checks do not install Hydra into the user's system.
