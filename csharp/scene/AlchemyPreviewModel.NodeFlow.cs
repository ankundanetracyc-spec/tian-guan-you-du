using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class AlchemyPreviewModel
{
    private readonly Dictionary<int,double> _growth=[];
    private double _hubAgitation,_hubSeparation;
    private readonly HashSet<int> _directed=[];
    private IEnumerable<Part> At(int id)=>Inventory.Where(p=>p.Location==id);
    private double Free(Part p)=>Math.Max(0,p.Amount*(1-p.Sealed)-p.Stuck);
    private double Eligible(Part p)=>p.ReadyBreath<=_executingBreath?Free(p):0;
    private double Separation(int id)=>id==0?_hubSeparation:InFurnace.First(d=>d.Id==id).Separation;
    private void SetSeparation(int id,double value){if(id==0)_hubSeparation=value;else InFurnace.First(d=>d.Id==id).Separation=value;}
    public HashSet<int> LiveNodeIds(bool includePending=true)
    {
        EnsureInventory();
        var live=InFurnace.Where(d=>d.Raw>1e-6||NodeAvailable(d.Id)>1e-6).Select(d=>d.Id).ToHashSet();
        var feeds=SignedLinks.Where(l=>l.UtilityId=="通"&&!l.Paused&&l.FlowOpen&&l.Transported+1e-6<l.TargetAmount&&(l.Built||PlannedLinks.Contains(l)))
            .Select(l=>(Source:l.SourceDoseId,Target:l.TargetDoseId))
            .Concat((includePending?PendingOperations.Concat(RepeatingOperations):RepeatingOperations).Where(o=>o.Kind==Hand.Draw).Select(o=>(Source:o.SourceId,Target:o.TargetId))).ToArray();
        bool changed;
        do
        {
            changed=false;
            foreach(var feed in feeds)if(live.Contains(feed.Source)&&feed.Target!=0)changed|=live.Add(feed.Target);
        }while(changed);
        return live;
    }
    private void RetireExhaustedNodes()
    {
        var live=LiveNodeIds(false);
        bool Dead(int id)=>id!=0&&!live.Contains(id);
        var retired=SignedLinks.Where(l=>!l.Paused&&(Dead(l.SourceDoseId)||Dead(l.TargetDoseId)||l.RouteTarget is {} route&&(Dead(route.SourceDoseId)||Dead(route.TargetDoseId)))).ToArray();
        foreach(var line in retired){line.Paused=true;PlannedLinks.Remove(line);line.Status="节点已彻底耗尽，停止维持";}
        RepeatingOperations.RemoveWhere(o=>Dead(o.TargetId)||(o.Kind==Hand.Draw&&Dead(o.SourceId)));
        if(retired.Length>0)Note($"耗尽退场：停止{retired.Length}条控制线并释放灵识；既有工艺绑定和炉内残余仍记账。");
    }
    private double Z(int id)=>id==0?_hubAgitation:InFurnace.First(d=>d.Id==id).Agitation;
    private void SetZ(int id,double value){if(id==0)_hubAgitation=Math.Clamp(value,0,100);else InFurnace.First(d=>d.Id==id).Agitation=Math.Clamp(value,0,100);}
    private void AddResidue(Dose source,double amount,int location)
    {
        if(amount<=1e-12)return;
        Inventory.Add(new(source,0){Slag=amount,Location=location,ReadyBreath=Breath+1});
    }
    private void EnsureInventory()
    {
        foreach(var d in InFurnace)
        {
            if(!_seeded.Add(d.Id))continue;
            if(d.Available>0)Inventory.Add(new(d,d.Available){Stuck=d.Herb.Craft.texture=="黏胶"?d.Available*.2:0});
            AddResidue(d,d.Slag,d.Id);
            if(d.Bound>0){var bound=new Part(d,d.Bound,true);bound.Capability.Clear();_structures.Add(bound);}
            if(!d.Herb.Processing.Dense)d.Open=1;
        }
    }
    private void RemoveFree(Part p,double x)
    {
        double sealedAmount=p.Amount*p.Sealed;p.Amount-=x;
        p.Sealed=p.Amount>1e-12?Math.Clamp(sealedAmount/p.Amount,0,1):0;
        p.Stuck=Math.Min(p.Stuck,p.Amount);
    }
    private void Refine(Dose d,double amount,double precision)
    {
        amount=Math.Clamp(amount,0,d.Raw);if(amount<1e-9)return;
        double usable=amount*d.Herb.Processing.Yield;
        d.Raw-=amount;d.Extracted+=amount;d.Available+=usable;d.Slag+=amount-usable;
        d.MaxPrecision=Math.Max(d.MaxPrecision,precision);
        var part=new Part(d,usable){Location=d.Id,ReadyBreath=Breath+1,Mix=1,Stuck=d.Herb.Craft.texture=="黏胶"?usable*.2:0};
        foreach(var u in d.Herb.Craft.utilities)part.Capability[u.utility_id]=d.Herb.Power(u.utility_id)*Math.Min(1,precision/AlchemyProcessConfig.Precision[u.grade]);
        Inventory.Add(part);AddResidue(d,amount-usable,d.Id);
        Note($"{d.Herb.Name}炼化{amount:F1}质量：药性{usable:F1}，残余{amount-usable:F1}；新药性下一息可主动使用");
    }
    private void ProcessRaw(double ratio)
    {
        foreach(var d in InFurnace.OrderBy(d=>d.Id))
        {
            if(d.Raw<1e-9)continue;
            var p=d.Herb.Processing;bool wasOpen=d.Open>=1-1e-9;
            var care=PendingOperations.Where(o=>o.TargetId==d.Id&&o.Kind is Hand.Care or Hand.Fine or Hand.Loop or Hand.Master).Select(o=>PrecisionFor(o.Kind,ratio)).DefaultIfEmpty(PrecisionFor(Hand.Care,ratio)).Max();
            if(p.Dense&&!wasOpen)
            {
                if(Heat>=p.Open&&Heat<=p.Safe)d.Open=Math.Min(1,d.Open+(Heat>=p.OptimalLow?.2:.1));
                Note($"{d.Herb.Name}结构开启{d.Open:P0}；已开后下一息炼化");continue;
            }
            double fit=Heat<p.Open||Heat>p.Safe||Heat==0?0:Heat<p.OptimalLow?.5:Heat>p.OptimalHigh?1.15:1;
            double automaticSpeed=_automaticRunActive?AlchemyProcessConfig.AutomaticProcessingMultiplier:1;
            Refine(d,p.Rate*(d.Original/100)*fit*automaticSpeed,care);
        }
    }
    private double SourcePower(Part p,string utility,double precision)
    {
        int g=p.Source.Herb.Grade(utility);return g==0?0:p.Capability.GetValueOrDefault(utility)*(1-p.Damage)*Math.Min(1,precision/AlchemyProcessConfig.Precision[g]);
    }
    private double Work(SignedLink link,double demand,double perWork,double ratio,bool bind=false)
    {
        if(demand<=1e-9)return 0;
        double precision=PrecisionFor(link.Technique??Hand.Care,ratio);
        var sources=At(link.SourceDoseId).Where(p=>Eligible(p)>1e-9&&SourcePower(p,link.UtilityId,precision)>1e-9).ToArray();
        double available=sources.Sum(Eligible),scale=Math.Min(1,AlchemyProcessConfig.InputLimit(OperatorAttainment)/Math.Max(available,1e-9));
        double capacity=sources.Sum(p=>Eligible(p)*scale*SourcePower(p,link.UtilityId,precision)*perWork);
        double result=Math.Min(demand,capacity);if(result<=1e-9){link.Status="无可用来源／精度不足／本息新入药性需等待";return 0;}
        foreach(var p in sources)
        {
            double amount=Eligible(p)*scale*result/capacity;
            if(amount<1e-12)continue;
            RemoveFree(p,amount);p.Source.Available-=amount;p.Source.Spent+=amount;
            int location=bind&&link.UtilityId!="通"?link.TargetDoseId:link.SourceDoseId;
            if(bind)
            {
                var structure=p.Split(amount,location,0);structure.Structure=true;structure.Capability.Clear();structure.Stuck=0;structure.Sealed=0;
                if(link.UtilityId=="固")structure.StructuralSupport["固"]=SourcePower(p,"固",precision);
                p.Source.Bound+=amount;_structures.Add(structure);link.Structures.Add(structure);
            }
            else {p.Source.Slag+=amount;AddResidue(p.Source,amount,location);}
            string key=p.Source.Herb.Name+"·"+link.UtilityId;
            Contributions[key]=Contributions.GetValueOrDefault(key,"")+$"{NodeName(link.SourceDoseId)}·{link.UtilityId}→{NodeName(link.TargetDoseId)}；实际投入{amount:F3}，去向{(bind?"结构绑定":"作用后残余")}\n";
        }
        link.Status=$"本息完成{result:F2}；来源按实际投入划账";Note($"{link.UtilityId}：{NodeName(link.SourceDoseId)}→{NodeName(link.TargetDoseId)}，工作{result:F2}");return result;
    }
    private IEnumerable<SignedLink> Scheduled(string utility)=>SignedLinks.Where(l=>l.UtilityId==utility&&!l.Paused&&PlannedLinks.Contains(l)).OrderBy(l=>l.SourceDoseId).ThenBy(l=>l.TargetDoseId).ToArray();
    private double RelationMultiplier(int source,int target)
    {
        var a=InFurnace.FirstOrDefault(d=>d.Id==source)?.Herb.Craft.element??"";
        var b=InFurnace.FirstOrDefault(d=>d.Id==target)?.Herb.Craft.element??"";
        var aa=a.Where(c=>"木火土金水".Contains(c)).Distinct().ToArray();var bb=b.Where(c=>"木火土金水".Contains(c)).Distinct().ToArray();
        if(aa.Length==0||bb.Length==0)return 1;
        string cycle="木火土金水",control="木土水火金";double modifier=1,weight=1d/(aa.Length*bb.Length);
        foreach(char x in aa)foreach(char y in bb)
        {
            if(cycle[(cycle.IndexOf(x)+1)%5]==y)modifier+=.20*weight;
            if(control[(control.IndexOf(y)+1)%5]==x)modifier-=.15*weight;
        }
        return Math.Clamp(modifier,.7,1.25);
    }
    public string ElementRelation(SignedLink l)
    {
        double factor=RelationMultiplier(l.SourceDoseId,l.TargetDoseId);
        return factor>1?"相生":factor<1?"受克":"同气／平行";
    }
    private void ExecuteNodePlan(double ratio)
    {
        _growth.Clear();_directed.Clear();foreach(var l in SignedLinks)l.LastFlow=0;
        foreach(var l in Scheduled("通"))
        {
            if(l.Built){l.Status="既有通路维持，不重复投入";continue;}
            double amount=Math.Min(200,NodeAvailable(l.SourceDoseId)),need=1+amount/100;
            double p=PrecisionFor(l.Technique??Hand.Care,ratio);
            double sourcePower=At(l.SourceDoseId).Where(x=>Eligible(x)>0).Select(x=>SourcePower(x,"通",p)).DefaultIfEmpty(0).Average();
            l.BuildProgress+=Work(l,Math.Max(0,need-l.BuildProgress),1,ratio,true);
            if(l.BuildProgress+1e-8>=need){l.Built=true;l.BuiltBreath=Breath;l.Capacity=20*sourcePower*(_automaticRunActive?2:1);l.Status=$"已建通路，下一息启用，容量{l.Capacity:F1}/息";}
        }
        foreach(var l in Scheduled("破"))
        {
            var d=InFurnace.FirstOrDefault(d=>d.Id==l.TargetDoseId);if(d is null||!d.Herb.Processing.Dense||d.Open>=1||Heat<1)continue;
            double m=Math.Max(1,d.Raw),points=Work(l,(1-d.Open)*m,12,ratio);d.Open=Math.Min(1,d.Open+points/m);
        }
        foreach(var l in Scheduled("化"))
        {
            var d=InFurnace.FirstOrDefault(d=>d.Id==l.TargetDoseId);if(d is null||Heat<1)continue;
            var stuck=At(d.Id).Where(p=>p.Stuck>0).ToArray();double held=stuck.Sum(p=>p.Stuck);
            if(held>1e-9)
            {
                double freed=Work(l,held,8,ratio);foreach(var p in stuck){p.Stuck-=freed*p.Stuck/held;p.Worked=true;}continue;
            }
            if(d.Open<1-1e-9||Heat<Math.Max(1,d.Herb.Processing.Open-2))continue;
            double processed=Work(l,d.Raw,8*RelationMultiplier(l.SourceDoseId,l.TargetDoseId),ratio);
            Refine(d,processed,PrecisionFor(l.Technique??Hand.Care,ratio));
        }
        foreach(var group in Routes(false).Where(l=>PlannedLinks.Contains(l)&&l.FlowOpen&&l.BuiltBreath<Breath).GroupBy(l=>l.SourceDoseId).ToArray())
        {
            double available=At(group.Key).Sum(Eligible);
            var routes=group.ToArray();
            var capacities=routes.Select(route=>
            {
                double pump=Scheduled("行").Where(l=>l.RouteTarget==route).Sum(l=>At(l.SourceDoseId).Sum(p=>Eligible(p)*SourcePower(p,"行",PrecisionFor(l.Technique??Hand.Care,ratio)))*10);
                double freeSpace=Math.Max(0,(route.TargetDoseId==0?400:200)-NodeMass(route.TargetDoseId));
                double residueRatio=NodeSlag(group.Key)/Math.Max(NodeAvailable(group.Key),1e-9)*(1-Separation(group.Key));
                return Math.Max(0,Math.Min(Math.Min(route.Capacity,route.Capacity/5+pump),Math.Min(route.TargetAmount-route.Transported,freeSpace/(1+residueRatio))));
            }).ToArray();
            double budget=Math.Min(available,capacities.Sum());
            double[] Allocate(double[] weights)
            {
                double[] result=new double[routes.Length];double left=budget;
                for(int pass=0;pass<routes.Length+1&&left>1e-9;pass++)
                {
                    double weight=Enumerable.Range(0,routes.Length).Where(i=>capacities[i]>result[i]+1e-9).Sum(i=>weights[i]);
                    if(weight<=0)break;double used=0;
                    for(int i=0;i<routes.Length;i++)if(capacities[i]>result[i]+1e-9){double add=Math.Min(capacities[i]-result[i],left*weights[i]/weight);result[i]+=add;used+=add;}
                    left-=used;if(used<=1e-9)break;
                }
                return result;
            }
            var requests=Allocate(routes.Select(l=>Math.Max(.001,l.Capacity)).ToArray());
            var guide=Scheduled("引").FirstOrDefault(l=>l.TargetDoseId==group.Key);
            if(guide is not null&&budget>0)
            {
                double RouteDemand(SignedLink route)
                {
                    if(route.TargetDoseId==0)
                    {
                        var source=InFurnace.FirstOrDefault(d=>d.Id==route.SourceDoseId);
                        double atHub=At(0).Where(p=>source is null||p.Source.Kind==source.Kind)
                            .Sum(p=>p.Amount*(1-p.Clustered));
                        double desired=source?.Kind=="主药"?Wanted*22:Wanted*12;
                        return Math.Max(0,desired-atHub);
                    }
                    var downstream=Routes(false).Where(next=>next.SourceDoseId==route.TargetDoseId&&next.FlowOpen&&next.TargetAmount>next.Transported).ToArray();
                    if(downstream.Length==0)return 0;
                    double free=Math.Max(0,200-NodeMass(route.TargetDoseId));
                    return Math.Min(free,downstream.Sum(next=>Math.Min(next.Capacity,Math.Max(0,next.TargetAmount-next.Transported))));
                }
                // 引不再要求玩家填写分支比例：优先把有限药流分给确有下游成丹需求的出口。
                var directed=Allocate(routes.Select(RouteDemand).ToArray());
                if(directed.Sum()<=1e-9)directed=requests;
                double coverage=Work(guide,directed.Sum(),10,ratio)/Math.Max(directed.Sum(),1e-9);
                for(int i=0;i<requests.Length;i++)requests[i]+=(directed[i]-requests[i])*coverage;
                guide.Status="引 · "+string.Join("／",routes.Select((l,i)=>NodeName(l.TargetDoseId)+requests[i].ToString("F1")));
            }
            for(int index=0;index<routes.Length;index++)
            {
                var route=routes[index];double request=requests[index];
                double baseline=Math.Min(request,_automaticRunActive?route.Capacity:route.Capacity/5),extra=0;
                foreach(var pump in Scheduled("行").Where(l=>l.RouteTarget==route))
                {
                    double texture=InFurnace.First(d=>d.Id==route.SourceDoseId).Herb.Craft.texture=="液体"?1.25:1;
                    extra+=Work(pump,Math.Max(0,request-baseline-extra),10*texture*RelationMultiplier(route.SourceDoseId,route.TargetDoseId),ratio);
                }
                double quantity=baseline+extra;
                route.LastFlow=Move(route.SourceDoseId,route.TargetDoseId,quantity);
                route.Transported+=route.LastFlow;
                route.Status=$"通 · 本息{route.LastFlow:F1} / 容量{route.Capacity:F1}";
                foreach(var pump in Scheduled("行").Where(l=>l.RouteTarget==route)){pump.LastFlow=Math.Max(0,route.LastFlow-baseline);pump.Status=$"行 · 增流{pump.LastFlow:F1}";}
            }
        }
        // 聚在牵药之后处理，使本息实际到达的药性可以成为受作用对象，不能作为本息来源。
    }
    private double Move(int source,int target,double requested)
    {
        if(source==target||requested<=1e-9)return 0;
        double sourceMass=NodeAvailable(source),sourceSlag=NodeSlag(source);
        double carryRatio=sourceSlag/Math.Max(sourceMass,1e-9)*(1-Separation(source));
        double space=(target==0?400:200)-NodeMass(target);requested=Math.Min(requested,Math.Max(0,space)/(1+carryRatio));
        var packets=At(source).Where(p=>Eligible(p)>1e-9).OrderByDescending(p=>_directed.Contains(source)&&p.Source.Kind=="主药").ThenBy(p=>p.Source.Id).ToArray();
        double available=packets.Sum(Eligible),amount=Math.Min(requested,available),remaining=amount;
        double existing=NodeAvailable(target);var old=At(target).Where(p=>p.Amount>0).ToArray();
        double y2=old.Sum(p=>p.Amount*Math.Sign(p.Source.Herb.Craft.polarity))/Math.Max(existing,1e-9);
        foreach(var p in packets)
        {
            double q=_directed.Contains(source)?Math.Min(Eligible(p),remaining):Math.Min(Eligible(p),amount*Eligible(p)/Math.Max(available,1e-9));
            q=Math.Min(q,remaining);if(q<=1e-9)continue;
            if(_directed.Contains(source)&&target==0&&p.Source.Kind=="主药")q=Math.Min(q,Math.Max(0,Wanted*22-At(0).Where(x=>x.Source.Kind=="主药").Sum(x=>x.Amount)));
            if(q<=1e-9)continue;
            var next=p.Split(q,target,Breath+1);next.Stuck=0;next.Sealed=0;
            RemoveFree(p,q);next.Returned|=next.Route.Contains(target)&&next.Route.Count>=2&&next.Worked;next.Route.Add(target);
            double y1=Math.Sign(p.Source.Herb.Craft.polarity),intensity=Math.Max(0,-y1*y2);
            double optimal=target==0?1:InFurnace.First(d=>d.Id==target).Herb.Processing.OptimalLow;
            int incoming=PlannedLinks.Count(l=>l.UtilityId=="行"&&l.TargetDoseId==target)+PendingOperations.Count(o=>o.Kind==Hand.Draw&&o.TargetId==target);
            double growth=12*intensity*Math.Min(1,q/25)*(1+.5*Math.Max(0,amount/10-1)+.25*Math.Max(0,Heat-optimal)+.2*Math.Max(0,incoming-2));
            growth=Math.Min(growth,Math.Max(0,25-_growth.GetValueOrDefault(target)));_growth[target]=_growth.GetValueOrDefault(target)+growth;
            if(growth>0){SetZ(target,Z(target)+growth);_shocked.Add(target);}
            bool contact=old.Any(x=>x.Source!=p.Source&&x.Amount>0);
            if(contact)
            {
                double total=existing+q;
                double mixed=Math.Min(1,NodeMixed(target)*Math.Pow(existing/Math.Max(total,1e-9),2)+next.Mix*Math.Pow(q/Math.Max(total,1e-9),2)+Math.Min(.2,q/Math.Max(total,1e-9)));
                foreach(var x in At(target).Where(x=>x.Amount>0)){x.Mix=mixed;x.Worked=true;}
                next.Mix=mixed;next.Worked=true;
            }
            if(target==0)HubStability*=existing/Math.Max(existing+q,1e-9);
            Inventory.Add(next);existing+=q;remaining-=q;
        }
        double moved=amount-remaining;
        double carried=Math.Min(sourceSlag,moved*carryRatio);
        foreach(var residue in At(source).Where(p=>p.Slag>0).ToArray())
        {
            double q=carried*residue.Slag/Math.Max(sourceSlag,1e-9);residue.Slag-=q;AddResidue(residue.Source,q,target);
        }
        if(moved>1e-9)Note($"牵药 {NodeName(source)}→{NodeName(target)} 药性{moved:F2}、夹渣{carried:F2}；保留原材来源，下一息才能再流出");return moved;
    }
    private void ExecuteLateUtilities(double ratio)
    {
        foreach(var l in Scheduled("聚"))
        {
            if(l.TargetDoseId==0&&RefiningGoal is not null&&!HasActualReturn&&!_coreCreated){l.Status="尚无完成加工的真实回流";continue;}
            double need=At(l.TargetDoseId).Where(p=>p.ReadyBreath<=_executingBreath).Sum(p=>p.Amount*(1-p.Clustered));
            double aggregated=Work(l,need,12,ratio,true);if(aggregated<=1e-9)continue;
            double left=aggregated;foreach(var p in At(l.TargetDoseId).Where(p=>p.Amount>0&&p.ReadyBreath<=_executingBreath)){double take=Math.Min(left,p.Amount*(1-p.Clustered));p.Clustered+=take/p.Amount;left-=take;if(left<=1e-9)break;}
            if(l.TargetDoseId==0&&RefiningGoal is not null&&HasActualReturn){_coreCreated=true;Note("真实回流经聚工位汇集，中心丹胚形成");}
        }
        foreach(var l in Scheduled("固"))
        {
            if(l.TargetDoseId==0&&Pills.Count>0)
            {
                double need=Pills.Sum(p=>(1-p.Form)*p.Amount),work=Work(l,need,8,ratio,true);
                foreach(var p in Pills)if(p.Amount>0)p.Form=Math.Min(1,p.Form+work*(1-p.Form)/Math.Max(need,1e-9));
            }
            else if(l.TargetDoseId==0&&NodeAvailable(0)>0){double m=NodeAvailable(0),work=Work(l,(1-HubStability)*m,8,ratio,true);HubStability=Math.Min(1,HubStability+work/m);}
        }
        foreach(var l in Scheduled("镇"))
        {
            double m=Math.Max(1,NodeAvailable(l.TargetDoseId)),done=Work(l,Z(l.TargetDoseId)*m/100,10,ratio);SetZ(l.TargetDoseId,Z(l.TargetDoseId)-done*100/m);
        }
        foreach(var l in Scheduled("明"))
        {
            var d=InFurnace.FirstOrDefault(x=>x.Id==l.TargetDoseId);
            if(d is null)continue;
            double work=Work(l,Math.Max(0,100-d.Diagnosis),15,ratio);
            d.Diagnosis=Math.Min(100,d.Diagnosis+work);
            // 明不代替排渣；它让后续排渣能更准确地分开药性与药渣，直接降低夹带。
            SetSeparation(d.Id,Math.Min(.99,Separation(d.Id)+work/100));
        }
        foreach(var l in Scheduled("藏"))
        {
            double needed=At(l.TargetDoseId).Sum(p=>p.Amount*(1-p.Sealed)),cover=Work(l,needed,10,ratio,true);
            foreach(var p in At(l.TargetDoseId).Where(p=>p.Amount>0).ToArray()){double take=Math.Min(cover,p.Amount*(1-p.Sealed));p.Sealed+=take/p.Amount;p.Worked|=take>0;cover-=take;}
        }
        foreach(var l in Scheduled("护"))_shield[l.TargetDoseId]=_shield.GetValueOrDefault(l.TargetDoseId)+Work(l,Threat(l.TargetDoseId),5,ratio);
    }
    private void ExecuteBasicPlan(double ratio)
    {
        foreach(var op in PendingOperations.Where(o=>o.Kind==Hand.Draw))
        {
            if(RefiningGoal is not null&&!HasActualReturn&&!HasPillCore&&op.TargetId==0){Note("复杂炼法未完成回流，本息不向中心牵药");continue;}
            Move(op.SourceId,op.TargetId,8*Math.Min(1,PrecisionFor(Hand.Draw,ratio)/40));
        }
        ExecuteLateUtilities(ratio);
        foreach(var op in PendingOperations.Where(o=>o.Kind!=Hand.Draw).ToArray())
        {
            double control=Math.Min(1,PrecisionFor(op.Kind,ratio)/40);
            if(op.Kind==Hand.Circulate){foreach(var p in At(op.TargetId)){p.Mix=Math.Min(1,p.Mix+.1*control);p.Worked=true;}}
            if(op.Kind is Hand.Separate or Hand.PreciseSeparate)
            {
                double cap=Math.Min(.99,PrecisionFor(op.Kind,ratio)/60);
                SetSeparation(op.TargetId,Math.Max(Separation(op.TargetId),Math.Min(cap,Separation(op.TargetId)+.15*control+(op.Kind==Hand.PreciseSeparate?.05:0))));
                Note($"{NodeName(op.TargetId)}分离度{Separation(op.TargetId):P0}");
            }
            if(op.Kind==Hand.Purge)PurgeNode(op.TargetId);
            if(op.Kind is Hand.Gather or Hand.Divide)Gather(control);
        }
    }
    private void PurgeNode(int id)
    {
        double waste=NodeSlag(id),remove=Math.Min(waste*.8,FeedLimit*100);
        foreach(var p in At(id).Where(p=>p.Slag>0).ToArray()){double q=p.Slag*remove/Math.Max(waste,1e-9);p.Slag-=q;p.Source.Slag-=q;p.Source.Expelled+=q;}
        double carried=remove*(1-Separation(id))*.2,available=NodeAvailable(id),actual=Math.Min(carried,available);
        foreach(var p in At(id).Where(p=>p.Amount>0).ToArray()){double q=actual*p.Amount/Math.Max(available,1e-9);p.Amount-=q;p.Stuck=Math.Min(p.Stuck,p.Amount);p.Source.Available-=q;p.Source.Expelled+=q;}
        Note($"{NodeName(id)}排出残余{remove:F2}，实际夹带药性{actual:F2}；未处理原料保留");
    }
    private double Threat(int id)
    {
        double threat=0;
        foreach(var p in At(id).Where(p=>p.Amount>0)){double hot=Math.Clamp(.04*(Heat-p.Source.Herb.Processing.Safe)*(p.Source.Herb.Craft.texture=="纤维"?1.5:1),0,.25);threat+=p.Amount*(hot+Math.Max(0,Z(id)-60)/400);}
        if(id==0)foreach(var p in Pills.SelectMany(p=>p.Parts).Where(p=>p.Amount>0)){double hot=Math.Clamp(.04*(Heat-p.Source.Herb.Processing.Safe)*(p.Source.Herb.Craft.texture=="纤维"?1.5:1),0,.25);threat+=p.Amount*(hot+Math.Min(.1,Math.Max(0,Z(id)-60)/400));}
        var d=InFurnace.FirstOrDefault(d=>d.Id==id);if(d is not null)threat+=d.Raw*Math.Clamp(.04*(Heat-d.Herb.Processing.Safe)*(d.Herb.Craft.texture=="纤维"?1.5:1),0,.25);
        return threat;
    }
    private void DamageAndPreserve(double ratio)
    {
        foreach(int id in InFurnace.Select(d=>d.Id).Append(0))
        {
            if(!_shocked.Contains(id))SetZ(id,Z(id)-2);
            double threat=Threat(id),factor=threat>0?Math.Max(0,1-_shield.GetValueOrDefault(id)/threat):0;
            foreach(var p in At(id).Where(p=>p.Amount>0).ToArray())
            {
                double hot=Math.Clamp(.04*(Heat-p.Source.Herb.Processing.Safe)*(p.Source.Herb.Craft.texture=="纤维"?1.5:1),0,.25);
                double injury=p.Amount*(hot+Math.Min(.1,Math.Max(0,Z(id)-60)/400))*factor;
                p.Damage=Math.Min(1,p.Damage+injury*.5/Math.Max(p.Amount,1e-9));
                double lost=Math.Min(p.Amount,injury*.5);p.Amount-=lost;p.Stuck=Math.Min(p.Stuck,p.Amount);p.Source.Available-=lost;p.Source.Slag+=lost;AddResidue(p.Source,lost,id);
            }
            var raw=InFurnace.FirstOrDefault(d=>d.Id==id);
            if(id==0)foreach(var p in Pills.SelectMany(p=>p.Parts).Where(p=>p.Amount>0).ToArray())
            {
                double hot=Math.Clamp(.04*(Heat-p.Source.Herb.Processing.Safe)*(p.Source.Herb.Craft.texture=="纤维"?1.5:1),0,.25);
                double injury=p.Amount*(hot+Math.Min(.1,Math.Max(0,Z(id)-60)/400))*factor;
                p.Damage=Math.Min(1,p.Damage+injury*.5/Math.Max(p.Amount,1e-9));
                double lost=Math.Min(p.Amount,injury*.5);p.Amount-=lost;p.Slag+=lost;
            }
            if(raw is not null&&raw.Raw>0)
            {
                double hot=Math.Clamp(.04*(Heat-raw.Herb.Processing.Safe)*(raw.Herb.Craft.texture=="纤维"?1.5:1),0,.25),hit=raw.Raw*hot*factor;
                raw.Disorder=Math.Min(1,raw.Disorder+hit*.5/Math.Max(raw.Raw,1e-9));raw.Raw-=hit*.5;raw.Slag+=hit*.5;AddResidue(raw,hit*.5,id);
            }
        }
        foreach(var l in Scheduled("生"))
        {
            var ps=At(l.TargetDoseId).Concat(l.TargetDoseId==0?Pills.SelectMany(p=>p.Parts):Enumerable.Empty<Part>()).Where(p=>p.Amount>0).ToArray();double injury=ps.Sum(p=>p.Amount*p.Damage),fixedAmount=Work(l,injury,4,ratio);
            foreach(var p in ps)if(injury>0){p.Damage=Math.Max(0,p.Damage*(1-fixedAmount/injury));p.Worked|=fixedAmount>0;}
        }
        foreach(var p in Inventory.Where(p=>p.Amount>0).ToArray())
        {
            double sealCoefficient=p.Source.Herb.Craft.texture=="气体"?(FurnaceIndex==1?.9:FurnaceIndex==2?.7:1):1;
            double rate=(p.Source.Herb.Craft.texture=="气体"?.03:.002)*sealCoefficient;
            double loss=p.Amount*rate*(1-.9*p.Sealed);p.Amount-=loss;p.Stuck*=1-rate;p.Stuck=Math.Min(p.Stuck,p.Amount);p.Source.Available-=loss;p.Source.Escaped+=loss;
        }
        Inventory.RemoveAll(p=>p.Amount+p.Slag<1e-10);
        var redundant=new HashSet<Part>();
        foreach(var group in Inventory.Where(p=>p.Amount==0).GroupBy(p=>(p.Source,p.Location)))
        {
            var pieces=group.ToArray();if(pieces.Length<2)continue;
            pieces[0].Slag=pieces.Sum(p=>p.Slag);for(int i=1;i<pieces.Length;i++)redundant.Add(pieces[i]);
        }
        Inventory.RemoveAll(redundant.Contains);
    }
    private void UpdateMetrics()
    {
        double total=InFurnace.Sum(d=>NodeAvailable(d.Id))+NodeAvailable(0);
        Agitation=InFurnace.Sum(d=>Z(d.Id)*NodeAvailable(d.Id))/Math.Max(total,1e-9)+Z(0)*NodeAvailable(0)/Math.Max(total,1e-9);
        foreach(var d in InFurnace){d.Mixed=NodeMixed(d.Id);d.Sealed=At(d.Id).Sum(p=>p.Amount*p.Sealed)/Math.Max(NodeAvailable(d.Id),1e-9);}
        Connection=SignedLinks.Count==0?0:SignedLinks.Count(l=>l.Built&&!l.Paused)/(double)SignedLinks.Count;
        Fusion=Inventory.Sum(p=>p.Amount*p.Mix)/Math.Max(Inventory.Sum(p=>p.Amount),1e-9);HubMix=NodeMixed(0);
    }
}
