from pathlib import Path
from PIL import Image,ImageDraw
import json
R=Path(__file__).resolve().parent.parent;P=R.parents[3];H=R/'hair_set';A=P/'mods/appearance/ink_three_variants_20261004/male'
Q=H/'qa';Q.mkdir(exist_ok=True)
def composite(f,b):
 out=Image.new('RGBA',(1024,1536),'#e8e1d6')
 for part,i in [('hair_back',b),('clothing_back',1),('face_shape',1),('clothing',1),('nose',1),('eyes',2 if f==3 else 1),('eyebrows',f),('mouth',1),('hair_front',f)]:out.alpha_composite(Image.open(A/part/f'{i:02}.png').convert('RGBA'))
 return out.crop((280,60,760,835)).resize((360,581))
strip=Image.new('RGB',(1080,581),'#e8e1d6');grid=Image.new('RGB',(1080,1803),'#e8e1d6');draw=ImageDraw.Draw(grid)
for f in range(1,4):
 strip.paste(composite(f,f),((f-1)*360,0))
 for b in range(1,4):
  grid.paste(composite(f,b),((b-1)*360,(f-1)*601+20));draw.text(((b-1)*360+8,(f-1)*601+4),f'Front {f} / Back {b}',fill='#333333')
strip.save(Q/'installed_three_styles.png');grid.save(Q/'installed_nine_mixes.png')
result=json.loads((H/'installation/runtime_check.json').read_text(encoding='utf8'));assert result['pass'],result['failures']
doc=P/'docs/美术规范/男性原图完整还原_20261105/README.md';s=doc.read_text(encoding='utf8')
s=s.replace('后发重新分层为独立候选，按“先把头弄好，再说”的顺序暂未接入，不把候选当成已修完的活动后发。','用户随后要求接入几款前后发：三款收窄后发已接入，与现有三款前发独立混搭。')
s=s.replace('## 待后续：后发归属','## 当前：三款前发与三款后发已接入')
s=s.replace('当前活动件尚未替换这些后发，避免在用户要求先处理头部时混装。','用户本轮授权接入后，三件后发已替换活动素材；现有三件前发沿用女性发色发质校准版。新头颈原生PNG、五官、七套衣服和全部女性素材保持，均为1024×1536共同画布、零偏移。')
s+='\n本轮接入记录：`skull_partition_fix_20261105/hair_set/installation/install_report.json`。实际替换3张后发，其余48张纹理保持；专门刷新三件导入缓存。`runtime_check.json`中'+str(result['checks'])+'项游戏检查通过，男女各54种组合。`hair_set/qa/installed_three_styles.png`及`installed_nine_mixes.png`直接读取活动素材合成预览；视觉待用户评价。\n'
doc.write_text(s,encoding='utf8')
for name in ['docs/README.md','docs/策划案摘要/README.md']:
 p=P/name;s=p.read_text(encoding='utf8');s=s.replace('后发归属重制仍为候选，按“先头再说”暂未接入。','随后已接入三款收窄后发，与三款现有前发独立混搭；新头颈保持，游戏检查通过，视觉待评价。');p.write_text(s,encoding='utf8')
print('Preview and current documentation updated; runtime checks:',result['checks'])
