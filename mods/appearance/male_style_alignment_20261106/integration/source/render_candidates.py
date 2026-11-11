"""Render diagnostic candidate combinations without changing any source layer."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


PACK = Path(__file__).resolve().parent.parent
CANVAS = (1024, 1536)
ORDER = ("hair_back", "clothing_back", "face_shape", "clothing", "nose",
         "eyes", "eyebrows", "mouth", "hair_front")
FONT = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 17)


def background(kind: str, size: tuple[int, int] = CANVAS) -> Image.Image:
    image = Image.new("RGBA", size, "#253136" if kind == "dark" else "#e8e1d6")
    if kind == "checker":
        draw = ImageDraw.Draw(image)
        for y in range(0, size[1], 24):
            for x in range(0, size[0], 24):
                if (x // 24 + y // 24) % 2:
                    draw.rectangle((x, y, x + 23, y + 23), fill="#aaa9a4")
    return image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--require-all-new", action="store_true",
                        help="Refuse diagnostic fallback for any of the nine replacement layers")
    args = parser.parse_args()
    qa = PACK / "qa"
    qa.mkdir(parents=True, exist_ok=True)
    inputs: dict[str, dict] = {}
    cache: dict[str, Image.Image] = {}

    def layer(part: str, clothing: int = 1) -> Image.Image:
        index = clothing if part in ("clothing", "clothing_back") else 1
        rel = f"{part}/{index:02}.png"
        new = PACK / "male" / rel
        preserved = PACK / "reference/current_male" / rel
        source = new if new.exists() else preserved
        diagnostic_fallback = index == 1 and not new.exists()
        if args.require_all_new and diagnostic_fallback:
            raise FileNotFoundError(f"Final review requires new layer: {new}")
        if not source.exists():
            raise FileNotFoundError(f"Missing candidate and frozen reference: {rel}")
        if rel not in cache:
            image = Image.open(source).convert("RGBA")
            if image.size != CANVAS:
                raise ValueError(f"Canvas mismatch: {source}")
            cache[rel] = image
            inputs[rel] = {"file": str(source), "new": new.exists(),
                           "diagnostic_fallback": diagnostic_fallback,
                           "preserved_other_outfit": index > 1 and not new.exists(),
                           "sha256": hashlib.sha256(source.read_bytes()).hexdigest()}
        return cache[rel]

    # Resolve all nine first: final mode must never silently reuse previous art.
    for part in ORDER:
        layer(part)

    def compose(kind: str, clothing: int = 1, hidden: set[str] | None = None) -> Image.Image:
        result = background(kind)
        for part in ORDER:
            if hidden and part in hidden:
                continue
            result.alpha_composite(layer(part, clothing))
        return result

    for kind in ("light", "dark", "checker"):
        full = compose(kind)
        full.save(qa / f"candidate_assembled_{kind}.png")
        full.crop((320, 230, 710, 570)).resize((780, 680)).save(qa / f"candidate_face_close_{kind}.png")
        compose(kind, hidden={"hair_front", "hair_back"}).save(qa / f"candidate_no_hair_{kind}.png")
        compose(kind, hidden={"clothing", "clothing_back"}).save(qa / f"candidate_no_clothes_{kind}.png")
        sheet = background(kind, (1400, 680))
        draw = ImageDraw.Draw(sheet)
        for clothing in range(1, 8):
            tile = compose(kind, clothing, {"hair_front", "hair_back"})
            tile = tile.crop((365, 485, 675, 785)).resize((350, 320))
            x, y = ((clothing - 1) % 4) * 350, ((clothing - 1) // 4) * 340
            sheet.alpha_composite(tile, (x, y + 20))
            draw.text((x + 8, y + 1), f"衣服 {clothing:02}  后领→新头颈→前衣", font=FONT,
                      fill="#e8e1d6" if kind == "dark" else "#263730")
        sheet.save(qa / f"seven_collars_{kind}.png")

    missing = [part for part in ORDER if inputs[f"{part}/01.png"]["diagnostic_fallback"]]
    result = {"canvas": list(CANVAS), "order_back_to_front": list(ORDER),
              "new_replacement_layers_ready": len(ORDER) - len(missing),
              "all_nine_new": not missing, "diagnostic_fallback_parts": missing,
              "mode": "final_all_new" if args.require_all_new else "diagnostic_fallback_allowed",
              "inputs": inputs,
              "note": "Other outfits02–07 intentionally retain frozen clothes. A missing first-outfit layer may appear only in diagnostic mode; final mode refuses all nine such fallbacks.",
              "installed": False}
    (qa / "candidate_assembly_audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"all_nine_new": not missing, "missing_new": missing,
                      "output": str(qa)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
