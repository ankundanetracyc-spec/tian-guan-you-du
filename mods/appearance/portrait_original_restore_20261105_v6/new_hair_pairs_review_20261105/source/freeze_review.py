from pathlib import Path
import hashlib,json,shutil
R=Path(__file__).resolve().parent.parent;P=R.parents[3];A=P/'mods/appearance/ink_three_variants_20261004';F=R/'review_02';F.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
selected=json.loads((R/'source/selected_assets.json').read_text(encoding='utf8'));records=[]
for record in selected['records']:
 source=R/record['file'];dest=F/'male'/record['part']/f"{record['index']:02}.png";dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest)
 assert sha(dest)==record['sha256'];records.append({**record,'frozen_file':dest.relative_to(F).as_posix()})
for name in ['three_new_pairs_light.png','three_new_pairs_dark.png','paired_layers.png','new_features_closeup.png','four_feature_samples.png']:
 shutil.copy2(R/'qa'/name,F/name)
report=json.loads((R.parent/'collar_hair_fixed_neck_20261105/collars_only/installation/runtime_check.json').read_text(encoding='utf8'));assert report['pass'] and not report['failures']
console=(R.parent/'collar_hair_fixed_neck_20261105/collars_only/installation/runtime_console.log').read_text(encoding='utf8',errors='replace');assert 'WARNING:' not in console and 'ERROR:' not in console
installed=json.loads((R.parent/'collar_hair_fixed_neck_20261105/collars_only/installation/install_report.json').read_text(encoding='utf8'))
for r in installed['records']:assert sha(P/r['resource_path'][6:])==r['sha256']
for p,d in installed['all_unchanged_sha256'].items():assert sha(P/p[6:])==d
manifest={'review_id':'male_new_pairs_features_review_02','status':'待用户确认；新前后发与新五官未接入','user_visual_approval':False,'canvas':[1024,1536],'head_neck_sha256':sha(A/'male/face_shape/01.png'),'configuration_sha256':sha(A/'appearance_parts.json'),'pairs':[{'id':i,'name':n,'front':f'male/hair_front/{i:02}.png','back':f'male/hair_back/{i:02}.png'} for i,n in enumerate(['疏朗半束','清劲高束','松风低束'],1)],'shared_new_features':['eyebrows','eyes','nose','mouth'],'feature_count_each':1,'layers':['hair_back','clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth','hair_front'],'records':records,'preview_sha256':sha(F/'three_new_pairs_light.png'),'collars_status':'七套前衣和后领已接入；新头发五官待确认','collars_runtime_checks':report['checks'],'approval_rule':'用户认可哪个编号，就使用本清单冻结PNG及哈希；不得重新生成后冒充同一选稿。'}
(F/'asset_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
doc=P/'docs/美术规范/男性原图完整还原_20261105/README.md'
s=doc.read_text(encoding='utf8');s=s.replace('当前状态：按用户最新要求，新高圆弧头部与连续颈部已经重新生成并接入；游戏检查通过。用户随后要求接入几款前后发：三款收窄后发已接入，与现有三款前发独立混搭。','当前状态：高圆弧头颈保持冻结；七套新前后领已接入并通过实机。用户随后要求舍弃旧前后发和五官，重新生成三套搭配、眉眼鼻嘴各一件先看样例。新样例已冻结编号，待用户明确认可后接入；不能把旧头发接入记录或衣领运行检查当成新发型五官已获认可。')
section='''
## 最新：新前后发与五官样例（待确认）

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/new_hair_pairs_review_20261105/`。最终审阅包为 `review_02/`，01疏朗半束、02清劲高束、03松风低束；每套对应独立前发与后发，四个五官样例由三套共用。眉、眼、鼻含山根、嘴均由内置image_gen新绘，无旧男性五官像素。首次浓睫毛、厚唇和嘴周色块稿被用户否定，已换成当前较清淡的新稿。

`review_02/asset_manifest.json`冻结各编号的10张PNG、预览及哈希。用户选定后必须接入这些同一份素材，不得另行生成后当作已认可稿。新发型和五官尚未加入活动包；旧稿仅保留工程历史，不能继续作为本轮候选。没有用户的明确认可不得把候选标为已实装。

新发型采用女性已装素材作发色与笔触参照，前发亮度中位约76、后发约68，压住高光。后盖在y250—400各检查行均窄于对应前发；前发蓬松稿过大已舍弃。所有素材使用1024×1536共同画布；头颈原PNG与配置未改。五官按源图实测瞳孔中心、鼻尖和唇线作整件配准，鼻子与山根先于眼、眉叠加。

样例图：`review_02/three_new_pairs_light.png`、`paired_layers.png`、`new_features_closeup.png`、`four_feature_samples.png`。生成提示分别保存在 `source/generation_prompts.json` 和 `features/source/generation_prompts.json`，全部使用内置image_gen；清边、发色统一和配准记录见 `source/selected_assets.json`。

## 最新：固定头颈重做七套领口（已接入）

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/collar_hair_fixed_neck_20261105/`。七套前衣与后领分别重新生成，并按冻结颈部与各套交领V点局部配准。最终使用连续的新绘衣服，避免新旧领线交叉渐混产生的重影；衣服设计配色沿用原款，不能宣称旧衣身逐像素不变。早期局部拼片稿及六发修订稿均未接入。

实际接入仅14张衣服层，头、颈、五官、六发及女性共37张纹理保持，配置保持。`collars_only/installation/runtime_check.json`共1250项检查通过，男女各54种组合，最终日志无ERROR／WARNING。备份配置已改为.before文件，避免被外观目录扫描为额外MOD。该检查只证明衣领接入和现有组合，不代替新发型、五官的视觉验收。

冻结头颈SHA：`ed790ac8bcd41fd54d9a2e0a7be55f21704413ff5b86213a91285782a5d6f7c2`。当前颈侧Alpha230逐行实测在 `source/frozen_head_neck.json`；头部没有缩放、移动或改Alpha。衣领浅／深底检查见 `qa/collars_light.png`、`qa/collars_dark.png`。

以下是此前阶段记录，以以上最新状态为准。

'''
s=s.replace('## 当前：直接重新生成头和脖子',section+'## 历史阶段：直接重新生成头和脖子',1);doc.write_text(s,encoding='utf8')
for file in ['docs/README.md','docs/策划案摘要/README.md']:
 p=P/file;s=p.read_text(encoding='utf8');lines=s.splitlines()
 for j,line in enumerate(lines):
  if line.startswith('男性高圆弧新头与颈部（'):
   suffix=line[line.find('[当前来源'): ] if '[当前来源' in line else ''
   lines[j]='男性立绘新样例与固定颈领（2026-11-05，衣领已接入，新发型五官待确认）：头颈原PNG与坐标冻结，七套前后领已重做，1250项实机通过。旧前后发和五官候选弃用，新三套前后发、眉眼鼻嘴各一件已按review_02冻结素材与预览；用户认可后接入，当前不宣称新样例已实装。'+suffix
 p.write_text('\n'.join(lines)+'\n',encoding='utf8')
print('Review 02 frozen with ten matching assets; collars installed and tested; new hair/features remain uninstalled')
