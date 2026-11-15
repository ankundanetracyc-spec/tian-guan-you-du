from pathlib import Path
from PIL import Image,ImageDraw
import hashlib,json
R=Path(__file__).resolve().parent.parent
B=R.parent;P=B.parents[2]
result=json.loads((R/'installation/runtime_check.json').read_text(encoding='utf8'))
assert result['pass'],result['failures']
head=json.loads((R/'qa/previous_head_skin_report.json').read_text(encoding='utf8'))
assert head['alpha_identical'] and head['changed_alpha_pixels']==0
report=json.loads((R/'installation/install_report.json').read_text(encoding='utf8'))
for r in report['records']:
 assert hashlib.sha256((P/r['resource_path'][6:]).read_bytes()).hexdigest()==r['sha256']
fixed=B/'rig/current_face_neck.json';rig=json.loads(fixed.read_text(encoding='utf8'))
rig['face_sha256']=head['output_sha256'];rig['face_alpha_identical_to_previous']=True
fixed.write_text(json.dumps(rig,ensure_ascii=False,indent=2),encoding='utf8')
out=Image.new('RGB',(1200,900),'#273238');draw=ImageDraw.Draw(out)
for col,(part,i,title) in enumerate([('hair_back',2,'Rear 02'),('hair_front',3,'Front 03'),('hair_front',1,'Front 01 - hair only')]):
 a=Image.open(R/'male'/part/f'{i:02}.png').crop((285,60,755,1060)).resize((400,851))
 bg=Image.new('RGBA',a.size)
 d=ImageDraw.Draw(bg)
 for y in range(0,851,16):
  for x in range(0,400,16):d.rectangle((x,y,x+15,y+15),fill='#30373b' if (x//16+y//16)%2 else '#242b2f')
 bg.alpha_composite(a);out.paste(bg,(col*400,38));draw.text((col*400+10,10),title,fill='white')
out.save(R/'qa/three_corrected_layers.png')
text=f'''# 男性原图完整还原（2026-11-05）

状态：干净分层重制已接入，游戏检查通过；最终观感待用户评价。用户否定上一版抠图碎片后，明确要求照当前还原版和原三联图重新生成；随后指定头部使用之前的头，肤色接近第三张，保持脖子锚点。本条为当前裁定。

## 当前选择

六个前后发由内置 image_gen 重制。本轮修正三处用户点名问题：后发02不再有三根分叉长条，改为连续垂落发片；前发03收除内侧杂丝和多余细圈；前发01去除误画的双耳，发层只含头发。其余可见断开碎片、衣领金纹及肤色残留一并清理。重生成的发丝RGB不宣称与原图逐像素一致。

头部使用本轮开始前的脸底；新生成的头明确弃用。只清理夹带的黑发、杂点和隐藏区污染。头型、耳朵、下颌与全部Alpha保持。原图第三款无遮挡额头与双颊4621个样本原本就与旧头相同，故没有强行叠色。第530／550／580行不透明颈部均为x430—605、宽176；全脸颈Alpha改动0像素。

三眉、两眼保留原图主体RGB与坐标，仅在两端用Alpha收掉夹带的前发；鼻、嘴保持。鼻子包括山根和鼻翼，位于眉眼下方。男女各部位均为1024×1536 RGBA、共同原点、零运行时偏移。七套前衣和后领、女性22张与空面饰保持本轮前文件。

## 交付与验证

当前制作与安装记录：`mods/appearance/portrait_original_restore_20261105_v6/edge_cleanup_20261105/`。本轮实改12张PNG：六发、三眉、两眼及旧脸底污染清理。原始v6安装记录保留为历史，不代表本次重制后的活动文件。

- `source/regen/prompts.json` / `revision_prompts.json`：内置生成提示、参考输入；所选原生稿与未用的新头一并留档。
- `source/select_regenerated.py`：所选六发，不缩放、不移动；保留用户要求的旧头。
- `source/clean_previous_head.py`、`qa/previous_head_skin_report.json`：旧头来源、肤色与固定Alpha证据。
- `qa/three_corrected_layers.png`：三处点名修正的棋盘透明底独立层；`qa/nine_mixes_dark.png` / `_light.png`：九种发型混搭。
- `before_male/` / `installation/baseline.json`：本轮开始前快照。
- `installation/install_report.json`：实际活动文件SHA；`runtime_check.json`：{result['checks']}项游戏检查通过、男女各54组合、51纹理与44选项、前后衣成对显示与鼻子层级检查。
- `installation/game_male_viewport.png`：真实游戏画面。

游戏检查数量仅证明上述工程行为，不能代替视觉评价。此前v6首次原像素拆层是本轮来源；其抠图残留已经用户指出并由本轮覆盖。
'''
(P/'docs/美术规范/男性原图完整还原_20261105/README.md').write_text(text,encoding='utf8')
for f,prefix in [('docs/README.md',''),('docs/策划案摘要/README.md','../')]:
 p=P/f;lines=p.read_text(encoding='utf8').splitlines()
 line=f'男性干净分层重制（2026-11-05，已接入、实机通过）：按用户要求照原图与当前版重生六发，修后发分叉尾、前发杂丝及误带耳朵；头部用回之前的头，肤色核对第三款，全脸颈Alpha保持，眉眼主体保留。12张清理替换，七套衣服与女性保持。{result["checks"]}项检查通过，视觉待评价。[当前来源、独立层与实际画面]({prefix}美术规范/男性原图完整还原_20261105/README.md)。'
 lines=[line if s.startswith('男性原图像素拆层（2026-11-05') or s.startswith('男性干净分层重制（2026-11-05') else s for s in lines]
 p.write_text('\n'.join(lines)+'\n',encoding='utf8')
print('Current documentation and independent transparent-layer review updated')
