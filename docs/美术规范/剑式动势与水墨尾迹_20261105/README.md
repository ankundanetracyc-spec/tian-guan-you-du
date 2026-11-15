# 剑式动势与水墨尾迹

> 2026-11-05用户最新裁定：**剑斩先暂缓，等待动作完成；优先改善现有技能的打击感**。以下为本轮阶段研究与候选记录，水平斩面、主面／尾迹分层和近远遮挡尚未最终视觉验收。旧专项、采集通过数不等于最终画面通过；恢复时须在完成的动作上重新核对剑路、空间位置与层级。当前工作转至[现有即时战斗打击反馈修订](../../plans/战斗与构筑/现有即时战斗打击反馈修订_20261105.md)。

2026-11-05，用户认为[上一轮圆润剑痕](../剑式水墨质感修订_20261105/README.md)仍显僵硬，要求同时学习本机《鬼谷八荒》和《龙胤立志传》。本轮保留圆润轮廓，修订剑尖、前锋、尾迹及身体的时间关系；[四段主案](../../plans/战斗与构筑/持剑普攻剑式四段与特效试作_20261105.md)的费用、伤害、范围、前压、释放点与CD不变。

## 重新核对的本机依据

《龙胤》来源仍为`D:/SteamLibrary/steamapps/common/LongYinLiZhiZhuan/LongYinLiZhiZhuan_Data/resources.assets`。对`墨圆斩/Slash_6`固定整格画布测量，图集自己就会扫动和换形：有效Alpha面积先由2.37%升至14.48%，再逐渐退去；质心持续移动，四个固定位置的主要可见期依次错开，旧笔段先退、前方继续接上。预制体另有旋转、局部平移和错开发射，不能把图集动画、单粒生命周期与整个预制体合并解释。[图集动态测量](../../../output/longyin_sword_vfx_dynamics_20261105/slash6_motion_metrics.png)、[固定画布相位](../../../output/longyin_sword_vfx_dynamics_20261105/slash6_motion_contact_sheet.jpg)、[真实预制体字段](../../../output/longyin_sword_vfx_dynamics_20261105/prefab_motion_summary.json)。这些是只读原素材研究，不是参考游戏窗口录像。

《鬼谷》重新读取`E:/SteamLibrary/steamapps/common/鬼谷八荒/guigubahuang_Data/resources.assets`与`ResourceManager`。`jiangongji/Slashes`的主面约0.054秒尺寸达峰、0.061秒完全显出；同预制体的暗余迹较晚出现，碎点另有生命周期。李四剑斩的后续刀光有0.06秒延迟和反向局部位移，主面与余迹没有共同停住。`AttackJian`包含实体剑纹理与投射配置，不拿飞剑当挥斩参考。[真实材质](../../../output/guigu_sword_dynamic_research_20261105/native_sword_material_contact_sheet.png)、[剑斩时序字段](../../../output/guigu_sword_dynamic_research_20261105/lisi_jianzhan_timing_summary.json)、[分层时序字段](../../../output/guigu_sword_dynamic_research_20261105/jiangongji_timing_summary.json)。

同时重新核对鬼谷`AttackIdle/AttackRun`原动作曲线：近腕快前伸、较慢回收，主副手幅度和速度峰不同。这两段更接近发射／出掌基动作，不冒称源游戏持剑挥砍，也不据此声称源躯干有未证实的滞后。[原动作曲线](../../../output/guigu_sword_dynamic_research_20261105/attack_source_curves.json)。本项目只借鉴快出慢收、主次不同与前后错峰，未把参考游戏纹理或预制对象引入运行素材。

## 上轮僵硬的原因

旧姿态每个插值区间都以smoothstep起落，蓄势点、接触点和段末反复减速到0。墨锋则在显锋完成后固定全部顶点与UV，四段约68%—73%的视觉寿命只作整片淡出；材质接收了年龄却未使用它推动墨流。第四段的剑已经回收，墨锋仍从反方向展开，前后脱节。

独立预览还一直传入Idle与零速度，第二／三段前压只平移模型，缺少既有腿部与衣摆响应。[原生帧时序对照](../../../output/sword_ink_stiffness_diagnosis_20261105/current_timing_contact_sheet.png)、[旧版时序指标](../../../output/sword_ink_stiffness_diagnosis_20261105/timing_metrics.json)保留上述证据。

## 当前实现

- 接触时剑尖穿过瞄准轴，随后沿本段圆弧继续带出余势；扇面段的剑尖角与墨锋前端读取同一视觉曲线，左右和斜向按世界角一致。肩、肘的插值错峰，不再在接触点同时停住；持剑仍读取真实右手挂点，骨长和图稿保持。
- 墨锋从已扫过的半弧起笔，前端快速推进，后方旧笔段持续退出、变薄，并轻微向圆心收墨。位置和方向仍锚定释放时刻，运动发生在原范围内部，不追随人物，也不额外延伸射程。
- UV保留原剑路位置，每个位置分别计算出生及干燥年龄；尾部先断开、前部稍后退去，淡墨层略迟。材质使用本段显式年龄，暂停冻结，不使用独立系统时间。
- 独立预览把真实每帧位移与时间交给已经验过的腿部／衣摆接口，前压会带动身体；精确释放采样只重算姿态，不重复积分衣摆或用微小时间制造巨大速度。共享人物与衣服源码保持。

实现入口：[共用剑尖／墨锋曲线](../../../tools/action_battle_test/sword_sweep_motion.gd)、[四段姿态](../../../tools/action_battle_test/sword_combo_pose.gd)、[扫过与旧迹退出](../../../tools/action_battle_test/sword_combo_vfx.gd)、[局部墨龄材质](../../../tools/action_battle_test/sword_brush_ink.gdshader)、[独立预览](../../../tools/action_battle_test/sword_combo_preview.gd)。

## 验收边界

1. 剑式仍持剑、0体力／0法力、不占灵识；原四段伤害、目标、80°／120°／宽20胶囊／170°保持。
2. 没有增加属性、资源、关键词、射程、位移或状态。真实前压仍6／14单位，攻击层仍在释放时取真实握柄高度。
3. 原时长、40%／45%释放点、续接互斥、中断及整套CD保持；残墨不追加判伤、返费、抽牌、额外行动或资源生成。
4. 规则数据、词典玩家正文和图谱不改，表现记录单独同步；独立演练不冒称正式世界战斗接入，几何采样不冒称NPC承伤。
5. 实际顶点边界、原规则、接触运动、镜像挂点、暂停与尾迹推进由相称专项和实际窗口连续录制验证；结果与图像补于下方。
