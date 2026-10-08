# 男性局内彩色皮影 V2（待验收样稿）

以女性 V2 的彩色皮影材质为参考，使用内置 `image_gen` 重新生成男修整图与分件。蓝灰、米白与少量朱红；脸部无眉眼鼻口，去掉旧稿的孔洞接头，重做头颈、肩部与紧凑掌指。当前已接入 `BattlePaperDoll` 和开始界面的局内皮影动作预览。

## 素材与配准

- `source/complete_reference.png`：完整设计参考。
- `source/{head,body,arm_far,arm_near,body_kneel}.png`：内置生成的透明分件原图，均为 1024×1536。
- [生成提示词](source/prompts.json)：完整样张与五个分件的最终提示词；采用内置工具模式，未使用 CLI/API 回退。
- [配准记录](alignment.json)：画布、各原图缩放/位置、肩部/颈根/腰部关节和绘制顺序。
- `layers/`：按同原点导出的 1024×1536 真透明附件。袖臂围绕肩关节调为 0.90 倍，两侧袖臂在衣身后，肩部由衣服覆盖。
- `runtime/`：Spine 3.8 骨架、图集及四段动作；由 `tools/build_colored_battle_puppets.py` 与 `tools/bake_colored_battle_puppets.gd` 重建。

## 游戏预览

- [动作入口截图](preview/ingame_preview.png)：实际 `CharacterSequenceShowcase` 场景的男修画面。
- [战斗尺寸](preview/battle_size_preview.png)：实际 `BattlePaperDoll` 男女角色，包含对手朝向翻转。
- [四种动作](preview/animation_sheet.png)：待机、攻击、受击和跪地的骨骼渲染。
- [静态拼合](preview/setup.png)：用于配准查看。
- `preview/previous_male_v1.png`：上一版留档。

跪地使用独立绘制的折叠衣裙附件，上身、头部和袖臂由骨架下沉；后段替换衣身，避免将正常长袍挤压成最终跪地形状。

当前每部位一款，头部与整套头发合并，袖臂为整体纸片，尚无独立肘关节和发丝摆动，也未联动局外捏脸、换发、换衣。跪地衣裙切换过渡仍是初版。本包已可在游戏中查看，不能视为已通过最终美术验收。

本轮 C# 编译通过（0 警告、0 错误）；使用项目自带 Spine 运行器渲染上述截图。渲染进程仍报告项目已有的空路径 autoload 与退出时纹理泄漏诊断，本轮未改动这些配置。
