using System;
using System.Linq;

/// <summary>十二功用的真实材料练习：通过投料、火候与实际操作准备初始状态。</summary>
public sealed class AlchemyUtilityExercise
{
    public static readonly string[] Order=["破","化","通","行","引","聚","固","镇","护","生","明","藏"];
    public AlchemyPreviewModel Model { get; }
    public string Utility { get; }
    public int LessonBreaths => Utility switch { "通"=>2,"护"=>2,_=>1 };
    public static string LessonName(string utility)=>utility switch
    {
        "破"=>"打开药材外壳","化"=>"炼化与解除滞留","通"=>"建立输药通路","行"=>"给通路增流",
        "引"=>"分配支路药流","聚"=>"收拢炉内药团","固"=>"稳住药团结构","镇"=>"平息阴阳躁动",
        "护"=>"过热前保护药性","生"=>"受损后修复药性","明"=>"辨察药团状态","藏"=>"封存易散药性",_=>utility
    };
    public string SourceName=>Model.NodeName(SourceId);
    public string TargetName=>Utility=="行"?"风络藤丝 → 溪纹藻丝的通路":Model.NodeName(TargetId);
    public string WiringSteps=>$"① 点击{SourceName}。\n② 在信息卡里点“{Utility}”。\n③ 点击{TargetName}。\n④ 点击【开炉炼丹】，观察自动运行前后的变化。";
    public int SourceId,TargetId;
    public AlchemyPreviewModel.SignedLink? Route;
    public string Instruction="",Boundary="";
    public AlchemyPreviewModel.Hand Command=AlchemyPreviewModel.Hand.Hold;
    private AlchemyUtilityExercise(string utility){Utility=utility;Model=new(){Wanted=1};Model.SelectFormula("sprout_gentle");Model.SelectMainHerb("青髓芽");}
    public int Id(string name)=>Model.InFurnace.Single(d=>d.Herb.Name==name).Id;
    public void Tick(AlchemyPreviewModel.Hand? command=null)
    {
        Model.Action=command??Command;string error=Model.Advance();
        if(error.Length>0||Model.Done)throw new InvalidOperationException("功用练习结算失败："+error+" "+Model.Result);
        Model.AssertConservation();
        Model.Action=AlchemyPreviewModel.Hand.Hold;
    }
    private void Prepare(int heat,params (string Name,double Amount)[] ingredients)
    {
        heat=(heat+2)/3;
        foreach(var (name,amount) in ingredients)
        {
            if(Model.Pending.Sum(d=>d.Original)/100+amount>Model.FeedLimit)Tick(AlchemyPreviewModel.Hand.Hold);
            string result=Model.QueueHerb(name,amount);if(!Model.Pending.Any(d=>d.Herb.Name==name))throw new InvalidOperationException(result);
        }
        while(Model.Heat<heat)Tick(AlchemyPreviewModel.Hand.Raise);
        for(int guard=0;Model.InFurnace.Any(d=>d.Raw>.001)&&guard<35;guard++)Tick(AlchemyPreviewModel.Hand.Hold);
        if(Model.InFurnace.Any(d=>d.Raw>.001))throw new InvalidOperationException("教学材料未在指定火候炼完");
    }
    private void Cool(){while(Model.Heat>1)Tick(AlchemyPreviewModel.Hand.Lower);}
    private void Draw(int source,int target=0){Model.QueueOperation(AlchemyPreviewModel.Hand.Draw,target,source);Tick(AlchemyPreviewModel.Hand.Hold);}
    public static AlchemyUtilityExercise Create(string utility)
    {
        var e=new AlchemyUtilityExercise(utility);var m=e.Model;
        switch(utility)
        {
            case "破":
                e.Prepare(5,("赤纹燧石",.4));e.Cool();m.QueueHerb("青木芯",.3);e.Tick();
                e.SourceId=e.Id("赤纹燧石");e.TargetId=e.Id("青木芯");
                e.Instruction="赤纹燧石的破 → 青木芯。青木芯带有致密外层；在同一观察时段内比较自然开壳与破的实际进度。";
                e.Boundary="破打开真实结构；结构打开不等于已经炼出药性。继续加工要看火候或化。";break;
            case "化":
                e.Prepare(3,("月露菌汁",.4),("凝露松脂",.4));e.Cool();
                e.SourceId=e.Id("月露菌汁");e.TargetId=e.Id("凝露松脂");
                e.Instruction="月露菌汁的化 → 凝露松脂。松脂已经炼出药性，但仍有黏胶滞留；比较化前后的可流动药性。";
                e.Boundary="化也可解除黏胶滞留；解滞不再收一次炼化得率，化不代替破开结构。";break;
            case "通":
                e.Prepare(5,("风络藤丝",.5),("青髓芽",.4));e.Cool();
                e.SourceId=e.Id("风络藤丝");e.TargetId=0;
                e.Instruction="风络藤丝的通 → 汇合位。通先建立联系，再按基础流量把药性送到炉口。";
                e.Boundary="通负责联系与基础缓流，建成不重复扣建设药性；没有行也能运输。";break;
            case "行":
                e.Prepare(5,("风络藤丝",.5),("溪纹藻丝",.4),("乌羽片",.4));e.Cool();
                m.SignLink(e.Id("风络藤丝"),e.Id("溪纹藻丝"),"通");e.Tick();e.Route=m.SignedLinks.Single();
                e.SourceId=e.Id("乌羽片");e.TargetId=e.Id("溪纹藻丝");
                e.Instruction="乌羽片的行 → 藤丝至藻丝这段通路。先记下基础流量，再沿同一条路线增流。";
                e.Boundary="行作用于路线，推动的可以是别的材料；增流仍受通路容量、来源和空间限制。";break;
            case "引":
                e.Prepare(5,("风络藤丝",.1),("青髓芽",.4),("雾铃花",.4));e.Cool();
                m.SignLink(e.Id("风络藤丝"),0,"通");m.SignLink(e.Id("风络藤丝"),e.Id("青髓芽"),"通");e.Tick();
                e.SourceId=e.Id("雾铃花");e.TargetId=e.Id("风络藤丝");
                e.Instruction="雾铃花的引 → 藤丝分流节点。直通炉口和通往无后续通路节点的两条支路同时存在；引会把药性导向仍有下游需求的路线。";
                e.Boundary="引按实际下游需求分配，不要求输入比例；它不增加药性或路线容量，断路与满载仍会限制分配。";break;
            case "聚": case "固":
                e.Prepare(5,("青髓芽",.5),(utility=="聚"?"回泉珠":"凝露松脂",.5));e.Cool();
                e.Draw(e.Id("青髓芽"));e.Draw(e.Id("青髓芽"));e.Draw(e.Id("青髓芽"));
                e.SourceId=e.Id(utility=="聚"?"回泉珠":"凝露松脂");e.TargetId=0;
                e.Instruction=utility=="聚"?"回泉珠的聚 → 汇合位。药性已经实际送到，比较已收拢量。":"凝露松脂的固 → 汇合位。比较已有药团的结构稳定。";
                e.Boundary=utility=="聚"?"聚收拢已到货药性，不从远处吸药；简单汇合不自动成为复杂闭环丹胚。":"固稳定已有组成，不纠正错配；新药流加入会稀释已有稳定，需要继续处理。";break;
            case "镇":
                e.Prepare(5,("泥脉石团",.4),("雾铃花",.4),("凝露松脂",.4));
                e.Draw(e.Id("雾铃花"));e.Draw(e.Id("凝露松脂"));
                e.SourceId=e.Id("泥脉石团");e.TargetId=0;
                e.Instruction="泥脉石团的镇 → 汇合位。阴阳药性刚发生真实接触，比较自然平息与主动压制。";
                e.Boundary="镇处理已有躁动，不降低线位与操作的灵识负担，也不能修复热伤。";break;
            case "护":
                m.Attainment=40; // 本课演示超过2档耐受的热伤，明确需要可控3档。
                e.Prepare(5,("霜叶枝",.4));e.Cool();
                m.PlaceHerb("风络藤丝",.5);
                m.PlaceHerb("风纹石英",.25);
                e.SourceId=e.Id("霜叶枝");e.TargetId=e.Id("风络藤丝");e.Command=AlchemyPreviewModel.Hand.Raise;
                e.Instruction="本课操作者造诣40，可控3档。风纹石英需要3档，风络藤丝的安全上限为2档；霜叶枝的护 → 风络藤丝，比较自动升温时的真实热伤。";
                e.Boundary="护先抵挡真实威胁，按实际抵挡投入；持续高火仍需持续供力，安全火候下没有热伤就不空耗。";break;
            case "生":
                m.Attainment=40;
                e.Prepare(5,("青髓芽",.5),("风络藤丝",.5));e.Tick(AlchemyPreviewModel.Hand.Raise);e.Cool();
                e.SourceId=e.Id("青髓芽");e.TargetId=e.Id("风络藤丝");
                e.Instruction="本课操作者造诣40，曾以3档制造过热对照。青髓芽的生 → 曾过热的风络藤丝。已敛焰至安全火候，比较可恢复能力损伤。";
                e.Boundary="生恢复仍存在药性的能力，不增加质量；已经转渣、逸散或消耗的部分不能复生。";break;
            case "明":
                e.Prepare(6,("清音玉片",.4),("青髓芽",.4));e.Cool();
                e.SourceId=e.Id("清音玉片");e.TargetId=e.Id("青髓芽");
                e.Instruction="清音玉片的明 → 青髓芽节点。明提升状态显露与实际分离度，后续排渣夹带会减少。";
                e.Boundary="明不会自动排渣，也不能追回已经逸散的药性；它只改善之后的辨析与分离。";break;
            case "藏":
                e.Prepare(5,("凝霜叶",.4),("青髓芽",.4));e.Cool();
                e.SourceId=e.Id("凝霜叶");e.TargetId=e.Id("青髓芽");
                e.Instruction="凝霜叶的藏 → 青髓芽。比较同一初始炉况下的实际逸散与封存覆盖量，数字显示至三位小数。";
                e.Boundary="藏减缓未来散失，不追回已散失药性；封存覆盖的部分不能直接被牵走。";break;
            default:throw new ArgumentOutOfRangeException(nameof(utility));
        }
        m.PlannedLinks.Clear();m.PendingOperations.Clear();m.RepeatingOperations.Clear();
        m.Explanation="功用练习 · "+utility+"\n"+e.Instruction;m.AssertConservation();return e;
    }
    public string Apply(bool reverse=false)
    {
        var m=Model;
        string result=Utility=="行"?m.SignFlowSupport(SourceId,Route!):m.SignLink(SourceId,TargetId,Utility);
        var line=m.SignedLinks.LastOrDefault(l=>l.UtilityId==Utility&&l.SourceDoseId==SourceId);
        if(line is null)throw new InvalidOperationException(result);
        m.RunAutomaticLesson(LessonBreaths);return result;
    }
    public void FollowUp()
    {
        // 专项课只比较这条线的一次真实自动运行；后续变化交给自由炉局探索。
    }
    public double Metric()
    {
        var m=Model;var target=m.InFurnace.FirstOrDefault(d=>d.Id==TargetId);
        var ps=m.Inventory.Where(p=>p.Location==TargetId).ToArray();
        double mass=ps.Sum(p=>p.Amount);
        return Utility switch
        {
            "破"=>target!.Open*100,"化"=>target!.Raw+ps.Sum(p=>p.Stuck),"通"=>m.NodeAvailable(0),"行"=>Route!.LastFlow,
            "引"=>m.SignedLinks.First(l=>l.UtilityId=="通"&&l.TargetDoseId==0).LastFlow,
            "聚"=>ps.Sum(p=>p.Amount*p.Clustered),"固"=>m.HubStability*100,"镇"=>m.NodeAgitation(0),
            "护" or "生"=>mass<=0?0:ps.Sum(p=>p.Amount*p.Damage)/mass*100,
            "明"=>target!.Separation*100,"藏"=>ps.Sum(p=>p.Amount*p.Sealed),_=>0
        };
    }
    public string MetricLabel=>Utility switch
    {
        "破"=>"开启进度(%)","化"=>"未处理原料＋黏胶滞留","通"=>"中心到货","行"=>"线路流量","引"=>"分支到货差异",
        "聚"=>"已收拢量","固"=>"结构稳定(%)","镇"=>"局部躁动","护" or "生"=>"能力损伤(%)","明"=>"节点分离度(%)",_=>"封存覆盖量"
    };
    public string MetricUnit=>Utility is "破" or "固" or "镇" or "护" or "生" or "明"?"%":"质量单位";
    public string Readout()
    {
        var m=Model;var target=m.InFurnace.FirstOrDefault(d=>d.Id==TargetId);
        var ps=m.Inventory.Where(p=>p.Location==TargetId).ToArray();
        string detail=Utility switch
        {
            "破"=>$"结构开启 {Metric():F1}% · 未处理原料 {target!.Raw:F3}",
            "化"=>$"未处理原料 {target!.Raw:F3}质量单位\n药性占比 {(m.NodeMass(TargetId)>0?m.NodeAvailable(TargetId)/m.NodeMass(TargetId):0):P1} · 黏胶滞留 {ps.Sum(p=>p.Stuck):F3}质量单位",
            "通"=>$"已建通路 {m.SignedLinks.Count(l=>l.UtilityId=="通"&&l.Built)} · 汇合到货 {m.NodeAvailable(0):F3}",
            "行"=>$"该段本息输送 {Route!.LastFlow:F3}／容量{Route.Capacity:F1}",
            "引"=>string.Join("\n",m.SignedLinks.Where(l=>l.UtilityId=="通").Select(l=>$"至{m.NodeName(l.TargetDoseId)}：{l.LastFlow:F3}")),
            "聚"=>$"已收拢 {Metric():F3}质量单位\n药团已在炉内，不会从远处吸药。",
            "固"=>$"结构稳定 {m.HubStability:P1}","镇"=>$"局部躁动 {m.NodeAgitation(0):F3}",
            "护" or "生"=>$"可恢复损伤 {Metric():F3}%\n药性占比 {(m.NodeMass(TargetId)>0?m.NodeAvailable(TargetId)/m.NodeMass(TargetId):0):P1} · 残渣 {(m.NodeMass(TargetId)>0?m.NodeSlag(TargetId)/m.NodeMass(TargetId):0):P1}",
            "明"=>$"诊断显露 {target!.Diagnosis:F1}% · 节点分离度 {target.Separation:P1} · 影响后续排渣夹带",
            "藏"=>$"封存覆盖 {Metric():F3}\n目标累计逸散 {target!.Escaped:F3}",_=>""
        };
        return detail+$"\n来源已消耗 {m.InFurnace.Single(d=>d.Id==SourceId).Spent:F3}质量单位\n灵识占用 {m.Preview().Spirit:F1}/{m.Preview().Capacity:F0}";
    }
}
