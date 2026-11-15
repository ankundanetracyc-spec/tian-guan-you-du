# 男修 Spine 可动样片 v1

独立测试包，2026-10-15。正式战斗继续使用静态 V2。

- [制作规范、动作组成与验收边界](../../../docs/美术规范/男修Spine素材样片_20261015.md)
- [生成提示与来源](../../../docs/美术规范/男修Spine素材样片_20261015_生成提示.md)
- [原生预览启动器](../../../tools/run_male_spine_sample_20261015_v1.cmd)
- [拼合母稿](source/master_1024x1536.png)
- [实机动作对照](preview/动作实机对照.png) / [实机动作 GIF](preview/动作实机预览.gif)
- [最终验证](preview/verification.json)

70 骨（原 64 骨名与父子关系保留＋6 根发／袍控制骨），23 槽，26 附件，12 动作。`layers/` 是同原点 1024×1536 RGBA，`images/` 为 JSON 导入所用裁边贴图。`alignment.json` 保存源图选区、变换、锚点与顺序。

`build.cjs` 重建本包；`preview.gd` 用真实 Spine 节点预览。启动器后加 `-- --sample-audit --sample-motion` 可重新导出检查与连续帧，再运行 `review.cjs` 打包 GIF、对照图和验证汇总。Node 需要 sharp；使用项目会话的捆绑 Node 环境。

桌面锁屏下真实窗口输入未验；运行器退出纹理回收警告未解决，详见正式记录。本包不改正式角色引用，不含剑，不含女修。
