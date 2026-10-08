using Godot;

/// <summary>
/// 行炁五行参悟节点的静态定义。
/// 金木水火土只表示五种运作特性；节点效果必须落到具体的牌面结算、派生收益或新卡牌权限。
/// </summary>
public sealed class QiNodeDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string Kind { get; }
    public string Element { get; }
    public IReadOnlyList<string> CostElements { get; }
    public IReadOnlyList<string> Prerequisites { get; }
    public IReadOnlyList<float> LevelCosts { get; }
    public IReadOnlyList<string> LevelEffects { get; }
    public int MaxLevel => LevelCosts.Count;

    // 兼容早期展示层的属性；真正的状态由 Character 的 qi_node_levels 持久化。
    public float InsightCost => GetLevelCost(1);
    public string Effect => GetLevelEffect(1);
    public bool Unlocked => false;

    public QiNodeDefinition(
        string id,
        string name,
        string kind,
        string element,
        IReadOnlyList<string> costElements,
        IReadOnlyList<float> levelCosts,
        IReadOnlyList<string> levelEffects,
        params string[] prerequisites)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Element = element;
        CostElements = costElements;
        LevelCosts = levelCosts;
        LevelEffects = levelEffects;
        Prerequisites = prerequisites;
    }

    public float GetLevelCost(int level)
    {
        if (LevelCosts.Count == 0) return 0f;
        return LevelCosts[Mathf.Clamp(level, 1, LevelCosts.Count) - 1];
    }

    public string GetLevelEffect(int level)
    {
        if (LevelEffects.Count == 0) return "尚未写入具体效果。";
        return LevelEffects[Mathf.Clamp(level, 1, LevelEffects.Count) - 1];
    }

    public string GetCostText(int level)
    {
        var cost = GetLevelCost(level);
        return string.Join(" + ", CostElements.Select(element => $"{element}悟 {cost:0.0}"));
    }
}

/// <summary>当前可见大层的五行参悟网盘；未来大层仍由同一套节点接口扩展。</summary>
public static class QiNodeCatalog
{
    public static readonly IReadOnlyList<string> Traits = new[] { "木", "火", "土", "金", "水" };

    private static readonly List<QiNodeDefinition> _currentRealm =
    [
        new("wood_root", "木·生发", "根", "木", new[] { "木" }, new[] { 1.0f, 2.0f }, new[]
        {
            "首次获得一项一次性派生收益时，允许该收益再结算一次；每回合限一次。",
            "上述延续可作用于法力、护盾或抽牌等派生收益，并把延续后的效果保留到本回合结束。"
        }),
        new("fire_root", "火·激发", "根", "火", new[] { "火" }, new[] { 1.0f, 2.0f }, new[]
        {
            "本回合首次支付至少3点法力的术法结算后，追加一次原效果25%的爆发结算。",
            "触发条件提高到至少4点法力时，追加结算提高到原效果50%，且不额外支付法力。"
        }),
        new("earth_root", "土·承载", "根", "土", new[] { "土" }, new[] { 1.0f, 2.0f }, new[]
        {
            "回合开始时，按当前拥有的法力每2点获得1点可永久叠加的护盾。",
            "由此获得的护盾本回合首次受到伤害后保留50%，并继续计入后续承载结算。"
        }),
        new("metal_root", "金·收束", "根", "金", new[] { "金" }, new[] { 1.0f, 2.0f }, new[]
        {
            "每回合首次把两次法力动作压成一次结算时，后一次动作费用-1并锁定为同一目标。",
            "本回合第二次进行收束时仍可触发费用-1，但只保留最后一次动作的目标。"
        }),
        new("water_root", "水·调运", "根", "水", new[] { "水" }, new[] { 1.0f, 2.0f }, new[]
        {
            "每回合首次未结算的法力效果可以移到下一张牌，效果不再丢失。",
            "被调运的效果若本回合仍未结算，则保留到下一回合开始，并只占用一次调运机会。"
        }),

        new("wood_fire_sheng", "木生火", "生", "木", new[] { "木", "火" }, new[] { 0.5f, 1.0f }, new[]
        {
            "同回合先触发生发收益、再支付至少3点法力时，下一张术法追加原效果25%的结算。",
            "追加结算提高到原效果50%，且可作用于下一张心法牌产生的派生收益。"
        }, "wood_root", "fire_root"),
        new("fire_earth_sheng", "火生土", "生", "火", new[] { "火", "土" }, new[] { 0.5f, 1.0f }, new[]
        {
            "本回合法力达到4时，额外获得10点可叠加护盾；每回合限一次。",
            "本回合法力达到6时可再次触发一次，第二次护盾按基础值的50%获得。"
        }, "fire_root", "earth_root"),
        new("earth_metal_sheng", "土生金", "生", "土", new[] { "土", "金" }, new[] { 0.5f, 1.0f }, new[]
        {
            "连续打出两张术法牌时，第二张术法牌费用-1。",
            "同回合首次连续打出三张术法牌时，第三张也费用-1；每回合限一次。"
        }, "earth_root", "metal_root"),
        new("metal_water_sheng", "金生水", "生", "金", new[] { "金", "水" }, new[] { 0.5f, 1.0f }, new[]
        {
            "本回合第一次弃牌时返还1点法力；返还值不超过该牌支付的法力。",
            "若返还后仍有未结算法力动作，可把其中一项移至下一张牌。"
        }, "metal_root", "water_root"),
        new("water_wood_sheng", "水生木", "生", "水", new[] { "水", "木" }, new[] { 0.5f, 1.0f }, new[]
        {
            "回合结束时仍有至少2点法力，复制一次本回合获得1点法力的派生收益。",
            "每多保留2点法力，复制收益再提高一次；最多额外提高两次。"
        }, "water_root", "wood_root"),

        new("wood_earth_ke", "木克土", "克", "木", new[] { "木", "土" }, new[] { 0.5f, 1.0f }, new[]
        {
            "敌方获得护盾后，你的下一张单体术法伤害+8；每回合首次触发。",
            "伤害加成提高到+12，并在该次结算后让目标护盾再减少4点。"
        }, "wood_root", "earth_root", "wood_fire_sheng"),
        new("earth_water_ke", "土克水", "克", "土", new[] { "土", "水" }, new[] { 0.5f, 1.0f }, new[]
        {
            "受到持续效果时，第一次结算延后1回合；每回合限一次。",
            "被延后的持续效果结算时，持续层数额外减少1层。"
        }, "earth_root", "water_root", "fire_earth_sheng"),
        new("water_fire_ke", "水克火", "克", "水", new[] { "水", "火" }, new[] { 0.5f, 1.0f }, new[]
        {
            "敌方完成一次爆发结算后，返还2点法力；每回合首次触发。",
            "返还提高到3点，并把下一张术法的爆发追加结算降低25%。"
        }, "water_root", "fire_root", "water_wood_sheng"),
        new("fire_metal_ke", "火克金", "克", "火", new[] { "火", "金" }, new[] { 0.5f, 1.0f }, new[]
        {
            "敌方护盾被削减后，本回合下一张术法牌费用-1；每回合首次触发。",
            "费用-1后，若该术法仍支付至少2点法力，追加一次原效果25%的结算。"
        }, "fire_root", "metal_root", "fire_earth_sheng"),
        new("metal_wood_ke", "金克木", "克", "金", new[] { "金", "木" }, new[] { 0.5f, 1.0f }, new[]
        {
            "敌方连续获得同名增益时，第二次收益降低30%；每个目标每回合限一次。",
            "第二次收益降低50%，并截断该同名增益本回合的第三次获得。"
        }, "metal_root", "wood_root", "metal_water_sheng"),

        new("wood_use", "木行·续生", "用", "木", new[] { "木" }, new[] { 1.0f, 2.0f, 3.0f }, new[]
        {
            "解锁新卡【续生】（1费·心法）：复制本回合最近一次获得的派生收益。",
            "【续生】可选择本回合最近两项派生收益之一，并把复制结果延长到回合结束。",
            "解锁【续生·复起】卡式：复制后的收益再触发一次，但每回合只能使用一次。"
        }, "wood_root", "water_wood_sheng", "wood_earth_ke"),
        new("fire_use", "火行·炽贯", "用", "火", new[] { "火" }, new[] { 1.0f, 2.0f, 3.0f }, new[]
        {
            "解锁新卡【炽贯】（2费·术法）：将当前法力的一半转为一次爆发伤害。",
            "【炽贯】若支付至少4点法力，爆发结算后追加一次25%的伤害。",
            "解锁【炽贯·越阶】卡式：可把当前法力全部转为爆发，但本回合不再获得法力。"
        }, "fire_root", "wood_fire_sheng", "fire_metal_ke"),
        new("earth_use", "土行·厚载", "用", "土", new[] { "土" }, new[] { 1.0f, 2.0f, 3.0f }, new[]
        {
            "解锁新卡【厚载】（2费·心法）：把当前未用法力转为可叠加护盾。",
            "【厚载】转化的护盾按当前法力每2点提高1点，并保留到战斗结束。",
            "解锁【厚载·固守】卡式：本回合首次护盾被击破时，返还其剩余值的25%。"
        }, "earth_root", "fire_earth_sheng", "earth_water_ke"),
        new("metal_use", "金行·一线", "用", "金", new[] { "金" }, new[] { 1.0f, 2.0f, 3.0f }, new[]
        {
            "解锁新卡【一线】（1费·术法）：将本回合两次法力动作压成一次明确结算。",
            "【一线】压缩后的结算费用-1，并锁定最后一次动作的目标。",
            "解锁【一线·裁定】卡式：压缩三次法力动作，但只保留最后一项收益。"
        }, "metal_root", "earth_metal_sheng", "metal_wood_ke"),
        new("water_use", "水行·回澜", "用", "水", new[] { "水" }, new[] { 1.0f, 2.0f, 3.0f }, new[]
        {
            "解锁新卡【回澜】（1费·心法）：把一项尚未结算的法力效果移至下一张牌。",
            "【回澜】移置的效果若仍未结算，可保留到下一回合开始。",
            "解锁【回澜·复流】卡式：把两项未结算法力效果按先后顺序移置。"
        }, "water_root", "metal_water_sheng", "water_fire_ke"),

        new("wood_merge", "木合式", "合", "木", new[] { "木", "水", "土" }, new[] { 0.5f, 1.0f, 1.5f }, new[]
        {
            "生发与克制同时满足时，首次结算追加一次等额派生收益。",
            "追加收益可延长一个回合，并不再占用生发或克制的触发次数。",
            "解锁新卡【回芽定势】：把一次被截断的收益恢复并立即结算。"
        }, "wood_use", "water_wood_sheng", "wood_earth_ke"),
        new("fire_merge", "火合式", "合", "火", new[] { "火", "木", "金" }, new[] { 0.5f, 1.0f, 1.5f }, new[]
        {
            "一张牌同时触发两种法力动作时，第二次结算强度提高25%。",
            "第二次结算强度提高50%，且不额外增加该牌的法力费用。",
            "解锁新卡【炽断】：把一次收束后的法力动作重新点燃为爆发结算。"
        }, "fire_use", "wood_fire_sheng", "fire_metal_ke"),
        new("earth_merge", "土合式", "合", "土", new[] { "土", "火", "水" }, new[] { 0.5f, 1.0f, 1.5f }, new[]
        {
            "本回合形成的护盾首次被击破时，保留20%并锁定1回合。",
            "保留比例提高到40%，且被锁定的护盾仍可继续叠加。",
            "解锁新卡【厚土回环】：把本回合已消耗的护盾转为下一回合开始时的法力。"
        }, "earth_use", "fire_earth_sheng", "earth_water_ke"),
        new("metal_merge", "金合式", "合", "金", new[] { "金", "土", "木" }, new[] { 0.5f, 1.0f, 1.5f }, new[]
        {
            "被收束的多段效果只消耗一次结算机会，并保留最后一段的目标。",
            "收束时还锁定最后一段的费用，不受后续费用变化影响。",
            "解锁新卡【一线裁天】：把三段法力动作压成一次结算并锁定结果。"
        }, "metal_use", "earth_metal_sheng", "metal_wood_ke"),
        new("water_merge", "水合式", "合", "水", new[] { "水", "金", "火" }, new[] { 0.5f, 1.0f, 1.5f }, new[]
        {
            "调运后的法力若未被使用，下一回合开始时仍保留一半。",
            "保留法力上限提高到本回合调运总量的三分之二。",
            "解锁新卡【回澜照影】：把一项已结算的法力收益回流到下一回合再结算一次。"
        }, "water_use", "metal_water_sheng", "water_fire_ke")
    ];

    private static readonly Dictionary<string, QiNodeDefinition> _byId = _currentRealm.ToDictionary(node => node.Id, node => node);

    public static IReadOnlyList<QiNodeDefinition> CurrentRealm => _currentRealm;

    public static QiNodeDefinition? Get(string id) => _byId.GetValueOrDefault(id);

    public static bool IsTrait(string element) => Traits.Contains(element);

    public static string KindName(string kind) => kind switch
    {
        "根" => "特性根节点",
        "生" => "相生特性节点",
        "克" => "相克特性节点",
        "用" => "专行特性节点",
        "合" => "合式特性节点",
        _ => "参悟节点"
    };

    public static string TraitAspects(string element) => element switch
    {
        "木" => "生发、延展、复起",
        "火" => "激发、跃迁、爆发",
        "土" => "承载、稳定、蓄存",
        "金" => "收束、裁定、截断",
        "水" => "流转、调运、回流",
        _ => "多重运作特性"
    };

    public static Color TraitColor(string element) => element switch
    {
        "木" => new Color("5f6964"),
        "火" => new Color("706258"),
        "土" => new Color("776e62"),
        "金" => new Color("656668"),
        "水" => new Color("5d6970"),
        _ => new Color("6b6254")
    };

    public static Color KindColor(string kind) => kind switch
    {
        "生" => new Color("9a6c35"),
        "克" => new Color("654f46"),
        "用" => new Color("6d5c38"),
        "合" => new Color("473b2d"),
        _ => new Color("84704b")
    };
}
