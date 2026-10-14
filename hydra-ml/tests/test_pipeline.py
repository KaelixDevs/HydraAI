import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, relative):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


dataset = load("prepare", "datasets/prepare.py")
evaluation = load("evaluate", "evaluation/evaluate.py")
tokenizer = load("byte_tokenizer", "tokenizer/byte_tokenizer.py")


class PipelineTests(unittest.TestCase):
    def record(self, **overrides):
        return {"id": "one", "text": "This original training example contains several distinct words and ideas for testing.", "source": "original unit test", "license": "CC0-1.0", "language": "en", **overrides}

    def test_dedup_normalizes_whitespace_case(self):
        record = self.record()
        rows, rejected = dataset.prepare([record, {**record, "id": "two", "text": record["text"].upper()}])
        self.assertEqual(len(rows), 1)
        self.assertEqual(rejected, {"duplicate_text": 1})

    def test_private_consent(self):
        rows, reasons = dataset.prepare([self.record(private=True)])
        self.assertEqual(rows, [])
        self.assertIn("no_private_training_consent", reasons)

    def test_pii(self):
        rows, reasons = dataset.prepare([self.record(text="A private person's email is alice@example.org and must not be included in training.")])
        self.assertEqual(rows, [])
        self.assertIn("pii_or_secret", reasons)

    def test_licenses_and_provenance(self):
        self.assertFalse(dataset.prepare([self.record(license="unknown")])[0])
        self.assertFalse(dataset.prepare([self.record(source="")])[0])

    def test_holdout_contamination(self):
        record = self.record()
        self.assertFalse(dataset.prepare([record], [record["text"]])[0])
        self.assertFalse(dataset.prepare([record], ["contains several distinct words and ideas"])[0])

    def test_stable_split(self):
        rows = [self.record(id=str(i), text=f"Example {i} contains different original words with enough data to demonstrate deterministic train splits.") for i in range(40)]
        self.assertEqual(dataset.prepare(rows)[0], dataset.prepare(reversed(rows))[0])

    def test_duplicate_ids_fail(self):
        with self.assertRaises(ValueError):
            dataset.prepare([self.record(), self.record()])

    def test_missing_predictions_are_failures(self):
        result = evaluation.evaluate([{"id": "1", "expected": "a"}, {"id": "2", "expected": "b"}], [{"id": "1", "prediction": "a"}])
        self.assertEqual(result["exact_match"], 0.5)
        self.assertEqual(result["coverage"], 0.5)

    def test_unknown_predictions_fail(self):
        with self.assertRaises(ValueError):
            evaluation.evaluate([{"id": "1", "expected": "a"}], [{"id": "2", "prediction": "a"}])

    def test_byte_tokenizer_unicode_roundtrip(self):
        text = "Hydra 世界 🐉"
        self.assertEqual(tokenizer.ByteTokenizer.decode(tokenizer.ByteTokenizer.encode(text)), text)


if __name__ == "__main__":
    unittest.main()
