"""Convert the approved generated 4x4 sheet into a true-alpha Godot sprite sheet.

The source generator rendered a visible light checkerboard despite a transparency request.
This script removes only the edge-connected neutral checkerboard, then normalizes the
four-by-four grid to 320px cells without changing frame order.
"""

from collections import deque
from pathlib import Path
from typing import Tuple
from PIL import Image

SOURCE = Path(r"C:\Users\34784\.codex\generated_images\01a0481a-467d-79c3-bb05-15de60e40db8\exec-12ac1cb8-337f-4882-b4a8-bd9d46609e11.png")
DESTINATION = Path(r"E:\stacklands\mods\appearance\水墨施法男修_animated_v1\idle_cast_16.png")
GRID = 4
CELL_SIZE = 384
BASELINE_Y = 352
CENTER_X = 192


def is_checker_pixel(red: int, green: int, blue: int) -> bool:
    """The generated checkerboard is neutral, almost-white and edge-connected."""
    return min(red, green, blue) >= 224 and max(red, green, blue) - min(red, green, blue) <= 12


def clear_edge_connected_checkerboard(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    seen = bytearray(width * height)
    queue = deque()

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if seen[index]:
            return
        red, green, blue, _ = pixels[x, y]
        if not is_checker_pixel(red, green, blue):
            return
        seen[index] = 1
        queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        red, green, blue, _ = pixels[x, y]
        pixels[x, y] = (red, green, blue, 0)
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < width:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < height:
            enqueue(x, y + 1)
    return rgba


def largest_component_bbox(image: Image.Image) -> Tuple[int, int, int, int]:
    """Find the torso/leg silhouette, ignoring detached ink effects and ribbons."""
    alpha = image.getchannel("A")
    width, height = image.size
    pixels = alpha.load()
    visited = bytearray(width * height)
    largest: list[tuple[int, int]] = []
    for start_y in range(height):
        for start_x in range(width):
            index = start_y * width + start_x
            if visited[index] or pixels[start_x, start_y] < 48:
                continue
            visited[index] = 1
            queue = deque([(start_x, start_y)])
            component: list[tuple[int, int]] = []
            while queue:
                x, y = queue.popleft()
                component.append((x, y))
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < width and 0 <= ny < height:
                        neighbour = ny * width + nx
                        if not visited[neighbour] and pixels[nx, ny] >= 48:
                            visited[neighbour] = 1
                            queue.append((nx, ny))
            if len(component) > len(largest):
                largest = component
    left = min(x for x, _ in largest)
    top = min(y for _, y in largest)
    right = max(x for x, _ in largest) + 1
    bottom = max(y for _, y in largest) + 1
    return left, top, right, bottom


def main() -> None:
    source = clear_edge_connected_checkerboard(Image.open(SOURCE))
    width, height = source.size
    output = Image.new("RGBA", (CELL_SIZE * GRID, CELL_SIZE * GRID))
    for row in range(GRID):
        top = round(row * height / GRID)
        bottom = round((row + 1) * height / GRID)
        for column in range(GRID):
            left = round(column * width / GRID)
            right = round((column + 1) * width / GRID)
            frame = source.crop((left, top, right, bottom))
            component_left, _, component_right, component_bottom = largest_component_bbox(frame)
            component_center = (component_left + component_right) / 2
            x = round(CENTER_X - component_center)
            y = BASELINE_Y - component_bottom
            output.alpha_composite(frame, (column * CELL_SIZE + x, row * CELL_SIZE + y))
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    output.save(DESTINATION)
    print(f"saved {DESTINATION} ({output.size[0]}x{output.size[1]}, RGBA)")


if __name__ == "__main__":
    main()
