# 皮影手工拼装工作台

打开 `res://tscn/scene/PuppetAssemblyWorkbench.tscn`。这是专门给编辑器手工摆放的场景，不进入正式局内流程。

`Puppet` 下每个节点都是一张原始 `1024 x 2048` 的透明画布，均从 `(0, 0)` 放置、缩放 `(1, 1)`、旋转 `0` 开始。请在该节点上调整 `Position`、`Scale`、`Rotation` 和 `Z Index`，不要裁剪原图。

固定层级由后向前：

1. `FarArm_Left`（Z=1）
2. `HairBack`（Z=2）
3. `Head_Male`（Z=3）
4. `Body`（Z=4）
5. `Ornament`（Z=5）
6. `HairFront`（Z=6）
7. `NearArm_Right`（Z=7）

当你摆好后，直接保存场景并告诉我；我会读取这张场景中各节点的变换数据，再反推新版透明素材的统一画布、孔径与锚点。 
