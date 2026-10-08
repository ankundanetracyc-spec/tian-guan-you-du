# c1 Spine 分层重制版

这是 `c1_spine` 的重制版素材，使用 `textures/characters/c1/001.png` 重新建立透明图层和 Spine 绑定；旧版 `textures/characters/c1_spine/` 保留为对照，不再作为本次重制的素材来源。

## 素材校验

- 画布：全部为 `720x960`、真实 Alpha、同一原点。
- 重建脚本：`tools/rebuild_c1_spine_layers_v2.py`。
- 静态合成：`preview/rest_pose_composite.png`。
- 静态差异：`preview/rest_pose_diff.png`；用于检查拆层边缘和 Alpha 差异。
- 分层检查：`preview/layers_contact_sheet.png`。
- 动作检查：`preview/idle_mid.png`、`preview/attack_mid.png`、`preview/hurt_hit.png`、`preview/death_kneel_end.png`。
- 身体底层与头颈底层包含局部遮挡补绘，动作预览不再依赖透明黑洞兜底。
- 当前只清理原画中的孤立边框像素，不改动局外立绘和 Godot 现有序列帧。

## 图层顺序

`01_hair_back → 02_feet_shoes → 03_body_clothes → 04_sleeve_scroll → 05_sleeve_pointing → 06_waist_accessories → 07_head_face → 08_hair_front`

身体、双袖、腰饰、头脸和前后发已分离；身体层不再把整张角色图当作占位图，双袖和脚也不再复用身体区域。

## Spine 工程

- 工程：`spine/c1_spine_v2_final.spine`（Spine 3.8.75）。
- 可重建导入源：`spine/c1_spine_v2_import.json`。
- 导出校验：`export/final/c1_spine_v2.json`。
- 动画：`idle`、`attack`、`hurt`、`death_kneel`。
- Dopesheet：30 FPS；动画时间按秒记录，未接管现有 64 FPS 序列帧播放。

### 骨骼树

```text
root
├─ hips
│  ├─ torso
│  │  ├─ neck
│  │  │  └─ head
│  │  │     ├─ hair_back
│  │  │     └─ hair_front
│  │  ├─ arm_scroll
│  │  └─ arm_pointing
│  └─ waist
└─ feet
```

这版转轴按角色结构重新放置：躯干、头部、持卷轴手、指向手、腰饰、双脚各有独立枢轴；四个动画都有可导入的关键帧入口，其中 `idle`、`attack`、`hurt`、`death_kneel` 均已写入首版动作数据。

## 当前边界

这是一套基于单张待机原画的可绑定首版，不替换已有局内序列帧，也没有把未出现的背面绘制凭空加入角色。若要支持更大幅度的翻身、蹲伏或侧身攻击，下一轮应继续将衣摆、双臂和脚拆成更细的前后层，并针对转动露出的背面进行补绘。
