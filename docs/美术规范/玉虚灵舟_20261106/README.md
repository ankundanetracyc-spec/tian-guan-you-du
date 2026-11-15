# 玉虚灵舟素材候选

2026-11-06，当前大世界用分层舟身、前栏、真实地图人物和水墨云尾，替代下方有顶整图候选。白石至玉虚的飞行采用独立高度和留在地面的影子；接引舟与持有器物后的玩家驾舟均使用分层素材及尾流。玩家的舟类取得与炼制尚未开放，个人飞行以隔离夹具验证。参考本机《鬼谷八荒》灵舟的船体／云／影及人物定位结构，只读研究其资源；没有将其素材复制到项目。

## 当前分层与核验

- [舟体与远栏](../../../textures/world/bai_chuan/vehicles/yuxu_boat_layered_20261106/hull_back.png)：透明 PNG，2172×724，中央留出人物站立空间。
- [近侧前栏](../../../textures/world/bai_chuan/vehicles/yuxu_boat_layered_20261106/rail_front.png)：透明 PNG，同画布；运行时取中央横栏，与舟身共用尺度，遮住人物脚部但不遮住人物。
- [云尾](../../../textures/world/bai_chuan/vehicles/spirit_boat_wake_20261106/ink_cloud_wisp.png)：透明 PNG；连续飞行时从尾部生成，随航向转动，停下后消散，旧尾迹不随转向翻转。
- 船上复用现有玩家 `WorldActionTraveler` 和 NPC `WorldNpcTraveler` 地图形象。层级为舟身、人物、近栏；独立影子留在地面。高度是行旅状态中独立保存的投影值，并非已建好的真实地形高度模型。
- [白石赴玉虚实机](白石赴玉虚实机.png) · [实机局部](白石赴玉虚局部.png) · [独立地图预览](分层飞行原景_向右.png) · [预览局部放大](分层飞行局部_4倍.png) · [反方向](分层飞行原景_向左.png) · [个人飞行夹具](个人灵舟_飞行验证.png) · [六人满载验证](个人灵舟_满载六人验证.png)。接引、个人驾舟和独立预览均使用同一分层节点；个人夹具图中的同行者来自真实同行状态，满载图包含玩家与五名同行者。

源码：[舟与人物组合](../../../gdscripts/world/WorldSpiritBoatVisual.gd)、[尾流](../../../gdscripts/world/SpiritBoatWake.gd)、[尾流场景](../../../tscn/world/bai_chuan/SpiritBoatWake.tscn)、[地图预览](../../../tools/world_map/SpiritBoatMapPreview.tscn)。尾流专项验证覆盖运动、停航、暂停、缩放、转向与高度。既有人物图集仍有直接读取图像的导出提示；此处验收为项目运行环境，未完成导出包验收。

## 行囊中的收起形态

[灵舟行囊图标](../../../textures/items/inventory_density_20261029/spirit_boat_icon_20261106.png)为 724×2172 透明 PNG，描绘原木舟收起后的缩小形态，与大世界的展开形态对应。设计上有须弥收放的意味，但不声称乘员可被收纳。[行囊夹具截图](个人灵舟_行囊验证.png)中的 2×6 占格只供当前验证，不代表最终空间占地。

图标使用内置 imagegen，以当前分层舟体、现有剑形行囊图标和既有水墨物品风格为参考生成，完整提示词：

> Create one original inventory item icon for a Chinese cultivation RPG. The first reference is the ACTUAL world-map boat whose identity must remain recognizable: long open warm-brown wooden hull, curved raised ends and restrained faded teal inlay, no roof, sail or characters. The second reference is the existing weapon inventory icon's isolated-object treatment. The third reference is the established inventory art style: soft painted watercolor/ink, muted natural colors, delicate texture, subdued volume and diffuse light. Depict this same vessel as a palm-sized miniature magical artifact lying flat and viewed from almost directly above, oriented vertically with the prow toward the top, full silhouette centered and comfortably within frame. Design a clean compact boat-shaped object with a visible open deck and low rail, harmonious dark wood, desaturated celadon details, restrained fine ink edge. It should remain legible at inventory-slot size. This is an icon, not a scene: no figures, clouds, shadow outside the object, ground, pedestal, aura, white outline, frame, lettering, numbers or UI. Output a single genuinely transparent PNG with soft alpha edges, no solid background.

### 当前素材生成提示词

舟身：

> Use case: stylized-concept. Asset type: back and hull layer of a 2D game world-map vehicle, isolated transparent PNG. Draw one original Chinese cultivation-world flying boat in an almost side-on, slightly elevated game camera view, traveling to the right, matching small existing humanoid game sprites. Make a simple compact wooden boat with a long OPEN unobstructed deck for several standing characters; the entire center of the deck must be clear empty air, with NO cabin, roof, large sail, wall, central mast, characters or people. The curved hull occupies only the lower part; a very low far-side railing and short decorative upturned prow and stern are allowed. The near-side foreground railing is a separate layer to be added later, so do not draw any railing across the front edge. Muted warm wood, charcoal ink lines and restrained pale teal details; handmade Chinese ink-and-watercolor game art with simplified shapes readable at 120 pixels width, not intricate illustration or realism. Boat fully in frame with transparent empty space above and around it. True alpha background, no text, logo, UI, cloud, detached effects, ground, shadow, or watermark. This is one back/hull layer, NOT a scene and no human silhouettes.

近栏，以舟身为参考：

> Use the provided boat as a precise style and perspective reference, NOT as an image to keep. Produce ONLY a separate low, narrow foreground near-side wooden ship rail, isolated on actual transparent background, to be composited over characters' feet on that boat. The rail should run left-to-right as one continuous long shallow upward-curving warm-brown beam, with a few short slim posts and subtle pale-teal trim, matching the reference's hand-painted Chinese ink-and-watercolor outline and camera angle. Its width should be much greater than its height, the central beam low and nearly horizontal, the two ends gently rising. No boat hull, deck, mast, walls, cabins, sky, characters, sea, text, logo or clouds; all surrounding pixels must be transparent. One simple foreground railing only, centered with transparent margin. This must read as a thin unobtrusive near edge, not a fence that hides figures.

云尾：

> A single reusable VFX sprite for a hand-painted Chinese ink-wash cultivation game world map. A graceful horizontal wisp of airborne cloud and wind, 3:1 wide composition, palest warm ivory and subtle muted celadon-grey, delicate dry-brush edges and layered semi-transparent vapor. The compact brighter end is on the right, trailing gradually into separated fine, airy, irregular wisps toward the left. The sprite will be drawn about 75 to 120 pixels wide behind a small wooden flying spirit boat. No boat, no people, no shadow, no ground, no words, no icons, no frame, no harsh glow, no fire, no sparks. Isolated effect with genuinely transparent background, natural soft transparent edges, readable at small scale.

## 历史：第一版有顶概念

首版透明 PNG 为1774×887。因中央屋顶遮挡人物，不作为当前地图舟素材，仅保留概念记录。

素材：[玉虚灵舟 v1](../../../textures/world/bai_chuan/vehicles/yuxu_spirit_boat_20261106_v1.png)。

## 生成提示词

> Use case: stylized-concept. Asset type: a standalone transparent-background game sprite for a Chinese xianxia world map. Create ONE complete, elegant Yuxu immortal sect spirit boat, a flying vessel capable of carrying a sect disciple and passengers, viewed from a consistent elevated three-quarter side angle suitable for a 2D overhead map. Ancient Chinese wooden boat silhouette, modest open deck with a small covered cabin, restrained jade-green and ink detailing, graceful bow and stern, designed so it still reads clearly at small map scale. Painterly hand-drawn Chinese ink-and-wash game art, muted moss green, desaturated blue-grey, warm natural wood and fine dark ink lines; fit into an antique yellow-green landscape map without a square frame. Full vessel entirely visible with comfortable transparent margins, centered; clean actual alpha transparency outside the silhouette. No people, no scenery, no ground, no sea, no clouds or ambient shadow outside the vessel, no text, no interface, no emblem, no watermark. This is an asset concept for review, not a full scene.
