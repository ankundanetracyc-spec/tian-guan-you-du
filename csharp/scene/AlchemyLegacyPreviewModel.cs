using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>独立试验演算。参数与丹效试算仅用于体验，不写入正式材料、丹药或存档。</summary>
public sealed class AlchemyLegacyPreviewModel
{
    public enum Hand { Hold, Raise, Lower, Circulate, Separate, Purge, Gather, Output, Fine }
    public static readonly string[] HandNames = ["维持", "加火", "减火", "搅动药性", "分离", "排渣", "凝丹", "出炉", "精细控火"];
    public static readonly Dictionary<string, string> Purposes = new()
    {
        ["破"]="瓦解尚未打开的结构", ["化"]="把已打开材料炼成可用部分", ["通"]="建立不同药性之间的联系",
        ["行"]="推动已连通药性循环", ["聚"]="汇集分散的药性", ["引"]="导向各颗丹药，改善分配",
        ["固"]="保持已聚拢部分的形状", ["镇"]="压制实际躁动", ["护"]="抵挡实际热伤与冲撞",
        ["生"]="修复尚可恢复的失调", ["明"]="显化炉况，帮助辨认分离对象", ["藏"]="收敛正在逸散的药性"
    };
    public sealed record Herb(string Name, ItemData Item, MaterialCraftData Craft)
    {
        public bool CanBeMain => Name is "青木芯";
        public int Grade(string id) => Craft.utilities.Where(u=>u.utility_id==id).Select(u=>u.grade).DefaultIfEmpty(0).Max();
        public double Power(string id) => Grade(id)==0 ? 0 : Math.Pow(1.8, Grade(id)-1);
    }
    // 一份材料总量100；未处理→可用80%+残渣20%。能力没有独立物质库存。
    public sealed class Dose(Herb herb, string kind, int batch)
    {
        public Herb Herb=herb;
        public string Kind=kind;
        public int Batch=batch;
        public double Raw=100, Available, Slag, Spent, Destroyed, Escaped, Expelled;
        public double Open, Exposure, Disorder, Separation;
        public double Processed => 100-Raw;
        public double Released => Processed/100;
        public double Mass => Raw+Available+Slag;
        public double Balance => Raw+Available+Slag+Spent+Destroyed+Escaped+Expelled;
        public double Readiness(string id)
        {
            int grade=Herb.Grade(id);
            if(grade==0) return 0;
            return Math.Min(1,Released/.8)*Math.Min(1,Exposure/(2+Math.Pow(grade,1.4)))*(1-Math.Clamp(Disorder,0,.8));
        }
        public double Ability(string id) => Herb.Power(id)*Readiness(id)*Available/80;
    }
    public sealed record Forecast(double Spirit, double Capacity, int Mana, double Temperature)
    { public double Ratio=>Spirit/Capacity; }
    public sealed record Pill(int Index, double MainAmount, double Mix, double Form, double Capability, int Score, string Reason);
    public sealed record TextureRule(double OpenHeat,double Ideal,double Safe,double Rate,string State);
    public static TextureRule Profile(string testProfile)
    {
        var all=testProfile.Split(new[]{'、',',','/'},StringSplitOptions.RemoveEmptyEntries).Select(t=>t.Trim()).ToArray();
        var rules=all.Select(t=>t switch
        {
            "骨"=>new TextureRule(40,75,94,21,"粉膏"), "甲"=>new(35,65,87,23,"膏"),
            "石"=>new(45,80,98,18,"粉"), "金"=>new(50,85,100,17,"熔质"),
            "晶"=>new(45,78,95,20,"晶液"), "玉"=>new(35,62,85,21,"玉膏"),
            "砂"=>new(30,60,85,29,"细粉"), "液"=>new(10,30,60,60,"液"),
            "气"=>new(5,25,48,75,"气"), "布"=>new(15,40,65,37,"纤膏"),
            _=>new TextureRule(20,44,66,32,"药膏")
        }).ToArray();
        if(rules.Length==0) return new(20,44,66,32,"药膏");
        return new(rules.Max(r=>r.OpenHeat),rules.Average(r=>r.Ideal),rules.Min(r=>r.Safe),rules.Min(r=>r.Rate),string.Join("/",rules.Select(r=>r.State).Distinct()));
    }
    public readonly List<Herb> Herbs=[];
    public readonly List<Dose> Pending=[], InFurnace=[];
    public readonly List<string> Log=[], Changes=[];
    public readonly Dictionary<string,string> Contributions=[];
    public readonly List<Pill> Pills=[];
    public int Breath, Mana=AlchemyPreviewRules.MainMana, HelperMana=AlchemyPreviewRules.HelperMana, Potions=2, Attainment=32, Wanted=3;
    public double Temperature=20, Heat=1, Connection, Fusion, Uniform=.3, Concentration, Shape, Agitation;
    public Hand Action=Hand.Hold;
    public bool Done, Helper, Recover, TakePotion, FineLearned;
    public string MainName="青木芯", Explanation="拖拽青木芯到丹炉，再选择手段并推进一息。", Result="";
    public int Count=>InFurnace.Concat(Pending).Count(d=>d.Kind=="主药")*3;
    public int OperatorAttainment=>Helper?24:Attainment;
    public double MainAmount=>InFurnace.Where(d=>d.Kind=="主药").Sum(d=>d.Available);
    public double Available=>InFurnace.Sum(d=>d.Available);
    public double Polarity=>Available<.001?0:InFurnace.Sum(d=>d.Available*Math.Sign(d.Herb.Craft.polarity))/Available;
    public double LastSupply=1;
    public double ActualFire=>Breath==0||Done?0:Heat*LastSupply;
    public double Slag=>InFurnace.Sum(d=>d.Slag);
    public AlchemyLegacyPreviewModel()
    {
        foreach(string name in new[]{"青木芯","灵泉露","苦苔根","凝露松脂","雾铃花","灵泉藻","风纹石英","雾心菌核","青羽翎","玄甲薄片","断角骨片","清音玉片"})
        {
            var item=GD.Load<ItemData>($"res://resources/entities/items/{name}.tres");
            if(item?.craft_data is not MaterialCraftData craft) throw new InvalidOperationException($"材料缺失：{name}");
            Herbs.Add(new(name,item,craft));
        }
    }
    public string QueueHerb(string name)
    {
        if(Done) return "本炉已结束，请重新开炉。";
        var h=Herbs.First(x=>x.Name==name);
        if(name==MainName&&!h.CanBeMain) return "本试方不允许这味材料作主药。";
        int previousCount=Count;
        if(name==MainName && InFurnace.Concat(Pending).Any(d=>d.Kind=="主药"&&d.Herb.Name!=name)) return "主药只能一种，请重新开炉更换。";
        if(InFurnace.Count+Pending.Count>=AlchemyPreviewRules.FurnaceCapacity) return "试验炉最多容纳24份。";
        int max=Math.Max(1,OperatorAttainment/8);
        if(Pending.Count>=max) return $"本次投料上限{max}份（当前操作者造诣{OperatorAttainment}）。请分息投入。";
        Pending.Add(new(h,name==MainName?"主药":"辅药",Breath+1));
        if(name==MainName&&(previousCount==0||Wanted==previousCount)) Wanted=Count;
        return $"已安排 {name}×1；推进一息才入炉。";
    }
    public string LearnFine()
    {
        if(Attainment<AlchemyPreviewRules.FineLearning) return "精细控火的学习资格需要造诣24。";
        FineLearned=true; return "已在试验页学会精细控火：不增加火力，专注保存高品能力；灵识占用较高。";
    }
    public double Effect(string id)=>InFurnace.Sum(d=>d.Ability(id));
    public Forecast Preview()
    {
        double mass=InFurnace.Sum(d=>d.Mass)/100+Pending.Count;
        double gradeLoad=InFurnace.Sum(d=>d.Herb.Craft.utilities.Sum(u=>Math.Pow(u.grade,1.35)*d.Readiness(u.utility_id)*d.Available/80));
        double spirit=7+mass*3+Pending.Count*4+gradeLoad*.8+Agitation*.1+
            (Action switch { Hand.Circulate=>9,Hand.Separate=>12,Hand.Purge=>5,Hand.Gather=>8+Wanted*2,Hand.Fine=>16,_=>3 });
        double heat=Math.Clamp(Heat+(Action==Hand.Raise?1:Action==Hand.Lower?-1:0),0,4);
        int mana=(int)Math.Ceiling(heat*(2+mass*.65));
        double target=20+heat*19;
        double temp=Temperature+(target-Temperature)*.43/(1+mass*.035);
        return new(spirit,Helper?58:80,mana,temp);
    }
    private void Note(string s) { Changes.Add(s); Log.Add($"第{Breath}息 · {s}"); }
    // 实际工作从同一份可用药性计一次支出；完成后不重复扣。返回实际完成工作量。
    private double Aid(string id,double demand,double efficiency,string objectName)
    {
        if(demand<=.0001) return 0;
        double done=0;
        foreach(var d in InFurnace)
        {
            double ability=d.Ability(id);
            double work=Math.Min(demand-done,Math.Min(ability*efficiency,d.Available*efficiency));
            if(work<=.0001) continue;
            double use=work/efficiency; d.Available-=use; d.Spent+=use; done+=work;
            Contributions[$"{d.Herb.Name}·{id}"]=$"{d.Kind} {d.Herb.Name}的{id}：{objectName} {work:F2}；投入共用药性 {use:F2}";
            if(done>=demand) break;
        }
        if(done>.005) Note($"{id}：{objectName} {done:F2}（按实际作用投入）。");
        return done;
    }
    private double Structural(string id,double demand,double rate,string target)
    {
        double result=Math.Min(demand,Effect(id)*rate);
        if(result>0)
        {
            foreach(var d in InFurnace.Where(d=>d.Ability(id)>0)) Contributions[$"{d.Herb.Name}·{id}"]=$"{d.Kind} {d.Herb.Name}的{id}：{target}；共用药性仍在炉内，已形成结果不重复收费";
            Note($"{id}：{target} +{result:F2}。");
        }
        return result;
    }
    public string Advance()
    {
        if(Done) return "本炉已结束。";
        if(OperatorAttainment<AlchemyPreviewRules.MinimumAttainment) return "九品丹要求控炉者造诣至少8。";
        if(!InFurnace.Concat(Pending).Any(d=>d.Kind=="主药")) return "请先投入选定主药。";
        if(Action==Hand.Fine&&!FineLearned) return "先学会精细控火。";
        if(Action==Hand.Fine&&OperatorAttainment<AlchemyPreviewRules.FineLearning) return "当前控炉者造诣不足24，不能施展精细控火。";
        if(Pending.Count>Math.Max(1,OperatorAttainment/8)) return "本息待投数量超出当前控炉者的造诣上限，请撤回部分药材或换人控炉。";
        if(Action==Hand.Gather&&Wanted>OperatorAttainment/8*3) return "本次分丹规模超过操作者造诣允许的上限，请减产或更换操作者。";
        if(Recover&&!Helper) return "回气需要副手接管；也可取消回气、改用服药。";
        var forecast=Preview(); Changes.Clear(); Contributions.Clear(); Breath++;
        Note($"操作：{HandNames[(int)Action]}；{(Helper?"副手":"本人")}控炉。");
        if(TakePotion&&Potions>0) { Mana=Math.Min(AlchemyPreviewRules.MainMana,Mana+AlchemyPreviewRules.PotionRestore); Potions--; Note("服回灵散：本人法力恢复45。"); } TakePotion=false;
        if(forecast.Ratio>1) { Done=true;Result="炸炉：灵识占用超过容量，失去控制。"; Note(Result);return ""; }
        if(forecast.Ratio>.8) Note($"灵识不支（{forecast.Ratio:P0}）：操作精细程度下降，炉内躁动增加。");
        Heat=Math.Clamp(Heat+(Action==Hand.Raise?1:Action==Hand.Lower?-1:0),0,4);
        int paid=Math.Min(Helper?HelperMana:Mana,forecast.Mana);
        if(Helper) HelperMana-=paid; else Mana-=paid;
        double supply=forecast.Mana==0?1:(double)paid/forecast.Mana;LastSupply=supply;
        Temperature=forecast.Temperature-(1-supply)*12;
        if(supply<1) Note("法力不足，供火衰减。请考虑副手接替或服药。");
        if(Helper&&Recover) { Mana=Math.Min(AlchemyPreviewRules.MainMana,Mana+AlchemyPreviewRules.RecoverRestore);Note("副手顶炉，本人回气：法力+25。"); }
        foreach(var d in Pending) { InFurnace.Add(d);Note($"投药：{d.Kind} {d.Herb.Name}×1，第{d.Batch}息入炉。"); } Pending.Clear();
        Wanted=Math.Clamp(Wanted,1,Math.Max(1,Count));
        double control=forecast.Ratio<=.8?1:Math.Max(.35,1-(forecast.Ratio-.8)*2.5);
        // 所有批次并行受热。化、破使用本息开始时已有的可用部分。
        foreach(var d in InFurnace)
        {
            var p=Profile(d.Herb.Craft.legacy_texture);
            double warm=Math.Clamp((Temperature-p.OpenHeat)/(p.Ideal-p.OpenHeat),0,1.15);
            if(warm>0&&d.Raw>0)
            {
                double open=Math.Min(1-d.Open,.26*warm);
                open+=Aid("破",Math.Max(0,1-d.Open-open),.10,"打开材料结构");
                d.Open=Math.Clamp(d.Open+open,0,1);
                double raw=Math.Min(d.Raw,p.Rate*warm*d.Open);
                raw+=Aid("化",Math.Max(0,Math.Min(d.Raw-raw,8)),4,"促进材料炼化");
                d.Raw-=raw;d.Available+=raw*AlchemyPreviewRules.UsefulYield;d.Slag+=raw*(1-AlchemyPreviewRules.UsefulYield);
                if(raw>0) Note($"{d.Herb.Name}炼化 {raw:F1}：炼出药性 {raw*.8:F1}，产生药渣 {raw*.2:F1}。");
            }
            if(Temperature>=p.OpenHeat&&Temperature<=p.Safe)
                d.Exposure+=(Action==Hand.Fine?3:1)*control/(1+InFurnace.Count*.08);
        }
        double contact=InFurnace.Any(d=>d.Available>1)?1:0;
        if(contact>0)
        {
            // 五行只提供本试验明确的同属性接触修正；不导入全局相生相克。
            var elements=InFurnace.Where(d=>d.Available>1&&d.Herb.Craft.element.Length>0).GroupBy(d=>d.Herb.Craft.element);
            double sameElement=elements.Any(g=>g.Count()>1)?.015:0;
            Connection=Math.Min(1,Connection+.035+sameElement+Structural("通",1-Connection,.035,"建立药性联系"));
            double need=Math.Max(1-Fusion,(1-Uniform)/.8);
            double flow=Math.Min(need,(Action==Hand.Circulate?.16:.018)*Connection*control);
            flow+=Aid("行",Math.Max(0,Math.Min(.2-flow,need-flow))*Connection,.10,"推动药性循环");
            Fusion=Math.Min(1,Fusion+flow);
            Uniform=Math.Min(1,Uniform+flow*.8);
            if(flow>0) Note($"药性交流：混匀 {Fusion:P0}，分配均匀 {Uniform:P0}。");
        }
        double impact=Agitation+(forecast.Ratio>.8?8:0)+(Action==Hand.Raise?4:0)+Math.Abs(Polarity)*2;
        impact=Math.Max(0,impact-3);
        impact-=Aid("镇",impact,5,"压制躁动");Agitation=Math.Clamp(impact,0,100);
        foreach(var d in InFurnace)
        {
            var p=Profile(d.Herb.Craft.legacy_texture);
            double danger=Math.Max(0,Temperature-p.Safe)*.09*d.Available/80+Agitation*.003;
            double blocked=Aid("护",danger,4,"抵挡热伤／冲撞");
            double harm=Math.Max(0,danger-blocked);
            double destroyed=Math.Min(d.Available,harm*.6);d.Available-=destroyed;d.Destroyed+=destroyed;
            d.Disorder=Math.Clamp(d.Disorder+harm*.018,0,.8);
            double repair=Aid("生",d.Disorder,.09,"修复可恢复失调");d.Disorder-=repair;
            double leak=d.Available*(.0008+Math.Max(0,Temperature-55)*.00008)*(1-Concentration*.6);
            leak-=Aid("藏",leak,6,"阻止药性逸散");leak=Math.Min(d.Available,Math.Max(0,leak));d.Available-=leak;d.Escaped+=leak;
            if(harm>.05) Note($"{d.Herb.Name}受损：烧毁{destroyed:F2}，可修复损伤{d.Disorder:P0}；烧毁不可恢复。");
        }
        if(Action==Hand.Separate)
        {
            double remaining=InFurnace.Select(d=>1-d.Separation).DefaultIfEmpty(0).Max();
            double discern=Structural("明",Math.Max(0,remaining-.4*control),.12,"认清药性与药渣");
            foreach(var d in InFurnace) d.Separation=Math.Min(1,d.Separation+.4*control+discern);
            Note($"分离完成程度 {InFurnace.Average(d=>d.Separation):P0}；排渣前可查看预计夹带损失。");
        }
        if(Action==Hand.Purge)
        {
            double removed=0,carried=0;
            foreach(var d in InFurnace)
            {
                double slag=d.Slag*.9;
                double carry=Math.Min(d.Available,slag*(1-d.Separation)*.18);
                d.Slag-=slag;d.Available-=carry;d.Expelled+=slag+carry;removed+=slag;carried+=carry;
            }
            Note($"排渣：残渣 {removed:F1}，夹带可用药性 {carried:F2}。未炼开材料仍留炉内。");
        }
        // 聚、固形成的结果留在炉内；对未完成部分增益，不每息重复消耗同一个结果。
        if(Action==Hand.Gather&&Available>0)
        {
            Concentration=Math.Min(1,Concentration+.2*control+Structural("聚",1-Concentration,.07,"汇集分散药性"));
            Uniform=Math.Min(1,Uniform+Aid("引",1-Uniform,.08,"导向各颗药团"));
            double heatFit=Temperature is >=28 and <=65?1:.35;
            Shape=Math.Min(1,Shape+(.17+Structural("固",1-Shape,.08,"保持药团形状"))*Concentration*heatFit*control);
            Note($"凝成{Wanted}颗：汇集{Concentration:P0}，成形{Shape:P0}；每颗主药分配见丹方判定。");
        }
        if(Action!=Hand.Gather&&Shape>0&&Agitation>10) Shape=Math.Max(0,Shape-Agitation*.001);
        Note($"法力消耗{paid}，火力{Heat:F0}档，余热炉温{Temperature:F0}；本息灵识{forecast.Ratio:P0}。");
        if(Action==Hand.Output) Finish();
        return "";
    }
    public string FunctionStatus(Herb h,string id)
    {
        if(Contributions.TryGetValue($"{h.Name}·{id}",out var actual)) return actual;
        var doses=InFurnace.Where(d=>d.Herb.Name==h.Name).ToArray();
        if(doses.Length==0) return "尚未投入丹炉。";
        if(doses.All(d=>d.Available<.001)) return "还没有炼出足够药性，或可用药性已经耗尽。";
        if(doses.All(d=>d.Readiness(id)<.05)) return "药性尚未充分炼出，需要继续适当加热和精细照管。";
        return id switch
        {
            "生"=>"本息没有需要修复的损伤；不会凭空增加药性。",
            "护"=>"本息没有需要抵挡的热伤或冲撞。",
            "镇"=>"本息没有需要压制的躁动。",
            "破" or "化"=>"本息没有可继续处理的材料，或火候尚不足。",
            "通"=>"药性已充分连通，本息不再重复建立联系。",
            "行"=>"药性已混匀且分配均匀，或尚未连通；本息不消耗药性重复运转。",
            "聚" or "固" or "引"=>"点击凝丹时帮助汇集、分配和保持形状；完成后不重复作用。",
            "明"=>"点击分离时帮助认清药性和药渣；分离完成后不重复作用。",
            _=>"本息没有需要阻止的药性流失。"
        };
    }
    public double Capability=>InFurnace.Where(d=>d.Kind=="主药").Select(d=>d.Readiness("生")).DefaultIfEmpty(0).Average();
    public double CarryRisk=>InFurnace.Sum(d=>Math.Min(d.Available,d.Slag*.9*(1-d.Separation)*.18));
    public double Allocation(int i)
    {
        double mid=(Wanted-1)/2d;
        return MainAmount/Wanted*(1+(i-mid)*.24*(1-Uniform));
    }
    public void Finish()
    {
        Pills.Clear();Done=true;
        // 体验用试方：完整明示各项得分。不是正式丹效算法，不影响正式丹药资源。
        for(int i=0;i<Wanted;i++)
        {
            double amount=Allocation(i), dose=Math.Min(1,amount/AlchemyPreviewRules.FullPerPill);
            var failures=new List<string>();
            if(amount<AlchemyPreviewRules.MinPerPill) failures.Add("分到的主药药性不足8份");
            if(Fusion<AlchemyPreviewRules.MinMix) failures.Add("混合不足60%");
            if(Shape<AlchemyPreviewRules.MinShape) failures.Add("成形不足60%");
            if(Math.Abs(Polarity)>AlchemyPreviewRules.MaxPolarity) failures.Add("试方阴阳偏向超出±0.75");
            if(Temperature>AlchemyPreviewRules.OpenTemperature) failures.Add("试验炉开炉温度超过70");
            double slagPenalty=Math.Min(20,Slag/Math.Max(1,Available)*20);
            int score=(int)Math.Round(Math.Clamp(AlchemyPreviewRules.DosePoints*dose+AlchemyPreviewRules.MixPoints*Fusion+AlchemyPreviewRules.ShapePoints*Shape+AlchemyPreviewRules.CapabilityPoints*Capability-slagPenalty,0,100));
            if(failures.Count>0) score=Math.Min(19,score);
            string reason=$"剂量{50*dose:F1}/50＋混合{20*Fusion:F1}/20＋成形{20*Shape:F1}/20＋主药生能力保存{10*Capability:F1}/10－残渣影响{slagPenalty:F1}";
            if(failures.Count>0) reason+="；未通过："+string.Join("、",failures);
            Pills.Add(new(i+1,amount,Fusion,Shape,Capability,score,reason));
        }
        int success=Pills.Count(p=>p.Score>=20);
        Result=success==0?"未成丹：查看各颗判定原因。":$"出炉：{success}颗成丹，{Pills.Count-success}份未成丹（丹效为试方试算）。";
        Note(Result);
    }
    public static string Tier(int n)=>n<20?"未成丹":n<=50?"薄效":n<=75?"良效":n<=90?"优效":"极效";
    public string Checks()=> $"主药可用 {MainAmount:F1}；尝试 {Wanted} 颗，每颗满量24、最低8、超过24不再加分。\n混合 {Fusion:P0}（至少60%）；成形 {Shape:P0}（至少60%）。\n阴阳偏向 {Polarity:+0.00;-0.00;0}（本试方允许±0.75）。\n炉内残渣 {Slag:F1}；现在排渣预计夹带 {CarryRisk:F2}。\n主药生能力保存 {Capability:P0}：仅为本试方的成品要求，不等同主效药力。\n本试验炉开炉要求≤70；没有全局低温成丹门槛。\n\n试算：剂量50分＋混合20分＋成形20分＋本试方所需能力保存10分－残渣影响。";
}
