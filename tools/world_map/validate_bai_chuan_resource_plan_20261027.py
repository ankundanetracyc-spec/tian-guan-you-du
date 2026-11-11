"""Synchronize resource proposal fields and check planning references."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(p.read_text(encoding='utf-8'))
s=(ROOT/'docs/plans/世界与探索/百川命名与资源归属修订_20261027.md').read_text(encoding='utf-8')
rows={}
for line in s.splitlines():
 c=[x.strip() for x in line.split('|')[1:-1]]
 if len(c)==5 and c[1] in ['大型','较大','较小']:rows[c[0]]=c
assert len(rows)==21
for n in d['nodes']:
 if n.get('resourceSite') or n['type']=='resource':
  c=rows[n['name']];n.update(scale=c[1],owner=c[2],supplyNote=c[4])
assert len(d['nodes'])==123
known={n['name'] for n in d['nodes']}
assert len(known)==123
assert all(0<=v<=100 for n in d['nodes'] for v in n['pos'])
assert all(a in known for g in d['governance'].values() for a in g['order'])
assert all(a in known for n in d['nodes'] if n.get('resourceSite') or n['type']=='resource' for a in n['supply'].split('、'))
assert d['detachedNodes'][0]['connections']==[]
assert sum(n.get('resourceSite',False) for n in d['nodes'])==21
assert sum(n['type']=='wild' for n in d['nodes'])==70
ids={n['id'] for n in d['nodes']}
transit={n['id'] for n in d['transitNodes']}
assert len(transit)==d['count']['transit'] and len(d['routes'])==d['count']['routes']
adj={i:set() for i in ids|transit}
for r in d['routes']:
 chain=[r['from']]+r['via']+[r['to']]
 assert r['from'] in ids|transit and r['to'] in ids|transit and r['via']
 for a,b in zip(chain,chain[1:]):
  assert a in adj and b in adj
  adj[a].add(b);adj[b].add(a)
seen=set();todo=[next(iter(ids))]
while todo:
 current=todo.pop()
 if current in seen:continue
 seen.add(current);todo.extend(adj[current]-seen)
assert len(seen)==d['count']['graphTotal']
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Validated 21 resource wilds, ownership and supply references, all {len(seen)} route nodes connected, {len(d["routes"])} routes, and detached alliance.')
