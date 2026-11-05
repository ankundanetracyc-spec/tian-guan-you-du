from pathlib import Path
from PIL import Image
import json,hashlib
R=Path(__file__).resolve().parent.parent;B=R.parent;P=B.parents[2]
result=json.loads((R/'installation/runtime_check.json').read_text(encoding='utf8'))
assert result['pass'],result['failures']
report=json.loads((R/'installation/install_report.json').read_text(encoding='utf8'))
for file,digest in report['all_unchanged_sha256'].items():assert hashlib.sha256((P/file[6:]).read_bytes()).hexdigest()==digest
for rec in report['records']:assert hashlib.sha256((P/rec['resource_path'][6:]).read_bytes()).hexdigest()==rec['sha256']
out=Image.new('RGB',(1350,800),'#e8e1d6')
for i in range(1,4):out.paste(Image.open(R/'qa'/f'style0{i}.png').crop((245,65,785,1025)).resize((450,800)),((i-1)*450,0))
out.save(R/'qa/three_current_styles.png')
doc=f'''# 男性原图完整还原（2026-11-05）

状态：男性耳侧连接及发质发色修订已接入，游戏检查通过；最终观感待用户评价。当前权威为本文本次“与女性发质发色对齐”，前轮干净分层重制为其来源。

## 当前修订：与女性发质发色对齐

用户指出第三款右耳侧结构奇怪、男性头发又泛白，要求参考已经实装的女性发质和发色。本轮读取活动女性六发作为色调和细高光参照，女性文件保持。

定位：用户截图对应男性前发03约x615—684、y363—443。该处原有环状横搭发丝、平灰多边形暗片和亮切口。使用内置image_gen原位修绘，再只选择右耳侧小区域，把上侧梳发自然接入向下长束；冠顶、发际主位置、长发末端和其余形状保持原件。

发色不是全图统一压黑。男性前发主体中位亮度本来略低于女性，泛白主要来自过强的局部亮线和微对比。按女性共同取样压低过强微高光，同时小幅校准暖灰色调，保留发丝纹理、主体暗部和层次。男前发01微高光比由0.392降至0.258、女性参照0.261；三后发由0.214—0.233降至0.166—0.172、女性参照0.168。指标针对固定采样区，不等于整张画风逐像素一致。

只替换六张男性前后发PNG。五张发层Alpha逐像素保持，前发03只有耳侧修绘区改变Alpha。所有图片仍1024×1536、无缩放、共同原点与零偏移。头部、耳朵、脸颈轮廓、脖子锚点、五官、七套衣服及女性素材均经SHA核对保持，固定鼻子与山根在眉眼下的层级。

## 当前交付与验证

当前制作包：`mods/appearance/portrait_original_restore_20261105_v6/female_hair_match_20261105/`。

- `source/front03_temple_raw.png`、`temple_edit_prompt.txt`：局部原生修绘及提示；`match_hair.py`：区域选择与女性参照校准。
- `qa/temple_before_after.png`：左旧右新；`qa/hair_front_female_before_after.png`、`hair_back_female_before_after.png`：上女性、中旧男、下当前男。
- `qa/hair_match_report.json`：校准与Alpha变化记录；`qa/nine_mixes_light.png` / `_dark.png`：九种前后发混搭。
- `before_male/`、`installation/baseline.json`：本轮开始前快照。
- `installation/install_report.json`：六件替换、其余45张活动纹理与配置保持的SHA证据；`import_refresh.json`：使用相同资源路径/参数完成的六件独立导入缓存刷新。
- `installation/runtime_check.json`：{result['checks']}项游戏检查通过、男女各54种组合、51纹理与44选项、前后衣成对显示与鼻子层级检查。
- `installation/game_male_viewport.png`：游戏实际加载；`qa/three_current_styles.png`：当前三款素材合成。

已人工查看修绘处3倍放大、男女发质同倍率对照及浅深底混搭，并独立复核未新增肤色残片、白点或接片边。工程检查与取样指标不能代替用户的视觉评价。

## 前轮来源

`edge_cleanup_20261105/`为上一轮干净分层来源：六发重生，修后发分叉尾、前发杂丝及误带双耳；用户要求旧头回用，肤色对照第三张，脸颈Alpha保持。当前继续沿用该旧头与七件原图主体五官，前后发由本文本次修订覆盖。此前各轮报告只作历史依据，不代表当前活动文件。
'''
(P/'docs/美术规范/男性原图完整还原_20261105/README.md').write_text(doc,encoding='utf8')
for file,prefix in [('docs/README.md',''),('docs/策划案摘要/README.md','../')]:
 p=P/file;lines=p.read_text(encoding='utf8').splitlines()
 entry=f'男性发质发色与女性对齐（2026-11-05，已接入、实机通过）：第三款右耳侧硬片、环圈改为连续下垂发束；六发按已实装女性校准色调和微高光，收敛银白亮线。六张替换，其余45张与头颈锚点保持。{result["checks"]}项检查通过，视觉待评价。[当前来源、男女对照与实际画面]({prefix}美术规范/男性原图完整还原_20261105/README.md)。'
 lines=[entry if s.startswith('男性干净分层重制（2026-11-05') or s.startswith('男性发质发色与女性对齐（2026-11-05') else s for s in lines]
 p.write_text('\n'.join(lines)+'\n',encoding='utf8')
print('Documentation finalized; six edits and 45 unchanged textures verified')
