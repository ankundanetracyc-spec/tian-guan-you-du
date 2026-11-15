# 玉虚杂役局内纹理生成记录

2026-11-03。使用内置 image_gen 工具，precise-object-edit 模式；未用脚本重绘、缩放或修改像素。源生成图原样复制到项目。

- 目标布局：tools/action_battle_test/art/yuxu_fullbody_v15/body.png 与 arm_relaxed_v19.png。
- 衣服身份来源：mods/appearance/ink_three_variants_20261004/male/clothing/04.png。
- 输出目录：tools/action_battle_test/art/queue_yuxu_servant_v1。
- 两张均为1024×1536 RGBA；角点透明。
- alpha>25外框：body=(282,322,666,1331)，arm=(368,355,696,622)。原body=(282,320,658,1333)，原arm=(370,354,696,623)。袖臂偏差约2px以内，袍身右缘8px偏宽，保留原UV须以实机网格遮罩验收；不声称生成逐像素相同。
- 头、腿、其他手型由运行时复用认可原素材；本文件不代表独立换发已完成。

## 袍身提示

Use case: precise-object-edit. Production transparent 2D skeletal game texture. Image 1 is the EDIT TARGET and exact UV registration template; image 2 is clothing DESIGN reference only, NOT pose/layout. Edit target cloth into warm grey-beige working robe, unpatterned slate blue crossed collar and belt, ivory inner collar and fine ivory border; a simple narrow slate vertical front trim, restrained small pale cloud accent near lower hem rather than large inner-disciple cloud emblem. Mandatory retain exactly 1024x1536 canvas, exact target object position and scale, exact transparent silhouette and all joint locations. Do not center/resize/repose. Only disconnected sleeveless torso plus continuous full long robe as target, NO head, skin neck, arms, hands, feet. Collar top at y322, waist belt y600-670, hem bottom y1330, entire silhouette x285..655. Torso and skirt must remain ONE continuous garment with belt connecting. Preserve target's clean dark ink outline, broad simple shaded folds, matching painted 2D game style; translate source garment construction and trim/embroidery, not merely hue swap. Transparent background everywhere outside object, no halo, no ground/shadow, no text. Do not copy source halfbody front silhouette. Render at EXACT original 1024 by1536.

## 袖臂提示

Use case: precise-object-edit. Production transparent 2D skeletal game texture. Image 1 is the EDIT TARGET and exact UV registration template; image 2 is clothing DESIGN reference only, NOT pose/layout. Edit target cloth into warm grey-beige sleeve, slate blue cuff, restrained pale ivory and grey-blue cloud embroidery near forearm/cuff, simple working disciple garment, remove old huge blue shoulder swirl. Mandatory retain exactly 1024x1536 canvas, exact target object position and scale, exact transparent silhouette and all joint locations. Do not center/resize/repose. Only this ONE detached bent sleeve and existing hand, shoulder at(403,357), elbow(438,540), wrist(620,460), hand tip(695,444), keep exact shape/pose/skin/palm and all empty canvas unchanged. All artwork stays inside x370..700,y355..625, not a full character. Preserve target's clean dark ink outline, broad simple shaded folds, matching painted 2D game style; translate source garment construction and trim/embroidery, not merely hue swap. Transparent background everywhere outside object, no halo, no ground/shadow, no text. Do not copy source halfbody front silhouette. Render at EXACT original 1024 by1536.

