# 女性局内彩色皮影 V2（待视觉验收）

本轮纠正全黑剪影：青绿与米白染色皮面、少量朱红点缀、刻纹和深色切边，面部留白。女性保持较窄肩腰，运行占屏小于男修。

长手整改：两侧袖臂按肩关节作 0.84 倍等比调整；肩部位置固定，袖口、手掌和指尖随整个附件向肩部收回。`preview/proportions_before.png` 保存本次整改前的拼合图。

- `source/`：内置 image_gen 生成的完整样张、分件原图和折叠衣裙。原图存在生成时的缩放与位移，不能直接同原点叠加。
- `source/prompts.json`：完整生成提示词与拆件提示词；采用内置工具，没有 CLI/API 调用。
- `alignment.json`：实测后采用的画布配准、图层顺序与骨骼关节。
- `layers/`：Godot 按配准导出的同原点 1024×1536 真透明附件。
- `runtime/`：Spine 3.8 数据与分页图集，四段动作可在开始界面查看。
- `preview/setup.png`：真实分件拼合；`preview/ingame_preview.png`：实际游戏预览界面；`preview/animation_sheet.png`：Spine 四动作取样。

重建先运行 `tools/build_colored_battle_puppets.py` 生成配准清单与骨骼数据，再用项目自带 Godot 执行 `tools/bake_colored_battle_puppets.gd` 导出附件，并导入刷新纹理缓存。生成原图的透明像素可能带有棕色 RGB，但 Alpha 为零；浅色拼合与游戏截图均无背景光晕。

当前头部与头发合并为一个运动附件，后发/飘带尚未独立摆动；局内换发、换衣没有联动局外选择。跪地后段使用独立绘制的折叠衣裙，切换过渡仍是初版。美术接缝、整体简化程度和动作均待用户验收。
