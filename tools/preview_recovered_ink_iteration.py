"""Read-only contact sheets and file checks for the recovered portrait iteration."""
from pathlib import Path
import json, hashlib, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
R=ROOT/'mods/appearance/ink_reference_recovery_20261004'
NAMES={1:'男 1 · 自然长披',2:'男 2 · 高束长发',3:'男 3 · 低束系带',5:'女 5 · 高髻披发',6:'女 6 · 偏分低束',7:'女 7 · 高马尾',8:'女 8 · 齐肩偏分',9:'女 9 · 盘辫侧垂',10:'女 10 · 高髻双垂'}
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
BG='#e8e3dc'

def tile(im,box,size):
 im=im.convert('RGBA').crop(box)
 im.thumbnail(size,Image.Resampling.LANCZOS)
 out=Image.new('RGBA',size,BG)
 out.alpha_composite(im,((size[0]-im.width)//2,(size[1]-im.height)//2))
 return out.convert('RGB')

def main():
 board=Image.new('RGB',(1440,1830),BG)
 d=ImageDraw.Draw(board);report=[]
 for i,(n,name) in enumerate(NAMES.items()):
  path=R/f'iteration/{n:02}.png';im=Image.open(path)
  assert im.mode=='RGBA' and im.size==(1024,1536)
  a=np.asarray(im);assert a[0,0,3]==0 and a[:,:,3].max()>240
  x=i%3*480;y=i//3*610
  board.paste(tile(im,(230,0,795,750),(470,560)),(x+5,y+42));d.text((x+15,y+8),name,font=FONT,fill='#352f2b')
  # Fixed sampling is only a rough tone audit; never treated as approval.
  sample=a[300:322,485:535,:3]
  report.append({'number':n,'path':path.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'canvas':list(im.size),'mode':im.mode,'alpha_range':[int(a[:,:,3].min()),int(a[:,:,3].max())],'forehead_sample_median':np.median(sample.reshape(-1,3),axis=0).tolist()})
 board.save(R/'iteration_overview.jpg',quality=95)
 # Existing composited game face, original recovered image, and new drawing.
 sys.path.insert(0,str(ROOT/'tools'))
 import build_ink_three_variants as b
 comp=Image.new('RGB',(1440,960),BG);d=ImageDraw.Draw(comp)
 for row,(sex,n) in enumerate([('male',2),('female',6)]):
  vs=[1]*9
  if sex=='female':vs[2:7]=[4]*5
  old=b.compose(sex,vs)
  oldbox=(385,302,652,558)
  ref=Image.open(R/f'references/{n:02}.png');new=Image.open(R/f'iteration/{n:02}.png')
  box=(365,295,665,570) if sex=='male' else (365,275,665,548)
  for col,(im,bb,label) in enumerate([(old,oldbox,'当前游戏五官'),(ref,box,'找回的参考'),(new,box,'本轮水墨迭代')]):
   comp.paste(tile(im,bb,(455,405)),(col*480+10,row*480+55));d.text((col*480+12,row*480+12),('男 · ' if row==0 else '女 · ')+label,font=FONT,fill='#352f2b')
 comp.save(R/'facial_comparison.jpg',quality=96)
 (R/'verification.json').write_text(json.dumps({'stage':'visual_master_candidates','count':len(report),'excluded_reference':4,'parts_registered':False,'game_integrated':False,'visual_user_approved':False,'items':report},ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'count':len(report),'checks':'RGBA dimensions and transparency passed','foreheads':[x['forehead_sample_median'] for x in report]}))

if __name__=='__main__':main()
