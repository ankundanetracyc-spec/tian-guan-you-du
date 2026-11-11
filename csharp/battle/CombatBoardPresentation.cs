using Godot;

public static class CombatBoardPresentation
{
    public const string HoverRules = "每回合选择一行，行内从左向右交锋；各单位当前招式按出手值排序，结算后才加入下一招。\n"
        + "行内单体招式共用所选目标；敌方只显示其目标，不显示招式与行。\n"
        + "招式首次被读到时检查一次，随后变暗；本回合失败不再重试。应对读到后待命，匹配来袭时才付款。\n同名多份共享原使用次数与冷却，改盘不重置。\n"
        + "伤害数值按当前角色自身修正计算，命中、会心和目标防护在实际攻击时结算。";

    public static HoverInfoManager.HoverInfoContent SkillHover(ActiveSkillData skill, GodotObject? owner,
        BattleManager? manager = null, Vector2I? footprint = null, CombatBoardRoundState? state = null,
        CombatBoardPlacement? placement = null)
    {
        var display = skill.skill_name == "复命" ? FumingPresentation.Resolve(owner, skill, manager) : skill;
        var name = skill.skill_name == "复命" ? FumingPresentation.Name(owner, skill, manager) : skill.skill_name;
        var energy = manager is not null && owner is not null ? manager.get_skill_energy_cost(owner, skill) : skill.energy_cost;
        var mana = manager is not null && owner is not null ? manager.get_skill_mana_cost(owner, skill) : skill.mana_cost;
        var capacity = manager is not null && owner is not null ? manager.get_skill_mana_capacity_cost(owner, skill) : 0;
        var cost = SkillCost("消耗", energy, mana, capacity);
        if (skill.skill_name == "复命" && !ReferenceEquals(display, skill) && owner is not null && manager is not null)
        {
            var fees = manager.gongfa_combat.PreviewBoardPayments(owner, skill, display);
            cost = $"{SkillCost("复命消耗", fees[0].Energy, fees[0].Mana, fees[0].Capacity)}\n{SkillCost("复制招式消耗", fees[1].Energy, fees[1].Mana, fees[1].Capacity)}"
                + $"\n{SkillCost("合计消耗", fees.Sum(fee => fee.Energy), fees.Sum(fee => fee.Mana), fees.Sum(fee => fee.Capacity))}";
        }
        var tags = display.get_presentation_tags();
        var target = skill.skill_name == "寄念" ? "盘面后一项不同名招式（暂未启用）" : TargetText(display);
        var battleLimit = skill.uses_per_battle > 0 ? skill.uses_per_battle : skill.max_charges;
        var battleLimitLine = battleLimit > 0 ? $"\n整局限制：{battleLimit}次，同名共享" : "";
        var basic = $"关键词：{(tags.Count > 0 ? string.Join("、", tags) : "无")}\n{cost}{battleLimitLine}\n目标：{target}";
        if (display.skill_type == ActiveSkillData.SkillType.ATTACK)
            basic += "\n出手方式：" + (display.attack_style == "ranged" ? "远程" : "近身");
        var size = footprint ?? skill.board_size;
        var cells = (from y in Enumerable.Range(0, Math.Max(0, size.Y))
            from x in Enumerable.Range(0, Math.Max(0, size.X)) select new Vector2I(x, y)).ToArray();
        return HoverInfoManager.Structured(name, BattleBoardView.Icon(display.icon_path), "",
            new("", basic), new("效果", CardPresentation.BoardHoverEffect(skill, owner))) with
        {
            CardWidth = 340f, IconSize = 96f, CompactGridFooter = true, SingleColumn = true,
            GridDiagrams = cells.Length > 0 ? [new("战斗占格", HoverGridKind.Combat, cells)] : null,
            RulesText = HoverRules,
        };
    }

    public static HoverInfoManager.HoverInfoContent PlacementHover(CombatBoardLayout layout, CombatBoardCatalog catalog,
        CombatBoardPlacement? item, GodotObject? owner, CombatBoardRoundState? state = null, BattleManager? manager = null)
    {
        if (item is null) return new("空格", "扫到时回复体力1。", CardWidth: 340f, ShowRules: false);
        if (item.kind == CombatBoardPlacement.ObjectKind.Skill && catalog.ResolvePlacement(layout, item) is { } skill)
            return SkillHover(skill, owner, manager, CombatBoardGeometry.Footprint(item, catalog.FindSkill), state, item);
        if (item.kind == CombatBoardPlacement.ObjectKind.Talisman && TalismanCombatCatalog.Load(item.source_key) is { } talisman)
            return TalismanCombatCatalog.Hover(talisman, item.expended);
        if (item.kind == CombatBoardPlacement.ObjectKind.Pill && BattleSupplyRules.Load(item.source_key) is { } pill)
        {
            var info = AlchemyPillVisual.Hover(pill, user: owner as Character);
            return info with { Sections = null, Body = info.Body + (item.expended
                ? "\n\n本颗已服用，空瓶仍占格；整理时可移出或补药。"
                : "\n\n一块一颗，每个占格各检查一次；生效才消耗本人库存一颗。") };
        }
        var source = catalog.FindRewrite(item.source_key);
        var title = item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator ? "周天指示" : source?.title ?? "化法";
        var text = Placement(layout, catalog, item, owner, state, manager);
        if (text.StartsWith(title + "\n", StringComparison.Ordinal)) text = text[(title.Length + 1)..];
        if (source is not null) text = text.Replace(source.get_board_display_name() + "\n", "");
        text = text.Replace("占一格，", "");
        return new(title, text, BattleBoardView.Icon(source?.icon_path ?? "res://textures/ui/combat/board_skill_icons/zhoutian_indicator.png"),
            CardWidth: 340f, IconSize: 96f, ShowRules: false, CompactGridFooter: true, SingleColumn: true,
            GridDiagrams: [new("战斗占格", HoverGridKind.Combat, [Vector2I.Zero])]);
    }

    public const string IndicatorText = "周天指示\n占一格，可移动和转向，不能移出。\n指向共边相邻的一份非应对招式；每回合允许其额外施放一次，照常支付费用、检查原次数与冷却。";

    private static string SkillCost(string label, int energy, int mana, int capacity) =>
        $"{label}：体力{energy}、法力{mana}" + (capacity > 0 ? $"、法力上限{capacity}" : "");

    private static string TargetText(ActiveSkillData skill)
    {
        if (CardRules.IsResponse(skill) || skill.target_type == 0) return "自身";
        var explicitAlly = skill.description.Contains("友方") || skill.description.Contains("队友") || skill.description.Contains("盟友");
        var automatic = skill.aoe_range > 0 || skill.description.Contains("全体") || skill.description.Contains("随机");
        if (skill.target_type == 1 && !explicitAlly) return "自身";
        if (automatic)
        {
            if (skill.aoe_range > 0 || skill.description.Contains("全体"))
                return skill.target_type == 2 ? "范围内合法敌方" : skill.target_type == 1 ? "范围内合法友方" : "范围内合法目标";
            return skill.target_type switch
            {
                1 => "随机一名合法友方",
                2 => "随机一名合法敌人",
                3 => "随机一名合法目标",
                _ => "按招式规则"
            };
        }
        return skill.target_type switch
        {
            1 => "本行选定的友方",
            2 => "本行选定的敌方",
            3 => "本行选定的目标",
            _ => "按招式规则"
        };
    }

    public static string Skill(ActiveSkillData skill, GodotObject? owner) =>
        $"{skill.get_board_display_name()}\n体力{skill.energy_cost} · 法力{skill.mana_cost}\n占格{skill.board_size.X}×{skill.board_size.Y}\n"
        + $"关键词：{(skill.get_presentation_tags().Count > 0 ? string.Join("、", skill.get_presentation_tags()) : "无")}\n"
        + $"目标：{TargetText(skill)}\n"
        + (skill.skill_type == ActiveSkillData.SkillType.ATTACK ? $"出手方式：{(skill.attack_style == "ranged" ? "远程" : "近身")}\n" : "")
        + PlayerInfoText.Display(CardPresentation.BoardFace(skill, owner))
        + (skill.cooldown_rounds > 0 ? $"\n冷却{skill.cooldown_rounds}回合；同招共享。" : "")
        + (skill.uses_per_round > 0 ? $"\n使用次数：每回合{skill.uses_per_round}次；同招共享。" : "")
        + (skill.skill_name == "寄念" ? "\n当前版本暂未启用。" : "");

    public static string Placement(CombatBoardLayout layout, CombatBoardCatalog catalog, CombatBoardPlacement item,
        GodotObject? owner, CombatBoardRoundState? state = null, BattleManager? manager = null)
    {
        if (item.kind == CombatBoardPlacement.ObjectKind.Talisman)
        {
            var talisman = TalismanCombatCatalog.Load(item.source_key);
            return talisman is null ? "此符箓暂不可用。" : talisman.entity_name + "\n消耗：体力1、法力0；符箓1张。\n"
                + TalismanCombatCatalog.EffectText(talisman.Snapshot!.RecipeId, talisman.Snapshot.Tier)
                + (item.expended ? "\n本张已使用，符痕仍占格。" : "\n首次读到检查一次，未能施用则保留。");
        }
        if (item.kind == CombatBoardPlacement.ObjectKind.Pill)
        {
            var pill = BattleSupplyRules.Load(item.source_key);
            return pill is null ? "此丹药暂不可用。" : pill.entity_name + "\n" + pill.EffectText
                + (item.expended ? "\n本颗已服用，空瓶仍占格；整理时可移出或补药。" : "\n一块一颗，每个占格各检查一次；生效才消耗本人库存一颗。 ");
        }
        if (item.kind == CombatBoardPlacement.ObjectKind.Skill)
        {
            var skill = catalog.ResolvePlacement(layout, item);
            if (skill is null) return "此招式来源暂不可用。";
            var text = Skill(skill, owner);
            if (owner is not null && manager is not null && skill.skill_name != "复命")
                text = text.Replace($"体力{skill.energy_cost} · 法力{skill.mana_cost}",
                    $"体力{manager.get_skill_energy_cost(owner, skill)} · 法力{manager.get_skill_mana_cost(owner, skill)}"
                    + (manager.get_skill_mana_capacity_cost(owner, skill) > 0 ? $" · 法力上限{manager.get_skill_mana_capacity_cost(owner, skill)}" : ""));
            if (skill.skill_name == "复命" && owner is not null && manager?.gongfa_combat.GetLastNonResponseSkill(owner) is { } copied)
            {
                var fees = manager.gongfa_combat.PreviewBoardPayments(owner, skill, copied);
                text = skill.get_board_display_name(FumingPresentation.Name(owner, skill, manager))
                    + $"\n{SkillCost("复命消耗", fees[0].Energy, fees[0].Mana, fees[0].Capacity)}"
                    + $"\n{SkillCost("复制招式消耗", fees[1].Energy, fees[1].Mana, fees[1].Capacity)}"
                    + $"\n{SkillCost("合计消耗", fees.Sum(fee => fee.Energy), fees.Sum(fee => fee.Mana), fees.Sum(fee => fee.Capacity))}\n"
                    + string.Join("\n", text.Split('\n').Skip(2));
            }
            if (state?.HasSucceeded(item.placement_id) == true) text += "\n本份本回合已发动。";
            else if (state?.IsArmed(item.placement_id) == true) text += "\n已待命。";
            else if (state?.HasBeenChecked(item.placement_id) == true) text += "\n本轮已检查。";
            if (state is not null && state.CooldownRoundsRemaining(skill) > 0) text += $"\n冷却中：还需{state.CooldownRoundsRemaining(skill)}回合。";
            return text;
        }
        var target = CombatBoardGeometry.PointedSkill(layout, item, catalog.FindSkill);
        if (item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator)
            return IndicatorText + (target is not null && catalog.FindSkill(target.source_key) is { } skill && !CardRules.IsResponse(skill)
                ? $"\n当前指向：{skill.skill_name}。" : "\n当前未指向可用招式。" );
        var source = catalog.FindRewrite(item.source_key);
        return source is null ? "此化法来源暂不可用。" : source.get_board_display_name() + "\n" + PlayerInfoText.Display(source.description)
            + (target is not null && source.target_keys.Contains(target.source_key)
                ? $"\n已强化：{catalog.FindSkill(target.source_key)?.skill_name}。" : "\n未生效：请指向共边相邻的适用招式。" );
    }
}
