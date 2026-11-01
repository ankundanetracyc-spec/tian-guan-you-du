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
    public const int PopulationVersion = 1;
    private static readonly System.Collections.Generic.Dictionary<string, ItemData> CarriedItemDefinitions = new(StringComparer.Ordinal);

    private static readonly string[] Surnames =
    ["李", "王", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴", "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗", "梁", "宋", "郑", "谢", "韩", "唐", "冯", "于", "萧", "程", "曹", "袁", "沈", "彭", "吕", "苏", "卢", "蒋", "蔡", "贾", "丁", "魏", "薛", "叶", "余", "潘", "杜", "戴", "夏", "钟", "汪", "田", "任", "姜", "范", "方", "石", "姚", "谭", "廖", "邹", "熊", "金", "陆", "秦", "江", "史", "顾", "侯", "邵", "孟", "龙", "段", "雷", "钱", "汤", "尹", "黎", "易", "常", "乔", "贺"];

    private static readonly string[] GivenNames =
    ["安", "宁", "远", "清", "川", "云", "松", "鹤", "舟", "衡", "岳", "岑", "昭", "明", "景", "晏", "怀", "谨", "言", "修", "玄", "朔", "凌", "霁", "澜", "渊", "珩", "瑾", "瑜", "宸", "彦", "昊", "辰", "铭", "哲", "霖", "浩", "然", "恒", "博", "文", "逸", "遥", "翔", "飞", "婉", "宁", "若", "初", "灵", "秀", "慧", "静", "芷", "兰", "蕙", "瑶", "琳", "月", "雪", "岚", "漪", "凝", "含", "素", "紫", "碧", "珊", "晴", "音", "棠", "微", "舒", "妍", "倩", "莹", "萱", "荷", "莲", "菱", "秋", "晚", "青", "绮", "真", "妤", "婧", "芸", "薇"];

    private static readonly string[] CultivationRealms =
    ["凡人", "凡人", "云息一层", "云息二层", "云息二层", "云息三层", "云息四层", "云息五层", "云息六层", "云息七层"];

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
        ensure_population(manager, mapData);
        manager.set_simulation_enabled(true);
    }

    /// <summary>给玉虚真传支线补齐稳定的择师与带教候选；只生成缺失 ID，读档和重进不会换人。</summary>
    public static bool ensure_yuxu_lineage_staff(NpcWorldManager manager, WorldNodeMapData mapData)
    {
        if (manager is null || mapData is null || mapData.get_node("yuxu_xianzong_core") is not { } sectNode)
            return false;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var usedNames = new System.Collections.Generic.HashSet<string>(
            manager.get_active_npcs().Select(npc => npc.npc_name), StringComparer.Ordinal);
        var changed = false;
        var subscene = unlocked_subscene_ids(sectNode).Contains("zifu") ? "zifu" : "";

        EnsurePair("金丹后期", "长老", "yuxu_lineage_teacher_late", "yuxu_lineage_guide_late");
        EnsurePair("金丹圆满", "长老", "yuxu_lineage_teacher_perfection", "yuxu_lineage_guide_perfection");
        return changed;

        void EnsurePair(string teacherRealm, string teacherIdentity, string teacherId, string guideId)
        {
            var teacher = manager.get_npc(teacherId);
            if (teacher is null)
            {
                teacher = create_lineage_staff(
                    teacherId,
                    sectNode,
                    subscene,
                    teacherRealm,
                    teacherIdentity,
                    $"玉虚仙宗修士，现于紫府峰处理师门事务。",
                    rng,
                    usedNames,
                    isFemale: null);
                teacher.sect_assigned_guide_npc_id = guideId;
                manager.register_npc(teacher);
                changed = true;
            }
            else if (teacher.sect_assigned_guide_npc_id != guideId)
            {
                teacher.sect_assigned_guide_npc_id = guideId;
                changed = true;
            }

            if (manager.get_npc(guideId) is not null) return;
            var guide = create_lineage_staff(
                guideId,
                sectNode,
                subscene,
                "云息十层",
                "真传弟子",
                $"由{teacher.npc_name}门下培养，现任玉虚真传带教同门。",
                rng,
                usedNames,
                isFemale: rng.RandiRange(0, 1) == 1);
            guide.sect_teacher_npc_id = teacherId;
            guide.relation_tags.Add("师承带教");
            manager.register_npc(guide);
            changed = true;
        }
    }

    /// <summary>Creates the stable, procedurally named elder who receives new entrants at the Yuxu gate.</summary>
    public static WorldNpcData? ensure_yuxu_admission_elder(NpcWorldManager manager, WorldNodeMapData mapData)
    {
        const string elderId = "yuxu_gate_elder";
        if (manager is null || mapData is null || mapData.get_node("yuxu_xianzong_core") is not { } sectNode)
            return null;
        if (manager.get_npc(elderId) is { } existing)
            return existing;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var usedNames = new System.Collections.Generic.HashSet<string>(
            manager.get_active_npcs().Select(npc => npc.npc_name), StringComparer.Ordinal);
        var elder = create_lineage_staff(
            elderId,
            sectNode,
            "",
            "金丹圆满",
            "山门接引长老",
            "玉虚仙宗山门接引长老，负责迎接初至山门的修士。",
            rng,
            usedNames,
            isFemale: null);
        elder.relation_tags.Remove("师承候选");
        elder.relation_tags.Add("山门接引");
        elder.long_term_goal = "守护山门秩序，甄别来访修士。";
        elder.short_term_goal = "接待初至玉虚仙宗的修士。";
        manager.register_npc(elder);
        return elder;
    }

    /// <summary>Creates the stable, procedurally named disciple who escorts a new entrant through registration.</summary>
    public static WorldNpcData? ensure_yuxu_admission_guide(NpcWorldManager manager, WorldNodeMapData mapData)
    {
        const string guideId = "yuxu_gate_guide";
        if (manager is null || mapData is null || mapData.get_node("yuxu_xianzong_core") is not { } sectNode)
            return null;
        if (manager.get_npc(guideId) is { } existing)
            return existing;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var usedNames = new System.Collections.Generic.HashSet<string>(
            manager.get_active_npcs().Select(npc => npc.npc_name), StringComparer.Ordinal);
        var guide = create_lineage_staff(
            guideId,
            sectNode,
            "guanfu",
            "云息八层",
            "外门弟子",
            "奉山门接引长老之命，负责带初入宗门的修士办理录籍。",
            rng,
            usedNames,
            isFemale: null);
        guide.relation_tags.Remove("师承候选");
        guide.relation_tags.Add("山门接引弟子");
        guide.long_term_goal = "在宗门中稳步修行。";
        guide.short_term_goal = "协助接引长老办理新入门修士的录籍。";
        guide.current_state = "师门常驻";
        manager.register_npc(guide);
        return guide;
    }

    private static WorldNpcData create_lineage_staff(
        string id,
        WorldNodeData sectNode,
        string subscene,
        string realm,
        string identity,
        string originDescription,
        RandomNumberGenerator rng,
        System.Collections.Generic.HashSet<string> usedNames,
        bool? isFemale)
    {
        var gender = isFemale.HasValue ? (isFemale.Value ? 1 : 0) : rng.RandiRange(0, 1);
        var (rootName, weights, _) = pick_yuxu_member_profile(rng);
        var age = rng.RandiRange(16, 68);
        var npc = new WorldNpcData
        {
            npc_id = id,
            npc_name = next_name(rng, usedNames),
            species = "人族",
            birth_year = 1 - age,
            birth_month = rng.RandiRange(1, 12),
            origin_node_id = sectNode.node_id,
            origin_description = originDescription,
            spirit_root_name = rootName,
            root_weights = weights,
            cultivation_realm = realm,
            faction_id = "yuxu_xianzong",
            formal_identity = identity,
            is_yuxu_lineage_staff = true,
            current_age = age,
            max_age = age + rng.RandiRange(24, 72),
            personality = pick(Personalities, rng),
            long_term_goal = "在师门中传授修行经验。",
            short_term_goal = "处理玉虚仙宗的日常事务。",
            current_node_id = sectNode.node_id,
            current_subscene_id = subscene,
            current_state = "师门常驻",
            is_alive = true,
            gender = gender,
        };
        npc.relation_tags.Add("玉虚仙宗");
        npc.relation_tags.Add("师承候选");
        NpcPersonalityRules.Ensure(npc);
        npc.ensure_appearance_identity();
        return npc;
    }

    public static bool ensure_population(NpcWorldManager manager, WorldNodeMapData mapData)
    {
        if (manager.population_version >= PopulationVersion) return false;
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var usedNames = new System.Collections.Generic.HashSet<string>(manager.npcs.Select(n => n.npc_name), StringComparer.Ordinal);
        var added = new System.Collections.Generic.List<WorldNpcData>();
        var sequence = 0;

        foreach (var node in mapData.get_all_nodes())
        {
            if (node is null || node.node_id == "home_village" || !node.enterable) continue;
            // Only authored locations in the current node graph can host world population.
            if (!manager.has_world_grid_anchor(node.node_id)) continue;
            // Include dead and traveling originals: migration never replaces a lost or absent person.
            var existing = manager.npcs.Count(n => !n.is_yuxu_lineage_staff &&
                (n.origin_node_id == node.node_id || string.IsNullOrWhiteSpace(n.origin_node_id) && n.current_node_id == node.node_id));
            var amount = Math.Max(0, population_for(node, (int)node.display_level) - existing);
            var subscene_ids = unlocked_subscene_ids(node);
            for (var index = 0; index < amount; index++)
            {
                var subscene_id = subscene_ids.Count == 0
                    ? ""
                    : subscene_ids[rng.RandiRange(0, subscene_ids.Count - 1)];
                var npc = create_npc(node, subscene_id, sequence++, rng, usedNames);
                npc.npc_id = "runtime_npc_" + Guid.NewGuid().ToString("N");
                manager.register_npc(npc, false);
                added.Add(npc);
            }
        }
        manager.seed_initial_world_travel(added);
        manager.population_version = PopulationVersion;
        return true;
    }

    public static int population_for(WorldNodeData node, int anchorLevel)
    {
        if (node.node_id == "east_woods") return 10;
        if (anchorLevel == 1) return 60;
        if (anchorLevel == 2) return 30;
        return node.node_type switch
        {
            WorldNodeData.NodeType.City => 50,
            WorldNodeData.NodeType.Village => 12,
            WorldNodeData.NodeType.Resource => 6,
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
        var soul = rng.RandfRange(3.0f, 9.5f);
        var age = rng.RandiRange(16, 68);
        var max_health = Mathf.RoundToInt(72.0f + physique * 11.0f + soul * 2.0f);
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
        NpcPersonalityRules.Ensure(npc);
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
            "res://resources/entities/items/灵石.tres",
        };
        if (rng.RandiRange(0, 99) >= 65) return;
        var path = choices[rng.RandiRange(0, choices.Length - 1)];
        if (!CarriedItemDefinitions.ContainsKey(path))
        {
            var item = ResourceLoader.Load<ItemData>(path);
            if (item is null) return;
            CarriedItemDefinitions[path] = item;
        }
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
            "res://resources/entities/items/兽皮.tres",
            "res://resources/entities/items/兽骨.tres",
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
