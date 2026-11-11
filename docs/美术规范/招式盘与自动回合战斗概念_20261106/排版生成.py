from pathlib import Path
from xml.sax.saxutils import escape

HERE = Path(__file__).resolve().parent
INK = '#293e3a'
PAPER = '#f5f0e2'
SUB = '#66796a'
GOLD = '#a9936e'
SAGE = '#668875'
RED = '#a75d4c'

def rect(x, y, w, h, fill, stroke='none', r=0, sw=1, opacity=1):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" fill-opacity="{opacity}" stroke="{stroke}" stroke-width="{sw}"/>'

def text(x, y, s, size=18, fill=INK, weight=400, anchor='start'):
    return f'<text x="{x}" y="{y}" font-size="{size}" fill="{fill}" font-weight="{weight}" text-anchor="{anchor}">{escape(s)}</text>'

def img(x, y, w, h, path, fit='xMidYMid meet', clip=''):
    cp = f' clip-path="url(#{clip})"' if clip else ''
    return f'<image href="{escape(path)}" x="{x}" y="{y}" width="{w}" height="{h}" preserveAspectRatio="{fit}"{cp}/>'

def svg(parts):
    return '''<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="1600" height="900" viewBox="0 0 1600 900">
<style>text{font-family:"Microsoft YaHei UI","Microsoft YaHei","SimHei",sans-serif} .grid{stroke:#b8c1aa;stroke-width:1}</style>
''' + '\n'.join(parts) + '\n</svg>\n'

SK = '../../../textures/ui/combat/skill_icons/'
ITEM = '../../../textures/items/inventory_density_20261029/'
skill = {
    '应敌':'凡俗_应敌_v5.png', '痛击':'凡俗_痛击_v5.png', '架招':'凡俗_架招_v5.png',
    '聚炁':'基础技能_聚炁_v2.png', '催血':'基础技能_催血_v3.png', '发劲':'基础技能_发劲_v1.png',
    '卸力':'基础技能_卸力_v1.png', '平息':'基础技能_平息_v1.png', '寄念':'基础技能_寄念_v2.png',
    '折锋':'斫基_折锋_v2.png', '云帔':'玄枢_云帔_v2.png', '焚元':'斫基_焚元_v2.png',
    '斩剑':'剑修基础_斩剑_v1.png', '血臂撼岳':'百骸_血臂撼岳_v3.png',
}

def tile(x, y, name, status, fraction, selected=False, width=108, height=53):
    p = [rect(x, y, width, height, '#fdf9ef', '#537d69' if selected else '#aeb7a2', 4, 2 if selected else 1)]
    if width < 60:
        p.append(img(x+4, y+3, width-8, 65, SK+skill[name]))
        p.append(text(x+width/2, y+82, name, 12, INK, 600, 'middle'))
        p.append(text(x+width/2, y+97, status, 10, '#607267', 400, 'middle'))
        p.append(rect(x+5, y+height-6, width-10, 3, '#d8ded0', r=2))
        p.append(rect(x+5, y+height-6, round((width-10)*fraction), 3, '#71987c', r=2))
    elif height > 70:
        p.append(img(x+4, y+4, 48, height-8, SK+skill[name]))
        p.append(text(x+54, y+30, name, 12, INK, 600))
        p.append(text(x+54, y+50, status, 10, '#607267'))
        p.append(rect(x+54, y+60, width-60, 4, '#d8ded0', r=2))
        p.append(rect(x+54, y+60, round((width-60)*fraction), 4, '#71987c', r=2))
    else:
        p.append(img(x+3, y+2, 37, 48, SK+skill[name]))
        p.append(text(x+43, y+18, name, 15, INK, 600))
        p.append(text(x+43, y+34, status, 11, '#607267'))
        p.append(rect(x+43, y+40, 57, 4, '#d8ded0', r=2))
        p.append(rect(x+43, y+40, round(57*fraction), 4, '#71987c', r=2))
    return p

def battle_image():
    p = [rect(0,0,1600,900,'#e9e0ca')]
    p.append(img(0,-95,1600,900,'../男女首套回合制人物_20261106/正式队列_1v1.png','xMidYMid slice'))
    p.append(rect(0,0,1600,78,'#eee8d8',opacity=.98))
    p.append(rect(0,77,1600,2,'#899b88'))
    p += [text(22,29,'招式盘 · 回合间整理',23,INK,700), text(23,54,'布局与进度示意  ·  6列×4行候选  ·  非实机画面',13,SUB)]
    p += [rect(524,13,646,51,'#f9f5e9','#bdc9b6',6),text(539,34,'行动条',15,INK,700),text(539,54,'到点处理整行',11,SUB)]
    p.append(rect(626,34,522,5,'#d8e1d1',r=2))
    p.append(rect(626,34,345,5,'#779785',r=2))
    p += [rect(792,17,81,28,'#d9e7d5','#8ca98e',5),text(832,36,'玉虚修士',12,INK,600,'middle')]
    p += [rect(963,17,69,28,'#e9dfd5','#bba08b',5),text(997,36,'对手',12,INK,600,'middle')]
    p += [rect(1200,19,130,37,'#f9f5e9','#a6b4a0',6),text(1265,43,'暂停中',16,INK,600,'middle')]
    p += [rect(1342,19,112,37,'#f9f5e9','#a6b4a0',6),text(1398,43,'倍速 ×1',15,INK,600,'middle')]
    p += [rect(1466,19,112,37,'#f9f5e9','#a6b4a0',6),text(1522,43,'战斗记录',15,INK,600,'middle')]
    p += [rect(47,91,276,52,'#f6f1e3','#bac7b4',7,opacity=.94), text(62,113,'回合间 · 全员已遍历',16,INK,700),text(62,133,'调阵完成后进入下一回合',12,SUB)]
    p += [rect(630,91,340,58,'#f6f1e3','#bac7b4',7,opacity=.97),text(649,114,'体力  3/6',16,INK,700),text(770,114,'法力  4/6',16,INK,700),text(649,135,'资源数值为界面示例',12,SUB)]
    p += [rect(1278,91,273,52,'#f6f1e3','#bac7b4',7,opacity=.94),text(1292,113,'对方构筑：未洞察',16,INK,700),text(1292,133,'未展示对方招式与布局',12,SUB)]
    p += [rect(17,492,1566,390,PAPER,'#a3ae9a',10,2,opacity=.98),rect(27,502,1546,370,'#fbf8ed','#e0e0cf',7)]
    p += [text(44,531,'可换入',20,INK,700),text(309,531,'本人的招式盘',20,INK,700),text(309,551,'6 × 4 · 占格示意',12,SUB),text(682,531,'当前选中',20,INK,700),text(1050,531,'其他角色',20,INK,700)]
    for x in [291,664,1031]: p.append(f'<path d="M{x} 515 V851" stroke="#d5decf"/>')
    p += [rect(43,544,68,28,'#ddeadd','#a1b9a3',5),text(77,563,'招式',13,INK,600,'middle')]
    p += [rect(116,544,68,28,'#f5f3e9','#d2d9c9',5),text(150,563,'丹药',13,INK,500,'middle')]
    p += [rect(189,544,68,28,'#f5f3e9','#d2d9c9',5),text(223,563,'符箓',13,INK,500,'middle')]
    p.append(text(44,595,'招式来源示意',13,SUB,600))
    for y,name,sub in [(603,'催血','示例 · 资格需核验'),(653,'焚元','示例 · 资格需核验')]:
        p.append(rect(43,y,228,44,'#fffdf5','#d5ddce',4))
        p.append(img(48,y+2,30,40,SK+skill[name]))
        p += [text(86,y+18,name,14,INK,600),text(86,y+34,sub,11,SUB)]
    p += [text(44,722,'本人行囊 · 库存示意',13,SUB,600)]
    p.append(rect(43,730,228,44,'#fffdf5','#d5ddce',4))
    p.append(img(49,733,28,38,ITEM+'pill_container_empty_porcelain.png'))
    p += [text(86,748,'回春丹',14,INK,600),text(86,765,'可补入 · 示例数量 2',11,SUB)]
    p.append(rect(43,780,228,44,'#fffdf5','#d5ddce',4))
    p += [rect(51,786,23,30,'#e7d6b1','#a48b63',2),text(62,806,'符',13,INK,600,'middle'),text(86,798,'符箓',14,INK,600),text(86,815,'可补入 · 示例项目',11,SUB)]
    bx,by,cell=309,566,55
    p.append(rect(bx,by,330,220,'#eeeedd','#b9c4af',4))
    for col in range(7): p.append(f'<path class="grid" d="M{bx+col*cell} {by} V{by+220}"/>')
    for row in range(5): p.append(f'<path class="grid" d="M{bx} {by+row*cell} H{bx+330}"/>')
    data = [(0,0,2,1,'应敌','已蓄满',1), (0,3,2,1,'痛击','蓄势中',.65),
            (1,0,1,2,'架招','待命',1), (1,1,2,1,'斩剑','蓄势中',.4),
            (1,4,1,2,'聚炁','蓄势中',.7), (2,1,2,2,'血臂撼岳','蓄势中',.55)]
    occupied={(2,5)}
    for row,col,w,h,name,status,frac in data:
        occupied.update((rr,cc) for rr in range(row,row+h) for cc in range(col,col+w))
    for row in range(4):
        for col in range(6):
            if (row,col) in occupied: continue
            x=bx+col*cell; y=by+row*cell
            p.append(rect(x+2,y+2,51,49,'#e2eadc','#a6bca5',3))
            p.append(text(x+27,y+22,'空格',12,SUB,500,'middle'))
            p.append(text(x+27,y+39,'回体力',10,'#64876a',600,'middle'))
    for row,col,w,h,name,status,frac in data:
        p += tile(bx+col*cell+1,by+row*cell+1,name,status,frac,name=='应敌',w*cell-2,h*cell-2)
    x=bx+5*cell; y=by+2*cell
    p.append(rect(x+1,y+1,53,53,'#fffbf2','#b9c4af',4))
    p.append(img(x+14,y+2,27,36,ITEM+'pill_container_empty_porcelain.png'))
    p.append(text(x+27,y+48,'回春',10,INK,500,'middle'))
    p += [text(309,810,'空格另回体力（数值待定），轮初 +3 保留',13,'#587966',600),text(309,831,'从左到右、从上到下；行动条到点处理整行',11,SUB),text(309,851,'仅空白格回复；图中占格与蓄势为示意',11,SUB)]
    p += [rect(681,549,332,227,'#f8f6ed','#d9e0d1',6),img(699,562,105,147,SK+skill['应敌']),text(822,579,'应敌',21,INK,700),text(822,603,'已蓄满 · 状态示意',13,'#5c7d66',600),text(822,626,'体力 1',14,INK),text(822,650,'自动随机选择合法敌人',12,SUB),text(822,675,'图像保持正向、等比显示',12,SUB),text(822,697,'进度与对象绑定',12,SUB)]
    p += [rect(700,738,292,7,'#d7e0d2',r=4),rect(700,738,292,7,'#71987c',r=4),text(700,767,'调整不刷新蓄势；具体结算边界待审',11,SUB)]
    p += [rect(1050,549,500,227,'#f4f2e7','#d4dacb',6),text(1070,576,'各参战者各有盘面',15,INK,600),text(1070,600,'本图仅展示主控角色；同伴按真实控制权限显示',12,SUB)]
    p += [rect(1070,617,460,135,'#e6e8dc','#c6cdbd',5),text(1300,680,'？',42,'#839887',500,'middle'),text(1300,711,'对方布阵未洞察',15,'#697e6d',500,'middle')]
    p += [text(1050,804,'移动 · 换入已学招式 · 补入本人丹符',13,INK,600),text(1050,824,'符箓与库存均为界面示例，不代表已接入',11,SUB)]
    p += [rect(1240,837,136,32,'#f5f3e9','#adbca9',5),text(1308,858,'放弃调整',13,INK,500,'middle'),rect(1387,837,161,32,'#577965','#577965',5),text(1467,858,'继续战斗',13,'#f8f8ef',600,'middle')]
    return svg(p)

def sketch_frame(x,y,title,desc):
    return [rect(x,y,175,218,'#faf6e9','#c1cebc',7),text(x+87,y+23,title,16,INK,700,'middle'),text(x+87,y+203,desc,11,SUB,400,'middle')]

def icon_image():
    p=[rect(0,0,1600,900,'#eee9d9'),rect(22,22,1556,856,'#fbf8ed','#acbba8',10,2)]
    p += [text(49,63,'招式图标 · 复用与差异化方向',27,INK,700),text(50,89,'视觉草图：现有资源保持原样，示例不代表新图已制作或接入',14,SUB)]
    p += [rect(48,112,684,423,'#f3f2e8','#d0d9c9',7),text(69,143,'现有共图：两组不同招式',18,INK,700),text(69,166,'每组内当前引用同一张图；盘面仍保留技能名称。',12,SUB)]
    for i,(name,source) in enumerate([('云帔','云帔'),('虚怀纳劲','云帔'),('斩剑','斩剑'),('云岚三叠','斩剑')]):
        x=70+i*160
        p.append(rect(x,185,140,255,'#fffdf5','#c5cfbd',6))
        p.append(img(x+15,196,110,185,SK+skill[source]))
        p += [text(x+70,405,name,16,INK,700,'middle'),text(x+70,426,'当前共用'+source+'图',11,SUB,400,'middle')]
    p += [text(69,475,'云帔与虚怀纳劲：共同护持，但主动／应对有别。',14,INK,600),text(69,499,'斩剑与云岚三叠：共同剑势，但技能来源与效果不同。',12,SUB)]
    p += [rect(750,112,800,423,'#f3f2e8','#d0d9c9',7),text(770,143,'差异化方向：墨形草图',18,INK,700),text(770,166,'仅表达不同轮廓，依据既有招式实际效果细化；不是正式素材。',12,SUB)]
    cards=[(770,'云帔','云气护身'),(960,'虚怀纳劲','临敌护持'),(1150,'斩剑','单道剑势'),(1340,'云岚三叠','连续剑势')]
    for x,title,sub in cards: p += sketch_frame(x,184,title,sub)
    # Distinct broad silhouettes in a deliberately unfinished sketch language.
    p += ['<path d="M802 334 C 782 310, 795 279, 823 272 C 836 239, 876 244, 884 275 C 917 273, 929 310, 909 334 M 812 336 Q 857 315 901 336" fill="none" stroke="#648775" stroke-width="9" stroke-linecap="round"/>',
          '<path d="M992 337 Q 1007 281 1030 265 Q 1049 247 1056 281 L 1060 316 M 1027 299 L 1092 274 M 1039 312 L 1093 295 M 990 348 Q 1040 358 1101 327" fill="none" stroke="#52675c" stroke-width="9" stroke-linecap="round"/>',
          '<path d="M1190 341 L 1254 260 M 1203 350 L 1266 268 M 1187 323 L 1212 346 M 1182 338 L 1199 354 M 1254 260 L 1271 249 L 1266 268" fill="none" stroke="#52675c" stroke-width="8" stroke-linecap="round"/>',
          '<path d="M1373 340 Q 1400 297 1453 270 M 1385 351 Q 1414 308 1467 281 M 1398 362 Q 1427 319 1480 292" fill="none" stroke="#52675c" stroke-width="8" stroke-linecap="round"/>']
    p += [text(770,450,'墨色、留白和轮廓要可在小格中辨认；不以图案暗示额外技能效果。',12,SUB),text(770,478,'配色只做辅助，名称与状态文字始终保留。',12,SUB)]
    p += [rect(48,554,1502,291,'#f3f2e8','#d0d9c9',7),text(69,587,'不同占格的显示方式',18,INK,700),text(69,610,'图标等比、正向放入占格；形状可旋转时，外框可旋转，人物插画不跟随横倒。',13,SUB)]
    p += [rect(93,637,90,163,'#fffdf5','#8eaa90',6,2),img(98,643,80,151,SK+skill['应敌']),text(138,824,'竖向占格示意',13,INK,500,'middle')]
    p += [text(226,720,'→',30,SUB,500,'middle'),rect(276,669,200,90,'#fffdf5','#8eaa90',6,2),img(338,673,75,82,SK+skill['应敌']),text(376,824,'横向占格示意，插画仍正向',13,INK,500,'middle')]
    p += [text(555,669,'招式图',16,INK,700),text(555,693,'沿用既有水墨人物／劲势图；按占格等比适配。',13,SUB),text(555,724,'丹药',16,INK,700),text(555,748,'继续使用现有瓷瓶图，用名称和效果区分不同丹药。',13,SUB),text(555,779,'蓄势与可用性',16,INK,700),text(555,803,'外层进度条和文字叠加，不修改原图或编码新效果。',13,SUB)]
    p += [text(1118,671,'制作边界',16,INK,700),text(1118,695,'建议先复用能清楚识别的现有图。',13,SUB),text(1118,720,'同图不同招式列为后续补绘清单。',13,SUB),text(1118,745,'草图线稿不是游戏内最终画风。',13,SUB),text(1118,793,'所有占格、状态及数量均为展示候选。',12,RED)]
    return svg(p)

(HERE/'战斗整理界面_候选.svg').write_text(battle_image(),encoding='utf-8')
(HERE/'招式图标对照与方向_候选.svg').write_text(icon_image(),encoding='utf-8')
