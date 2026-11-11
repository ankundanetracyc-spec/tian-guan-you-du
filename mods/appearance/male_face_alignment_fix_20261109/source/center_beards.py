"""Center four existing beard styles with a single whole-layer translation each."""
import json
import numpy as np
from PIL import Image,ImageDraw
from diagnose_geometry import ROOT,PACK,ACTIVE,QA,SHA,freeze,write_json
from diagnose_beards import measure
from center_nose_mouth import shift_horizontal,current_comp

def main():
    baseline=freeze()
    diagnosis=json.loads((QA/'beard_geometry_diagnosis.json').read_text(encoding='utf-8'))
    records=[];pairs=[]
    for old in diagnosis['records']:
        n=old['physical_index']
        if n not in [2,3,4,5]:continue
        anchor='upper_moustache' if old['upper_moustache'] is not None else 'lower_chin_jaw'
        field='endpoint_midpoint_x' if anchor=='upper_moustache' else 'lower15pct_center'
        before_x=old[anchor][field] if field=='endpoint_midpoint_x' else old[anchor][field][0]
        dx=513.0-before_x
        srcpath=PACK/f'source/frozen/male/face_accessory/{n:02}.png'
        assert SHA(srcpath)==SHA(ACTIVE/f'male/face_accessory/{n:02}.png')
        source=Image.open(srcpath).convert('RGBA')
        candidate=shift_horizontal(source,dx)
        dest=PACK/f'male/face_accessory/{n:02}.png';dest.parent.mkdir(parents=True,exist_ok=True);candidate.save(dest)
        a=np.asarray(candidate)
        upper=measure(a,[436,449,586,490]);lower=measure(a,[421,490,606,585])
        records.append({'physical_index':n,'name':old['name'],'source':srcpath.relative_to(PACK).as_posix(),'source_sha256':SHA(srcpath),
                        'candidate':dest.relative_to(PACK).as_posix(),'candidate_sha256':SHA(dest),'selected_semantic_anchor':anchor+'.'+field,
                        'source_center_x':before_x,'translation':[dx,0],'matrix':[[1,0,dx],[0,1,0]],'scale':1,'rotation_deg':0,
                        'one_pass_premultiplied_alpha_bilinear_sampling':True,'local_warps':False,'RGB_repainting':False,'source_rgb_paste':False,
                        'upper_moustache_after':upper,'lower_chin_jaw_after':lower})
        pairs.append((n,source,candidate))
    nose=Image.open(PACK/'male/nose/01.png').convert('RGBA');mouth=Image.open(PACK/'male/mouth/01.png').convert('RGBA')
    table=Image.new('RGB',(4*430,2*330),'white')
    for col,(n,source,candidate) in enumerate(pairs):
        for row,beard in enumerate([source,candidate]):
            out=current_comp(1,nose,mouth);out.alpha_composite(beard)
            view=out.crop((405,435,620,585)).resize((430,300),Image.Resampling.NEAREST).convert('RGB')
            d=ImageDraw.Draw(view);d.line(((513-405)*2,0,(513-405)*2,299),fill=(0,170,130),width=1)
            table.paste(view,(col*430,row*330+28));ImageDraw.Draw(table).text((col*430+4,row*330+6),f'beard{n:02} '+('BEFORE' if row==0 else 'AFTER whole-layer translation'),fill='black')
    table.save(QA/'beards02_05_whole_layer_centering_before_after_2x.png')
    for old in baseline['files']:assert SHA(ROOT/old['file'])==old['sha256'],old['file']
    report={'schema':'male-four-beards-whole-layer-centering/v1','axis_x':513,'source_measurements':'qa/beard_geometry_diagnosis.json',
            'records':records,'beard01_empty_preserved':True,'beard06_processed_separately':'source/beard06_registration.json',
            'active67_png_two_configs_preserved':True}
    write_json(PACK/'source/beard_centering.json',report)
    print(json.dumps({'staged':len(records),'records':[{'n':r['physical_index'],'dx':r['translation'][0],
                  'moustache_x':r['upper_moustache_after']['endpoint_midpoint_x'] if r['upper_moustache_after'] else None,
                  'chin_x':r['lower_chin_jaw_after']['lower15pct_center'][0] if r['lower_chin_jaw_after'] else None} for r in records],'baseline_preserved':True}))

if __name__=='__main__':main()
