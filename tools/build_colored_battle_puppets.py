"""Build Spine data and alignment manifests; raster art is never drawn here."""
from pathlib import Path
import json
import struct

ROOT = Path(__file__).resolve().parents[1]

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")

def male_arm_scale(filename):
    # The edited art preserves its layout but arrives on an 887x1774 canvas.
    # Normalize that canvas before applying the previously marked shoulder.
    path = ROOT / "textures/characters/c1_male_puppet_v1/source" / filename
    width, height = struct.unpack(">II", path.read_bytes()[16:24])
    return [.92*1024/width, 1.1*2048/height]

def main():
    old = "res://mods/appearance/皮影局内男修_v5/"
    female = "res://textures/characters/c1_female_puppet_v2/source/"
    male = "res://textures/characters/c1_male_puppet_v2/source/"
    packages = [
        ("c1_female_puppet_v2", {
            "head": [[female+"head.png", [.6,.6], [220,14]]],
            "arm_far": [[female+"arm_far.png", [.5,.49], [112,402]]],
            "body": [[female+"body.png", [.94,.912], [29,149]]],
            "arm_near": [[female+"arm_near.png", [.62,.68], [346,222]]],
            "body_kneel": [[female+"body_kneel.png", [1,1], [15,55]]],
        }, {"hips": [638,592], "torso": [620,475], "head": [585,375],
             "arm_far": [488,435], "arm_near": [680,450]}, 900),
        ("c1_male_puppet_v1", {
            "head": [[old+p, [.6,.6], [225,-121]] for p in
                     ["hair/hair_back.png", "head/male_bald.png", "ornament/cloud_hairpin.png", "hair/hair_front.png"]],
            "arm_far": [["res://textures/characters/c1_male_puppet_v1/source/arm_far_compact.png", male_arm_scale("arm_far_compact.png"), [60,-570]]],
            "body": [[old+"body/vermilion_body.png", [.92,1.1], [60,-570]]],
            "arm_near": [["res://textures/characters/c1_male_puppet_v1/source/arm_near_compact.png", male_arm_scale("arm_near_compact.png"), [60,-570]]],
            "body_kneel": [["res://textures/characters/c1_male_puppet_v1/source/body_kneel.png", [1,1.05], [0,-30]]],
        }, {"hips": [531,706], "torso": [531,550], "head": [532,423],
             "arm_far": [410,515], "arm_near": [662,515]}, 1000),
        ("c1_male_puppet_v2", {
            "head": [[male+"head.png", [.4,.4], [310,0]]],
            "arm_far": [[male+"arm_far.png", [.68,.68], [68,137]]],
            "body": [[male+"body.png", [.86,.91], [80,160]]],
            "arm_near": [[male+"arm_near.png", [.65,.65], [334,175]]],
            "body_kneel": [[male+"body_kneel.png", [.86,.91], [80,190]]],
        }, {"hips": [635,600], "torso": [600,450], "head": [595,320],
             "arm_far": [440,385], "arm_near": [705,430]}, 900),
    ]
    for stem, layers, anchors, fold_start in packages:
        layer_order = (["head", "arm_far", "arm_near", "body"]
                       if stem == "c1_male_puppet_v2"
                       else ["head", "arm_far", "body", "arm_near"])
        # Shorten the complete sleeve-arm about its marked shoulder, keeping
        # that attachment's joint aligned in every animation and pose.
        arm_scale = {"c1_female_puppet_v2":.84, "c1_male_puppet_v1":.92,
                     "c1_male_puppet_v2":.90}[stem]
        for part in ["arm_far", "arm_near"]:
            pivot_x, pivot_y = anchors[part]
            for source in layers[part]:
                scale, position = source[1], source[2]
                source[1] = [round(v*arm_scale,6) for v in scale]
                source[2] = [round(pivot_x+(position[0]-pivot_x)*arm_scale,6),
                             round(pivot_y+(position[1]-pivot_y)*arm_scale,6)]
        package = ROOT / "textures/characters" / stem
        for d in ["runtime", "layers", "preview"]:
            (package/d).mkdir(parents=True, exist_ok=True)
        write(package / "alignment.json", {"canvas": [1024,1536], "layers": layers,
             "anchors_px": anchors, "arm_scale_about_shoulder": arm_scale,
             "order": layer_order})
        world = {"root": (0,0)} | {k: (x-512,768-y) for k,(x,y) in anchors.items()}
        parents = {"hips":"root", "torso":"hips", "head":"torso", "arm_far":"torso", "arm_near":"torso"}
        names = ["root", "hips", "torso", "head", "arm_far", "arm_near"]
        bones = [{"name":"root"}]
        for name in names[1:]:
            parent = parents[name]
            bones.append({"name":name, "parent":parent,
                          "x":world[name][0]-world[parent][0], "y":world[name][1]-world[parent][1]})
        slots, attachments = [], {}
        for part in layer_order:
            bone = "root" if part == "body" else part
            slot = "slot_"+part
            slots.append({"name":slot,"bone":bone,"attachment":part})
            x,y = world[bone]
            attachment = {"type":"region", "path":part, "x":-x, "y":-y, "width":1024,"height":1536}
            if part == "body":
                # The upper robe follows the torso; the shoes remain grounded.
                # Skirt weights transition smoothly through hips to root.
                cols, rows = 8, 24
                uvs, vertices, triangles = [], [], []
                for row in range(rows+1):
                    py = row*1536/rows
                    for col in range(cols+1):
                        px = col*1024/cols
                        uvs.extend([col/cols,row/rows])
                        if py < anchors["hips"][1]:
                            blend = max(0,min(1,(py-anchors["hips"][1]+160)/160))
                            weights = [("torso",1-blend),("hips",blend)]
                        else:
                            blend = max(0,min(1,(py-fold_start)/(1450-fold_start)))
                            weights = [("hips",1-blend),("root",blend)]
                        weights = [(b,w) for b,w in weights if w>.00001]
                        vertices.append(len(weights))
                        for b,w in weights:
                            bx,by = world[b]
                            vertices.extend([names.index(b),px-512-bx,768-py-by,w])
                for row in range(rows):
                    for col in range(cols):
                        a = row*(cols+1)+col
                        triangles.extend([a,a+cols+1,a+1,a+1,a+cols+1,a+cols+2])
                attachment = {"type":"mesh","path":part,"uvs":uvs,"triangles":triangles,
                              "vertices":vertices,"width":1024,"height":1536}
            attachments[slot] = {part:attachment}
        def rotate(values):
            return [{"time":t,"angle":a} for t,a in values]
        kneel_drop = {"c1_female_puppet_v2":340, "c1_male_puppet_v1":293,
                      "c1_male_puppet_v2":455}[stem]
        if "body_kneel" in layers:
            hx, hy = world["hips"]
            body_index = next(i for i,s in enumerate(slots) if s["name"] == "slot_body")
            slots.insert(body_index+1, {"name":"slot_kneel","bone":"hips"})
            attachments["slot_kneel"] = {"body_kneel":{"type":"region","path":"body_kneel",
                "x":-hx,"y":-hy+kneel_drop,"width":1024,"height":1536}}
        animations = {
            "idle":{"bones":{
                "hips":{"translate":[{"y":0},{"time":1.3,"y":3},{"time":2.6,"y":0}]},
                "head":{"rotate":rotate([(0,0),(1.3,-1),(2.6,0)])},
                "arm_near":{"rotate":rotate([(0,0),(1.3,1.4),(2.6,0)])},
                "arm_far":{"rotate":rotate([(0,0),(1.3,-1.2),(2.6,0)])}}},
            "attack":{"bones":{
                "torso":{"rotate":rotate([(0,0),(.25,-4),(.4,-6),(.75,0)])},
                "head":{"rotate":rotate([(0,0),(.4,3),(.75,0)])},
                "arm_near":{"rotate":rotate([(0,0),(.15,-10),(.4,68),(.75,0)])},
                "arm_far":{"rotate":rotate([(0,0),(.4,-12),(.75,0)])}}},
            "hurt":{"bones":{
                "torso":{"rotate":rotate([(0,0),(.1,10),(.55,0)])},
                "head":{"rotate":rotate([(0,0),(.1,7),(.55,0)])},
                "arm_near":{"rotate":rotate([(0,0),(.1,-12),(.55,0)])}}},
            "death_kneel":{"bones":{
                "hips":{"translate":[{"y":0},{"time":.4,"y":-100},{"time":1.1,"y":-kneel_drop}]},
                "torso":{"rotate":rotate([(0,0),(1.1,0)])},
                "head":{"rotate":rotate([(0,0),(1.1,-24)])},
                "arm_near":{"rotate":rotate([(0,0),(1.1,-14)])},
                "arm_far":{"rotate":rotate([(0,0),(1.1,12)])}}},
        }
        if "body_kneel" in layers:
            animations["death_kneel"]["slots"] = {
                "slot_body":{"attachment":[{"name":"body"},{"time":.65,"name":None}]},
                "slot_kneel":{"attachment":[{"name":None},{"time":.65,"name":"body_kneel"}]},
            }
        rig = {"skeleton":{"spine":"3.8.99","hash":stem,"x":-512,"y":-768,"width":1024,"height":1536,"images":"../layers/"},
               "bones":bones,"slots":slots,"skins":[{"name":"default","attachments":attachments}],"animations":animations}
        for ext in ["json","spinejson"]:
            write(package/"runtime"/(stem+"."+ext),rig)
        atlas = []
        for part in layers:
            atlas.append(f"../layers/{part}.png\nsize: 1024,1536\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\n{part}\n  rotate: false\n  xy: 0, 0\n  size: 1024, 1536\n  orig: 1024, 1536\n  offset: 0, 0\n  index: -1\n")
        (package/"runtime"/(stem+".atlas")).write_text("\n".join(atlas),encoding="utf-8")

if __name__ == "__main__":
    main()
