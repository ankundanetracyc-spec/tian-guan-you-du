using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>当前槽位配伍的成品识别模板与可载入投料样例；旧 Formula 数据仅供兼容预览。</summary>
public static class AlchemyRecipeCatalog
{
    // Legacy process notes retained for the unused step-by-step preview model.
    public sealed record Ingredient(string HerbName, int Recommended, string Timing);
    public sealed record Formula(string Id, string Name, string MainHerb, string Comparison,
        string[] FeedOrder, string[] Operations, Ingredient[] Ingredients)
    {
        public Ingredient Main => Ingredients.Single(i => i.HerbName == MainHerb);
        public IEnumerable<Ingredient> Assistants => Ingredients.Where(i => i.HerbName != MainHerb);
    }
    /// <param name="AssistantSlot">-1 Jun; 0 Chen; 1/2/4 Zuo; 3/5/6 Shi. Expansion slots require attainment.</param>
    public sealed record SampleIngredient(string HerbName, int Dose, int AssistantSlot);
    public sealed record SampleRoute(string Name, string Direction, SampleIngredient[] Ingredients);
    // Legacy quality requirements; AlchemySlotSimulation does not read these as recipe gates.
    public sealed record SupportingRequirement(string Utility,int StartsAbove,double At100,string Purpose)
    {
        public double Need(int score)=>score<=StartsAbove?0:At100*Math.Pow((score-StartsAbove)/(100d-StartsAbove),1.15);
    }
    public static SupportingRequirement[] SupportingRequirements(string id)=>id switch
    {
        "huichun"=>[new("固",50,3,"使生机药力稳住丹体"),new("化",65,4,"使药力化合、便于承接"),new("藏",75,3,"使药性收敛并持效")],
        "tongmai"=>[new("固",50,3,"维持稳定的通脉药力"),new("行",65,4,"使疏通后的药力运行"),new("护",75,3,"避免强通伤及经脉")],
        "jingxin"=>[new("固",50,3,"维持丹体与药力"),new("藏",65,4,"使镇摄药力收敛"),new("明",75,3,"使安神兼具清明")],
        _=>[]
    };
    public sealed record PillType(string Id, string Name, int Rank, string CoreUtility,
        string[] MainCandidates, double PolarityTarget, double PolarityToleranceAt20, double PolarityToleranceAt100,
        string Purpose, string[] ExplorationDirections, SampleRoute[] SampleRoutes,
        Formula[] Formulas)
    {
        public double PolarityTolerance(int score)
        {
            double t = Math.Clamp((score - 20) / 80d, 0, 1);
            return PolarityToleranceAt20 + (PolarityToleranceAt100 - PolarityToleranceAt20) * t;
        }
    }

    private static Ingredient I(string name, string timing, int recommended = 1) => new(name, recommended, timing);
    private static SampleIngredient S(string herb, int dose, int assistantSlot) => new(herb, dose, assistantSlot);

    public static readonly PillType[] Pills =
    [
        new("huichun", "九品回春丹", 9, "生", ["青木芯", "青髓芽"], 0, .72, .20,
            "以生机药性修复可恢复的伤损；成丹偏向以阴阳相对平和为宜。",
            ["聚：提高拟成颗数，换取单颗药力分摊", "引／行：把药力集中送入君药主效", "破／化／明：分别处理致密残余、全炉残余与夹带纯度", "调配木、水及阴阳，让材料组成贴近丹方目标"],
            [
                new("赤核平衡方", "采用高品核心，配液质转化与藏性稳形，阴阳和五行共同调和。", [S("赤瓤木核", 1, -1), S("伏地兽筋", 3, 3), S("凝霜叶", 2, 1), S("灵泉露", 1, 2), S("雾铃花", 1, 0)]),
                new("藤心通路方", "青岚藤心作君，溪纹藻丝由使位通路，佐位稳形与识残。", [S("青岚藤心", 3, -1), S("清音玉片", 1, 1), S("溪纹藻丝", 2, 3), S("山蚕长丝", 1, 2), S("朱阳花籽", 1, 0)]),
                new("青芽多味方", "青髓芽作君，臣位固形、佐位化残、使位通藏，尝试以分工和材料互补搭配。", [S("青髓芽", 2, -1), S("灵石", 2, 3), S("回泉珠", 2, 1), S("凝露松脂", 1, 0)]),
                new("青木清露方", "木性君药与水性辅药配伍，佐化转残、使通相合。", [S("青木芯", 2, -1), S("山蚕长丝", 1, 0), S("灵泉露", 2, 1), S("溪纹藻丝", 2, 3)]),
                new("青芽明净方", "青芽作君，以佐明降低夹带，保留四味分工。", [S("青髓芽", 2, -1), S("山蚕长丝", 1, 0), S("清音玉片", 2, 1), S("溪纹藻丝", 2, 3)])
            ],
            [
                new("qingmu_standard", "青木回春常方", "青木芯", "青木芯的生机足，通可建立药性联系；阴阳相对的辅材有助于把药团拉回平和。",
                    ["青木芯","灵泉露","凝露松脂","苦苔根"],
                    ["木质主药先在自身开炼线内缓炼，观察生能力何时真正可用。", "灵泉露易在低温炼开，可先取得液质药性，避免为了它把炉温推高。", "凝露松脂与苦苔根分息加入，按炉况接近平和；未连通时别急着搅动。", "通路建立后再搅动药性，分离时看实际药渣与夹带。"],
                    [I("青木芯","先投主材；缓炼至木质开始炼开，再按实际药性决定是否继续供火。"), I("灵泉露","液质较易炼开；较低炉温即可开始取得药性。"), I("凝露松脂","阳性材料；与阴性材料错息投放，边看阴阳边调整。"), I("苦苔根","阴性材料；加入后观察整体偏向及连通，再决定何时搅动。")]),
                new("sprout_gentle", "青髓芽温养方", "青髓芽", "青髓芽的生能力品级较低，炼出较容易；用量和控制仍会限制单颗药效，适合较平稳地练习。",
                    ["青髓芽","回泉珠","砾地菌伞","凝霜叶"],
                    ["青髓芽先炼，低品生较易取得；若追求高丹效，应比较增加主材与减产保效的代价。", "回泉珠在低炉温下先炼，减轻木质材料的等待时间。", "砾地菌伞与凝霜叶不作为必需辅材；按它们的阴阳、功用和耐热线决定是否采用。", "最终以回春丹平和的阴阳范围判定，缺少这几味不会直接判失败。"],
                    [I("青髓芽","投为主材；九品生较易炼出，但每份核心能力较弱。"), I("回泉珠","液质较易炼；需要聚、化的实际对象出现时才会消耗其功用。"), I("砾地菌伞","按木质开炼线安排；它只是建议辅材，可替换。"), I("凝霜叶","阴性可帮助调节阴阳；不足或过量都由成品实测判定。")])
            ]),
        new("tongmai", "九品通脉丹", 9, "通", ["青木芯", "溪纹藻丝"], 0, .68, .16,
            "以通路药性改善经脉运行；成丹要求药性联系充分，并将阴阳维持在窄范围。",
            ["通／行：增强多味配伍的相合与送达", "引：集中君药核心，减少辅材对主效的稀释", "聚：提高颗数，但会分摊每颗通脉药力", "以水木相生或材料精简路线，分别追求药性发挥与灵识节省"],
            [
                new("藻丝多味方", "溪纹藻丝作君，臣佐使各尽其位，兼顾通路、稳形与阴阳。", [S("溪纹藻丝", 3, -1), S("荒藤结节", 1, 0), S("风络藤丝", 2, 3), S("清音玉片", 1, 1)]),
                new("兽筋木水方", "用兽筋作君，配合木性辅药与使位通路，观察多味材料如何互补。", [S("伏地兽筋", 1, -1), S("兽皮", 1, 0), S("回泉珠", 2, 1), S("青木芯", 3, 3), S("瘴泥珠", 1, 2)]),
                new("石英清润方", "风纹石英提供通脉核心，佐位的化与固分别处理残余和稳形，使位通性改善送达。", [S("风纹石英", 4, -1), S("清音玉片", 1, 3), S("灵泉露", 2, 1), S("石蜂蜡", 1, 2)]),
                new("石英泉珠方", "石英作君，佐化转残，使通连合，比较金水配伍。", [S("风纹石英", 3, -1), S("凝露松脂", 1, 0), S("回泉珠", 2, 1), S("溪纹藻丝", 2, 3)]),
                new("藻丝清露方", "藻丝作君，臣固稳形，佐化与使通各尽其位。", [S("溪纹藻丝", 3, -1), S("山蚕长丝", 1, 0), S("灵泉露", 2, 1), S("风络藤丝", 2, 3)])
            ],
            [
                new("qingmu_channels", "青木石英通脉方", "青木芯", "两种通性来源可以各自提供联系；晶材炼化较慢，耐热的安排比固定堆料更重要。",
                    ["灵泉露","青木芯","风纹石英","雾铃花"],
                    ["先让灵泉露在低温炼开，再投入青木芯建立第一段真实联系。", "风纹石英到自身开炼线后才会持续炼化；用加火与减火在晶材速度和木材耐热线之间取舍。", "待两批通性来源已经连通，再搅动推动交换；雾铃花不是通脉丹的必需材料。", "成品要同时看实际混匀和更窄的阴阳范围，光有高通性不能补阴阳失衡。"],
                    [I("青木芯","作为主材；通的实际能力建立药材之间的联系。"), I("灵泉露","较早炼出低开炼线的液质药性。"), I("风纹石英","晶质开炼线高、炼化慢；达到开炼线后仍需留意青木芯的耐热线。"), I("雾铃花","仅作引的候选辅材；缺少分配缺口时，引不会额外生效。")]),
                new("stream_fiber", "溪纹藻丝行气方", "溪纹藻丝", "溪纹藻丝带通八品；青羽翎耐热较低、清音玉片开炼较慢，可用错开投料和火候处理不同质地。",
                    ["溪纹藻丝","灵泉藻","青羽翎","清音玉片","凝露松脂"],
                    ["先投木质溪纹藻丝建立通路，再按连通情况加入其他药性。", "青羽翎耐热线偏低；若采用，应在升温前投，或在之后减火等余热回落。", "清音玉片开炼较慢；先处理低温材料，再把炉温逐步引到玉质开炼线。", "凝露松脂用于抵消青羽翎带来的阴性，使整体靠近通脉丹的平和目标；也可换其他阳性辅材。", "这是错开耐热线的建议路线，不是四味材料清单；阴阳偏差和真实混匀才决定结果。"],
                    [I("溪纹藻丝","投为主材；木质开炼线较低，先观察通性何时建立联系。"), I("灵泉藻","聚只在有药性汇集需求时发挥；不是必需辅材。"), I("青羽翎","布质耐热线较低；采用时应较早投或先减火。"), I("清音玉片","玉质需要较高炉温；明只在分离药性和药渣时发挥。"), I("凝露松脂","阳性调和候选；建议用量看最终阴阳，不是固定配伍。")])
            ]),
        new("jingxin", "九品静心丹", 9, "镇", ["雾心菌核", "泥脉石团"], -.50, .68, .16,
            "以镇性药力压低实际躁动；成丹偏阴，阴阳与过程中的冲撞都要兼顾。",
            ["自然偏阴配伍：让材料组成直接贴近丹方偏性", "镇：允许混合阴阳，但降低相激损耗", "固／藏：稳住丹体；明／化：分别通过识残与转化争取纯度", "护：为高冲突路线兜底，但不能弥补混合损耗"],
            [
                new("瘴泥偏阴方", "瘴泥珠作君，材料天然偏阴；佐位明／藏处理纯度与稳形，五行材料互补。", [S("瘴泥珠", 2, -1), S("风纹石英", 3, 3), S("灵泉藻", 1, 0), S("清音玉片", 1, 1), S("凝霜叶", 1, 2)]),
                new("泥脉木水方", "君臣佐使各一味：泥脉石团作君，山蚕长丝在臣位固形，回泉珠在佐位化合，青木芯在使位相合。", [S("泥脉石团", 3, -1), S("山蚕长丝", 2, 0), S("青木芯", 1, 3), S("回泉珠", 2, 1)]),
                new("菌核阴衡方", "雾心菌核作君，以佐位化残、藏性稳形和使位通路配伍，调整剂量可改走不同偏性。", [S("雾心菌核", 1, -1), S("灵泉露", 3, 1), S("风络藤丝", 1, 3), S("沉云玉屑", 2, 2), S("玄甲薄片", 1, 0)]),
                new("瘴泥清音方", "偏阴君药搭配和性辅药，佐明提纯，使通相合。", [S("瘴泥珠", 2, -1), S("山蚕长丝", 1, 0), S("清音玉片", 2, 1), S("溪纹藻丝", 2, 3)]),
                new("泥脉月露方", "阴性石材与菌汁配伍，以佐化处理残余。", [S("泥脉石团", 2, -1), S("山蚕长丝", 1, 0), S("月露菌汁", 2, 1), S("风纹石英", 2, 3)])
            ],
            [
                new("mistheart_calm", "雾心菌核静心方", "雾心菌核", "利用少量阳性辅材主动形成轻微阴阳接触；先建联系、再让镇处理真实躁动，是本方的精细操作。",
                    ["月露菌汁","雾心菌核","凝霜叶","凝露松脂","清音玉片"],
                    ["月露菌汁、雾心菌核、凝霜叶先后投入，建立偏阴的药性主体。", "凝露松脂阳性较强，控制用量并错息投入；不要在高火、未连通时搅动。", "观察阴阳偏向与炉内躁动：镇只会压制真实躁动，静稳时不空耗。", "药性连通后再搅动，再以清音玉片的明辅助分离；没有清音玉片也可用普通分离。", "用少量阳材触发真实的镇摄对象，最终仍须落在静心丹偏阴的成丹范围。"],
                    [I("雾心菌核","作为主材；镇只在炉内确有躁动时消耗并压制。"), I("月露菌汁","阴性液质较易炼开；化只帮助尚未炼开的材料。"), I("凝霜叶","阴性材料可维持偏阴；数量由成品要求决定。"), I("凝露松脂","少量阳性材料用于错开接触；多了可能偏离静心丹目标。"), I("清音玉片","和性材料不显著拉偏阴阳；明在实际分离时帮助辨识。")]),
                new("clay_stillness", "泥脉双镇定心方", "泥脉石团", "偏阴石材配伍，过程较慢；主要难点是石质开炼线与耐热控制，操作负担低于主动制造阴阳冲撞。",
                    ["灵泉露","泥脉石团","瘴泥珠","云雾菌"],
                    ["灵泉露先炼，等待时不用为液质继续加火。", "泥脉石团达到石质开炼线后再持续炼化；它速度较慢，别用过火弥补等待。", "瘴泥珠偏阴，与主材分批接触；看连通度再搅动，避免把高温接触变成躁动。", "云雾菌仅在出现可恢复损伤时提供生机修复，不能返还烧毁药性。", "目标是自然维持偏阴，不要求为了使用镇而刻意制造躁动。"],
                    [I("泥脉石团","作为主材；石质开炼线高、炼化较慢。"), I("灵泉露","低温易炼的和性辅材，可替换。"), I("瘴泥珠","阴性石材；与主材分批接触，连通后再搅动。"), I("云雾菌","生仅在有可恢复损伤时发挥；不修复烧毁材料。")])
            ])
    ];

    public static IEnumerable<string> MaterialNames => Pills.SelectMany(p => p.MainCandidates.Concat(p.Formulas.SelectMany(f => f.Ingredients.Select(i => i.HerbName)))).Distinct();
    public static PillType GetPill(string id) => Pills.First(p => p.Id == id);
    public static Formula GetFormula(string pillId, string formulaId) =>
        GetPill(pillId).Formulas.First(f => f.Id == formulaId);
}
