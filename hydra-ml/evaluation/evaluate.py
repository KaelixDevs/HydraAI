"""Evaluate actual predictions, preserving missing answers and artifact identity."""
import argparse
import hashlib
import json
import re
from pathlib import Path


def keyed(rows, field):
    result = {}
    for row in rows:
        if not isinstance(row.get("id"), str) or not isinstance(row.get(field), str) or row["id"] in result:
            raise ValueError("Invalid or duplicate evaluation record")
        result[row["id"]] = row[field]
    return result


def evaluate(cases, predictions):
    expected, actual = keyed(cases, "expected"), keyed(predictions, "prediction")
    if not expected:
        raise ValueError("No evaluation cases")
    if actual.keys() - expected.keys():
        raise ValueError("Prediction IDs not present in evaluation cases")
    correct = sum(actual.get(k, "\0").strip() == value.strip() for k, value in expected.items())
    return {"total": len(expected), "answered": len(actual), "correct": correct, "exact_match": correct / len(expected), "coverage": len(actual) / len(expected)}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("cases", type=Path)
    p.add_argument("predictions", type=Path)
    p.add_argument("output", type=Path)
    p.add_argument("--model-id", required=True)
    p.add_argument("--model-sha256", required=True)
    args = p.parse_args()
    if not re.fullmatch(r"[0-9a-fA-F]{64}", args.model_sha256):
        p.error("model-sha256 must be a SHA-256 digest")
    data = [path.read_bytes() for path in (args.cases, args.predictions)]
    rows = [[json.loads(line) for line in raw.decode().splitlines() if line.strip()] for raw in data]
    report = {"schema_version": 1, "model_id": args.model_id, "model_sha256": args.model_sha256,
              "cases_sha256": hashlib.sha256(data[0]).hexdigest(), "predictions_sha256": hashlib.sha256(data[1]).hexdigest(),
              "metrics": evaluate(*rows), "limitations": "Exact-match evaluation only. No flagship or human-quality comparison implied."}
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
