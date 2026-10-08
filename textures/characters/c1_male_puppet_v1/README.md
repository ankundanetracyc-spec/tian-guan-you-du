# 男性局内彩色皮影 V1（过渡样机）

已由重新生成的 [男性 V2](../c1_male_puppet_v2/README.md) 替换局内默认加载；本包保留用于追溯旧素材和配准过程。

将项目已有 `mods/appearance/皮影局内男修_v5/` 的彩色皮影头部、前后发、发簪、衣身与双臂重新配准，导出同原点的 1024×1536 附件，接入当前 Spine 动作链路。没有把旧真实立绘染黑。头部缩小，衣身拉长，保留原素材的染色皮纹、朱红与墨黑服装、云纹和接头；接头孔洞和整体材质仍待与女性新样张统一。

`source/body_kneel.png` 为内置 image_gen 新生成的折叠衣裙，生成提示词见 `source/prompts.json`。`alignment.json` 保存关节与配准参数。骨架、图集与同画布附件由 `tools/build_colored_battle_puppets.py`、`tools/bake_colored_battle_puppets.gd` 重建。

每部位一款，头发随头部整体运动，尚未联动局外外观选项。本包是可操作的过渡素材，不能视为最终美术。

长手整改：`source/arm_far_compact.png` 与 `source/arm_near_compact.png` 是内置 image_gen 修小掌指后的分件；生成画布为 887×1774，先在配准中归一到来源的 1024×2048，再围绕肩关节将整个袖臂调为 0.92 倍。原图与修正图没有混用画布坐标。`preview/proportions_before.png` 保存整改前的拼合图，提示词见 `source/hand_proportion_prompt.json`。
