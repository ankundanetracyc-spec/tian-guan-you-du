from pathlib import Path
import hashlib
import json
import struct

folder = Path(__file__).resolve().parent
root = folder.parents[2]
raw = (folder / '浏览器验收输出.log').read_text(encoding='utf-8-sig')
payload = raw.split('### Result\n', 1)[1].split('\n### Ran Playwright code', 1)[0]
record = json.loads(payload)
record['captureDimensions'] = {}
for name in record['captures']:
    header = (folder / name).read_bytes()[:24]
    assert header[:8] == b'\x89PNG\r\n\x1a\n'
    size = list(struct.unpack('>II', header[16:24]))
    expected = [1920, 1080] if '_1920' in name else [1280, 720]
    assert size == expected, (name, size)
    record['captureDimensions'][name] = size
record['scope'] = '独立浏览器样稿；测试只操作隔离内存夹具，未接正式游戏。'
record['visualReview'] = '目视1920白昼、1280入夜／日期／固定／未绑定完整图；布局与控件均保持在画面内。'
(folder / '验收记录.json').write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
sources = [
    ('textures/ui/cultivation/quiet_room_20261010/rest_day.png', '白昼卧房原图，完整只读引用'),
    ('textures/ui/cultivation/quiet_room_20261010/rest_night.png', '同一卧房夜景原图，完整只读引用'),
    ('textures/ui/yuxu_library_20261107/library_frame.png', '薄纸九区取样，沿现有paper定义'),
    ('docs/美术规范/角色与静室纸墨木石样稿_20261108/materials.js', '只读素材组件'),
    ('docs/美术规范/角色与静室纸墨木石样稿_20261108/skin-data.js', '只读六类素材与图集坐标'),
    ('docs/美术规范/角色与静室纸墨木石样稿_20261108/assets/控件补充图集_v1.png', '进度槽／填充与分隔线原素材'),
    ('resources/font/LXGWZhenKaiSlabGB-Regular.ttf', '项目现有字体'),
]
for name in ['按钮_常规_v4.png','按钮_短页签_v4.png','按钮_主操作_v4.png','控件_输入槽_v4.png']:
    sources.append(('docs/美术规范/角色与静室纸墨木石样稿_20261108/assets/按钮分型_v4/' + name, '按尺寸与用途复用，沿原低Alpha显示过滤，不改原图'))
for path, purpose in [('csharp/panel/RestPanel.cs','时长／日期／固定限制与恢复预览'),('csharp/panel/QuietRoomStyle.cs','昼夜边界与原卧房路径'),('csharp/system/WorldStaminaRules.cs','每小时恢复1点精力'),('csharp/debug/QuietRoomSmoke.cs','1年3月15日、40／100与固定8小时的隔离样例来源'),('csharp/MainScene.YuxuSectLife.cs','当前观复峰居所文字来源')]:
    sources.append((path,purpose))
source_record = {'date':'2026-11-08','newBitmapAssets':0,'screenshotsAreBrowserCaptures':True,'sharedFilesModified':False,'playerFilesRead':False,'sources':[{'path':path,'purpose':purpose,'sha256':hashlib.sha256((root/path).read_bytes()).hexdigest(),'mode':'只读引用'} for path,purpose in sources],'materialException':'数字增减采用裸三角；所有按钮／输入／进度与便笺仍用素材。'}
(folder / '素材来源.json').write_text(json.dumps(source_record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'passed':record['passed'],'captures':len(record['captures']),'dimensionsVerified':len(record['captureDimensions'])},ensure_ascii=False))
