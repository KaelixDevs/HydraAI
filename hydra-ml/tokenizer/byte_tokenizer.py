"""A reversible UTF-8 byte baseline. Production tokenizer training is future work."""
class ByteTokenizer:
    vocab_size = 256

    @staticmethod
    def encode(text):
        return list(text.encode("utf-8"))

    @staticmethod
    def decode(tokens):
        return bytes(tokens).decode("utf-8", errors="replace")
