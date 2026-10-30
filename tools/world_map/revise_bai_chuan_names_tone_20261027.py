"""Apply the user's broader xianxia naming direction to current planning artifacts."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
changes={'伏岚观':'镇岳观','栖霞谷':'百药谷','澄渊水府':'沧溟水府','九幽教':'血河教','镜月宫':'蚀日宫','枯禅寺':'枯骨寺','云帆城':'镇海城','汀兰城':'临渊城','清嘉城':'承天城','承露城':'金阙城','永夜城':'永夜关','蘅芜':'万毒谷','修篁深处':'盘龙藤海','菁华药圃':'长生药圃','扶疏灵林':'古木禁林','清露原':'玉髓原','含英谷':'断岳矿山','幽苔泽':'黑瘴大泽','寒璧古台':'镇星台','松声入壑':'千仞沟','尘外':'绝仙谷','空明泽':'雷泽','熹微谷':'葬日谷','潋滟滩':'碎星滩'}
def replace(s):
 for a,b in changes.items():s=s.replace(a,b)
 return s
names=['百川宗门站队与城镇轮值及资源节点规划_20261027.md','百川区域仙盟大势力与天人议盟规划.md','百川势力与节点分布规划_20261027.md','百川城镇与野外节点名录草案_20261027.md','大陆格局与百川区域设计.md','百川固定方格世界地图设计.md','百川知名资源产地与宗门供给规划_20261027.md','百川命名与资源归属修订_20261027.md']
for name in names:
 p=ROOT/'docs/plans'/name;s=p.read_text(encoding='utf-8');marker='## 七、玉虚仙宗内景（首期）'
 if marker in s:
  a,b=s.split(marker,1);s=replace(a)+marker+b
 else:s=replace(s)
 p.write_text(s,encoding='utf-8')
p=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(replace(p.read_text(encoding='utf-8')))
for n in d['nodes']:
 if n['name']=='万毒谷':n['terrain']='药谷，含有毒生境与可开放采集区域；名称不新增毒性材料或效果'
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
p=ROOT/'docs/plans/百川命名与资源归属修订_20261027.md'
s=p.read_text(encoding='utf-8')
s=s.replace('地方宗门不齐字、不齐尾','地方宗门保留不同道统气质')
s=s.replace('以山称宗的独立传承，药圃、泉源与养生修持','以山称宗的独立传承，药圃、泉源与内炼修持')
s=s.replace('府院、世家与经营网络','府院、世家与经营网络')
start=s.index('## 三、地点名长短错落')
end=s.index('## 四、资源归属的初步分配')
s=s[:start]+'''## 三、地名有清浊险正，不统一追求柔美

最新命名口径来自用户：放开诗词限制，只要有修仙味道。保留玉虚道宗、玄都仙府、无相圣宗三个一级名称；其余混合宗、观、山、谷、宫、书院、水府、教、寺，兼有正统、古修、丹道、魔道与异端气质。

十地方势力为太微道宫、镇岳观、抱朴山、百药谷、丹鼎宗、归藏书院、沧溟水府、血河教、蚀日宫、枯骨寺。镇岳体现山门威势，沧溟突出水府深远，血河、蚀日、枯骨分别呈现魔修气质；名称不自动引入新属性、流派机制或既定恶行。

城市采用承天城、金阙城、镇海城、临渊城、永夜关等较有分量的名称，也保留白石城、赤水城、溪云城等朴实或清雅名称。永夜关仍按城市级聚落设计，不额外增加一类节点。

野外与产地采用断岳矿山、古木禁林、黑瘴大泽、长生药圃、盘龙藤海、葬日谷、绝仙谷、镇星台、千仞沟、雷泽等不同长度和气质；同时保留涵虚泽、蒹葭浦、濯缨泉等柔美山水。险名不等于必有首领，禁林不等于所有成员均禁止进入，长生与玉髓不额外生成寿元效果或新材料。万毒谷的既有药材池先保留，毒瘴生态与风险内容另配。

这轮只规范现行方案名称，旧名映射由修订工具中的逐项表保留，避免把改名当成新增地点。一级近域分别为太和城—洗心镇—松月坊、金阙城—含光镇—琼华坊、永夜关—听澜镇—夜泊坊，外围配置野外和产地。

'''+s[end:]
s=s.replace('林谷道场，药材和藤木灵林','药道山谷，药材、险谷和藤木灵林').replace('古寺道统，自然蜕壳与灵石合营','古寺异修，自然蜕壳与灵石合营')
p.write_text(s,encoding='utf-8')
print('Updated current planning names; runtime lore unchanged.')
