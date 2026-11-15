"""Read-only comparison of the source robe carrier and a new costume alpha."""
from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--art", type=Path, default=Path(__file__).parent / "art/ink_outfit_01_20261104/assets/body.png")
    parser.add_argument("--output", type=Path, default=Path(__file__).parent / "output/ink_outfit_body_domain_audit")
    parser.add_argument("--closed-carrier", action="store_true")
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[2]
    frames = json.loads((project / "resources/appearance/guigu_male101/model_frames_20261104.json").read_text(encoding="utf-8"))
    template = json.loads((Path(__file__).parent / "art/ink_outfit_01_20261104/assets/template/fullcanvas_template_anchors.json").read_text(encoding="utf-8"))["components"]["body"]
    mesh = next(m for m in frames["meshes"] if m["category"] == "body")
    affine = template["cropUvToFullCanvasNormalized"]
    vertices = [(60*(affine[0][0]*u+affine[0][1]*v+affine[0][2]),100*(affine[1][0]*u+affine[1][1]*v+affine[1][2])) for u,v in mesh["uv"]]
    if args.closed_carrier:
        vertices.append((12.2,94.5))
        mesh["triangles"] = mesh["triangles"] + [[30,31,40],[31,32,40],[32,33,40]]
    edges = Counter(tuple(sorted((tri[i],tri[(i+1)%3]))) for tri in mesh["triangles"] for i in range(3))
    boundary = [pair for pair,count in edges.items() if count == 1]
    degrees = Counter(i for pair in boundary for i in pair)
    adjacency: dict[int,list[int]] = {}
    for a,b in boundary:
        adjacency.setdefault(a,[]).append(b)
        adjacency.setdefault(b,[]).append(a)
    loops = []
    remaining = set(adjacency)
    while remaining:
        start = min(remaining)
        previous, current = -1,start
        loop = []
        while current not in loop:
            loop.append(current)
            remaining.discard(current)
            following = next(i for i in adjacency[current] if i != previous)
            previous,current = current,following
        loops.append(loop)
    scale = 24
    canvas = (60*scale,100*scale)
    mask = Image.new("L",canvas)
    draw_mask = ImageDraw.Draw(mask)
    for triangle in mesh["triangles"]:
        draw_mask.polygon([(vertices[i][0]*scale,vertices[i][1]*scale) for i in triangle],fill=255)
    art = Image.open(args.art).convert("RGBA")
    art_alpha = art.getchannel("A").resize(canvas,Image.Resampling.NEAREST)
    total = outside = hem_total = hem_outside = 0
    alpha_pixels = art_alpha.load()
    mask_pixels = mask.load()
    for y in range(canvas[1]):
        for x in range(canvas[0]):
            if alpha_pixels[x,y] >= 179:
                total += 1
                if not mask_pixels[x,y]: outside += 1
                if y >= canvas[1]*0.75:
                    hem_total += 1
                    if not mask_pixels[x,y]: hem_outside += 1
    args.output.mkdir(parents=True,exist_ok=True)
    mask.save(args.output / "source_body_triangle_domain.png")
    report = {
        "source_sprite":int(mesh["spriteId"]),"source_vertices":40,"source_triangles":43,"carrier_vertices":len(vertices),"carrier_triangles":len(mesh["triangles"]),"engineered_rear_extension":args.closed_carrier,
        "unique_edges":len(edges),"boundary_edges":len(boundary),"boundary_loops":loops,
        "boundary_degree_is_two":all(v==2 for v in degrees.values()),
        "euler_characteristic":len(vertices)-len(edges)+len(mesh["triangles"]),
        "mesh_has_topological_slit_or_hole":len(loops)!=1,
        "full_canvas_vertices":vertices,"triangles":mesh["triangles"],"art":str(args.art.resolve()),
        "art_size":art.size,"alpha_threshold":0.70,"diagnostic_raster_scale":24,
        "opaque_art_pixels":total,"opaque_art_outside_carrier":outside,
        "opaque_art_outside_carrier_fraction":outside/max(1,total),
        "lower_quarter_opaque_pixels":hem_total,"lower_quarter_outside_carrier":hem_outside,
        "interpretation":"A single disk carrier has no split notch. Newly painted opaque alpha outside this polygon would still be clipped; alpha holes inside it can be closed without new topology. Raster counts describe this candidate only, not future repainted art.",
    }
    (args.output / "body_carrier_audit.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    preview_scale = 10
    width,height = 60*preview_scale,100*preview_scale
    page = Image.new("RGB",(1380,1130),"#e5e3d9")
    font_path = Path("C:/Windows/Fonts/msyh.ttc")
    font = ImageFont.truetype(str(font_path),19)
    small = ImageFont.truetype(str(font_path),13)
    painter = ImageDraw.Draw(page)
    title = "闭袍后摆扩展：保原40点，新增1点/3面；单一连续承载面" if args.closed_carrier else "原单片衣身承载面：40点 / 43三角 / 单一闭环，无网格开衩孔"
    painter.text((35,18),title,font=font,fill="#303d39")
    for origin, heading in [((35,85),"源承载域（完整60×100画布）"),((735,85),"候选新图与承载边界（红线外会裁切）")]:
        painter.text((origin[0],origin[1]-34),heading,font=font,fill="#303d39")
        painter.rectangle((origin[0],origin[1],origin[0]+width,origin[1]+height),fill="#bdc5bd")
        for x in range(0,61,10): painter.line((origin[0]+x*preview_scale,origin[1],origin[0]+x*preview_scale,origin[1]+height),fill="#9ca99f")
        for y in range(0,101,10): painter.line((origin[0],origin[1]+y*preview_scale,origin[0]+width,origin[1]+y*preview_scale),fill="#9ca99f")
    source_layer = Image.new("RGBA",(width,height),(0,0,0,0))
    source_layer.putalpha(mask.resize((width,height),Image.Resampling.NEAREST).point(lambda a: int(a*.65)))
    fill = Image.new("RGBA",(width,height),(86,123,135,0))
    fill.putalpha(source_layer.getchannel("A"))
    page.paste(fill,(35,85),fill)
    native_preview = art.resize((width,height),Image.Resampling.LANCZOS)
    page.paste(native_preview,(735,85),native_preview)
    painter = ImageDraw.Draw(page)
    for origin in [(35,85),(735,85)]:
        points = [(origin[0]+x*preview_scale,origin[1]+y*preview_scale) for x,y in vertices]
        for a,b in edges: painter.line((points[a],points[b]),fill="#71817b",width=1)
        for a,b in boundary: painter.line((points[a],points[b]),fill="#b53d35",width=3)
        for i,(x,y) in enumerate(points):
            if i<35: painter.text((x+3,y-11),str(i),font=small,fill="#912f29")
    painter.text((35,1095),f"候选 alpha≥70% 越域 {outside/max(1,total):.2%}；下四分之一越域 {hem_outside/max(1,hem_total):.2%}。不是视觉合格判定。",font=font,fill="#303d39")
    page.save(args.output / "body_closed_carrier_audit.png")
    print(json.dumps({"output":str(args.output),"boundary_loops":len(loops),"euler":report["euler_characteristic"],"outside_fraction":report["opaque_art_outside_carrier_fraction"]},ensure_ascii=False))


if __name__ == "__main__":
    main()
