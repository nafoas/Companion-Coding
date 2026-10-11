"""Regenerates src/CompanionCore.Capture.Worker/SceneLabels.json (VIS-04).

The Scene Sniff compares each frame's SigLIP image embedding against a fixed, neutral
vocabulary of scene descriptions. Their text embeddings are computed once, here, with the
pinned SigLIP text model, so the app ships only the vision model.

Usage (Python 3.11+, run from the repository root):
    python -m venv .venv && .venv/bin/pip install onnxruntime tokenizers numpy
    python tools/scene-labels/make_scene_labels.py <dir with text_model.onnx and tokenizer.json>

The files come from https://huggingface.co/Xenova/siglip-base-patch16-224 at REVISION; the
text model must match TEXT_MODEL_SHA256. The logit scale and bias are read from Google's
original weights (google/siglip-base-patch16-224, Apache-2.0) at GOOGLE_REVISION.
"""

import base64
import hashlib
import json
import pathlib
import sys

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

REVISION = "4649052661e53c7000355844105f8a1792088239"
GOOGLE_REVISION = "7fd15f0689c79d79e38b1c2e2e2370a7bf2761ed"
TEXT_MODEL_SHA256 = "3aa7fdbd20eaa8740cce17bf82913de641fcb632a768fed59f661cdcd0c32553"
VISION_MODEL_SHA256 = "f89d41bac7f4d4b87e010a467d93f98689d708916ed22f5a07f96fdfa26f475f"
LOGIT_SCALE = 4.764997  # google/siglip-base-patch16-224 logit_scale (log of the multiplier)
LOGIT_BIAS = -12.932437  # google/siglip-base-patch16-224 logit_bias
MAX_TOKENS = 64

# Stable keys (their order is the wire index; append only) and neutral descriptions.
LABELS = [
    ("menu", "a video game menu screen"),
    ("loading", "a video game loading screen"),
    ("map", "a video game map screen"),
    ("inventory", "a video game inventory screen"),
    ("dialogue", "a video game dialogue scene with subtitles"),
    ("cutscene", "a cinematic video game cutscene"),
    ("combat", "a video game combat scene with enemies"),
    ("boss", "a video game boss fight"),
    ("explosion", "an explosion in a video game"),
    ("closeup", "a close up of a video game character"),
    ("landscape", "a video game character exploring an outdoor landscape"),
    ("dungeon", "a dark video game cave or dungeon"),
    ("town", "a video game town with buildings and people"),
    ("underwater", "an underwater video game scene"),
    ("driving", "driving a vehicle in a video game"),
    ("flying", "flying through the sky in a video game"),
    ("game-over", "a video game over screen"),
    ("victory", "a video game victory or reward screen"),
    ("crafting", "a video game crafting or building screen"),
    ("shop", "a video game shop or merchant screen"),
    ("puzzle", "a video game puzzle"),
    ("night", "a video game scene at night"),
    ("snow", "a snowy video game scene"),
    ("desert", "a desert in a video game"),
    ("forest", "a forest in a video game"),
    ("space", "outer space in a video game"),
    ("interior", "a video game scene inside a building"),
    ("character-creation", "a video game character creation screen"),
    ("dark", "a mostly black screen"),
    ("title", "a video game title screen"),
    ("platformer", "a side scrolling platformer video game"),
    ("strategy", "a top down strategy video game"),
]


def main(model_dir: str) -> None:
    root = pathlib.Path(model_dir)
    text_model = root / "text_model.onnx"
    digest = hashlib.sha256(text_model.read_bytes()).hexdigest()
    if digest != TEXT_MODEL_SHA256:
        raise SystemExit(f"text_model.onnx digest {digest} does not match the pinned model")

    tokenizer = Tokenizer.from_file(str(root / "tokenizer.json"))
    pad = tokenizer.token_to_id("</s>")
    ids = []
    for _, text in LABELS:
        tokens = tokenizer.encode(text.lower()).ids[:MAX_TOKENS]
        ids.append(tokens + [pad] * (MAX_TOKENS - len(tokens)))

    session = ort.InferenceSession(str(text_model), providers=["CPUExecutionProvider"])
    embeddings = session.run(None, {"input_ids": np.array(ids, dtype=np.int64)})[1].astype(np.float32)
    embeddings /= np.linalg.norm(embeddings, axis=1, keepdims=True)

    table = {
        "model": "Xenova/siglip-base-patch16-224",
        "revision": REVISION,
        "visionModelSha256": VISION_MODEL_SHA256,
        "textModelSha256": TEXT_MODEL_SHA256,
        "logitScale": LOGIT_SCALE,
        "logitBias": LOGIT_BIAS,
        "dimensions": int(embeddings.shape[1]),
        "labels": [
            {
                "key": key,
                "text": text,
                "embedding": base64.b64encode(embeddings[index].astype("<f4").tobytes()).decode("ascii"),
            }
            for index, (key, text) in enumerate(LABELS)
        ],
    }
    out = pathlib.Path(__file__).resolve().parents[2] / "src" / "CompanionCore.Capture.Worker" / "SceneLabels.json"
    out.write_text(json.dumps(table, indent=1) + "\n", encoding="utf-8")
    print(f"wrote {len(LABELS)} labels × {embeddings.shape[1]} to {out}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
