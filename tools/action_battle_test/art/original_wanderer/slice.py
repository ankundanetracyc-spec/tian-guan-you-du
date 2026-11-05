from PIL import Image
from pathlib import Path
p=Path(__file__).parent
im=Image.open(p/'source.png')
regions={'head':(40,35,310,410),'body':(360,30,580,500),'far_arm':(630,65,810,555),'near_arm':(845,70,1015,580),'far_upper':(1070,40,1295,440),'far_lower':(1070,420,1295,745),'near_upper':(1305,45,1525,460),'near_lower':(1305,440,1525,745),'front':(55,415,395,960),'back':(495,500,1310,995)}
for name,box in regions.items():
 part=im.crop(box); bounds=part.getchannel('A').getbbox(); part=part.crop(bounds); part.save(p/(name+'.png'))
 print(name,part.size)
