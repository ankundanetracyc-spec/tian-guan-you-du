from pathlib import Path
from PIL import Image
import json,hashlib
R=Path(__file__).resolve().parent.parent;B=R.parent;P=B.parents[2];H=R/'head_only';I=H/'installation'
result=json.loads((I/'runtime_check.json').read_text(encoding='utf8'));assert result['pass'],result['failures']
report=json.loads((I/'install_report.json').read_text(encoding='utf8'))
for file,digest in report['all_unchanged_sha256'].items():assert hashlib.sha256((P/file[6:]).read_bytes()).hexdigest()==digest
source=R/'source/head_final_native.png';installed=P/'mods/appearance/ink_three_variants_20261004/male/face_shape/01.png'
assert installed.read_bytes()==source.read_bytes()
a=Image.open(installed);bg=Image.new('RGBA',a.size,'#e8e1d6');bg.alpha_composite(a);bg.crop((345,150,685,740)).resize((510,885)).save(R/'qa/final_head_and_neck.png')
doc=f'''# 男性原图完整还原（2026-11-05）

当前状态：按用户最新要求，新高圆弧头部与连续颈部已经重新生成并接入；游戏检查通过。后发重新分层为独立候选，按“先把头弄好，再说”的顺序暂未接入，不把候选当成已修完的活动后发。

## 当前：直接重新生成头和脖子

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
- `head_only/installation/runtime_check.json`：{result['checks']}项游戏检查通过，男女各54种组合；活动51纹理／44选项、导入像素一致、前后衣配对、鼻子层级、选项显隐／切换均通过。
- `head_only/installation/game_male_viewport.png`：实际游戏显示。

程序检查证明上述素材加载与拼装行为，不代替用户的头形、肤色与整体观感评价。

## 待后续：后发归属

用户指出后发含大量前冠、额侧、耳前绕鬓，属于实际分层错误。三件后发候选已按百会—耳尖的前后空间收为后盖、后髻、耳后及颈肩后连续披发；前侧发冠应留在前发。候选在本目录 `male/hair_back/`，旧／新对照 `qa/rear_only_before_after.png`。当前活动件尚未替换这些后发，避免在用户要求先处理头部时混装。

## 既有来源

`female_hair_match_20261105/`为当前活动头发的发色发质校准来源：男性前发03耳侧硬片与环圈修绘，六发向女性既有暖黑灰色调和柔和微高光靠拢。

`edge_cleanup_20261105/`及更早v6原像素拆层是历史来源，不能用其“头部保持”或旧哈希覆盖本次用户要求的新绘头部。后续继续处理后发时，以本次新头和当前颈领实测为依据。
'''
(P/'docs/美术规范/男性原图完整还原_20261105/README.md').write_text(doc,encoding='utf8')
for file,prefix in [('docs/README.md',''),('docs/策划案摘要/README.md','../')]:
 p=P/file;lines=p.read_text(encoding='utf8').splitlines()
 entry=f'男性高圆弧新头与颈部（2026-11-05，已接入、实机通过）：用户选最早高圆弧头形后要求直接重生；最终原生头颈PNG原样实装，旧描边／凹口稿弃用，七套颈领核对。仅换头部一张，50张纹理与配置保持，{result["checks"]}项检查通过。后发归属重制仍为候选，按“先头再说”暂未接入。[当前来源与实际画面]({prefix}美术规范/男性原图完整还原_20261105/README.md)。'
 lines=[entry if s.startswith('男性发质发色与女性对齐（2026-11-05') or s.startswith('男性高圆弧新头与颈部（2026-11-05') else s for s in lines]
 p.write_text('\n'.join(lines)+'\n',encoding='utf8')
print('Fresh native head/neck installation finalized; 50 other textures preserved')
