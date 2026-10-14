# Hydra ML laboratory

These are reproducible research tools, not trained Hydra intelligence. No weights, private conversations, or third-party datasets are included.

## Dataset pipeline (standard library)

Input JSONL records require `id`, `text`, `source`, `license`, and `language`. Supported license identifiers are explicitly allowlisted as an initial policy, not legal advice or proof of rights. Private records require `training_consent: true`. Records with email, phone-like sequences, SSNs or common credential markers are rejected by heuristics; this is not complete PII detection. Language is supplied as provenance; automatic language identification is reserved for a reviewed adapter.

`python datasets/prepare.py input.jsonl output-directory --holdout evaluation.jsonl --seed 42`

The pipeline normalizes and deduplicates text, filters short/low-variety records, excludes exact and 5-word overlap with held-out answers/text, creates stable hash-based splits, records reasons without leaking record contents, and writes a hash/version manifest. Version the manifest and provenance, not sensitive raw data. Use curated holdouts; approximate contamination checks are conservative and imperfect.

## Small dense transformer experiment (optional PyTorch)

Install `requirements-training.txt` into an isolated environment. Run:

`python training/train.py --data prepared/train.jsonl --output experiment --steps 100 --seed 42`

The byte tokenizer and causal dense Transformer support a small next-token pretraining/continued-training experiment. Checkpoints include model, optimizer, random state, configuration and data hash. Resume with `--resume experiment/checkpoint.pt`; only load checkpoints you created/trust. This is not large-scale training, SFT masking, preference optimization, MoE, RL or multimodal training. Exact reproducibility can vary with hardware/PyTorch version. PyTorch training was not run unless the validation report explicitly says so.

## Evaluation

`python evaluation/evaluate.py cases.jsonl predictions.jsonl report.json --model-id upstream-name --model-sha256 HASH`

Cases require `id`, `expected`; predictions require `id`, `prediction`. Reports show exact match and coverage, input hashes and model identity. Unknown/duplicate IDs fail validation; unanswered cases remain failures, not silently excluded. This evaluator never executes untrusted generated code. SWE-style repository execution requires a disposable VM/container runner with networking off and resource limits. Rubric evaluation for tutoring, creativity, citations, reasoning and meshes remains planned.

`python -m unittest discover -s tests -v`

Directories for alignment, distillation, quantization, retrieval, tool use and multimodal work document planned interfaces and data requirements. Implementations remain on the roadmap. Synthetic/distillation data must record teacher identity, license and permission to use outputs.
