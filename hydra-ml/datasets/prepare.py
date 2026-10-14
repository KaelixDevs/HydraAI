"""Conservative, provenance-preserving dataset preparation. No network access."""
import argparse
import hashlib
import json
import re
import unicodedata
from collections import Counter
from pathlib import Path

LICENSES = {"MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "CC0-1.0", "CC-BY-4.0"}
PII = re.compile(r"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|\b\d{3}-\d{2}-\d{4}\b|\b(?:\+?\d[\s().-]*){10,15}\b|-----BEGIN .*PRIVATE KEY-----|\b(?:sk-[A-Za-z0-9]{16,}|AKIA[A-Z0-9]{16})\b")


def normalized(text):
    return " ".join(unicodedata.normalize("NFKC", text).casefold().split())


def digest(data):
    return hashlib.sha256(data).hexdigest()


def grams(text, n=5):
    words = normalized(text).split()
    return {" ".join(words[i:i+n]) for i in range(max(0, len(words)-n+1))}


def prepare(records, holdouts=(), seed=42):
    holdout_text = [normalized(t) for t in holdouts if t.strip()]
    contamination = set().union(*(grams(t) for t in holdout_text)) if holdout_text else set()
    seen, ids = set(), set()
    accepted, rejected = [], Counter()
    for record in records:
        if not all(isinstance(record.get(k), str) and record[k].strip() for k in ("id", "text", "source", "license", "language")):
            rejected["missing_provenance"] += 1
            continue
        if record["id"] in ids:
            raise ValueError("Duplicate record ID")
        ids.add(record["id"])
        text = record["text"]
        if record["license"] not in LICENSES:
            rejected["license_not_allowed"] += 1
        elif record.get("private", False) and record.get("training_consent") is not True:
            rejected["no_private_training_consent"] += 1
        elif PII.search(text):
            rejected["pii_or_secret"] += 1
        elif len(text.strip()) < 40 or len(set(text.casefold().split())) < 5:
            rejected["quality"] += 1
        else:
            normal = normalized(text)
            sha = digest(normal.encode())
            if normal in holdout_text or any(h in normal for h in holdout_text) or grams(text) & contamination:
                rejected["holdout_contamination"] += 1
            elif sha in seen:
                rejected["duplicate_text"] += 1
            else:
                seen.add(sha)
                split_value = int(digest(f"{seed}:{sha}".encode())[:8], 16) % 100
                split = "test" if split_value < 5 else "validation" if split_value < 10 else "train"
                accepted.append({**record, "sha256": sha, "split": split})
    return sorted(accepted, key=lambda r: r["sha256"]), dict(rejected)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--holdout", type=Path)
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()
    raw = args.input.read_bytes()
    records = [json.loads(line) for line in raw.decode("utf-8").splitlines() if line.strip()]
    holdout = []
    if args.holdout:
        holdout = [json.loads(line) for line in args.holdout.read_text(encoding="utf-8").splitlines() if line.strip()]
    rows, rejected = prepare(records, [r.get("text", r.get("expected", "")) for r in holdout], args.seed)
    args.output.mkdir(parents=True, exist_ok=True)
    hashes = {}
    for split in ("train", "validation", "test"):
        content = "".join(json.dumps(r, ensure_ascii=False, sort_keys=True) + "\n" for r in rows if r["split"] == split)
        (args.output / f"{split}.jsonl").write_text(content, encoding="utf-8", newline="\n")
        hashes[split] = digest(content.encode())
    manifest = {"pipeline_version": 1, "seed": args.seed, "input_sha256": digest(raw), "holdout_sha256": digest(args.holdout.read_bytes()) if args.holdout else None,
                "accepted": len(rows), "rejected": rejected, "split_sha256": hashes, "language_detection": "provided_metadata_not_inferred"}
    (args.output / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
