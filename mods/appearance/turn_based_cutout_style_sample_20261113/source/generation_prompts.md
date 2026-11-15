# 生成提示与来源

日期：2026-11-13。工具：内置 ImageGen。此次为项目审阅样稿，不替换现行素材。

## 参考角色全身图

Image 1：`mods/appearance/附图剑修_Spine四动作_v3/preview/assembled_jianfinger_v3.png`，只作项目人物造型参考，保留白青袍、束发、长飘带和剑指姿势。

Image 2：从本机《废墟图书馆》`char_571.pres` 中提取的罗兰常态拼接预览 `Roland_Default_assembled.png`，只作紧凑比例与浓墨线条参考；不复制罗兰的服装、武器或脸部设计。

### Prompt 1

Use case: stylized-concept.
Asset type: full-body transparent battle-sprite master for the current xianxia project.
Input images: Image 1 is the project's existing white-and-teal male sword cultivator; preserve his identity cues, long dark hair in a bun with teal ribbon, white/teal long robe, calm side-profile facing right, and raised two-finger sword gesture. Image 2 is a local Library of Ruina assembled combat sprite; use only as a guide for compact chibi proportions and bold hand-inked line treatment. Do not copy its suit, weapon, colors, face, or exact design.
Primary request: make one original in-game male cultivator sprite in a compact, roughly three-head-tall chibi silhouette, ready to be separated into animation parts.
Style/medium: hand-inked 2D game sprite, confident thick charcoal-black outlines with slight dry-brush irregularity, simplified cel-shaded forms and restrained painterly texture; preserve the project's white, deep teal, and charcoal palette.
Composition: single complete standing figure centered on a genuinely transparent background; head clearly larger than the original, short torso and limbs, full feet visible, three-quarter side view facing right. Keep the two-finger sword gesture raised; no physical sword.
Constraints: retain the source cultivator's calm expression and recognizable robe construction, broad teal waist sash, wide sleeves, long split robe hem, dark trousers and teal foot wraps. Keep all silhouette edges clean and closed for future part separation. No background, no scene, no text, no labels, no watermark, no extra people, no added weapon, no angry expression, no missing hands or feet.

## 分件图集

Image 1：Prompt 1 生成的项目人物全身图 `preview/assembled_master.png`，作为唯一角色设计参考。

### Prompt 2

Use case: stylized-concept.
Asset type: transparent component atlas for the project's in-game cultivator sprite.
Input image: Image 1 is the exact character-design reference. Keep the same male cultivator, compact proportions, dark tied hair and teal ribbon, white/deep-teal robe, charcoal trousers, calm right-facing expression, two-finger sword gesture, black hand-inked outline, and restrained painted shading.
Primary request: make one clean 3-column by 4-row atlas of twelve separate cutout sprites for reconstructing this character. Each cell contains exactly one isolated part, with generous transparent gutters; no full assembled figure.
Cell order, left to right then top to bottom:
1 rear hair and bun, excluding front fringe and ribbon;
2 bare head and ears, no facial features or hair;
3 only the facial features (eyebrows, eyes, nose, mouth) for the same calm expression;
4 front hair;
5 teal headband and both ribbon tails;
6 upper robe torso with collar and waist sash knot;
7 left wide sleeve only;
8 right wide sleeve only;
9 left forearm and raised two-finger sword hand, no sleeve;
10 right forearm and relaxed hand, no sleeve;
11 lower robe panels and trailing sash ends;
12 trousers, lower legs, teal wraps and shoes.
Composition: exact evenly spaced cells on a transparent 3:4 portrait canvas, each part large, fully visible and uncropped, similar scale to its role on the reference character. No panel outlines or labels.
Constraints: preserve one coherent design and matching ink/shading across all parts; draw complete cutout silhouettes with clean transparent edges, including hidden overlap tabs where a part must tuck beneath another. Facial features stay separate from the head base. Sleeves stay separate from skin arms. No background or floor, no text, no symbols, no watermark, no extra parts, no duplicates, no weapon.

## 分件图集整理

Image 1：Prompt 2 的图集；Image 2：全身概念图。用于保留分件顺序、修整格间留白。

### Prompt 3

Use case: stylized-concept.
Asset type: cleaned transparent component atlas for the same in-game cultivator sprite.
Input image 1 is the atlas to refine; Image 2 is the exact full-body character master for design reference.
Primary request: preserve the existing 3-column by 4-row atlas and the same twelve part assignments in row-major order, but recompose the sheet so every part is completely isolated inside its own cell. Each cell is a 362-by-362 square with at least 28 pixels of fully transparent margin on all four sides. Scale parts down as needed; do not let any strand, ribbon, sleeve, or arm touch or cross a cell edge.
Cell order: rear hair and bun; bare head base; separate face features; front hair; teal headband and both ribbon tails; upper robe torso; left sleeve; right sleeve; raised two-finger arm; relaxed right arm; complete lower robe with all sash tails; trousers, wraps and shoes.
Constraints: remove all stray fragments from neighboring parts, repair any clipped edges, and keep each part fully visible. Preserve the exact character design, pose, colors, bold charcoal ink outline, and painted texture from the references. Maintain transparent background. No grid lines, boxes, labels, text, background, watermark, duplicate pieces, or extra objects.

注：生成图集仍是供审阅的分件草稿；项目接入前须重新检查单件边缘、锚点和重拼效果。
