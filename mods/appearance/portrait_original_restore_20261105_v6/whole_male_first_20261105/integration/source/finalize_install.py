from pathlib import Path
import json,hashlib,shutil
from PIL import Image
import numpy as np
R=Path(__file__).resolve().parent.parent;P=R.parents[4];B=R.parents[1];A=P/'mods/appearance/ink_three_variants_20261004';I=R/'installation'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report=json.loads((I/'install_report.json').read_text(encoding='utf8'));runtime=json.loads((I/'runtime_check.json').read_text(encoding='utf8'))
assert runtime['pass'] and not runtime['failures'],runtime['failures']
log=(I/'runtime_console.log').read_text(encoding='utf8',errors='replace');assert 'ERROR:' not in log and 'WARNING:' not in log
for r in report['records']:assert sha(P/r['resource_path'][6:])==r['sha256']
for p,d in report['baseline_sha256'].items():assert sha(P/p[6:])==d
assert sha(A/'appearance_parts.json')==report['config_sha256']
assert np.array_equal(np.array(Image.open(A/'male/face_shape/01.png'))[:,:,3],np.array(Image.open(R/'before/male/face_shape/01.png'))[:,:,3])
assert runtime['male_hair_feature_combinations']==1 and runtime['female_hair_feature_combinations']==54
assert runtime['female_preserved']==22
# Copy only the actual game texture for the final illustration preview.
actual=Image.open(I/'game_male_composite.png').convert('RGBA');out=Image.new('RGBA',actual.size,'#e8e1d6');out.alpha_composite(actual);out.convert('RGB').save(R/'qa/installed_game_portrait.png')
manifestpath=R.parent/'source/whole_review_manifest.json';manifest=json.loads(manifestpath.read_text(encoding='utf8'));manifest.update(status='用户已认可完整稿；同源九件已接入并完成实机检查',user_visual_approval=True,integration='integration/',installed_part_count=9,retired_old_variant_count=7,runtime_checks=runtime['checks'],user_part_visual_approval=False);manifestpath.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
summary={'approved_master_sha256':report['approved_master_sha256'],'installed_parts':9,'retired_old_variants':7,'male_selectable':15,'female_selectable':22,'total_selectable':37,'registered_png_count':44,'head_neck_alpha_changed_pixels':0,'runtime':runtime,'user_approved_whole':True,'user_approved_reconstructed_parts':False,'axis_alignment':report.get('axis_alignment',{}),'brow_eye_edge_cleanup':report.get('brow_eye_edge_cleanup',[])}
(I/'final_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
doc=P/'docs/美术规范/男性原图完整还原_20261105/README.md';text=doc.read_text(encoding='utf8');old=text.splitlines()[2]
text=text.replace(old,'当前状态：用户已认可完整男性新稿01并明确“可以，加”。同源的头脸、两发、四个五官与首套前后衣共九件已接入；旧七个多余发型／眉眼选项撤下，男性先保留这一套头脸搭配。实机检查通过，头颈轮廓及颈领锚点保持；女性与另外六套衣服保持。',1)
section=f'''## 当前：认可整稿01的部件已接入（{runtime['checks']}项实机检查通过）

完整母版固定为 `whole_male_first_20261105/male_whole_01.png`，SHA `76e789ffa97e72c8ece941401e4b7aa93ba74e7567e881ed2e7f48a67ae266c2`。用户已明确认可并授权接入，不再请求重复批准。制作、备份与检查集合位于 `whole_male_first_20261105/integration/`。

实际替换九张：头脸、前发、后发、眉、眼、鼻含山根、嘴、01前衣与01后领。四个五官使用内置image_gen提取透明层并以母版实际瞳孔、鼻尖和唇线整件配准；核心可见笔画及肤色RGB保留认可母版，原发丝遮挡的小眉眼外端由生成稿补齐。没有使用已弃用的review_02五官。前发按母版发际整层配准并统一至母版发色，母版可见中性发色区域优先保留原RGB；后发为连续后盖、原款髻簪和披发，不再硬剪后盖轮廓。被头发遮挡的衣料和头皮需补绘，因此整个组合不宣称与整稿逐像素相同。

头脸沿用当前活动件的完整Alpha，改变像素为0；只更换皮肤绘画，边缘颜色从新稿实心肤色延展。头颈不缩放、不平移，固定颈侧Alpha230仍为y530 x428—601、y550 x426—603、y580 x421—608。五官保持母版实际眼位约y365而非强拉旧379行；山根随鼻层在眉、眼下。01衣领控制点回到母版(433,545)/(599,545)/(532,680)，其他六套前后衣未动，七套均在无头发遮挡的浅／深底检查。

撤下旧前发02/03、后发02/03、眉02/03、眼02共七件并保留备份；旧保存样式ID回落至新01。男性九部位仍可分别显隐与选择，其中头发、五官各一款、衣服七款。共男15＋女22＝37可选项，另有男私有后领七件，注册纹理共44。女性22张及配置字典、其他六套衣服十二张、无须空层共35张保持。

接入记录：`integration/installation/install_report.json`；9张活动PNG按原导入路径刷新缓存，`import_refresh.json`保留对应关系。`runtime_check.json`记录{runtime['checks']}项通过、零失败，含男性一套与女性54种组合、所有七套衣前后联动／显隐、随机／性别记忆、退役样式回落、正式渲染器鼻层级和活动PNG与实际导入纹理一致。最终运行日志无ERROR／WARNING。没有修改生产C#，测试脚本为 `tools/verify_portrait_approved_whole_20261105.gd`。

最终预览直接取游戏生成纹理：`integration/qa/installed_game_portrait.png`，若已运行最新截图，界面见`integration/installation/game_male_viewport_latest_20261106.png`。原`game_male_viewport.png`及母版与拆件对照是中线修订前记录；不能当成最新修边效果。七套领口见 `qa/seven_collars_light.png`／`seven_collars_dark.png`。这些工程检查证明接入、层序与接口，不把整稿认可扩张为用户已认可全部重构部件。

生成模式均为内置image_gen；根部提示在 `integration/source/generation_prompts.json`，五官提示在 `integration/face_parts/source/generation_prompts.json`，服装提示与原生来源在 `integration/garment_parts/source/selection_manifest.json`。后续扩款仍以同一认可整稿的画风与当前固定头颈为依据，不恢复旧弃用样例。

'''
revision='''## 最新修订（2026-11-06）：中线与眉眼透明边缘

用户指出头脸不对称。复核发现固定头骨Alpha中轴约512.5，而认可母版原生五官中轴约518—520，拆件后相差约5像素；并不是耳朵被单侧放大。眉眼鼻嘴和前发作为一组整体整数左移5像素，不缩放、不局部扭曲，头脸／脖子和全部衣服文件逐字节保持。校准后暗瞳中点513.01，与头轴相差约0.5像素。母版双眼约0.4像素高差、轻微眉尾形状差保留。见`integration/axis_alignment_20261106/correction.json`及`integration/qa/asymmetry_audit_20261106.json`。

随后用户指出眉毛下沿、眼角有未清干净的肤片和硬边。两件的透明边缘另行修订，不能只凭核心笔画覆盖率宣称抠干净。修订来源、深浅／棋盘放大检查和实际改变记录在`integration/edge_cleanup_20261106/`；核心笔画RGB、位置和头颈保持，当前活动哈希见`installation_report.json`。初版`face_parts/source/final_manifest.json`为左移／修边前来源，不代表当前最终哈希。

当前预览以游戏实际合成的`integration/qa/installed_game_portrait.png`和最新截图为准。返回开始页再进入立绘测试页会清理旧合成缓存并读取更新，不需改变游戏存档。

'''
text=text.replace('## 最新：先完整整体，再逐步制作部件（待审阅）',revision+section+'## 已完成阶段：先完整整体，再逐步制作部件（用户已认可）',1)
text=text.replace('本轮只交付整体稿：','当时只交付整体稿：',1).replace('该稿未拆部件、未接入；用户认可后，再以同一张认可稿逐步制作。','该阶段未拆部件、未接入；现已按用户认可完成上节接入。',1)
doc.write_text(text,encoding='utf8')
for relative in ['docs/README.md','docs/策划案摘要/README.md']:
 p=P/relative;lines=p.read_text(encoding='utf8').splitlines()
 for i,line in enumerate(lines):
  if line.startswith('男性立绘先审整体（'):
   suffix=line[line.find('[当前来源'): ] if '[当前来源' in line else ''
   lines[i]=f'男性认可整稿部件接入（2026-11-05，已接入）：用户认可完整01并授权；头、两发、四五官与首套前后衣九件已替换，旧七款发眉眼撤下，男15／女22共37选项。头颈Alpha逐像素保持，七套领口与{runtime["checks"]}项实机检查通过，女性及其他六套衣保持；整体认可不等于重构部件已获用户视觉评价。'+suffix
 p.write_text('\n'.join(lines)+'\n',encoding='utf8')
print(f'Installed approved portrait parts;{runtime["checks"]} checks passed; final portrait and current documentation saved')
