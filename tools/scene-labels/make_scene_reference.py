"""Regenerates tests/CompanionCore.Capture.Worker.Tests/SceneReference.json (VIS-04).

A synthetic 640x360 pattern is preprocessed exactly as ScenePreprocessor does it (area-sampled
224x224 squash, 4x4 taps, RGB scaled to [-1, 1], channel-major) and embedded with the pinned
SigLIP vision model on ONNX Runtime's CPU provider. The worker tests check that C# produces
the same tensor (on every platform) and, on Windows CI with the real model, the same embedding.

Usage: python tools/scene-labels/make_scene_reference.py <dir with vision_model.onnx>
"""

import base64
import hashlib
import json
import pathlib
import sys

import numpy as np
import onnxruntime as ort

VISION_MODEL_SHA256 = "f89d41bac7f4d4b87e010a467d93f98689d708916ed22f5a07f96fdfa26f475f"
WIDTH, HEIGHT, SIZE, TAPS = 640, 360, 224, 4


def pattern() -> np.ndarray:
    """RGB pattern: horizontal red ramp, vertical green ramp, blue stripes, and a red block."""
    image = np.zeros((HEIGHT, WIDTH, 3), dtype=np.int64)
    for y in range(HEIGHT):
        for x in range(WIDTH):
            if 200 <= x < 440 and 120 <= y < 240:
                image[y, x] = (230, 40, 40)
            else:
                image[y, x] = (x * 255 // (WIDTH - 1), y * 255 // (HEIGHT - 1), ((x + y) % 64) * 4)
    return image


def preprocess(image: np.ndarray) -> np.ndarray:
    height, width, _ = image.shape
    columns = [[min(width - 1, int(((x + (tap + 0.5) / TAPS) * width) / SIZE)) for tap in range(TAPS)] for x in range(SIZE)]
    rows = [[min(height - 1, int(((y + (tap + 0.5) / TAPS) * height) / SIZE)) for tap in range(TAPS)] for y in range(SIZE)]
    tensor = np.zeros((3, SIZE, SIZE), dtype=np.float32)
    scale = np.float32(1) / np.float32(TAPS * TAPS * 127.5)
    for y in range(SIZE):
        block = image[rows[y]]  # TAPS x width x 3
        for x in range(SIZE):
            total = block[:, columns[x], :].reshape(-1, 3).sum(axis=0)
            tensor[:, y, x] = total.astype(np.float32) * scale - np.float32(1)
    return tensor


def main(model_dir: str) -> None:
    model = pathlib.Path(model_dir) / "vision_model.onnx"
    if hashlib.sha256(model.read_bytes()).hexdigest() != VISION_MODEL_SHA256:
        raise SystemExit("vision_model.onnx does not match the pinned model")

    tensor = preprocess(pattern())
    session = ort.InferenceSession(str(model), providers=["CPUExecutionProvider"])
    embedding = session.run(["pooler_output"], {"pixel_values": tensor[None]})[0][0].astype(np.float32)
    embedding /= np.linalg.norm(embedding)
    flat = tensor.reshape(-1)
    probes = [0, 1, 1000, 50175, 50176, 75000, 100352, 150527]
    reference = {
        "width": WIDTH,
        "height": HEIGHT,
        "visionModelSha256": VISION_MODEL_SHA256,
        "tensorSum": float(flat.astype(np.float64).sum()),
        "tensorProbes": {str(index): float(flat[index]) for index in probes},
        "embedding": base64.b64encode(embedding.astype("<f4").tobytes()).decode("ascii"),
    }
    out = pathlib.Path(__file__).resolve().parents[2] / "tests" / "CompanionCore.Capture.Worker.Tests" / "SceneReference.json"
    out.write_text(json.dumps(reference, indent=1) + "\n", encoding="utf-8")
    print(f"wrote {out}; tensor sum {reference['tensorSum']:.4f}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
