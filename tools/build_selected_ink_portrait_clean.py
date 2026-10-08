"""Clean the user's frozen selected painting in its own portrait coordinates.

Pixel matting was explicitly authorized after generative extraction moved the
eyes. Visible artwork comes from the selected composite, never Guigu assets.
"""
from pathlib import Path
import importlib.util
import hashlib
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[1]
PACKS = PROJECT / "mods/appearance"
ROOT = PACKS / "selected_ink_portrait_clean_20261003_v1"
SELECTED = PACKS / "selected_ink_portrait_rig_20261003_v1"
V4 = PACKS / "ink_portrait_pair_20261003_v4"
W, H = 1024, 1536
YY, XX = np.indices((H, W))
ORDER = ["hair_back", "face_shape", "clothing", "eyes", "eyebrows", "nose", "mouth", "face_accessory", "hair_front"]
NAMES = {"hair_back":"后发", "face_shape":"脸型与完整颈部", "clothing":"衣服", "eyes":"眼睛", "eyebrows":"眉毛", "nose":"鼻子", "mouth":"嘴巴", "face_accessory":"面饰（空）", "hair_front":"前发"}


def read(path):
    return np.array(Image.open(path).convert("RGBA")).copy()


def save(path, a):
    path.parent.mkdir(parents=True, exist_ok=True)
    a = np.round(np.clip(a, 0, 255)).astype(np.uint8).copy()
    a[a[:, :, 3] == 0, :3] = 0
    Image.fromarray(a).save(path)


def polygon(points):
    a = Image.new("L", (W*4, H*4))
    ImageDraw.Draw(a).polygon([(round(x*4), round(y*4)) for x,y in points], fill=255)
    return np.array(a.resize((W,H), Image.Resampling.LANCZOS)).astype(np.float32)/255


def union(paths):
    return np.maximum.reduce([polygon(p) for p in paths])


def compose(layers, hidden=()):
    a = Image.new("RGBA", (W,H))
    for key in ORDER:
        if key not in hidden:
            a.alpha_composite(Image.fromarray(layers[key]))
    return np.array(a)


def background(a, color="#e8e3dc"):
    b = Image.new("RGBA", (W,H), color)
    b.alpha_composite(Image.fromarray(a))
    return b.convert("RGB")


# Trimap supports are traced in the selected painting's canvas. They describe
# ownership, not a rectangular crop or a silhouette borrowed from another game.
HAIR_PATHS = {
    "male": [
        [(255,610),(305,513),(330,396),(322,289),(350,208),(410,156),(502,137),(562,141),(637,180),(689,238),(704,320),(705,400),(734,498),(794,614),(699,641),(652,576),(615,525),(610,467),(617,425),(629,401),(628,377),(616,359),(607,331),(597,303),(578,278),(542,270),(517,277),(477,271),(447,280),(429,307),(422,337),(417,364),(407,376),(403,394),(414,421),(425,438),(427,482),(435,520),(425,552),(399,578),(360,610)],
        [(367,520),(418,518),(420,590),(407,667),(393,714),(373,773),(371,818),(379,861),(395,898),(421,945),(445,980),(425,963),(399,930),(375,898),(352,865),(338,836),(337,790),(347,748),(363,696),(366,637)],
        [(603,519),(638,520),(649,612),(673,705),(694,766),(711,823),(716,873),(709,910),(696,938),(670,964),(646,982),(666,959),(685,927),(695,889),(696,847),(684,800),(665,754),(644,706),(625,635)],
    ],
    "female": [
        [(271,656),(313,556),(337,461),(346,392),(331,317),(336,246),(380,184),(449,145),(518,131),(589,143),(654,184),(693,246),(707,320),(705,362),(689,380),(674,396),(665,423),(647,450),(627,457),(615,488),(606,532),(627,559),(674,610),(748,659),(782,692),(689,661),(638,624),(606,582),(587,552),(585,519),(603,485),(619,447),(626,421),(633,399),(626,367),(610,337),(599,304),(582,279),(551,268),(518,272),(484,266),(452,274),(431,303),(420,339),(413,369),(407,394),(403,413),(418,449),(433,480),(445,508),(448,538),(433,561),(409,591),(358,626)],
        [(380,513),(435,515),(431,575),(420,641),(404,706),(384,762),(380,813),(383,850),(397,888),(419,926),(442,957),(421,943),(394,915),(373,884),(358,850),(354,812),(360,765),(375,707),(389,641)],
        [(591,515),(627,515),(637,584),(657,658),(676,723),(697,774),(713,828),(714,871),(704,904),(685,926),(658,947),(635,961),(655,940),(678,912),(691,882),(695,848),(687,806),(671,766),(652,713),(628,644)],
    ],
}
SIDE_LOCKS={
 'male':[
  [(441,279),(456,279),(451,306),(444,330),(439,370),(437,413),(442,442),(418,441),(411,410),(414,370),(422,330),(430,296)],
  [(579,279),(595,282),(608,315),(619,350),(628,391),(634,428),(629,452),(613,449),(605,411),(601,373),(596,338),(588,308)]],
 'female':[
  [(441,278),(456,273),(452,301),(444,329),(438,357),(433,389),(426,421),(425,441),(410,441),(402,424),(406,389),(414,352),(426,315),(434,292)],
  [(584,276),(602,281),(615,311),(628,348),(638,389),(643,425),(641,452),(623,451),(617,415),(612,382),(607,351),(597,320),(590,292)]],
}

EYE_CORE = {
 "male": [
  [(426,369),(438,367),(452,365.5),(465,365.5),(477,369),(486,375),(493,380),(486,380.5),(477,381.5),(466,382),(456,382),(447,380.5),(438,377),(433,373),(430,372)],
  [(539,380),(548,374),(559,367.5),(571,365),(584,365),(597,366.5),(610,368),(602,373),(595,377),(586,380),(576,381.7),(565,381.5),(552,381),(546,380.5)],
 ],
 "female": [
  [(424,375),(431,377),(439,376),(447,372.5),(458,371),(470,372),(480,377),(487,383),(493,389),(486,388),(477,390),(466,391.8),(459,391.7),(449,389),(440,383),(436,379),(430,378)],
  [(546,389),(552,381),(561,375),(573,370.5),(587,371),(598,374),(607,376.5),(616,375),(610,378),(603,380.5),(596,387),(587,391),(576,392),(565,391),(555,389)],
 ],
}
FEATURE_PATHS = {
 "male": {
  "eyes":[[(426,361),(441,357),(462,357),(480,362),(496,379),(498,389),(479,401),(450,399),(431,386)],[(535,377),(551,360),(575,356),(599,357),(613,362),(612,382),(590,398),(559,399),(538,387)]],
  "eyebrows":[[(417,351),(430,341),(450,340),(471,344),(490,350),(498,358),(477,359),(454,355),(434,352)],[(536,357),(550,347),(573,340),(597,339),(613,345),(612,350),(590,351),(566,356),(547,359)]],
  "nose":[[(500,405),(533,405),(541,432),(548,451),(540,462),(529,473),(516,478),(502,469),(492,459),(489,442)]],
  "mouth":[[(476,477),(490,471),(507,471),(518,474),(534,472),(549,478),(558,484),(553,497),(533,513),(507,514),(488,503),(477,491)]],
 },
 "female": {
  "eyes":[[(423,366),(439,356),(460,355),(480,361),(497,381),(497,398),(474,411),(446,404),(429,387)],[(541,381),(557,360),(580,355),(604,361),(620,368),(613,389),(589,408),(560,410),(541,397)]],
  "eyebrows":[[(417,356),(428,347),(449,345),(470,348),(490,354),(501,363),(483,364),(463,357),(439,354)],[(537,363),(550,352),(574,346),(599,344),(617,350),(599,356),(576,358),(553,365)]],
  "nose":[[(502,405),(535,405),(541,432),(547,452),(540,467),(526,477),(514,479),(500,469),(491,459),(490,441)]],
  "mouth":[[(479,477),(496,471),(511,471),(519,474),(531,472),(548,478),(558,485),(551,501),(533,516),(511,518),(491,508),(479,493)]],
 },
}
LIP_CORE = {
 "male":[(481,480),(494,476),(507,475),(517,478),(531,476),(545,480),(554,483),(540,487),(528,493),(516,495),(503,492),(491,486)],
 "female":[(485,482),(499,478),(510,477),(518,480),(529,478),(541,482),(551,486),(539,492),(530,501),(519,503),(508,499),(498,492),(489,488)],
}
BROW_CORE={
 'male':[
  [(431,344),(446,343),(459,344),(474,347),(485,350),(493,355),(479,355),(455,351),(436,349)],
  [(541,354),(551,349),(567,344),(581,342),(598,342),(603,346),(586,348),(566,352),(549,356)]],
 'female':[
  [(427,349),(441,346),(456,347),(471,350),(486,355),(495,359),(479,358),(459,353),(440,352)],
  [(544,360),(554,354),(575,349),(594,346),(610,348),(599,352),(578,354),(554,361)]],
}


def fit_rear(sex):
    # A complete authored underpainting, with one uniform transform. It is
    # smaller than the visible frontal silhouette; there are no face holes,
    # neck holes, shoulder fades, or clipping to the composite silhouette.
    raw = read(V4 / "source/rear_hair_generated_candidate.png")
    s = .38
    x = 517 if sex == "male" else 519
    root_y = 271 if sex == "male" else 257
    matrix = np.array([[s,0,x-s*512],[0,s,root_y-s*260]],np.float32)
    af = raw[:,:,3].astype(np.float32)/255
    premul = raw[:,:,:3].astype(np.float32)*af[:,:,None]
    aa = cv2.warpAffine(af,matrix,(W,H),flags=cv2.INTER_LANCZOS4)
    cc = cv2.warpAffine(premul,matrix,(W,H),flags=cv2.INTER_LANCZOS4)
    out = np.zeros((H,W,4),np.uint8)
    out[:,:,:3] = np.round(np.clip(cc/np.maximum(aa[:,:,None],1/255),0,255))
    out[:,:,3] = np.round(np.clip(aa,0,1)*255)
    out[out[:,:,3]<10] = 0
    return out, matrix.tolist()


def frontal_regions(sex,clothing_alpha):
    """Visible AND in front: forehead cap, temple locks, chest tresses.

    The side drapes behind the neck/shoulders belong to the rear even though
    a frontal camera can see them. All points use our selected-master rig.
    """
    cap=polygon([(300,120),(725,120),(715,325),(683,374),(651,390),
                 (624,363),(600,330),(435,330),(418,363),(391,390),
                 (340,377),(312,322)])
    # A lock becomes a chest lock where it crosses onto the robe. The
    # continuation behind the bare neck remains rear hair, not a side panel
    # cut out and attached to the front slot.
    chest=union(HAIR_PATHS[sex][1:])
    chest[(YY<550)|(clothing_alpha==0)]=0
    side=union(SIDE_LOCKS[sex])*np.clip((447-YY)/30,0,1)
    return np.maximum.reduce([cap,side,chest])


def complete_posterior(layers,hair,sex):
    # Keep the complete authored rear painting. Restore the actual selected
    # posterior drapes over it, including the parts behind frontal locks.
    # The upper overlap is beneath the frontal cap, not a shoulder fade.
    posterior=hair.copy()
    support=polygon(HAIR_PATHS[sex][0])
    overlap=np.clip((YY-340)/80,0,1).astype(np.float32)
    overlap=overlap*overlap*(3-2*overlap)
    posterior[:,:,3]=np.round(posterior[:,:,3]*support*overlap)
    posterior[posterior[:,:,3]==0,:3]=0
    result=Image.fromarray(layers['hair_back'])
    result.alpha_composite(Image.fromarray(posterior))
    return np.array(result)


def clear_facial_opening(sex):
    left=([(438,337),(426,343),(418,353),(418,380),(421,410),(428,440)]
          if sex=='male' else
          [(432,337),(422,343),(416,357),(410,382),(405,410),(420,440)])
    right=([(599,337),(609,343),(615,353),(619,380),(624,410),(624,440)]
           if sex=='male' else
           [(600,337),(610,343),(615,357),(623,382),(632,410),(634,440)])
    return polygon(left+list(reversed(right)))>.03


def legal_alpha(target, under):
    # Work one channel at a time; retaining several full RGB temporaries
    # together with the authored masks can exhaust Windows commit memory.
    result=np.zeros(target.shape[:2],np.float32)
    for channel in range(3):
        t=target[:,:,channel];b=under[:,:,channel]
        delta=t-b
        denominator=np.where(delta<0,np.maximum(b,1),np.maximum(255-b,1))
        np.maximum(result,np.abs(delta)/denominator,out=result)
    return np.clip(result,0,1)


def extract_rgb(target, under, alpha, canvas_alpha=None):
    aa = np.ceil(np.clip(alpha,0,1)*255)/255
    fg = (target-(1-aa[:,:,None])*under)/np.maximum(aa[:,:,None],1/255)
    out = np.zeros((H,W,4),np.uint8)
    out[:,:,:3] = np.round(np.clip(fg,0,255))
    out[:,:,3] = np.round(aa*255)
    if canvas_alpha is not None:
        out[:,:,3] = np.minimum(out[:,:,3],canvas_alpha)
    out[out[:,:,3]==0,:3] = 0
    return out


def build(sex, geometry):
    master = read(SELECTED / "reference" / f"{sex}_selected_master.png")
    bald = read(SELECTED / "reference" / f"{sex}_bald_support.png")
    blank_source = read(PACKS / 'ink_portrait_pair_20261003_v3/source' / f'{sex}_clean_skin_generated.png')
    visible_face, full_face = geometry.face_geometry(bald,sex)
    rgb = master[:,:,:3].astype(np.float32)
    sr,sg,sb=rgb.transpose(2,0,1)
    # The selected jaw is a little different from the bald support. Include
    # its visible warm contour instead of treating that contour as back hair.
    own_jaw=(YY>395)&(YY<530)&(XX>421)&(XX<616)&(sr>90)&(sr-sg>17)&(sg-sb>5)&(master[:,:,3]>0)
    visible_face |= own_jaw
    full_face |= own_jaw
    support = union(HAIR_PATHS[sex])
    skin_delta = np.median(rgb[295:326,480:559]-bald[295:326,480:559,:3],axis=(0,1))
    own_bald = np.clip(bald[:,:,:3].astype(np.float32)+skin_delta,0,255)
    own_bald[own_jaw&(bald[:,:,3]==0)]=rgb[own_jaw&(bald[:,:,3]==0)]
    # Coloring is unchanged where it is actually visible. Hidden bald parts
    # are adapted only to match that selected clear forehead.
    own_bald[~full_face] = bald[~full_face,:3]
    r,g,b = rgb.transpose(2,0,1)
    # Compare with the real skin/cloth underpainting. A global neutral-color
    # key incorrectly drops warm hairline strokes and translucent strands on
    # blue/green robes. Projection measures the painted lock's coverage.
    direction = own_bald-np.array([35,32,30],np.float32)
    hair_color = np.clip(np.sum((own_bald-rgb)*direction,axis=2)/np.maximum(np.sum(direction*direction,axis=2),1),0,1)
    neutral_core = (r<175)&(r-g<24)&(r-g>-3)&(r-b<50)&(g-b<20)
    hair_color = np.maximum(hair_color,neutral_core.astype(np.float32))
    extended = cv2.dilate((support>.02).astype(np.uint8),np.ones((19,19),np.uint8))
    # The extension only recovers thin warm locks entering the forehead/ear
    # boundary. It cannot reach the central brow or eye organs.
    recovered_edge = (extended>0)&(YY<460)&(np.max(np.abs(rgb-own_bald),axis=2)>45)&(r-g<28)&(r<220)
    hair_owner = ((support>.02)&(hair_color>.15)|recovered_edge)&(master[:,:,3]>0)
    hair_owner |= (support>.02)&(bald[:,:,3]==0)&(master[:,:,3]>0)
    # Interior gaps are retained as transparency, not filled by a crop.
    hair_owner = cv2.morphologyEx(hair_owner.astype(np.uint8),cv2.MORPH_CLOSE,np.ones((3,3),np.uint8)).astype(bool)
    # Every stroke beyond the bald head/body must be frontal hair. This also
    # keeps the complete crown and thin flyaways with their original alpha.
    external = (bald[:,:,3]==0)&(master[:,:,3]>0)&(YY<705)
    hair_owner |= external
    side_locks=union(SIDE_LOCKS[sex])
    hair_owner |= (side_locks>.03)&(r<205)&(r-g<24)&(r-g>-3)
    hair_owner |= (side_locks>.03)&(YY<340)&(r<220)&(np.max(np.abs(rgb-own_bald),axis=2)>35)
    # Ear skin is part of our head. Only neutral, visibly changed ink strokes
    # may enter the hair slot within an ear, even when the support crosses it.
    ears=(YY>350)&(YY<452)&(((XX<424)&(XX>365))|((XX>618)&(XX<670)))
    ear_skin=(r-g>17)&(g-b>11)
    hair_owner[ears&ear_skin]=False
    eye_core=union(EYE_CORE[sex])
    brow_core=union(BROW_CORE[sex])
    # Visible eyelash wings and eyebrow tails are facial organs. Their dark
    # pixels cannot become hair just because a temple support overlaps them.
    hair_owner[(eye_core>.03)|(brow_core>.03)]=False
    clean_target = rgb.copy()
    clean_target[hair_owner] = own_bald[hair_owner]
    face = np.zeros_like(master)
    face[visible_face] = bald[visible_face]
    face[full_face,:3] = own_bald[full_face]
    face[full_face,3] = 255
    # Hidden neck is a continuous continuation of our own underpainting.
    ext = full_face & ~visible_face
    for channel in range(3):
        samples = own_bald[:,:,channel].copy()
        samples[ext] = 0
        filled = cv2.inpaint(samples.astype(np.uint8),ext.astype(np.uint8)*255,9,cv2.INPAINT_NS)
        face[ext,channel] = filled[ext]
    exposed = visible_face & ~hair_owner & (master[:,:,3]>0)
    face[exposed,:3] = rgb[exposed]
    face[exposed,3] = 255
    # Removing the selected hair is independent of the opacity key. Even a
    # faint warm hairline stroke cannot remain engraved on the blank head.
    erase_hair=visible_face&((support>.03)|(side_locks>.03))
    face[erase_hair,:3]=own_bald[erase_hair]
    forehead=visible_face&(YY<335)
    temples=visible_face&(YY>330)&(YY<435)&((XX<432)|(XX>615))
    face[forehead|temples,:3]=own_bald[forehead|temples]
    # The complete original ears are already authored in our bald reference.
    # Reusing selected split pixels here would bake scattered hair/alpha
    # fragments into the ear cartilage when frontal hair is hidden.
    clean_ears=visible_face&ears
    face[clean_ears,:3]=own_bald[clean_ears]
    # Gentle shared baseline across the entire facial support union removes
    # old features. Colored straight-alpha extraction restores the selected
    # feature pigment without gluing a plate of skin to the organ.
    fields = {k:union(v) for k,v in FEATURE_PATHS[sex].items()}
    fields = {k:cv2.dilate((v>.03).astype(np.uint8),np.ones((9,9),np.uint8)) for k,v in fields.items()}
    nose_field=np.zeros((H,W),np.uint8)
    cv2.ellipse(nose_field,(519,438),(48,64),0,0,360,1,-1)
    fields['nose']=np.maximum(fields['nose'],nose_field)
    fields['eyes'][fields['eyebrows']>0]=0
    fields['nose'][fields['eyes']>0]=0
    fields['nose'][YY>=470]=0
    fields['mouth'][YY<470]=0
    all_fields = np.maximum.reduce(list(fields.values()))
    blend = np.clip(cv2.distanceTransform(all_fields,cv2.DIST_L2,3)/5,0,1)
    sx = 1.4 if sex=='male' else 2.5
    sy = 1.33 if sex=='male' else 2.57
    source_top = 225 if sex=='male' else 85
    map_x=(512+(XX-517)*sx).astype(np.float32)
    map_y=(source_top+(YY-(171 if sex=='male' else 169))*sy).astype(np.float32)
    baseline=cv2.remap(blank_source[:,:,:3],map_x,map_y,cv2.INTER_LINEAR).astype(np.float32)
    tone = np.median(rgb[295:326,480:559]-baseline[295:326,480:559],axis=(0,1))
    baseline = np.clip(baseline+tone,0,255)
    clean = clean_target*(1-blend[:,:,None])+baseline*blend[:,:,None]
    eyelid=cv2.dilate((eye_core>.03).astype(np.uint8),np.ones((15,15),np.uint8))
    clear_eye_skin=(fields['eyes']>0)&(eyelid==0)&(clean_target.mean(2)>=clean.mean(2)-10)
    if sex=='male':
        glints=(((XX-466)/4)**2+((YY-360)/2)**2<1)|(((XX-567)/4)**2+((YY-360)/2)**2<1)
        clear_eye_skin &= ~glints
    # Paper grain and unmarked socket skin stay with the head; extracting
    # every texture difference made a floating skin-colored patch on eyes.
    clear_brow_skin=(fields['eyebrows']>0)&(brow_core<.03)&(clean_target.mean(2)>=clean.mean(2)-10)
    face[all_fields>0,:3]=np.round(clean[all_fields>0])
    neck_points=([(439,530),(593,530),(597,606),(580,666),(548,695),(517,713),(484,692),(451,658),(434,598),(439,565)] if sex=='male' else [(455,530),(584,530),(588,604),(572,665),(542,695),(517,713),(489,693),(458,657),(450,595),(454,565)])
    neck=polygon(neck_points)>.02
    neck &= YY>543
    br,bg,bb=own_bald.transpose(2,0,1)
    known_neck=neck&(bald[:,:,3]>240)&(br>150)&(br-bg>25)&(bg-bb>12)&(XX>468)&(XX<565)&(YY>550)&(YY<600)
    # Extend the real painted neck, never the candidate's translucent halo or
    # the robe's V outline. All nearest samples are actual bare skin.
    _,nearest=cv2.distanceTransformWithLabels((~known_neck).astype(np.uint8),cv2.DIST_L2,5,labelType=cv2.DIST_LABEL_PIXEL)
    neck_rgb=own_bald[known_neck][np.maximum(nearest-1,0)]
    neck_rgb=cv2.GaussianBlur(neck_rgb,(0,0),8)
    clear_grain=rgb[293:330,472:567]-cv2.GaussianBlur(rgb[293:330,472:567],(0,0),3)
    neck_rgb=np.clip(neck_rgb+clear_grain[(YY-543)%37,(XX-434)%95]*.7,0,255)
    complete_neck=neck|(full_face&(YY>543))
    face[complete_neck,:3]=np.round(neck_rgb[complete_neck]);face[complete_neck,3]=255
    bare_points=([(437,515),(597,515),(594,550),(584,577),(563,621),(531,690),(501,647),(470,602),(441,553)] if sex=='male' else [(452,515),(583,515),(584,552),(574,580),(551,634),(520,685),(493,645),(468,595),(450,553)])
    bare=polygon(bare_points)>.02
    bare_neck=neck&bare&(master[:,:,3]>0)&~hair_owner
    bare_distance=cv2.distanceTransform(bare.astype(np.uint8),cv2.DIST_L2,3)
    # Preserve clear central neck paint, but remove the complete former
    # collar boundary from the reusable head, including its pale wash.
    bare_blend=np.clip((bare_distance-14)/12,0,1)
    bare_blend=np.maximum(bare_blend,np.clip((585-YY)/20,0,1)*np.clip(bare_distance/3,0,1))
    collar_ink=bare_neck&(YY>560)&(bare_distance<26)
    face[bare_neck,:3]=np.round((rgb*bare_blend[:,:,None]+neck_rgb*(1-bare_blend[:,:,None]))[bare_neck])
    # One continuous featureless skin painting, rather than separate oval
    # patches whose boundaries remain visible when an organ is hidden.
    # Preserve the selected complexion's broad light/color distribution by
    # estimating a smooth tint only from clear authored skin, not organs.
    clear_skin=visible_face&(all_fields==0)&~hair_owner&(YY>278)&(YY<540)&(XX>438)&(XX<600)&(r>170)&(r-g>17)
    weights=cv2.GaussianBlur(clear_skin.astype(np.float32),(0,0),28)
    correction=cv2.GaussianBlur((rgb-baseline)*clear_skin[:,:,None],(0,0),28)/np.maximum(weights[:,:,None],.001)
    painted=np.clip(baseline+np.clip(correction,-18,18),0,255)
    head_interior=visible_face&(YY<541)&~ears
    head_blend=np.clip(cv2.distanceTransform(head_interior.astype(np.uint8),cv2.DIST_L2,3)/8,0,1)
    head_blend*=np.clip((YY-265)/65,0,1)*np.clip((541-YY)/15,0,1)
    face[:,:,:3]=np.round(face[:,:,:3]*(1-head_blend[:,:,None])+painted*head_blend[:,:,None])
    # The technical blank underpainting carried a few thin generator marks
    # beside the ear roots. Remove only those interior strokes; the authored
    # ears, jaw contour and exterior head outline remain untouched.
    scar_support=union([[(414,343),(428,343),(430,388),(414,402)],
                        [(612,343),(626,343),(626,400),(612,400)]])>.03
    fr,fg,fb=face[:,:,:3].astype(np.float32).transpose(2,0,1)
    scars=scar_support&visible_face&(fr<185)&(fr-fg<24)&(fg-fb<18)
    scars=cv2.dilate(scars.astype(np.uint8),np.ones((3,3),np.uint8))>0
    scars &= scar_support&visible_face
    # Two measured tiny brow-tail remnants lie in the warm skin at the ear
    # roots, so a neutral-ink key alone does not find them.
    scars |= (((XX-420)/3.8)**2+((YY-367)/12)**2<1)&visible_face
    scars |= (((XX-620)/3.8)**2+((YY-382)/10)**2<1)&visible_face
    restored=cv2.inpaint(face[:,:,:3],scars.astype(np.uint8)*255,5,cv2.INPAINT_NS)
    face[scars,:3]=restored[scars]
    clean=face[:,:,:3].astype(np.float32)
    del blank_source,baseline,painted,correction,weights,neck_rgb,map_x,map_y
    del restored,samples,filled,direction
    lip_core = polygon(LIP_CORE[sex])
    layers = {}
    for key,field in fields.items():
        alpha = legal_alpha(clean_target,clean)
        # Leave skin texture on the face. Difference strokes carry colored
        # pigment at fractional opacity; only the eyeball/lips are opaque.
        alpha[field==0]=0
        threshold=np.full((H,W),8,np.float32)
        if key=='nose':
            nose_pigment=((XX-519)/20)**2+((YY-446)/17)**2<1
            threshold[nose_pigment]=1
        alpha[np.max(np.abs(clean_target-clean),axis=2)<threshold]=0
        if key=='eyes':
            alpha=np.maximum(alpha,eye_core)
            outer_skin=(eye_core<.05)&(clean_target[:,:,0]>130)&((clean_target[:,:,0]-clean_target[:,:,1])>12)
            alpha[outer_skin]=np.minimum(alpha[outer_skin],.88)
            alpha[clear_eye_skin]=0
            # Outside the eyeball retain only attached eyelid ink, not a
            # rectangular skin wash or an accidental temple-hair fragment.
            distance=cv2.distanceTransform((eye_core<.03).astype(np.uint8),cv2.DIST_L2,3)
            outer_limit=9 if sex=='male' else 10
            edge_feather=np.clip((outer_limit-distance)/6,0,1)
            field_feather=np.clip(cv2.distanceTransform(field,cv2.DIST_L2,3)/5,0,1)
            outside=eye_core<.03
            alpha[outside]*=(edge_feather*field_feather)[outside]
            alpha[outside&(clean_target.mean(2)>=clean.mean(2)-2)]=0
            left_end=494 if sex=='male' else 495
            right_start=538 if sex=='male' else 544
            left_start=425 if sex=='male' else 423
            right_end=612 if sex=='male' else 618
            eye_span=((XX>=left_start)&(XX<=left_end))|((XX>=right_start)&(XX<=right_end))
            alpha[~eye_span]=0
            if sex=='male':
                alpha[((XX<432)&(YY<367))|((XX>602)&(YY<365))]=0
            else:
                alpha[((XX<433)&(YY<374))|((XX>606)&(YY<374))]=0
        if key=='eyebrows':
            alpha[YY<(340 if sex=='male' else 346)]=0
            alpha[clear_brow_skin]=0
            brow_distance=cv2.distanceTransform((brow_core<.03).astype(np.uint8),cv2.DIST_L2,3)
            alpha*=np.clip((1.8-brow_distance)/1.8,0,1)
        if key=='mouth':alpha=np.maximum(alpha,lip_core)
        alpha[~visible_face]=0
        alpha[hair_owner]=0
        # Disconnected slivers near a temple are hair fragments, not part of
        # the paired organs. Keep only components attached to their organ.
        if key in ('eyes','eyebrows'):
            components=(alpha>.015).astype(np.uint8)
            count,labels,stats,_=cv2.connectedComponentsWithStats(components,8)
            kept=np.zeros(count,bool)
            connected_core=eye_core>.1 if key=='eyes' else ((YY>340)&(YY<364)&(((XX>443)&(XX<491))|((XX>548)&(XX<598))))
            for index in range(1,count):
                kept[index]=bool(np.any(connected_core&(labels==index)))
            alpha[~kept[labels]]=0
        layers[key]=extract_rgb(clean_target,clean,alpha)
        if key=='eyes' and sex=='male':
            # Equal, restrained upper-lid wash in the two eye-local frames.
            # This replaces the former hard white island with soft symmetric
            # highlights, while preserving the selected iris/eye opening.
            glint=np.zeros((H,W,4),np.uint8)
            strength=np.maximum(np.exp(-(((XX-466)/2.5)**2+((YY-360)/.7)**2)/2),
                                np.exp(-(((XX-567)/2.5)**2+((YY-359)/.7)**2)/2))
            strength[strength<.02]=0
            glint[:,:,:3]=[247,230,211]
            glint[:,:,3]=np.round(strength*.20*255)
            overlay=Image.fromarray(layers[key]);overlay.alpha_composite(Image.fromarray(glint))
            layers[key]=np.array(overlay)
    clothing = np.zeros_like(master)
    cloth = (bald[:,:,3]>0)&~bare&~(visible_face&(YY<543))&(YY>=(520 if sex=='male' else 530))
    clothing[cloth]=bald[cloth]
    clear_cloth = cloth & ~hair_owner & (master[:,:,3]>0)
    clothing[clear_cloth]=master[clear_cloth]
    # Parts concealed by a former lock remain a real robe, not a cutout.
    clothing[cloth & (support>.03)]=bald[cloth & (support>.03)]
    # The V-edge line and its cast wash belong to the garment. Reconstruct
    # only their pigment over the clean neck, rather than leaving that V
    # engraved on the head or adding opaque bits of skin to the robe.
    collar_alpha=legal_alpha(rgb,face[:,:,:3].astype(np.float32))
    collar_alpha[~collar_ink]=0
    collar_alpha[np.max(np.abs(rgb-face[:,:,:3]),axis=2)<3]=0
    collar_layer=extract_rgb(rgb,face[:,:,:3].astype(np.float32),collar_alpha)
    collar_result=Image.fromarray(clothing);collar_result.alpha_composite(Image.fromarray(collar_layer))
    clothing=np.array(collar_result)
    del clean,clean_target,collar_layer,collar_alpha
    rear,rear_transform = fit_rear(sex)
    layers.update(face_shape=face,clothing=clothing,hair_back=rear,
                  face_accessory=np.zeros_like(master),hair_front=np.zeros_like(master))
    under = compose(layers)
    # Resolve the original hair against its actual new underpainting. Dark
    # cores retain their selected RGB, while warm mixed edges are unmixed.
    under_rgb = under[:,:,:3].astype(np.float32)
    # Recover faint warm hairline/temple strokes after cleaning the support,
    # retaining the selected soft painted transition rather than a hard cut.
    edge_area=(support>.02)|(side_locks>.02)|((extended>0)&(YY<335))
    late=edge_area&(master[:,:,3]>0)&((YY<335)|ears)&(rgb.mean(2)<under_rgb.mean(2)-6)
    late &= ~(ears&ear_skin)
    late &= (eye_core<.03)&(brow_core<.03)
    hair_owner|=late
    alpha = np.maximum(legal_alpha(rgb,under_rgb),hair_color)
    alpha[~hair_owner]=0
    opaque_core = hair_owner & neutral_core & (np.max(np.abs(rgb-own_bald),axis=2)>20)
    alpha[hair_owner&~opaque_core]=np.minimum(alpha[hair_owner&~opaque_core],.90)
    alpha[opaque_core]=1
    alpha[hair_owner & (under[:,:,3]==0)]=master[:,:,3][hair_owner & (under[:,:,3]==0)]/255
    hair = extract_rgb(rgb,under_rgb,alpha)
    direct = hair_owner & (under[:,:,3]==0)
    hair[direct]=master[direct]
    # A contaminated straight RGB fringe can contain robe blue/green or ear
    # skin. Keep the selected neutral ink and solve the occluded underpaint
    # instead of putting those fragments into the interchangeable hair PNG.
    aa=hair[:,:,3].astype(np.float32)/255
    fg=hair[:,:,:3].astype(np.float32)
    contaminated=hair_owner&~direct&~opaque_core&((fg.max(2)-fg.min(2)>35)|((fg[:,:,1]-fg[:,:,0])>12))
    value=np.clip(np.mean(fg,axis=2),24,155)
    pigment=np.stack([value+3,value,value-2],axis=2)
    repaired_under=(rgb-aa[:,:,None]*pigment)/np.maximum(1-aa[:,:,None],1/255)
    feasible=contaminated&(YY>335)&(repaired_under.min(2)>=0)&(repaired_under.max(2)<=255)
    hair[feasible,:3]=np.round(np.clip(pigment[feasible],0,255))
    face_pixels=np.zeros((H,W),bool)
    cloth_pixels=feasible&cloth
    face[face_pixels,:3]=np.round(repaired_under[face_pixels])
    clothing[cloth_pixels,:3]=np.round(repaired_under[cloth_pixels])
    # Final semantic check of the blank head: neutral dark strands in the
    # temple/ear band are removed even when their low opacity evaded the key.
    fr,fgc,fb=face[:,:,:3].astype(np.float32).transpose(2,0,1)
    skin_band=visible_face&(YY>330)&(YY<435)&((XX<435)|(XX>610))
    left_over=skin_band&(fr<165)&(np.abs(fr-fgc)<20)&(np.abs(fgc-fb)<15)
    face[left_over,:3]=np.round(own_bald[left_over])
    del own_bald,repaired_under,under_rgb,fg,pigment
    # The blank head and hidden robe have now been finalized. Resolve hair
    # against that final underpainting, rather than a stale pre-clean image;
    # otherwise even a one-pixel repair can leave a pale square at a temple.
    final_under=compose(layers)
    final_rgb=final_under[:,:,:3].astype(np.float32)
    revised_alpha=np.maximum(hair[:,:,3].astype(np.float32)/255,legal_alpha(rgb,final_rgb))
    revised_alpha[~hair_owner]=0
    repaired_hair=extract_rgb(rgb,final_rgb,revised_alpha)
    repaired_hair[direct]=master[direct]
    hair=repaired_hair
    # Ear-edge mixtures are resolved as neutral ink over the complete own
    # ear. A generic RGB unmix can otherwise export bright flesh speckles
    # into the hair and make the ear border appear stitched together.
    ear_fringe=ears&hair_owner&~opaque_core
    pigment=np.array([40,37,34],np.float32)
    direction=final_rgb-pigment
    coverage=np.clip(np.sum((final_rgb-rgb)*direction,axis=2)/np.maximum(np.sum(direction*direction,axis=2),1),0,1)
    hair[ear_fringe,:3]=pigment
    hair[ear_fringe,3]=np.round(coverage[ear_fringe]*255)
    # The old selected split includes light ear-outline pixels outside its
    # bald alpha. They are flesh/edge contamination, never frontal hair.
    ear_skin_fragments=ears&hair_owner&((rgb.mean(2)>175)|(r-g>17)|(g-b>11))
    hair[ear_skin_fragments]=0
    # The former rear/front split put a few ivory coat-edge pixels into hair.
    # Replace only those tiny bright strokes; retain the original alpha and
    # all neutral ink strands, including their transparent fine tips.
    bright=(hair[:,:,3]>80)&(hair[:,:,:3].mean(2)>185)&(YY>520)&(YY<720)
    if np.any(bright):
        hair[:,:,:3]=cv2.inpaint(hair[:,:,:3],bright.astype(np.uint8)*255,3,cv2.INPAINT_NS)
    # No forced opaque plate on the outside; preserve the selected flyaways.
    hair[:,:,3][hair_owner & (master[:,:,3]<255) & (under[:,:,3]<20)]=master[:,:,3][hair_owner & (master[:,:,3]<255) & (under[:,:,3]<20)]
    hair[ear_skin_fragments]=0
    hair[hair[:,:,3]==0,:3]=0
    # Anatomical ownership overrides color keys. Remove the disconnected
    # brow/eye strokes from both hair slots, including the brow-eye gap.
    opening=clear_facial_opening(sex)
    hair[opening]=0
    frontal=frontal_regions(sex,layers['clothing'][:,:,3])
    front=hair.copy();front[:,:,3]=np.round(front[:,:,3]*frontal)
    front[front[:,:,3]==0,:3]=0
    layers['hair_back']=complete_posterior(layers,hair,sex)
    layers['hair_front']=front
    return layers,master,eye_core,lip_core,hair_owner,visible_face,rear_transform


def font(n):
    return ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)


def previews(sex,layers,master):
    combined=compose(layers)
    save(ROOT/f'{sex}_portrait.png',combined)
    for name,color in [('light','#e8e3dc'),('dark','#252423')]:
        background(combined,color).save(ROOT/'preview'/f'{sex}_{name}.png')
    sheet=Image.new('RGB',(1050,1470),'#d6d0c7')
    d=ImageDraw.Draw(sheet)
    for n,key in enumerate(ORDER):
        x,y=n%3*350,n//3*490
        d.text((x+12,y+10),NAMES[key],font=font(18),fill='#292521')
        tile=Image.new('RGBA',(326,448),'#e8e3dc')
        a=Image.fromarray(layers[key]);box=a.getchannel('A').getbbox()
        if box:
            crop=a.crop((max(box[0]-8,0),max(box[1]-8,0),min(box[2]+8,W),min(box[3]+8,H)))
            crop.thumbnail((310,432),Image.Resampling.LANCZOS)
            tile.alpha_composite(crop,((326-crop.width)//2,(448-crop.height)//2))
        sheet.paste(tile.convert('RGB'),(x+12,y+36))
    sheet.save(ROOT/'preview'/f'{sex}_nine_parts.png')
    for key in ['hair_front','hair_back','eyes','eyebrows','nose','mouth','clothing']:
        background(compose(layers,[key])).save(ROOT/'preview'/f'{sex}_without_{key}.png')
    for key in ['eyes','eyebrows','nose','mouth','face_shape']:
        a=Image.fromarray(layers[key]).crop((350,160,695,695) if key=='face_shape' else (409,330,632,526))
        for name,color in [('light','#e8e3dc'),('dark','#252423')]:
            tile=Image.new('RGBA',a.size,color);tile.alpha_composite(a)
            tile.convert('RGB').resize((a.width*3,a.height*3)).save(ROOT/'preview'/f'{sex}_{key}_close_{name}.png')
    board=Image.new('RGB',(1024,660),'#e8e3dc')
    for n,a in enumerate([master,combined]):
        b=background(a).crop((305,125,730,720));b.thumbnail((460,610))
        board.paste(b,(n*512+30,40))
        ImageDraw.Draw(board).text((n*512+30,10),'选定母版' if n==0 else '清理后九件拼合',font=font(18),fill='#292521')
    board.save(ROOT/'preview'/f'{sex}_comparison.png')
    return combined


def main():
    (ROOT/'preview').mkdir(parents=True,exist_ok=True)
    spec=importlib.util.spec_from_file_location('own_bald_geometry',PACKS/'ink_portrait_pair_20261003_v2/build_assets.py')
    geometry=importlib.util.module_from_spec(spec);spec.loader.exec_module(geometry)
    reports={};pair=Image.new('RGB',(2048,1536),'#e8e3dc')
    for n,sex in enumerate(['male','female']):
        layers,master,eyes,lips,hair_owner,face_visible,rear_transform=build(sex,geometry)
        for key,a in layers.items():save(ROOT/sex/f'{key}.png',a)
        combined=previews(sex,layers,master)
        pair.paste(background(combined),(n*W,0))
        diff=np.abs(np.array(background(master)).astype(int)-np.array(background(combined)).astype(int))
        facial=(YY>=310)&(YY<=540)&(XX>=425)&(XX<=612)&~hair_owner
        eye=layers['eyes'];skin=(eye[:,:,3]>240)&(eyes<.05)&(eye[:,:,0]>130)&((eye[:,:,0].astype(int)-eye[:,:,1])>12)
        no_rear=compose(layers,['hair_back'])
        reports[sex]={
            'selected_master_sha256':hashlib.sha256((SELECTED/'reference'/f'{sex}_selected_master.png').read_bytes()).hexdigest(),
            'canvas':[W,H],'export_offset':[0,0],
            'pair_light_rgb_mean_error':float(diff.mean()),
            'facial_light_rgb_max_error':int(diff[facial].max()),
            'opaque_eye_skin_outside_opening':int(skin.sum()),
            'complete_rear_single_uniform_transform':rear_transform,
            'rear_center_alpha':int(layers['hair_back'][440,517,3]),
            'visible_rear_difference_pixels':int(np.any(no_rear!=combined,axis=2).sum()),
            'zero_alpha_rgb_zero':all(not np.any(a[:,:,:3][a[:,:,3]==0]) for a in layers.values()),
            'bounds':{k:Image.fromarray(a[:,:,3]).getbbox() for k,a in layers.items()},
        }
    pair.save(ROOT/'preview/pair_light.png')
    (ROOT/'matting_verification.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'authoring_geometry.json').write_text(json.dumps({'hair_paths':HAIR_PATHS,'side_locks':SIDE_LOCKS,'eye_core':EYE_CORE,'brow_core':BROW_CORE,'feature_paths':FEATURE_PATHS,'lip_core':LIP_CORE},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(reports,ensure_ascii=False,indent=2))


if __name__=='__main__':main()
