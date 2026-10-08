using Godot;
using Godot.Collections;

/// <summary>
/// 百川区域当前原型的人口入口。
///
/// 现阶段不读取手写 NPC 名单，而是在首次离开青溪里时为各个地点随机创建一批人物。
/// 这里的姓名、身份、境界与属性只是临时生成池，后续可以直接替换成策划确定的池子与约束。
/// 生成结果留在 NpcWorldManager 中，当前运行期间不会重复抽取。
/// </summary>
public static class CloudStateNpcCatalog
{
    private const string PopulationMeta = "cloud_state_population_loaded";

    private static readonly string[] Surnames =
    ["李", "王", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴", "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗", "梁", "宋", "郑", "谢", "韩", "唐", "冯", "于", "萧", "程", "曹", "袁", "沈", "彭", "吕", "苏", "卢", "蒋", "蔡", "贾", "丁", "魏", "薛", "叶", "余", "潘", "杜", "戴", "夏", "钟", "汪", "田", "任", "姜", "范", "方", "石", "姚", "谭", "廖", "邹", "熊", "金", "陆", "秦", "江", "史", "顾", "侯", "邵", "孟", "龙", "段", "雷", "钱", "汤", "尹", "黎", "易", "常", "乔", "贺"];

    private static readonly string[] GivenNames =
    ["安", "宁", "远", "清", "川", "云", "松", "鹤", "舟", "衡", "岳", "岑", "昭", "明", "景", "晏", "怀", "谨", "言", "修", "玄", "朔", "凌", "霁", "澜", "渊", "珩", "瑾", "瑜", "宸", "彦", "昊", "辰", "铭", "哲", "霖", "浩", "然", "恒", "博", "文", "逸", "遥", "翔", "飞", "婉", "宁", "若", "初", "灵", "秀", "慧", "静", "芷", "兰", "蕙", "瑶", "琳", "月", "雪", "岚", "漪", "凝", "含", "素", "紫", "碧", "珊", "晴", "音", "棠", "微", "舒", "妍", "倩", "莹", "萱", "荷", "莲", "菱", "秋", "晚", "青", "绮", "真", "妤", "婧", "芸", "薇"];

    private static readonly string[] CultivationRealms =
    ["凡人", "凡人", "云息一层", "云息二层", "云息二层", "云息三层", "云息四层", "玄基一层", "玄基二层", "金丹一层"];

    private static readonly string[] CommonIdentities =
    ["行旅散修", "采药人", "猎户", "商旅", "护路人", "寻路客", "抄经人", "炼气士"];

    private static readonly string[] CultivatorIdentities =
    ["外门弟子", "内门弟子", "轮值执事", "客卿", "行走弟子", "供奉", "护山弟子"];

    private static readonly string[] CityIdentities =
    ["铺户", "渡客", "巡路人", "药商", "账房", "散修", "镖客", "说书人"];

    private static readonly string[] Personalities =
    ["沉稳", "机敏", "寡言", "圆融", "谨慎", "爽直", "清峻", "温厚", "多疑", "随和"];

    private static readonly string[] LongTermGoals =
    ["攒下足够灵石，换一门合用的功法。", "寻到一处安稳居所，避开无谓的争端。", "沿着商路游历各地，见识更多修行法门。", "查清一段家中旧事，再决定下一步去处。", "把手上的营生做稳，慢慢积累自己的根基。", "在山川险地中寻找适合自己的机缘。"];

    private static readonly string[] ShortTermGoals =
    ["打听最近几日的路况与消息。", "寻一份短工或护送差事。", "补齐随身的药材和干粮。", "找一处安静地方调息。", "确认下一段路是否仍然通行。", "与当地人换取一件有用的物品。"];

    private static readonly string[] SpiritRoots =
    ["无灵根", "金灵根", "木灵根", "水灵根", "火灵根", "土灵根", "金木杂灵根", "水木杂灵根", "火土杂灵根", "金水杂灵根"];
    private static readonly string[] RootElementNames = ["金", "木", "水", "火", "土"];

    public static void activate(NpcWorldManager manager, WorldNodeMapData mapData)
    {
        if (manager is null || mapData is null) return;
        manager.set_world_map(mapData);
        if (!manager.HasMeta(PopulationMeta))
        {
            // 当前原型没有接入存档人口表；重新开始一次运行时，整片百川区域人口重新抽取。
            manager.npcs.Clear();
            generate_population(manager, mapData);
            manager.SetMeta(PopulationMeta, true);
        }
        manager.set_simulation_enabled(true);
    }

    private static void generate_population(NpcWorldManager manager, WorldNodeMapData mapData)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var usedNames = new System.Collections.Generic.HashSet<string>();
        var sequence = 0;

        foreach (var node in mapData.get_all_nodes())
        {
            if (node is null || node.node_id == "home_village" || !node.enterable) continue;
            var amount = population_for(node, rng);
            var subscene_ids = unlocked_subscene_ids(node);
            for (var index = 0; index < amount; index++)
            {
                var subscene_id = subscene_ids.Count == 0
                    ? ""
                    : subscene_ids[rng.RandiRange(0, subscene_ids.Count - 1)];
                manager.register_npc(create_npc(node, subscene_id, sequence++, rng, usedNames));
            }
        }
    }

    private static int population_for(WorldNodeData node, RandomNumberGenerator rng)
    {
        return node.node_type switch
        {
            WorldNodeData.NodeType.Sect => rng.RandiRange(2, 4),
            WorldNodeData.NodeType.City => rng.RandiRange(2, 4),
            WorldNodeData.NodeType.Village => rng.RandiRange(1, 3),
            WorldNodeData.NodeType.Resource => rng.RandiRange(1, 2),
            WorldNodeData.NodeType.Dungeon => rng.RandiRange(0, 1),
            WorldNodeData.NodeType.War => rng.RandiRange(1, 2),
            WorldNodeData.NodeType.Wilderness => rng.RandiRange(0, 2),
            _ => 0,
        };
    }

    private static WorldNpcData create_npc(WorldNodeData node, string subscene_id, int sequence, RandomNumberGenerator rng, System.Collections.Generic.HashSet<string> usedNames)
    {
        var realm = pick(CultivationRealms, rng);
        var is_yuxu_member = node.node_type == WorldNodeData.NodeType.Sect && node.faction_id == "yuxu_xianzong";
        string spirit_root;
        Array<int> root_weights;
        string identity;
        if (is_yuxu_member)
            (spirit_root, root_weights, identity) = pick_yuxu_member_profile(rng);
        else
        {
            spirit_root = pick_spirit_root(realm, rng);
            root_weights = build_root_weights(spirit_root, rng);
            identity = pick_identity(node, rng);
        }
        var physique = rng.RandfRange(3.0f, 9.5f);
        var intelligence = rng.RandfRange(3.0f, 9.5f);
        var agility = rng.RandfRange(3.0f, 9.5f);
        var soul = rng.RandfRange(3.0f, 9.5f);
        var age = rng.RandiRange(16, 68);
        var max_health = Mathf.RoundToInt(72.0f + physique * 8.0f + agility * 3.0f + soul * 2.0f);
        var npc = new WorldNpcData
        {
            npc_id = $"runtime_npc_{sequence:000}",
            npc_name = next_name(rng, usedNames),
            species = "人族",
            birth_year = 1 - age,
            birth_month = rng.RandiRange(1, 12),
            origin_node_id = node.node_id,
            origin_description = $"在{node.node_name}附近长大，后来沿着道路来到此处。",
            spirit_root_name = spirit_root,
            root_weights = root_weights,
            cultivation_realm = realm,
            spirit_strands = realm == "凡人" ? 0 : rng.RandiRange(1, 18),
            physique = physique,
            intelligence = intelligence,
            agility = agility,
            soul = soul,
            current_health = max_health,
            max_health = max_health,
            current_age = age,
            max_age = age + rng.RandiRange(24, 72),
            faction_id = node.faction_id,
            formal_identity = identity,
            personality = pick(Personalities, rng),
            long_term_goal = pick(LongTermGoals, rng),
            short_term_goal = pick(ShortTermGoals, rng),
            player_relationship = 0,
            current_node_id = node.node_id,
            current_subscene_id = subscene_id,
            current_state = "空闲",
            is_traveling = false,
            is_alive = true,
        };
        npc.resources["灵石"] = rng.RandiRange(0, 24);
        npc.resources["杂材"] = rng.RandiRange(0, 12);
        npc.regional_renown[RegionalRenownSystem.BaiChuanRegionId] = estimate_bai_chuan_renown(npc);
        seed_carried_items(npc, rng);
        seed_trade_inventory(npc, rng);
        npc.behavior_tags.Add(node.node_type == WorldNodeData.NodeType.Wilderness ? "野外活动" : "地点活动");
        npc.behavior_tags.Add(realm == "凡人" ? "凡俗营生" : "修行");
        npc.relation_tags.Add(string.IsNullOrWhiteSpace(node.faction_id) ? "地方熟人" : node.faction_id);
        npc.ensure_appearance_identity();
        return npc;
    }

    /// <summary>为随机人物生成少量真实随身物品；与商贸库存、抽象资源余额分开。</summary>
    private static void seed_carried_items(WorldNpcData npc, RandomNumberGenerator rng)
    {
        var choices = new[]
        {
            "res://resources/entities/items/兽骨.tres",
            "res://resources/entities/items/兽皮.tres",
            "res://resources/entities/items/肉干.tres",
            "res://resources/entities/items/灵石.tres",
        };
        if (rng.RandiRange(0, 99) >= 65) return;
        var path = choices[rng.RandiRange(0, choices.Length - 1)];
        if (ResourceLoader.Load<ItemData>(path) is null) return;
        npc.carried_items[path] = rng.RandiRange(1, 2);
    }

    private static int estimate_bai_chuan_renown(WorldNpcData npc)
    {
        if (npc.faction_id != "yuxu_xianzong") return 0;
        var multiplier = npc.formal_identity switch
        {
            "杂役" or "杂役弟子" => 0.6f,
            "外门弟子" or "行走弟子" or "护山弟子" => 1.0f,
            "内门弟子" or "轮值执事" => 1.5f,
            "真传弟子" => 3.0f,
            _ => 1.0f,
        };
        return Mathf.RoundToInt(50 * multiplier);
    }

    private static void seed_trade_inventory(WorldNpcData npc, RandomNumberGenerator rng)
    {
        var paths = new[]
        {
            "res://resources/entities/items/灵石.tres",
            "res://resources/entities/items/铁矿石.tres",
            "res://resources/entities/items/山麻.tres",
            "res://resources/entities/items/兽皮.tres",
            "res://resources/entities/items/兽骨.tres",
            "res://resources/entities/items/雷击符.tres",
        };
        var count = rng.RandiRange(3, 5);
        for (var i = 0; i < count; i++)
        {
            var path = paths[rng.RandiRange(0, paths.Length - 1)];
            npc.trade_inventory[path] = rng.RandiRange(1, 5);
        }
    }

    private static string pick_identity(WorldNodeData node, RandomNumberGenerator rng)
    {
        if (!string.IsNullOrWhiteSpace(node.faction_id) && node.node_type == WorldNodeData.NodeType.Sect)
            return pick(CultivatorIdentities, rng);
        if (node.node_type is WorldNodeData.NodeType.City or WorldNodeData.NodeType.Village)
            return pick(CityIdentities, rng);
        return pick(CommonIdentities, rng);
    }

    private static string pick_spirit_root(string realm, RandomNumberGenerator rng)
    {
        if (realm == "凡人" || rng.RandiRange(0, 99) < 24) return "无灵根";
        return pick(SpiritRoots[1..], rng);
    }

    private static (string spirit_root, Array<int> root_weights, string identity) pick_yuxu_member_profile(RandomNumberGenerator rng)
    {
        // 复用角色生成器既有的资质分档；无灵根者不进入玉虚仙宗成员池。
        var roll = rng.RandiRange(1, 100);
        while (roll <= 5) roll = rng.RandiRange(1, 100);
        var root_count = roll <= 45 ? 1 : roll <= 70 ? 2 : roll <= 85 ? 3 : roll <= 95 ? 4 : 5;

        var elements = new List<int> { 0, 1, 2, 3, 4 };
        for (var index = 0; index < root_count; index++)
        {
            var selected_index = rng.RandiRange(index, elements.Count - 1);
            (elements[index], elements[selected_index]) = (elements[selected_index], elements[index]);
        }

        var weights = new Array<int> { 0, 0, 0, 0, 0 };
        var base_weight = root_count switch { 1 => 80, 2 => 45, 3 => 30, 4 => 25, _ => 20 };
        var names = new List<string>();
        foreach (var element in elements.Take(root_count).OrderBy(value => value))
        {
            weights[element] = base_weight;
            names.Add(RootElementNames[element]);
        }

        var root_name = string.Concat(names) + (root_count == 1 ? "灵根（天灵根）" : "灵根");
        var identity = root_count switch
        {
            1 => "真传弟子",
            2 => "内门弟子",
            3 => "外门弟子",
            _ => "杂役",
        };
        return (root_name, weights, identity);
    }

    private static Array<int> build_root_weights(string root, RandomNumberGenerator rng)
    {
        var result = new Array<int> { 0, 0, 0, 0, 0 };
        if (root == "无灵根") return result;
        var first = rng.RandiRange(0, 4);
        result[first] = root.Contains("杂灵根") ? 45 : 80;
        if (root.Contains("杂灵根"))
        {
            var second = rng.RandiRange(0, 4);
            while (second == first) second = rng.RandiRange(0, 4);
            result[second] = 45;
        }
        return result;
    }

    private static Array<string> unlocked_subscene_ids(WorldNodeData node)
    {
        var result = new Array<string>();
        foreach (var subscene in node.subscenes)
            if (subscene is not null && subscene.unlocked && !string.IsNullOrWhiteSpace(subscene.subscene_id))
                result.Add(subscene.subscene_id);
        return result;
    }

    private static string next_name(RandomNumberGenerator rng, System.Collections.Generic.HashSet<string> usedNames)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            var name = $"{pick(Surnames, rng)}{pick(GivenNames, rng)}{(rng.RandiRange(0, 99) < 18 ? pick(GivenNames, rng) : "")}";
            if (usedNames.Add(name)) return name;
        }
        var fallback = $"行人{usedNames.Count + 1}";
        usedNames.Add(fallback);
        return fallback;
    }

    private static string pick(string[] pool, RandomNumberGenerator rng) => pool[rng.RandiRange(0, pool.Length - 1)];
}
