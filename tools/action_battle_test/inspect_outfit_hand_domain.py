"""Read-only alpha/mesh-domain audit of the current full-canvas hand variants."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output",type=Path,default=Path(__file__).parent/'output/ink_outfit_hand_domain_audit')
    parser.add_argument("--extended-near",action='store_true')
    args = parser.parse_args()
    base = Path(__file__).parent
    source = json.loads((base.parents[1]/'resources/appearance/guigu_male101/model_frames_20261104.json').read_text(encoding='utf-8'))
    template = json.loads((base/'art/ink_outfit_01_20261104/assets/template/fullcanvas_template_anchors.json').read_text(encoding='utf-8'))
    art = base/'art/ink_outfit_01_20261104/assets'
    args.output.mkdir(parents=True,exist_ok=True)
    rows = []
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
    for side, sprite, cut in [('sleeve_near',79543,.662),('sleeve_far',84807,.650)]:
        mesh = next(m for m in source['meshes'] if int(m['spriteId'])==sprite)
        matrix = template['components'][side]['cropUvToFullCanvasNormalized']
        points = [(matrix[0][0]*u+matrix[0][1]*v+matrix[0][2],matrix[1][0]*u+matrix[1][1]*v+matrix[1][2]) for u,v in mesh['uv']]
        if side=='sleeve_near' and args.extended_near:
            points.append((26.5/60,48.5/60))
            mesh['triangles']=mesh['triangles']+[[15,16,35],[16,17,35]]
        for style in ['open','fist','sword']:
            path = art/(side+('' if style=='open' else '_'+style)+'.png')
            if not path.exists(): continue
            image = Image.open(path).convert('RGBA')
            width,height = image.size
            carrier = Image.new('L',image.size)
            draw = ImageDraw.Draw(carrier)
            for triangle in mesh['triangles']:
                draw.polygon([(points[i][0]*width,points[i][1]*height) for i in triangle],fill=255)
            alpha = image.getchannel('A')
            p_alpha,p_carrier = alpha.load(),carrier.load()
            opaque = outside = 0
            outside_box = [width,height,-1,-1]
            for y in range(int((cut-.003)*height),height):
                for x in range(width):
                    if p_alpha[x,y]<179: continue
                    opaque+=1
                    if p_carrier[x,y]: continue
                    outside+=1
                    outside_box=[min(outside_box[0],x),min(outside_box[1],y),max(outside_box[2],x),max(outside_box[3],y)]
            rows.append({'part':side,'style':style,'native_size':[width,height],'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'hand_region_cut_v':cut,'alpha_threshold':.70,'hand_opaque_pixels':opaque,'hand_alpha_outside_carrier':outside,'outside_fraction':outside/max(opaque,1),'outside_bbox':outside_box if outside else None})
            page = Image.new('RGB',(540,470),'#dddcd1')
            y0=int((cut-.06)*height)
            crop=image.crop((int(.30*width),y0,int(.73*width),int(.90*height)))
            composite=Image.new('RGBA',image.size,(0,0,0,0))
            lines=ImageDraw.Draw(composite)
            edges={tuple(sorted((tri[i],tri[(i+1)%3]))) for tri in mesh['triangles'] for i in range(3)}
            for a,b in edges: lines.line([(points[a][0]*width,points[a][1]*height),(points[b][0]*width,points[b][1]*height)],fill=(185,65,55,170),width=2)
            image_with_lines=Image.alpha_composite(image,composite).crop((int(.30*width),y0,int(.73*width),int(.90*height)))
            ratio=min(500/image_with_lines.width,390/image_with_lines.height)
            resized=image_with_lines.resize((round(image_with_lines.width*ratio),round(image_with_lines.height*ratio)),Image.Resampling.LANCZOS)
            page.paste(resized,(20,60),resized)
            painter=ImageDraw.Draw(page)
            painter.text((15,12),f'{side} {style} / hand alpha outside {outside/max(opaque,1):.2%}',font=font,fill='#424b48')
            page.save(args.output/(side+'_'+style+'_carrier.png'))
    result={'scope':'read-only native alpha versus arm carrier; source data and art untouched','engineered_near_extension':args.extended_near,'method':'native PNG alpha>=70%, exact full-canvas affine; lower wrist/hand region only','results':rows,'interpretation':'Nonzero edge pixels require visual review; zero coverage loss does not establish anatomical hand quality.'}
    (args.output/'hand_carrier_audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(rows,ensure_ascii=True))


if __name__=='__main__':
    main()
