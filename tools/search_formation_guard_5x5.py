"""Exact finite-grid placement/routing search. No game or bitmap files are altered."""
import sys, json, pathlib, time
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / '.godot/test-tools/formation_solver'))
import z3 as z

N=5; POWER=9; use_not='--no-not' not in sys.argv
# side N/E/S/W; canonical signal nets are independent physical red networks.
parts={
 'state':(1,1,[(0,0,1,1,False)]),
 'compare':(2,1,[(0,0,3,1,True),(1,0,1,2,False)]),
 'branch':(1,1,[(0,0,3,2,True),(0,0,1,3,False),(0,0,0,4,False)]),
 'guard':(1,2,[(0,0,0,3,True),(0,1,2,POWER,True)]),
 'cast':(1,1,[(0,0,1,6,False)]),
 'gate':(1,1,[(0,0,3,6,True),(0,0,0,5 if use_not else 4,True),(0,0,1,7,False)]),
 'fire':(1,2,[(0,0,0,7,True),(0,1,2,POWER,True)]),
 'source':(2,1,[(1,0,1,POWER,False)]),
}
if use_not: parts['invert']=(1,1,[(0,0,3,4,True),(0,0,1,5,False)])
s=z.Solver(); s.set(timeout=180000)
poses={}; occupied=[]; ports=[]
for name,(w,h,ps) in parts.items():
 x,y,r=z.Ints(name+'_x '+name+'_y '+name+'_r'); poses[name]=(x,y,r)
 ww=z.If(r%2==0,w,h); hh=z.If(r%2==0,h,w)
 s.add(x>=0,y>=0,r>=0,r<4,x+ww<=N,y+hh<=N)
 occupied.append((name,x,y,ww,hh))
 for lx,ly,d,net,inp in ps:
  options=[]; cw,ch=w,h
  for rotation in range(4):
   options.append((lx,ly,d)); lx,ly=ch-1-ly,lx; cw,ch=ch,cw; d=(d+1)%4
  choose=lambda i: z.Sum([z.If(r==rotation, option[i], 0) for rotation,option in enumerate(options)])
  px,py,pd=x+choose(0),y+choose(1),choose(2)
  nx=px+z.If(pd==1,1,z.If(pd==3,-1,0)); ny=py+z.If(pd==2,1,z.If(pd==0,-1,0))
  ports.append((name,px,py,pd,nx,ny,net,inp))
s.add(poses['state'][2]==0)
for i,(_,x,y,w,h) in enumerate(occupied):
 s.add(z.Or(x+ w<=2,x>2,y+h<=2,y>2))
 for _,a,b,c,d in occupied[i+1:]: s.add(z.Or(x+w<=a,a+c<=x,y+h<=b,b+d<=y))
nets=sorted(set(p[6] for p in ports)); cells={(x,y):z.Int(f'v{x}_{y}') for y in range(N) for x in range(N)}
rank={(x,y):z.Int(f'rank{x}_{y}') for x,y in cells}
def at(x,y): return z.Sum([z.If(z.And(x==cx,y==cy),v,0) for (cx,cy),v in cells.items()])
def point(p,x,y): return z.And(p[4]==x,p[5]==y)
for (x,y),v in cells.items():
 s.add(z.Or(v==0,*[v==net for net in nets]),rank[x,y]>=0,rank[x,y]<=25)
 occ=z.Or(z.And(x==2,y==2),*[z.And(x>=a,x<a+w,y>=b,y<b+h) for _,a,b,w,h in occupied])
 s.add(z.Implies(occ,v==0))
 neighbours=[(a,b) for a,b in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)] if (a,b) in cells]
 for a,b in neighbours:
  s.add(z.Implies(z.And(v>0,v!=POWER,cells[a,b]>0,cells[a,b]!=POWER),v==cells[a,b]))
 for net in nets:
  degree=z.Sum([z.If(cells[a,b]==net,1,0) for a,b in neighbours]+[z.If(point(p,x,y),1,0) for p in ports if p[6]==net])
  s.add(z.Implies(v==net,z.Or(degree==2,degree==3) if net==POWER else degree==2))
  roots=[point(p,x,y) for p in ports if p[6]==net and not p[7]]
  lower=[z.And(cells[a,b]==net,rank[a,b]<rank[x,y]) for a,b in neighbours]
  s.add(z.Implies(v==net,z.Or(*(roots+lower))))
for p in ports:
 direct=[z.And(p[4]==q[1],p[5]==q[2],(p[3]+2)%4==q[3]) for q in ports if p[6]==q[6] and p[7]!=q[7]]
 s.add(z.Or(at(p[4],p[5])==p[6],*direct))
 if p[6]!=POWER: s.add(z.Implies(z.And(at(p[4],p[5])>0,at(p[4],p[5])!=POWER),at(p[4],p[5])==p[6]))
start=time.time(); result=s.check(); print(result, 'seconds=',round(time.time()-start,2),flush=True)
if result==z.sat:
 m=s.model(); val=lambda a: m.eval(a).as_long()
 data={'size':N,'normally_closed':not use_not,'eye':[2,2],
       'components':{name:[val(x),val(y),val(r)] for name,(x,y,r) in poses.items()},
       'wires':[[x,y,val(v)] for (x,y),v in cells.items() if val(v)>0]}
 print(json.dumps(data,indent=2),flush=True)
 path=pathlib.Path(__file__).resolve().parents[1]/'.godot/test-results/formation_guard_search'
 path.mkdir(parents=True,exist_ok=True); (path/('with_not.json' if use_not else 'normally_closed.json')).write_text(json.dumps(data,indent=2),encoding='utf8')
