"""同步用户确认的首卷主修效果、卷章成长与公共回落文案；保留图谱布局。"""
from pathlib import Path
import html
import json
import re

ROOT = Path(__file__).resolve().parents[2]
HEARTS = {
    '黄庭养形篇': [
        '效果：每次自身主动使气血层数降低后，获得身法1层。',
        '主修额外追加：每回合首次主动使气血层数降低后，回复精力1点。',
        '第二卷追加：每回合前两次主动气血回落时，各额外获得身法1层。',
        '第四卷追加：每回合首次主动气血回落时，再额外获得身法1层。'],
    '乘物游骸真经': [
        '效果：每回合首次由自身主动使气血层数降低后，获得游骸1层；同次效果至多触发一次。',
        '主修额外追加：每次实际消耗游骸，获得身法3层；抵挡、托身和化法消耗均计入。',
        '第二卷追加：心法给予游骸时，额外获得身法1层。',
        '第四卷追加：心法给予游骸时，再额外获得身法1层。'],
    '内景损益篇': [
        '效果：每层当前内伤或外伤，使自身主动物理伤害倍率提高3%。',
        '主修额外追加：每层伤势提供的上述伤害倍率再提高1个百分点。',
        '第二卷追加：每回合首次受到实际生命伤害后，获得外伤1层。',
        '第三卷追加：每层伤势提供的上述伤害倍率再提高1个百分点；辅修每层合计4%，主修每层合计5%。'],
    '赤阳参同经': [
        '效果：自身主动使气血层数降低时，本次不回复生命，改为获得血炉值 = 气血实际降低层数×当前气血回落回复率R；其他回落效果照常触发。赤阳化法以血炉强化本招伤害时，每点实际消耗使本招伤害倍率提高1%。',
        '主修额外追加：上述每点血炉提供的本招伤害倍率再提高0.5个百分点；不改血炉的公共效果。',
        '第二卷追加：每次赤阳化法实际消耗至少1点血炉后，获得身法1层。',
        '第四卷追加：每次赤阳化法实际消耗至少1点血炉后，再获得身法1层。'],
    '阴符应物经': [
        '效果：累计以化解实际抵消的敌方物理伤害，每满4点获得借力1层；不足部分留至本场后续应对。',
        '主修额外追加：上述门槛降低1点，即每满3点获得借力1层。',
        '第三卷追加：上述门槛再降低1点；辅修每满3点、主修每满2点获得借力1层。'],
}
GROWTH = {
    ('黄庭养形篇', 4): '每回合首次主动气血回落时，额外获得身法1层。',
    ('乘物游骸真经', 4): '心法给予游骸时，再额外获得身法1层。',
    ('赤阳参同经', 4): '每次赤阳化法实际消耗至少1点血炉后，再获得身法1层。',
    ('内景损益篇', 3): '每层伤势提供的心法物理伤害倍率再提高1个百分点；辅修每层合计4%，主修每层合计5%。',
    ('阴符应物经', 3): '心法累计抵消伤害的借力转化门槛再降低1点；辅修每满3点、主修每满2点获得借力1层。',
}

def public_fall_text(s):
    # 只移除“调用公共回落”后的写死基准量；独立治疗、提高回落率等明确改写保留。
    s = re.sub(r'触发公共气血回落[，；]?[ ]*(?:回复|恢复)最大生命(?:×|的)?3%', '触发气血层数降低时的回复效果', s)
    s = s.replace('触发公共气血回落', '触发气血层数降低时的回复效果')
    s = s.replace('气血回落按公共规则回复最大生命×3%', '触发气血层数降低时的回复效果')
    return s

def update_diagram(p):
    book=p.stem
    def cell(m):
        raw=html.unescape(m.group(2))
        cid=re.search(r'\bid="([^"]+)"',m.group(1)).group(1)
        raw=public_fall_text(raw)
        if book in HEARTS and cid=='c1_cards':
            raw=re.sub(r'(类型：心法<br>关键词：[^<]*<br>).*?(?=<br><br>|$)',
                       lambda x:x.group(1)+'<br>'.join(HEARTS[book]), raw, count=1)
        if (book, int(cid[1])) in GROWTH if re.fullmatch(r'c[234]_cards',cid) else False:
            n=int(cid[1]); label={2:'第二卷',3:'第三卷',4:'第四卷'}[n]
            raw=re.sub(r'心法追加（[^）]+）：[^<]*(?:<br>|$)', '', raw)
            raw='心法追加（'+label+'）：'+GROWTH[(book,n)]+'<br>'+raw.lstrip('<br>')
        # 清除仍留在化法中的“主修需后续卷”表述；倍率由首卷心法统一赋予。
        raw=raw.replace('主修在第四卷心法进阶后提高为', '主修心法使该倍率提高为')
        raw=raw.replace('主修第四卷心法追加后', '主修心法生效时')
        if book in PROPOSAL_EDITS:
            for a,b in PROPOSAL_EDITS[book].get(cid,[]):
                if a not in raw: raise ValueError((book,cid,a))
                raw=raw.replace(a,b)
        if cid=='c1_cards' and book in PROPOSAL_MAIN:
            raw=re.sub(r'(类型：心法<br>关键词：[^<]*<br>.*?)(?=<br><br>|$)',
                       lambda x:x.group(1)+'<br>主修额外追加：'+PROPOSAL_MAIN[book],raw,count=1)
        return m.group(1)+html.escape(raw,quote=True)+m.group(3)
    s=p.read_text('utf-8-sig')
    s=re.sub(r'(<mxCell\b[^>]*?\bvalue=")([^"]*)(")',cell,s)
    p.write_text(s,encoding='utf-8')

PROPOSAL_MAIN={
 '坎离错综周流参同秘录':'任意术法槽中的技能实际支付法力后，改为获得炁1；若与本回合上一项实际付法技能的术法槽奇偶相反，再获得炁1。替代基础偶数槽产炁，不与之重复。',
 '沧浪心印录':'来源技能已达念3时，成功结算后给后方第一个来自其他术法槽的未结算主动技能赋念1；每个来源技能每回合一次。无合法接收者不消耗本次机会，赋念不施展技能或继续传播。',
 '玄匣藏真候天诀':'封藏技能因资源不足或无合法目标而施展失败时，不消耗本回合成功施展机会；本回合后续周天可重试。每回合最多成功施展一次，成功后产生的周天不能再次启匣。',
 '玄籁返真秘录':'每回合首次完成本心法认可的回文后，中心技能额外获得念1；只赋念，不复现行动。',
 '璇玑移星易序篇':'本回合首次成功改序后，被移到较前位置的技能下一次结算时少耗法力1点，最低为0；结算或跳过后移除标记，回合结束清除。',
 '神室易质秘录':'灵台照万象首次跨入3念和6念时，各回复精力1点，并立即为当前生命比例最低的己方施加该技能基础护盾的一半；并列时随机。每道门槛本场各一次，不施展技能、不触发赋念。',
 '谷神应籁篇':'两侧技能念均不少于3层时，空谷位技能结算中需读取自身念的效果，改读两侧念的较小值；该技能实际附着念仍为0。仅替换本次读取值，不赋念、不改变空谷位判定或触发额外行动。',
}
PROPOSAL_EDITS={
 '抱朴归元篇':{'c1_cards': [('主修额外追加：首回合法力补给完成后，额外获得炁1；每场战斗一次。','主修额外追加：每回合法力补给完成后，获得炁1；该次炁可正常触发周天，每回合一次。')]},
 '坎离错综周流参同秘录':{'c2_cards':[('任一术法槽实际支付法力后获得炁1；与本轮上一项付法术法槽奇偶相反时，再获得炁1。','')]},
 '玄匣藏真候天诀':{
   'c1_cards':[('每场战斗首次周天后','每回合首次周天后')],
   'c2_cards':[('自动施展时机升级为每轮首次周天后各尝试一次，成功施展仍受该技能原有使用次数限制。若本轮已经成功施展，不再触发。','封藏技能自动施展时，精力费用减少1点，最低为0；仍受自身回合冷却与使用次数限制。')],
   'c3_cards':[('若自动施展因精力不足、法力不足或没有合法目标而失败，不扣资源、不扣技能使用次数，也不消耗本轮成功施展次数；本轮后续周天可再次检查。每轮最多成功施展一次。技能使用次数已经耗尽时，本轮不再检查。','封藏技能成功自动施展且实际支付法力后，获得炁1；每回合最多一次。成功施展标记先于产炁写入，不能借此周天再次启匣。')],
   'c4_cards':[('封藏技能成功自动施展且实际支付至少1点法力后，结算完技能效果获得炁1，每轮最多一次。该炁可正常触发周天；本书每轮成功施展上限已用尽，因此不会因新周天递归施展自身。','第三卷追加给予的炁由1层提高为2层；每回合仍最多一次。')],
   'status_rules':[('费用不足或无合法目标时，第三卷起等到下一次周天再试。','主修时费用不足或无合法目标可在本回合下一次周天重试；辅修保留首次尝试一次的限制。')]},
 '璇玑移星易序篇':{
   'c1_cards':[('每场战斗首次完成周天后','每回合首次完成周天后'),('本场首次改序机会','本回合首次改序机会')],
   'c2_cards':[('每轮首次完成周天后改序一次。每次交换本角色队列中接下来两个尚未结算的行动槽；不足两项时不改序，机会保留至本轮后续周天。替代第一卷每场战斗一次的规则，不额外交换。','每回合首次成功改序后，回复法力1点，不超过当前上限。')],
   'c4_cards':[('本轮首次改序后若再次完成周天，将接下来最多三个尚未结算的行动槽倒序排列；不足三项时将现有行动槽中的技能倒序。每轮最多倒序一次。行动及已选目标一并移动；系统行动、空位、已结算行动与术法槽不变。本书的改序和倒序本身不会再触发本书。','第三卷追加给予的炁由1层提高为2层；每回合仍最多一次。')],
   'status_rules':[('再次周天时，倒序后续最多三个行动槽。','第四卷起，该次追加产炁提高为2层。')]},
 '玄籁返真秘录':{'c4_cards':[('本额外赋念每场战斗最多一次','本额外赋念每回合最多一次')]},
 '神室易质秘录':{'c1_cards':[('战斗开始时，给已装配的灵台照万象赋念1层','每回合开始时，给已装配的灵台照万象赋念1层')],
   'c4_cards':[('灵台照万象达到3—5念时，改为对随机2名不同己方各施加基础护盾×0.65；达到6念时，改为对全体己方各施加基础护盾×0.40。该目标形态替代低念单体形态，目标不足时对现有己方结算。','灵台照万象各念形态的基础护盾倍率提高10%；目标形态、目标数量与念层门槛沿用技能原规则。')]
 }
}

if __name__=='__main__':
    for p in (ROOT/'docs/秘籍词典/drawio').rglob('*.drawio'):
        if not p.stem.startswith('_'):update_diagram(p)
    for book,lines in HEARTS.items():
        for p in (ROOT/'resources/activeskills'/book).glob('*.tres'):
            s=p.read_text('utf-8-sig')
            if 'skill_type = 6' not in s:continue
            # 卷章强化单独从已学卷章拼入Buff，避免未学效果提前显示及重复展示。
            desc=' '.join(x for x in lines if x.startswith(('效果：','主修额外追加：'))).replace('效果：','',1)
            s=re.sub(r'^description = ".*"$',lambda m:'description = '+json.dumps(desc,ensure_ascii=False),s,flags=re.M)
            p.write_text(s,encoding='utf-8')
    for book in HEARTS:
        p=ROOT/'resources/gongfa'/f'{book}.tres';s=p.read_text('utf-8-sig')
        def chapter(m):
            chunk=m.group(0)
            n=re.search(r'^order = (\d+)',chunk,re.M)
            if n and (book,int(n[1])) in GROWTH:
                chunk=re.sub(r'^heart_method_upgrade_description = ".*"$',lambda _: 'heart_method_upgrade_description = '+json.dumps(GROWTH[(book,int(n[1]))],ensure_ascii=False),chunk,flags=re.M)
            return chunk
        s=re.sub(r'\[sub_resource[^\n]*\][\s\S]*?(?=\n\[|\Z)',chapter,s)
        p.write_text(s,encoding='utf-8')
    # 现行技能、化法及参考数据中的公共回落文案。
    for p in (ROOT/'resources/activeskills').rglob('*.tres'):
        s=p.read_text('utf-8-sig');v=public_fall_text(s)
        v=v.replace('主修在第四卷心法进阶后提高为','主修心法使该倍率提高为')
        if v!=s:p.write_text(v,encoding='utf-8')
    p=ROOT/'resources/card_reference.json'
    data=json.loads(p.read_text('utf-8-sig'))
    def walk(x):
        if isinstance(x,str):return public_fall_text(x).replace('主修在第四卷心法进阶后提高为','主修心法使该倍率提高为')
        if isinstance(x,list):return [walk(v) for v in x]
        if isinstance(x,dict):return {k:walk(v) for k,v in x.items()}
        return x
    p.write_text(json.dumps(walk(data),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
