using Godot;
using Godot.Collections;

/// <summary>即时队列战斗的防御结算冒烟检查。</summary>
public partial class InstantBattleDamageSmoke : Node
{
    private int _checks;

    public override void _Ready() => CallDeferred(nameof(run));

    private void check(bool condition, string label)
    {
        if (!condition) throw new System.Exception(label);
        _checks++;
    }

    private static StatusEffect make_status(string id, int cap = 8) => new()
    {
        effect_id = id,
        name = id,
        max_stacks = cap,
        duration_type = StatusEffect.DurationType.Trigger,
        duration_default = -1
    };

    private static ulong no_critical_seed()
    {
        for (ulong seed = 1; seed < 100000; seed++)
        {
            GD.Seed(seed);
            var noCriticals = true;
            for (var segment = 0; segment < 3; segment++)
            {
                _ = GD.Randf(); // 每段先进行命中判定。
                if (GD.Randf() < .01f) noCriticals = false;
            }
            if (noCriticals) return seed;
        }
        throw new System.Exception("无法为实时结算冒烟检查选取固定随机种子");
    }

    private Creature make_defender(float baseReduction = 0, int physicalDefense = 0)
    {
        var defender = new Creature
        {
            entity_data = new HumanData { physical_damage_reduction = baseReduction, physical_defense = physicalDefense }
        };
        AddChild(defender);
        return defender;
    }

    private async void run()
    {
        try
        {
            var plain = make_defender();
            check(BattleActionResolver.apply_defense(100, "physical", plain) == 100, "无减免时保持原伤害");

            var layered = make_defender(.20f);
            var tempering = new Dictionary { ["wound_physical_reduction_pp_per_stack"] = 1f };
            layered.combat_tempering_by_gongfa["内景损益篇"] = tempering;
            layered.add_status(make_status("心法:内景损益", 1), -1, 1, "smoke");
            layered.add_status(make_status("内伤", 4), -1, 4, "smoke");
            layered.add_status(make_status("外伤", 4), -1, 4, "smoke");
            check(layered.get_status_stacks("内伤") + layered.get_status_stacks("外伤") == 8, "内外伤层数读取");
            check(BattleActionResolver.apply_defense(100, "physical", layered) == 74, "20%与8%独立减伤乘算为26.4%");

            var noHeart = make_defender(.20f);
            noHeart.combat_tempering_by_gongfa["内景损益篇"] = tempering;
            noHeart.add_status(make_status("内伤", 4), -1, 4, "smoke");
            noHeart.add_status(make_status("外伤", 4), -1, 4, "smoke");
            check(BattleActionResolver.apply_defense(100, "physical", noHeart) == 80, "心法未生效时不启用内景淬炼减伤");

            var capped = make_defender(.90f);
            capped.combat_tempering_by_gongfa["内景损益篇"] = tempering;
            capped.add_status(make_status("心法:内景损益", 1), -1, 1, "smoke");
            capped.add_status(make_status("内伤", 4), -1, 4, "smoke");
            capped.add_status(make_status("外伤", 4), -1, 4, "smoke");
            check(BattleActionResolver.apply_defense(100, "physical", capped) == 10, "百分比减伤合计封顶90%");

            var armored = make_defender(0, 100);
            check(BattleActionResolver.apply_defense(100, "physical", armored) == 1, "正物理伤害经过物防后保留最低值");
            check(BattleActionResolver.apply_defense(0, "physical", armored) == 0, "化解完全抵消后不强制造成伤害");
            check(BattleActionResolver.apply_defense(100, "soul", capped) == 100, "物理防御不减免神魂伤害");

            await verify_skill_executor_and_buff_display();

            GD.Print($"INSTANT_BATTLE_DAMAGE_SMOKE: {_checks} checks passed");
            GetTree().Quit();
        }
        catch (System.Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task verify_skill_executor_and_buff_display()
    {
        var ui = new CanvasLayer { Name = "UILayer" }; AddChild(ui);
        var manager = new BattleManager { Name = "BattleMgr" };
        AddChild(manager);

        var attacker = new Creature
        {
            entity_data = new HumanData { physique = 30, soul = 10, body_proficiency = 10, precision = 10000, critical = 0, crit_damage_mult = 1.5f, max_health = 1000, current_health = 1000 },
            faction = (int)WorldEntity.Faction.PLAYER
        };
        AddChild(attacker);
        var defender = make_defender(.20f);
        defender.faction = (int)WorldEntity.Faction.ENEMY;
        defender.dodge = 0;
        defender.physique = 0;
        defender.soul = 0;
        defender.physical_defense = 0;
        defender.crit_resist = 1000000;
        defender.shield = 3;
        defender.max_health = 1000;
        defender.current_health = 1000;
        defender.combat_tempering_by_gongfa["内景损益篇"] = new Dictionary { ["wound_physical_reduction_pp_per_stack"] = .5f };
        defender.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/内伤.tres")!, -1, 4, "smoke", 4, "伤势");
        defender.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/外伤.tres")!, -1, 2, "smoke", 4, "伤势");
        defender.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/化解.tres")!, -1, 246, "smoke");

        manager.player_units.Add(attacker);
        manager.enemy_units.Add(defender);
        manager.all_units.Add(attacker);
        manager.all_units.Add(defender);
        var attackerState = new BattleUnit();
        attackerState.setup(attacker, null);
        var defenderState = new BattleUnit();
        defenderState.setup(defender, null);
        manager.battle_units[attacker] = attackerState;
        manager.battle_units[defender] = defenderState;
        manager.gongfa_combat.Begin(attacker);
        manager.gongfa_combat.Begin(defender);

        var innerScenePower = GD.Load<ActiveSkillData>("res://resources/activeskills/内景损益篇/内景损益.tres")!;
        check(manager.gongfa_combat.Utility(defender, innerScenePower), "内景心法由战斗被动入口生效");
        var youhai = GD.Load<StatusEffect>("res://resources/battle/statuses/游骸.tres")!;
        attacker.add_status(youhai, -1, 4, "smoke");
        var toshen = GD.Load<ActiveSkillData>("res://resources/activeskills/乘物游骸真经/托身.tres")!;
        for (var i = 0; i < 4; i++) check(manager.gongfa_combat.Utility(attacker, toshen), $"托身消耗游骸并计入第 {i + 1} 层");
        check(attacker.GetMeta("spent_youhai").AsInt32() == 4, "本场已消耗游骸驱动冯虚三段");

        var skill = GD.Load<ActiveSkillData>("res://resources/activeskills/乘物游骸真经/冯虚.tres")!.Duplicate() as ActiveSkillData
            ?? throw new System.Exception("冯虚技能资源加载失败");
        // 维持真实技能身份和执行入口，只把伤害数值固定，便于逐段核算。
        skill.damage_formula = "";
        skill.base_damage = 126;
        skill.damage_bonuses.Clear();
        var action = new BattleQueuedSkill(skill, defender);
        attackerState.skill_queue.Add(action);
        manager.SetProcess(false); manager.battle_running = true;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(BattleManager).GetField("_resolvingUnit", flags)!.SetValue(manager, attacker);
        typeof(BattleManager).GetField("_segmentPhase", flags)!.SetValue(manager, BattleManager.SegmentPhase.Executing);

        GD.Seed(no_critical_seed());
        manager.GetChildren().OfType<SkillExecutor>().Single().execute_skill(attacker, skill, defender);
        var settled = false;
        var deadline = Time.GetTicksMsec() + 6000;
        while (Time.GetTicksMsec() < deadline)
        {
            if (action.completed) { settled = true; break; }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        check(settled, "SkillExecutor 完整结算后返回实时队列");
        check(defender.get_status_stacks("化解") == 0, "三段攻击共用同一化解池并按段消耗");
        check(defender.shield == 0, "减免后伤害再由护盾吸收");
        check(defender.current_health == 901, "内景减伤逐段读取最新伤势，结算为 99 点气血伤害");
        check(defender.get_status_stacks("外伤") == 4, "穿透护盾的两段伤害各生成一层外伤");

        for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var overlay = manager.GetChildren().OfType<BattleUnitOverlay>().FirstOrDefault();
        PanelContainer? heartBadge = null;
        if (overlay is not null)
            foreach (var row in overlay.FindChildren("Statuses", "HFlowContainer", true, false).OfType<HFlowContainer>())
            {
                // 统一悬停已不使用 Godot TooltipText；按真实专属图标识别徽记。
                heartBadge = row.GetChildren().OfType<PanelContainer>().FirstOrDefault(b =>
                    (b.GetChild(0) as TextureRect)?.Texture?.ResourcePath == "res://textures/ui/combat/status_icons/心法_内景损益_v1.png");
                if (heartBadge is not null) break;
            }
        var heartIcon = heartBadge?.GetChild(0) as TextureRect;
        check(defender.get_status_stacks("心法:内景损益") == 1 && heartBadge is not null && heartIcon?.Texture is not null, "常驻内景心法实际进入 Buff 栏且显示专属图标");
    }
}
