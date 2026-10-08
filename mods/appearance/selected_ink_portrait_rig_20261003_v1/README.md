# 用户选定立绘的锚点包

母版就是用户本次上传的男女组合。单人参考与附件浅底像素完全一致；当前新发型修稿不是本包的造型来源。

- `reference/`：原附件、两张真透明单人母版和原光头补充图。
- `rig/portrait_rig.json`：男、女各 40 个点位与 7 个导入辅助点；不同性别分别为两个兼容族。
- `preview/*_anchor_guide.png`：在这套母版上标注点位的说明图。
- `rig/*_overlay.png`：游戏里可显隐的同原点锚点叠图。
- `register_variant.py`：整层等比配准工具；需要源图对应控制点的绑定记录。
- `source_verification.json`、`registration_verification.json`：来源及配准验证。

此包是冻结母版与定位规范。当前新拆件在[九部位清理包](../selected_ink_portrait_clean_20261003_v1/README.md)，游戏测试已读取该包并显示本项目锚点；清理候选的视觉认可仍独立记录，不直接替换正式角色或存档。四个发梢参考点已重新测量，固定拼接控制架不变。

正式规则见 `docs/美术规范/本轮男女立绘锚点规范_20261003.md`。
