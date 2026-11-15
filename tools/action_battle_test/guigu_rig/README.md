# 鬼谷八荒 10002 原版骨骼与蒙皮导出

这里保存本机《鬼谷八荒》`resources.assets` 中战斗人物 10002 的可重建数据快照和重新导出工具。只供本项目动作战斗测试使用；源文件只读。本目录没有修改试玩主脚本。

## 文件

- `rig.json`：`/10002/Root` 起始的完整 71 节点层级、本地 TRS、根空间 rest matrix、activeSelf/activeInHierarchy、动画根及所用骨骼节点。
- `skins.json`：15 个 SpriteRenderer 的网格，含 9 个真实蒙皮 Sprite；保存顶点、三角形、SpriteBone 顺序、逐顶点权重、原始 bind pose、纹理映射及 renderer 到 rig root 的矩阵。
- `animations.json`：Idle、Run_New1、Run_New2、AttackIdle、AttackRun、Step1 六段 AnimationClip。除解码后的帧/绑定外，也保存源 StreamedClip 的 uint32 原始流、DenseClip 和 ConstantClip 数据。
- `decoded_animation_curves.json`：既有本机解码结果的独立快照，供导出器复现已解析的系数帧；每个 clip 的原始 Unity 流仍同时保存在 `animations.json`。
- `export_check.json`：导出时的结构、权重及动画路径检查结果。
- `validation.json`：JSON 快照、SpriteSkin 引用、权重、UV 和旧挂点坐标复核结果。
- `export_guigu_rig.py`：从本机 Unity 资源重新生成 JSON。
- `validate_guigu_rig_export.py`：只读取导出 JSON 和已有 layout，做结构、权重、索引、UV 及挂点坐标检查。

## 坐标和蒙皮约定

- 导出根为 `/10002/Root`。外层 GameObject 181892 的位置 `(10100, 10096, 9990)` 和 scale `(5, 5, 1)` 是关卡中的实例放置变换，记录在 `rig.json.characterInstance`，不混入可复用角色骨架。
- hierarchy 的 `localTRS.position`、网格顶点均为 Unity 原始单位：X 向右、Y 向上、Z 为深度；没有对顶点做 Y 轴翻转。角色 Sprite 的 PPU 为 100。
- 矩阵使用嵌套四行数组，顺序为 Unity `e00..e33`；向量按列向量乘，平移放在最后一列。`rendererToRigRoot` 已合成 SpriteRenderer 所在节点到 `/10002/Root` 的完整父链。
- 每个 skinned mesh 的 `vertices` 是源 Sprite local 坐标。`boneWeights[].boneIndex` 同时指向该 mesh 的 `skinning.spriteBones[index]` 和 `bindposeMatrices[index]`；`spriteBones[].rigPath` 将此索引对应到稳定的 hierarchy 路径。权重保持源值，未重归一化。9 个 skinned renderer 的权重和检查范围为 `0.99999994` 至 `1.00000012`。
- 无 SpriteBone 的脸、阴影和装饰件是原资源中的非蒙皮网格；`boneWeights` 为空，实际位置由 `rendererToRigRoot` 和父层级决定。`rawVertexInfluences` 保留 Unity 通用顶点布局中的原始通道值，不能把它们当成这些非蒙皮件的有效骨骼绑定。
- 当前 SpriteRenderData 顶点 UV 通道本身是零值，Unity 资源另存 `uvTransform` 和 `textureRect`。因此导出器按 `uvTransform` 从顶点恢复源纹理坐标，再用 `textureRect` 映射到裁切 PNG 的 `uvCropLowerLeft`。两组 UV 均为左下原点；Godot 使用时做 `v = 1 - v`。结果在 0..1 边界附近（含少量浮点误差）。
- 旧 `art/guigu_reference/layout.json` 中 position 是根空间、按 100 PPU 转成像素并翻到 Y-down 的平铺坐标，不是局部位移。导出检查已逐项验证 8 个原布局挂点与层级矩阵结果一致。

## 动画数据

所有 AnimationClip 绑定路径相对于 `/10002/Root/Body` 下的 Animator 解析；这个 Animator 在原资源中 active。`/10002/Root/Foot2` 的第二个 Animator 所在父节点 activeSelf=false，虽然仍保留在完整层级与数据里，但它的双腿 renderer 不属于默认显示。实际默认可见腿是 `/10002/Root/Body/Foot/zuotui/zuotui` 和 `/10002/Root/Body/Foot/youtui/youtui`，对应源 layout.json 的两个腿锚点。`tracks[].samples` 保留源帧内出现的稀疏组件值和四项系数，`decodedStreamFrames` 保留帧时间及完整键数组；`rawStreamedClip.dataUInt32` 可用于精确复解 Unity 流。当前已解析帧来自本目录的 `decoded_animation_curves.json` 快照，其 SHA-256 记录在 `animations.json.decodedCurveSource`。动画曲线的状态机、混合树切换时序和事件没有并入本次切片。

9 个 skinned renderer 的 MonoBehaviour 均确认是 `UnityEngine.U2D.Animation.SpriteSkin`。其序列化 typetree 在该资源中缺失，导出器直接解析了 raw payload 中的 `rootBone` 和 `boneTransforms[]` PPtr，并保留整段原始十六进制：8 个组件的数组逐索引吻合 SpriteBone；头发组件只序列化 `rootBone`，数组为空，因此按 SpriteBone 父子关系记录骨骼路径，且保留这个差异。权重、bind pose 和 SpriteBone 来自 SpriteRenderData；SpriteBone 路径与 SpriteSkin 引用的 Transform 路径已交叉验证。

## 重导出和检查

在本机安装 UnityPy 的 Python 环境下运行：

```powershell
python tools/action_battle_test/guigu_rig/export_guigu_rig.py
python tools/action_battle_test/guigu_rig/validate_guigu_rig_export.py
```

导出时会校验资源版本、层级 ID、权重索引/总和、SpriteBone 到 Transform 的映射，以及 AnimationClip 路径。输入资源 SHA-256 写在每份 JSON 的 `source` 中，当前值为 `0d1e14ae4a2719b91055e201fadf294b600f730e5bfbba14f4806b7619ec3ced`。
