using Godot;
using GArray = Godot.Collections.Array<string>;

/// <summary>只装配已学内容；不研读、不发放属性、不读取对手或玩家队列。</summary>
public static class NpcCombatBuildPlanner
{
    public sealed record Build(string Main, string[] Subs, string[] Skills, string Explanation);

    public static GongfaResource? ResolveBook(string id) => GongfaAvailability.EnabledBooks()
        .FirstOrDefault(book => book.ResourcePath == id || book.gongfa_name == id);

    public static void ImportLearning(WorldNpcData npc, HumanData data)
    {
        data.unlocked_skill_node_ids=npc.unlocked_skill_node_ids.Duplicate();
        foreach (var id in npc.learned_gongfa_ids)
            if (ResolveBook(id) is { } book && !data.learned_gongfa_paths.Contains(book.ResourcePath))
                data.learned_gongfa_paths.Add(book.ResourcePath);
        // 卷章必须有明确记录；仅有书名不视为学完第一卷或全书。
        foreach (var book in GongfaAvailability.EnabledBooks())
        {
            var recorded = npc.learned_gongfa_chapters.GetValueOrDefault(book.ResourcePath,
                npc.learned_gongfa_chapters.GetValueOrDefault(book.gongfa_name, new GArray())).AsGodotArray();
            if (!data.learned_gongfa_paths.Contains(book.ResourcePath)) continue;
            data.learned_gongfa_chapters[book.ResourcePath] = new GArray(recorded.Select(value => value.AsString())
                .Where(id => book.get_chapter_by_id(id) is not null));
        }
    }

    public static HumanData TestLearning(IEnumerable<GongfaResource> books) 
    {
        var data = new HumanData { physique = 10, intelligence = 10, soul = 10,
            base_physique = 10, base_intelligence = 10, base_soul = 10,
            cultivation_realm = "云息九层", max_mana = 3, current_mana = 3 };
        // 对练入口本来就可选公共节点技能；这里只构造测试用学习池，不写入世界角色。
        foreach(var node in SkillNodeNetworkConfig.load_default()?.get_nodes() ?? Enumerable.Empty<SkillNodeConfig>())
            data.unlocked_skill_node_ids.Add(node.node_id);
        foreach (var book in books)
        {
            data.learned_gongfa_paths.Add(book.ResourcePath);
            data.learned_gongfa_chapters[book.ResourcePath] = new GArray(book.get_sorted_chapters().Select(chapter => chapter.get_id()));
        }
        return data;
    }

    /// <summary>测试全卷配置仍走角色的化法合并与辅修左右支规则。</summary>
    public static void ResolveTestLoadout(BattleEntityData unit)
    {
        var books=new[]{unit.main_gongfa_path}.Concat(unit.sub_gongfa_paths).Select(ResolveBook).OfType<GongfaResource>().ToArray();
        var data=TestLearning(books);
        data.main_gongfa_path=unit.main_gongfa_path;data.sub_gongfa_paths=unit.sub_gongfa_paths.Duplicate();
        foreach(var book in books.Where(book=>unit.sub_gongfa_paths.Contains(book.ResourcePath)))
        foreach(var chapter in book.get_sorted_chapters())
            data.equipped_gongfa_chapter_routes[book.ResourcePath+":"+chapter.get_id()]=
                chapter.get_deck_rewrites(false).Any(rewrite=>rewrite.enabled)?"right":"left";
        var actor=new Character();actor.load_from_data(data);
        // 同一次装配只构造一次技能池，持有资源至复制完成。
        // 逐槽调用角色完整快照会反复重建/回收整套 C# Resource，触发原生句柄回收竞态。
        var basic=Character.PublicBasicDeckResource;
        var skills=GongfaSkillRewriteRuntime.ApplyBasicSkillRewriteSkills(actor,basic.get_deck_skills())
            .Concat(actor.get_available_battle_skills()).ToArray();
        var resolved=new Godot.Collections.Array<ActiveSkillData>();
        foreach(var path in unit.combat_skill_slot_paths)
        {
            if(string.IsNullOrWhiteSpace(path)){resolved.Add(ArtifactSkillFactory.EmptySlot());continue;}
            var original=ResourceLoader.Load<ActiveSkillData>(path);
            var source=skills.FirstOrDefault(skill=>skill.skill_name==original?.skill_name)??original;
            if(!Character.is_combat_skill_slot_eligible(source)){resolved.Add(ArtifactSkillFactory.EmptySlot());continue;}
            var copy=(ActiveSkillData)source!.Duplicate(true);
            copy.combat_source_key=Character.combat_skill_key(source);
            resolved.Add(copy);
        }
        // 存档/测试选项仍保存原引用；只有运行时副本应用化法。
        unit.combat_resolved_skills=resolved;
        unit.combat_passive_paths=actor.get_combat_passive_paths();
        unit.combat_heart_method_upgrades=actor.get_combat_heart_method_upgrades();
        unit.combat_tempering_by_gongfa=actor.get_active_combat_tempering_by_gongfa();
        GC.KeepAlive(skills);
        GC.KeepAlive(basic);
        actor.Free();
    }

    public static Build Equip(Character actor, string? fixedMain = null, IEnumerable<string>? fixedSubs = null)
    {
        var resources=(actor.current_health,actor.max_health,actor.current_stamina,actor.max_stamina,actor.current_mana,actor.max_mana);
        var known = actor.get_known_gongfas().Where(book => actor.has_learned_gongfa(book)
            && actor.get_learned_gongfa_chapter_ids(book).Count > 0).OrderBy(book => book.gongfa_name, StringComparer.Ordinal).ToArray();
        var main = fixedMain is not null ? known.FirstOrDefault(book => book.ResourcePath == fixedMain)
            : known.Where(book => book.can_be_main && book.is_wuxing_compatible(actor.spirit_root_type))
                .OrderByDescending(book => MainValue(actor, book, known)).FirstOrDefault();
        foreach (var old in actor.sub_gongfa_list.ToArray()) actor.remove_sub_gongfa(old);
        actor.unequip_main_gongfa();
        if (main is not null && !actor.switch_main_gongfa(main)) main = null;
        var subs = known.Where(book => book != main && book.can_be_sub && book.is_wuxing_compatible(actor.spirit_root_type));
        if (fixedSubs is not null) subs = subs.Where(book => fixedSubs.Contains(book.ResourcePath));
        foreach (var book in subs.OrderByDescending(book => SubValue(actor, main, book)).Take(actor.max_sub_slots))
        {
            actor.add_sub_gongfa(book);
            foreach (var chapter in book.get_sorted_chapters().Where(chapter => actor.is_gongfa_chapter_learned(book, chapter.get_id())))
                actor.set_sub_gongfa_chapter_route(book, chapter.get_id(),
                    chapter.get_deck_rewrites(false).Any(rewrite => rewrite.enabled) ? "right" : "left");
        }
        // 换主辅修会刷新派生属性；不能把测试资源上限或世界角色当前生命重置。
        actor.max_health=resources.max_health;actor.current_health=resources.current_health;
        actor.max_stamina=resources.max_stamina;actor.current_stamina=resources.current_stamina;
        actor.max_mana=resources.max_mana;actor.current_mana=resources.current_mana;
        var basic=Character.PublicBasicDeckResource;
        var pool = GongfaSkillRewriteRuntime.ApplyBasicSkillRewriteSkills(actor,basic.get_deck_skills())
            .Concat(actor.get_available_battle_skills()).Where(Character.is_combat_skill_slot_eligible)
            .GroupBy(skill => skill.skill_name, StringComparer.Ordinal).Select(group => group.First())
            .OrderByDescending(skill => SkillValue(actor, skill)).ToList();
        var keys = Enumerable.Repeat("", 10).ToArray();
        var xuan = main?.gongfa_name == "玄枢含章经" || actor.sub_gongfa_list.Any(book => book.gongfa_name == "玄枢含章经");
        if (xuan)
        {
            // 复命的自动目标与前次技能依赖较强，优先把稳定的攻击/护盾放入自动槽。
            var automatic = pool.Where(skill => !CardRules.IsResponse(skill) && skill.skill_name != "复命"
                && skill.energy_cost <= actor.max_stamina && skill.mana_cost <= actor.max_mana)
                .OrderByDescending(skill => skill.skill_name == "惊蛰" ? 100 : skill.skill_name == "云帔" ? 90 : SkillValue(actor, skill)).FirstOrDefault();
            if (automatic is not null) { keys[9] = Character.combat_skill_key(automatic); pool.Remove(automatic); }
        }
        for (var i = 0; i < Math.Min(xuan ? 9 : 10, pool.Count); i++) keys[i] = Character.combat_skill_key(pool[i]);
        if(actor.entity_data is HumanData current)
        {
            current.combat_skill_slots_configured=true;
            current.combat_skill_slot_paths=new GArray(keys);
            current.combat_skill_references=new Godot.Collections.Array<CombatSkillReference>(keys.Select(CombatSkillReference.FromKey));
        }
        var subPaths = actor.sub_gongfa_list.Select(book => book.ResourcePath).ToArray();
        var names = actor.sub_gongfa_list.Select(book => book.gongfa_name);
        GC.KeepAlive(basic);
        return new(main?.ResourcePath ?? "", subPaths, keys,
            $"主修：{main?.gongfa_name ?? "无"}；辅修：{string.Join("、", names.DefaultIfEmpty("无"))}。" +
            (known.Any(book => book.gongfa_name == "百骸衍枝化生宝册") ? "气血铺垫后接近身攻击。" : "按现有资源安排攻守。") +
            (xuan ? "第10槽留给周天自动施展。" : ""));
    }

    private static float MainValue(Character actor, GongfaResource book, GongfaResource[] known)
    {
        var order = book.get_sorted_chapters().Where(chapter => actor.is_gongfa_chapter_learned(book, chapter.get_id())).Select(chapter => chapter.order).DefaultIfEmpty(0).Max();
        var value = order * 12f;
        if (book.gongfa_name == "百骸衍枝化生宝册") value += actor.physique * 2 + (known.Any(item => item.gongfa_name == "太和感物养元宝册") ? 20 : 0);
        if (book.gongfa_name == "斫基损契玄经") value += actor.intelligence * 2 + (actor.max_mana > 1 ? 15 : -100);
        if (book.gongfa_name == "玄枢含章经") value += actor.intelligence + actor.soul + (known.Any(item => item.gongfa_name == "斫基损契玄经") ? 12 : 0);
        if (book.gongfa_name == "太和感物养元宝册") value += actor.physique;
        return value;
    }

    private static float SubValue(Character actor, GongfaResource? main, GongfaResource book) =>
        actor.get_learned_gongfa_chapter_ids(book).Count * 10 +
        ((main?.gongfa_name == "百骸衍枝化生宝册" && book.gongfa_name == "太和感物养元宝册") ||
         (main?.gongfa_name == "太和感物养元宝册" && book.gongfa_name == "百骸衍枝化生宝册") ? 60 : 0) +
        ((main?.gongfa_name == "玄枢含章经" && book.gongfa_name == "斫基损契玄经") ||
         (main?.gongfa_name == "斫基损契玄经" && book.gongfa_name == "玄枢含章经") ? 50 : 0);

    private static float SkillValue(Character actor, ActiveSkillData skill) => skill.skill_name switch
    {
        "催血" => 100, "聚炁" => actor.main_gongfa?.gongfa_name=="玄枢含章经"||actor.sub_gongfa_list.Any(book=>book.gongfa_name=="玄枢含章经")?98:45,
        "血臂撼岳" => 95, "乘势再攻" => 90, "焚元" => actor.max_mana > 1 ? 85 : 5,
        "折锋" => 82, "惊蛰" => 80, "虚怀纳劲" => 75, "架招" => 70, "应敌" => 65, "云帔" => 60,
        "痛击" => 55, "复命" => 50, _ => 40 + skill.calculate_damage(actor)
    };
}
