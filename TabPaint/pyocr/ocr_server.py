import base64
import io
import json
import os
import sys
import traceback

import numpy as np
from PIL import Image
from rapidocr_onnxruntime import RapidOCR


def send(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def load_image_from_base64(image_base64: str):
    raw = base64.b64decode(image_base64)
    image = Image.open(io.BytesIO(raw)).convert("RGB")
    return np.array(image)


def flatten_text(result):
    """
    RapidOCR 返回格式: list[ [box, text, confidence], ... ] 或 None
    每个元素是 [四点坐标, 文字, 置信度]
    """
    if not result:
        return ""

    lines = []
    for item in result:
        if not item or len(item) < 2:
            continue
        text = item[1]
        if text:
            lines.append(str(text))
    return "\n".join(lines).strip()


def extract_lines(result):
    """
    返回行级结构：
    [{"text": "...", "box": [x1, y1, x2, y2]}]
    box 由四点坐标归一成外接矩形
    """
    if not result:
        return []

    lines = []
    for item in result:
        if not item or len(item) < 2:
            continue

        text = str(item[1]).strip() if item[1] is not None else ""
        if not text:
            continue

        box = item[0] if len(item) > 0 else None
        rect = None
        if box and isinstance(box, (list, tuple)) and len(box) >= 4:
            try:
                xs = [float(p[0]) for p in box if isinstance(p, (list, tuple)) and len(p) >= 2]
                ys = [float(p[1]) for p in box if isinstance(p, (list, tuple)) and len(p) >= 2]
                if xs and ys:
                    rect = [min(xs), min(ys), max(xs), max(ys)]
            except Exception:
                rect = None

        lines.append({"text": text, "box": rect})

    return lines


def main():
    try:
        ocr = RapidOCR()

        # 预热：消化首次推理的延迟初始化
        dummy = np.zeros((32, 100, 3), dtype=np.uint8)
        ocr(dummy)

        send({"event": "ready", "ok": True})
    except Exception as ex:
        send({"event": "ready", "ok": False, "error": str(ex)})
        return

    for raw in sys.stdin:
        raw = raw.strip()
        if not raw:
            continue

        req_id = None
        try:
            req = json.loads(raw)
            req_id = req.get("Id") or req.get("id")
            image_base64 = req.get("ImageBase64") or req.get("image_base64")
            if not image_base64:
                raise ValueError("Missing image_base64")

            image = load_image_from_base64(image_base64)
            result, elapse = ocr(image)
            text = flatten_text(result)
            lines = extract_lines(result)
            send({"id": req_id, "ok": True, "text": text, "lines": lines})
        except Exception as ex:
            send(
                {
                    "id": req_id,
                    "ok": False,
                    "error": str(ex),
                    "traceback": traceback.format_exc(limit=2),
                }
            )


if __name__ == "__main__":
    main()
