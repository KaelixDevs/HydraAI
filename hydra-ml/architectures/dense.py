"""Small causal dense baseline. Not a released Hydra model architecture."""
from dataclasses import dataclass
import torch
from torch import nn


@dataclass
class Config:
    vocab_size: int = 256
    context: int = 128
    width: int = 128
    heads: int = 4
    layers: int = 4


class TinyCausalTransformer(nn.Module):
    def __init__(self, config):
        super().__init__()
        self.config = config
        self.token = nn.Embedding(config.vocab_size, config.width)
        self.position = nn.Embedding(config.context, config.width)
        layer = nn.TransformerEncoderLayer(config.width, config.heads, 4 * config.width, dropout=0.0, batch_first=True)
        self.layers = nn.TransformerEncoder(layer, config.layers, enable_nested_tensor=False)
        self.norm = nn.LayerNorm(config.width)
        self.output = nn.Linear(config.width, config.vocab_size, bias=False)

    def forward(self, ids):
        length = ids.shape[1]
        if length > self.config.context:
            raise ValueError("Input exceeds configured context")
        positions = torch.arange(length, device=ids.device)
        x = self.token(ids) + self.position(positions)
        mask = torch.triu(torch.ones(length, length, device=ids.device, dtype=torch.bool), diagonal=1)
        return self.output(self.norm(self.layers(x, mask=mask, is_causal=True)))
