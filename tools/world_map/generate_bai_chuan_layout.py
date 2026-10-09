"""Template-based, seeded scene authoring. Never invoked by the running game."""
from __future__ import annotations
import argparse, json, re, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT/'resources/world/bai_chuan/map_layout_templates.json'
CATALOG = ROOT/'resources/world/bai_chuan/natural_asset_catalog.json'
SCENE = ROOT/'tscn/world/bai_chuan/dual_grid_v1/BaiChuanDualGridTerrainPrototype_v1.tscn'
OUTPUT = ROOT/'resources/world/bai_chuan/generated_nature_layout.json'

def read_json(path): return json.loads(path.read_text(encoding='utf-8-sig'))

def generate(seed: int, density: float = 1.0):
    from bai_chuan_region_distribution import compose
    anchors = read_json(ROOT/'resources/world/bai_chuan/bai_chuan_grid_anchors.json')['anchors']
    return compose(seed, density, read_json(CONFIG), read_json(CATALOG), anchors, SCENE.read_text(encoding='utf-8'))

def apply_to_scene(layout):
    text=SCENE.read_text(encoding='utf-8');parts=re.split(r'(?=^\[node )',text,flags=re.M)
    backup=ROOT/'tmp/guigu_map_study_20261009/BaiChuan_before_template_random.tscn'
    if not backup.exists():shutil.copy2(SCENE,backup)
    kept=[];landmarks=[]
    for block in parts[1:]:
        head=block.split('\n',1)[0]
        if block.startswith('[node name="LandmarkProps"') or 'parent="NatureDecorations/LandmarkProps' in head:landmarks.append(block);continue
        if block.startswith('[node name="NatureDecorations"') or 'parent="NatureDecorations' in head:continue
        kept.append(block)
    header=re.sub(r'^\[ext_resource[^\n]*id="(?:natural_\d+|template_art_\d+|layout_editor)"[^\n]*\n','',parts[0],flags=re.M)
    paths=sorted({r['asset'] for r in layout['records']});ids={p:f'template_art_{i:03d}' for i,p in enumerate(paths)}
    header+='[ext_resource type="Script" path="res://scripts/world/bai_chuan/BaiChuanTemplateLayoutEditor.gd" id="layout_editor"]\n'
    header+=''.join(f'[ext_resource type="Texture2D" path="{p}" id="{ids[p]}"]\n' for p in paths)+'\n'
    nodes=[f'[node name="NatureDecorations" type="Node2D" parent="."]\nscript = ExtResource("layout_editor")\nlayout_seed = {layout["seed"]}\nlayout_density = {layout["density"]}\nmetadata/placement_mode = "seeded_templates_baked_into_scene"\nmetadata/layout_seed = {layout["seed"]}\nmetadata/water_logic_cells = 0\n\n']
    made=set()
    def ensure(path):
        pieces=path.split('/')
        for n in range(1,len(pieces)+1):
            sub='/'.join(pieces[:n])
            if sub in made:continue
            parent='NatureDecorations'+('/'+'/'.join(pieces[:n-1]) if n>1 else '')
            nodes.append(f'[node name="{pieces[n-1]}" type="Node2D" parent="{parent}"]\nz_index = {3 if n==1 else 0}\ny_sort_enabled = true\n\n');made.add(sub)
    for group in ['MountainRanges','ForestPatches','SmallScenery']:ensure(group)
    for i,r in enumerate(layout['records']):
        ensure(r['parent']);x,y,w,h=r['footprint'];px,py=r['position'];ox,oy=r['offset'];scale=r['scale']
        node=f'[node name="Scenery_{i:05d}" type="Sprite2D" parent="NatureDecorations/{r["parent"]}"]\ntexture_filter = 2\nposition = Vector2({px}, {py})\nscale = Vector2({scale:.8f}, {scale:.8f})\noffset = Vector2({ox}, {oy})\ntexture = ExtResource("{ids[r["asset"]]}")\nmetadata/asset_family = "{r["family"]}"\nmetadata/cell_footprint = Rect2i({x}, {y}, {w}, {h})\n'
        if r['role']=='tree':node+=f'metadata/map_cell = Vector2i({x}, {y})\nmetadata/tree_count = 1\nmetadata/upright = true\n'
        if r['role']=='mountain':node+=f'metadata/footprint_dimensions = Vector2i({w}, {h})\n'
        node+='metadata/generated_from_template = true\n\n';nodes.append(node)
    SCENE.write_text(header+''.join(kept)+''.join(nodes)+''.join(landmarks),encoding='utf-8',newline='\n')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--seed',type=int,default=20261009)
    parser.add_argument('--density',type=float,default=1.0)
    parser.add_argument('--output',type=Path,default=OUTPUT)
    parser.add_argument('--apply',action='store_true',help='Bake editable sprites into the current scene')
    args=parser.parse_args()
    if not .3<=args.density<=2:parser.error('density must be between 0.3 and 2')
    layout=generate(args.seed,args.density)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(layout,ensure_ascii=False,indent=2),encoding='utf-8')
    if args.apply:apply_to_scene(layout)
    print(json.dumps({k:v for k,v in layout.items() if k!='records'},ensure_ascii=False))

if __name__=='__main__':main()
