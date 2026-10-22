using Godot;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;

public partial class ForgedEquipmentSmoke : Node
{
    private const string Evidence = "res://docs/美术规范/单器装备与锋锐试炼_20261022/";
    private int _checks;
    private void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException(label); }
    private static string Data(object value) => JsonSerializer.Serialize(value);
    private static object? Invoke(BattleManager manager, string method, params object[] args) => typeof(BattleManager).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(manager, args);
    private static void Set(BattleManager manager, string field, object value) => typeof(BattleManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(manager, value);
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var model = new ProcessForgingTrial(); var session = new ProcessForgingTrial.Session(model.Parameters.Profiles.Keys);
            var artifact = session.Submit(model, model.ExampleDraft(1), 1).Product!;
            var equipment = new ForgedEquipmentLoadout(artifact);
            var baseline = ForgedEquipmentPreview.Baseline(); string before = Data(artifact);
            var attack = baseline.combat_resolved_skills[0]; var blood = baseline.combat_resolved_skills[2]; var guard = baseline.combat_resolved_skills[3];
            Check(equipment.SetSwordUse(attack, true) && !equipment.SetSwordUse(guard, true), "只有物理攻伐可明确用剑");
            var blockEffect = artifact.Effects.Single(x => x.AtomId == "defuse_flat" && x.CarrierId == "binding");
            var ignoreEffect = artifact.Effects.Single(x => x.AtomId == "physical_defense_ignore_ratio" && x.CarrierId == "binding");
            Check(equipment.Bind(blockEffect.EffectId, guard) && !equipment.Bind(blockEffect.EffectId, attack), "应对化解绑定条件");
            Check(!equipment.Bind(ignoreEffect.EffectId, attack) && equipment.Bind(ignoreEffect.EffectId, blood), "物理穿防化法检查气血关键词与单体伤害");
            var snapshot = equipment.BuildSnapshot(baseline);
            Check(baseline.physical_defense == 5 && baseline.trial_forged_equipment_json == "" && attack.forged_weapon_instance_id == "", "装配不修改基线或公共资源");
            Check(snapshot.combat_resolved_skills.Count == 10 && snapshot.combat_resolved_skills[8].combat_empty_slot, "固定十位及空位保留");
            Check(snapshot.combat_resolved_skills[0].forged_weapon_instance_id == artifact.InstanceId && snapshot.combat_resolved_skills[1].forged_weapon_instance_id == "", "用剑引用逐技能显式设置");
            Check(snapshot.combat_resolved_skills[2].physical_defense_ignore_ratio == (float)ignoreEffect.Value && snapshot.combat_resolved_skills[3].artifact_block_bonus == (int)blockEffect.Value, "两种化法独立生效");
            Check(snapshot.combat_resolved_skills[0].energy_cost == attack.energy_cost && snapshot.combat_resolved_skills[0].target_type == attack.target_type, "费用目标不变");
            bool denied = false; try { equipment.BuildSnapshot(snapshot); } catch (InvalidOperationException) { denied = true; } Check(denied, "禁止在已装备快照上叠第二器");
            // 真实秘籍化法先生成，后追加器物化法。
            var owner = new Character { entity_data = new HumanData { unique_id = "forging-rewrite-owner", cultivation_realm = "云息九层", physique = 20, soul = 10, max_health = 250, current_health = 250 } }; AddChild(owner);
            var book = GD.Load<GongfaResource>("res://resources/gongfa/百骸衍枝化生宝册.tres"); var human = (HumanData)owner.entity_data!;
            human.learned_gongfa_paths.Add(book.ResourcePath); human.learned_gongfa_chapters[book.ResourcePath] = new Godot.Collections.Array<string>(book.get_sorted_chapters().Select(x => x.get_id()));
            human.main_gongfa_path = book.ResourcePath; owner.main_gongfa = book;
            var rewritten = owner.resolve_combat_skill(ForgedEquipmentPreview.SkillPaths[2])!;
            Check(rewritten.gongfa_rewrite_effect_ids.Contains("baihai_c2_cuishan"), "真实秘籍化法已生成");
            Check(equipment.Bind(ignoreEffect.EffectId, rewritten), "以秘籍化法后的技能来源重新绑定");
            var withRewrite = ForgedEquipmentPreview.Baseline(); withRewrite.combat_resolved_skills[2] = rewritten;
            var combined = equipment.BuildSnapshot(withRewrite).combat_resolved_skills[2];
            Check(combined.gongfa_rewrite_effect_ids.Contains("baihai_c2_cuishan") && combined.physical_defense_ignore_ratio > 0 && rewritten.physical_defense_ignore_ratio == 0, "秘籍化法和器物化法共存且不污染来源");
            owner.QueueFree();
            var preview = GD.Load<PackedScene>("res://tools/ForgedEquipmentPreview.tscn").Instantiate<ForgedEquipmentPreview>(); preview.Configure(artifact); GetTree().Root.AddChild(preview);
            for (int f = 0; f < 4; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var manager = preview.GetNode<BattleManager>("BattleMgr"); manager.SetProcess(false); manager.set_spectator_battle(true);
            var player = (Creature)manager.player_units[0]; var enemy = (Creature)manager.enemy_units[0];
            int defenseBonus = (int)artifact.Effects.Single(x => x.CarrierId == "base" && x.AtomId == "physical_defense").Value;
            Check(player.physical_defense == 5 + defenseBonus, "真实战斗快照附加装备物防");
            ForgedEquipmentLoadout.ApplyBattleBonuses(player, snapshot.trial_forged_equipment_json);
            Check(player.physical_defense == 5 + defenseBonus, "重复初始化不重复叠加");
            var sword = snapshot.combat_resolved_skills[0]; var punch = snapshot.combat_resolved_skills[1];
            Check(ForgedEquipmentLoadout.WeaponIgnore(player, sword) == artifact.Sharpness && ForgedEquipmentLoadout.WeaponIgnore(player, punch) == 0, "锋锐仅匹配已装备实例与明确用剑动作");
            var wrongSource = ForgedEquipmentLoadout.CopySkill(sword); wrongSource.forged_weapon_instance_id = "other-instance"; Check(ForgedEquipmentLoadout.WeaponIgnore(player, wrongSource) == 0, "失去来源引用不生效");
            foreach (string type in new[] { "magic", "soul" }) { var skill = ForgedEquipmentLoadout.CopySkill(sword); skill.damage_type = type; Check(ForgedEquipmentLoadout.WeaponIgnore(player, skill) == 0, "非物理伤害不读锋锐"); }
            var arm = ForgedEquipmentLoadout.CopySkill(sword); arm.combat_is_blood_arm_segment = true; Check(ForgedEquipmentLoadout.WeaponIgnore(player, arm) == 0, "血臂衍生段不借剑锋锐");
            enemy.physical_defense = 10; enemy.physical_damage_reduction = 0;
            Check(BattleActionResolver.apply_defense(20, "physical", enemy, 0, 5) == 15, "五锋锐恰好忽略五物防");
            Check(BattleActionResolver.apply_defense(20, "physical", enemy, .2f, 5) == 16 && enemy.physical_defense == 10, "先固定再比例且不永久减防");
            Check(BattleActionResolver.apply_defense(20, "physical", enemy, 0, 100) == 20, "物防最低为零不负防增伤");
            Check(BattleActionResolver.apply_defense(0, "physical", enemy, 0, 5) == 0, "化解完全抵消后不穿出伤害");
            float expected = sword.calculate_damage(player) * (1 + CardRules.Number(player, "forged_physical_bonus"));
            Check(Math.Abs(BattleActionResolver.preview_damage(player, sword) - expected) < 1e-4, "实际物理伤害读装备百分比");
            Check(CardRules.Block(snapshot.combat_resolved_skills[3], player) == CardRules.Block(guard, player) + (int)blockEffect.Value && CardRules.NormalCast(snapshot.combat_resolved_skills[3]) is null, "化解追加及空放分支分离");
            Check(CardPresentation.CombatEffectText(sword, player).Contains("锋锐") && CardPresentation.CombatEffectText(snapshot.combat_resolved_skills[3], player).Contains("器物化法"), "悬停保留用器和化法说明");
            ForgedEquipmentLoadout.ApplyBattleBonuses(player, "");
            Check(player.physical_defense == 5 && ForgedEquipmentLoadout.WeaponIgnore(player, sword) == 0 && CardRules.Number(player, "forged_physical_bonus") == 0, "卸下撤销全部基础收益与锋锐");
            Check(Data(artifact) == before && session.Artifacts.Count == 1, "战斗不改成器或试炼库存档案");
            // 通过真实队列支付与结算，而不只检查静态描述。
            ForgedEquipmentLoadout.ApplyBattleBonuses(player, snapshot.trial_forged_equipment_json);
            player.current_stamina = 6; player.current_mana = 3; player.critical = 0; enemy.dodge = 0; enemy.crit_resist = 100000;
            var loadouts = (System.Collections.Generic.Dictionary<GodotObject, Godot.Collections.Array<ActiveSkillData>>)typeof(BattleManager).GetField("_combatSkills", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            loadouts[player] = snapshot.combat_resolved_skills;
            Invoke(manager, "InitializeBattleSkillUses"); Invoke(manager, "ResetRoundSkillUses", player);
            var state = manager.get_battle_unit(player); state.skill_queue.Clear(); state.segment_action_index = 0; state.skill_queue.Add(new BattleQueuedSkill(sword, enemy));
            Set(manager, "_segmentPhase", BattleManager.SegmentPhase.Executing); Set(manager, "_segmentNumber", 1); manager.active_unit = player; manager.battle_running = true;
            int healthBefore = enemy.current_health; enemy.shield = 4; GD.Seed(525);
            var hit = BattleActionResolver.resolve_hit(player, enemy, sword); GD.Seed(525);
            Invoke(manager, "ResolveSegmentAction", player, state.skill_queue[0]);
            ulong completionDeadline = Time.GetTicksMsec() + 6000;
            while (!state.skill_queue[0].completed && Time.GetTicksMsec() < completionDeadline) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(state.skill_queue[0].completed && player.current_stamina == 6 - sword.energy_cost, $"真实行动完成且原费用仅支付一次：完成{state.skill_queue[0].completed}，体力{player.current_stamina}");
            Check(enemy.current_health == healthBefore - Math.Max(0, hit.DamageBeforeShield - 4), "实际伤害与预演一致且护盾正常吸收");
            var emptyGuard = new BattleQueuedSkill(snapshot.combat_resolved_skills[3], player);
            state.skill_queue.Clear(); state.skill_queue.Add(emptyGuard); state.segment_action_index = 0; player.current_stamina = 6;
            Invoke(manager, "ResolveSegmentAction", player, emptyGuard);
            completionDeadline = Time.GetTicksMsec() + 6000;
            while (!emptyGuard.completed && Time.GetTicksMsec() < completionDeadline) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(emptyGuard.completed && player.current_stamina == 6 && player.get_status_stacks("化解") == 0, "真实架招空放不支付费用也不领取化法追加");
            state.skill_queue.Clear(); state.segment_action_index = 0; var triggeredGuard = new BattleQueuedSkill(snapshot.combat_resolved_skills[3], player); state.skill_queue.Add(triggeredGuard);
            Check(manager.try_auto_response(player, enemy, "physical", ForgedEquipmentPreview.Baseline().combat_resolved_skills[0]), "真实来袭接口触发队首应对");
            Check(triggeredGuard.completed && player.current_stamina == 6 - guard.energy_cost && player.get_status_stacks("化解") == CardRules.Block(snapshot.combat_resolved_skills[3], player), "实际应对支付原费用并得到包含器物追加的化解");
            var controls = preview.GetNode<CanvasLayer>("UILayer");
            var swordToggle = (CheckBox)controls.FindChild("SwordUse0", true, false)!; swordToggle.ButtonPressed = true;
            foreach (string atom in new[] { "defuse_flat", "physical_defense_ignore_ratio" })
            {
                var pick = (OptionButton)controls.FindChild("Binding_" + atom, true, false)!; pick.Select(1); pick.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            }
            manager.set_spectator_battle(false);
            var apply = (Button)controls.FindChild("ApplyEquipment", true, false)!; apply.EmitSignal(Button.SignalName.Pressed);
            GD.Print("FORGED_UI_APPLIED");
            for (int f = 0; f < 4; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            manager.SetProcess(false); var equippedPlayer = manager.player_units[0];
            var currentSkills = (Godot.Collections.Array<ActiveSkillData>)Invoke(manager, "LoadCombatSkills", equippedPlayer)!;
            Check(ForgedEquipmentLoadout.WeaponIgnore(equippedPlayer, currentSkills[0]) == artifact.Sharpness && currentSkills[3].artifact_block_bonus > 0 && currentSkills[2].physical_defense_ignore_ratio > 0, "界面勾选及绑定按钮生成真实战斗装配");
            GD.Print("FORGED_UI_VERIFIED");
            controls.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text == "隐藏队列")?.EmitSignal(Button.SignalName.Pressed);
            await Capture("01_单器对练.png");
            var openSetup = (Button)controls.FindChild("OpenEquipmentSetup", true, false)!; openSetup.EmitSignal(Button.SignalName.Pressed); await Capture("02_试器装配.png"); openSetup.EmitSignal(Button.SignalName.Pressed);
            var equipToggle = (CheckBox)controls.FindChild("EquipSingle", true, false)!; equipToggle.ButtonPressed = false; apply.EmitSignal(Button.SignalName.Pressed);
            Check(CardRules.Number(manager.player_units[0], "physical_defense") == 5 && CardRules.Number(manager.player_units[0], "forged_physical_bonus") == 0, "界面卸下重开撤销收益");
            equipToggle.ButtonPressed = true; apply.EmitSignal(Button.SignalName.Pressed);
            Check(CardRules.Number(manager.player_units[0], "physical_defense") == 5 + defenseBonus, "界面反复重开不重复加物防");
            Invoke(manager, "finish_battle", false); Check(manager.GetMeta("isolated_trial_finished", false).AsBool() && !manager.battle_running, "对练结束只结算本地，不调用世界回传");
            preview.Close(); for (int f = 0; f < 3; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var workshop = GD.Load<PackedScene>("res://tscn/scene/ProcessForgingScene.tscn").Instantiate<ProcessForgingScene>(); GetTree().Root.AddChild(workshop);
            typeof(ProcessForgingScene).GetMethod("LoadExample", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(workshop, [1]);
            typeof(ProcessForgingScene).GetMethod("Craft", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(workshop, null);
            var workshopSession = (ProcessForgingTrial.Session)typeof(ProcessForgingScene).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(workshop)!;
            int workshopStock = workshopSession.Stock.Values.Sum();
            Check(!workshop.FindChildren("*", "Button", true, false).Cast<Button>().Any(b => b.Text.Contains("进入独立对练")), "工坊只显示物品，试器保留开发测试入口");
            Check(workshop.Visible && workshopSession.Stock.Values.Sum() == workshopStock && workshopSession.Artifacts.Count == 1, "成器展示不重复炼制或扣库存");
            workshop.QueueFree(); for (int f = 0; f < 3; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Calibrate(model);
            using (var file = Godot.FileAccess.Open(Evidence + "装备验收.md", Godot.FileAccess.ModeFlags.Write)) file.StoreString($"# 单器装备验收\n\n{_checks}项断言通过；真实成器、单器装配、运行时副本、秘籍合并、物防与物伤、来源匹配锋锐、固定/比例穿防、非物理/衍生段排除、护盾、原费用及真实队列结算。\n\n未接正式装备/存档、关键词常驻、触发、直施和御器。\n");
            GD.Print($"FORGED_EQUIPMENT_SMOKE_OK {_checks}"); CallDeferred(nameof(Finish));
        }
        catch (Exception e) { GD.PushError(e.ToString()); Input.SetCustomMouseCursor(null); GetTree().Quit(1); }
    }
    private void Finish()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Input.SetCustomMouseCursor(null); GetTree().Quit();
    }
    private void Calibrate(ProcessForgingTrial model)
    {
        var report = new StringBuilder("# 剑类真实材料标定记录\n\n当前公式v3；只统计实际结果，不将区间自动标为法器小档或正式平衡。\n\n| 投料基线 | 造诣 | 裂甲石份数 | H | S | 判阶 | 锋锐 | 实际效果 |\n| --- | ---: | ---: | ---: | ---: | --- | ---: | --- |\n");
        int legal = 0; double maxH = 0;
        foreach (int sample in new[] { 0, 1, 3 }) foreach (int z in new[] { 0, 20, 40, 60, 80, 100 }) foreach (int quantity in new[] { 1, 2, 4, 6, 8 })
        {
            var draft = model.ExampleDraft(sample); draft.Proficiency = z;
            draft.Inputs.Single(x => x.Role == "性材" && x.Material == "裂甲石").Quantity = quantity;
            if (!model.Inspect(draft).CanSubmit) continue;
            var a = model.Evaluate(draft).Product!; var g = a.Calculation.Assessment; legal++; maxH = Math.Max(maxH, g.SpiritLevel);
            report.AppendLine($"| {model.Parameters.Examples[sample].Name} | {z} | {quantity} | {g.SpiritLevel:F3} | {g.Stability:F4} | {a.Tier} | {a.Sharpness} | " + string.Join("；", a.Effects.Select(x => x.Route + " " + x.Description)) + " |");
        }
        report.AppendLine($"\n90组原始样本中合法{legal}组，最大H={maxH:F3}。非法位置组合保留拒绝，不自动删材料。此表用于检视来源、效力分散和成长，不据此穷举配方。当前目录覆盖有限，法器四小档仍待扩大材料层次覆盖后标定。\n");
        using var file = Godot.FileAccess.Open(Evidence + "数值标定.md", Godot.FileAccess.ModeFlags.Write); file.StoreString(report.ToString());
    }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        Input.WarpMouse(new(1880, 1040)); HoverInfoManager.HideAll(); await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screenshot = GetViewport().GetTexture().GetImage(); Check(screenshot.SavePng(Evidence + name) == Error.Ok, "对练截图保存");
    }
}

