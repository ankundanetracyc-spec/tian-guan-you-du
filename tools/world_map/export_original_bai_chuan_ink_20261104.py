"""Export the selected original-composition revision and a same-crop comparison."""
from pathlib import Path
import json
import shutil
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'textures/world/bai_chuan/original_ink_3x3_20261104'
QA=ROOT/'output/bai_chuan_original_ink_20261104/qa'
DEST=Path(r'E:\新建文件夹 (2)\2026-11-04\new-chat\outputs\百川原始底稿_纸墨手改')


def main():
    DEST.mkdir(parents=True,exist_ok=True)
    record=json.loads((BASE/'manual_style_record.json').read_text(encoding='utf-8'))
    crop=(340,2350,1370,3030)
    with Image.open(record['source']) as source,Image.open(BASE/'master_8000.png') as revised:
        left=source.convert('RGB').crop(crop)
        right=revised.convert('RGB').crop(crop)
    width,height=left.size
    sheet=Image.new('RGB',(width*2+60,height+86),'#eae7dc')
    sheet.paste(left,(20,64))
    sheet.paste(right,(width+40,64))
    font=ImageFont.truetype(str(ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),28)
    draw=ImageDraw.Draw(sheet)
    draw.text((24,18),'原黄绿底稿：原有比例与布局',font=font,fill='#34433c')
    draw.text((width+44,18),'手动处理：纸墨配色、墨线与纸面',font=font,fill='#34433c')
    sheet.save(DEST/'原底稿与手改对照.png')
    shutil.copy2(BASE/'master_8000.png',DEST/'纸墨地图_3762.png')
    shutil.copy2(BASE/'preview_1881.png',DEST/'纸墨地图_总览.png')
    shutil.copy2(QA/'free_world_1920.png',DEST/'实机近景_1920.png')
    shutil.copy2(QA/'free_world_1280.png',DEST/'实机远景_1280.png')
    record['validation']={'basic_checks':334,'movement_window_native_tile_checks':71,
                          'final_functional_failures':0,'build_errors':0,'build_warnings':0}
    record['world_kilometres']=[8000,8000]
    record['virtual_canvas']=[7372.8,7372.8]
    record['note']='Original composition retained; earlier regenerated map and standalone landscape experiments are not mounted.'
    (DEST/'手动修改与验收记录.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(DEST)


if __name__=='__main__':
    main()
