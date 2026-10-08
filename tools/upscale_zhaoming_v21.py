# -*- coding: utf-8 -*-
"""Create a crisp presentation-scale terrain from the approved clean source."""
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter


SOURCE = Path(r"C:\Users\34784\.codex\generated_images\019fd814-9245-70f0-83fb-c97b952236b0\exec-c05df959-7813-4f87-9cc7-ab69fcf3367d.png")
MASTER_OUT = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v21_clean.png")
PRESENTATION_OUT = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v21_clean_4096.png")


def main() -> None:
    if not SOURCE.exists():
        raise RuntimeError(f"Clean terrain source missing: {SOURCE}")
    MASTER_OUT.parent.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert("RGB")
    source.save(MASTER_OUT, optimize=True)
    # Two-stage scaling avoids the washed-out appearance of one giant resize.
    # Mild unsharp masking restores painted coast and mountain edges without
    # inventing noisy terrain texture.
    enlarged = source.resize((2508, 2508), Image.Resampling.LANCZOS)
    enlarged = enlarged.filter(ImageFilter.UnsharpMask(radius=1.4, percent=115, threshold=3))
    enlarged = ImageEnhance.Contrast(enlarged).enhance(1.025)
    enlarged = enlarged.resize((4096, 4096), Image.Resampling.LANCZOS)
    enlarged = enlarged.filter(ImageFilter.UnsharpMask(radius=1.0, percent=70, threshold=4))
    enlarged.save(PRESENTATION_OUT, optimize=True)
    print(PRESENTATION_OUT, enlarged.size)


if __name__ == "__main__":
    main()
