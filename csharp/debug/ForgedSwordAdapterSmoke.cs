using Godot;
using System.Reflection;
using GSkills = Godot.Collections.Array<ActiveSkillData>;

public partial class ForgedSwordAdapterSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private int _checks;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private static object? Call(BattleManager manager, string name, params object[] args) => typeof(BattleManager).GetMethod(name, Hidden)!.Invoke(manager, args);
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var model = new ProcessForgingTrial();
            var owner = new Character(); AddChild(owner);
            var manager = new BattleManager { isolated_trial_battle = true }; AddChild(manager); manager.SetProcess(false); manager.set_spectator_battle(true);
            manager.all_units.Add(owner); manager.player_units.Add(owner); manager.battle_units[owner] = new BattleUnit(); manager.action_gauges[owner] = 0;
            var loadouts = (Dictionary<GodotObject, GSkills>)typeof(BattleManager).GetField("_combatSkills", Hidden)!.GetValue(manager)!;
            var seenSkills = 0;
            for (var i = 0; i <= model.Parameters.Examples.Count; i++)
            {
                var draft = model.ExampleDraft(Math.Min(i, model.Parameters.Examples.Count - 1)); draft.Proficiency = 60;
                var result = model.Evaluate(draft); if (!result.Success) continue;
                var s = result.Product!; s.InstanceId = "arbitrary-name-" + i; s.Name = "同名测试剑";
                if (i == model.Parameters.Examples.Count)
                {
                    // 专用接口配置，不能混作生产公式产物。
                    s.BodyMaximum = 12; s.SpiritRequired = 2;
                    s.Effects.Add(new ProcessForgingTrial.Effect { EffectId = "smoke-summon", CarrierId = "summon", AtomId = "physical_damage_base", Value = 10 });
                }
                var item = ForgedArtifactInventory.Create(s);
                Check(owner.add_artifact_to_inventory(item) && owner.equip_artifact(item.instance_id), "不同投料剑可入包装备 " + i);
                Check(owner.get_equipped_forged_sword()?.instance_id == item.instance_id && owner.get_used_artifact_spirit() == 0, "实例替换与装备免费");
                var skills = owner.get_artifact_skills().ToList(); seenSkills += skills.Count;
                Check(skills.Count == ForgedSwordBattleAdapter.Skills(item).Count(), "只生成真实主动能力");
                Check(owner.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(skills.Select(ArtifactSkillFactory.Key))), "主动能力手动装槽");
                owner.max_spirit_perception = 100; owner.used_spirit_perception = 1; owner.current_stamina = 6; owner.current_mana = 3;
                var snapshot = owner.get_snapshot_data();
                Call(manager, "ResetArtifacts"); Call(manager, "InitializeArtifacts", owner, snapshot);
                loadouts[owner] = new GSkills(skills);
                Check(owner.used_spirit_perception == 1, "保留其他持续实体占用");
                foreach (var skill in skills)
                {
                    if (skill.artifact_instance_id.Length > 0)
                    {
                        var queued = new BattleQueuedSkill(skill, owner) { artifact_instance_id = item.instance_id, artifact_intent = ArtifactBattleRuntime.Intent.Summon };
                        Call(manager, "ResolveArtifactSummon", owner, queued);
                        Check(owner.used_spirit_perception == 1 + s.SpiritRequired, "召出真实器体及在场占用");
                    }
                    var before = owner.used_spirit_perception;
                    owner.max_spirit_perception = before;
                    Check(!manager.can_pay_skill_mana_cost(owner, skill), "施法峰值不足拒绝");
                    owner.max_spirit_perception = before + 1;
                    Check(manager.can_pay_skill_mana_cost(owner, skill), "额外一灵识可施法");
                    Call(manager, "ReserveForgedSpirit", owner, skill);
                    Check(owner.used_spirit_perception == before + 1, "施法临时登记");
                    Call(manager, "ReleaseForgedSpirit", owner); Call(manager, "ReleaseForgedSpirit", owner);
                    Check(owner.used_spirit_perception == before, "释放只还临时部分且幂等");
                }
                Call(manager, "FinishArtifacts"); Check(owner.used_spirit_perception == 1, "战斗结束释放在场占用");
                Check(owner.unequip_artifact(item.instance_id) && owner.get_combat_skill_slot_paths().All(string.IsNullOrEmpty), "卸下清空来源且保留槽位");
            }
            Check(seenSkills > 0, "实际投料形成主动能力");
            var persistedItem = owner.get_artifact_items().Last();
            owner.equip_artifact(persistedItem.instance_id);
            var persistedKeys = new Godot.Collections.Array<string>(owner.get_artifact_skills().Select(ArtifactSkillFactory.Key));
            owner.set_combat_skill_slot_paths(persistedKeys);
            DirAccess.MakeDirRecursiveAbsolute("user://tests");
            const string save = "user://tests/forged_sword_adapter.tres";
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = (HumanData)owner.entity_data! }, save) == Error.Ok, "保存装备与技能来源");
            var saved = ResourceLoader.Load<PersistentGameSave>(save, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            var restored = new Character(); AddChild(restored); restored.load_from_data(saved.player_data!);
            Check(restored.get_equipped_forged_sword()?.instance_id == persistedItem.instance_id && persistedKeys.All(key => restored.resolve_combat_skill(key) is not null), "读回按实例重建技能，不保存召出态");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
            if (OS.GetCmdlineUserArgs().Contains("--capture-equipment"))
            {
                var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(panel);
                panel.show_entity(restored);
                typeof(InfoPanel).GetMethod("render_new_page", Hidden)!.Invoke(panel, [4]);
                for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var shot = GetViewport().GetTexture().GetImage();
                Check(shot.SavePng("res://docs/美术规范/炼成剑通用适配_20261024/装备页.png") == Error.Ok, "正式装备页截图");
            }
            GD.Print("FORGED_SWORD_ADAPTER_OK " + _checks); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
