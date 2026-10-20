using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class AlchemyPreviewModel
{
    private void ReclaimPills()
    {
        foreach(var pill in Pills)foreach(var part in pill.Parts)
        {
            var d=part.Source;d.Allocated-=part.Amount+part.Slag;
            part.Location=0;part.ReadyBreath=Breath+1;
            if(part.Structure){d.Bound+=part.Amount;_structures.Add(part);}
            else {d.Available+=part.Amount;d.Slag+=part.Slag;Inventory.Add(part);}
        }
        Pills.Clear();Note("改变颗数：旧药团实际退回汇合位，下一息重新分配；已付工艺成本不返还。");
    }
    private void Gather(double control)
    {
        if(Pills.Count>0&&Pills.Count!=Wanted){ReclaimPills();return;}
        var candidates=At(0).Where(p=>p.ReadyBreath<=_executingBreath&&Free(p)>1e-9).ToArray();
        if(Pills.Count==0)
        {
            if(!candidates.Any(p=>p.Source.Kind=="主药")){Note("汇合位没有可分配的主药；请先实际输送，下一息凝丹。");return;}
            var def=CurrentPill;
            for(int i=0;i<Wanted;i++)Pills.Add(new(){Index=i+1,PillTypeId=def.Id,FormulaId=CurrentFormula.Id,
                MainHerbName=MainName,CoreUtilityId=def.CoreUtility,CoreReferencePower=AlchemyProcessConfig.Strength[Math.Clamp(10-def.Rank,1,9)],
                PolarityTarget=def.PolarityTarget,PolarityToleranceAt20=def.PolarityToleranceAt20,PolarityToleranceAt100=def.PolarityToleranceAt100,
                EffectName=def.EffectName,EffectUnit=def.EffectUnit,Form=HubStability});
        }
        double baseBudget=_automaticRunActive?double.PositiveInfinity:8*control;
        var residueParts=Pills.Select(p=>p.Parts.Where(x=>x.Amount==0).GroupBy(x=>x.Source).ToDictionary(g=>g.Key,g=>g.First())).ToArray();
        foreach(var part in candidates.OrderByDescending(p=>p.Source.Kind=="主药"))
        {
            double allowed=Math.Min(Free(part),baseBudget+part.Amount*part.Clustered);
            if(allowed<=1e-9)continue;
            double[] gaps=Pills.Select(p=>part.Source.Kind=="主药"?Math.Max(0,22-p.MainAmount):allowed/Pills.Count).ToArray();
            double total=gaps.Sum(),take=Math.Min(allowed,total);
            if(take<=1e-9)continue;
            double unclustered=Math.Max(0,take-part.Amount*part.Clustered);baseBudget=Math.Max(0,baseBudget-unclustered);
            double nodeAmount=NodeAvailable(0),waste=NodeSlag(0);
            double carry=Math.Min(waste,take*waste/Math.Max(nodeAmount,1e-9)*(1-_hubSeparation));
            foreach(var residue in At(0).Where(p=>p.Slag>0).ToArray())
            {
                double q=carry*residue.Slag/Math.Max(waste,1e-9);residue.Slag-=q;residue.Source.Slag-=q;residue.Source.Allocated+=q;
                if(q<=1e-10)continue;
                for(int i=0;i<Pills.Count;i++)if(gaps[i]>0)
                {
                    if(!residueParts[i].TryGetValue(residue.Source,out var piece)){piece=new(residue.Source,0){Location=0};residueParts[i][residue.Source]=piece;Pills[i].Parts.Add(piece);}
                    piece.Slag+=q*gaps[i]/total;
                }
            }
            for(int i=0;i<Pills.Count;i++)
            {
                double q=take*gaps[i]/Math.Max(total,1e-9);if(q<=1e-9)continue;
                var split=part.Split(q,0,0);split.Sealed=0;split.Stuck=0;Pills[i].Parts.Add(split);
                part.Source.Available-=q;part.Source.Allocated+=q;
            }
            RemoveFree(part,take);
        }
        // 仅收拢／稳形的中心结构能进入丹体；运输线与封存结构保留其实际用途。
        foreach(var link in SignedLinks.Where(l=>l.TargetDoseId==0&&l.UtilityId is "聚" or "固"))
        foreach(var part in link.Structures.ToArray())
        {
            if(!_structures.Remove(part))continue;
            part.Source.Bound-=part.Amount;part.Source.Allocated+=part.Amount;
            for(int i=0;i<Pills.Count;i++){var split=part.Split(part.Amount/Pills.Count,0,0);Pills[i].Parts.Add(split);}
            link.Structures.Remove(part);
        }
        foreach(var pill in Pills)if(pill.MainAmount>0)
            pill.Form=Math.Min(1,_automaticRunActive?Math.Max(pill.Form,.65+.30*HubStability):pill.Form+.15*control);
        foreach(var pill in Pills)
        foreach(var group in pill.Parts.Where(p=>p.Amount<=1e-10).GroupBy(p=>p.Source).ToArray())
        {
            var pieces=group.ToArray();if(pieces.Length<2)continue;
            double waste=pieces.Sum(p=>p.Slag);foreach(var piece in pieces)pill.Parts.Remove(piece);
            pill.Parts.Add(new(group.Key,0){Slag=waste,Location=0});
        }
        Note($"按汇合位真实药性分配{Pills.Count}颗，稳定{Shape:P0}；未从其他节点自动取药。");
    }
    public double Allocation(int i)=>i<Pills.Count?Pills[i].MainAmount:0;
    private (string[] Main,string[] Blend,string[] Body) Gaps(Pill p,int q)
    {
        double t=(q-20)/80d;var main=new System.Collections.Generic.List<string>();var blend=new System.Collections.Generic.List<string>();var body=new System.Collections.Generic.List<string>();
        void Min(System.Collections.Generic.List<string> list,string name,double value,double need){if(value+1e-7<need)list.Add($"{name}{value:F3}，需≥{need:F3}");}
        void Max(System.Collections.Generic.List<string> list,string name,double value,double limit){if(value-1e-7>limit)list.Add($"{name}{value:F3}，需≤{limit:F3}");}
        double coreScale=AlchemyProcessConfig.Strength[Math.Clamp(10-AlchemyRecipeCatalog.GetPill(p.PillTypeId).Rank,1,9)];
        Min(main,"有效主药量",Math.Min(22,p.MainAmount),12+10*Math.Pow(t,1.15));
        Min(main,$"保留{p.CoreUtilityId}能力",p.CoreCapability,(8+10*Math.Pow(t,1.2))*coreScale);
        var pillRule=AlchemyRecipeCatalog.GetPill(p.PillTypeId);
        if(!pillRule.MainCandidates.Contains(p.MainHerbName))main.Add($"主材{p.MainHerbName}不符合本丹种要求");
        if(p.Parts.Where(x=>x.Source.Kind=="主药").Any(x=>x.Source.Herb.Name!=p.MainHerbName))main.Add($"本炉主材应为{p.MainHerbName}");
        Min(blend,"实际混匀",p.Mix,.55+.40*t);
        foreach(var need in AlchemyRecipeCatalog.SupportingRequirements(p.PillTypeId))
            if(need.Need(q)>0)Min(blend,"配伍"+need.Utility+"（"+need.Purpose+"）",p.SupportingCapability(need.Utility),need.Need(q)*coreScale);
        Max(blend,"阴阳偏向距目标",Math.Abs(p.Polarity-p.PolarityTarget),p.PolarityToleranceAt20+(p.PolarityToleranceAt100-p.PolarityToleranceAt20)*t);
        Min(body,"结构保持",p.Form,.62+.36*t);Max(body,"实际夹渣比例",p.SlagAmount/Math.Max(.001,p.Amount+p.SlagAmount),.06-.057*t);Max(body,"可恢复损伤",p.Damage,.25-.23*t);
        return(main.ToArray(),blend.ToArray(),body.ToArray());
    }
    private int AxisQuality(Pill p,Func<(string[] Main,string[] Blend,string[] Body),string[]> select)
    {
        int score=0;for(int q=20;q<=100;q++){if(select(Gaps(p,q)).Length>0)break;score=q;}return score;
    }
    public int ProjectedQuality(Pill p)=>Math.Min(AxisQuality(p,g=>g.Main),Math.Min(AxisQuality(p,g=>g.Blend),AxisQuality(p,g=>g.Body)));
    public string QualityGaps(Pill p,int target=90){var g=Gaps(p,target);return string.Join("；",g.Main.Concat(g.Blend).Concat(g.Body));}
    public void Finish()
    {
        if(Done)return;Done=true;
        foreach(var p in Pills)
        {
            p.MainQuality=AxisQuality(p,g=>g.Main);p.BlendQuality=AxisQuality(p,g=>g.Blend);p.BodyQuality=AxisQuality(p,g=>g.Body);p.Score=Math.Min(p.MainQuality,Math.Min(p.BlendQuality,p.BodyQuality));
            if(p.Score<20)
            {
                var atLeastOne=Gaps(p,20);var causes=atLeastOne.Main.Concat(atLeastOne.Blend).Concat(atLeastOne.Body);
                p.Score=0;p.Reason=$"未成丹：主药发挥{p.MainQuality}、配伍协调{p.BlendQuality}、丹体稳定{p.BodyQuality}；最低项未达到20。\n{string.Join("；",causes)}";continue;
            }
            var next=new System.Collections.Generic.List<string>();if(p.MainQuality==p.Score)next.AddRange(Gaps(p,p.Score+1).Main);if(p.BlendQuality==p.Score)next.AddRange(Gaps(p,p.Score+1).Blend);if(p.BodyQuality==p.Score)next.AddRange(Gaps(p,p.Score+1).Body);
            p.NextGap=p.Score==100?"已达到本丹种九品上限":"下一分先补："+string.Join("；",next);
            p.Reason=$"{CurrentPill.Name} · 主材{p.MainHerbName} · 参考丹方：{CurrentFormula.Name}\n主药发挥 {p.MainQuality} · 配伍协调 {p.BlendQuality} · 丹体稳定 {p.BodyQuality}\n丹效取三项最低：{p.Score}。{p.NextGap}\n阴阳{p.Polarity:+0.000;-0.000;0}（目标{p.PolarityTarget:+0.00;-0.00;0.00}，容许偏差{p.PolarityTolerance(p.Score):F2}）· 试验效果：{p.EffectText}；定义键{p.DefinitionKey}";
        }
        int success=Pills.Count(p=>p.Score>=20);Result=Pills.Count==0?"未形成药团，出炉无成品。":$"成丹{success}颗，失败{Pills.Count-success}颗；丹效由各颗实际成品判定，未追加随机分数。";Note(Result);
        Note($"留炉药渣{Slag:F2}不自动扣成品丹效；未用药性{InFurnace.Sum(d=>d.Available+d.Bound):F2}列为本炉余留，不返还原材。");
    }
    public string FunctionStatus(Herb herb,string id)
    {
        if(Contributions.TryGetValue($"{herb.Name}·{id}",out var value))return value;
        var doses=InFurnace.Where(d=>d.Herb==herb).ToArray();if(doses.Length==0)return "尚未投药";
        if(doses.All(d=>d.Available<.001))return "没有可投入的自由药性；新取得部分须到下一息才能主动作用";
        if(doses.All(d=>d.Readiness(id)<.05))return "能力尚未可用；查看受热、精度上限与保存状态";
        return id switch{"生"=>"没有需要修复的可恢复损伤，不消耗","护"=>"没有热伤或冲撞对象，不消耗","镇"=>"没有实际躁动，不消耗","破" or "化"=>"没有待处理对象，或本息作用预算已用尽","通"=>"关系已建立，或没有第二批可交换对象；不重复收费","行"=>"没有可推进的交换，或已混匀","聚" or "引" or "固"=>"本息没有汇集/分配/结构缺口","明"=>"本息未执行分离，或已达到精度上限",_=>"没有可阻止的实际散失，或本息作用预算已用尽"};
    }
    public double Effect(string id)=>InFurnace.Sum(d=>d.Ability(id));
    public static string Tier(int q)=>q<20?"未成丹":q<=50?"薄效":q<=75?"良效":q<=90?"优效":"极效";
    public string Checks()
    {
        var formula=CurrentFormula;var definition=CurrentPill;
        string materials=string.Join("\n",formula.Ingredients.Select(i=>$"{(i.HerbName==formula.MainHerb?"本方主材":"建议辅材")} · {i.HerbName}：建议{i.Recommended}份"));
        string curve=string.Join("，",definition.EffectCurve.Select(p=>$"{p.Score}分→{p.Value:0.#}{definition.EffectUnit}"));
        var mainHerb=Herbs.First(h=>h.Name==SelectedMainHerbName);
        double coreScale=mainHerb.Power(definition.CoreUtility)/AlchemyProcessConfig.CoreReferencePower;
        double currentPolarity=Available<.001?0:Polarity;
        double tolerance=definition.PolarityToleranceAt20+(definition.PolarityToleranceAt100-definition.PolarityToleranceAt20)*(60/80d);
        string recipeDelta=SelectedMainHerbName==formula.MainHerb?"主材符合所选参考丹方":"主材已偏离所选参考丹方，当前属于改方试炼";
        return $"{definition.Name} · 成丹要求\n丹种作用：{definition.Purpose}\n主材必须从以下候选中选一味：{string.Join("、",definition.MainCandidates)}。当前选择：{SelectedMainHerbName}；该主材须有材料原表中的{definition.CoreUtility}功用，实际品级影响可炼出的能力与精度要求。主药每100份理论最多4颗。\n辅材：可从材料架自由选择、替换和增减；不要求凑齐任何一张丹方的材料清单。炼出的实际药性仍按材料原表、质地、阴阳和炉内操作计算。\n阴阳要求：目标偏向{definition.PolarityTarget:+0.00;-0.00;0.00}；丹效20时容许偏差±{definition.PolarityToleranceAt20:F2}，丹效100时收紧至±{definition.PolarityToleranceAt100:F2}。当前炉内偏向{currentPolarity:+0.00;-0.00;0.00}。\n其余成品要求：丹效至少20；主药量和核心功用、实际混匀、成形、夹渣及损伤按丹效逐级收紧。丹效三项分别判分后取最低值，缺陷不能互相补。当前检查见下方具体判定。\n\n参考丹方：{formula.Name}（{recipeDelta}）\n路线特点：{formula.Comparison}\n建议搭配（可删、可换、可增减；以下不是必需材料或限制范围）：\n{materials}\n\n建议投料次序（需按炉温、可用药性和阴阳实时调整，不是固定阶段）：\n{string.Join("\n",formula.FeedOrder.Select((name,index)=>$"{index+1}　{name}：{formula.Ingredients.First(i=>i.HerbName==name).Timing}"))}\n\n参考操作手法：\n{string.Join("\n",formula.Operations.Select(note=>"• "+note))}\n\n丹效只作评级：20–50薄效 · 51–75良效 · 76–90优效 · 91–100极效；实际药效另按曲线：{curve}。参考方不改变丹药成丹条件。\n\n本炉计划{Wanted}颗，实际药团{Pills.Count}颗，主药可用药性{MainAmount:F2}；当前控制精度{Preview().Precision:F1}；所选主材核心功用标准倍率{coreScale:P0}。\n"+string.Join("\n",Pills.Select(p=>$"{p.Index}号：{p.Score}分 {Tier(p.Score)} · {p.EffectText}\n{p.Reason}"));
    }
}
