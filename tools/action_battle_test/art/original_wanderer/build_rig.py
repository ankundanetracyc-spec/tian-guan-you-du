from PIL import Image
from pathlib import Path
import json
root=Path('tools/action_battle_test/art/original_wanderer'); parts=root/'parts';parts.mkdir(exist_ok=True)
old=Path('tools/action_battle_test/art/bilan_rig_parts_v1')
man=json.loads((old/'parts_manifest.json').read_text(encoding='utf-8'))
source=Image.open(root/'source.png')
# Trim faint generated halo on each isolated piece.
def prep(im, size):
 a=im.getchannel('A').point(lambda x: 0 if x<24 else x)
 im.putalpha(a)
 return im.resize(size, Image.Resampling.LANCZOS)
for item in man['parts']:
 name=item['name']; target=tuple(item['size'])
 if name=='head_hair': im=Image.open(root/'head.png')
 elif name=='robe_body': im=Image.open(root/'body.png')
 elif 'upper_arm' in name:
  im=Image.open(root/('far_arm.png' if name.startswith('far') else 'near_arm.png'));im=im.crop((0,0,im.width,int(im.height*.55)))
 elif 'forearm' in name:
  im=Image.open(root/('far_arm.png' if name.startswith('far') else 'near_arm.png'));im=im.crop((0,int(im.height*.45),im.width,im.height))
 else: im=Image.open(root/('far_lower.png' if name.startswith('far') else 'near_lower.png'))
 prep(im,target).save(root/item['png'])
# Fit the new complete trouser/boot asset to the original leg mesh UV canvas.
leg=source.crop((1070,40,1295,745));leg=prep(leg,(60,276)); canvas=Image.new('RGBA',(76,292));canvas.alpha_composite(leg,(8,8));canvas.save(parts/'bilan_weighted_leg_v2.png')
upper=canvas.copy();a=upper.getchannel('A');a.paste(0,(0,187,76,292));upper.putalpha(a);upper.save(parts/'bilan_weighted_leg_upper.png')
# Both cloth textures share the original robe mesh UV coordinates.
old_back=Image.open(old/'parts/bilan_robe_back_panel.png');size=old_back.size
back=Image.new('RGBA',size);panel=prep(Image.open(root/'back.png'),(int(size[0]*.85),int(size[1]*.52)));back.alpha_composite(panel,(0,int(size[1]*.43)));back.save(parts/'bilan_robe_back_panel.png')
front=Image.new('RGBA',size);panel=prep(Image.open(root/'front.png'),(int(size[0]*.28),int(size[1]*.50)));front.alpha_composite(panel,(int(size[0]*.58),int(size[1]*.4)));front.save(parts/'bilan_robe_front_panel.png')
back.save(parts/'bilan_robe_body_weighted.png')
man['bindings']['head_hair']['scale']=[0.052,0.072];man['bindings']['head_hair']['position']=[-6.5,-5.5];man['bindings']['robe_body']['scale']=[0.082,0.11]
man['schema']='original-wanderer-rig-parts/v1';man['source']='source.png';(root/'parts_manifest.json').write_text(json.dumps(man,ensure_ascii=False,indent=2),encoding='utf-8')
