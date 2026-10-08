# 卡牌水墨意象图标

方向：人物适度抽象，保留可辨认的动作和有张力的意象。弱化五官、发型和服饰细节，以浓淡墨块、飞白、留白和少量朱砂表达力量。

工具：内置 imagegen。

## 交付与验证

- 12 张最终图：`textures/cards/expressive_ink/*_ink_v3.png`，已经更新对应卡牌的 `icon_path`。
- 完整提示词、生成源路径与卡牌映射：同目录 `manifest.json`。
- 实际 Godot 卡牌渲染总览：同目录 `cards-preview.png`。预览使用无持有者的基础数值。
- 卡面绘图区按说明区高度完整适配图像，避免方图放大裁切。
- 项目编译：0 错误、0 警告；现有卡牌回归：370 项通过。
- 运行日志仍提示项目既有的资源 UID 回退；图标已正常加载。

## 应敌试稿提示词

Create a square ink-wash game card artwork for 应敌, a basic unarmed attack in a Chinese cultivation game. The aesthetic is expressive figurative abstraction, dramatic tension and evocative imagery, NOT a detailed character illustration and NOT arbitrary brush marks. One anonymous martial human silhouette lunges diagonally into a decisive punch. Small faceless head bowed, a compact hunched shoulder mass, one unmistakable clenched fist as the main focal shape, long grounded rear leg and bent leading leg forming a taut triangular stance. The figure is built from only 8-12 bold black ink masses and dry-brush strokes, with enough negative-space separations to read arm, fist, torso and legs. Anatomically plausible but intentionally simplified and slightly exaggerated for force. No eyes, face, hair details, garment seams, decorative costume or portrait. Fist and arm are solid legible silhouettes; the trailing lower robe dissolves into textured flying-white dry-brush. Asymmetric dynamic composition, figure fills central 75 percent with breathing room, force driven toward upper right. Restrained pale gray ink wash counterweight behind the figure, a single tiny vermilion brush accent at the striking wrist. Warm off-white rice paper, predominantly monochrome sumi-e, sophisticated raw calligraphic energy, strong white negative space, handmade traditional Chinese martial ink painting. No letters, no seal, no borders, no circular halo, no speed-line comic graphics, no photorealism, no anime face. At small size it should evoke 'a person driving a punch' before any detail is seen. One image only.

## 整套构图规划

- 应敌：前倾拳势，斜向发力。
- 痛击：近景重拳，压迫性的透视。
- 架招：交臂护身，紧实的交叉结构。
- 养息：松弛盘坐，淡墨呼吸。
- 静观：隐约侧面，目光与留白对峙。
- 发劲（资源文件仍名“拳脚”）：短距发拳，腰胯向拳端传力。
- 撼山：拳落裂岩，重量与碎裂。
- 卸力：借掌导开来拳，弧线与直线相交。
- 催血：躯干墨影，胸腹朱砂气血向四肢舒展。
- 站桩：低沉马步，稳固如根。
- 避锋：侧身避开剑锋，危险的窄隙。
- 蓄势重击：拧腰收拳，压缩的爆发前一刻。
