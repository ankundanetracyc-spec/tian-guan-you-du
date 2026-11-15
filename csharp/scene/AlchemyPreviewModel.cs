using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>共享整数火候、节点来源账与自动跑炉的预览模型，不写正式行囊。</summary>
public sealed partial class AlchemyPreviewModel
{
    public enum Hand { Hold,Raise,Lower,Circulate,Separate,Purge,Gather,Output,Fine,SlowFeed,PreciseSeparate,Quench,Care,Draw,Loop,Divide,Master }
    public static readonly string[] HandNames=["守炉","添焰","敛焰","搅和","分离","排渣","凝丹","出炉","分流御药","分批缓投","精细分离","断络止乱","照料","牵药","回流照管","分丹持形","百络精御"];
    public static readonly Dictionary<string,string> Purposes=new(){["破"]="打开未开结构",["化"]="炼化或解除滞留",["通"]="建通路并缓慢导流",["行"]="为既有通路增流",["聚"]="收拢已到工位的药性",["引"]="分配出口药流",["固"]="稳住已有形状",["镇"]="压低局部躁动",["护"]="抵挡真实损伤",["生"]="修复可恢复损伤",["明"]="诊断或精细分离",["藏"]="封存易散药性"};
    public sealed record Herb(string Name,ItemData Item,MaterialCraftData Craft)
    {
        public bool CanBeMainFor(string id)=>Grade(id)>0;
        public bool CanBeMain=>CanBeMainFor("生");
        public int Grade(string id)=>Craft.utilities.Where(u=>u.utility_id==id).Select(u=>Math.Clamp(u.grade,1,9)).DefaultIfEmpty(0).Max();
        public double Power(string id)=>AlchemyProcessConfig.Strength[Grade(id)];
        public AlchemyProcessConfig.Processing Processing=>AlchemyProcessConfig.Profile(Craft);
    }
    public sealed class Dose(Herb herb,string kind,int batch)
    {
        public Herb Herb=herb; public string Kind=kind; public int Batch=batch,Id;
        public double Original=100,Raw=100,Available,Slag,Bound,Allocated,Spent,Destroyed,Escaped,Expelled,Open,Disorder,Separation,MaxPrecision,Mixed,Extracted,Sealed,Agitation,Diagnosis;
        public readonly Dictionary<string,double> BoundCapabilities=[];
        public double Processed=>Original-Raw;
        public double Released=>Extracted/Math.Max(.000001,Original);
        public double Mass=>Raw+Available+Slag+Bound+Allocated;
        public double Balance=>Mass+Destroyed+Escaped+Expelled;
        public double Readiness(string id)=>Herb.Grade(id)==0?0:Math.Min(1,MaxPrecision/AlchemyProcessConfig.Precision[Herb.Grade(id)])*(1-Math.Clamp(Disorder,0,1));
        public double Ability(string id)=>Herb.Power(id)*Readiness(id)*Available;
    }
    public sealed class SignedLink(int sourceDoseId,int targetDoseId,string utilityId)
    {
        public int SourceDoseId=sourceDoseId,TargetDoseId=targetDoseId;
        public string UtilityId=utilityId,Status="尚未安排施用";
        public bool Paused,Built; public double Capacity,LastFlow,BuildProgress; public int BuiltBreath;
        public SignedLink? RouteTarget;
        public bool FlowOpen=true,Repeat; public double TargetAmount=double.PositiveInfinity,Transported,BranchWeight=1;
        public readonly Dictionary<SignedLink,double> BranchWeights=[];
        public Hand? Technique;
        public readonly List<Part> Structures=[];
    }
    public sealed record Operation(Hand Kind,int TargetId=0,int SourceId=0);
    public sealed record ComplexGoal(string Name,string ReturnReason,double MinimumMix=.8,double MaximumDamage=.02);
    public sealed record TextureRule(double OpenHeat,double SafeHeat,double Rate);
    // 旧原型检查保留显式兼容入口；新演算、界面均使用 Herb.Processing。
    public static TextureRule Profile(string legacy)
    {
        var rules=legacy.Split(new[]{'、',',','/'},StringSplitOptions.RemoveEmptyEntries).Select(s=>AlchemyProcessConfig.TestProfiles[s.Trim()]).ToArray();
        return new(rules.Max(p=>p.Open),rules.Min(p=>p.Safe),rules.Min(p=>p.Rate));
    }
    public sealed record Forecast(double Spirit,double Capacity,int Mana,double Temperature,double NextTemperature,double UpperSpirit,double Precision)
    {
        public double Ratio=>Spirit/Math.Max(.0001,Capacity);
        public double UpperRatio=>UpperSpirit/Math.Max(.0001,Capacity);
        public double Sustained { get; init; }
        public double Temporary=>Spirit-Sustained;
    }
    public sealed class Part
    {
        public Dose Source; public double Amount,Slag,Mix,Damage,Sealed,Stuck,Clustered; public bool Structure,Worked,Returned;
        public int Location,ReadyBreath; public readonly HashSet<int> Route=[];
        public readonly Dictionary<string,double> Capability=[];
        public readonly Dictionary<string,double> StructuralSupport=[];
        public Part(Dose source,double amount,bool structure=false)
        {
            Source=source;Amount=amount;Structure=structure;Location=source.Id;Mix=source.Mixed;Damage=source.Disorder;Route.Add(source.Id);
            foreach(var u in source.Herb.Craft.utilities.DistinctBy(u=>u.utility_id))Capability[u.utility_id]=source.Herb.Power(u.utility_id)*Math.Min(1,source.MaxPrecision/AlchemyProcessConfig.Precision[u.grade]);
        }
        public Part Split(double amount,int location,int ready)
        {
            var p=new Part(Source,amount,Structure){Location=location,ReadyBreath=ready,Mix=Mix,Damage=Damage,Sealed=Sealed,Worked=Worked,Returned=Returned,Clustered=Clustered};
            p.Capability.Clear();foreach(var (key,value) in Capability)p.Capability[key]=value;
            foreach(var (key,value) in StructuralSupport)p.StructuralSupport[key]=value;
            p.Route.Clear();p.Route.UnionWith(Route);return p;
        }
    }
    public sealed class Pill
    {
        public int Index,Score,MainQuality,BlendQuality,BodyQuality;public double Form;public string Reason="",NextGap="";
        public readonly List<Part> Parts=[];
        public string PillTypeId="huichun",FormulaId="qingmu_standard",MainHerbName="青木芯",CoreUtilityId="生";
        public double CoreReferencePower=1,PolarityTarget,PolarityToleranceAt20=.72,PolarityToleranceAt100=.20;
        public double Amount=>Parts.Sum(p=>p.Amount);
        public double SlagAmount=>Parts.Sum(p=>p.Slag);
        public double MainAmount=>Parts.Where(p=>!p.Structure&&p.Source.Kind=="主药"&&p.Capability.GetValueOrDefault(CoreUtilityId)>0).Sum(p=>p.Amount);
        public double CoreCapability=>Parts.Where(p=>!p.Structure&&p.Source.Kind=="主药").Sum(p=>p.Amount*p.Capability.GetValueOrDefault(CoreUtilityId)*(1-p.Damage));
        public double SupportingCapability(string id)=>Parts.Sum(p=>p.Amount*Math.Max(p.Capability.GetValueOrDefault(id),p.StructuralSupport.GetValueOrDefault(id))*(1-p.Damage));
        public double Mix=>Amount<1e-9?0:Parts.Sum(p=>p.Amount*p.Mix)/Amount;
        public double Polarity=>Amount<1e-9?0:Parts.Sum(p=>p.Amount*Math.Sign(p.Source.Herb.Craft.polarity))/Amount;
        public double Damage=>Amount<1e-9?0:Parts.Sum(p=>p.Amount*p.Damage)/Amount;
        public double PolarityTolerance(int q)=>PolarityToleranceAt20+(PolarityToleranceAt100-PolarityToleranceAt20)*Math.Clamp((q-20)/80d,0,1);
        public string DefinitionKey=>$"trial_{PillTypeId}_{MainHerbName}_q{Score:D3}";
        public string QualityText=>$"丹效{Score}分 · {Tier(Score)}";
    }
    public readonly List<Herb> Herbs=[]; public readonly List<Dose> Pending=[],InFurnace=[];
    public readonly List<string> Log=[],Changes=[];public readonly Dictionary<string,string> Contributions=[];
    public readonly List<Pill> Pills=[];public readonly List<SignedLink> SignedLinks=[];
    public readonly List<Operation> PendingOperations=[];public readonly HashSet<SignedLink> PlannedLinks=[];
    public readonly HashSet<Operation> RepeatingOperations=[];
    public readonly HashSet<Hand> Learned=[],HelperLearned=[];
    public readonly HashSet<string> LibraryBooks=AlchemyProcessConfig.Tools.Values.Where(t=>t.Book.Length>0).Select(t=>t.Book).ToHashSet();
    public readonly List<Part> Inventory=[];
    private readonly List<Part> _structures=[];
    private readonly HashSet<int> _seeded=[];
    private readonly HashSet<int> _shocked=[];
    private readonly Dictionary<int,double> _shield=[];
    private bool _activeHelper,_emergencyUsed,_coreCreated;
    private int _nextId=1,_executingBreath;
    private double _heat;
    public int Breath,Mana=AlchemyProcessConfig.MainMana,HelperMana=AlchemyProcessConfig.HelperMana,Potions=2,Attainment=36,Wanted=3;
    public double Heat { get=>_heat; set=>_heat=Math.Max(0,(int)value); }
    public double Temperature { get=>Heat; set=>Heat=value; } // 旧场景 API 的同一档位别名，无第二份温度。
    public double Connection,Fusion,Agitation,LastSupply,StudyHours;
    public Hand Action=Hand.Hold;public bool Done,Helper,Recover,TakePotion,EmergencyArmed;
    public string SelectedPillId="huichun",SelectedFormulaId="qingmu_standard",SelectedMainHerbName="青木芯";
    public ComplexGoal? RefiningGoal;
    public string MainName=>SelectedMainHerbName;
    public AlchemyRecipeCatalog.PillType CurrentPill=>AlchemyRecipeCatalog.GetPill(SelectedPillId);
    public AlchemyRecipeCatalog.Formula CurrentFormula=>AlchemyRecipeCatalog.GetFormula(SelectedPillId,SelectedFormulaId);
    public string Explanation="拖入药材并在节点网上预设功用线；布置完成后开炉自动运行。",Result="",LastFeedback="";
    public bool CanChangeRecipe=>!AutoRunStarted&&Breath==0&&Pending.Count==0&&!Done;
    public int FurnaceIndex,FireIndex,HelperAttainment=AlchemyProcessConfig.HelperAttainment,MainSpirit=AlchemyProcessConfig.MainSpirit,HelperSpirit=AlchemyProcessConfig.HelperSpirit;
    public int OperatorAttainment=>Helper?HelperAttainment:Attainment;
    public bool FineLearned{get=>Learned.Contains(Hand.Fine);set{if(value)Learned.Add(Hand.Fine);else Learned.Remove(Hand.Fine);}}
    public int FeedLimit=>AlchemyProcessConfig.FeedLimit(OperatorAttainment)+(PendingOperations.Any(o=>o.Kind==Hand.SlowFeed)?1:0);
    public int MaxPills=>AlchemyProcessConfig.PillLimit(OperatorAttainment)+(PendingOperations.Any(o=>o.Kind==Hand.Divide)?2:0);
    public double FurnaceCapacity=>FurnaceIndex==0?12:FurnaceIndex==1?8:24;
    public double FurnaceMass=>InFurnace.Sum(d=>d.Mass)/100;
    public int FireLimit=>Math.Min(AlchemyProcessConfig.FireLimit(OperatorAttainment),Math.Min(FurnaceIndex==0?13:FurnaceIndex==1?16:31,FireIndex==0?31:16));
    public int Count=>(int)Math.Floor(InFurnace.Concat(Pending).Where(d=>d.Kind=="主药").Sum(d=>d.Original)/25);
    public double MainAmount=>InFurnace.Where(d=>d.Kind=="主药").Sum(d=>d.Available+d.Allocated);
    public double Available=>InFurnace.Sum(d=>d.Available+d.Bound+d.Allocated);
    public double Slag=>InFurnace.Sum(d=>d.Slag);
    public double ActualFire=>Done?0:Heat;
    public double Polarity=>Inventory.Concat(Pills.SelectMany(p=>p.Parts)).Where(p=>p.Amount>0).Sum(p=>p.Amount*Math.Sign(p.Source.Herb.Craft.polarity))/Math.Max(.000001,Inventory.Sum(p=>p.Amount)+Pills.Sum(p=>p.Amount));
    public double Capability=>InFurnace.Where(d=>d.Kind=="主药").Select(d=>d.Readiness(CurrentPill.CoreUtility)).DefaultIfEmpty(0).Average();
    public double CarryRisk=>InFurnace.Sum(d=>Math.Min(d.Available,d.Slag*.8*(1-d.Separation)*.2));
    public double Shape=>Pills.Select(p=>p.Form).DefaultIfEmpty(0).Average();
    public int ElapsedSeconds=>Breath*AlchemyProcessConfig.SecondsPerBreath;
    public double ElapsedHours=>Breath*AlchemyProcessConfig.HoursPerBreath;
    public string ElapsedText=>$"{Breath/10}时辰{Breath%10}息";
    public bool HasPillCore=>_coreCreated;
    public double HubStability,HubMix;
    public bool HasActualReturn=>Inventory.Any(p=>p.Returned&&p.Worked);
    public double NodeAvailable(int nodeId)=>Inventory.Where(p=>p.Location==nodeId).Sum(p=>p.Amount);
    public double NodeSlag(int nodeId)=>Inventory.Where(p=>p.Location==nodeId).Sum(p=>p.Slag);
    public double NodeAgitation(int nodeId)=>Z(nodeId);
    public double NodeMixed(int nodeId)
    {
        var ps=Inventory.Where(p=>p.Location==nodeId&&p.Amount>0).ToArray();double n=ps.Sum(p=>p.Amount);
        return n>0?ps.Sum(p=>p.Amount*p.Mix)/n:0;
    }
    public string NodeName(int id)=>id==0?(HasPillCore?"丹胚":NodeAvailable(0)>1e-6?"炉内药团":"丹炉口"):InFurnace.FirstOrDefault(d=>d.Id==id)?.Herb.Name??"失效节点";
    public double NodeMass(int id)=>Inventory.Where(p=>p.Location==id).Sum(p=>p.Amount+p.Slag)+_structures.Where(p=>p.Location==id).Sum(p=>p.Amount)+(InFurnace.FirstOrDefault(d=>d.Id==id)?.Raw??0);
    private static List<Herb>? _materialCatalog;
    private static readonly List<ItemData> _catalogResources=[];
    public AlchemyPreviewModel()
    {
        if(_materialCatalog is not null){Herbs.AddRange(_materialCatalog);return;}
        using var directory=DirAccess.Open("res://resources/entities/items");
        if(directory is null)throw new InvalidOperationException("无法读取药材目录");
        foreach(string file in directory.GetFiles().Where(f=>f.EndsWith(".tres",StringComparison.OrdinalIgnoreCase)).OrderBy(f=>f,StringComparer.Ordinal))
        {
            if(file.Contains("骨",StringComparison.Ordinal))continue;
            var item=GD.Load<ItemData>("res://resources/entities/items/"+file);
            if(item is not null)_catalogResources.Add(item);
            if(item?.craft_data is not MaterialCraftData craft)continue;
            AlchemyProcessConfig.Profile(craft);Herbs.Add(new(file[..^5],item,craft));
        }
        foreach(string name in AlchemyRecipeCatalog.MaterialNames)if(Herbs.All(h=>h.Name!=name))throw new InvalidOperationException("丹方材料配置缺失："+name);
        _materialCatalog=Herbs.ToList();
    }
    public bool SelectPill(string id){if(!CanChangeRecipe||!AlchemyRecipeCatalog.Pills.Any(p=>p.Id==id))return false;SelectedPillId=id;SelectedFormulaId=CurrentPill.Formulas[0].Id;SelectedMainHerbName=CurrentPill.MainCandidates[0];RefreshDoseRoles();return true;}
    public bool SelectFormula(string id){if(!CanChangeRecipe||!CurrentPill.Formulas.Any(f=>f.Id==id))return false;SelectedFormulaId=id;SelectedMainHerbName=CurrentFormula.MainHerb;RefreshDoseRoles();return true;}
    public bool SelectMainHerb(string name){if(!CanChangeRecipe||!CurrentPill.MainCandidates.Contains(name))return false;SelectedMainHerbName=name;RefreshDoseRoles();return true;}
    public bool CanUse(Hand h)
    {
        var t=AlchemyProcessConfig.Tools[HandNames[(int)h]];
        return t.Learn==0||(OperatorAttainment>=t.Learn&&(Helper?HelperLearned:Learned).Contains(h));
    }
    public string LearnFine()=>Learn(Hand.Fine);
    public string Learn(Hand hand)
    {
        var t=AlchemyProcessConfig.Tools[HandNames[(int)hand]];
        if(t.Book.Length==0)return "这是基本手段，无需研读。";
        if(!CanChangeRecipe)return "请在开炉和安排投料前研读书籍，中炉不能临时学习。";
        if(!LibraryBooks.Contains(t.Book))return "试验书架没有《"+t.Book+"》。";
        if(Attainment<t.Learn)return $"研读《{t.Book}》掌握技巧需要造诣{t.Learn}。";
        if(Learned.Contains(hand))return "已经掌握，无需重复研读。";
        Learned.Add(hand);StudyHours+=2;
        return $"研读《{t.Book}》一个时辰，掌握{HandNames[(int)hand]}；书籍保留，副手需独立研读。";
    }
    public string QueueHerb(string name,double portions=1)
    {
        if(Done)return "本炉已结束";
        var herb=Herbs.FirstOrDefault(h=>h.Name==name);if(herb is null)return "药材定义不存在";
        if(OperatorAttainment<8)return "九品丹需要当前操作者造诣至少8";
        if(portions<.1||Math.Abs(portions*10-Math.Round(portions*10))>1e-7)return "投料以0.1份为单位";
        if(Pending.Sum(d=>d.Original)/100+portions>FeedLimit+1e-8)return $"本息投料上限{FeedLimit}份，请先减少待投或安排分批缓投";
        if(FurnaceMass+Pending.Sum(d=>d.Original)/100+portions>FurnaceCapacity+1e-8)return "超过丹炉真实容量";
        while(InFurnace.Concat(Pending).Any(d=>d.Id==_nextId))_nextId++;
        Pending.Add(new(herb,name==MainName?"主药":"辅药",Breath+1){Id=_nextId++,Original=portions*100,Raw=portions*100,Open=herb.Processing.Dense?0:1});
        return $"已安排{herb.Name}×{portions:0.#}；选择炉令后入炉，当前息数不变。";
    }
    public string QueueOperation(Hand kind,int targetId=0,int sourceId=0)
    {
        if(Done)return "本炉已结束";
        if(kind is Hand.Hold or Hand.Raise or Hand.Lower)return "炉令用于推进一息。";
        if(kind==Hand.Output){if(Heat>1)return "先敛焰至0—1档再安全出炉。";Finish();return Result;}
        if(!CanUse(kind))return $"尚未从对应书籍掌握{HandNames[(int)kind]}，或当前操作者造诣不足。";
        if(kind==Hand.Quench){EmergencyArmed=!EmergencyArmed;return EmergencyArmed?"已预备一次断络应急。":"已取消应急预备。";}
        var op=new Operation(kind,targetId,sourceId);
        if(PendingOperations.Remove(op)){RepeatingOperations.Remove(op);return "已撤回本息"+HandNames[(int)kind];}
        if(targetId!=0&&!InFurnace.Any(d=>d.Id==targetId))return "目标节点不存在";
        if(kind==Hand.Draw&&(sourceId==targetId||!InFurnace.Any(d=>d.Id==sourceId)))return "请选择不同的实际来源和目标节点";
        PendingOperations.Add(op);return $"已安排{HandNames[(int)kind]}→{NodeName(targetId)}；炉令推进时执行。";
    }
    public string SignLink(int sourceId,int targetId,string utility)
    {
        EnsureInventory();
        if(Done||AutoRunStarted)return "开炉后节点网已锁定；请重新开炉再布置。";
        var source=InFurnace.FirstOrDefault(d=>d.Id==sourceId);
        if(source is null||NodeAvailable(sourceId)<=.01&&source.Raw<=.01)return "起点没有原料或可用药性。";
        if(source.Herb.Grade(utility)<=0||!Purposes.ContainsKey(utility))return "该材料没有这项功用。";
        if(sourceId==targetId)return "起点与目标必须不同。";
        if(targetId==0)
        {
            if(utility is not ("通" or "聚" or "引" or "固" or "镇" or "护" or "生" or "明" or "藏"))return "该功用不能作用于汇合位。";
            if(RefiningGoal is not null&&!HasActualReturn&&!HasPillCore)return "该复杂炼法先完成真实回流加工，再接中心工位。";
        }
        else if(!InFurnace.Any(d=>d.Id==targetId))return "目标节点不存在。";
        if(utility=="行")return "请选择一段已建通路作为行的作用对象。";
        if(SignedLinks.Any(l=>l.SourceDoseId==sourceId&&l.TargetDoseId==targetId&&l.UtilityId==utility))return "该功用线已存在，左键点线可安排施用、暂停或撤线。";
        var link=new SignedLink(sourceId,targetId,utility);SignedLinks.Add(link);PlannedLinks.Add(link);
        return Explanation=$"已签{NodeName(sourceId)}·{utility}→{NodeName(targetId)}，持续占用1灵识；已安排本息施用。";
    }
    public string ToggleLink(SignedLink link)
    {
        if(Done||AutoRunStarted||!SignedLinks.Contains(link)||link.Paused)return "该线当前不可施用。";
        if(!PlannedLinks.Remove(link)){PlannedLinks.Add(link);return "已安排本息施用："+link.UtilityId;}
        return "已取消本息施用，线路仍占1灵识。";
    }
    public string SignFlowSupport(int sourceId,SignedLink route)
    {
        EnsureInventory();
        var source=InFurnace.FirstOrDefault(d=>d.Id==sourceId);
        if(Done||AutoRunStarted||source is null||source.Herb.Grade("行")==0||NodeAvailable(sourceId)<=0&&source.Raw<=0)return "该来源没有行药材或可用行药性。";
        bool availableRoute=route.Built||!AutoRunStarted&&PlannedLinks.Contains(route);
        if(!SignedLinks.Contains(route)||route.UtilityId!="通"||!availableRoute||route.Paused)return "请选择已布置且未暂停的通路。";
        if(SignedLinks.Any(l=>l.SourceDoseId==sourceId&&l.UtilityId=="行"&&l.RouteTarget==route))return "已对该路线签行。";
        var link=new SignedLink(sourceId,route.TargetDoseId,"行"){RouteTarget=route};SignedLinks.Add(link);PlannedLinks.Add(link);
        return "已签行→"+NodeName(route.SourceDoseId)+"至"+NodeName(route.TargetDoseId)+"；开炉后通路建成时自动增流。";
    }
    public string PauseLink(SignedLink link)
    {
        if(Done||AutoRunStarted)return "开炉后节点网已锁定";link.Paused=!link.Paused;PlannedLinks.Remove(link);
        if(link.Paused)foreach(var dependent in SignedLinks.Where(l=>l.RouteTarget==link)){dependent.Paused=true;PlannedLinks.Remove(dependent);}
        return link.Paused?"已暂停线路，释放1灵识，保留已建结构。":"已恢复线路，重新占1灵识；施用需另行安排。";
    }
    public string RemoveSignedLink(SignedLink link)
    {
        if(Done||AutoRunStarted||!SignedLinks.Remove(link))return "线路不可撤回";
        PlannedLinks.Remove(link);
        foreach(var dependent in SignedLinks.Where(l=>l.RouteTarget==link)){dependent.Paused=true;PlannedLinks.Remove(dependent);}
        foreach(var p in link.Structures.ToArray()){p.Source.Bound-=p.Amount;p.Source.Slag+=p.Amount;AddResidue(p.Source,p.Amount,p.Location);_structures.Remove(p);}
        link.Structures.Clear();
        return Explanation="已撤回"+link.UtilityId+"线，释放1灵识；已用于建线的药性成为残余，不返还为自由药性。";
    }
    public HashSet<int> ClosedLoopDoseIds()
    {
        var ids=new HashSet<int>();
        foreach(var l in Routes(false))if(HasDirectedPassage(l.TargetDoseId,l.SourceDoseId)){ids.Add(l.SourceDoseId);ids.Add(l.TargetDoseId);}
        return ids;
    }
    public bool IsClosedLoopLink(SignedLink l)=>l.UtilityId=="通"&&l.Built&&!l.Paused&&HasDirectedPassage(l.TargetDoseId,l.SourceDoseId);
    private IEnumerable<SignedLink> Routes(bool planning)=>SignedLinks.Where(l=>l.UtilityId=="通"&&!l.Paused&&(l.Built||(planning&&PlannedLinks.Contains(l))));
    public bool HasDirectedPassage(int source,int target)=>HasDirectedPassage(source,target,false);
    public bool HasDirectedPassage(int source,int target,bool planning)
    {
        if(source==target)return false;var seen=new HashSet<int>();var q=new Queue<int>();q.Enqueue(source);
        while(q.Count>0){int id=q.Dequeue();if(!seen.Add(id))continue;foreach(var l in Routes(planning).Where(l=>l.SourceDoseId==id)){if(l.TargetDoseId==target)return true;q.Enqueue(l.TargetDoseId);}}
        return false;
    }
    public double SustainedLoad=>AutoRunStarted?PlannedLinks.Count(l=>!l.Paused):SignedLinks.Count(l=>!l.Paused)+Math.Min(4,Agitation/25);
    private double LinkInput(SignedLink l)=>l.UtilityId=="通"&&l.Built?0:Math.Min(AlchemyProcessConfig.InputLimit(OperatorAttainment),Inventory.Where(p=>p.Location==l.SourceDoseId&&p.ReadyBreath<=Breath+1).Sum(p=>Math.Max(0,p.Amount-p.Stuck)));
    public double OperationLoad(Operation o)
    {
        double mass=NodeMass(o.TargetId);
        return o.Kind switch
        {
            Hand.SlowFeed=>.5,Hand.Separate=>2+.5*Math.Min(mass,FeedLimit*100)/100,Hand.PreciseSeparate=>3.5+.5*Math.Min(mass,FeedLimit*100)/100,
            Hand.Purge=>1+.25*Math.Min(NodeSlag(o.TargetId)*.8,FeedLimit*100)/100,
            Hand.Gather=>1+Wanted,Hand.Divide=>3+Wanted,Hand.Fine=>3,Hand.Loop=>4,Hand.Master=>6,_=>2
        };
    }
    public Forecast Preview()
    {
        EnsureInventory();
        if(_automaticRunActive)
        {
            int autoCapacity=Helper?HelperSpirit:MainSpirit;
            double active=PlannedLinks.Count(l=>!l.Paused&&SignedLinks.Contains(l));
            return new(active,autoCapacity,AlchemyProcessConfig.CommandMana((int)Heat,Action,FireIndex),Heat,
                Math.Max(0,Heat+(Action==Hand.Raise?1:Action==Hand.Lower?-1:0)),active,PrecisionFor(Hand.Care,0)) { Sustained=active };
        }
        foreach(var l in SignedLinks.Where(l=>l.Repeat&&!l.Paused))PlannedLinks.Add(l);
        foreach(var op in RepeatingOperations)if(!PendingOperations.Contains(op))PendingOperations.Add(op);
        int next=(int)Heat+(Action==Hand.Raise?1:Action==Hand.Lower?-1:0);
        double feed=Pending.Count==0?0:1+Pending.Sum(d=>d.Original)/100+.25*(Pending.Count-1);
        if(PendingOperations.Any(o=>o.Kind==Hand.SlowFeed))feed*=.8;
        double temporary=1+feed+PendingOperations.Sum(OperationLoad)+(TakePotion?1:0)+(Helper!=_activeHelper?2:0);
        foreach(var l in PlannedLinks.Where(l=>!l.Paused&&SignedLinks.Contains(l)))if(!(l.UtilityId=="通"&&l.Built))temporary+=1+.5*LinkInput(l)+(l.Technique is {} tech?AlchemyProcessConfig.Tools[HandNames[(int)tech]].Load:0);
        double sustained=SustainedLoad,spirit=sustained+temporary,cap=Helper?HelperSpirit:MainSpirit;
        double precision=PrecisionFor(Hand.Care,spirit/cap);
        return new(spirit,cap,AlchemyProcessConfig.CommandMana((int)Heat,Action,FireIndex),Heat,next,spirit,precision){Sustained=sustained};
    }
    private double PrecisionFor(Hand hand,double ratio)
    {
        var t=AlchemyProcessConfig.Tools[HandNames[(int)hand]];
        double support=FurnaceIndex==1?1.10:FurnaceIndex==2?1.15:1;
        double spiritFactor=_automaticRunActive?1:ratio<=.8?1:Math.Max(.7,1-1.5*(Math.Min(1,ratio)-.8));
        return Math.Min(t.PrecisionCap,(AlchemyProcessConfig.BasePrecision(OperatorAttainment)+t.Bonus)*support)*spiritFactor;
    }
    private void Note(string text){Changes.Add(text);Log.Add($"第{Breath}息 · {text}");}
    private string Validate()
    {
        if(Done)return "本炉已结束";
        if(Action is not (Hand.Hold or Hand.Raise or Hand.Lower))return "先安排操作，再选择添焰、敛焰或守炉推进。";
        if(OperatorAttainment<8)return "当前操作者造诣不足8";
        if(Action==Hand.Lower&&Heat<=0)return "当前无火，不能再敛焰。";
        if(Action==Hand.Raise&&Heat>=FireLimit)return $"当前最高可控火候为{FireLimit}档。";
        if(Heat>FireLimit&&Action!=Hand.Lower)return "当前操作者或器具不能驾驭此档，请先敛焰或接替。";
        if(Pending.Sum(d=>d.Original)/100>FeedLimit+1e-8)return "待投超过本息投料上限";
        if(FurnaceMass+Pending.Sum(d=>d.Original)/100>FurnaceCapacity+1e-8)return "炉内质量超过容量";
        if(PendingOperations.Any(o=>!CanUse(o.Kind)))return "当前操作者没有本息所需书籍技巧";
        if(PlannedLinks.Any(l=>l.Technique is {} h&&!CanUse(h)))return "当前操作者没有线路所选技巧";
        if(Recover&&!Helper)return "本人回气需副手实际掌炉";
        if(TakePotion&&Potions<=0)return "测试恢复药已用完";
        if(PendingOperations.Any(o=>o.Kind is Hand.Gather or Hand.Divide)&&(Wanted<1||Wanted>Math.Min(MaxPills,Count)))return $"当前最多同时凝{Math.Min(MaxPills,Count)}颗";
        if(PendingOperations.Any(o=>o.Kind is Hand.Gather or Hand.Divide)&&(Heat+(Action==Hand.Raise?1:Action==Hand.Lower?-1:0) is <1 or >3))return "九品丹需在1—3档塑形";
        if(RefiningGoal is not null&&!HasPillCore&&PendingOperations.Any(o=>o.Kind is Hand.Gather or Hand.Divide))return "复杂炼法尚未完成回流与中心聚合";
        int mp=Helper?HelperMana:Mana;if(TakePotion)mp=Math.Min(Helper?AlchemyProcessConfig.HelperMana:AlchemyProcessConfig.MainMana,mp+AlchemyProcessConfig.PotionRestore);
        int fee=Preview().Mana;if(mp<fee)return $"本息需{fee}法力，安排恢复后仍只有{mp}；请恢复、接替或结束本炉。";
        return "";
    }
    public string Advance()
    {
        EnsureInventory();
        if(Action==Hand.Output){if(Heat>1)return "先敛焰至0—1档安全出炉";Finish();return "";}
        string error=Validate();if(error.Length>0)return error;
        var forecast=Preview();double before=Heat;Changes.Clear();Contributions.Clear();_shield.Clear();_shocked.Clear();
        Breath++;_executingBreath=Breath;
        if(TakePotion){if(Helper)HelperMana=Math.Min(AlchemyProcessConfig.HelperMana,HelperMana+AlchemyProcessConfig.PotionRestore);else Mana=Math.Min(AlchemyProcessConfig.MainMana,Mana+AlchemyProcessConfig.PotionRestore);Potions--;TakePotion=false;Note("服测试恢复药，恢复60法力");}
        if(Recover){Mana=Math.Min(AlchemyProcessConfig.MainMana,Mana+AlchemyProcessConfig.RecoverRestore);Note("副手掌炉，本人回气30法力");}
        foreach(var d in Pending){InFurnace.Add(d);_seeded.Add(d.Id);Note($"投入{d.Herb.Name}×{d.Original/100:0.#}");}Pending.Clear();
        if(Helper)HelperMana-=forecast.Mana;else Mana-=forecast.Mana;
        Heat=forecast.NextTemperature;LastSupply=Heat>0?1:0;_activeHelper=Helper;
        if(!_automaticRunActive&&!ControlPeak(forecast.Spirit))return "";
        ProcessRaw(forecast.Ratio);
        ExecuteNodePlan(forecast.Ratio);
        ExecuteBasicPlan(forecast.Ratio);
        DamageAndPreserve(forecast.Ratio);
        UpdateMetrics();
        RetireExhaustedNodes();
        double finalPeak=forecast.Spirit-forecast.Sustained+SustainedLoad;
        if(!_automaticRunActive&&finalPeak>forecast.Capacity&&!ControlPeak(Math.Max(forecast.Spirit,finalPeak)))return "";
        Note($"{HandNames[(int)Action]}：火候{before:F0}→{Heat:F0}档，支付{forecast.Mana}法力；{(_automaticRunActive?"并行线路":"灵识峰值")} {Math.Max(forecast.Spirit,finalPeak):F2}/{forecast.Capacity:F0}；累计{ElapsedText}（{ElapsedHours:0.0}小时）");
        LastFeedback=string.Join("\n",Changes.TakeLast(7));
        PlannedLinks.Clear();PendingOperations.Clear();
        AssertConservation();return "";
    }
    private bool ControlPeak(double peak)
    {
        int cap=Helper?HelperSpirit:MainSpirit;if(peak<=cap+1e-9)return true;
        if(EmergencyArmed&&!_emergencyUsed&&CanUse(Hand.Quench)&&(Helper?HelperMana:Mana)>=20)
        {
            if(Helper)HelperMana-=20;else Mana-=20;_emergencyUsed=true;EmergencyArmed=false;
            var candidates=SignedLinks.Where(l=>!l.Paused).TakeLast(4).ToArray();
            double reduction=0;foreach(var l in candidates){reduction+=1+(PlannedLinks.Contains(l)&&!(l.UtilityId=="通"&&l.Built)?1+.5*LinkInput(l)+(l.Technique is {} t?AlchemyProcessConfig.Tools[HandNames[(int)t]].Load:0):0);l.Paused=true;PlannedLinks.Remove(l);}
            if(PendingOperations.Count>0){reduction+=OperationLoad(PendingOperations[^1]);PendingOperations.RemoveAt(PendingOperations.Count-1);}
            Note($"断络止乱：付20法力，暂停{candidates.Length}线并放弃尚未执行操作；不返还炉令耗费");
            if(peak-reduction<=cap+1e-9)return true;
        }
        Done=true;
        foreach(var d in InFurnace){d.Destroyed+=d.Mass;d.Raw=d.Available=d.Slag=d.Bound=d.Allocated=0;}
        Inventory.Clear();_structures.Clear();Pills.Clear();PlannedLinks.Clear();PendingOperations.Clear();
        Result=$"炸炉：灵识峰值{peak:F2}/{cap}，没有能将负担降回可控范围的应急。";Note(Result);LastFeedback=Result;return false;
    }
    public void AssertConservation()
    {
        foreach(var d in InFurnace)
        {
            double available=Inventory.Where(p=>p.Source==d).Sum(p=>p.Amount);
            double slag=Inventory.Where(p=>p.Source==d).Sum(p=>p.Slag);
            double bound=_structures.Where(p=>p.Source==d).Sum(p=>p.Amount);
            double allocated=Pills.SelectMany(p=>p.Parts).Where(p=>p.Source==d).Sum(p=>p.Amount+p.Slag);
            if(Math.Abs(d.Balance-d.Original)>1e-6||Math.Abs(available-d.Available)>1e-6||Math.Abs(slag-d.Slag)>1e-6||Math.Abs(bound-d.Bound)>1e-6||Math.Abs(allocated-d.Allocated)>1e-6)
                throw new InvalidOperationException($"来源账不守恒：{d.Herb.Name} balance={d.Balance:F8}/{d.Original}, free={available:F8}/{d.Available:F8}, slag={slag:F8}/{d.Slag:F8}, bound={bound:F8}/{d.Bound:F8}, pill={allocated:F8}/{d.Allocated:F8}");
        }
    }
}
