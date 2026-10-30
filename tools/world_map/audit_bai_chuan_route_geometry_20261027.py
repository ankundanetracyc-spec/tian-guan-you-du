"""Check that rendered route geometry matches node connectivity and landmarks."""
import json, itertools, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
d=json.loads((ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json').read_text(encoding='utf-8'))
nodes={n['id']:n for n in d['nodes']+d['transitNodes']};segments={}
for route in d['routes']:
 chain=[route['from']]+route['via']+[route['to']]
 assert route['points']==[nodes[i]['pos'] for i in chain]
 assert len(chain)==len(set(chain)),route['id']
 for a,b in zip(chain,chain[1:]):
  assert math.dist(nodes[a]['pos'],nodes[b]['pos'])>1e-5
  segments[tuple(sorted([a,b]))]=(nodes[a]['pos'],nodes[b]['pos'])
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
crossings=[]
for (ids1,(a,b)),(ids2,(c,e)) in itertools.combinations(segments.items(),2):
 if set(ids1)&set(ids2):continue
 if cross(a,b,c)*cross(a,b,e)<-1e-7 and cross(c,e,a)*cross(c,e,b)<-1e-7:crossings.append([ids1,ids2])
assert not crossings,crossings
features=d['terrainPlanning']['crossingLandmarks']
assert len({f['id'] for f in features})==len(features)
assert sum(f['sourceType']=='water' for f in features)==15
assert sum(f['sourceType']=='ridge' for f in features)==13
assert all(nodes[f['nodeId']]['pos']==f['pos'] for f in features)
assert all(nodes[f['nodeId']]['type']=='transit' and nodes[f['nodeId']]['name'] is None for f in features)
assert all(any(f['nodeId'] in r['via'] for r in d['routes']) for f in features)
if 'roadCurveGeometry' in d:
 from curve_bai_chuan_roads_20261027 import audit_curves
 curves=d['roadCurveGeometry']['edges']
 assert len(curves)==len(segments)
 assert not audit_curves(curves)
 for route in d['routes']:
  rendered=[]
  for step in route['movementSegments']:
   edge=curves[step['edge']];points=list(reversed(edge['points'])) if step['reverse'] else edge['points']
   rendered.extend(points if not rendered else points[1:])
  assert rendered==route['renderPoints'],route['id']
 # A curved road may cross a river only at an existing bridge location.
 bridges=[f['pos'] for f in features if f['sourceType']=='water']
 def intersection(a,b,c,e):
  ab=[b[i]-a[i] for i in (0,1)];ce=[e[i]-c[i] for i in (0,1)];ca=[c[i]-a[i] for i in (0,1)]
  det=ab[0]*ce[1]-ab[1]*ce[0]
  if abs(det)<1e-10:return None
  t=(ca[0]*ce[1]-ca[1]*ce[0])/det;u=(ca[0]*ab[1]-ca[1]*ab[0])/det
  if 0<t<1 and 0<u<1:return [a[i]+t*ab[i] for i in (0,1)]
  return None
 for edge in curves.values():
  for a,b in zip(edge['points'],edge['points'][1:]):
   for water in d['terrainPlanning']['waters']:
    for c,e in zip(water['points'],water['points'][1:]):
     point=intersection(a,b,c,e)
     assert point is None or min(math.dist(point,p) for p in bridges)<.02,('unbridged crossing',point)
print(f'Geometry verified: {len(segments)} shared edges, matching render/movement curves, 15 bridge locations, 13 passes, no unmarked road crossings.')
