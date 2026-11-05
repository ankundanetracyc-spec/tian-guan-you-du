# 男性原图完整还原（2026-11-05）

当前状态：用户再次全部否定前后发与五官样例，review_02整批弃用。现改为先生成一张完整男性立绘统一整体观感，再逐步制作部件。完整新稿01已生成，待用户审阅；本轮未拆件、未接入、活动素材与配置保持。


## 最新：先完整整体，再逐步制作部件（待审阅）

用户明确要求“丑，全部去掉。你先生成整体效果吧，然后再慢慢生成部件”。此前三套前后发和新五官样例整批废止，`new_hair_pairs_review_20261105/review_02/asset_manifest.json`与`source/selected_assets.json`均标为用户拒绝、禁止接入。历史文件只作来源，不是可继续选装的候选。

新制作目录：`mods/appearance/portrait_original_restore_20261105_v6/whole_male_first_20261105/`。`male_whole_01.png`为内置image_gen一次完整绘制的1024×1536整张稿，原生结果未修改。以当前女性实际完整组合`reference/female_active.png`对齐绘画质感、暖黑灰发色和细柔发丝，以原男性三联图参考自然男性五官、长发与青绿道袍审美。没有继续用前轮部件拼成新预览。

本轮只交付整体稿：五官、发型、皮肤、头颈与衣领在同一张画里一起确定。提示与来源见`source/whole_male_prompt.txt`、`source/whole_review_manifest.json`，女性实际组合清单在`reference/female_reference_manifest.json`。该稿未拆部件、未接入；用户认可后，再以同一张认可稿逐步制作。头颈共同画布、锚点、百会前后归属和鼻山根在眉眼下的要求保留为后续分件约束，不把本轮提示坐标当作已实测通过的锚点。

本轮核对当前活动51张纹理和配置均未改变。此前七套衣领接入记录是历史工程状态，不表示本次整张新稿已经实装或通过用户视觉评价。

## 已弃用：新前后发与五官样例（用户整批否定）

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/new_hair_pairs_review_20261105/`。最终审阅包为 `review_02/`，01疏朗半束、02清劲高束、03松风低束；每套对应独立前发与后发，四个五官样例由三套共用。眉、眼、鼻含山根、嘴均由内置image_gen新绘，无旧男性五官像素。首次浓睫毛、厚唇和嘴周色块稿被用户否定，已换成当前较清淡的新稿。

`review_02/asset_manifest.json`冻结各编号的10张PNG、预览及哈希。用户随后整批否定，该清单现标为弃用、禁止接入，不再作为可选择候选。新发型和五官未加入活动包；旧稿仅保留工程历史。

新发型采用女性已装素材作发色与笔触参照，前发亮度中位约76、后发约68，压住高光。后盖在y250—400各检查行均窄于对应前发；前发蓬松稿过大已舍弃。所有素材使用1024×1536共同画布；头颈原PNG与配置未改。五官按源图实测瞳孔中心、鼻尖和唇线作整件配准，鼻子与山根先于眼、眉叠加。

样例图：`review_02/three_new_pairs_light.png`、`paired_layers.png`、`new_features_closeup.png`、`four_feature_samples.png`。生成提示分别保存在 `source/generation_prompts.json` 和 `features/source/generation_prompts.json`，全部使用内置image_gen；清边、发色统一和配准记录见 `source/selected_assets.json`。

## 历史工程记录：固定头颈重做七套领口（已接入）

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/collar_hair_fixed_neck_20261105/`。七套前衣与后领分别重新生成，并按冻结颈部与各套交领V点局部配准。最终使用连续的新绘衣服，避免新旧领线交叉渐混产生的重影；衣服设计配色沿用原款，不能宣称旧衣身逐像素不变。早期局部拼片稿及六发修订稿均未接入。

实际接入仅14张衣服层，头、颈、五官、六发及女性共37张纹理保持，配置保持。`collars_only/installation/runtime_check.json`共1250项检查通过，男女各54种组合，最终日志无ERROR／WARNING。备份配置已改为.before文件，避免被外观目录扫描为额外MOD。该检查只证明衣领接入和现有组合，不代替新发型、五官的视觉验收。

冻结头颈SHA：`ed790ac8bcd41fd54d9a2e0a7be55f21704413ff5b86213a91285782a5d6f7c2`。当前颈侧Alpha230逐行实测在 `source/frozen_head_neck.json`；头部没有缩放、移动或改Alpha。衣领浅／深底检查见 `qa/collars_light.png`、`qa/collars_dark.png`。

以下是此前阶段记录，以以上最新状态为准。

## 历史阶段：直接重新生成头和脖子

用户指出男头骨不对，明确选择“最早颅顶较高、圆弧完整”的形状；随后要求直接重新生成，不继续在旧图抠修。最终采用内置image_gen完整新绘的无五官头部、双耳与连续颈部。最早旧高头只作结构参考；原三联第三款作肤色依据。前两次抠修／Alpha约束稿均作为过程归档，最终没有采用。

最终原生PNG为 `skull_partition_fix_20261105/source/head_final_native.png`。活动脸底与该原生结果逐字节相同：RGB、Alpha均未再编辑，不裁切、不缩放、不平移。头顶保持高圆弧，脸为空白，原五官继续独立叠加；不把旧图像素贴回新头，也不把旧颈部的凹口和毛刺保回来。

脖子按原画布中的下巴和颈领位置生成，左右轮廓连续，没有上一稿y510—520处的凹口／台阶。新颈不宣称与旧Alpha逐像素相同：实测Alpha≥230轮廓为y530的x428—601、y550的x426—603、y580的x421—608。上下位置沿用1024×1536共同画布与零运行时偏移，七套衣服在不靠头发遮挡的浅／深底条件下逐套检查，颈领连续、无漏底。下方尖端是交领后的隐藏皮肤覆盖，并非最终裸露颈形。

本次实际只改一张男性face_shape/01.png。其余50张活动纹理保持，包括六发、女性22张、眉眼鼻嘴、七套前衣和后领。男性鼻子与山根仍在眉眼下。运行时选项与配置保持。

## 当前来源与验证

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/skull_partition_fix_20261105/`。

- `source/head_final_native.png` / `head_final_native_prompt.txt`：最终原生新稿与完整生成要求。
- `qa/head_final_native_preview.png`：新头深底预览；`qa/final_head_and_neck.png`：实际安装PNG的浅底展示。
- `qa/new_face_seven_clothes_dark.png` / `_light.png`：七套颈领实际素材组合。
- `head_only/male/`：本次选定的28件男性素材检查集合，其中只有头部更新，其他件来自本轮前快照。
- `head_only/installation/install_report.json`：一张替换、50张保持及最终原生来源SHA。
- `head_only/installation/runtime_check.json`：1250项游戏检查通过，男女各54种组合；活动51纹理／44选项、导入像素一致、前后衣配对、鼻子层级、选项显隐／切换均通过。
- `head_only/installation/game_male_viewport.png`：实际游戏显示。

程序检查证明上述素材加载与拼装行为，不代替用户的头形、肤色与整体观感评价。

## 当前：三款前发与三款后发已接入

用户指出后发含大量前冠、额侧、耳前绕鬓，属于实际分层错误。三件后发候选已按百会—耳尖的前后空间收为后盖、后髻、耳后及颈肩后连续披发；前侧发冠应留在前发。候选在本目录 `male/hair_back/`，旧／新对照 `qa/rear_only_before_after.png`。用户本轮授权接入后，三件后发已替换活动素材；现有三件前发沿用女性发色发质校准版。新头颈原生PNG、五官、七套衣服和全部女性素材保持，均为1024×1536共同画布、零偏移。

## 既有来源

`female_hair_match_20261105/`为当前活动头发的发色发质校准来源：男性前发03耳侧硬片与环圈修绘，六发向女性既有暖黑灰色调和柔和微高光靠拢。

`edge_cleanup_20261105/`及更早v6原像素拆层是历史来源，不能用其“头部保持”或旧哈希覆盖本次用户要求的新绘头部。后续继续处理后发时，以本次新头和当前颈领实测为依据。

本轮接入记录：`skull_partition_fix_20261105/hair_set/installation/install_report.json`。实际替换3张后发，其余48张纹理保持；专门刷新三件导入缓存。`runtime_check.json`中1250项游戏检查通过，男女各54种组合。`hair_set/qa/installed_three_styles.png`及`installed_nine_mixes.png`直接读取活动素材合成预览；视觉待用户评价。
