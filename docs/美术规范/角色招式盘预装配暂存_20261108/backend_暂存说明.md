# 预装配数据分线暂存

2026-11-08。用户最新范围为先实装人物七页和按钮，战斗配招等另一对话验收；本分线已停止并撤回预装配正式接入。以下源稿不参与编译，不代表已经可用。已批准的休息整纸按钮与日期修改保持。

新增服务、Character／BattleEntityData专用partial、定向Smoke和场景移入[source](source)，保留原相对路径，扩展名为.cs.txt／.tscn.txt；三个自动生成的UID也以.txt保存。没有删除或整文件回滚原本存在的源文件。

原Character.cs只摘除本线Capture／Apply两行，BattleEntityData.cs只摘除copy一行；HumanData新增默认和命名库字段、Layout导出名称及其复制行已摘除。BattleManager的默认注入／显式预装配分支／局内载入方法和BattleHud的服务读取钩子已精确恢复；原全局方案文件读取和Geometry校验行为保留。其他对话已有改动保持。

[可复用接入差异](backend_可复用接入差异.patch)仅包含本分线六个原文件的小改和五个新增文件，供未来审阅，当前未应用。由于项目混合行尾，静态可应用检查使用git apply --ignore-space-change --check，已通过；这不是编译或功能通过。实际续接须在战斗稿验收后重新核对当时规则和源码，不能用完整源文件覆盖其他对话改动。

[原接口说明](evidence/backend_original_interface.md)、[文件移动清单](evidence/backend_moved_files.json)和[撤回边界](backend_撤回核对.json)记录了原位置和暂存位置。

本次在独立APPDATA运行了[76项数据断言](evidence/backend_assertions_76.json)，包括真实人类战斗场景、服丹及空瓶回传、局内草稿隔离、原冷却账本、直接存档及DetachedResourceSnapshot后台名称往返；断言均过，但退出阶段原生Array销毁触发System.AccessViolationException，进程-1073741819。完整[退出崩溃日志](evidence/backend_headless_76_exit_crash.log)保留，不能把“76断言”说成整体验收通过。暂存Smoke包含之后补的生命周期清理，未重编译复测。

后续仍需解决并验收：失去玄枢资格后旧locked周天须允许玩家显式移除；实际仍需周天时继续锁定。审计发现BattleBoardView拖出删除也有独立locked判断，不能只改HUD。保存dirty通知需沿现有MainScene链。本轮均停止，未继续加入这些挂钩。

临时比对JSON与已空的旧正式记录目录保持保留。自动审批在启动清理命令前拒绝Remove-Item，唯一返回理由为“blocked by policy”，未给更详细说明；没有继续尝试删除或绕过。准确路径与动作见撤回核对记录，不影响本线正式源码已退出编译。
