# C1 局内整图 Spine 绑定 v6

这一版直接使用原先的水墨/水彩整身角色作为局内原型：

- 已去掉手中的竹简，并重建为空手姿态；
- 已去掉黑灰、青色和棕色的氛围背景，PNG 保留真实透明 Alpha；
- 保留原角色的比例、站姿、衣服、头发、发饰、飘带和柔和笔触；
- 不再拆身体、头发或五官，整张角色仍使用同一张透明贴图；
- 将整图转换为带权重的 Spine mesh attachment，增加 `hips`、`torso`、`neck`、`head`、双臂/前臂和飘带骨骼；
- 附带 `idle`、`attack`、`hurt`、`death_kneel` 四个具有局部形变的基础动画；死亡动作保持头脚方向，不做整图翻转。

## 文件

- `generated_parts/character_full_watercolor_no_scroll_raw.png`：生成器输出的透明原图；
- `parts/01_character_full.png`：720×960 的局内 Spine 贴图；
- `previews/rest_pose_composite.png`：透明背景静态预览；
- `spine/c1_spine_unified_final.spine`：Spine 3.8.75 工程文件；
- `export/final/c1_spine_unified_import.json`：导入后的 JSON 校验结果。
- `runtime/c1_spine_unified.spinejson`：运行时使用的网格权重资源；与同目录的 `c1_spine_unified.atlas` 配套。

## 60 FPS 说明

这张整图只有一个主要 attachment，贴图尺寸为 720×960，作为局内角色原型在 60 FPS 下是轻量可行的。四个基础动画由 Spine 时间轴实时驱动，运行时按 60 FPS 采样，不需要把同一动作预先烘成 60 张重复图片。实际运行帧率由局内运行时、特效数量、分辨率和目标设备共同决定，因此“角色动画可按 60 FPS 播放”可以保证，不能仅凭单个角色承诺整局始终锁定 60 FPS。

## 当前绑定边界

当前使用同一张整图的网格权重，不裁切原图。头部、躯干、双臂、下摆和飘带拥有独立权重区域，可以在 Spine 中做局部旋转和形变；网格 UV 已按项目运行时的纹理坐标方向校正，保持头在上、脚在下；后续如果要进一步提高袖口、手指和发丝的精度，再细化网格即可。

本包位于 `textures/characters`，是局内原型资源，不替换 `mods/appearance` 下的正式模块化立绘规范。
