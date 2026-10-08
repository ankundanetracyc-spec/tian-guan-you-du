"""Generate the runtime resource for the broad nine-state world map.

The editable SVG and the preview image remain the design source.  This file
turns the same coordinates into a Godot Resource, keeping the 5x5 mask grid
as a loading detail rather than a political grid.
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Dict, Iterable, List, Sequence, Tuple

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from build_zhaoming_vast_nine_states_draft import (  # noqa: E402
    CANGMING_POLYGON,
    MASK_COLORS,
    STATES,
    runtime_point,
)


OUT = ROOT / "resources" / "world" / "zhaoming_vast_nine_states_map.tres"
MASK_DIR = "res://textures/world_map/territory_masks_vast_nine_states_5x5_8000"

NODE_TYPES = {
    "City": 0,
    "Sect": 1,
    "Resource": 2,
    "Dungeon": 3,
    "War": 4,
    "Village": 5,
    "Home": 6,
}
DISPLAY_LEVELS = {"World": 1, "Region": 2, "Local": 3}


STATE_META: Dict[str, Dict[str, str]] = {
    "xuanshuo": {
        "name": "玄朔州",
        "subtitle": "北冥雪原",
        "description": "北冥雪原与极北冰海相接，雪关、冻土、裂谷和古老守界遗迹绵延数千里。这里既有镇守边荒的宗门，也有逐冰海而生的商旅与部族。",
        "main_faction": "北极仙宫、玄枵院、雪关诸部",
        "faction_summary": "守界宗门、北境学宫、冰海部族与边地商路并存；州名来自地理与旧朝疆域，不代表某一条修行流派。",
    },
    "xiangshan": {
        "name": "相山州",
        "subtitle": "相山北海",
        "description": "相山州由起伏山脉、北海群礁与古代地脉构成，山庄、分阁、渡口和散修集市沿地脉分布。",
        "main_faction": "九脉山庄、天机阁相山分阁、北海商盟",
        "faction_summary": "地师家族、商盟分阁和地方宗门共同经营山海通道；玩家可从客卿、学徒或独行寻脉者切入。",
    },
    "guanhe": {
        "name": "关河州",
        "subtitle": "西陵荒域",
        "description": "西陵荒域从风蚀盆地伸向古河道，边军戍所、矿脉、荒漠城镇与无人险地相互交错。",
        "main_faction": "西陵戍所、玄黄矿盟、荒域行商",
        "faction_summary": "军务、矿业、古道商旅和寻遗修士各有立足处；危险来自地形、旧战场与失控的灵脉，而非单一宗门。",
    },
    "yunque": {
        "name": "云阙州",
        "subtitle": "中州玉虚",
        "description": "云阙州位于九州腹心，河网、湖泽、古都和田野向四方铺开，是最适合初次远行、求学和建立人脉的中州腹地。",
        "main_faction": "玉虚仙宗、白鹿书院、青云观、澄心碑会",
        "faction_summary": "传法宗门、书院、地方道观和地方势力彼此牵制；青溪里只是南缘的一处起始落脚点。",
    },
    "donghua": {
        "name": "东华州",
        "subtitle": "东陆机巧",
        "description": "东华州拥有平原、河港、工坊城与灵材产区，阵法、炼器、丹道和商贸在这里交汇，亦有大量不依附宗门的匠修。",
        "main_faction": "千机城、丹鼎会、百器门",
        "faction_summary": "城邦工坊、丹师会馆、器师门庭和跨州商路并立；加入任何一方都不会锁定玩家的修行流派。",
    },
    "cangming": {
        "name": "沧溟州",
        "subtitle": "东海群岛",
        "description": "沧溟州是以群岛、海峡、潮汐和远洋航线构成的海上疆域，岛屿之间的距离本身就是修行与贸易的门槛。",
        "main_faction": "蓬莱仙盟、瀛洲剑派、方丈仙岛",
        "faction_summary": "仙盟、岛宗、海商、海族与寻宝者共用航路；瀛洲剑派只是群岛势力之一，不代表整州只修剑。",
    },
    "yunxiu": {
        "name": "云岫州",
        "subtitle": "百脉山原",
        "description": "百脉山原由层叠山原、峡谷、古道和高地城镇组成，山脉之间保留着多种传承与地方技艺。铸剑城只是其中一座地方城邦。",
        "main_faction": "铸剑城、长风镖局、百脉散修会",
        "faction_summary": "城邦、镖局、山民聚落与不同传承的山门共同存在；这里强调地理交通和百脉交汇，不把整州定义成剑修州。",
    },
    "cangwu": {
        "name": "苍梧州",
        "subtitle": "南荒万山",
        "description": "南荒万山横跨雨林、瘴泽、古木与群峰，妖族、部族、药师和探遗者都在这里留下聚落与道路。",
        "main_faction": "药王谷、万妖谷、五仙教",
        "faction_summary": "药师谷地、妖族联盟、部族祭祀与外来探险队各自守着不同山路；善恶与归属不会只按族群划分。",
    },
    "guichao": {
        "name": "归潮州",
        "subtitle": "南冥海疆",
        "description": "南冥海疆由潮汐海岸、自由港、群岛和归墟外环组成，是九州面向未知外洋的前沿。",
        "main_faction": "天衡仙国、不夜港、潮生门、南冥海商",
        "faction_summary": "河口仙国、自由港、潮汐宗门、海商和异族航队共存；愿力作为后续与灵力分立的玩法入口，归墟则是跨越九州边界的高危门槛。",
    },
}


def node(
    node_id: str,
    name: str,
    node_type: str,
    display_level: str,
    x: int,
    y: int,
    territory_id: str,
    description: str,
    faction_id: str,
    *,
    locked: bool = True,
    terrain: str = "",
    spirit_pressure: int = 3,
    danger_level: int = 3,
    population: int = 0,
    unlock_hint: str = "",
) -> Dict[str, object]:
    state_name = STATE_META[territory_id]["name"]
    if not unlock_hint:
        unlock_hint = f"先在序章后取得出门资格，再沿路线探索{state_name}，与当地人建立联系。"
    return {
        "id": node_id,
        "name": name,
        "type": node_type,
        "level": display_level,
        "point": (x, y),
        "territory": territory_id,
        "description": description,
        "faction": faction_id,
        "locked": locked,
        "terrain": terrain,
        "spirit": spirit_pressure,
        "danger": danger_level,
        "population": population,
        "unlock_hint": unlock_hint,
    }


NODES: List[Dict[str, object]] = [
    node("home_village", "青溪里", "Home", "World", 1280, 795, "yunque", "序章的起始落脚点。离开这里后，青溪里仍作为可回访的个人修行据点存在。", "青溪里", locked=False, terrain="湖畔村落", spirit_pressure=2, danger_level=1, population=120),
    node("white_stone_town", "白石镇", "City", "World", 1130, 700, "yunque", "连接青溪里与中州驿路的第一座镇子，可补给、打听消息并接触各类行旅修士。", "白石镇商会", locked=False, terrain="河谷城镇", spirit_pressure=3, danger_level=2, population=2600),
    node("east_woods", "东林地", "Resource", "World", 1370, 760, "yunque", "青溪里东侧的林地资源区，适合采集灵药、灵材和低阶野外材料。", "青溪里", locked=False, terrain="低丘林地", spirit_pressure=3, danger_level=2, population=40),
    node("herb_valley", "灵草谷", "Resource", "Region", 1200, 850, "yunque", "河网与湿地交汇的药材产区，谷内灵草受季候影响明显。", "灵草谷采药会", terrain="湖泽药谷", spirit_pressure=5, danger_level=4, population=180),
    node("qinglan_observatory", "青岚观", "Sect", "World", 1010, 600, "yunque", "靠近中州水脉的地方道观，收留游学修士，也为远行者提供观星、问路和短期清修。", "青岚观", locked=False, terrain="丘陵道观", spirit_pressure=5, danger_level=2, population=300, unlock_hint="序章后可先从青溪里或白石镇打听青岚观的入山路径。"),
    node("xuan_ferry", "玄水渡", "City", "World", 900, 430, "xuanshuo", "通往北境与相山北海的水陆换乘渡口，季节性的冰汛会改变可走路线。", "玄水渡行会", terrain="河口渡城", spirit_pressure=4, danger_level=4, population=900),
    node("mirror_marsh", "镜月泽", "Dungeon", "Region", 1350, 900, "yunque", "湖泽深处会映出并不存在的道路，既可能发现遗迹，也可能误入临时异象。", "无主险地", terrain="镜泽湿地", spirit_pressure=7, danger_level=6, population=0),

    node("arctic_palace", "北极仙宫", "Sect", "World", 650, 230, "xuanshuo", "北境守界宗门，镇守冰海裂隙与雪关旧线，门下并非只有战斗修士。", "北极仙宫", terrain="雪原冰海", spirit_pressure=8, danger_level=5, population=6200),
    node("xuanxiao_academy", "玄枵院", "Sect", "Region", 820, 300, "xuanshuo", "研究北冥星历、冰脉与古代天象的学宫，允许外来修士以客席、学徒或委托人身份往来。", "玄枵院", terrain="雪原学宫", spirit_pressure=7, danger_level=4, population=1400),
    node("nine_vein_manor", "九脉山庄", "City", "World", 1200, 350, "xiangshan", "掌握多条山脉旧图的地师家族聚居地。玩家可在此作为家族客卿发展，不进入家族经营管理。", "九脉山庄·客卿", terrain="山原庄园", spirit_pressure=6, danger_level=3, population=2200),
    node("tianji_branch", "天机阁·相山分阁", "Sect", "Region", 1450, 380, "xiangshan", "天机阁在相山州的分阁，提供地脉测绘、情报交易和短期委托。", "天机阁", terrain="北海山城", spirit_pressure=6, danger_level=3, population=1800),

    node("xiling_garrison", "西陵戍所", "War", "World", 360, 620, "guanhe", "古道边的军务据点，负责维护关河商路与失控灵脉的警戒线。", "西陵戍所", terrain="荒漠关城", spirit_pressure=4, danger_level=6, population=3400),
    node("ghost_cry_ridge", "鬼哭岭", "Dungeon", "Region", 560, 700, "guanhe", "旧战场与风蚀峡谷叠在一起，夜间回声常会引出并不属于此地的脚步。", "无主险地", terrain="风蚀峡谷", spirit_pressure=7, danger_level=8, population=0),
    node("xuanhuang_mine", "玄黄矿脉", "Resource", "Region", 450, 520, "guanhe", "被玄黄砂包围的古矿脉，矿工和寻遗修士都在争夺更深处的旧坑。", "玄黄矿盟", terrain="荒漠矿区", spirit_pressure=5, danger_level=6, population=600),

    node("yuxu_sect", "玉虚仙宗", "Sect", "World", 1050, 565, "yunque", "中州传法宗门，拥有多种兼修与客席路径，是九州诸多修士往来的节点之一。", "玉虚仙宗", terrain="中州山门", spirit_pressure=8, danger_level=3, population=9000),
    node("xian_kingdom", "天衡仙国", "City", "World", 1840, 990, "guichao", "位于归潮州北部、东华州南缘的河口腹地，以城邑、驿路与国祀维系广域秩序。后续愿力玩法在此展开；愿力与灵力分立，不替代修士的吐纳、境界与个人成长。", "天衡仙国", terrain="河口仙都", spirit_pressure=7, danger_level=4, population=24000),
    node("bailu_academy", "白鹿书院", "Sect", "Region", 990, 720, "yunque", "兼收凡俗学子与修行者的书院，重视典籍、推演、游学和地方见闻。", "白鹿书院", terrain="河畔书院", spirit_pressure=5, danger_level=2, population=2600),
    node("chengxin_steles", "澄心碑会", "Sect", "Region", 1120, 770, "yunque", "由碑林守护者、游方学者和修士共同维持的地方组织，适合接取短期研究与护送委托。", "澄心碑会", terrain="湖畔碑林", spirit_pressure=6, danger_level=3, population=500),

    node("qianji_city", "千机城", "City", "World", 1590, 650, "donghua", "东华州的大型工坊城，阵法、机关、炼器和商队在城中形成复杂的协作网络。", "千机城", terrain="东陆工坊城", spirit_pressure=7, danger_level=3, population=24000),
    node("dan_ding_hall", "丹鼎会", "Sect", "Region", 1510, 730, "donghua", "由丹师与药商组成的会馆，提供学习、试炼和炼丹材料交易。", "丹鼎会", terrain="河港会馆", spirit_pressure=6, danger_level=3, population=1100),
    node("baiji_gate", "百器门", "Sect", "Region", 1690, 760, "donghua", "器师门庭与外来工匠共用的山门，重视器物实作，不限制弟子兼修其他道路。", "百器门", terrain="工坊山门", spirit_pressure=6, danger_level=4, population=1500),

    node("sword_forge_city", "铸剑城", "City", "World", 620, 925, "yunxiu", "百脉山原中的地方城邦，以铸剑工艺闻名，但城内同时有镖局、药商、山民与多种散修传承。", "铸剑城", terrain="高地城邦", spirit_pressure=6, danger_level=4, population=7600),
    node("changfeng_escort", "长风镖局", "City", "Region", 820, 720, "yunxiu", "跨越山原和中州的行旅组织，玩家可以以护送、采集或独行客身份建立关系。", "长风镖局", terrain="山口驿站", spirit_pressure=4, danger_level=4, population=420),

    node("yaowang_valley", "药王谷", "Sect", "World", 1030, 1060, "cangwu", "南荒药师聚居的山谷，接受外来修士以弟子、客卿或采药合作人身份往来。", "药王谷", terrain="雨林药谷", spirit_pressure=8, danger_level=5, population=4800),
    node("demon_valley", "万妖谷", "Sect", "World", 1200, 1000, "cangwu", "多族妖修和通灵者共同维护的山谷联盟，内部有不同族群与个人修行传统。", "万妖谷", terrain="南荒群峰", spirit_pressure=9, danger_level=7, population=5200),
    node("wuxian_teach", "五仙教", "Sect", "Region", 1320, 1120, "cangwu", "擅长虫、蛇、蛊与山地药理的地方宗门，和药王谷既合作又有边界争执。", "五仙教", terrain="瘴泽山门", spirit_pressure=8, danger_level=7, population=1700),
    node("cangwu_sunfall", "苍梧落日台", "Dungeon", "Region", 1080, 1210, "cangwu", "古木与断崖之间的观日遗址，传说藏有跨越南荒群山的旧航标。", "无主险地", terrain="古木断崖", spirit_pressure=8, danger_level=8, population=0),

    node("penglai_alliance", "蓬莱仙盟", "Sect", "World", 1960, 250, "cangming", "由海岛宗门、航队和修士家族组成的松散仙盟，负责维护东海主航线。", "蓬莱仙盟", terrain="东海群岛", spirit_pressure=8, danger_level=5, population=6800),
    node("yingzhou_sect", "瀛洲剑派", "Sect", "Region", 2070, 420, "cangming", "东海群岛中的一支剑道宗门，影响范围主要在本岛与附近航线，不代表整州修行方向。", "瀛洲剑派", terrain="海岛山门", spirit_pressure=8, danger_level=5, population=2300),
    node("fangzhang_isle", "方丈仙岛", "Sect", "Region", 2160, 510, "cangming", "以岛屿秘境、灵植和古代航标闻名的仙岛，常年只对持有信物者开放。", "方丈仙岛", terrain="远海仙岛", spirit_pressure=9, danger_level=6, population=900),
    node("tianwen_cliff", "天问崖", "Dungeon", "Region", 1880, 600, "cangming", "位于外海边缘的断崖与古观星台，风暴季节会出现无法解释的星光潮。", "无主海崖", terrain="外海断崖", spirit_pressure=9, danger_level=7, population=0),

    node("notnight_port", "不夜港", "City", "World", 1700, 1110, "guichao", "南冥自由港，商队、海盗、散修、异族和各地中介都能在此找到临时落脚处。", "不夜港", terrain="潮汐港城", spirit_pressure=7, danger_level=5, population=16000),
    node("chaosheng_gate", "潮生门", "Sect", "Region", 1850, 1200, "guichao", "依潮汐而建的海门宗派，掌握部分南冥近海航路与潮汐法阵。", "潮生门", terrain="潮汐海门", spirit_pressure=8, danger_level=6, population=2100),
    node("guixu", "归墟", "Dungeon", "Region", 2130, 1260, "guichao", "南冥海疆的深海险地，既是高危探索目标，也是后期通往外洋与异象空间的门槛。", "归墟", terrain="深海异境", spirit_pressure=10, danger_level=10, population=0),
]


def domain(
    domain_id: str,
    name: str,
    faction_id: str,
    faction_name: str,
    state_id: str,
    parent_id: str,
    level: int,
    polygon: Sequence[Point],
    description: str,
    governance: str,
    direct: bool = False,
) -> Dict[str, object]:
    color = MASK_COLORS[state_id]
    return {
        "id": domain_id,
        "name": name,
        "faction_id": faction_id,
        "faction_name": faction_name,
        "state": state_id,
        "parent": parent_id,
        "level": level,
        "polygon": list(polygon),
        "description": description,
        "governance": governance,
        "direct": direct,
        "region_subtitle": "大势力范围" if level == 1 else governance,
        "faction_summary": f"上级势力：{faction_name}；治理关系：{governance}。",
        "fill_rgb": color,
        "fill_alpha": 0.13 if level == 1 else 0.075,
        "outline_rgb": color,
        "outline_alpha": 0.82 if level == 1 else 0.58,
        "min_zoom": 1.2 if level == 1 else 2.0,
    }


# Political layers are intentionally independent of the natural-state masks.
# A major sphere is an umbrella polygon; its level-2 children are the direct
# core and subordinate fiefs shown only after the player zooms in.
DOMAINS: List[Dict[str, object]] = [
    domain("xuanshuo_arctic", "北极仙宫辖域", "arctic_palace", "北极仙宫", "xuanshuo", "xuanshuo", 1,
           [(210, 420), (315, 255), (525, 140), (825, 115), (1090, 205), (1035, 405), (815, 485), (585, 430), (355, 525), (205, 585)],
           "北极仙宫在北冥雪原维持守界、观天与冰海互市秩序。", "仙宫统辖",),
    domain("xuanshuo_arctic_core", "北极仙宫直辖核心", "arctic_palace", "北极仙宫", "xuanshuo", "xuanshuo_arctic", 2,
           [(430, 260), (560, 190), (770, 180), (930, 245), (900, 350), (760, 400), (575, 365), (450, 330)],
           "仙宫山门、守界台与冰原灵脉组成的直辖核心。", "大势力直辖", True),
    domain("xuanshuo_xuanxiao", "玄枵院属地", "arctic_palace", "北极仙宫", "xuanshuo", "xuanshuo_arctic", 2,
           [(300, 290), (430, 260), (450, 330), (365, 500), (255, 520), (240, 420)],
           "以星历、冰川测绘和古代守界档案见长的学宫属地。", "学宫属地"),
    domain("xuanshuo_snow_pass", "雪关诸部属地", "arctic_palace", "北极仙宫", "xuanshuo", "xuanshuo_arctic", 2,
           [(770, 180), (1000, 230), (970, 370), (900, 350), (900, 245)],
           "雪关部族、驭兽队与边地商旅共享的冰海前哨。", "部族盟属"),

    domain("xiangshan_nine_veins", "九脉山庄辖域", "nine_vein_manor", "九脉山庄", "xiangshan", "xiangshan", 1,
           [(1090, 205), (1435, 165), (1690, 255), (1765, 435), (1590, 535), (1350, 515), (1160, 430), (1035, 405)],
           "九脉山庄依山海地脉经营寻脉、渡运和古图保全。", "山庄统辖"),
    domain("xiangshan_nine_core", "九脉山庄直辖核心", "nine_vein_manor", "九脉山庄", "xiangshan", "xiangshan_nine_veins", 2,
           [(1210, 235), (1420, 205), (1585, 275), (1570, 385), (1430, 455), (1260, 410), (1150, 335)],
           "山庄本部和九条地脉交会的直辖山城。", "大势力直辖", True),
    domain("xiangshan_tianji", "天机阁相山分阁属地", "nine_vein_manor", "九脉山庄", "xiangshan", "xiangshan_nine_veins", 2,
           [(1420, 205), (1650, 275), (1720, 420), (1585, 385), (1585, 275)],
           "天机阁分阁借山庄地脉权限开展测绘、推演和情报交易。", "分阁属地"),
    domain("xiangshan_northsea", "北海商盟属地", "nine_vein_manor", "九脉山庄", "xiangshan", "xiangshan_nine_veins", 2,
           [(1115, 220), (1210, 235), (1150, 335), (1065, 385), (1070, 300)],
           "北海商盟控制群礁渡口和海货集散点，保留较大的自治空间。", "商盟盟属"),

    domain("guanhe_xiling", "西陵戍所辖域", "xiling_garrison", "西陵戍所", "guanhe", "guanhe", 1,
           [(205, 585), (355, 525), (585, 430), (815, 485), (750, 735), (565, 860), (330, 790), (195, 690)],
           "西陵戍所以关隘、军镇和旧河道为骨架维持荒域秩序。", "戍所统辖"),
    domain("guanhe_xiling_core", "西陵戍所直辖核心", "xiling_garrison", "西陵戍所", "guanhe", "guanhe_xiling", 2,
           [(260, 600), (380, 555), (500, 515), (500, 680), (420, 760), (300, 720), (235, 660)],
           "戍所关城、军田和旧朝烽燧组成的直辖核心。", "军镇直辖", True),
    domain("guanhe_mine_league", "玄黄矿盟属地", "xiling_garrison", "西陵戍所", "guanhe", "guanhe_xiling", 2,
           [(500, 515), (575, 455), (785, 505), (720, 630), (600, 650), (500, 680)],
           "矿盟负责玄黄矿脉的开采、护运与工坊分成。", "矿盟属地"),
    domain("guanhe_ghost_ridge", "鬼哭岭属地", "xiling_garrison", "西陵戍所", "guanhe", "guanhe_xiling", 2,
           [(500, 680), (600, 650), (720, 630), (725, 710), (550, 820), (420, 760)],
           "旧战场峡谷由戍所登记、探遗队和地方守墓人共同管理。", "战场管辖"),
    domain("guanhe_black_white", "黑白坊市属地", "xiling_garrison", "西陵戍所", "guanhe", "guanhe_xiling", 2,
           [(260, 600), (500, 515), (500, 680), (420, 760), (300, 720), (235, 660)],
           "荒域行商聚集的黑白坊市，以通行凭证和货物税换取戍所保护。", "坊市属地"),

    domain("yunque_yuxu", "玉虚仙宗辖域", "yuxu_sect", "玉虚仙宗", "yunque", "yunque", 1,
           [(815, 485), (1035, 405), (1160, 430), (1350, 515), (1330, 745), (1160, 865), (890, 815), (750, 735)],
           "玉虚仙宗在中州腹地主持传法、护脉和跨州修士往来。", "仙宗统辖"),
    domain("yunque_yuxu_core", "玉虚仙宗直辖核心", "yuxu_sect", "玉虚仙宗", "yunque", "yunque_yuxu", 2,
           [(960, 470), (1080, 435), (1190, 475), (1220, 600), (1130, 680), (980, 640), (900, 550)],
           "玉虚山门、主峰灵脉与传法台组成的宗门直辖地。", "宗门直辖", True),
    domain("yunque_qingyun", "青云观属地", "yuxu_sect", "玉虚仙宗", "yunque", "yunque_yuxu", 2,
           [(820, 500), (960, 470), (900, 550), (980, 640), (900, 790), (780, 710), (800, 600)],
           "青云观是中州地方道观网络的一支，保留独立收徒与游方传统。", "道观属地"),
    domain("yunque_bailu", "白鹿书院属地", "yuxu_sect", "玉虚仙宗", "yunque", "yunque_yuxu", 2,
           [(1190, 475), (1320, 535), (1310, 720), (1150, 840), (1030, 780), (980, 640), (1130, 680), (1220, 600)],
           "白鹿书院以典籍、游学和地方治理学问为主，接受玉虚传法保护。", "书院属地"),
    domain("yunque_chengxin", "澄心碑会属地", "yuxu_sect", "玉虚仙宗", "yunque", "yunque_yuxu", 2,
           [(1100, 640), (1220, 600), (1310, 720), (1150, 840), (1030, 780), (1040, 700)],
           "碑会守护古碑与地方记忆，按自身规约而非宗门戒律行事。", "碑会属地"),

    domain("donghua_qianji", "千机城辖域", "qianji_city", "千机城", "donghua", "donghua", 1,
           [(1350, 515), (1590, 535), (1765, 435), (1855, 600), (1940, 850), (1720, 945), (1470, 835), (1330, 745)],
           "千机城以工坊、河港和器物贸易为核心，吸纳各类匠修与商队。", "城邦统辖"),
    domain("donghua_qianji_core", "千机城直辖核心", "qianji_city", "千机城", "donghua", "donghua_qianji", 2,
           [(1510, 560), (1650, 550), (1760, 620), (1760, 760), (1650, 830), (1500, 760)],
           "千机城主城、核心工坊和河港税关组成的城邦直辖地。", "城邦直辖", True),
    domain("donghua_gongsun", "公孙氏阵府属地", "qianji_city", "千机城", "donghua", "donghua_qianji", 2,
           [(1365, 545), (1510, 560), (1500, 760), (1390, 720), (1345, 630)],
           "公孙氏阵府负责城外阵基、桥梁和灵脉工事。", "家族属地"),
    domain("donghua_danding", "丹鼎会属地", "qianji_city", "千机城", "donghua", "donghua_qianji", 2,
           [(1760, 620), (1835, 610), (1900, 830), (1760, 760)],
           "丹鼎会控制东陆丹材集散与丹师互助会馆。", "会馆属地"),
    domain("donghua_baiji", "百器门属地", "qianji_city", "千机城", "donghua", "donghua_qianji", 2,
           [(1650, 830), (1760, 760), (1900, 830), (1715, 925), (1500, 815)],
           "百器门在城邦许可下维护器师山门与外来工匠聚落。", "门庭属地"),

    domain("yunxiu_alliance", "百脉会盟辖域", "yunxiu_alliance", "百脉会盟", "yunxiu", "yunxiu", 1,
           [(330, 790), (565, 860), (750, 735), (890, 815), (865, 1015), (720, 1135), (525, 1090), (370, 1140), (270, 925)],
           "百脉会盟是山原城邦、镖局与地方家族的松散议约，不把云岫州归为单一流派。", "会盟统辖"),
    domain("yunxiu_alliance_core", "百脉会盟直辖核心", "yunxiu_alliance", "百脉会盟", "yunxiu", "yunxiu_alliance", 2,
           [(560, 875), (735, 755), (840, 835), (735, 980), (560, 1030), (450, 950)],
           "山原议庭与共用山口构成的会盟直辖地。", "会盟直辖", True),
    domain("yunxiu_sword_forge", "铸剑城盟属地", "yunxiu_alliance", "百脉会盟", "yunxiu", "yunxiu_alliance", 2,
           [(350, 805), (560, 875), (450, 950), (385, 1120), (300, 930)],
           "铸剑城以地方城邦身份参加会盟，同时保有自己的铸造与市政传统。", "城邦盟属"),
    domain("yunxiu_changfeng", "长风镖局盟属地", "yunxiu_alliance", "百脉会盟", "yunxiu", "yunxiu_alliance", 2,
           [(735, 980), (840, 835), (865, 1000), (705, 1115), (535, 1075), (560, 1030)],
           "长风镖局负责山口驿路和跨州护送，是会盟最重要的流动组织。", "镖局盟属"),
    domain("yunxiu_clans", "山原家族盟属地", "yunxiu_alliance", "百脉会盟", "yunxiu", "yunxiu_alliance", 2,
           [(735, 755), (865, 835), (840, 835), (735, 980), (560, 875)],
           "各地家族以客卿与互市参与会盟，不形成单一宗门控制。", "家族盟属"),

    domain("cangwu_demon", "万妖谷辖域", "demon_valley", "万妖谷", "cangwu", "cangwu", 1,
           [(865, 1015), (890, 815), (1160, 865), (1330, 745), (1470, 835), (1510, 1065), (1380, 1215), (1035, 1305), (720, 1135)],
           "万妖谷以族群议约、山路互市和南荒灵脉共同维持多族聚居地。", "妖谷统辖"),
    domain("cangwu_demon_core", "万妖谷直辖核心", "demon_valley", "万妖谷", "cangwu", "cangwu_demon", 2,
           [(1150, 885), (1310, 760), (1450, 850), (1380, 980), (1260, 1040), (1120, 1000)],
           "万妖谷主谷、族群议场和古木灵脉组成的直辖核心。", "妖谷直辖", True),
    domain("cangwu_yaowang", "药王谷属地", "demon_valley", "万妖谷", "cangwu", "cangwu_demon", 2,
           [(880, 1000), (900, 835), (1050, 880), (1120, 1000), (1040, 1150), (900, 1120)],
           "药王谷以采药、救治和灵植培育参与南荒事务。", "药谷属地"),
    domain("cangwu_wuxian", "五仙教属地", "demon_valley", "万妖谷", "cangwu", "cangwu_demon", 2,
           [(1120, 1000), (1260, 1040), (1360, 1190), (1040, 1280), (1040, 1150)],
           "五仙教守着瘴泽与虫蛇灵脉，在万妖谷的统辖范围内保持教门边界。", "教门属地"),
    domain("cangwu_tribes", "百族部落盟属地", "demon_valley", "万妖谷", "cangwu", "cangwu_demon", 2,
           [(1380, 980), (1485, 1050), (1360, 1190), (1260, 1040)],
           "南荒百族部落以山路、猎场和季节性聚落方式参加万妖谷议约。", "部族盟属"),

    domain("cangming_penglai", "蓬莱仙盟辖域", "penglai_alliance", "蓬莱仙盟", "cangming", "cangming", 1,
           [(1740, 140), (2030, 130), (2310, 220), (2340, 500), (2160, 650), (1900, 690), (1760, 520)],
           "蓬莱仙盟串联东海岛宗、航队和修士家族，维持群岛间的主航权。", "仙盟统辖"),
    domain("cangming_penglai_core", "蓬莱仙盟直辖核心", "penglai_alliance", "蓬莱仙盟", "cangming", "cangming_penglai", 2,
           [(1830, 150), (2010, 145), (2110, 230), (2050, 360), (1900, 380), (1800, 280)],
           "蓬莱主岛、盟议庭和东海航标组成的仙盟直辖核心。", "仙盟直辖", True),
    domain("cangming_yingzhou", "瀛洲剑派盟属地", "penglai_alliance", "蓬莱仙盟", "cangming", "cangming_penglai", 2,
           [(2110, 230), (2310, 240), (2320, 480), (2180, 560), (2050, 360)],
           "瀛洲剑派只在本岛和附近航线拥有影响力，是仙盟下的地方岛宗。", "岛宗盟属"),
    domain("cangming_fangzhang", "方丈仙岛盟属地", "penglai_alliance", "蓬莱仙盟", "cangming", "cangming_penglai", 2,
           [(1800, 380), (2050, 360), (2180, 560), (2130, 650), (1900, 680), (1760, 500)],
           "方丈仙岛以灵植、秘境和古航标维护自己的岛屿传统。", "仙岛盟属"),

    domain("guichao_tianheng", "天衡仙国辖域", "tianheng_kingdom", "天衡仙国", "guichao", "guichao", 1,
           [(1550, 1075), (1720, 945), (1940, 850), (2050, 1065), (1910, 1210), (1640, 1265), (1380, 1350), (1380, 1215)],
           "天衡仙国在归潮州北部和东华南缘建立河口、城邑与愿契秩序。愿力玩法在此展开，并与灵力分立。", "仙国统辖"),
    domain("guichao_tianheng_core", "天衡仙国直辖河口", "tianheng_kingdom", "天衡仙国", "guichao", "guichao_tianheng", 2,
           [(1720, 970), (1925, 875), (1980, 1020), (1910, 1120), (1770, 1120), (1660, 1050)],
           "天衡都城、河口神坛与愿契官署组成的仙国直辖核心。", "仙国直辖", True),
    domain("guichao_notnight", "不夜港盟属地", "tianheng_kingdom", "天衡仙国", "guichao", "guichao_tianheng", 2,
           [(1580, 1085), (1660, 1050), (1770, 1120), (1680, 1210), (1450, 1260), (1430, 1210)],
           "不夜港接受天衡的水关保护，同时保留自由港自治权。", "自由港盟属"),
    domain("guichao_chaosheng", "潮生门属地", "tianheng_kingdom", "天衡仙国", "guichao", "guichao_tianheng", 2,
           [(1910, 1120), (1980, 1020), (2030, 1070), (1900, 1190), (1680, 1210), (1770, 1120)],
           "潮生门掌管部分潮汐法阵与近海灵脉，受仙国文书约束但保持宗门自治。", "宗门属地"),
    domain("guichao_sea_clans", "鲛纱阁盟属地", "tianheng_kingdom", "天衡仙国", "guichao", "guichao_tianheng", 2,
           [(1925, 875), (2030, 1070), (1980, 1020), (1910, 1120)],
           "鲛纱阁与南冥海商、海族航队共同维护外海互市，属于仙国共管属地。", "海商盟属"),
]


def route(route_id: str, name: str, from_id: str, to_id: str, days: int, description: str, unlocked: bool = False) -> Dict[str, object]:
    return {
        "id": route_id,
        "name": name,
        "from": from_id,
        "to": to_id,
        "days": days,
        "description": description,
        "unlocked": unlocked,
    }


ROUTES: List[Dict[str, object]] = [
    route("qingxi_white_stone", "青溪近路", "home_village", "white_stone_town", 1, "沿湖岸和旧驿道行走，适合作为序章后的第一条远行路线。", True),
    route("qingxi_east_woods", "东林地小径", "home_village", "east_woods", 2, "穿过村东林地，采集与修行可以在同一趟行程中完成。", True),
    route("white_qinglan", "白石镇—青岚观山道", "white_stone_town", "qinglan_observatory", 2, "从镇后山口进入青岚观，雨季需要绕行。", True),
    route("qingxi_xuan_ferry", "青溪—玄水渡旧水路", "home_village", "xuan_ferry", 5, "通向北境和相山的旧水路，需先取得渡口凭信。"),
    route("white_herb", "白石镇—灵草谷药道", "white_stone_town", "herb_valley", 2, "药商与采药人反复使用的湿地小道，灵草季会变得拥挤。"),
    route("white_changfeng", "白石镇—长风镖路", "white_stone_town", "changfeng_escort", 3, "随镖队翻越山口，可换取护送经验与沿途消息。"),
    route("qinglan_yuxu", "青岚观—玉虚山门", "qinglan_observatory", "yuxu_sect", 3, "连接中州地方道观与玉虚仙宗的传法山路。"),
    route("qinglan_bailu", "青岚观—白鹿书院", "qinglan_observatory", "bailu_academy", 2, "沿河谷下行的游学路线，适合携带典籍和见闻前往。"),
    route("bailu_xian", "白鹿书院—天衡仙国远行线", "bailu_academy", "xian_kingdom", 10, "由云阙沿人皇旧道东行，转入东华南缘与南冥河口，需经过数个关驿与水陆换乘点。"),
    route("xian_qianji", "天衡仙国—千机城古运线", "xian_kingdom", "qianji_city", 8, "沿归潮北部河口折返东华工坊带，连接愿契文书、器物贸易与跨州商路。"),
    route("qianji_dan", "千机城—丹鼎会", "qianji_city", "dan_ding_hall", 2, "工坊城内外的丹材采购路线。"),
    route("qianji_baiji", "千机城—百器门", "qianji_city", "baiji_gate", 3, "从城东工坊区前往器师山门的道路。"),
    route("qianji_night", "千机城—不夜港海道", "qianji_city", "notnight_port", 7, "从东华河港转入南冥海线，受季风影响明显。"),
    route("xian_notnight", "天衡仙国—不夜港潮路", "xian_kingdom", "notnight_port", 3, "沿归潮北部潮河与盐田下行至自由港，是仙国人员、海商和外来修士最常用的短线。"),
    route("xuan_ferry_night", "玄水渡—不夜港远航线", "xuan_ferry", "notnight_port", 12, "横跨九州东南的远航路线，需要可靠船队或仙国凭证。"),
    route("xiling_ghost", "西陵戍所—鬼哭岭", "xiling_garrison", "ghost_cry_ridge", 2, "进入旧战场峡谷的警戒线，戍所会要求留下行踪记录。"),
    route("xiling_mine", "西陵戍所—玄黄矿脉", "xiling_garrison", "xuanhuang_mine", 3, "通往矿区的荒漠补给路，沙暴季需要跟随矿盟车队。"),
    route("xiling_sword", "西陵戍所—铸剑城古道", "xiling_garrison", "sword_forge_city", 6, "穿过关河与百脉山原的旧朝商道。"),
    route("sword_changfeng", "铸剑城—长风镖局", "sword_forge_city", "changfeng_escort", 2, "山城与镖局的近路，适合打听跨州行程。"),
    route("sword_yaowang", "铸剑城—药王谷山线", "sword_forge_city", "yaowang_valley", 6, "翻越百脉山原南坡，途中有药材交换点。"),
    route("sword_demon", "铸剑城—万妖谷山线", "sword_forge_city", "demon_valley", 7, "穿越山原与南荒交界，需尊重各族的山路规矩。"),
    route("yaowang_demon", "药王谷—万妖谷雨林线", "yaowang_valley", "demon_valley", 3, "南荒内部的通行路线，外来者需有熟人引路。"),
    route("yaowang_wuxian", "药王谷—五仙教瘴泽线", "yaowang_valley", "wuxian_teach", 4, "穿行瘴泽的药师路线，需准备驱瘴与辨毒手段。"),
    route("yaowang_sunfall", "药王谷—苍梧落日台", "yaowang_valley", "cangwu_sunfall", 4, "通往南荒古航标的支线。"),
    route("demon_wuxian", "万妖谷—五仙教山口线", "demon_valley", "wuxian_teach", 3, "族群与宗门共享的山口线路，季节变化会影响安全度。"),
    route("arctic_xuanxiao", "北极仙宫—玄枵院雪线", "arctic_palace", "xuanxiao_academy", 3, "沿冰脊前往星历学宫，需准备御寒与辨识裂隙的手段。"),
    route("xuanxiao_nine", "玄枵院—九脉山庄北线", "xuanxiao_academy", "nine_vein_manor", 4, "穿越北境山口与相山北缘的古线。"),
    route("nine_yuxu", "九脉山庄—玉虚仙宗地脉线", "nine_vein_manor", "yuxu_sect", 6, "沿古地脉南下的山路，地师与传法修士常在此交换消息。"),
    route("nine_tianji", "九脉山庄—天机阁相山分阁", "nine_vein_manor", "tianji_branch", 2, "相山州内部的测绘与情报路线。"),
    route("penglai_yingzhou", "蓬莱仙盟—瀛洲航线", "penglai_alliance", "yingzhou_sect", 3, "东海主航线的一段，潮汐和风向决定实际耗时。"),
    route("penglai_night", "蓬莱仙盟—不夜港南航线", "penglai_alliance", "notnight_port", 8, "从东海群岛驶向南冥自由港的远洋航线。"),
    route("yingzhou_fangzhang", "瀛洲—方丈仙岛", "yingzhou_sect", "fangzhang_isle", 4, "岛宗之间的季风航路，只有在特定潮窗开放。"),
    route("notnight_chaosheng", "不夜港—潮生门", "notnight_port", "chaosheng_gate", 2, "沿南冥潮岸东行，港口商队与潮生门弟子共用。"),
    route("chaosheng_guixu", "潮生门—归墟", "chaosheng_gate", "guixu", 5, "进入深海异境的危险航段，通常需要组队或持有特殊信物。"),
    route("tianwen_penglai", "天问崖—蓬莱仙盟", "tianwen_cliff", "penglai_alliance", 5, "外海观星台与东海主航线之间的断续航路。"),
    route("tianwen_fangzhang", "天问崖—方丈仙岛", "tianwen_cliff", "fangzhang_isle", 4, "风暴前后才较为稳定的外海支线。"),
    route("east_mirror", "东林地—镜月泽", "east_woods", "mirror_marsh", 2, "从林地边缘进入湖泽的野路，雨后会出现镜面异象。"),
    route("xuan_mirror", "玄水渡—镜月泽水线", "xuan_ferry", "mirror_marsh", 4, "沿河网南下的水线，需先取得渡口许可。"),
]


def gd_string(value: object) -> str:
    text = str(value)
    return '"' + text.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'


def gd_color(rgb: Sequence[int], alpha: float) -> str:
    values = [max(0.0, min(1.0, channel / 255.0)) for channel in rgb[:3]] + [max(0.0, min(1.0, alpha))]
    return "Color(" + ", ".join(f"{value:.6f}" for value in values) + ")"


def gd_vector(point: Sequence[int]) -> str:
    return f"Vector2({point[0]}, {point[1]})"


def gd_polygon(points: Iterable[Sequence[int]]) -> str:
    return "PackedVector2Array(" + ", ".join(f"{x}, {y}" for x, y in points) + ")"


def gd_string_array(values: Iterable[str]) -> str:
    return "Array[String]([" + ", ".join(gd_string(value) for value in values) + "])"


def transform_polygon(points: Iterable[Sequence[int]]) -> List[Tuple[int, int]]:
    return [runtime_point((int(point[0]), int(point[1]))) for point in points]


def build_adjacency() -> Dict[str, List[str]]:
    adjacency = {str(item["id"]): [] for item in NODES}
    for item in ROUTES:
        source = str(item["from"])
        target = str(item["to"])
        if target not in adjacency[source]:
            adjacency[source].append(target)
        if source not in adjacency[target]:
            adjacency[target].append(source)
    return adjacency


def write_resource() -> None:
    adjacency = build_adjacency()
    lines: List[str] = [
        '[gd_resource type="Resource" format=3]',
        "",
        '[ext_resource type="Script" path="res://csharp/world/WorldNodeData.cs" id="1_node"]',
        '[ext_resource type="Script" path="res://csharp/world/WorldNodeMapData.cs" id="2_map"]',
        '[ext_resource type="Script" path="res://csharp/world/WorldTerritoryData.cs" id="3_territory"]',
        '[ext_resource type="Script" path="res://csharp/world/WorldRouteData.cs" id="4_route"]',
        '[ext_resource type="Texture2D" path="res://textures/world_map/zhaoming_vast_nine_states_runtime_8000_native.png" id="5_background"]',
    ]
    for row in range(5):
        for col in range(5):
            index = row * 5 + col
            lines.append(f'[ext_resource type="Texture2D" path="{MASK_DIR}/zhaoming_territory_id_r{row}{col}.png" id="mask_{row}{col}"]')
    lines.append("")

    for item in NODES:
        node_id = str(item["id"])
        lines.extend(
            [
                f'[sub_resource type="Resource" id="node_{node_id}"]',
                'script = ExtResource("1_node")',
                f'node_id = {gd_string(node_id)}',
                f'node_name = {gd_string(item["name"])}',
                f'node_type = {NODE_TYPES[str(item["type"])]}',
                f'display_level = {DISPLAY_LEVELS[str(item["level"])]}',
                f'territory_id = {gd_string(item["territory"])}',
                f'requires_unlock = {str(bool(item["locked"])).lower()}',
                f'unlocked = {str(not bool(item["locked"])).lower()}',
                f'unlock_hint = {gd_string(item["unlock_hint"])}',
                f'map_position = {gd_vector(runtime_point(item["point"]))}',
                'min_zoom = 0.35',
                'max_zoom = -1.0',
                'marker_size = 150.0',
                f'terrain = {gd_string(item["terrain"])}',
                f'spirit_pressure = {item["spirit"]}',
                f'danger_level = {item["danger"]}',
                f'population = {item["population"]}',
                f'faction_id = {gd_string(item["faction"])}',
                f'description = {gd_string(item["description"])}',
                f'connected_nodes = {gd_string_array(adjacency[node_id])}',
                "",
            ]
        )

    state_polygons = {str(state["id"]): state["polygon"] for state in STATES}
    # Draw the maritime envelope first so overlapping coastline pixels remain
    # owned by the adjacent land state during hit-testing.
    state_order = ["cangming"] + [str(state["id"]) for state in STATES]
    for state_id in state_order:
        meta = STATE_META[state_id]
        if state_id == "cangming":
            polygon = transform_polygon(CANGMING_POLYGON)
        else:
            polygon = transform_polygon(state_polygons[state_id])
        color = MASK_COLORS[state_id]
        state_fill = next(state["fill"] for state in STATES if state["id"] == state_id) if state_id != "cangming" else (80, 150, 160, 220)
        lines.extend(
            [
                f'[sub_resource type="Resource" id="territory_{state_id}"]',
                'script = ExtResource("3_territory")',
                f'territory_id = {gd_string(state_id)}',
                f'territory_name = {gd_string(meta["name"])}',
                f'region_subtitle = {gd_string(meta["subtitle"])}',
                f'description = {gd_string(meta["description"])}',
                f'main_faction = {gd_string(meta["main_faction"])}',
                f'faction_summary = {gd_string(meta["faction_summary"])}',
                'territory_level = 0',
                'parent_territory_id = ""',
                'controlling_faction_id = ""',
                'controlling_faction_name = ""',
                'governance_type = "地理州域"',
                'is_direct_domain = false',
                'requires_unlock = false',
                'unlocked = true',
                f'mask_color = {gd_color(color, 1.0)}',
                f'polygon = {gd_polygon(polygon)}',
                f'fill_color = {gd_color(state_fill[:3], 0.06)}',
                f'outline_color = {gd_color(color, 0.46)}',
                'min_zoom = 0.35',
                'max_zoom = -1.0',
                "",
            ]
        )

    for item in DOMAINS:
        domain_id = str(item["id"])
        state_id = str(item["state"])
        lines.extend(
            [
                f'[sub_resource type="Resource" id="territory_{domain_id}"]',
                'script = ExtResource("3_territory")',
                f'territory_id = {gd_string(domain_id)}',
                f'territory_name = {gd_string(item["name"])}',
                f'region_subtitle = {gd_string(item["region_subtitle"])}',
                f'description = {gd_string(item["description"])}',
                f'main_faction = {gd_string(item["faction_name"])}',
                f'faction_summary = {gd_string(item["faction_summary"])}',
                f'territory_level = {item["level"]}',
                f'parent_territory_id = {gd_string(item["parent"])}',
                f'controlling_faction_id = {gd_string(item["faction_id"])}',
                f'controlling_faction_name = {gd_string(item["faction_name"])}',
                f'governance_type = {gd_string(item["governance"])}',
                f'is_direct_domain = {str(bool(item["direct"])).lower()}',
                'requires_unlock = false',
                'unlocked = true',
                f'mask_color = {gd_color(MASK_COLORS[state_id], 1.0)}',
                f'polygon = {gd_polygon(transform_polygon(item["polygon"]))}',
                f'fill_color = {gd_color(item["fill_rgb"], item["fill_alpha"])}',
                f'outline_color = {gd_color(item["outline_rgb"], item["outline_alpha"])}',
                f'min_zoom = {item["min_zoom"]}',
                'max_zoom = -1.0',
                '',
            ]
        )

    for item in ROUTES:
        route_id = str(item["id"])
        lines.extend(
            [
                f'[sub_resource type="Resource" id="route_{route_id}"]',
                'script = ExtResource("4_route")',
                f'route_id = {gd_string(route_id)}',
                f'route_name = {gd_string(item["name"])}',
                f'description = {gd_string(item["description"])}',
                f'from_node_id = {gd_string(item["from"])}',
                f'to_node_id = {gd_string(item["to"])}',
                f'travel_days = {item["days"]}',
                f'unlocked = {str(bool(item["unlocked"])).lower()}',
                "",
            ]
        )

    lines.extend(
        [
            "[resource]",
            'script = ExtResource("2_map")',
            'map_name = "昭明界·九州广袤"',
            'map_size = Vector2(8000, 8000)',
            'background_texture = ExtResource("5_background")',
            'background_grid = Vector2i(1, 1)',
            'territory_id_mask_grid = Vector2i(5, 5)',
            'use_catalog = false',
            "territory_id_mask_tiles = Array[Texture2D]([",
        ]
    )
    mask_refs = [f'ExtResource("mask_{row}{col}")' for row in range(5) for col in range(5)]
    lines.append(", ".join(mask_refs) + "])" )
    state_refs = [f'SubResource("territory_{state_id}")' for state_id in state_order]
    domain_refs = [f'SubResource("territory_{item["id"]}")' for item in DOMAINS]
    territory_refs = ", ".join(state_refs + domain_refs)
    lines.append(f'territories = Array[ExtResource("3_territory")]([{territory_refs}])')
    node_refs = ", ".join(f'SubResource("node_{item["id"]}")' for item in NODES)
    lines.append(f'nodes = Array[ExtResource("1_node")]([{node_refs}])')
    route_refs = ", ".join(f'SubResource("route_{item["id"]}")' for item in ROUTES)
    lines.append(f'routes = Array[ExtResource("4_route")]([{route_refs}])')
    lines.append('ephemeral_node_templates = Array[ExtResource("1_node")]([])')
    lines.append("")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    known_nodes = {str(item["id"]) for item in NODES}
    referenced_nodes = {str(item["from"]) for item in ROUTES} | {str(item["to"]) for item in ROUTES}
    missing = sorted(referenced_nodes - known_nodes)
    if missing:
        raise SystemExit(f"routes reference unknown nodes: {missing}")
    state_ids = {"cangming"} | {str(state["id"]) for state in STATES}
    domain_ids = [str(item["id"]) for item in DOMAINS]
    if len(domain_ids) != len(set(domain_ids)):
        raise SystemExit("domains contain duplicate ids")
    known_territories = state_ids | set(domain_ids)
    missing_parents = sorted(str(item["parent"]) for item in DOMAINS if str(item["parent"]) not in known_territories)
    if missing_parents:
        raise SystemExit(f"domains reference unknown parents: {missing_parents}")
    major_domains = [item for item in DOMAINS if int(item["level"]) == 1]
    if {str(item["state"]) for item in major_domains} != state_ids or len(major_domains) != len(state_ids):
        raise SystemExit("each state must have exactly one major-faction domain")
    for major in major_domains:
        major_id = str(major["id"])
        children = [item for item in DOMAINS if str(item["parent"]) == major_id and int(item["level"]) == 2]
        if len(children) < 2 or not any(bool(item["direct"]) for item in children):
            raise SystemExit(f"major domain {major_id} needs a direct core and at least two subordinate domains")
    write_resource()
    print(OUT)
    print(f"nodes={len(NODES)} routes={len(ROUTES)} territories={len(state_ids) + len(DOMAINS)}")


if __name__ == "__main__":
    main()
