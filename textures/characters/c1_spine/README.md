# c1 Spine 分层初版（保留对照）

> 该目录中的素材和工程是重制前的粗裁初版，仅用于对照。当前重制版位于 [`textures/characters/c1_spine_v2/`](../c1_spine_v2/)，主工程为 `spine/c1_spine_v2_final.spine`，包括重新拆分的透明图层、遮挡补绘、重设枢轴和可导入 Spine 工程。

所有 PNG 保持原始 `720x960` 画布和相同锚点，可直接作为 Spine 插槽纹理的对齐底稿。

## 层序

`01_hair_back` → `02_head_face` → `03_hair_front` → `04_body_clothes` → `05_sleeve_right_hand` → `06_sleeve_left_scroll` → `07_waist_accessories` → `08_feet_shoes`

这是基于待机第 1 帧的像素裁切初版。裁切层保留原画透明边缘；被遮挡的背面像素尚未进行 AI 补绘，绑定大幅旋转前需要在 Spine 或绘图软件中补齐。

## Spine 工程初版

- 工程：`spine/c1_spine.spine`（Spine 3.8.75）
- 可重建导入源：`spine/c1_spine_import.json`
- 当前动画名：`idle`、`attack`、`hurt`、`death_kneel`
- 当前只有 `idle` 写入首版关键帧，时长 1 秒；其余三个动画先保留入口，不替换 Godot 现有序列帧播放。
- 当前工程的 Dopesheet 默认是 30 FPS；关键帧按秒对齐，和现有 64 FPS 序列帧保持资源层解耦，后续接入运行时再统一采样策略。

### 骨骼树

```text
root
├─ hips
│  ├─ torso
│  │  ├─ neck
│  │  │  └─ head
│  │  │     ├─ hair_back
│  │  │     └─ hair_front
│  │  ├─ arm_far
│  │  └─ arm_near
│  └─ waist
└─ feet
```

所有插槽仍以整张 720×960 画布对齐；骨骼只负责首版小幅姿态驱动。当前后发、头部、身体服装、袖臂、腰饰和双脚仍是从单帧裁出的完整层，大幅旋转前必须补齐被遮挡区域，并视需要继续拆分前后臂、双脚和衣摆。
