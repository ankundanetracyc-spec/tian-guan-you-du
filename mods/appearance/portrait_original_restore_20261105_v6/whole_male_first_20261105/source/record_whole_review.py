from pathlib import Path
from PIL import Image
import json,hashlib
R=Path(__file__).resolve().parent.parent;P=R.parents[3];B=R.parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
image=R/'male_whole_01.png'
with Image.open(image) as im:assert im.size==(1024,1536);mode=im.mode
# Reject all preceding part previews. Keep historical files only for provenance.
for relative in ['new_hair_pairs_review_20261105/review_02/asset_manifest.json','new_hair_pairs_review_20261105/source/selected_assets.json']:
 p=B/relative;data=json.loads(p.read_text(encoding='utf8'));data.update(status='用户全部否定，已弃用；禁止接入。现改为先审阅整张立绘，再制作部件。',user_visual_approval=False,rejected_by_user=True,superseded_by='whole_male_first_20261105/male_whole_01.png');p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
installed=json.loads((B/'collar_hair_fixed_neck_20261105/collars_only/installation/install_report.json').read_text(encoding='utf8'))
for record in installed['records']:assert sha(P/record['resource_path'][6:])==record['sha256']
for path,digest in installed['all_unchanged_sha256'].items():assert sha(P/path[6:])==digest
assert sha(P/'mods/appearance/ink_three_variants_20261004/appearance_parts.json')==installed['config_sha256']
manifest={'status':'完整男性立绘01，待用户审阅；未拆部件、未接入游戏','user_visual_approval':False,'generation_mode':'built-in image_gen; whole illustration generated in one pass','image':'male_whole_01.png','image_sha256':sha(image),'canvas':[1024,1536],'mode':mode,'native_image_unmodified':True,'reference_roles':{'reference/female_active.png':'女性当前活动素材完整组合：整体绘画质量、发色发质、肤色、细腻淡彩纸面质感','original_user_male_triptych':'原男性审美参考：自然男性五官比例、传统长发与青绿道袍，不采用前轮被否定的拆件稿'},'prompt':'source/whole_male_prompt.txt','workflow':'先完整立绘统一五官、发型、皮肤与颈领；用户认可后才逐步制作部件。届时以认可整稿为唯一视觉依据，统一画布与固定头颈锚点、百会前后归属和鼻山根层级。','old_parts_status':'review_02整批弃用，禁止据其进行接入；历史文件仅保留来源','active_game_textures_changed_this_turn':0,'active_game_configuration_changed_this_turn':False}
(R/'source/whole_review_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
doc=P/'docs/美术规范/男性原图完整还原_20261105/README.md';text=doc.read_text(encoding='utf8')
old_status=text.splitlines()[2]
text=text.replace(old_status,'当前状态：用户再次全部否定前后发与五官样例，review_02整批弃用。现改为先生成一张完整男性立绘统一整体观感，再逐步制作部件。完整新稿01已生成，待用户审阅；本轮未拆件、未接入、活动素材与配置保持。',1)
new='''## 最新：先完整整体，再逐步制作部件（待审阅）

用户明确要求“丑，全部去掉。你先生成整体效果吧，然后再慢慢生成部件”。此前三套前后发和新五官样例整批废止，`new_hair_pairs_review_20261105/review_02/asset_manifest.json`与`source/selected_assets.json`均标为用户拒绝、禁止接入。历史文件只作来源，不是可继续选装的候选。

新制作目录：`mods/appearance/portrait_original_restore_20261105_v6/whole_male_first_20261105/`。`male_whole_01.png`为内置image_gen一次完整绘制的1024×1536整张稿，原生结果未修改。以当前女性实际完整组合`reference/female_active.png`对齐绘画质感、暖黑灰发色和细柔发丝，以原男性三联图参考自然男性五官、长发与青绿道袍审美。没有继续用前轮部件拼成新预览。

本轮只交付整体稿：五官、发型、皮肤、头颈与衣领在同一张画里一起确定。提示与来源见`source/whole_male_prompt.txt`、`source/whole_review_manifest.json`，女性实际组合清单在`reference/female_reference_manifest.json`。该稿未拆部件、未接入；用户认可后，再以同一张认可稿逐步制作。头颈共同画布、锚点、百会前后归属和鼻山根在眉眼下的要求保留为后续分件约束，不把本轮提示坐标当作已实测通过的锚点。

本轮核对当前活动51张纹理和配置均未改变。此前七套衣领接入记录是历史工程状态，不表示本次整张新稿已经实装或通过用户视觉评价。

'''
text=text.replace('## 最新：新前后发与五官样例（待确认）',new+'## 已弃用：新前后发与五官样例（用户整批否定）',1)
text=text.replace('## 最新：固定头颈重做七套领口（已接入）','## 历史工程记录：固定头颈重做七套领口（已接入）',1)
text=text.replace('用户选定后必须接入这些同一份素材，不得另行生成后当作已认可稿。新发型和五官尚未加入活动包；旧稿仅保留工程历史，不能继续作为本轮候选。没有用户的明确认可不得把候选标为已实装。','用户随后整批否定，该清单现标为弃用、禁止接入，不再作为可选择候选。新发型和五官未加入活动包；旧稿仅保留工程历史。',1)
doc.write_text(text,encoding='utf8')
for relative in ['docs/README.md','docs/策划案摘要/README.md']:
 p=P/relative;lines=p.read_text(encoding='utf8').splitlines()
 for i,line in enumerate(lines):
  if line.startswith('男性立绘新样例与固定颈领（'):
   suffix=line[line.find('[当前来源'): ] if '[当前来源' in line else ''
   lines[i]='男性立绘先审整体（2026-11-05，完整新稿待审阅）：用户整批否定此前前后发与五官样例，review_02已弃用、禁止接入。现以内置绘图一次生成完整男修01，女性实际整图对齐发色发质与画风；先认可整体，再逐步做部件。本轮未拆件或接入，现有头颈锚点与活动素材保持。'+suffix
 p.write_text('\n'.join(lines)+'\n',encoding='utf8')
print('Whole illustration01 saved; previous previews rejected;51 active textures and configuration unchanged')
