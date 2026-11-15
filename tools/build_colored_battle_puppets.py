"""Build Spine data and alignment manifests; raster art is never drawn here."""
from pathlib import Path
import argparse
import copy
import json
import struct

ROOT = Path(__file__).resolve().parents[1]

def ready_arm_angles(stem):
    """Local joint rotations: the elbow bends relative to the upper arm."""
    female = stem == "c1_female_puppet_v2"
    return {
        "idle": {"near": (-110, 145, -15) if female else (-104, 140, -10),
                 "far": (75, 135, -15) if female else (85, 120, -20)},
        "ready_cast": {"near": (-85, 145, -30), "far": (75, 140, -35)},
        "ready_sword": {"near": (-78, 145, 23), "far": (85, 120, -20)},
    }

def action_reference_arm_angles(stem):
    # One-shot clips keep their authored reference; Spine mixes to/from the selected ready pose.
    female = stem == "c1_female_puppet_v2"
    return {"near": (-70, 110, 20) if female else (-60, 95, 25),
            "far": (75, 135, -15) if female else (85, 120, -20)}

def set_ready_idle_baseline(animations, stem):
    for side, angles in action_reference_arm_angles(stem).items():
        for bone, target in zip(("arm_", "forearm_", "hand_"), angles):
            track = animations["idle"]["bones"].setdefault(bone + side, {})
            keys = track.setdefault("rotate", [{"time": 0, "angle": 0}])
            old_start = keys[0].get("angle", 0)
            for key in keys:
                key["angle"] = target + key.get("angle", 0) - old_start

def add_ready_pose_animations(animations, stem):
    """Complete hand tracks reset every digit when changing poses/actions."""
    def held_rotation(angle, sway=0):
        return [{"time": t, "angle": round(angle + offset*sway, 4),
                 **({"curve": [.25, 0, .75, 1]} if t < 2.4 else {})}
                for t, offset in ((0, 0), (.6, 1), (1.2, 0), (1.8, -1), (2.4, 0))]

    def hand_angles(side, gesture):
        mirror = 1 if side == "near" else -1
        result = {"palm_" + side: 0}
        for digit in ("thumb", "finger", "middle", "ring", "pinky"):
            curl = (0, 0, 0)
            if gesture == "fist" or (gesture == "sword" and digit in {"ring", "pinky"}):
                curl = (-58, -70, -45)
            elif gesture == "gather":
                curl = (-18, -24, -12)
            if digit == "thumb":
                curl = (28, -25, -12) if gesture != "open" else (12, 0, 0)
            if gesture == "sword" and digit in {"finger", "middle"}:
                curl = (3 if digit == "finger" else -4, 0, 0)
            for suffix, angle in zip(("", "_mid", "_tip"), curl):
                result[f"{digit}_{side}{suffix}"] = angle * mirror
        return result

    guards = ready_arm_angles(stem)
    for name, sides in guards.items():
        if name != "idle":
            animations[name] = copy.deepcopy(animations["idle"])
        tracks = animations[name]["bones"]
        for side, angles in sides.items():
            for prefix, angle in zip(("arm_", "forearm_", "hand_"), angles):
                tracks[prefix + side] = {"rotate": held_rotation(angle, .6 if prefix != "hand_" else .35)}
            gesture = "sword" if name == "ready_sword" and side == "near" else (
                "gather" if name == "ready_cast" else "open" if side == "near" else "fist")
            for bone, angle in hand_angles(side, gesture).items():
                tracks[bone] = {"rotate": held_rotation(angle)}

    for name, animation in animations.items():
        if name in guards:
            continue
        tracks = animation["bones"]
        duration = max(key.get("time", 0) for values in tracks.values()
                       for keys in values.values() for key in keys)
        for side in ("near", "far"):
            gesture = "gather" if name == "cast" else "open" if name == "death_kneel" or side == "near" else "fist"
            for bone, angle in hand_angles(side, gesture).items():
                tracks[bone] = {"rotate": [{"time": 0, "angle": angle}, {"time": duration, "angle": angle}]}
            if name == "death_kneel":
                ending = (-68, 55, -10) if side == "near" else (60, 110, 15)
                for prefix, start, end in zip(("arm_", "forearm_", "hand_"), action_reference_arm_angles(stem)[side], ending):
                    tracks[prefix + side] = {"rotate": [
                        {"time": 0, "angle": start, "curve": [.25, 0, .75, 1]},
                        {"time": round(duration*.55, 4), "angle": start+(end-start)*.6, "curve": [.25, 0, .75, 1]},
                        {"time": duration, "angle": end}]}

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")

def tune_combat_animation_curves(animations):
    """Soften motion relative to idle while keeping the idle and death end poses."""
    idle_tracks = animations.get("idle", {}).get("bones", {})
    slowed_actions = {"cast", "attack", "attack_rising", "attack_leap", "hurt", "death_kneel"}
    amplitude = .5

    for animation_name, animation in animations.items():
        if animation_name not in {"idle", "cast", "attack", "attack_rising", "attack_leap", "hurt", "rest", "breath", "death_kneel"}:
            continue

        for bone_name, timelines in animation.get("bones", {}).items():
            idle_bone = idle_tracks.get(bone_name, {})
            for timeline_name, keys in timelines.items():
                if timeline_name not in {"rotate", "translate"} or not keys:
                    continue
                components = ("angle",) if timeline_name == "rotate" else ("x", "y")
                idle_keys = idle_bone.get(timeline_name, [])
                for component in components:
                    keyed = [(key, float(key[component])) for key in keys if component in key]
                    if not keyed:
                        continue
                    idle_value = float(idle_keys[0].get(component, 0)) if idle_keys else 0.0
                    source_start = keyed[0][1]

                    if animation_name == "death_kneel":
                        source_end = keyed[-1][1]
                        start_time = float(keyed[0][0].get("time", 0))
                        end_time = float(keyed[-1][0].get("time", start_time))
                        duration = max(.0001, end_time - start_time)
                        for key, source_value in keyed:
                            progress = (float(key.get("time", 0)) - start_time) / duration
                            source_path = source_start + (source_end - source_start) * progress
                            target_path = idle_value + (source_end - idle_value) * progress
                            key[component] = round(target_path + (source_value - source_path) * amplitude, 4)
                    else:
                        for key, source_value in keyed:
                            key[component] = round(idle_value + (source_value - source_start) * amplitude, 4)

        if animation_name in slowed_actions:
            for timelines in animation.get("bones", {}).values():
                for keys in timelines.values():
                    for key in keys:
                        if "time" in key:
                            key["time"] = round(float(key["time"]) * 1.25, 4)
            for timelines in animation.get("slots", {}).values():
                for keys in timelines.values():
                    for key in keys:
                        if "time" in key:
                            key["time"] = round(float(key["time"]) * 1.25, 4)

def male_arm_scale(filename):
    # The edited art preserves its layout but arrives on an 887x1774 canvas.
    # Normalize that canvas before applying the previously marked shoulder.
    path = ROOT / "textures/characters/c1_male_puppet_v1/source" / filename
    width, height = struct.unpack(">II", path.read_bytes()[16:24])
    return [.92*1024/width, 1.1*2048/height]

def main(only=None):
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
        if only is not None and stem not in only:
            continue
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
        expanded_rig = stem in {"c1_female_puppet_v2", "c1_male_puppet_v2"}
        if expanded_rig:
            # Preserve the existing art anchors while adding joints for a full
            # combat pose. V1 remains byte-for-byte on its original six-bone rig.
            original_world = dict(world)
            names = ["root", "hips"]
            parents = {"hips": "root"}

            # Match the skeleton-only preview to the shared 320 px combat
            # character scale. Both figures keep comparable head-to-ankle
            # height; the upright female is slightly taller through the chest,
            # while the male's low, broad stance is carried by his limbs.
            proportion = {
                "c1_female_puppet_v2": {
                    # Drop the pelvis in the screen preview while shortening
                    # the leg chains by the same amount. This keeps the head
                    # and foot line fixed and gives the torso more room.
                    "pelvis_drop": 60,
                    "neck_height": 235, "head_height": 385,
                    "shoulders": {
                        "far": (72, 10, 175, 150, 20, 12),
                        "near": (84, 18, 200, 170, 24, 17),
                    },
                    "legs": {
                        "far": ((-26, -25), (-105, -382), (-210, -750), 36),
                        "near": ((26, -25), (40, -390), (90, -770), 52),
                    },
                },
                "c1_male_puppet_v2": {
                    # The male keeps a lower center and wider base than the
                    # female, with a slightly stronger leg reduction.
                    "pelvis_drop": 70,
                    "neck_height": 220, "head_height": 370,
                    "shoulders": {
                        "far": (78, 6, 185, 160, 20, 12),
                        "near": (92, 12, 210, 180, 24, 17),
                    },
                    "legs": {
                        "far": ((-55, -25), (-150, -375), (-260, -740), 38),
                        "near": ((55, -25), (95, -395), (170, -770), 52),
                    },
                },
            }[stem]
            pelvis_drop = proportion["pelvis_drop"]

            def add_bone_at_world(name, parent, point):
                parents[name] = parent
                world[name] = (round(point[0], 4), round(point[1], 4))
                names.append(name)

            hip = original_world["hips"]
            old_torso = original_world["torso"]
            head_anchor = original_world["head"]
            # Keep the shoulder/head landmarks fixed in the preview while
            # moving the pelvis lower: the added length belongs below the
            # torso pivot, and the legs are shortened toward their old feet.
            torso_height = proportion["neck_height"] * .55 + pelvis_drop
            torso_point = (old_torso[0], hip[1] + torso_height)
            lower_abdomen = (hip[0] + (torso_point[0]-hip[0])*.25,
                             hip[1] + (torso_point[1]-hip[1])*.25)
            waist = (hip[0] + (torso_point[0]-hip[0])*.5,
                     hip[1] + (torso_point[1]-hip[1])*.5)
            spine_mid = (hip[0] + (torso_point[0]-hip[0])*.75,
                         hip[1] + (torso_point[1]-hip[1])*.75)
            # Keep chest and neck heights comparable across the two figures;
            # use the source head anchor for the three-quarter side offset.
            neck = (torso_point[0] + (head_anchor[0]-torso_point[0])*.72,
                    hip[1] + proportion["neck_height"] + pelvis_drop)
            head_point = (head_anchor[0], hip[1] + proportion["head_height"] + pelvis_drop)
            ribcage = (torso_point[0] + (neck[0]-torso_point[0])*.34,
                       torso_point[1] + (neck[1]-torso_point[1])*.34)
            chest = (torso_point[0] + (neck[0]-torso_point[0])*.68,
                     torso_point[1] + (neck[1]-torso_point[1])*.68)
            add_bone_at_world("lower_abdomen", "hips", lower_abdomen)
            add_bone_at_world("waist", "lower_abdomen", waist)
            add_bone_at_world("spine_mid", "waist", spine_mid)
            add_bone_at_world("torso", "spine_mid", torso_point)
            add_bone_at_world("ribcage", "torso", ribcage)
            add_bone_at_world("chest", "ribcage", chest)
            add_bone_at_world("neck", "chest", neck)
            add_bone_at_world("head", "neck", head_point)

            for side in ["far", "near"]:
                arm_name = "arm_" + side
                direction = -1 if side == "far" else 1
                span, shoulder_y, upper_length, forearm_length, hand_length, elbow_drop = proportion["shoulders"][side]
                chest_point = world["chest"]
                shoulder = (chest_point[0] + direction*span, chest_point[1]+shoulder_y)
                clavicle = (chest_point[0] + (shoulder[0]-chest_point[0])*.32,
                            chest_point[1] + (shoulder[1]-chest_point[1])*.32)
                scapula = (chest_point[0] + (shoulder[0]-chest_point[0])*.16,
                           chest_point[1] + (shoulder[1]-chest_point[1])*.16)
                add_bone_at_world("scapula_" + side, "chest", scapula)
                add_bone_at_world("clavicle_" + side, "scapula_" + side, clavicle)
                add_bone_at_world(arm_name, "clavicle_" + side, shoulder)
                forearm = (shoulder[0] + direction*upper_length, shoulder[1]-elbow_drop)
                hand = (forearm[0] + direction*forearm_length, forearm[1]-3)
                upper_mid = (shoulder[0]+(forearm[0]-shoulder[0])*.52,
                             shoulder[1]+(forearm[1]-shoulder[1])*.52)
                forearm_mid = (forearm[0]+(hand[0]-forearm[0])*.52,
                               forearm[1]+(hand[1]-forearm[1])*.52)
                palm = (hand[0]+direction*hand_length, hand[1]-1)
                add_bone_at_world("upper_arm_deform_" + side, arm_name, upper_mid)
                add_bone_at_world("forearm_" + side, arm_name, forearm)
                add_bone_at_world("forearm_deform_" + side, "forearm_" + side, forearm_mid)
                add_bone_at_world("hand_" + side, "forearm_" + side, hand)
                add_bone_at_world("palm_" + side, "hand_" + side, palm)
                if side == "near":
                    digit_specs = [
                        ("thumb_near", (7,-8), (12,-8)),
                        ("finger_near", (20,-2), (16,0)),
                        ("middle_near", (18,5), (15,2)),
                        ("ring_near", (14,11), (12,2)),
                        ("pinky_near", (10,16), (9,2)),
                    ]
                else:
                    digit_specs = [
                        ("thumb_far", (-6,-7), (-8,-7)),
                        ("finger_far", (-17,-2), (-14,0)),
                        ("middle_far", (-15,4), (-13,2)),
                        ("ring_far", (-12,9), (-10,2)),
                        ("pinky_far", (-9,13), (-8,2)),
                    ]
                for digit, first, second in digit_specs:
                    first = tuple(value * 1.6 for value in first)
                    second = tuple(value * 1.6 for value in second)
                    base = (hand[0]+first[0], hand[1]+first[1])
                    add_bone_at_world(digit, "palm_" + side, base)
                    middle = (base[0]+second[0]*.55, base[1]+second[1]*.55)
                    add_bone_at_world(digit + "_mid", digit, middle)
                    add_bone_at_world(digit + "_tip", digit + "_mid",
                                      (hand[0]+first[0]+second[0], hand[1]+first[1]+second[1]))

            # Separate the near/far leg lines to match each portrait's stance.
            # The far foot sits slightly higher and closer to the body in depth.
            for side in ["far", "near"]:
                hip_offset, knee_offset, ankle_offset, toe_length = proportion["legs"][side]
                ankle_depth = abs(ankle_offset[1])
                leg_height_scale = (ankle_depth - pelvis_drop) / ankle_depth
                thigh = (hip[0]+hip_offset[0], hip[1]+hip_offset[1]*leg_height_scale)
                shin = (hip[0]+knee_offset[0], hip[1]+knee_offset[1]*leg_height_scale)
                foot = (hip[0]+ankle_offset[0], hip[1]+ankle_offset[1]*leg_height_scale)
                toe = (foot[0]+toe_length, foot[1]-5)
                add_bone_at_world("thigh_"+side, "hips", thigh)
                add_bone_at_world("shin_"+side, "thigh_"+side, shin)
                add_bone_at_world("foot_"+side, "shin_"+side, foot)
                add_bone_at_world("toe_"+side, "foot_"+side, toe)

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
        def eased_rotate(values):
            keys = rotate(values)
            for key in keys[:-1]:
                key["curve"] = [.25, 0, .75, 1]
            return keys
        def eased_translate(values):
            keys = [{"time":t,"x":x,"y":y} for t,x,y in values]
            for key in keys[:-1]:
                key["curve"] = [.25, 0, .75, 1]
            return keys
        kneel_drop = {"c1_female_puppet_v2":110, "c1_male_puppet_v1":293,
                      "c1_male_puppet_v2":110 if expanded_rig else 455}[stem]
        if "body_kneel" in layers:
            hx, hy = world["hips"]
            body_index = next(i for i,s in enumerate(slots) if s["name"] == "slot_body")
            slots.insert(body_index+1, {"name":"slot_kneel","bone":"hips"})
            attachments["slot_kneel"] = {"body_kneel":{"type":"region","path":"body_kneel",
                "x":-hx,"y":-hy+kneel_drop,"width":1024,"height":1536}}
        if expanded_rig:
            animations = {
                "idle":{"bones":{
                    "hips":{"translate":[{"y":0},{"time":.6,"y":2},{"time":1.2,"y":0},{"time":1.8,"y":-1},{"time":2.4,"y":0}]},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.6,-.3),(1.2,0),(1.8,.25),(2.4,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.6,-.7),(1.2,0),(1.8,.5),(2.4,0)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.6,.5),(1.2,0),(1.8,-.4),(2.4,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.6,.9),(1.2,0),(1.8,-.7),(2.4,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.6,.8),(1.2,0),(1.8,-.5),(2.4,0)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.6,1.2),(1.2,0),(1.8,-.8),(2.4,0)])},
                    "neck":{"rotate":eased_rotate([(0,0),(1.2,.4),(2.4,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.6,-.8),(1.2,0),(1.8,.6),(2.4,0)])},
                    "clavicle_near":{"rotate":eased_rotate([(0,0),(.6,2),(1.2,0),(1.8,-1),(2.4,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,86),(.6,88),(1.2,86),(1.8,84),(2.4,86)])},
                    "upper_arm_deform_near":{"rotate":eased_rotate([(0,0),(.6,-3),(1.2,0),(1.8,2),(2.4,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,86),(.6,88),(1.2,86),(1.8,84),(2.4,86)])},
                    "forearm_deform_near":{"rotate":eased_rotate([(0,0),(.6,3),(1.2,0),(1.8,-2),(2.4,0)])},
                    "hand_near":{"rotate":eased_rotate([(0,0),(.6,-3),(1.2,0),(1.8,2),(2.4,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,-86),(.6,-84),(1.2,-86),(1.8,-88),(2.4,-86)])},
                    "upper_arm_deform_far":{"rotate":eased_rotate([(0,0),(.6,2),(1.2,0),(1.8,-2),(2.4,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,-86),(.6,-84),(1.2,-86),(1.8,-88),(2.4,-86)])},
                    "forearm_deform_far":{"rotate":eased_rotate([(0,0),(.6,-3),(1.2,0),(1.8,2),(2.4,0)])},
                    "thigh_near":{"rotate":eased_rotate([(0,0),(1.2,.5),(2.4,0)])},
                    "thigh_far":{"rotate":eased_rotate([(0,0),(1.2,-.5),(2.4,0)])}}},
                "attack":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.18,0,-18),(.36,112,14),(.54,64,72),(.78,7,88),(1.02,0,18),(1.2,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.18,-9),(.36,18),(.54,10),(.78,-5),(1.2,0)])},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.18,-8),(.36,13),(.54,10),(.78,-14),(1.02,-5),(1.2,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.18,-15),(.36,23),(.54,14),(.78,-8),(1.2,0)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.18,-7),(.36,12),(.54,7),(.78,-4),(1.2,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.18,-20),(.36,34),(.54,18),(.78,-12),(1.2,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.18,-12),(.36,23),(.54,14),(.78,-26),(1.02,-10),(1.2,0)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.18,-9),(.36,20),(.54,10),(.78,-7),(1.2,0)])},
                    "neck":{"rotate":eased_rotate([(0,0),(.36,8),(.54,-8),(.78,-3),(1.2,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.2,-10),(.46,12),(.72,-8),(1.2,0)])},
                    "clavicle_near":{"rotate":eased_rotate([(0,0),(.18,-12),(.36,15),(.54,8),(.78,-6),(1.2,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.18,-38),(.36,-82),(.48,55),(.66,25),(.88,-52),(1.2,0)])},
                    "upper_arm_deform_near":{"rotate":eased_rotate([(0,0),(.18,-12),(.36,18),(.48,28),(.66,8),(.88,-12),(1.2,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.18,28),(.36,64),(.48,-42),(.66,-18),(.88,34),(1.2,0)])},
                    "forearm_deform_near":{"rotate":eased_rotate([(0,0),(.36,-10),(.48,18),(.72,-12),(1.2,0)])},
                    "hand_near":{"rotate":eased_rotate([(0,0),(.36,-18),(.48,24),(.72,-20),(1.2,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.18,18),(.36,-12),(.54,-6),(.78,12),(1.2,0)])},
                    "upper_arm_deform_far":{"rotate":eased_rotate([(0,0),(.18,8),(.36,-12),(.54,-8),(.78,8),(1.2,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.36,-22),(.7,16),(1.2,0)])},
                    "thigh_near":{"rotate":eased_rotate([(0,0),(.18,20),(.36,-12),(.54,-18),(.78,12),(1.2,0)])},
                    "shin_near":{"rotate":eased_rotate([(0,0),(.18,-28),(.36,12),(.54,20),(.78,-10),(1.2,0)])},
                    "thigh_far":{"rotate":eased_rotate([(0,0),(.18,-14),(.36,8),(.54,5),(.78,-8),(1.2,0)])},
                    "shin_far":{"rotate":eased_rotate([(0,0),(.36,16),(.72,-15),(1.2,0)])}}},
                "attack_rising":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.2,0,-28),(.42,72,42),(.62,95,122),(.82,52,150),(1.08,4,76),(1.28,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.2,-12),(.42,12),(.62,20),(.82,-8),(1.28,0)])},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.2,-10),(.42,9),(.62,18),(.82,-18),(1.08,-7),(1.28,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.2,-18),(.42,14),(.62,23),(.82,-10),(1.28,0)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.2,-8),(.42,9),(.62,12),(.82,-5),(1.28,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.2,-22),(.42,20),(.62,32),(.82,-14),(1.28,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.2,-12),(.42,15),(.62,25),(.82,-24),(1.08,-8),(1.28,0)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.2,-12),(.42,12),(.62,17),(.82,-8),(1.28,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.2,-9),(.62,16),(.82,-8),(1.28,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.2,-78),(.42,-62),(.62,52),(.82,18),(1.04,-42),(1.28,0)])},
                    "upper_arm_deform_near":{"rotate":eased_rotate([(0,0),(.2,-12),(.42,14),(.62,22),(.82,8),(1.04,-10),(1.28,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.2,46),(.42,38),(.62,-50),(.82,-22),(1.04,28),(1.28,0)])},
                    "hand_near":{"rotate":eased_rotate([(0,0),(.62,18),(.82,-16),(1.28,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.2,22),(.62,-24),(.82,-12),(1.28,0)])},
                    "upper_arm_deform_far":{"rotate":eased_rotate([(0,0),(.2,10),(.62,-16),(.82,-6),(1.28,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.62,26),(.82,12),(1.28,0)])},
                    "thigh_near":{"rotate":eased_rotate([(0,0),(.2,26),(.42,-8),(.62,-22),(.82,10),(1.28,0)])},
                    "shin_near":{"rotate":eased_rotate([(0,0),(.2,-38),(.62,20),(.82,-12),(1.28,0)])},
                    "thigh_far":{"rotate":eased_rotate([(0,0),(.2,-18),(.42,8),(.62,12),(.82,-10),(1.28,0)])},
                    "shin_far":{"rotate":eased_rotate([(0,0),(.62,18),(.82,-16),(1.28,0)])}}},
                "attack_leap":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.18,0,-32),(.38,150,82),(.58,222,150),(.78,142,180),(1.0,24,106),(1.22,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.18,-16),(.38,10),(.58,24),(.78,-12),(1.22,0)])},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.18,-12),(.38,8),(.58,22),(.78,-20),(1.0,-8),(1.22,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.18,-20),(.38,12),(.58,28),(.78,-15),(1.22,0)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.18,-10),(.38,12),(.58,16),(.78,-7),(1.22,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.18,-18),(.38,20),(.58,38),(.78,-18),(1.22,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.18,-10),(.38,16),(.58,28),(.78,-28),(1.0,-10),(1.22,0)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.18,-8),(.38,14),(.58,20),(.78,-10),(1.22,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.38,8),(.58,16),(.78,-10),(1.22,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.18,-62),(.38,-42),(.58,72),(.78,48),(1.0,-50),(1.22,0)])},
                    "upper_arm_deform_near":{"rotate":eased_rotate([(0,0),(.18,-10),(.38,12),(.58,22),(.78,12),(1.0,-12),(1.22,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.18,42),(.38,16),(.58,-34),(.78,-58),(1.0,30),(1.22,0)])},
                    "hand_near":{"rotate":eased_rotate([(0,0),(.58,-22),(.78,18),(1.22,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.18,28),(.58,-32),(.78,-24),(1.22,0)])},
                    "upper_arm_deform_far":{"rotate":eased_rotate([(0,0),(.18,12),(.58,-18),(.78,-10),(1.22,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.58,30),(.78,18),(1.22,0)])},
                    "thigh_near":{"rotate":eased_rotate([(0,0),(.18,32),(.38,-12),(.58,-30),(.78,24),(1.22,0)])},
                    "shin_near":{"rotate":eased_rotate([(0,0),(.18,-48),(.58,34),(.78,-26),(1.22,0)])},
                    "thigh_far":{"rotate":eased_rotate([(0,0),(.18,-24),(.38,16),(.58,22),(.78,-18),(1.22,0)])},
                    "shin_far":{"rotate":eased_rotate([(0,0),(.58,24),(.78,-24),(1.22,0)])}}},
                "hurt":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.08,0,-12),(.2,0,4),(.52,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.1,8),(.3,-2),(.52,0)])},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.1,13),(.3,-4),(.52,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.1,12),(.3,-3),(.52,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.1,18),(.3,-5),(.52,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.1,22),(.3,-7),(.52,0)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.1,14),(.3,-4),(.52,0)])},
                    "neck":{"rotate":eased_rotate([(0,0),(.1,-8),(.3,3),(.52,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.1,13),(.3,-5),(.52,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.1,-22),(.3,8),(.52,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.1,30),(.3,-8),(.52,0)])},
                    "hand_near":{"rotate":eased_rotate([(0,0),(.1,-18),(.3,5),(.52,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.1,20),(.3,-8),(.52,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.1,-22),(.3,6),(.52,0)])},
                    "thigh_near":{"rotate":eased_rotate([(0,0),(.1,-10),(.3,3),(.52,0)])},
                    "shin_near":{"rotate":eased_rotate([(0,0),(.1,18),(.3,-5),(.52,0)])},
                    "thigh_far":{"rotate":eased_rotate([(0,0),(.1,8),(.3,-2),(.52,0)])}}},
                "cast":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.18,0,-3),(.48,0,7),(.76,0,3),(1.08,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.18,-2),(.48,2),(.76,1),(1.08,0)])},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.18,-3),(.48,3),(.76,1),(1.08,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.18,-3),(.48,4),(.76,2),(1.08,0)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.18,-2),(.48,3),(.76,1),(1.08,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.18,-5),(.48,8),(.76,3),(1.08,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.18,-4),(.48,7),(.76,2),(1.08,0)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.18,-3),(.48,5),(.76,2),(1.08,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.18,-3),(.48,5),(.76,3),(1.08,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.18,-8),(.44,-58),(.66,-76),(.88,-32),(1.08,0)])},
                    "upper_arm_deform_far":{"rotate":eased_rotate([(0,0),(.44,-12),(.66,-18),(.88,-10),(1.08,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.44,20),(.66,44),(.88,20),(1.08,0)])},
                    "forearm_deform_far":{"rotate":eased_rotate([(0,0),(.66,-12),(.88,-6),(1.08,0)])},
                    "hand_far":{"rotate":eased_rotate([(0,0),(.66,-18),(.88,-4),(1.08,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.18,5),(.48,12),(.76,4),(1.08,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.48,-16),(.76,-8),(1.08,0)])},
                    "forearm_deform_near":{"rotate":eased_rotate([(0,0),(.48,12),(.76,6),(1.08,0)])},
                    "hand_near":{"rotate":eased_rotate([(0,0),(.66,-6),(.88,-2),(1.08,0)])}}},
                "rest":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.22,0,-7),(.62,0,-8),(.96,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.22,-5),(.62,-4),(.96,0)])},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.22,-3),(.62,-2),(.96,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.22,-6),(.62,-5),(.96,0)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.22,-4),(.62,-3),(.96,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.22,-8),(.62,-6),(.96,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.22,-6),(.62,-4),(.96,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.22,-7),(.62,-5),(.96,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.22,18),(.62,10),(.96,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.22,-34),(.62,-18),(.96,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.22,-18),(.62,-10),(.96,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.22,34),(.62,18),(.96,0)])},
                    "thigh_near":{"rotate":eased_rotate([(0,0),(.22,12),(.62,8),(.96,0)])},
                    "shin_near":{"rotate":eased_rotate([(0,0),(.22,-18),(.62,-12),(.96,0)])},
                    "thigh_far":{"rotate":eased_rotate([(0,0),(.22,-12),(.62,-8),(.96,0)])},
                    "shin_far":{"rotate":eased_rotate([(0,0),(.22,18),(.62,12),(.96,0)])}}},
                "breath":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.24,0,0),(.58,0,7),(.9,0,4),(1.2,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.24,0),(.58,2),(.9,1),(1.2,0)])},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.24,0),(.58,1),(.9,1),(1.2,0)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.24,0),(.58,3),(.9,1),(1.2,0)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.24,0),(.58,4),(.9,2),(1.2,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.24,0),(.58,5),(.9,2),(1.2,0)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.24,0),(.58,7),(.9,2),(1.2,0)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.24,0),(.58,6),(.9,2),(1.2,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.24,0),(.58,-3),(.9,-1),(1.2,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.24,-12),(.58,-24),(.9,-10),(1.2,0)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.24,18),(.58,28),(.9,12),(1.2,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.24,12),(.58,24),(.9,10),(1.2,0)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.24,-18),(.58,-28),(.9,-12),(1.2,0)])}}},
                "death_kneel":{"bones":{
                    "root":{"translate":eased_translate([(0,0,0),(.35,0,-70),(.75,0,-235),(1.1,0,-235)])},
                    "hips":{"translate":[{"y":0},{"time":.35,"y":-42},{"time":1.1,"y":-kneel_drop}]},
                    "lower_abdomen":{"rotate":eased_rotate([(0,0),(.4,-4),(1.1,-3)])},
                    "waist":{"rotate":eased_rotate([(0,0),(.4,-7),(1.1,-8)])},
                    "spine_mid":{"rotate":eased_rotate([(0,0),(.4,-8),(1.1,-10)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.4,-12),(1.1,-14)])},
                    "ribcage":{"rotate":eased_rotate([(0,0),(.4,-10),(1.1,-10)])},
                    "chest":{"rotate":eased_rotate([(0,0),(.4,-7),(1.1,-7)])},
                    "head":{"rotate":eased_rotate([(0,0),(.5,-24),(1.1,-26)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.45,-18),(1.1,-38)])},
                    "forearm_near":{"rotate":eased_rotate([(0,0),(.45,18),(1.1,44)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.45,18),(1.1,32)])},
                    "forearm_far":{"rotate":eased_rotate([(0,0),(.45,-22),(1.1,-40)])},
                    "thigh_near":{"rotate":eased_rotate([(0,0),(.35,35),(.75,90),(1.1,90)])},
                    "shin_near":{"rotate":eased_rotate([(0,0),(.4,-45),(.75,-90),(1.1,-90)])},
                    "foot_near":{"rotate":eased_rotate([(0,0),(.5,10),(.75,0),(1.1,0)])},
                    "thigh_far":{"rotate":eased_rotate([(0,0),(.35,-15),(.75,0),(1.1,0)])},
                    "shin_far":{"rotate":eased_rotate([(0,0),(.4,-40),(.75,-73),(1.1,-73)])},
                    "foot_far":{"rotate":eased_rotate([(0,0),(.5,-30),(.75,-80),(1.1,-99)])}}},
            }
        else:
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
                "cast":{"bones":{
                    "root":{"translate":eased_translate([
                        (0,0,0),(.18,0,-3),(.48,0,7),(.76,0,3),(1.08,0,0)])},
                    "hips":{"rotate":eased_rotate([(0,0),(.18,-2),(.48,2),(.76,1),(1.08,0)])},
                    "torso":{"rotate":eased_rotate([(0,0),(.18,-5),(.48,3),(.76,2),(1.08,0)])},
                    "head":{"rotate":eased_rotate([(0,0),(.18,-3),(.48,5),(.76,3),(1.08,0)])},
                    "arm_near":{"rotate":eased_rotate([(0,0),(.16,-12),(.46,96),(.66,106),(.84,62),(1.08,0)])},
                    "arm_far":{"rotate":eased_rotate([(0,0),(.18,-7),(.48,16),(.76,8),(1.08,0)])}}},
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
        if expanded_rig:
            set_ready_idle_baseline(animations, stem)
            tune_combat_animation_curves(animations)
            add_ready_pose_animations(animations, stem)
        rig = {"skeleton":{"spine":"3.8.99","hash":stem,"x":-512,"y":-768,"width":1024,"height":1536,"images":"../layers/"},
               "bones":bones,"slots":slots,"skins":[{"name":"default","attachments":attachments}],"animations":animations}
        for ext in ["json","spinejson"]:
            write(package/"runtime"/(stem+"."+ext),rig)
        atlas = []
        for part in layers:
            atlas.append(f"../layers/{part}.png\nsize: 1024,1536\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\n{part}\n  rotate: false\n  xy: 0, 0\n  size: 1024, 1536\n  orig: 1024, 1536\n  offset: 0, 0\n  index: -1\n")
        (package/"runtime"/(stem+".atlas")).write_text("\n".join(atlas),encoding="utf-8")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Build the colored battle puppet Spine resources.")
    parser.add_argument("--only", nargs="+", choices=[
        "c1_female_puppet_v2", "c1_male_puppet_v1", "c1_male_puppet_v2"
    ], help="Build only the named character packages.")
    main(parser.parse_args().only)
