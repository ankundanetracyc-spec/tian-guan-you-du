"""Build Spine 3.8 weighted cutouts from the approved v6 wolf.

Raster artwork is generated with imagegen and never repainted here. Alpha is
read only to derive silhouette geometry. Joint-aware interior vertices and
weights follow curved anatomical chains, rather than image rows.
"""
import json
import math
from collections import deque
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / 'textures/characters/forest_wolf_spine_rig_v6'
OUT = PACKAGE / 'runtime'
SOURCE = PACKAGE / 'source'
ORIGIN = (760, 940)
bones, bind, indices, slots, attachments, ik, transforms = [], {}, {}, [], {}, [], []
mesh_report = []


def rotate(p, angle):
    a = math.radians(angle)
    return p[0]*math.cos(a)-p[1]*math.sin(a), p[0]*math.sin(a)+p[1]*math.cos(a)


def to_world(p):
    return p[0]-ORIGIN[0], ORIGIN[1]-p[1]


def add(name, parent, screen, angle=0, length=0):
    pos = to_world(screen)
    data = {'name': name}
    if parent:
        bp, ba = bind[parent]
        local = rotate((pos[0]-bp[0], pos[1]-bp[1]), -ba)
        data.update(parent=parent, x=local[0], y=local[1], rotation=angle-ba)
    if length:
        data['length'] = length
    indices[name] = len(bones)
    bones.append(data)
    bind[name] = pos, angle


def smooth(value, start, end):
    t = max(0, min(1, (value-start)/(end-start)))
    return t*t*(3-2*t)


def cross(a, b, c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])


def area(poly):
    return sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in range(len(poly)))/2


def point_in_poly(p, poly):
    inside = False
    j = len(poly)-1
    for i, b in enumerate(poly):
        a = poly[j]
        if (a[1] > p[1]) != (b[1] > p[1]) and p[0] < (b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0]:
            inside = not inside
        j = i
    return inside


def seg_distance(p, a, b):
    vx, vy = b[0]-a[0], b[1]-a[1]
    t = max(0, min(1, ((p[0]-a[0])*vx+(p[1]-a[1])*vy)/(vx*vx+vy*vy)))
    q = a[0]+vx*t, a[1]+vy*t
    return math.dist(p, q), t


def rdp(points, epsilon):
    if len(points) < 3:
        return points
    best, index = 0, 0
    for i in range(1, len(points)-1):
        d, _ = seg_distance(points[i], points[0], points[-1])
        if d > best:
            best, index = d, i
    if best <= epsilon:
        return [points[0], points[-1]]
    return rdp(points[:index+1], epsilon)[:-1]+rdp(points[index:], epsilon)


def components(path):
    image = Image.open(path)
    w, h = image.size
    alpha = image.getchannel('A').tobytes()
    seen = bytearray(w*h)
    found = []
    for i, value in enumerate(alpha):
        if value < 80 or seen[i]:
            continue
        q, pixels = deque([i]), set()
        seen[i] = 1
        while q:
            j = q.pop()
            pixels.add(j)
            x = j % w
            for k in ((j-1 if x > 0 else -1), (j+1 if x < w-1 else -1), j-w, j+w):
                if 0 <= k < len(alpha) and not seen[k] and alpha[k] >= 80:
                    seen[k] = 1
                    q.append(k)
        if len(pixels) > 600:
            found.append(pixels)
    return w, h, found


def silhouette(pixels, width, max_vertices=125):
    # Trace the exterior of opaque texels; no raster file is changed.
    edges = {}
    def edge(a, b):
        edges.setdefault(a, []).append(b)
    for i in pixels:
        x, y = i % width, i // width
        if i-width not in pixels: edge((x,y), (x+1,y))
        if i+1 not in pixels: edge((x+1,y), (x+1,y+1))
        if i+width not in pixels: edge((x+1,y+1), (x,y+1))
        if i-1 not in pixels: edge((x,y+1), (x,y))
    loops = []
    while edges:
        start = next(iter(edges))
        p, loop = start, []
        while p in edges:
            loop.append(p)
            values = edges[p]
            q = values.pop()
            if not values: del edges[p]
            p = q
            if p == start: break
        if len(loop) >= 3: loops.append(loop)
    loop = max(loops, key=lambda poly: abs(area(poly)))
    k = max(range(len(loop)), key=lambda i: math.dist(loop[0],loop[i]))
    epsilon = 2.2
    while True:
        poly = rdp(loop[:k+1],epsilon)[:-1]+rdp(loop[k:]+[loop[0]],epsilon)[:-1]
        if len(poly) <= max_vertices: break
        epsilon += .4
    if area(poly) < 0: poly.reverse()
    # Two-pixel outward allowance retains antialiased fur outside alpha=80.
    result=[]
    for i,p in enumerate(poly):
        prev, nxt = poly[i-1], poly[(i+1)%len(poly)]
        v=(nxt[1]-prev[1], prev[0]-nxt[0])
        length=math.hypot(*v)
        result.append((p[0]+v[0]/length*2,p[1]+v[1]/length*2))
    return result


def triangulate(poly):
    ids = list(range(len(poly)))
    if area(poly) < 0: ids.reverse()
    tris=[]
    while len(ids)>3:
        for j in range(len(ids)):
            a,b,c=ids[j-1],ids[j],ids[(j+1)%len(ids)]
            if cross(poly[a],poly[b],poly[c]) <= 1e-7: continue
            if any(min(cross(poly[a],poly[b],poly[k]),cross(poly[b],poly[c],poly[k]),cross(poly[c],poly[a],poly[k]))>=-1e-7 for k in ids if k not in (a,b,c)):
                continue
            tris.append((a,b,c));ids.pop(j);break
        else:
            raise ValueError('Silhouette cannot be triangulated')
    tris.append(tuple(ids))
    return tris


def build_geometry(poly, interior):
    points=list(poly)
    tris=triangulate(poly)
    for p in interior:
        if not point_in_poly(p,poly) or min(math.dist(p,q) for q in points)<6:
            continue
        for ti,(a,b,c) in enumerate(tris):
            if min(cross(points[a],points[b],p),cross(points[b],points[c],p),cross(points[c],points[a],p))>1e-6:
                n=len(points);points.append(p)
                tris[ti:ti+1]=[(a,b,n),(b,c,n),(c,a,n)]
                break
    # Local edge flips improve skinny triangles while preserving the outline.
    boundary={tuple(sorted((i,(i+1)%len(poly)))) for i in range(len(poly))}
    for _ in range(10):
        edges={}
        for ti,tri in enumerate(tris):
            for j in range(3):
                edge=tuple(sorted((tri[j],tri[(j+1)%3])))
                edges.setdefault(edge,[]).append((ti,tri[(j+2)%3]))
        changed=False
        for (a,b),entries in edges.items():
            if (a,b) in boundary or len(entries)!=2:continue
            (t1,c),(t2,d)=entries
            if set(tris[t1])!={a,b,c} or set(tris[t2])!={a,b,d}:continue
            if cross(points[c],points[d],points[a])*cross(points[c],points[d],points[b])>=-1e-6:continue
            def angle(u,v,z):
                p,q=points[u],points[v];r=points[z]
                v1=(p[0]-r[0],p[1]-r[1]);v2=(q[0]-r[0],q[1]-r[1])
                return math.acos(max(-1,min(1,(v1[0]*v2[0]+v1[1]*v2[1])/(math.hypot(*v1)*math.hypot(*v2)))))
            if angle(a,b,c)+angle(a,b,d)<=math.pi+1e-4:continue
            replacement=[(c,d,a),(d,c,b)]
            tris[t1],tris[t2]=[t if cross(points[t[0]],points[t[1]],points[t[2]])>0 else (t[0],t[2],t[1]) for t in replacement]
            changed=True
        if not changed:break
    return points,tris


W,H,parts=components(SOURCE/'wolf_parts.png')
def component_at(p):
    return min(parts,key=lambda c:min(math.dist(p,(i%W,i//W)) for i in c))


PART_PINS={'body':(250,200),'head':(750,200),'jaw':(1080,270),'tail':(260,620),
           'front_near':(650,600),'front_far':(1050,600),'hind_near':(250,1000),
           'hind_far':(620,1000),'mouth':(1050,1030)}
polygons={n:silhouette(component_at(p),W) for n,p in PART_PINS.items()}

def mapping(pivot, position, scale):
    return lambda p:(position[0]+(p[0]-pivot[0])*scale[0], position[1]+(p[1]-pivot[1])*scale[1])


body_map=mapping((200,215),(650,445),(1.55,1.05))
head_map=mapping((715,300),(275,350),(1.05,1.05))
jaw_map=mapping((1180,278),(278,360),(.73,.45))
tail_map=mapping((80,470),(1120,460),(1.0,.95))
mouth_map=mapping((1120,1028),(278,360),(.85,.55))

add('root',None,ORIGIN)
add('body_control','root',ORIGIN)
add('hips','body_control',(1075,500))
add('spine_mid','hips',(820,445))
add('chest','spine_mid',(590,500))
add('breathe','chest',(600,470))
add('neck','chest',(425,380))
add('head','neck',(275,350))
add('jaw_adjust','head',(278,360))
add('jaw','jaw_adjust',(278,360),180,175)
add('mouth_corner','head',(278,360))
add('brow','head',head_map((660,205)))
add('ear_near','head',head_map((757,146)),105,100)
add('ear_far','head',head_map((650,148)),105,90)

LEGS={
    'front_near':dict(parent='chest',src=[(646,470),(688,605),(665,760),(620,818)],pos=(590,500),scale=(1.08,1.12)),
    'front_far':dict(parent='chest',src=[(1021,482),(1068,604),(1064,758),(1010,815)],pos=(505,515),scale=(.94,1.03)),
    'hind_near':dict(parent='hips',src=[(206,917),(231,1011),(321,1093),(301,1179),(255,1220)],pos=(1040,500),scale=(.98,1.32)),
    'hind_far':dict(parent='hips',src=[(607,932),(609,1012),(699,1080),(671,1170),(615,1210)],pos=(950,510),scale=(.96,1.23)),
}
for name,c in LEGS.items():
    c['map']=mapping(c['src'][0],c['pos'],c['scale'])
    c['world']=[c['map'](p) for p in c['src']]
    c['chain']=[name+'_upper',name+'_lower']+([name+'_hock'] if name.startswith('hind') else [])+[name+'_paw']
    for i,b in enumerate(c['chain']):
        a,z=c['world'][i:i+2]
        angle=0 if b.endswith('_paw') else math.degrees(math.atan2(a[1]-z[1],z[0]-a[0]))
        add(b,c['parent'] if i==0 else c['chain'][i-1],a,angle,math.dist(a,z))
for name,c in LEGS.items():
    hind=name.startswith('hind')
    add(name+'_target','root',c['world'][-2])
    if hind:add(name+'_hock_target',name+'_target',c['world'][-3])
    ik.append({'name':name+'_ik','order':len(ik),'bones':c['chain'][:2],
               'target':name+('_hock_target' if hind else '_target'),'bendPositive':hind,'mix':1,'softness':4})
    if hind:ik.append({'name':name+'_hock_ik','order':len(ik),'bones':[name+'_hock'],
                      'target':name+'_target','mix':1})
for name,c in LEGS.items():
    transforms.append({'name':name+'_paw_orientation','order':len(ik)+len(transforms),
                       'bones':[name+'_paw'],'target':name+'_target','rotateMix':1,
                       'translateMix':0,'scaleMix':0,'shearMix':0})

tail_src=[(80,470),(220,605),(335,678),(420,747)]
tail_names=['tail_base','tail_mid','tail_tip']
for i,name in enumerate(tail_names):
    a,z=[tail_map(p) for p in tail_src[i:i+2]]
    add(name,'hips' if i==0 else tail_names[i-1],a,math.degrees(math.atan2(a[1]-z[1],z[0]-a[0])),math.dist(a,z))

DW,DH,death_parts=components(SOURCE/'wolf_death.png')
death_poly=silhouette(max(death_parts,key=len),DW,160)
death_map=mapping((40,710),(90,910),(.80,.80))
add('corpse_root','root',ORIGIN)
for name,p in [('corpse_hips',(1150,500)),('corpse_chest',(700,480)),('corpse_head',(250,600)),('corpse_tail',(1450,630)),('corpse_forepaw',(540,690))]:
    add(name,'corpse_root',death_map(p))


def curved_weights(p, chain_points, names, bandwidth=24):
    lengths=[math.dist(a,b) for a,b in zip(chain_points,chain_points[1:])]
    cum=[0]
    for length in lengths:cum.append(cum[-1]+length)
    d,t,index=min(((*seg_distance(p,a,b),i) for i,(a,b) in enumerate(zip(chain_points,chain_points[1:]))),key=lambda q:q[0])
    arc=cum[index]+t*lengths[index]
    blends=[smooth(arc,cum[i]-bandwidth,cum[i]+bandwidth) for i in range(1,len(names))]
    weights={names[0]:1-blends[0]}
    for i in range(1,len(names)-1):weights[names[i]]=blends[i-1]*(1-blends[i])
    weights[names[-1]]=blends[-1]
    return weights,arc


def leg_weights(name):
    c=LEGS[name]
    def weight(p):
        w,arc=curved_weights(p,c['src'],c['chain'],22)
        proximal=(1-smooth(arc,0,95))*.95
        w={b:v*(1-proximal) for b,v in w.items()}
        w[c['parent']]=proximal
        return w
    return weight


def body_weights(p):
    x,y=body_map(p)
    hip=smooth(x,775,1100)
    chest=1-smooth(x,610,850)
    neck=(1-smooth(x,420,540))*(1-smooth(y,310,490))*.65
    breathe=chest*(1-neck)*.18
    return {'hips':hip*(1-chest),'spine_mid':(1-hip)*(1-chest),
            'chest':chest*(1-neck)-breathe,'neck':chest*neck,'breathe':breathe}


def head_weights(p):
    x,y=p
    neck=smooth(x,825,925)*.35
    ear_n=(1-smooth(y,120,175))*smooth(x,690,730)
    ear_f=(1-smooth(y,120,175))*(1-smooth(x,675,700))
    brow=math.exp(-((x-655)**2+(y-206)**2)/(2*20**2))*.25
    remainder=max(0,1-neck-ear_n-ear_f-brow)
    return {'head':remainder,'neck':neck,'ear_near':ear_n,'ear_far':ear_f,'brow':brow}


def jaw_weights(p):
    # Anterior mandible rotates rigidly; the cheek at the hinge stays connected.
    head=smooth(p[0],1140,1205)*.65
    return {'jaw':1-head,'head':head*.55,'mouth_corner':head*.45}


def mouth_weights(p):
    lower=smooth(p[1],990,1060)
    corner=smooth(p[0],1090,1135)*.6
    return {'head':(1-lower)*(1-corner),'jaw':lower*(1-corner),'mouth_corner':corner}


def grid(poly,step):
    xs,ys=zip(*poly)
    return [(x,y) for x in range(int(min(xs))+step//2,int(max(xs)),step) for y in range(int(min(ys))+step//2,int(max(ys)),step)]


def chain_vertices(points):
    out=[]
    for i,(a,b) in enumerate(zip(points,points[1:])):
        dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy)
        for t in (.08,.35,.65,.92):
            for offset in (-32,-14,0,14,32):
                out.append((a[0]+dx*t-dy/length*offset,a[1]+dy*t+dx/length*offset))
    return out


def mesh(name, poly, mapper, weight, interior=None, page='parts', dimensions=(W,H), visible=True):
    points,tris=build_geometry(poly,interior or [])
    vertices=[];influences=[]
    for p in points:
        world=to_world(mapper(p))
        weights={b:max(0,v) for b,v in weight(p).items() if v>1e-5}
        total=sum(weights.values());weights={b:v/total for b,v in weights.items()}
        vertices.append(len(weights));influences.append(len(weights))
        for bone,w in weights.items():
            bp,ba=bind[bone]
            local=rotate((world[0]-bp[0],world[1]-bp[1]),-ba)
            vertices.extend([indices[bone],round(local[0],5),round(local[1],5),round(w,6)])
    dw,dh=dimensions
    attachment={'type':'mesh','path':page,'uvs':[v for p in points for v in (p[0]/dw,p[1]/dh)],
                'triangles':[v for t in tris for v in t],'vertices':vertices,'hull':len(poly),'width':dw,'height':dh}
    assert len(points)<1024 and len(tris)*3<=3072
    slots.append({'name':name,'bone':'root',**({'attachment':name} if visible else {})})
    attachments[name]={name:attachment}
    mesh_report.append({'slot':name,'vertices':len(points),'triangles':len(tris),'max_influences':max(influences),
                        'outline':poly,'interior':[p for p in points[len(poly):]],'world_vertices':[mapper(p) for p in points]})


mesh('tail',polygons['tail'],tail_map,lambda p:curved_weights(p,tail_src,tail_names,28)[0],chain_vertices(tail_src))
for name in ('hind_far','front_far','hind_near','front_near'):
    c=LEGS[name];mesh(name,polygons[name],c['map'],leg_weights(name),chain_vertices(c['src']))
# The body fur silhouette covers all four proximal roots; no hard-edged UV patch.
mesh('body',polygons['body'],body_map,body_weights,grid(polygons['body'],45))
mesh('mouth',polygons['mouth'],mouth_map,mouth_weights,grid(polygons['mouth'],25),visible=False)
mesh('jaw',polygons['jaw'],jaw_map,jaw_weights,grid(polygons['jaw'],28))
mesh('head',polygons['head'],head_map,head_weights,grid(polygons['head'],40))


def corpse_weights(p):
    centers={'corpse_hips':(1150,500),'corpse_chest':(700,480),'corpse_head':(250,600),
             'corpse_tail':(1450,630),'corpse_forepaw':(540,690)}
    w={b:math.exp(-((p[0]-c[0])**2/(2*200**2)+(p[1]-c[1])**2/(2*115**2))) for b,c in centers.items()}
    return dict(sorted(w.items(),key=lambda i:-i[1])[:3])


mesh('corpse',death_poly,death_map,corpse_weights,grid(death_poly,65),page='death',dimensions=(DW,DH),visible=False)


def animation(duration, keys=(), mouth=None, dead=False):
    tracks={n:{'rotate':[{'time':0,'angle':0},{'time':duration,'angle':0}],
               'translate':[{'time':0,'x':0,'y':0},{'time':duration,'x':0,'y':0}]} for n in bind if n!='root'}
    for name,kind,values in keys:
        tracks[name][kind]=[dict(time=t,**({'angle':v} if kind=='rotate' else {'x':v[0],'y':v[1]} if kind=='translate' else {'x':v[0],'y':v[1]})) for t,v in values]
    for timelines in tracks.values():
        for frames in timelines.values():
            for key in frames[:-1]:key['curve']=[.25,0,.75,1]
    slot_tracks={s['name']:{'attachment':[{'time':0,'name':s.get('attachment')}]} for s in slots}
    if mouth:slot_tracks['mouth']['attachment']=[{'time':t,'name':'mouth' if on else None} for t,on in mouth]
    if dead:
        for s in slots:
            slot_tracks[s['name']]['attachment'].append({'time':.78,'name':'corpse' if s['name']=='corpse' else None})
    return {'bones':tracks,'slots':slot_tracks,
            'ik':{c['name']:[{'time':0,'mix':1,'bendPositive':c.get('bendPositive',True)}] for c in ik},
            'transform':{c['name']:[{'time':0,'rotateMix':1,'translateMix':0,'scaleMix':0,'shearMix':0}] for c in transforms}}


def rot(n,*values):return n,'rotate',values
def move(n,*values):return n,'translate',values
def scale(n,*values):return n,'scale',values


idle=animation(2.4,[move('chest',(0,(0,0)),(1.2,(0,3.5)),(2.4,(0,0))),
    move('hips',(0,(0,0)),(1.2,(0,1)),(2.4,(0,0))),
    rot('head',(0,0),(1.2,-.6),(2.4,0)),rot('ear_near',(0,0),(1.2,1),(2.4,0)),
    rot('tail_base',(0,-1),(1.2,1.5),(2.4,-1)),rot('tail_mid',(0,1),(1.2,-2.5),(2.4,1)),rot('tail_tip',(0,2),(1.2,-3),(2.4,2))])
attack_keys=[move('body_control',(0,(0,0)),(.15,(14,-14)),(.33,(-70,8)),(.48,(-55,-6)),(.68,(-12,0)),(.95,(0,0))),
    rot('neck',(0,0),(.15,-4),(.33,6),(.48,4),(.95,0)),
    rot('head',(0,0),(.15,-7),(.33,9),(.48,4),(.95,0)),
    rot('jaw',(0,0),(.14,5),(.26,27),(.39,16),(.47,1),(.55,0),(.95,0)),
    rot('ear_near',(0,0),(.15,-4),(.33,-8),(.68,0),(.95,0)),
    rot('tail_base',(0,0),(.15,-5),(.33,7),(.68,2),(.95,0))]
for name in LEGS:
    fore=name.startswith('front')
    attack_keys.append(move(name+'_target',(0,(0,0)),(.15,(10,18 if fore else 0)),(.33,(-95 if fore else -22,55 if fore else 0)),(.48,(-80 if fore else -15,6 if fore else 0)),(.68,(-15 if fore else 0,0)),(.95,(0,0))))
attack=animation(.95,attack_keys,[(0,False),(.14,True),(.52,False)])
hurt=animation(.65,[move('body_control',(0,(0,0)),(.11,(28,-8)),(.25,(13,-3)),(.65,(0,0))),
    rot('head',(0,0),(.11,-8),(.25,-3),(.65,0)),rot('neck',(0,0),(.11,-3),(.65,0)),
    rot('tail_base',(0,0),(.13,-6),(.65,0)),rot('ear_near',(0,0),(.11,-8),(.65,0))])
death_keys=[move('body_control',(0,(0,0)),(.15,(12,-20)),(.45,(-10,-90)),(.78,(-20,-150)),(1.65,(-20,-150))),
    rot('spine_mid',(0,0),(.45,3),(.78,7),(1.65,7)),
    rot('neck',(0,0),(.15,3),(.45,25),(.78,48),(1.65,48)),
    rot('head',(0,0),(.15,-5),(.45,-10),(.78,-25),(1.65,-25)),
    rot('tail_base',(0,0),(.45,10),(.78,18),(1.65,18)),
    move('corpse_root',(0,(0,0)),(.78,(0,100)),(.94,(0,-3)),(1.12,(0,0)),(1.65,(0,0))),
    rot('corpse_head',(0,0),(.78,-1.6),(1.12,0),(1.65,0)),
    scale('corpse_chest',(0,(1,1)),(.78,(1,1.025)),(1.15,(1,1)),(1.65,(1,1))),
    rot('corpse_tail',(0,0),(.78,2),(1.18,0),(1.65,0))]
for name in LEGS:
    fore=name.startswith('front')
    death_keys.append(move(name+'_target',(0,(0,0)),(.15,(0,0)),(.45,(-30 if fore else -5,0)),(.78,(-90 if fore else -12,0)),(1.65,(-90 if fore else -12,0))))
death=animation(1.65,death_keys,dead=True)

data={'skeleton':{'spine':'3.8.99','hash':'forest_wolf_v6_anatomical_cutouts','x':-760,'y':-40,'width':1536,'height':1024},
      'bones':bones,'slots':slots,'ik':ik,'transform':transforms,
      'skins':[{'name':'default','attachments':attachments}],
      'animations':{'idle':idle,'attack':attack,'hurt':hurt,'death':death}}
OUT.mkdir(parents=True,exist_ok=True)
stem='forest_wolf_spine_rig_v6'
for ext in ('json','spinejson'):
    (OUT/(stem+'.'+ext)).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
atlas=''
for file,region,w,h in [('wolf_parts.png','parts',W,H),('wolf_death.png','death',DW,DH)]:
    atlas+=f'../source/{file}\nsize: {w}, {h}\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\n{region}\n  rotate: false\n  xy: 0, 0\n  size: {w}, {h}\n  orig: {w}, {h}\n  offset: 0, 0\n  index: -1\n\n'
(OUT/(stem+'.atlas')).write_text(atlas,encoding='utf-8')
(PACKAGE/'mesh_report.json').write_text(json.dumps({'bones':len(bones),'slots':len(slots),'ik':len(ik),
    'transform_constraints':len(transforms),'meshes':mesh_report},indent=2),encoding='utf-8')
print(f'Built {len(bones)} bones, {len(slots)} slots, {len(ik)} IK, {len(transforms)} paw orientation constraints')
for r in mesh_report:print(r['slot'],r['vertices'],'vertices',r['triangles'],'triangles')
