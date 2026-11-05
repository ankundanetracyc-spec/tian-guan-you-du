# 玉虚真传局内素材生成记录

2026-11-03。本次使用内置 imagegen 编辑模式；未使用 Python 重绘、裁剪或重着色素材。生成后仍须验收画布尺寸、骨骼锚点与实际蒙皮，不把提示中的精确要求当作生成成功证明。

## 来源与输出

- 真实衣着：`mods/appearance/ink_three_variants_20261004/male/clothing/07.png`，象牙白袍、深蓝领襟与腰带、细金边、山松和云带纹样。
- 身体几何：`tools/action_battle_test/art/yuxu_fullbody_v15/body.png`。
- 袖手几何：`tools/action_battle_test/art/yuxu_fullbody_v15/arm_relaxed_v19.png`。
- 目标目录：`tools/action_battle_test/art/queue_yuxu_true_v1/`。

## Body 提示

Use case precise-object-edit. Edit image1 game skeletal BODY TEXTURE only; image2 is Yuxu true-disciple clothing identity reference. Output exactly 1024x1536 transparent PNG. Absolutely preserve image1 side-facing torso and continuous ankle-length skirt silhouette, pixel position, dimensions, waist anchor, neckline edge and all existing contour: top begins around(425,323), waist band y600-671 x415-562, hem y1328 x285-655. Do not center, enlarge or reframe. No head, neck, arms, hands, legs or shoes. Change garment design within silhouette to authentically match image2: ivory white base, deep slate-blue wide overlapping collar with fine warm antique-gold piping, blue layered belt with narrow gold line, simplified blue-gray ink mountain-and-pine landscape groups on chest and lower skirt with a few restrained flowing gold cloud ribbons. Replace old large solid cloud logo entirely with readable mountain/pine design, not simple recolor. Keep side-view 2D game ink outlined cel-shaded style of image1, broad clear shapes at small gameplay scale; no photographic fabric/no microdetail. Preserve exact original garment pose and continuous closed skirt, no slit. All pixels outside silhouette truly transparent, no black background, no glow/halo/shadow, no text.

## 袖手提示

Use case precise-object-edit. Edit image1 skeletal game sleeve+hand sprite ONLY; image2 clothing identity reference Yuxu true disciple. Output exactly 1024x1536 transparent PNG. LOCK image1 silhouette, position and scale precisely: sleeve starts pointed shoulder around(404,358), upper arm x373-515 y360-560, elbow bottom around(466,620), cuff x579-650 y443-529, unchanged hand x608-694 y407-477. Do NOT center/reframe/enlarge. Most of canvas must remain empty transparent. Keep existing bent elbow pose, shoulder/elbow/wrist pivots exactly; preserve original hand skin contour, proportions and fingers unchanged. Only change sleeve fabric identity from ivory with large solid cloud logo to true-disciple ivory with dark slate-blue cuff, narrow antique-gold edging, blue-gray simplified mountain-and-pine landscape groups and restrained fine gold cloud ribbons as in image2. Pattern must differ from original big solid blue swirl, not merely recolor. Same clean bold ink contours and 2D game cel-shaded broad forms as image1, avoid photo-real fabric or excessive detail. No torso/head/legs/additional hands. Entire outside sleeve/hand must be genuinely transparent; no black matte, glow, halo, shadow or text. Geometry is priority for existing bone mesh compatibility.

## 验收状态

两张已生成并复制至目标目录，均为 1024×1536 RGBA。原始输出保留于 `E:/tmp/CodexData/generated_images/01a1a0d4-ddd9-75c2-a465-d12127d14e93/`：身体 `exec-ed6e2167-93f0-434f-a81c-58f32c154b05.png`，袖手 `exec-7034ed1b-1940-4a6f-99e3-3ea2e00f0091.png`。

仅只读检查 alpha>127 的轮廓：

| 项目 | 原边界（x最小、y最小、x最大、y最大） | 新边界 | 轮廓交并比 |
|---|---|---|---|
| body | 283,321,657,1331 | 281,321,663,1329 | 98.22% |
| arm_relaxed_v19 | 370,355,695,622 | 370,357,694,621 | 97.85% |

整体画布、姿势和锚点位置接近，不能宣称生成实现了绝对逐像素锁定。相对旧体型最大边界差6像素。生成图替换了原大云纹，采用真实衣着的深蓝金边领襟、金线云带与山松组合纹样；未修改原内门资产。

运行时验收：四套同时在420像素高近景显示并播放8秒待机、攻击、受击、架招、闪避，检查 `close_000.png`、`close_026.png`、`close_082.png`、`close_118.png`，未见矩形背景或白晕；肩部、腕口、领口和腰袍连接连续，真传攻击及架招未切回原内门袖纹。另在实际石坪3v3展示四类真实衣服映射，采用当前快步前进/倒步回位，没有飞行。实际截图与动态位于同目录 `battle_*.png` / `battle_motion.gif`，近景为 `close_motion.gif`。近景是独立动作展示，不是实际战场镜头尺寸。保留固定发髻的已知限制，不宣称独立换发完成。
