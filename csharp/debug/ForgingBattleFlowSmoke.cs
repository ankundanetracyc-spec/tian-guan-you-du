using Godot;
using System.Reflection;
using GArray = Godot.Collections.Array;
using GSkills = Godot.Collections.Array<ActiveSkillData>;

/// <summary>地点器作实际开炼→行囊→装备→正式保存容器→正式战斗场景及队列伤害。</summary>
public partial class ForgingBattleFlowSmoke : Node
{
    private const string Slash = "res://resources/activeskills/剑修基础/斩剑.tres";
    private const string Punch = "res://resources/activeskills/应敌.tres";
    private const string Save = "user://tests/forging_battle_flow_20261104.tres";
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private int checks;
    private static object? Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, args);
    private static void Field(object target, string field, object value) => target.GetType().GetField(field, Hidden)!.SetValue(target, value);
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); GD.Print("PASS " + label); checks++; }
    private async Task Frames(int n = 2) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        Character? owner = null, restored = null;
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--read-forged-flow"))
            {
                var save = ResourceLoader.Load<PersistentGameSave>(Save, cacheMode: ResourceLoader.CacheMode.Ignore)!;
                Check(save?.player_data?.artifact_inventory.Count == 1 && save.player_data.forging_records.Count == 1, "跨进程恢复实际炼制产物与记录");
                restored = new Character(); AddChild(restored); restored.load_from_data(save!.player_data!);
                var savedItem = restored.get_equipped_forged_sword()!;
                Check(savedItem is not null && restored.resolve_combat_skill(Slash)?.forged_weapon_instance_id == savedItem.instance_id, "跨进程恢复已装备剑及普通剑招来源");
                await VerifyBattle(restored);
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Save));
                GD.Print("FORGING_BATTLE_FLOW_RESTART_OK " + checks); GetTree().Quit(); return;
            }
            var model = new ProcessForgingTrial(); var draft = ForgingSmokeFixtures.SmallDraft(model);
            owner = ForgingSmokeFixtures.Actor(model, draft); AddChild(owner);
            var human = (HumanData)owner.entity_data!;
            human.forging_proficiency = draft.Proficiency; owner.max_spirit_perception = 10000; human.current_world_stamina = 100;
            Check(owner.get_spatial_inventory().All(i => i.IsPlaced), "开炼前投料及衣着真实摆入行囊");
            var main = new EastWoodsActionHarness(); AddChild(main);
            var layer = new CanvasLayer { Name = "SceneLayer" }; main.AddChild(layer);
            var overview = GD.Load<PackedScene>("res://tscn/world/bai_chuan/YuxuXianzongOverview.tscn").Instantiate<Control>();
            overview.Name = "YuxuXianzongOverview"; layer.AddChild(overview);
            var facilities = new LocationCraftingPanel(); main.AddChild(facilities);
            typeof(MainScene).GetField("_locationCrafting", Hidden)!.SetValue(main, facilities);
            typeof(MainScene).GetField("player", Hidden)!.SetValue(main, owner);
            human.sect_affiliation_id = "yuxu_xianzong"; human.sect_current_rank = "外门弟子";
            var action = typeof(MainScene).GetMethod("on_yuxu_life_action_requested", Hidden)!;
            action.Invoke(main, ["tiangong_forging", "tiangong"]);
            Check(!facilities.Visible, "非天工峰拒绝器作入口");
            overview.Show(); overview.Call("_on_peak_pressed", "tiangong");
            typeof(MainScene).GetField("_player_current_yuxu_peak_id", Hidden)!.SetValue(main, "tiangong");
            overview.GetNode<Control>("PeakInterior").Call("set_sect_duty_context", true, "");
            action.Invoke(main, ["tiangong_forging", "tiangong"]);
            var room = facilities.GetChildren().OfType<ProcessForgingScene>().Single();
            Field(room, "_draft", draft); Call(room, "Refresh");
            var stock = owner.GetForgingStock(model); var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            double before = clock.get_precise_game_hours();
            ((Dictionary<string, Button>)typeof(ProcessForgingScene).GetField("_buttons", Hidden)!.GetValue(room)!)["craft"].EmitSignal(Button.SignalName.Pressed);
            Check(human.artifact_inventory.Count == 1 && human.forging_records.Count == 1, "点击实际开炼按钮成器入包并生成记录");
            var item = human.artifact_inventory.Single();
            Check(ForgedArtifactInventory.TryRead(item, out var artifact), "实际产物保留完整快照");
            Check(draft.Inputs.GroupBy(i => i.Material).All(g => owner.GetForgingStock(model)[g.Key] == stock[g.Key] - g.Sum(i => i.Quantity)), "地点开炼按跨岗合计扣真实投料");
            Check(owner.current_world_stamina == 100 - artifact.CraftingEnergy && Math.Abs(clock.get_precise_game_hours() - before - artifact.CraftingHours * 2) < .001, "地点开炼同步扣精力并按时辰推进历法");
            Check(owner.get_spatial_inventory().All(i => i.IsPlaced) && human.equipped_artifact_ids.Count == 0, "成品占地合法且没有自动装备");
            facilities.ReturnToLocation(); facilities.ReturnToLocation(); await Frames();
            Check(!facilities.Visible && overview.GetNode<Control>("PeakInterior").Visible, "炼器详情和器作逐层返回天工峰"); main.QueueFree();
            Check(owner.equip_artifact(item.instance_id) && owner.get_used_artifact_spirit() == 0, "实际产物可装备且手持不占灵识");
            var swordNode = owner.get_skill_node_configs().Single(n => n.get_skill_paths().Contains(Slash));
            human.unlocked_skill_node_ids.Add(swordNode.node_id); // 夹具人物已参悟既有剑式节点，不改学习规则。
            Check(owner.set_combat_skill_slot_paths(new() { Slash, Punch }), "已学普通剑招及拳脚手动装槽");
            Check(owner.resolve_combat_skill(Slash)!.forged_weapon_instance_id == item.instance_id && GD.Load<ActiveSkillData>(Slash).forged_weapon_instance_id.Length == 0, "普通剑招引用本剑且不修改共享技能资源");
            DirAccess.MakeDirRecursiveAbsolute("user://tests");
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = human }, Save) == Error.Ok, "实际产物装备与技能位置写入正式保存容器");
            var saved = ResourceLoader.Load<PersistentGameSave>(Save, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            restored = new Character(); AddChild(restored); restored.load_from_data(saved.player_data!);
            Check(restored.get_equipped_forged_sword()?.instance_id == item.instance_id && restored.resolve_combat_skill(Slash)?.forged_weapon_instance_id == item.instance_id, "读取存档重建器物及剑招来源");
            await VerifyBattle(restored);
            Check(restored.unequip_artifact(item.instance_id) && restored.resolve_combat_skill(Slash)!.forged_weapon_instance_id.Length == 0, "卸下后撤销普通剑招的用器来源");
            await VerifyBattle(restored, equipped: false);
            GD.Print("FORGING_BATTLE_FLOW_OK " + checks); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
        finally
        {
            if (IsInstanceValid(owner)) owner!.Free();
            if (IsInstanceValid(restored)) restored!.Free();
        }
    }
    private async Task VerifyBattle(Character owner, bool equipped = true)
    {
        var item = owner.get_equipped_forged_sword();
        ProcessForgingTrial.Artifact? artifact = null;
        if (item is not null) { ForgedArtifactInventory.TryRead(item, out var read); artifact = read; }
        var snapshot = owner.get_snapshot_data(); snapshot.faction = 1;
        var opponent = ForgedEquipmentPreview.Baseline(); opponent.uid = "forging-flow-enemy"; opponent.faction = 2;
        var character = ForgedEquipmentPreview.CharacterScene();
        var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
        Field(transition, "pending_attackers", new Godot.Collections.Array<PackedScene> { character });
        Field(transition, "pending_defenders", new Godot.Collections.Array<PackedScene> { character });
        Field(transition, "pending_attacker_data", new GArray { snapshot });
        Field(transition, "pending_defender_data", new GArray { opponent });
        Field(transition, "_spectatorBattleActive", true);
        var scene = GD.Load<PackedScene>("res://tscn/battle/BattleScene.tscn").Instantiate<BattleScene>();
        var manager = scene.GetNode<BattleManager>("BattleMgr"); manager.isolated_trial_battle = true;
        AddChild(scene); await Frames(8); manager.SetProcess(false); manager.set_spectator_battle(true);
        Check(manager.player_units.Count == 1 && manager.enemy_units.Count == 1 && snapshot.trial_forged_equipment_json.Length == 0, "正式战斗场景消费正式装备包且无试器数据");
        var unit = (Creature)manager.player_units[0]; var enemy = (Creature)manager.enemy_units[0];
        var skills = (GSkills)Call(manager, "LoadCombatSkills", unit)!;
        var slash = skills[0]; var punch = skills[1];
        float expectedBonus = (float)(artifact?.Effects.Where(e => e.CarrierId == "base" && e.AtomId == "physical_damage_percent").Sum(e => e.Value) ?? 0) / 100;
        if (equipped) Check(expectedBonus > 0 && artifact is { Sharpness: > 0 }, "实际小炉产物具有正数物伤加成及锋锐");
        else Check(expectedBonus == 0 && artifact is null, "卸下后的战斗快照没有原器物收益");
        Check(Math.Abs(CardRules.Number(unit, "forged_physical_bonus") - expectedBonus) < .000001, "正式战斗保留真实物伤加成且未被试器清零");
        Check(unit.physical_defense == snapshot.physical_defense, "正式战斗不重复叠加已计入人物的装备物防");
        int sharpness = artifact?.Sharpness ?? 0;
        Check(ForgedEquipmentLoadout.WeaponIgnore(unit, slash) == sharpness && ForgedEquipmentLoadout.WeaponIgnore(unit, punch) == 0, "普通手持剑招应用本剑锋锐且拳脚不享受");
        Check(CardPresentation.Face(slash, unit).Contains("用器：") == equipped, "技能展示随实际用器来源显示或撤销锋锐说明");
        var rawSlash = GD.Load<ActiveSkillData>(Slash);
        Check(ForgedEquipmentLoadout.WeaponIgnore(unit, rawSlash) == 0, "剑关键词不能代替实际用器来源");
        var wrong = ForgedEquipmentLoadout.CopySkill(slash); wrong.forged_weapon_instance_id = "unrelated-sword";
        Check(ForgedEquipmentLoadout.WeaponIgnore(unit, wrong) == 0, "其他剑器的来源不能借用本剑");
        foreach (var flag in new[] { "magic", "soul", "blood", "derived", "no-tag" })
        {
            var excluded = ForgedEquipmentLoadout.CopySkill(slash);
            if (flag == "blood") excluded.combat_is_blood_arm_segment = true;
            else if (flag == "derived") excluded.combat_is_gongfa_bonus_damage_segment = true;
            else if (flag == "no-tag") excluded.tags.Clear();
            else excluded.damage_type = flag;
            Check(ForgedEquipmentLoadout.WeaponIgnore(unit, excluded) == 0, "锋锐排除 " + flag);
        }
        unit.current_stamina = 6; unit.current_mana = unit.max_mana; unit.critical = 0; enemy.dodge = 0; enemy.crit_resist = 100000;
        enemy.physical_defense = sharpness + 10; enemy.physical_damage_reduction = 0; enemy.shield = 0;
        float raw = BattleActionResolver.preview_damage(unit, slash);
        int expectedDamage = BattleActionResolver.apply_defense(Math.Max(1, Mathf.RoundToInt(raw)), "physical", enemy, slash.physical_defense_ignore_ratio, sharpness);
        GD.Seed(525); var hit = BattleActionResolver.resolve_hit(unit, enemy, slash, evaded_override: false);
        Check(hit.DamageBeforeShield == expectedDamage, "真实伤害结算使用装备增伤及锋锐穿防");
        var stale = ForgedEquipmentLoadout.CopySkill(slash); stale.forged_weapon_instance_id = ""; stale.forged_flat_ignore = 1000;
        GD.Seed(525); var unbound = BattleActionResolver.resolve_hit(unit, enemy, stale, evaded_override: false);
        Check(unbound.DamageBeforeShield == BattleActionResolver.apply_defense(Math.Max(1, Mathf.RoundToInt(raw)), "physical", enemy, stale.physical_defense_ignore_ratio, 0),
            "旧固定穿防字段不能绕过来源校验或重复追加锋锐");
        var state = manager.get_battle_unit(unit); state.skill_queue.Clear(); state.segment_action_index = 0;
        var queued = new BattleQueuedSkill(slash, enemy); state.skill_queue.Add(queued);
        Field(manager, "_segmentPhase", BattleManager.SegmentPhase.Executing); Field(manager, "_segmentNumber", 1);
        manager.active_unit = unit; manager.battle_running = true;
        int health = enemy.current_health; GD.Seed(525);
        Call(manager, "ResolveSegmentAction", unit, queued);
        var deadline = Time.GetTicksMsec() + 6000;
        while (!queued.completed && Time.GetTicksMsec() < deadline) await Frames(1);
        Check(queued.completed && enemy.current_health == health - expectedDamage && unit.current_stamina == 6 - slash.energy_cost, "实际队列执行伤害与预演一致并支付原技能费用");
        Check(unit.used_spirit_perception == snapshot.used_spirit_perception, "普通手持剑招不新增灵识占用");
        Call(manager, "FinishArtifacts"); scene.QueueFree(); await Frames(3);
        GC.Collect(); GC.WaitForPendingFinalizers();
    }
}
