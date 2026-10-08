"""Build the female battle puppet's aligned vector layers and Spine 3.8 data.

The SVG sources are original flat silhouettes. Godot rasterizes them into the
full-size atlas pages; no portrait facial features enter the battle rig.
"""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_RIG = ROOT / "textures/characters/c1_spine_ingame_rig_v8/runtime"
PACKAGE = ROOT / "textures/characters/c1_female_shadow_v1"
SOURCE = PACKAGE / "source"
LAYERS = PACKAGE / "layers"
RUNTIME = PACKAGE / "runtime"
STEM = "c1_female_shadow_v1"


def svg(width: int, height: int, contents: str) -> str:
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" '
        f'height="{height}" viewBox="0 0 {width} {height}">'
        f'<g fill="#000000">{contents}</g></svg>'
    )


ART = {
    "body_core": svg(1024, 1536, """
      <path d="M481 384 Q494 373 510 377 L552 389 Q550 446 569 477 Q624 485 675 531
        L655 659 Q636 718 635 782 Q657 870 701 1026 Q592 1081 459 1076
        Q369 1044 350 1004 Q405 846 398 720 L365 544 Q414 490 472 474
        Q493 439 481 384Z"/>
      <path d="M460 473 Q502 498 525 551 Q554 506 572 478 L597 494
        Q563 561 548 622 Q518 582 475 520Z"/>
      <path d="M400 832 Q511 857 630 822 L648 888 Q526 918 386 886Z"/>
      <path d="M489 887 Q513 910 529 896 L554 1119 L515 1162 L481 1100Z"/>
      <path d="M548 891 Q567 908 579 892 L607 1088 L577 1124Z"/>
    """),
    "head_hair": svg(1024, 1536, """
      <path d="M428 156 Q416 115 450 87 Q492 52 541 82 Q585 110 560 162
        Q532 183 482 183 Q448 180 428 156Z"/>
      <path d="M382 272 Q389 192 468 171 Q549 149 603 216 Q628 259 621 317
        Q638 335 646 354 L625 374 Q631 416 609 457 Q592 489 562 499
        Q519 511 481 476 Q447 445 428 393 Q398 362 382 272Z"/>
      <path d="M390 261 Q340 306 348 398 Q351 490 310 570 Q283 628 229 674
        Q272 665 301 636 Q285 710 237 785 Q310 746 353 678 Q366 757 341 825
        Q405 761 423 688 Q447 574 441 457 Q401 383 390 261Z"/>
      <path d="M555 175 Q614 193 637 248 Q652 314 619 391
        Q617 479 657 575 Q677 641 716 685 Q647 671 612 605
        Q567 558 554 487 Q578 405 555 175Z"/>
      <path d="M398 255 Q439 221 494 225 Q543 227 581 258
        Q557 263 538 282 Q498 278 465 306 Q432 293 398 314Z"/>
      <path d="M441 168 Q475 145 524 153 Q565 165 582 185
        Q535 191 494 178 Q465 183 441 168Z"/>
    """),
    "arm_waist": svg(1024, 1536, """
      <path d="M378 510 Q332 502 300 542 Q250 601 223 684
        Q197 777 154 904 Q180 943 240 951 Q299 944 347 898
        Q403 832 424 724 Q425 621 378 510Z"/>
      <path d="M310 547 Q360 579 374 651 Q355 744 287 852
        Q249 904 197 927 Q243 932 291 902 Q380 825 405 711
        Q413 601 359 520Z"/>
    """),
    "arm_raised": svg(1024, 1536, """
      <path d="M651 506 Q705 486 755 522 Q812 570 842 621
        Q880 651 935 682 Q927 740 892 790 Q861 831 813 856
        Q747 827 701 770 Q653 703 633 620 Q629 549 651 506Z"/>
      <path d="M761 531 Q818 526 851 477 Q884 448 916 466
        Q932 481 917 499 Q902 511 875 507 Q858 565 826 596Z"/>
      <path d="M706 724 Q763 796 825 819 Q880 792 912 723
        Q910 787 864 841 Q815 882 775 850Z"/>
    """),
    "lower_body": svg(1024, 1536, """
      <path d="M399 844 Q505 867 638 841 Q650 941 679 1048
        Q710 1164 751 1324 Q700 1376 631 1371 Q592 1339 556 1265
        Q534 1372 500 1406 Q445 1415 407 1364 Q351 1377 312 1335
        Q354 1166 376 1051 Q399 937 399 844Z"/>
      <path d="M374 1315 Q420 1340 460 1344 L452 1458
        Q392 1490 340 1459 Q346 1382 374 1315Z"/>
      <path d="M605 1330 Q653 1350 702 1326 L741 1449
        Q687 1485 625 1455Z"/>
      <path d="M452 1360 Q486 1334 513 1283 L532 1386
        Q509 1415 451 1423Z"/>
    """),
    "lower_kneel": svg(1024, 1536, """<g transform="translate(0 250)">
      <path d="M398 912 Q509 935 634 909 Q663 963 694 1001
        Q741 1043 773 1089 Q763 1139 710 1164 Q619 1187 525 1154
        Q423 1186 319 1143 Q269 1102 292 1064 Q343 1005 398 912Z"/>
      <path d="M346 1130 Q407 1169 491 1154 Q459 1214 385 1215
        Q336 1206 319 1177Z"/>
      <path d="M585 1146 Q660 1163 718 1134 L744 1174
        Q706 1226 634 1205Z"/></g>
    """),
    "lower_empty": svg(1024, 1536, ""),
    "ribbons": svg(1536, 1024, """<g transform="translate(350 0)">
      <path d="M693 281 Q603 316 474 384 Q342 478 180 495
        Q370 531 506 477 Q635 423 740 327Z"/>
      <path d="M730 314 Q618 450 499 624 Q421 739 266 822
        Q444 788 545 690 Q676 546 785 352Z"/>
      <path d="M747 289 Q695 279 667 307 Q678 343 736 354
        Q797 340 804 304 Q784 280 747 289Z"/></g>
    """),
}


def main() -> None:
    for directory in (SOURCE, LAYERS, RUNTIME):
        directory.mkdir(parents=True, exist_ok=True)
    for name, contents in ART.items():
        (SOURCE / f"{name}.svg").write_text(contents, encoding="utf-8")

    previous = json.loads((SOURCE_RIG / "c1_desktop_layers_v8.json").read_text(encoding="utf-8"))
    # Rigid cutout attachments keep sleeves and hair intact. Each pivot is
    # marked on this female artwork rather than borrowing the male mesh grid.
    positions = {
        "root": (0, 0), "hips": (0, -107), "torso": (0, 148),
        "neck": (2, 293), "head": (2, 438),
        "arm_waist": (-145, 222), "arm_waist_forearm": (-229, 43),
        "arm_raised": (145, 218), "arm_raised_forearm": (304, 143),
        "ribbon_base": (-60, 623), "ribbon_mid": (-165, 588),
        "ribbon_tip": (-275, 533), "lower_left": (-55, -227),
        "lower_right": (86, -227),
    }
    bones = previous["bones"]
    for bone in bones:
        if "parent" not in bone:
            continue
        x, y = positions[bone["name"]]
        parent_x, parent_y = positions[bone["parent"]]
        bone["x"], bone["y"] = x - parent_x, y - parent_y

    bindings = {
        "lower_body": "root", "body_core": "torso",
        "lower_kneel": "root", "ribbons": "ribbon_base",
        "arm_waist": "arm_waist", "arm_raised": "arm_raised",
        "head_hair": "head",
    }

    def region(part: str, bone: str) -> dict:
        x, y = positions[bone]
        if part == "ribbons":
            return {"type": "region", "path": part, "x": -212 - x,
                    "y": 568 - y, "width": 553, "height": 369}
        return {"type": "region", "path": part, "x": -x, "y": -y,
                "width": 1024, "height": 1536}

    slots = []
    attachments = {}
    for part, bone in bindings.items():
        slot = {"name": f"slot_{part}", "bone": bone}
        if part != "lower_kneel":
            slot["attachment"] = part
        slots.append(slot)
        attachments[slot["name"]] = {part: region(part, bone)}
    attachments["slot_lower_body"]["lower_empty"] = region("lower_empty", "root")

    animations = previous["animations"]
    death = animations["death_kneel"]
    death["bones"]["root"] = {"translate": [{"x": 0, "y": 0}, {"time": 1.1, "x": 0, "y": 0}]}
    death["bones"]["hips"] = {
        "translate": [{"x": 0, "y": 0}, {"time": 0.3, "x": 0, "y": -100},
                      {"time": 0.72, "x": 0, "y": -220}, {"time": 1.1, "x": 0, "y": -240}],
        "rotate": [{"angle": 0}, {"time": 1.1, "angle": -4}],
    }
    death["bones"]["torso"]["rotate"] = [{"angle": 0}, {"time": 1.1, "angle": -8}]
    death["bones"]["head"]["rotate"] = [{"angle": 0}, {"time": 1.1, "angle": -12}]
    death["bones"]["arm_raised"]["rotate"] = [{"angle": 0}, {"time": 0.55, "angle": -38}, {"time": 1.1, "angle": -65}]
    death["bones"]["arm_waist"]["rotate"] = [{"angle": 0}, {"time": 1.1, "angle": 25}]
    # Delay switching the folded skirt until the body is near its kneeling height.
    death["slots"]["slot_lower_body"]["attachment"][1]["time"] = 0.55
    death["slots"]["slot_lower_kneel"]["attachment"][0]["time"] = 0.55
    rig = {
        "skeleton": {"hash": STEM, "spine": "3.8.99", "x": -512,
                     "y": -768, "width": 1024, "height": 1536, "images": "../layers/"},
        "bones": bones, "slots": slots,
        "skins": [{"name": "default", "attachments": attachments}],
        "animations": animations,
    }
    payload = json.dumps(rig, ensure_ascii=False, separators=(",", ":"))
    for extension in ("json", "spinejson"):
        (RUNTIME / f"{STEM}.{extension}").write_text(payload, encoding="utf-8")
    atlas = (SOURCE_RIG / "c1_desktop_layers_v8.atlas").read_text(encoding="utf-8")
    (RUNTIME / f"{STEM}.atlas").write_text(atlas, encoding="utf-8")


if __name__ == "__main__":
    main()
