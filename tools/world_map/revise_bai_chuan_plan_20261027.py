"""One-time revision of planning documents and planning data, never runtime assets."""
import json, math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
P = ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
rename = {
 '玉虚仙宗':'玉虚道宗','玄都天府':'玄都仙府',
 '抱朴宗':'抱朴山','丹霞宗':'丹鼎宗','归藏院':'归藏书院','澄渊宫':'澄渊水府','幽泉教':'九幽教','照影宫':'镜月宫','灰骨寺':'枯禅寺',
 '云津城':'通济城','归云镇':'临皋镇','烟霞城':'绛阙城','照夜城':'永夜城','泉隐坊':'忘机坊',
 '青芝圃':'菁华药圃','青木岫':'扶疏灵林','听雨竹坞':'修篁深处','天籁峡':'天籁长峡','蘅芜谷':'蘅芜',
 '赤霞谷':'朱明谷','流霞滩':'潋滟滩','青岚岭':'浮翠岭','青冥泽':'空明泽','白石涧':'浣纱涧','照影潭':'疏影潭',
 '景云岭':'垂虹岭','云汀原':'芙蓉原','离照谷':'熹微谷','听松谷':'松声入壑','绝尘谷':'尘外','地火洞':'地火窟','寒璧台':'寒璧古台','藏真洞':'藏真洞天',
}
def names(s):
 for a,b in rename.items(): s=s.replace(a,b)
 return s
d=json.loads(names(P.read_text(encoding='utf-8')))
positions={n['name']:n['pos'] for n in d['nodes']}
for group,delta in zip(d['neighborhoods'],[(4,-1),(12,-1),(4,-13)]):
 keys=[group['major'],group['city'],group['town'],group['market'],group['res'][0]]+[w[0] for w in group['wild']]
 for name in keys:
  old=positions[name];positions[name]=[old[0]+delta[0],old[1]+delta[1]]
positions.update({'丹鼎宗':[55,10],'镜月宫':[82,88],'暮烟城':[76,71]})
for n in d['nodes']:
 if n['name'] in positions:n['pos']=positions[n['name']]
d['nodes']=[n for n in d['nodes'] if n['type'] not in ('memory','council')]
d['detachedNodes']=[{'name':'仙盟','type':'alliance','pos':None,'connections':[],'note':'独立节点，暂不与外界相接；进入方式未定。'}]
for s in d['sects']:s['pos']=positions[s['name']]
for name,g in d['governance'].items():g['pos']=positions[name]
for row in d['resources']:row[-1]=positions[row[0]]
for g in d['neighborhoods']:
 for key in ('city','town','market'):g[key+'Pos']=positions[g[key]]
 for w in g['wild']:w[1]=positions[w[0]]
 g['res'][-1]=positions[g['res'][0]]
for n in d['nodes']:
 if n['type']=='resource':n['note']='知名产地候选；归属、成员便利和宗门供给见资源主案，非自动领奖点。'
d['count'].update(memory=0,council=0,allianceDetached=1,worldTotal=123,total=124)
d['note']='现行命名与相对布局工作稿；123处世界地点，另1仙盟独立节点；21知名资源产地，无额外小采集点。'
assert len(d['nodes'])==123
assert len({n['name'] for n in d['nodes']})==123
P.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
files=[ROOT/'docs/plans'/n for n in ['百川宗门站队与城镇轮值及资源节点规划_20261027.md','百川区域仙盟大势力与天人议盟规划.md','百川势力与节点分布规划_20261027.md','百川城镇与野外节点名录草案_20261027.md','大陆格局与百川区域设计.md','百川固定方格世界地图设计.md','百川知名资源产地与宗门供给规划_20261027.md']]
notice='> 2026-10-27 本轮最新修订：[命名、资源归属与供给方案](百川命名与资源归属修订_20261027.md)优先。一级采用玉虚道宗、玄都仙府、无相圣宗；二级与地名不强求齐字。取消青溪里世界节点，仙盟独立且暂不相接。123处世界地点＋1仙盟；21处知名产地，暂不铺设额外小资源点。旧文中125处、青溪里锚点与盟府地理落点均已替代。运行时与既有剧情名称未迁移。\n\n'
for p in files:
 s=p.read_text(encoding='utf-8')
 # Existing 玉虚内景 appendix is historical implementation documentation; keep it intact.
 marker='## 七、玉虚仙宗内景（首期）'
 if marker in s:
  head,tail=s.split(marker,1);s=names(head)+marker+tail
 else:s=names(s)
 for sect in d['sects']:
  if sect['name'] in positions:
   import re
   s=re.sub(r'(\| '+re.escape(sect['name'])+r' \| )\d+／\d+( \|)',lambda m:m[1]+str(sect['pos'][0])+'／'+str(sect['pos'][1])+m[2],s)
 s=s.replace('| 盟府议场 | 1 |\n| 青溪里旧地 | 1 |','| 仙盟（独立，暂不相接） | 1 |').replace('| **合计** | **125** |','| **合计** | **124** |')
 p.write_text(s.split('\n',1)[0]+'\n\n'+notice+s.split('\n',1)[1].lstrip('\n'),encoding='utf-8')
for p,prefix in [(ROOT/'docs/README.md','plans/'),(ROOT/'docs/策划案摘要/README.md','../plans/')]:
 s=p.read_text(encoding='utf-8');lines=s.splitlines();lines[2]='百川本轮修订（2026-10-27）：[命名与资源归属方案]('+prefix+'百川命名与资源归属修订_20261027.md)、[资源供给主案]('+prefix+'百川知名资源产地与宗门供给规划_20261027.md)。三大为玉虚道宗、玄都仙府、无相圣宗，十地方宗门独立站队；地名长短错落。123处世界地点，另1个不相接的仙盟；取消青溪里世界锚点。21处为知名资源产地候选，不扩铺小采集点；归属可因纷争改变，成员有便利，宗门按产地供给定期开放资源。份额、周期、价格未定，运行时未改。';p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
majors=[s for s in d['sects'] if s['level']==1]
print(json.dumps({'worldNodes':len(d['nodes']),'detached':len(d['detachedNodes']),'majorDistances':[round(math.dist(a['pos'],b['pos']),1) for i,a in enumerate(majors) for b in majors[i+1:]],'nearestMinor':round(min(math.dist(a['pos'],b['pos']) for a in majors for b in d['sects'] if b['level']==2),1)},ensure_ascii=True))
