# 女性局内皮影 V1

**已撤出默认运行链路。** 用户否定本包的全黑剪影方向，当前局内使用彩色皮影 [`c1_female_puppet_v2`](../c1_female_puppet_v2/README.md)。下文只保留旧样机记录，不代表现行美术规范。

本包是女性局内的首版无五官骨骼样机。剪影单独绘制，使用较窄肩腰、发髻与长发、宽袖和裙摆轮廓；局外半身像仍使用自己的组合素材。

- `source/*.svg`：同原点的矢量剪影分件源文件。
- `layers/*.png`：1024×1536 透明附件；飘带页为 1536×1024。
- `runtime/c1_female_shadow_v1.spinejson`：Spine 3.8.99 数据，沿用男性 V8 的骨骼名、插槽名与四段动作时长；女性关节按本包重新标定，使用保持形状的纸片附件，跪地单独设置下沉和裙摆切换。
- `runtime/c1_female_shadow_v1.atlas`：本包 PNG 分页索引。
- `preview/female_shadow_setup.png`：浅底静态拼合预览。
- `preview/female_shadow_static.png`：透明静态回退；不包含骨骼动作。
- `preview/female_shadow_animation_sheet.png`：从项目 Spine 运行器实际取样的四动作预览。

`BattlePaperDoll` 在 `BattleEntityData.gender == 1` 时选择本包。女性缩放为 0.21（男性为 0.24），并单独调整落地位置。开始界面的局内动作展示可点击女修按钮或按 F 选择本包；M 选择男修，A 攻击，H 受击，D 跪地死亡。

重新构建：先执行 `python tools/build_c1_female_shadow_v1.py` 生成源图和骨骼数据，再通过 Godot 执行 `tools/rasterize_female_shadow_v1.gd` 导出 PNG。完整当前规范见 [`模块化角色立绘系统`](../../../docs/美术规范/模块化角色立绘系统.md)。

当前每个分件只有一款。局内发型、服装切换未与局外选择联动。

本轮已通过 C# 编译（0 警告、0 错误），并在项目自带 Spine 运行器中检查男女资源选择、双方朝向、攻击和受击返回待机、死亡保持，以及预览界面的男女按钮切换。四段动作与跪地中间姿势已实际渲染检查。
