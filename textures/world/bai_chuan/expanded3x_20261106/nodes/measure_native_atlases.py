"""Measure native atlases and create metadata / diagnostic previews only.
This script never edits any authored source PNG.
"""
import hashlib, json
from collections import Counter
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

BASE = Path(__file__).resolve().parent
PROJECT = BASE.parents[4]
PREFIX = "res://textures/world/bai_chuan/expanded3x_20261106/nodes/"
jobs = json.loads((BASE / "prompts_final.json").read_text("utf-8"))["jobs"]
provenance = json.loads((BASE / "generation_provenance.json").read_text("utf-8"))
source_by_name = {x["file"]: x for x in provenance["atlases"]}
labels = {
    "yuxu": "玉虚仙宗", "xuandu": "玄都天府", "wuxiang": "无相圣宗", "taiwei": "太微道宫",
    "zhenyue": "镇岳观", "baopu": "抱朴山", "baiyao": "百药谷", "danding": "丹鼎宗",
    "guizang": "归藏书院", "cangming": "沧溟水府", "xuehe": "血河教", "shiri": "蚀日宫",
    "kugu": "枯骨寺", "baishi": "白石城", "city": "内陆城", "city_water": "水城",
    "city_fortress": "关城", "town": "集镇", "town_river": "河镇", "town_mountain": "山前镇",
    "market": "工艺坊市", "market_river": "河运坊市", "village": "村落", "dock": "渡口"
}
widths = dict(zip(labels, [
    400,400,400,320,280,260,280,280,280,300,280,280,
    260,300,280,280,280,220,220,220,180,180,150,160
]))
catalog = {
    "version": 1,
    "status": "complete_24_native_sprites",
    "source_mode": "built_in_imagegen",
    "native_alpha_preserved": True,
    "asset_count": 24,
    "authored_atlas_count": 6,
    "nominal_camera_zoom": 0.8,
    "world_side": 22118.4,
    "world_span_km": 8000,
    "avatar_height_world": 52,
    "generation_provenance": PREFIX + "generation_provenance.json",
    "old_source_catalog": "res://textures/world/bai_chuan/node_sprites_20261030/catalog.json",
    "coordinate_conventions": {
        "region": "[x,y,width,height] in native source atlas pixels",
        "foot_px": "[x,y] in native source atlas pixels; building-ground contact",
        "pivot": "foot point normalized within region",
        "world_placement": "Root integrator supplies map X/Y at the foot anchor",
        "z_sort": "Same Y sorting layer as people and landscape; object's Y is its foot Y",
        "source_images": "No resize, recolor, alpha cleanup or source PNG re-encoding",
        "region_measurement": "Visible alpha >8 plus 6 native pixels padding; source PNG retains all original near-invisible alpha dust. full_cell is available for preserving every generated pixel in rendering."
    },
    "atlases": {}, "assets": {}, "location_bindings": [], "wild_locations": []
}
qa = {"source_byte_identity": [], "regions_in_bounds": [], "native_display_sampling": [], "atlas_summaries": []}
images = {}
for job in jobs:
    path = BASE / job["file"]
    data = path.read_bytes()
    original = Path(source_by_name[job["file"]]["tool_source"])
    assert data == original.read_bytes(), "Source copy is not byte identical"
    im = Image.open(path).convert("RGBA")
    images[job["file"]] = im
    w,h = im.size
    assert w % 2 == 0 and h % 2 == 0
    alpha = im.getchannel("A")
    hist = alpha.histogram()
    assert hist[0] > w*h*0.4 and sum(hist[161:]) > 50000
    atlas = {
        "path": PREFIX + job["file"], "native_size": [w,h], "mode": "RGBA",
        "sha256": hashlib.sha256(data).hexdigest(),
        "prompt_file": source_by_name[job["file"]]["prompt_file"],
        "prompt_job_index": source_by_name[job["file"]]["prompt_job_index"],
        "repair_prompt_file": source_by_name[job["file"]]["repair_prompt_file"]
    }
    catalog["atlases"][job["file"]] = atlas
    qa["source_byte_identity"].append(job["file"])
    qa["atlas_summaries"].append({
        "file":job["file"], "size":[w,h], "fully_transparent_pixels":hist[0],
        "fully_opaque_pixels":hist[255], "partial_alpha_pixels":sum(hist[1:255])
    })
    for k, asset_id in enumerate(job["ids"]):
        x,y = k%2*(w//2),k//2*(h//2)
        cw,ch = w//2,h//2
        cell_alpha = alpha.crop((x,y,x+cw,y+ch))
        bbox = cell_alpha.point(lambda v:255 if v>8 else 0).getbbox()
        assert bbox
        b0=max(0,bbox[0]-6); b1=max(0,bbox[1]-6)
        b2=min(cw,bbox[2]+6); b3=min(ch,bbox[3]+6)
        region=[x+b0,y+b1,b2-b0,b3-b1]
        core=cell_alpha.point(lambda v:255 if v>160 else 0)
        cb=core.getbbox()
        assert cb
        # Drop isolated one-pixel translucent flecks from ground measurement only.
        # Architecture / original alpha pixels remain byte-for-byte unchanged.
        min_row=max(4,int((cb[2]-cb[0])*0.008))
        foot_y=cb[3]-1
        for yy in range(cb[3]-1,cb[1]-1,-1):
            if sum(v>160 for v in cell_alpha.crop((0,yy,cw,yy+1)).getdata())>=min_row:
                foot_y=yy
                break
        foot=[x+(cb[0]+cb[2])/2,y+foot_y]
        pivot=[(foot[0]-region[0])/region[2],(foot[1]-region[1])/region[3]]
        world_width=widths[asset_id]
        world_height=world_width*region[3]/region[2]
        screen_width=world_width*catalog["nominal_camera_zoom"]
        assert screen_width<=region[2], (asset_id,screen_width,region[2])
        assert region[0]>=0 and region[1]>=0 and region[0]+region[2]<=w and region[1]+region[3]<=h
        qa["regions_in_bounds"].append(asset_id)
        qa["native_display_sampling"].append({
            "asset":asset_id,"native_region_width":region[2],
            "nominal_world_width":world_width,"screen_width_at_0_8":screen_width,
            "source_pixels_per_screen_pixel":round(region[2]/screen_width,3)
        })
        catalog["assets"][asset_id]={
            "name":labels[asset_id], "atlas":atlas["path"], "region":region,
            "native_size":[region[2],region[3]], "full_cell":[x,y,cw,ch],
            "visible_alpha_bbox":[x+bbox[0],y+bbox[1],bbox[2]-bbox[0],bbox[3]-bbox[1]],
            "foot_px":foot,"pivot":pivot,
            "suggested_world_width":world_width,
            "suggested_world_size":[world_width,round(world_height,3)],
            "sort_anchor":"foot_y",
            "child_z_index":0,
            "native_alpha_unchanged":True
        }

presentation=json.loads((PROJECT/"textures/world/bai_chuan/acceptance_20261030/presentation.json").read_text("utf-8"))
sect_assets={f"bc_place_{i+1:03d}":a for i,a in enumerate([
    "yuxu","xuandu","wuxiang","taiwei","zhenyue","baopu","baiyao","danding",
    "guizang","cangming","xuehe","shiri","kugu"
])}
water_cities={18,19,20,26}
fortress_cities={118}
river_towns={29,31,33,34,119}
mountain_towns={27,28,30,35,36,107,113}
for node in presentation["nodes"]:
    nid=node["id"]; n=int(nid.rsplit("_",1)[1]); typ=node["type"]
    if node.get("asset") is None:
        catalog["wild_locations"].append({"id":nid,"name":node["name"],"asset":None})
        continue
    if typ=="sect": asset=sect_assets[nid]
    elif typ=="city":
        asset="baishi" if n==21 else "city_water" if n in water_cities else "city_fortress" if n in fortress_cities else "city"
    elif typ=="town":
        asset="town_river" if n in river_towns else "town_mountain" if n in mountain_towns else "town"
    elif typ=="market": asset="market_river" if n==120 else "market"
    elif typ=="village": asset="village"
    elif typ=="dock": asset="dock"
    else: raise ValueError(typ)
    assert asset in catalog["assets"]
    catalog["location_bindings"].append({
        "id":nid,"name":node["name"],"type":typ,"asset":asset,
        "world_width":widths[asset],
        "placement_anchor":"foot",
        "x_source":"expanded layout drawPos[0]",
        "y_source":"expanded layout drawPos[1]",
        "z_source":"Y sorting from foot Y"
    })
assert len(catalog["assets"])==24
assert len(catalog["location_bindings"])==53
assert len(catalog["wild_locations"])==70
qa["coverage"]={
    "built_locations":len(catalog["location_bindings"]),
    "wild_locations":len(catalog["wild_locations"]),
    "unique_sect_assets":len(sect_assets),
    "types":dict(Counter(x["type"] for x in catalog["location_bindings"])),
    "all_assets_used":all(any(b["asset"]==a for b in catalog["location_bindings"]) for a in catalog["assets"])
}
assert qa["coverage"]["all_assets_used"]
(BASE/"node_catalog.json").write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding="utf-8")
(BASE/"asset_qa.json").write_text(json.dumps(qa,ensure_ascii=False,indent=2),encoding="utf-8")

# Contact sheet is a diagnostic display composite; it is not an authored source asset.
font_path=Path("C:/Windows/Fonts/msyh.ttc")
font=ImageFont.truetype(str(font_path),21) if font_path.exists() else ImageFont.load_default()
small=ImageFont.truetype(str(font_path),15) if font_path.exists() else ImageFont.load_default()
cell_w,cell_h=380,310
preview=Image.new("RGBA",(cell_w*4,cell_h*6),(209,189,143,255))
draw=ImageDraw.Draw(preview)
for i,(asset_id,a) in enumerate(catalog["assets"].items()):
    ax=(i%4)*cell_w; ay=(i//4)*cell_h
    r=a["region"]; filename=a["atlas"].rsplit("/",1)[1]
    cut=images[filename].crop((r[0],r[1],r[0]+r[2],r[1]+r[3]))
    cut.thumbnail((340,242),Image.Resampling.LANCZOS)
    preview.alpha_composite(cut,(ax+(cell_w-cut.width)//2,ay+250-cut.height))
    draw.text((ax+15,ay+260),a["name"],font=font,fill=(61,52,40,255))
    draw.text((ax+15,ay+288),f'{a["native_size"][0]}x{a["native_size"][1]} native / {a["suggested_world_width"]} world',font=small,fill=(82,69,49,255))
preview.convert("RGB").save(BASE/"contact_sheet_24.jpg",quality=95)
print(json.dumps({
    "catalog":str(BASE/"node_catalog.json"),"preview":str(BASE/"contact_sheet_24.jpg"),
    "coverage":qa["coverage"],"native_sizes":{k:v["native_size"] for k,v in catalog["assets"].items()},
    "min_source_pixels_per_screen_pixel":min(x["source_pixels_per_screen_pixel"] for x in qa["native_display_sampling"])
},ensure_ascii=False,indent=2))

