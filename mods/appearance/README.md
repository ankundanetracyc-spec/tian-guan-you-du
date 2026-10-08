# 外观 MOD

一个外观包对应一个子目录，并包含 `appearance_parts.json` 与它引用的透明 PNG。

当前正式素材以 `resources/appearance/half_body_portrait_rig.json` 为唯一位置规范：所有部件均为 `1024 × 1536` 的透明半身画布。绘制顺序固定为后发、头部（含耳朵和延长颈部）、姿势与服饰、五官、前发。`clothes` 是“身体姿势＋服装”绑定层，不得独立混搭身体；JSON 的 `id` 是捏脸选项的稳定标识；`gender` 为 `male` 或 `female`；`part` 使用 `clothes`、`face`、`hair_back`、`eye`、`nose`、`mouth`、`beard`、`hair_front`。
