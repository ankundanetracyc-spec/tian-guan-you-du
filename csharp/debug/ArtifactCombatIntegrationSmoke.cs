using Godot;
using Godot.Collections;
using System.Reflection;

public partial class ArtifactCombatIntegrationSmoke : Node
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _checks;
    private void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); _checks++; }
    private static object? Invoke(BattleManager manager, string method, params object[] args) => typeof(BattleManager).GetMethod(method, Private)!.Invoke(manager, args);
    private static void Set(BattleManager manager, string field, object value) => typeof(BattleManager).GetField(field, Private)!.SetValue(manager, value);
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--read-artifact-save"))
            {
                var restored = ResourceLoader.Load<PersistentGameSave>("user://artifact_combat_restart.tres", cacheMode: ResourceLoader.CacheMode.Ignore)!;
                Check(restored.format_version == PersistentGameSave.CurrentFormatVersion && restored.player_data!.artifact_inventory.Count == 4, "新进程读回法器实例");
                Check(restored.player_data!.combat_skill_references[2].artifact_instance_id == "sample-outer-a", "新进程读回来源引用");
                Check(restored.player_data.artifact_inventory[0].form!.form_id == "mingfeng" && restored.player_data.artifact_inventory[0].combat_profile!.use_base_damage == 10, "新进程读回构型及效果配置");
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://artifact_combat_restart.tres"));
                GD.Print($"ARTIFACT_SAVE_RESTART_READ: {_checks} checks passed"); GetTree().Quit(); return;
            }
            var owner = new Character { entity_data = new HumanData { max_health = 100, current_health = 100, max_mana = 3, max_stamina = 6,
                current_stamina = 6, current_mana = 3, max_spirit_perception = 5, artifact_slot_count = 4, unique_id = "artifact-test-owner" } };
            AddChild(owner);
            var attack = ArtifactCombatSamples.Create(false, false, "sample-outer-a");
            var guard = ArtifactCombatSamples.Create(true, false, "sample-outer-b");
            var invalidFree = ArtifactCombatSamples.Create(false, false, "invalid-free");
            invalidFree.combat_profile!.use_energy_cost = 0; invalidFree.combat_profile.use_mana_cost = 0;
            Check(!ArtifactSkillFactory.IsConfigured(invalidFree), "无条件无限零费御使配置不开放");
            invalidFree.combat_profile.uses_per_round = 1;
            Check(ArtifactSkillFactory.IsConfigured(invalidFree), "明确限次零费可按总案配置");
            var inner = ArtifactCombatSamples.Create(false, true, "sample-inner-a");
            var innerGuard = ArtifactCombatSamples.Create(true, true, "sample-inner-b");
            foreach (var item in new[] { attack, guard, inner, innerGuard })
            { Check(owner.add_artifact_to_inventory(item), "样本进入测试行囊"); Check(owner.equip_artifact(item.instance_id), "测试法器可装备"); }
            Check(owner.get_used_artifact_spirit() == 0, "装备不占灵识且内炼不占灵识");
            var keys = new Array<string> { "res://resources/activeskills/应敌.tres", "", "artifact:" + attack.instance_id, "artifact:" + guard.instance_id };
            Check(owner.set_combat_skill_slot_paths(keys), "混合装配");
            var slots = owner.get_combat_skill_slot_paths();
            Check(slots.Count == 10 && slots[1] == "" && slots[2] == keys[2], "固定十位且空位不压缩");
            var innerSkill = owner.resolve_combat_skill(keys[0])!;
            Check(Mathf.IsEqualApprox(innerSkill.physical_defense_ignore_ratio, .2f), "内炼追加穿防");
            Check(ResourceLoader.Load<ActiveSkillData>(keys[0]).physical_defense_ignore_ratio == 0, "原资源不污染");
            Check(owner.resolve_combat_skill("res://resources/activeskills/架招.tres")!.artifact_block_bonus == 2, "守锋内炼追加化解");
            var book = ResourceLoader.Load<GongfaResource>("res://resources/gongfa/玄枢含章经.tres");
            var ownerData = (HumanData)owner.entity_data!;
            ownerData.learned_gongfa_paths.Add(book.ResourcePath);
            ownerData.learned_gongfa_chapters[book.ResourcePath] = new Array<string>(book.get_sorted_chapters().Select(chapter => chapter.get_id()));
            owner.main_gongfa = book; ownerData.main_gongfa_path = book.ResourcePath;
            Check(owner.bind_inner_artifact(innerGuard.instance_id, "res://resources/activeskills/架招.tres"), "可绑定当前开放秘籍化法后的架招");
            var merged = owner.resolve_combat_skill("res://resources/activeskills/架招.tres")!;
            Check(merged.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao") && merged.artifact_block_bonus == 2, "秘籍化法与法器化法共存");
            owner.unequip_artifact(innerGuard.instance_id);
            var withoutArtifact = owner.resolve_combat_skill("res://resources/activeskills/架招.tres")!;
            Check(withoutArtifact.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao") && withoutArtifact.artifact_block_bonus == 0, "卸下后保留秘籍化法");
            owner.equip_artifact(innerGuard.instance_id);
            var sameName = ArtifactCombatSamples.Create(false, false, "sample-same-name");
            owner.unequip_artifact(inner.instance_id); owner.remove_artifact_from_inventory(inner.instance_id);
            owner.add_artifact_to_inventory(sameName); owner.equip_artifact(sameName.instance_id);
            Check(owner.get_artifact_skills().Count() == 3, "同名实例分别生成临时技能");
            owner.unequip_artifact(sameName.instance_id);
            owner.unequip_artifact(attack.instance_id);
            Check(owner.get_combat_skill_slot_paths()[2] == "" && owner.get_combat_skill_slot_paths()[3] == keys[3], "撤销来源只清原位");
            owner.equip_artifact(attack.instance_id);
            Check(owner.get_combat_skill_slot_paths()[2] == "", "重装不自动恢复技能栏");
            owner.set_combat_skill_slot_paths(keys);
            var oldSave = new PersistentGameSave { format_version = 9, player_data = new HumanData { combat_skill_slots_configured = true,
                combat_skill_slot_paths = [keys[0], "", "res://resources/activeskills/架招.tres"] } };
            Check(oldSave.migrate_to_current_format() && oldSave.format_version == PersistentGameSave.CurrentFormatVersion, "v9沿迁移链迁到当前版本");
            Check(oldSave.player_data!.combat_skill_references.Count == 10 && oldSave.player_data.combat_skill_references[1].Key == "", "迁移保留空位");
            var savePath = "user://artifact_v10_smoke.tres";
            Check(ResourceSaver.Save(oldSave, savePath) == Error.Ok, "v10写盘");
            var reread = ResourceLoader.Load<PersistentGameSave>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(reread.player_data!.combat_skill_references[2].Key.EndsWith("架招.tres"), "独立资源重读来源顺序");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));

            var manager = new BattleManager { Name = "ArtifactBattleTestManager" };
            AddChild(manager); manager.SetProcess(false); manager.set_spectator_battle(true);
            var other = new Creature { entity_data = new HumanData { current_health = 200, max_health = 200, physical_defense = 10,
                precision = 100, critical = 0, crit_resist = 100000, max_mana = 3, max_stamina = 6 }, faction = 2 };
            AddChild(other); other.faction = 2; owner.faction = 1;
            manager.all_units.Add(owner); manager.all_units.Add(other); manager.player_units.Add(owner); manager.enemy_units.Add(other);
            manager.battle_units[owner] = new BattleUnit(); manager.battle_units[other] = new BattleUnit();
            manager.action_gauges[owner] = 0f; manager.action_gauges[other] = 0f;
            Set(manager, "_mainPlayerUnit", owner); Set(manager, "_segmentPhase", BattleManager.SegmentPhase.Planning);
            var snapshot = owner.get_snapshot_data();
            var battleClone = new Character(); AddChild(battleClone); battleClone.SetMeta("artifact_battle_clone", true); battleClone.apply_battle_data(snapshot);
            Check(battleClone.max_spirit_perception == snapshot.max_spirit_perception, "真实快照应用复制灵识容量");
            Check(!battleClone.set_combat_skill_slot_paths(keys) && !battleClone.unequip_artifact(attack.instance_id), "战斗角色禁止换栏与换装");
            battleClone.QueueFree();
            Invoke(manager, "InitializeArtifacts", owner, snapshot);
            var loadout = (Array<ActiveSkillData>)Invoke(manager, "LoadCombatSkills", owner)!;
            var loadouts = (System.Collections.Generic.Dictionary<GodotObject, Array<ActiveSkillData>>)typeof(BattleManager).GetField("_combatSkills", Private)!.GetValue(manager)!;
            loadouts[owner] = loadout; loadouts[other] = [];
            Invoke(manager, "InitializeBattleSkillUses"); Invoke(manager, "ResetRoundSkillUses", owner);
            var outer = loadout[2]; var outerGuard = loadout[3];
            Check(loadout.Count == 10 && loadout[1].combat_empty_slot && outer.artifact_instance_id == attack.instance_id, "战斗栏来源与空位正确");
            var state = manager.get_battle_unit(owner);
            state.skill_queue.Add(new BattleQueuedSkill(outer, null)); state.skill_queue.Add(BattleQueuedSkill.Rest()); state.skill_queue.Add(new BattleQueuedSkill(outer, other));
            Invoke(manager, "PreviewArtifactQueue", owner);
            Check(state.skill_queue[0].artifact_intent == ArtifactBattleRuntime.Intent.Summon && state.skill_queue[2].artifact_intent == ArtifactBattleRuntime.Intent.Use, "首次召唤后续御使");
            owner.max_spirit_perception = 4;
            Check(!manager.can_plan_artifact_summon(owner, outerGuard), "AI预测已排召唤的灵识预算");
            owner.max_spirit_perception = 5;
            Check(manager.can_plan_artifact_summon(owner, outerGuard), "AI可预测两件法器恰好满容量");
            Check(!manager.is_skill_ready(owner, ArtifactSkillFactory.CreateOuter(attack)), "同来源未装配技能副本不可伪装排队");
            Check(state.skill_queue[0].execution_skill.mana_cost == 0 && state.skill_queue[2].execution_skill.mana_cost == 1, "分支费用分离");
            owner.current_stamina = 0; owner.current_mana = 3;
            Check(!(bool)Invoke(manager, "CanResolveSegmentAction", owner, state.skill_queue[0], false)!, "召唤资源不足拒绝");
            Check(!(bool)Invoke(manager, "CanResolveSegmentAction", owner, state.skill_queue[2], false)!, "后续御使不变召唤");
            owner.current_stamina = 6; owner.current_mana = 3; owner.max_spirit_perception = 0;
            Check(!(bool)Invoke(manager, "CanResolveSegmentAction", owner, state.skill_queue[0], false)!, "零容量拒绝召唤");
            owner.max_spirit_perception = 5;
            Check((bool)Invoke(manager, "CanResolveSegmentAction", owner, state.skill_queue[0], false)!, "合法召唤可执行");
            outer.cooldown_rounds = 1; outer.uses_per_round = 1;
            Invoke(manager, "ResetRoundSkillUses", owner);
            Invoke(manager, "ResolveArtifactSummon", owner, state.skill_queue[0]);
            Check(manager.get_skill_cooldown_rounds(owner, outer) == 0 && (bool)Invoke(manager, "HasAvailableSegmentSkillUses", owner, outer)!, "召唤不启动御使冷却或消耗御使次数");
            outer.cooldown_rounds = 0; outer.uses_per_round = 0;
            var body = manager.get_target_candidates().OfType<ArtifactBody>().Single();
            Check(owner.used_spirit_perception == 2 && owner.current_stamina == 5 && owner.current_mana == 3, "召唤支付并在场占用");
            Check(!manager.all_units.Contains(body) && !manager.battle_units.ContainsKey(body), "器体不占行动列表");
            var foeAttack = new ActiveSkillData { base_damage = 3, damage_type = "physical", target_type = 2 };
            Check(BattleManager.ArtifactAcceptsSkill(body, other, foeAttack), "单攻可选器体");
            Check(!BattleManager.ArtifactAcceptsSkill(body, owner, foeAttack), "不能以敌方攻击选择友方器体");
            var soul = new ActiveSkillData { base_damage = 99, damage_type = "soul", target_type = 2 };
            Check(manager.damage_artifact_body(other, soul, body) == 0 && body.current_health == 12, "神魂不伤器体");
            Check(BattleActionResolver.preview_hit_chance(other, body) == 1 && BattleActionResolver.preview_crit_chance(other, body) == 0, "器体不闪避不暴击");
            manager.active_unit = other;
            Check(manager.pick_card_target(foeAttack, manager.get_unit_screen_rect(body).GetCenter()) == body, "真实目标拾取可选择敌方器体");
            Check(manager.damage_artifact_body(other, foeAttack, body) == 3 && body.current_health == 9, "器体直接承伤");
            Check(BattleActionResolver.apply_defense(20, "physical", other, .2f) == 12, "穿透只修正物防");
            state.skill_queue.Clear(); state.skill_queue.Add(new BattleQueuedSkill(outer, other)); Invoke(manager, "PreviewArtifactQueue", owner);
            Check(state.skill_queue[0].artifact_intent == ArtifactBattleRuntime.Intent.Use, "跨回合直接御使");
            outer.cooldown_rounds = 1; outer.uses_per_round = 1;
            Invoke(manager, "ResetRoundSkillUses", owner); Set(manager, "_segmentPhase", BattleManager.SegmentPhase.Executing); Set(manager, "_segmentNumber", 1);
            Invoke(manager, "StartSkillCooldown", owner, outer);
            Check(!(bool)Invoke(manager, "CanResolveSegmentAction", owner, state.skill_queue[0], false)!, "御使冷却与限次生效");
            outer.cooldown_rounds = 0; outer.uses_per_round = 0; Invoke(manager, "InitializeBattleSkillUses");
            Check(manager.depart_artifact(owner, attack.instance_id, "dispelled") && owner.used_spirit_perception == 0, "完好离场释放");
            Check(!(bool)Invoke(manager, "CanResolveSegmentAction", owner, state.skill_queue[0], false)!, "离场后本场禁用");
            state.skill_queue.Clear(); state.skill_queue.Add(new BattleQueuedSkill(outerGuard, owner)); Invoke(manager, "PreviewArtifactQueue", owner);
            Invoke(manager, "ResolveArtifactSummon", owner, state.skill_queue[0]);
            var guardBody = manager.get_target_candidates().OfType<ArtifactBody>().Single();
            Check(owner.used_spirit_perception == 3 && guardBody.current_health == 18, "守锋召唤器体与占用");
            manager.battle_running = true;
            state.skill_queue.Clear(); state.segment_action_index = 0;
            state.skill_queue.Add(new BattleQueuedSkill(outerGuard, owner)); Invoke(manager, "PreviewArtifactQueue", owner);
            owner.current_stamina = 6; owner.current_mana = 3;
            var beforeActualShield = owner.shield;
            Check((bool)Invoke(manager, "CanResolveSegmentAction", owner, state.skill_queue[0], false)!, "真实御使分支合法");
            Invoke(manager, "ResolveSegmentAction", owner, state.skill_queue[0]);
            for (var frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(owner.shield == beforeActualShield + 6 && owner.current_stamina == 5 && owner.current_mana == 2, "真实队列支付一次并结算护盾");
            Check(owner.used_spirit_perception == 3 && state.skill_queue[0].completed, "御使不重复占灵识且行动正常完成");
            // 群攻混合命中角色与器体；测试固定种子、低暴击，器体应精确承伤。
            var mixed = new ActiveSkillData { base_damage = 3, damage_type = "physical", target_type = 2, aoe_range = 1,
                status_effects = [new Dictionary { ["status_id"] = "外伤", ["stacks"] = 1 }] };
            var executorMixed = manager.GetChildren().OfType<SkillExecutor>().Single();
            owner.dodge = 0; other.precision = 10000;
            var healthBefore = owner.current_health; var bodyBefore = guardBody.current_health;
            executorMixed.execute_aoe_skill(other, mixed, Vector2I.Zero, manager);
            for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(guardBody.current_health == bodyBefore - 3, "混合群攻包含器体且不触发暴击防御");
            Check(owner.shield < beforeActualShield + 6 || owner.current_health < healthBefore, "混合群攻仍结算生灵目标");
            Check(manager.get_target_candidates().OfType<ArtifactBody>().Contains(guardBody), "群攻后未崩解器体仍在场");
            var fatal = new ActiveSkillData { base_damage = 99, damage_type = "magic", target_type = 2 };
            manager.damage_artifact_body(other, fatal, guardBody);
            Check(guardBody.Collapsed && owner.used_spirit_perception == 0, "崩解释放灵识");
            Check(manager.build_artifact_reports().Count == 2 && manager.build_artifact_reports().Any(report => report["collapsed"].AsBool()), "战损报告按实例生成");
            var transition = new BattleTransitionManager();
            transition.set_artifact_reports(manager.build_artifact_reports());
            Check(transition.consume_results()[3].AsGodotArray().Count == 2 && transition.consume_results()[3].AsGodotArray().Count == 0, "报告一次消费");
            transition.Free();
            var response = owner.resolve_combat_skill("res://resources/activeskills/架招.tres")!;
            loadout[6] = response; state.skill_queue.Clear(); state.segment_action_index = 0;
            state.skill_queue.Add(new BattleQueuedSkill(response, owner));
            owner.current_stamina = 6;
            var beforeBlock = owner.get_status_stacks("化解");
            Check(manager.try_auto_response(owner, other, "physical"), "实际队首应对触发");
            Check(owner.get_status_stacks("化解") == beforeBlock + CardRules.Block(response, owner), "实际应对包含法器化解追加");
            owner.consume_status_stacks("化解", owner.get_status_stacks("化解"));
            state.skill_queue.Clear(); state.segment_action_index = 0; state.skill_queue.Add(new BattleQueuedSkill(response, owner));
            Invoke(manager, "ResolveSegmentAction", owner, state.skill_queue[0]);
            for (var frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(owner.get_status_stacks("化解") == 0, "未应对空放不追加化解");
            // 通过真实执行器验证主动护盾及器体过滤，避免只测字段。
            var executor = manager.GetChildren().OfType<SkillExecutor>().Single();
            var shieldBefore = owner.shield;
            executor.execute_skill(owner, outerGuard, owner);
            for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(owner.shield == shieldBefore + 6, "真实执行器御使护盾");
            Invoke(manager, "ResetArtifacts"); Invoke(manager, "InitializeArtifacts", owner, snapshot);
            state.skill_queue.Clear(); state.segment_action_index = 0;
            state.skill_queue.Add(new BattleQueuedSkill(outer, owner)); Invoke(manager, "PreviewArtifactQueue", owner);
            owner.current_stamina = 6; owner.current_mana = 3;
            Invoke(manager, "ResolveArtifactSummon", owner, state.skill_queue[0]);
            owner.current_health = 0; Invoke(manager, "PruneDeadQueues");
            Check(owner.used_spirit_perception == 0 && manager.build_artifact_reports().Any(report => report["departure_reason"].AsString() == "owner_defeated"), "主人倒下使法器离场且释放占用");
            owner.current_health = 100;
            var persist = new HumanData { combat_skill_slots_configured = true, artifact_slot_count = 4,
                artifact_inventory = [attack, guard, inner, innerGuard], equipped_artifact_ids = [attack.instance_id, guard.instance_id, inner.instance_id, innerGuard.instance_id],
                combat_skill_references = [CombatSkillReference.FromKey(keys[0]), new(), CombatSkillReference.FromKey(keys[2]), CombatSkillReference.FromKey(keys[3])] };
            while (persist.combat_skill_references.Count < 10) persist.combat_skill_references.Add(new());
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = persist }, "user://artifact_combat_restart.tres") == Error.Ok, "法器与来源引用写入隔离测试存档");
            manager.QueueFree();
            GD.Print($"ARTIFACT_COMBAT_INTEGRATION_SMOKE: {_checks} checks passed");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
