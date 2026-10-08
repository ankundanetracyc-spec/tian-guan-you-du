# 附图剑修 Spine 四动作 v1

来源是 `source/attached_material_sheet.jpg`；`source/assembled_character_reference.jpg` 用于对照造型与比例。棋盘格 JPG 经过局部纹理清理并按部件框拆分。当前包含 Spine 3.8.75 JSON、Atlas 和区域附件切片骨架；打开编辑器验证后再保存为 `.spine` 项目。

## 从《觅长生》本机安装包只读观察到的可借鉴点
- Unity 托管程序集引用 `spine-csharp` 与 `spine-unity`；游戏程序集元数据可见 `PlayerSpine`、`AvatarSkeleton`、`LoadSpine`、`SetSpine`、`AnimationState_Start/Complete` 等名称，适合借鉴“角色组件管理骨架数据、以回调处理动作开始/结束”的组织方式。
- 可见 UI 路径使用 `SkeletonGraphic`；运行时库含 `SkeletonAnimation`。因此本包采用固定骨骼、插槽和动作名，便于游戏角色组件按名字播放。
- 安装包只有编译后的 DLL 与 Unity 资源数据，没有可直接阅读的 C# 源文件；没有提取或复制游戏美术资源，也未读取存档。其状态机和具体动画参数无法由程序集名称确认。

## 文件
- `runtime/附图剑修_spine.json`、`.spinejson` 与 `.atlas`：Spine 3.8.75 骨架数据与图集。
- `layers/`：骨骼插槽使用的透明图层。
- `preview/assembled_idle.png`：棋盘底拼合预览；`assembled_idle_solid.png`：纯色底边缘检查预览。

## 动作
- `idle`：呼吸、头部与发带轻摆，1 秒；播放时设为循环。
- `attack`：前压、右臂挥剑并收势，0.82 秒。
- `hurt`：受击后仰并回稳，0.58 秒。
- `death`：生命值归零后下沉、躯干前倾、手臂与武器垂落并保持，1.05 秒。

## 质量边界
输入素材是压平棋盘格 JPG，自动拆分后头发外沿与少数部件边缘仍有零星压缩残点；头发使用手绘轮廓蒙版。骨架为区域切片，关节变形、攻击挥砍弧线和死亡落地姿势需要在编辑器里校正。当前是可继续编辑的动作草稿，不等同最终动画品质。
    