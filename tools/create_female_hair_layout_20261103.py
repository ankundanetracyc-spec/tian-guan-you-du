"""Internal imagegen placement guides only; not candidate artwork."""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np
R=Path(__file__).resolve().parents[1]
O=R/'mods/appearance/portrait_generated_anchors_20261103/reference'
def bezier_path(segments):
 points=[segments[0]];p=np.array(segments[0],float)
 for c1,c2,end in segments[1:]:
  a,b,c=map(lambda x:np.array(x,float),(c1,c2,end))
  for t in np.linspace(0,1,40)[1:]:points.append(tuple((1-t)**3*p+3*(1-t)**2*t*a+3*(1-t)*t*t*b+t**3*c))
  p=c
 return points
cap=bezier_path([(390,356),((345,295),(370,236),(410,204)),((446,172),(483,159),(519,161)),((558,159),(603,177),(633,207)),((668,240),(687,298),(648,357)),((639,369),(629,378),(620,385)),((618,351),(610,316),(595,292)),((576,270),(547,259),(519,265)),((489,259),(459,270),(443,291)),((426,315),(420,351),(416,385)),((404,375),(397,364),(390,356))])
lock=bezier_path([(422,341),((415,393),(407,440),(413,494)),((421,541),(403,580),(400,624)),((398,675),(415,713),(418,756)),((421,804),(411,844),(408,880)),((410,832),(406,803),(402,774)),((396,734),(384,681),(386,630)),((385,579),(405,541),(399,492)),((393,447),(404,389),(408,349)),((411,344),(416,342),(422,341))])
im=Image.new('RGBA',(4096,6144));d=ImageDraw.Draw(im)
for poly in [cap,lock,[(1038-x,y) for x,y in lock]]:d.polygon([(round(x*4),round(y*4)) for x,y in poly],fill=(61,56,53,255))
im.resize((1024,1536),Image.Resampling.LANCZOS).save(O/'front_low_thin_layout.png')
# Continuous posterior hair, compact hidden crown and controlled side curtains.
back=bezier_path([(519,116),((579,116),(628,165),(639,228)),((650,287),(654,348),(656,402)),((657,460),(672,529),(681,594)),((693,668),(691,750),(659,810)),((623,875),(568,921),(519,945)),((470,921),(415,875),(379,810)),((347,750),(345,668),(357,594)),((366,529),(381,460),(382,402)),((384,348),(388,287),(399,228)),((410,165),(459,116),(519,116))])
im=Image.new('RGBA',(4096,6144));d=ImageDraw.Draw(im);d.polygon([(round(x*4),round(y*4)) for x,y in back],fill=(61,56,53,255));im.resize((1024,1536),Image.Resampling.LANCZOS).save(O/'back_compact_layout.png')
