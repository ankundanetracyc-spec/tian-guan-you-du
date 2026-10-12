"""Build independent anatomical meshes and planted-foot IK for Spine 3.8.

The generated atlas is never repainted or cut. Polygon UV boundaries isolate
each illustration, and each joint has its own bind transform.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / 'textures/characters/forest_wolf_spine_rig_v3'
OUT = PACKAGE / 'runtime'
W, H = 1448, 1086
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
add('hips','body_control',(990,450))
add('torso','hips',(585,475))
add('neck','torso',(480,340))
add('head','neck',(375,310))
add('jaw','head',(385,310),-14)
legs={
    'front_near':('torso',(585,475),(673,678.5),(656.5,920.5),
                  rect(70,405,376,722),(190,470),(1.1,1.1),rect(460,402,680,735),(560,460),(1.1,1.1)),
    'front_far':('torso',(450,430),(534.15,570.25),(505.45,730.15),
                 rect(790,426,1045,716),(875,485),(.85,.85),rect(1175,420,1370,725),(1265,475),(.82,.82)),
    'hind_far':('hips',(875,420),(987,568),(955,724),
                rect(772,725,1060,1079),(865,805),(.8,.8),rect(1185,738,1382,1053),(1280,800),(.8,.8)),
    'hind_near':('hips',(990,450),(1130,645),(1101.5,858.75),
                 rect(62,722,422,1079),(185,800),(1,1),rect(463,737,680,1065),(560,795),(.95,.95)),
}
for name,(parent,upper,knee,paw,*_) in legs.items():
    ua=math.degrees(math.atan2(upper[1]-knee[1],knee[0]-upper[0]))
    la=math.degrees(math.atan2(knee[1]-paw[1],paw[0]-knee[0]))
    add(name+'_upper',parent,upper,ua,math.dist(upper,knee))
    add(name+'_lower',name+'_upper',knee,la,math.dist(knee,paw))
    add(name+'_paw',name+'_lower',paw,0,fixed=True)
for name,(_,_,_,paw,*_) in legs.items():
    add(name+'_target','root',paw)
add('tail_base','hips',(1100,320))
add('tail_mid','tail_base',(1338,500))
add('tail_tip','tail_mid',(1408,700))

slots=[];attachments={}
def mesh(name, poly, pivot, position, scale, weight, depth=0, extra_angle=0):
    points,tris=subdivide(poly,triangulate(poly),depth)
    vertices=[]
    for x,y in points:
        delta=rotate(((x-pivot[0])*scale[0],-(y-pivot[1])*scale[1]),extra_angle)
        world=(position[0]-760+delta[0],996-position[1]+delta[1])
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


def limb(name):
    _,up,knee,paw,pu,pivu,su,pl,pivl,sl=legs[name]
    mesh(name+'_upper',pu,pivu,up,su,lambda x,y:{name+'_upper':1})
    ankle=pivl[1]+(paw[1]-knee[1])/sl[1]-48
    mesh(name+'_lower',pl,pivl,knee,sl,
         lambda x,y:{name+'_lower':1-smooth(y,ankle-45,ankle+35),name+'_paw':smooth(y,ankle-45,ankle+35)},4)


tailpoly=[(1070,20),(1448,20),(1448,422),(1260,422),(1255,365),(1190,330),(1150,305),(1070,215)]
def tailweights(x,y):
    mid=smooth(y,95,240);tip=smooth(y,240,390)
    return {'tail_base':1-mid,'tail_mid':mid*(1-tip),'tail_tip':mid*tip}
mesh('tail',tailpoly,(1130,95),(1100,320),(1.4,1.4),tailweights,3)
limb('hind_far');limb('front_far')
bodypoly=[(0,0),(460,0),(450,185),(426,263),(422,304),(396,363),(370,405),(0,405)]
mesh('body',bodypoly,(245,220),(750,390),(1.8,1.18),
     lambda x,y:{'hips':smooth(x,150,365),'torso':1-smooth(x,150,365)},3)
limb('hind_near');limb('front_near')
mesh('jaw',rect(798,210,1065,375),(1035,255),(385,310),(.95,.95),lambda x,y:{'jaw':1},extra_angle=-14)
headpoly=[(525,38),(646,60),(692,115),(789,150),(789,340),(725,413),(585,407),(578,313),(504,307),(448,319),(419,283),(433,253),(468,230),(483,159),(505,127)]
mesh('head',headpoly,(610,280),(375,310),(1.1,1.05),lambda x,y:{'head':1})


def animation(duration, keys):
    # Explicit setup keys avoid leaving death transforms behind on replay.
    tracks={n:{'rotate':[{'time':0,'angle':0},{'time':duration,'angle':0}],
               'translate':[{'time':0,'x':0,'y':0},{'time':duration,'x':0,'y':0}]}
            for n in bind if n!='root'}
    for name,kind,values in keys:
        tracks[name][kind]=[dict(time=t,**({'angle':v} if kind=='rotate' else {'x':v[0],'y':v[1]})) for t,v in values]
    for timelines in tracks.values():
        for keys in timelines.values():
            for key in keys[:-1]:
                key['curve']=[.25,0,.75,1]
    return {'bones':tracks}


def rot(n,*v): return (n,'rotate',v)
def move(n,*v): return (n,'translate',v)


idle=animation(1.6,[move('body_control',(0,(0,0)),(.8,(0,8)),(1.6,(0,0))),
    rot('head',(0,0),(.8,-1.3),(1.6,0)),rot('tail_mid',(0,-2),(.8,3),(1.6,-2)),
    rot('tail_tip',(0,-3),(.8,4),(1.6,-3))])
attackkeys=[move('body_control',(0,(0,0)),(.18,(32,-28)),(.36,(-92,12)),(.55,(-42,-12)),(.82,(0,0))),
    rot('head',(0,0),(.18,9),(.36,-12),(.55,-4),(.82,0)),
    rot('neck',(0,0),(.18,4),(.36,-8),(.82,0)),
    rot('jaw',(0,0),(.18,22),(.36,37),(.55,12),(.82,0)),
    rot('tail_base',(0,0),(.18,8),(.36,-10),(.82,0))]
for n in legs:
    fore=n.startswith('front')
    attackkeys.append(move(n+'_target',(0,(0,0)),(.18,(20,65 if fore else 0)),(.36,(-140,95 if fore else 5)),(.55,(-65,0)),(.82,(0,0))))
attack=animation(.82,attackkeys)
hurt=animation(.5,[move('body_control',(0,(0,0)),(.09,(48,-18)),(.2,(27,-8)),(.5,(0,0))),
    rot('head',(0,0),(.09,-15),(.2,-8),(.5,0)),rot('jaw',(0,0),(.09,14),(.5,0)),
    rot('tail_base',(0,0),(.12,12),(.5,0))])
deathkeys=[move('body_control',(0,(0,0)),(.18,(25,-35)),(.6,(5,-190)),(1.05,(-15,-335)),(1.4,(-15,-335))),
    rot('torso',(0,0),(.6,7),(1.05,12),(1.4,12)),
    rot('head',(0,0),(.18,-18),(.6,10),(1.05,15),(1.4,15)),
    rot('neck',(0,0),(.6,4),(1.05,8),(1.4,8)),
    rot('jaw',(0,0),(.18,20),(1.05,-3),(1.4,-3)),
    rot('tail_base',(0,0),(.6,12),(1.05,30),(1.4,30)),
    rot('tail_mid',(0,0),(.6,10),(1.05,25),(1.4,25)),
    rot('tail_tip',(0,0),(1.05,20),(1.4,20))]
for n in legs:
    near=n.endswith('near');fore=n.startswith('front')
    delta=(-100 if fore else 90,-20 if near else -120)
    deathkeys.append(move(n+'_target',(0,(0,0)),(.4,(delta[0]*.3,delta[1]*.3)),(1.05,delta),(1.4,delta)))
    # Fade out stance IK during the fall and pose the folded limbs explicitly.
    # This avoids the opposite IK solution as the hips pass below the feet.
    ua=bind[n+'_upper'][1];la=bind[n+'_lower'][1]
    end_upper=-145 if near else -150
    parent_end=12 if fore else 0
    upper_delta=end_upper-parent_end-ua
    lower_delta=(-5-end_upper)-(la-ua)
    deathkeys.extend([rot(n+'_upper',(0,0),(.25,0),(.65,upper_delta*.55),(1.05,upper_delta),(1.4,upper_delta)),
                      rot(n+'_lower',(0,0),(.25,0),(.65,lower_delta*.55),(1.05,lower_delta),(1.4,lower_delta))])
death=animation(1.4,deathkeys)
for action in (idle,attack,hurt):
    action['ik']={'ik_'+n:[{'time':0,'mix':1,'bendPositive':False}] for n in legs}
death['ik']={'ik_'+n:[{'time':0,'mix':1,'bendPositive':False},{'time':.25,'mix':1,'bendPositive':False},
                       {'time':.65,'mix':0,'bendPositive':False},{'time':1.4,'mix':0,'bendPositive':False}] for n in legs}
data={'skeleton':{'spine':'3.8.99','hash':'forest_wolf_anatomical_v3','x':-760,'y':-28,'width':1536,'height':1024},
      'bones':bones,'slots':slots,
      'ik':[{'name':'ik_'+n,'order':i,'bones':[n+'_upper',n+'_lower'],'target':n+'_target','bendPositive':False,'mix':1,'softness':8} for i,n in enumerate(legs)],
      'skins':[{'name':'default','attachments':attachments}],
      'animations':{'idle':idle,'attack':attack,'hurt':hurt,'death':death}}
OUT.mkdir(parents=True,exist_ok=True)
stem='forest_wolf_spine_rig_v3'
for ext in ('json','spinejson'):
    (OUT/(stem+'.'+ext)).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
(OUT/(stem+'.atlas')).write_text(f'../source/wolf_parts_atlas.png\nsize: {W}, {H}\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\nparts\n  rotate: false\n  xy: 0, 0\n  size: {W}, {H}\n  orig: {W}, {H}\n  offset: 0, 0\n  index: -1\n',encoding='utf-8')
print(f'Built {len(bones)} bones, {len(slots)} independent meshes, 4 IK chains, 4 animations.')
