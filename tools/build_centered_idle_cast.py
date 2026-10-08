"""Create a deterministic, aligned 16-frame idle loop from the approved full character.

Unlike a generated multi-cell sheet, every frame is composed onto the same 512x768
canvas after the character's feet have been pinned to one common baseline.
"""

from collections import deque
from math import pi, sin
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

SOURCE = Path(r"C:\Users\34784\AppData\Local\Temp\codex-clipboard-f2429727-e4e8-4b84-9627-b7e0de142495.png")
DESTINATION = Path(r"E:\stacklands\mods\appearance\水墨施法男修_animated_v1\idle_cast_16_centered.png")
FRAME_W, FRAME_H, GRID = 512, 768, 4
BASELINE_Y = 718
TARGET_HEIGHT = 660


def is_checker_pixel(r, g, b):
    return min(r, g, b) >= 224 and max(r, g, b) - min(r, g, b) <= 13


def remove_edge_checkerboard(image):
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    visited = bytearray(width * height)
    queue = deque()

    def add(x, y):
        index = y * width + x
        if visited[index]:
            return
        r, g, b, _ = pixels[x, y]
        if not is_checker_pixel(r, g, b):
            return
        visited[index] = 1
        queue.append((x, y))

    for x in range(width):
        add(x, 0); add(x, height - 1)
    for y in range(height):
        add(0, y); add(width - 1, y)
    while queue:
        x, y = queue.popleft()
        r, g, b, _ = pixels[x, y]
        pixels[x, y] = (r, g, b, 0)
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height:
                add(nx, ny)
    return rgba


def ink_qi(frame_index):
    """A light, symmetric rise-and-fall ink curl during the middle eight frames."""
    progress = max(0.0, 1.0 - abs(frame_index - 8.0) / 4.0)
    if progress <= 0:
        return None
    layer = Image.new("RGBA", (FRAME_W, FRAME_H))
    draw = ImageDraw.Draw(layer)
    cx, cy = 390, 215
    radius = int(16 + progress * 34)
    for ring in range(3):
        inset = ring * 8
        opacity = int((0.18 - ring * 0.04) * 255 * progress)
        draw.arc((cx - radius + inset, cy - radius + inset, cx + radius - inset, cy + radius - inset),
                 205, 510, fill=(63, 86, 96, opacity), width=max(1, 4 - ring))
    return layer.filter(ImageFilter.GaussianBlur(radius=1.1))


def main():
    source = remove_edge_checkerboard(Image.open(SOURCE))
    box = source.getbbox()
    character = source.crop(box)
    base_width = round(character.width * TARGET_HEIGHT / character.height)
    character = character.resize((base_width, TARGET_HEIGHT), Image.LANCZOS)
    sheet = Image.new("RGBA", (FRAME_W * GRID, FRAME_H * GRID))

    for index in range(16):
        phase = 2 * pi * index / 16
        # Breathing is applied from the feet upward, preserving the shared baseline.
        height = round(TARGET_HEIGHT * (1 + 0.009 * sin(phase)))
        width = round(base_width * (1 + 0.002 * sin(phase)))
        posed = character.resize((width, height), Image.LANCZOS)
        frame = Image.new("RGBA", (FRAME_W, FRAME_H))
        x = (FRAME_W - width) // 2
        y = BASELINE_Y - height
        frame.alpha_composite(posed, (x, y))
        qi = ink_qi(index)
        if qi:
            frame.alpha_composite(qi)
        col, row = index % GRID, index // GRID
        sheet.alpha_composite(frame, (col * FRAME_W, row * FRAME_H))

    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(DESTINATION)
    print(f"saved {DESTINATION} ({sheet.size[0]}x{sheet.size[1]}, RGBA)")


if __name__ == "__main__":
    main()
