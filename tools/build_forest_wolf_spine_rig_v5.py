"""Build independent anatomical meshes and planted-foot IK for Spine 3.8.

The generated atlas is never repainted or cut. Polygon UV boundaries isolate
each illustration, and each joint has its own bind transform.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / 'textures/characters/forest_wolf_spine_rig_v5'
OUT = PACKAGE / 'runtime'
W, H = 1254, 1254
bones, bind, indices = [], {}, {}


def rotate(p, a):
    a = math.radians(a)
    return (p[0]*math.cos(a)-p[1]*math.sin(a), p[0]*math.sin(a)+p[1]*math.cos(a))


def add(name, parent, screen, angle=0, length=0, fixed=False):
    p = (screen[0]-760, 996-screen[1])
    b = {'name': name}
    if parent:
        b['parent'] = parent
        pp, pa = bind[parent]
        local = rotate((p[0]-pp[0], p[1]-pp[1]), -pa)
        b.update(x=local[0], y=local[1], rotation=angle-pa)
        if fixed:
            b.update(transform='noRotationOrReflection', rotation=angle)
    if length:
        b['length'] = length
    indices[name] = len(bones)
    bones.append(b)
    bind[name] = (p, angle)


def rect(x1,y1,x2,y2):
    return [(x1,y1),(x2,y1),(x2,y2),(x1,y2)]


def triangulate(poly):
    # Ear clipping keeps concave atlas boundaries away from adjacent parts.
    ids = list(range(len(poly)))
    area = sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in ids)
    if area < 0:
        ids.reverse()
    def cross(a,b,c):
        return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    tris=[]
    while len(ids)>3:
        for j in range(len(ids)):
            a,b,c=ids[j-1],ids[j],ids[(j+1)%len(ids)]
            if cross(poly[a],poly[b],poly[c])<=0:
                continue
            if any(all(cross(poly[u],poly[v],poly[k])>=-1e-8 for u,v in [(a,b),(b,c),(c,a)]) for k in ids if k not in (a,b,c)):
                continue
            tris.append((a,b,c));ids.pop(j);break
        else:
            raise ValueError('Invalid UV polygon')
    tris.append(tuple(ids))
    return tris


def subdivide(points, tris, depth):
    points=list(points)
    for _ in range(depth):
        mids={}; out=[]
        def midpoint(a,b):
            key=tuple(sorted((a,b)))
            if key not in mids:
                mids[key]=len(points)
                points.append(((points[a][0]+points[b][0])/2,(points[a][1]+points[b][1])/2))
            return mids[key]
        for a,b,c in tris:
            ab,bc,ca=midpoint(a,b),midpoint(b,c),midpoint(c,a)
            out.extend([(a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)])
        tris=out
    return points,tris


add('root',None,(760,996))
add('body_control','root',(760,996))
add('hips','body_control',(1015,460))
add('torso','hips',(560,495))
add('neck','torso',(480,345))
add('head','neck',(375,300))
LEGS={
    'front_near':dict(parent='torso',poly=rect(950,426,1132,818),
        src=[(1030,478),(1060,578),(1070,750)],pos=(560,495),scale=(1.3,1.3)),
    'front_far':dict(parent='torso',poly=rect(135,843,318,1234),
        src=[(225,910),(250,1005),(260,1170)],pos=(465,445),scale=(.95,.95)),
    'hind_near':dict(parent='hips',poly=rect(575,840,780,1233),
        src=[(685,900),(645,1020),(714,1095),(687,1172)],pos=(1015,460),scale=(1.15,1.25)),
    'hind_far':dict(parent='hips',poly=rect(975,840,1193,1234),
        src=[(1080,905),(1045,1035),(1135,1100),(1110,1175)],pos=(920,420),scale=(.9,.95)),
}
ik=[]
for n,c in LEGS.items():
    src=c['src'];p=c['pos'];scale=c['scale']
    points=[(p[0]+(s[0]-src[0][0])*scale[0],p[1]+(s[1]-src[0][1])*scale[1]) for s in src]
    c['world']=points
    names=[n+'_upper',n+'_lower']+([n+'_ankle'] if n.startswith('hind') else [])
    for i,b in enumerate(names):
        a,z=points[i:i+2]
        angle=math.degrees(math.atan2(a[1]-z[1],z[0]-a[0]))
        add(b,c['parent'] if i==0 else names[i-1],a,angle,math.dist(a,z))
    add(n+'_paw',names[-1],points[-1],0,fixed=True)
for n,c in LEGS.items():
    hind=n.startswith('hind')
    add(n+'_target','root',c['world'][-1])
    if hind:
        add(n+'_hock_target',n+'_target',c['world'][-2])
    ik.append({'name':'ik_'+n,'order':len(ik),'bones':[n+'_upper',n+'_lower'],
               'target':n+('_hock_target' if hind else '_target'),
               'bendPositive':hind,'mix':1,'softness':2})
    if hind:
        ik.append({'name':'ik_'+n+'_ankle','order':len(ik),'bones':[n+'_ankle'],'target':n+'_target','mix':1})
add('tail_base','hips',(1025,380))
add('tail_mid','tail_base',(1210,560))
add('tail_tip','tail_mid',(1320,750))


slots=[];attachments={}
def mesh(name, poly, pivot, position, scale, weight, depth=0, extra_angle=0, mapper=None):
    points,tris=subdivide(poly,triangulate(poly),depth)
    vertices=[]
    for x,y in points:
        delta=rotate(((x-pivot[0])*scale[0],-(y-pivot[1])*scale[1]),extra_angle)
        world=(position[0]-760+delta[0],996-position[1]+delta[1])
        if mapper:
            sx,sy=mapper(x,y)
            world=(sx-760,996-sy)
        weights={k:v for k,v in weight(x,y).items() if v>1e-6}
        vertices.append(len(weights))
        for bone,w in weights.items():
            bp,ba=bind[bone]
            local=rotate((world[0]-bp[0],world[1]-bp[1]),-ba)
            vertices.extend([indices[bone],round(local[0],5),round(local[1],5),round(w,6)])
    attachment={'type':'mesh','path':'parts','uvs':[v for x,y in points for v in (x/W,y/H)],
                'triangles':[v for t in tris for v in t],'vertices':vertices,
                'hull':len(poly),'width':W,'height':H}
    assert len(points)<1024 and len(tris)*3<=3072
    slots.append({'name':name,'bone':'root','attachment':name})
    attachments[name]={name:attachment}


def smooth(v,a,b):
    t=max(0,min(1,(v-a)/(b-a)))
    return t*t*(3-2*t)


def leg(n):
    c=LEGS[n];src=c['src']
    chain=[n+'_upper',n+'_lower']+([n+'_ankle'] if len(src)==4 else [])+[n+'_paw']
    def weights(x,y):
        blend=[smooth(y,s[1]-22,s[1]+22) for s in src[1:]]
        w={chain[0]:1-blend[0]}
        for i in range(1,len(chain)-1):
            w[chain[i]]=blend[i-1]*(1-blend[i])
        w[chain[-1]]=blend[-1]
        proximal=(1-smooth(y,src[0][1]+5,src[0][1]+65))*.8
        w={b:v*(1-proximal) for b,v in w.items()}
        w[c['parent']]=proximal
        return w
    mesh(n,c['poly'],src[0],c['pos'],c['scale'],weights,4)


def tailweights(x,y):
    a=smooth(y,485,620);b=smooth(y,650,775)
    return {'tail_base':1-a,'tail_mid':a*(1-b),'tail_tip':a*b}
mesh('tail',rect(452,426,845,818),(500,480),(1025,380),(1.1,1.1),tailweights,4)
leg('hind_far');leg('front_far')
# One continuous mesh per whole leg. No circular knee caps or separate boots.
leg('hind_near');leg('front_near')
mesh('body',rect(10,40,441,385),(245,240),(780,425),(1.8,1.05),
     lambda x,y:{'hips':smooth(x,140,360),'torso':1-smooth(x,140,360)},4)
for name,poly,pivot,scale in [
    ('closed',rect(454,10,839,407),(640,235),(1.08,1.08)),
    ('open',rect(866,10,1246,408),(1050,235),(1.08,1.08)),
    ('dead',rect(30,429,435,819),(220,650),(1.02,1.02))]:
    mesh('head_'+name,poly,pivot,(375,300),scale,lambda x,y:{'head':1})
head_attachments={n:attachments.pop('head_'+n)['head_'+n] for n in ('closed','open','dead')}
slots[:]=[s for s in slots if not s['name'].startswith('head_')]
slots.append({'name':'head','bone':'head','attachment':'closed'})
attachments['head']=head_attachments


def animation(duration, keys, heads=None):
    tracks={n:{'rotate':[{'time':0,'angle':0},{'time':duration,'angle':0}],
               'translate':[{'time':0,'x':0,'y':0},{'time':duration,'x':0,'y':0}]}
            for n in bind if n!='root'}
    for name,kind,values in keys:
        tracks[name][kind]=[dict(time=t,**({'angle':v} if kind=='rotate' else {'x':v[0],'y':v[1]})) for t,v in values]
    for timelines in tracks.values():
        for keys in timelines.values():
            for key in keys[:-1]:key['curve']=[.25,0,.75,1]
    return {'bones':tracks,'slots':{'head':{'attachment':[{'time':t,'name':n} for t,n in (heads or [(0,'closed')])]}}}


def rot(n,*v):return n,'rotate',v
def move(n,*v):return n,'translate',v


idle=animation(1.6,[move('body_control',(0,(0,0)),(.8,(0,7)),(1.6,(0,0))),
    rot('head',(0,0),(.8,-1),(1.6,0)),rot('tail_mid',(0,-2),(.8,3),(1.6,-2)),
    rot('tail_tip',(0,-2),(.8,4),(1.6,-2))])
attackkeys=[move('body_control',(0,(0,0)),(.18,(25,-22)),(.36,(-85,12)),(.55,(-35,-8)),(.82,(0,0))),
    rot('head',(0,0),(.18,6),(.36,-9),(.55,-3),(.82,0)),
    rot('neck',(0,0),(.18,3),(.36,-5),(.82,0)),
    rot('tail_base',(0,0),(.18,5),(.36,-7),(.82,0))]
for n in LEGS:
    fore=n.startswith('front')
    attackkeys.append(move(n+'_target',(0,(0,0)),(.18,(15,45 if fore else 0)),(.36,(-120,65 if fore else 3)),(.55,(-55,0)),(.82,(0,0))))
attack=animation(.82,attackkeys,[(0,'closed'),(.18,'open'),(.55,'closed')])
hurt=animation(.5,[move('body_control',(0,(0,0)),(.09,(40,-15)),(.2,(20,-6)),(.5,(0,0))),
    rot('head',(0,0),(.09,-10),(.2,-5),(.5,0)),rot('tail_base',(0,0),(.12,8),(.5,0))])
deathkeys=[move('body_control',(0,(0,0)),(.18,(20,-28)),(.6,(0,-170)),(1.05,(-15,-300)),(1.4,(-15,-300))),
    rot('torso',(0,0),(.6,4),(1.05,8),(1.4,8)),
    rot('head',(0,0),(.18,-10),(.6,10),(1.05,17),(1.4,17)),
    rot('neck',(0,0),(.6,5),(1.05,9),(1.4,9)),
    move('neck',(0,(0,0)),(.6,(0,-60)),(1.05,(0,-150)),(1.4,(0,-150))),
    rot('tail_base',(0,0),(.6,12),(1.05,28),(1.4,28)),
    rot('tail_mid',(0,0),(.6,10),(1.05,20),(1.4,20)),
    rot('tail_tip',(0,0),(1.05,12),(1.4,12))]
for n,c in LEGS.items():
    hind=n.startswith('hind')
    ua=bind[n+'_upper'][1];la=bind[n+'_lower'][1]
    end_upper=-160 if hind else -150
    end_lower=0 if hind else -155
    upper_delta=end_upper-(0 if hind else 8)-ua
    lower_delta=end_lower-end_upper-(la-ua)
    # Forelegs reach forward along the ground instead of folding 150 degrees.
    for b,delta in [(n+'_upper',upper_delta),(n+'_lower',lower_delta)]:
        deathkeys.append(rot(b,(0,0),(.25,0),(.65,delta*.55),(1.05,delta),(1.4,delta)))
    if hind:
        aa=bind[n+'_ankle'][1]
        delta=-120-end_lower-(aa-la)
        deathkeys.append(rot(n+'_ankle',(0,0),(.25,0),(.65,delta*.55),(1.05,delta),(1.4,delta)))
death=animation(1.4,deathkeys,[(0,'closed'),(.6,'dead')])
for a in (idle,attack,hurt):
    a['ik']={c['name']:[{'time':0,'mix':1,'bendPositive':c.get('bendPositive',True)}] for c in ik}
death['ik']={c['name']:[{'time':t,'mix':m,'bendPositive':c.get('bendPositive',True)}
                      for t,m in [(0,1),(.25,1),(.65,0),(1.4,0)]] for c in ik}
data={'skeleton':{'spine':'3.8.99','hash':'forest_wolf_continuous_v5','x':-760,'y':-28,'width':1536,'height':1024},
      'bones':bones,'slots':slots,'ik':ik,
      'skins':[{'name':'default','attachments':attachments}],
      'animations':{'idle':idle,'attack':attack,'hurt':hurt,'death':death}}
OUT.mkdir(parents=True,exist_ok=True)
stem='forest_wolf_spine_rig_v5'
for ext in ('json','spinejson'):
    (OUT/(stem+'.'+ext)).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
(OUT/(stem+'.atlas')).write_text(f'../source/wolf_continuous_parts.png\nsize: {W}, {H}\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\nparts\n  rotate: false\n  xy: 0, 0\n  size: {W}, {H}\n  orig: {W}, {H}\n  offset: 0, 0\n  index: -1\n',encoding='utf-8')
print(f'Built {len(bones)} bones, {len(slots)} slots, four continuous legs and three complete heads.')


