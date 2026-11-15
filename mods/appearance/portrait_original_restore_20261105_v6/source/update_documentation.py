from pathlib import Path
import json
B=Path(__file__).resolve().parent.parent
P=B.parents[2]
result=json.loads((B/'installation/runtime_check.json').read_text(encoding='utf8'))
assert result['pass'],result['failures']
report=json.loads((B/'installation/install_report.json').read_text(encoding='utf8'))
changed=sum(r['sha256']!=r['before_sha256'] for r in report['records'])
assert changed==14
text=f'''# 男性原图完整还原（2026-11-05）

状态：原图像素拆层版已接入，游戏检查通过；美术观感待用户评价。上一轮 v5 的工程通过不代表还原到位，本轮覆盖其男性头部素材。

用户已明确选择“按原图像素拆层，优先忠实还原”。三联原图是外观母版。两眼、三眉、一鼻、一嘴共七张素材的全部非透明像素 RGB 与各自原图同坐标完全相等，没有几何缩放；边缘使用透明度过渡。可见发丝优先保留原图 RGB，生成图供分离遮罩、被遮住的后发、无五官脸底和原图裁切以外发尾使用。不能宣称整张合成人像逐像素等同原图：共用脸底、共用鼻嘴及不可见区域补绘均有差异。

本轮实改14张男性头部 PNG。七套前衣及七张后领文件保持，女性22张与空面饰保持。统一1024×1536 RGBA、共同原点、零运行时偏移。鼻子包括山根、鼻梁、鼻尖、鼻翼、鼻孔，层级位于眉眼下方。

颈领问题核查：此前图片画布始终相同，但脸底内部轮廓改变；第550行不透明颈宽曾由149变165再变175像素。本轮固定连续颈部支撑，测量点记录于 `rig/current_face_neck.json`，衣领保持原位；七套衣服均在浅底／深底且不靠头发遮挡的条件下检查。

验证：{result['checks']}项游戏检查通过，男女各{result['male_hair_feature_combinations']}种组合；检查活动纹理与候选像素、导入缓存、前后衣成对显示、鼻子与眉眼层级、选项切换与女性基线。另人工查看三套全身、九种前后发混搭与七套颈领。检查数量只证明列出的工程行为，不能代替用户的审美评价。

制作包：`mods/appearance/portrait_original_restore_20261105_v6/`。

- `source/reference_canvas_*.png`：同画布原图；`source/split_original.py`：用户授权的原像素拆层与共同坐标。
- `source/*prompts.json`：内置 image_gen 提示；`source/*raw.png`：原生结果；不用CLI生成。
- `installation/before_male/`：替换前男性文件；配置快照与 `current_active_baseline.json`：安装前核对依据。
- `qa/final_review.json`：七件五官逐像素核对与人工查看范围；`qa/final_original_comparison.png`：同倍率原图／当前合成对照。
- `qa/new_face_seven_clothes_light.png` / `_dark.png`：七套颈领；`qa/nine_hair_mixes_dark.png`：混搭。
- `installation/install_report.json` / `runtime_check.json`：实际安装与游戏检查记录。
'''
(P/'docs/美术规范/男性原图完整还原_20261105/README.md').write_text(text,encoding='utf8')
for file,prefix in [('docs/README.md',''),('docs/策划案摘要/README.md','../')]:
 p=P/file;s=p.read_text(encoding='utf8');lines=s.splitlines()
 line=f'男性原图像素拆层（2026-11-05，已接入、实机通过）：用户明确选原像素拆层；七件眉眼鼻嘴保留原坐标与RGB，头发优先保留原可见像素，遮挡区及原图外发尾补绘。14件头部替换，七套衣服与女性保持；固定颈领，鼻子与山根在眉眼下。{result["checks"]}项检查通过，视觉待评价。[本轮来源与对照]({prefix}美术规范/男性原图完整还原_20261105/README.md)。此前重生成方案转历史。'
 lines=[line if x.startswith('男性原图还原（2026-11-05') or x.startswith('男性原图像素拆层（2026-11-05') else x for x in lines]
 p.write_text('\n'.join(lines)+'\n',encoding='utf8')
print(f'Documentation updated; actual changed PNG: {changed}')
