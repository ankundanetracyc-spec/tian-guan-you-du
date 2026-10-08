from __future__ import annotations

from collections import deque
from pathlib import Path
import shutil
import sys

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "textures" / "characters" / "c1_spine_ingame_rig_v8"
SOURCE_DIR = PACKAGE / "source"
LAYER_DIR = PACKAGE / "layers"
SOURCE_SHEET = SOURCE_DIR / "c1_material_sheet_source.jpg"
V7_LAYER_DIR = ROOT / "textures" / "characters" / "c1_spine_ingame_rig_v7" / "layers"
TARGET_SIZE = (1024, 1536)


def is_checker_candidate(rgb: tuple[int, int, int]) -> bool:
    spread = max(rgb) - min(rgb)
    value = sum(rgb) / 3.0
    # The supplied JPG uses a neutral gray checkerboard.  The chromatic
    # character pixels are therefore deliberately excluded before flood fill.
    # JPEG compression makes the checkerboard slightly uneven, but the
    # character's dark hair and trousers carry enough warm/cool tint that a
    # tight neutral threshold keeps their interiors out of the flood fill.
    return spread <= 7 and 55 <= value <= 178


def remove_checkerboard(image: Image.Image) -> Image.Image:
    image = image.convert("RGB")
    width, height = image.size
    pixels = image.load()
    background = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def try_seed(x: int, y: int) -> None:
        index = y * width + x
        if background[index] or not is_checker_candidate(pixels[x, y]):
            return
        background[index] = 1
        queue.append((x, y))

    for x in range(width):
        try_seed(x, 0)
        try_seed(x, height - 1)
    for y in range(height):
        try_seed(0, y)
        try_seed(width - 1, y)

    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or nx >= width or ny < 0 or ny >= height:
                continue
            index = ny * width + nx
            if background[index] or not is_checker_candidate(pixels[nx, ny]):
                continue
            background[index] = 1
            queue.append((nx, ny))

    output = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    out_pixels = output.load()
    for y in range(height):
        row = y * width
        for x in range(width):
            if not background[row + x]:
                r, g, b = pixels[x, y]
                out_pixels[x, y] = (r, g, b, 255)
    return output


def remove_small_components(image: Image.Image, minimum_size: int = 12) -> Image.Image:
    """Drop isolated JPG speckles without touching connected sprite edges."""
    image = image.copy().convert("RGBA")
    width, height = image.size
    alpha = image.getchannel("A")
    values = alpha.load()
    visited = bytearray(width * height)
    remove: list[tuple[int, int]] = []
    for y in range(height):
        for x in range(width):
            index = y * width + x
            if visited[index] or values[x, y] == 0:
                continue
            visited[index] = 1
            queue: deque[tuple[int, int]] = deque([(x, y)])
            component: list[tuple[int, int]] = []
            while queue:
                px, py = queue.popleft()
                component.append((px, py))
                for nx in range(px - 1, px + 2):
                    for ny in range(py - 1, py + 2):
                        if nx < 0 or nx >= width or ny < 0 or ny >= height:
                            continue
                        ni = ny * width + nx
                        if visited[ni] or values[nx, ny] == 0:
                            continue
                        visited[ni] = 1
                        queue.append((nx, ny))
            if len(component) < minimum_size:
                remove.extend(component)
    for x, y in remove:
        values[x, y] = 0
    image.putalpha(alpha)
    return image


def harden_alpha(path: Path, target: Path) -> None:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 150 else 0)
    image.putalpha(alpha)
    image.save(target, "PNG", optimize=True)


def trim_alpha(image: Image.Image) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    return image.crop(bbox) if bbox else Image.new("RGBA", (1, 1), (0, 0, 0, 0))


def make_kneel_layer(clean_sheet: Image.Image, target: Path) -> None:
    # The source sheet's middle-lower kneeling legs are deliberately used as
    # an alternate attachment rather than simply rotating the standing legs.
    # Coordinates are source-sheet pixels; the sheet maps to the 1024x1536
    # runtime canvas at 1.5x.
    kneel = clean_sheet.crop((205, 630, 390, 720))
    kneel = remove_small_components(trim_alpha(kneel), minimum_size=18)
    kneel = kneel.resize((round(kneel.width * 2.8), round(kneel.height * 2.8)), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0))
    # Place the knees under the waist while leaving the long robe layer above
    # them.  The death animation rotates the torso and root into the pose.
    x = (TARGET_SIZE[0] - kneel.width) // 2 - 12
    y = 930
    canvas.alpha_composite(kneel, (x, y))
    canvas.save(target, "PNG", optimize=True)


def main() -> None:
    source_arg = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(
        r"C:\Users\34784\AppData\Local\Temp\codex-clipboard-ba8628f4-4414-4f60-b9d5-3b79f8ce03a6.jpg"
    )
    if not source_arg.exists():
        raise FileNotFoundError(source_arg)

    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    LAYER_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_arg, SOURCE_SHEET)

    clean_sheet = remove_small_components(remove_checkerboard(Image.open(source_arg)))
    clean_sheet.save(SOURCE_DIR / "c1_material_sheet_clean_v1.png", "PNG", optimize=True)

    # Keep the already aligned base art, but remove its generated atmospheric
    # alpha haze so every v8 layer is a genuine transparent sprite layer.
    for name in ("body_core", "head_hair", "lower_body", "ribbons", "arm_waist", "arm_raised"):
        harden_alpha(V7_LAYER_DIR / f"{name}.png", LAYER_DIR / f"{name}.png")

    make_kneel_layer(clean_sheet, LAYER_DIR / "lower_kneel.png")
    Image.new("RGBA", TARGET_SIZE, (0, 0, 0, 0)).save(LAYER_DIR / "lower_empty.png", "PNG", optimize=True)
    print(SOURCE_DIR / "c1_material_sheet_clean_v1.png")
    print(LAYER_DIR / "lower_kneel.png")


if __name__ == "__main__":
    main()
