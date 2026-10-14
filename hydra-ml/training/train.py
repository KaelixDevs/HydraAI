"""Reproducible CPU baseline; uses only explicitly prepared local JSONL."""
import argparse
import hashlib
import json
import random
import sys
from dataclasses import asdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import torch
from architectures.dense import Config, TinyCausalTransformer
from tokenizer.byte_tokenizer import ByteTokenizer


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--data", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--steps", type=int, default=100)
    p.add_argument("--seed", type=int, default=42)
    p.add_argument("--learning-rate", type=float, default=3e-4)
    p.add_argument("--resume", type=Path)
    args = p.parse_args()
    if args.steps < 1:
        p.error("steps must be positive")
    random.seed(args.seed)
    torch.manual_seed(args.seed)
    torch.use_deterministic_algorithms(True)
    raw = args.data.read_bytes()
    data_hash = hashlib.sha256(raw).hexdigest()
    records = [json.loads(line) for line in raw.decode().splitlines() if line.strip()]
    if any(r.get("split") != "train" or not r.get("sha256") for r in records):
        raise ValueError("Use prepared train.jsonl, never validation or private chat exports")
    tokens = torch.tensor(ByteTokenizer.encode("\n\n".join(r["text"] for r in records)), dtype=torch.long)
    config = Config()
    if tokens.numel() <= config.context + 1:
        raise ValueError("Not enough licensed training text")
    model = TinyCausalTransformer(config)
    optimizer = torch.optim.AdamW(model.parameters(), lr=args.learning_rate)
    step = 0
    if args.resume:
        checkpoint = torch.load(args.resume, map_location="cpu", weights_only=True)
        if checkpoint["data_sha256"] != data_hash or checkpoint["config"] != asdict(config):
            raise ValueError("Resume requires identical data/configuration")
        model.load_state_dict(checkpoint["model"])
        optimizer.load_state_dict(checkpoint["optimizer"])
        torch.set_rng_state(checkpoint["rng"])
        step = checkpoint["step"]
    model.train()
    losses = []
    for _ in range(args.steps):
        starts = torch.randint(0, tokens.numel() - config.context - 1, (4,))
        batch = torch.stack([tokens[i:i+config.context+1] for i in starts.tolist()])
        logits = model(batch[:, :-1])
        loss = torch.nn.functional.cross_entropy(logits.reshape(-1, config.vocab_size), batch[:, 1:].reshape(-1))
        optimizer.zero_grad(set_to_none=True)
        loss.backward()
        torch.nn.utils.clip_grad_norm_(model.parameters(), 1.0)
        optimizer.step()
        step += 1
        losses.append(float(loss.detach()))
        print(json.dumps({"step": step, "training_loss": losses[-1]}))
    args.output.mkdir(parents=True, exist_ok=True)
    temporary = args.output / "checkpoint.partial"
    torch.save({"model": model.state_dict(), "optimizer": optimizer.state_dict(), "config": asdict(config), "rng": torch.get_rng_state(), "step": step, "data_sha256": data_hash}, temporary)
    temporary.replace(args.output / "checkpoint.pt")
    (args.output / "run.json").write_text(json.dumps({"status": "research_only", "seed": args.seed, "steps": step, "data_sha256": data_hash, "torch": str(torch.__version__), "config": asdict(config), "training_losses": losses}, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
