import json,math,random
from pathlib import Path
ms=json.loads(Path('outputs/alchemy_materials_20261023.json').read_text(encoding='utf-8'));ms=[m for m in ms if '骨' not in m['name']]
S=[0,1,1.65,2.9,5.4,10.7,22.6,50.9,121,304];ys={'纤维':.85,'致密':.75,'颗粒':.8,'黏胶':.9,'液体':.95,'气体':.9}
allow=[set('镇'),set('聚固'),set('破化固镇护明藏'),set('破化固镇护明藏'),set('通行引藏'),set('通行引藏')]
main=next(i for i,m in enumerate(ms) if m['name']=='泥脉石团')
pools=[[main]]+[[i for i,m in enumerate(ms) if set(m['u'])&a] for a in allow[1:]]
def calc(xs):
 raw=[100*d for i,d,r in xs]; av=[raw[j]*ys[ms[i]['texture']] for j,(i,d,r) in enumerate(xs)];res=[r-a for r,a in zip(raw,av)]
 ps={}
 f=0
 for i,d,r in xs:
  for u,g in ms[i]['u'].items():
   if u in allow[r]:
    f+=1
    if r:ps[u]=ps.get(u,0)+d*S[g]
 for u,limit,eligible in [('破',.1,[j for j,(i,d,r) in enumerate(xs) if ms[i]['texture']=='致密']),('化',.15,list(range(len(xs))))]:
  amt=min(sum(res[j] for j in eligible),limit*sum(raw[j] for j in eligible),.05*ps.get(u,0)*sum(raw[j] for j in eligible))
  for j in sorted(eligible,key=lambda j:ys[ms[xs[j][0]]['texture']]):
   t=min(amt,res[j]);av[j]+=t;res[j]-=t;amt-=t
 c=min(4,max(1,math.floor(av[0]/25*min(1,.75+.05*ps.get('聚',0)))))
 total=sum(av);neg=sum(av[j] for j,(i,d,r) in enumerate(xs) if ms[i]['polarity']<0);pos=sum(av[j] for j,(i,d,r) in enumerate(xs) if ms[i]['polarity']>0)
 z=max(0,200*min(neg,pos)/total-10*ps.get('镇',0));risk=max(0,z-5*ps.get('护',0))
 con=min(1,.7+.1*ps.get('通',0)) if len(xs)>1 else 1;delivery=min(1,.6+.1*ps.get('行',0));fac=con*(1-.002*z)*delivery
 delivered=total*fac;carry=sum(res)*max(0,.15-.1*ps.get('明',0));J=carry/(delivered+carry)
 pure=0 if J>.06 else 20+80*min(1,max(0,(.06-J)/.057))
 vec={e:sum(av[j] for j,(i,d,r) in enumerate(xs) if ms[i]['element']==e)/total for e in '木火土金水'}
 dist=.5*sum(abs(vec[e]-t) for e,t in zip('木火土金水',[.3,.05,.25,.1,.3]))
 k=max(.75,min(1.25,1+.25*(.5-dist)+.125*(vec['火']-vec['木'])))
 mass=av[0]*fac/c;power=mass*k*min(1.25,1+.05*ps.get('引',0))
 core=0 if mass<12 or power<8 else min(20+80*min(1,((mass-12)/10)**(1/1.15)),20+80*min(1,((power-8)/10)**(1/1.2)))
 dev=abs((pos-neg)/total+.5);yin=100 if dev<=.16 else 0 if dev>.68 else 20+80*(.68-dev)/.52
 solid=0
 for j,(i,d,r) in enumerate(xs):
  if r:solid+=av[j]*fac/c*k*(S[ms[i]['u'].get('固',0)] if '固' in allow[r] else 0)
  if r in (2,3):solid+=av[j]*fac/c*k*.65*S[ms[i]['u'].get('藏',0)]
 stable=min(pure,45+55*min(1,solid/3)**(1/1.15))
 L=len(xs)+2*f+max(0,(sum(d for i,d,r in xs)-len(xs))//3)
 return [core,yin,100*con,stable],dict(c=c,L=L,fac=fac,k=k,J=J,risk=risk,mass=mass,power=power,pure=pure,solid=solid)
def score(xs):
 a,d=calc(xs)
 if d['L']>24 or d['risk']>=90 or min(a)<20:return -1,d
 eff=1
 for j in range(1,len(xs)):
  if 100*xs[j][1]*ys[ms[xs[j][0]]['texture']]/sum(100*z[1]*ys[ms[z[0]]['texture']] for z in xs)<.05:continue
  b,_=calc(xs[:j]+xs[j+1:])
  if any(x<99.999 and x-y>=1 for x,y in zip(a,b)):eff+=1
 cap=[0,50,75,90,99,100,100][eff]
 return math.floor(min(cap,*a)),dict(d,axes=a,eff=eff)
random.seed(100)
best=-1;winners=[]
for it in range(100000):
 roles=[0]+[r for r in range(1,6) if random.random()<.8]
 ids=[random.choice(pools[r]) for r in roles]
 if len(set(ids))!=len(ids):continue
 doses=[1]*len(roles)
 for _ in range(random.randint(0,8-len(roles))):
  j=random.randrange(len(roles))
  if doses[j]<4:doses[j]+=1
 xs=list(zip(ids,doses,roles));q,d=score(xs)
 if q>best:
  best=q;winners=[(xs,d)];print('BEST',it,q,[(ms[i]['name'],dose,r) for i,dose,r in xs],d,flush=True)
 elif q==best and len(winners)<20:winners.append((xs,d))
 if best>=99:break
Path('outputs/alchemy_optimal_20261023.json').write_text(json.dumps(dict(best=best,winners=[dict(inputs=[dict(name=ms[i]['name'],dose=d,role=r) for i,d,r in xs],metrics=m) for xs,m in winners]),ensure_ascii=False,indent=2),encoding='utf-8')
