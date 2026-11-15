using Godot;

public partial class ArtifactBondSmoke : Node
{
    private int _checks;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private static ArtifactInstanceData Item(string id, int sword, int defense) => ForgedArtifactInventory.Create(new ProcessForgingTrial.Artifact {
        InstanceId = id, Name = "器蕴专用测试剑", Category = "剑", Tier = "法器", BondContributions = ["惊鸿 +" + sword, "抱朴 +" + defense],
        Effects = [new() { CarrierId = "base", AtomId = "physical_defense", Value = 10 }] });
    public override async void _Ready()
    {
        try
        {
            var a = Item("bond-a", 2, 2); var b = Item("bond-b", 2, 2);
            Check(ArtifactBondRuntime.Aggregate([a, a]).Jinghong == 2, "实例去重");
            Check(ArtifactBondRuntime.Aggregate([a, b]).SwordDamageBonus == .2f, "同名不同实例累计4点总20%");
            Check(ArtifactBondRuntime.Ratio(1) == 0 && ArtifactBondRuntime.Ratio(2) == .1f && ArtifactBondRuntime.Ratio(3) == .1f && ArtifactBondRuntime.Ratio(8) == .2f, "档位与超额不重复叠加");
            var owner = new Character(); AddChild(owner); owner.add_artifact_to_inventory(a);
            Check(owner.get_artifact_bonds().Jinghong == 0, "仅持有不贡献");
            owner.equip_artifact(a.instance_id);
            Check(owner.physical_defense == 11 && owner.get_artifact_bonds().Baopu == 2 && owner.used_spirit_perception == 0, "装备抱朴提高完整角色物防且无灵识费");
            owner.refresh_gongfa_effects(); Check(owner.physical_defense == 11, "重算不叠加");
            var manager = new BattleManager { isolated_trial_battle = true }; AddChild(manager); manager.SetProcess(false);
            var initialize = typeof(BattleManager).GetMethod("InitializeArtifacts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var worldSnapshot = owner.get_snapshot_data();
            initialize.Invoke(manager, [owner, worldSnapshot]);
            Check(owner.physical_defense == 11 && worldSnapshot.artifact_bond_defense_applied, "正式人物快照不重复加物防");
            var proxy = new Creature { entity_data = new HumanData() }; AddChild(proxy);
            var standalone = new BattleEntityData { physical_defense = 10, equipped_artifacts = [a] };
            initialize.Invoke(manager, [proxy, standalone]);
            Check(proxy.physical_defense == 11, "独立战斗快照应用抱朴");
            var sword = new ActiveSkillData { base_damage = 100, damage_type = "physical", tags = ["剑"] };
            var normal = new ActiveSkillData { base_damage = 100, damage_type = "physical" };
            Check(Math.Abs(BattleActionResolver.preview_damage(owner, sword) - 110) < .01f, "剑关键词实际物伤+10%");
            Check(Math.Abs(BattleActionResolver.preview_damage(proxy, sword) - 110) < .01f, "非人物战斗单位读取器蕴来源");
            Check(BattleActionResolver.preview_damage(owner, normal) == 100, "普通物伤不吃惊鸿");
            sword.damage_type = "magic"; Check(BattleActionResolver.preview_damage(owner, sword) == 100, "法术伤害不吃惊鸿");
            owner.unequip_artifact(a.instance_id); Check(owner.physical_defense == 0 && owner.get_artifact_bonds().Jinghong == 0, "卸下撤销器蕴");
            var model = new ProcessForgingTrial(); int generated = 0;
            for (var i = 0; i < model.Parameters.Examples.Count; i++)
            {
                var draft = model.ExampleDraft(i); draft.Proficiency = 60;
                var result = model.Evaluate(draft); if (!result.Success) continue;
                var s = result.Product!; generated += s.BondContributions.Count;
                Check(s.Calculation.Used.All(pair => pair.Value <= s.Calculation.Resources.GetValueOrDefault(pair.Key) + 1e-7), "词条及器蕴共同预算守恒 " + i);
                Check(s.Tier != "凡器" || s.BondContributions.Count == 0, "凡器不生成器蕴");
                var replay = ProcessForgingTrial.Replay(s);
                Check(replay.Success && replay.Product!.BondContributions.SequenceEqual(s.BondContributions), "快照参数重演保留贡献");
            }
            GD.Print("实际样方器蕴条数=" + generated);
            var oldConfig = model.Parameters; oldConfig.BondParameters.Clear(); oldConfig.Version = "sword-process-trial-3";
            var old = ProcessForgingTrial.Isolated(oldConfig, model.Evaluate(model.ExampleDraft(1)).Product!.Sources.Select(source => source.Material));
            Check(old.Evaluate(old.ExampleDraft(1)).Product!.BondContributions.Count == 0, "旧参数不补发器蕴");
            owner.equip_artifact(a.instance_id);
            const string save = "user://tests/artifact_bond.tres"; DirAccess.MakeDirRecursiveAbsolute("user://tests");
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = (HumanData)owner.entity_data! }, save) == Error.Ok, "器蕴装备存档保存");
            var saved = ResourceLoader.Load<PersistentGameSave>(save, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            var restored = new Character(); AddChild(restored); restored.load_from_data(saved.player_data!);
            Check(restored.get_artifact_bonds().Jinghong == 2 && restored.physical_defense == 11, "读回重建累计与实际收益");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(save));
            if (OS.GetCmdlineUserArgs().Contains("--capture-bonds"))
            {
                var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(panel); panel.show_entity(restored);
                typeof(InfoPanel).GetMethod("render_new_page", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(panel, [5]);
                for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var shot = GetViewport().GetTexture().GetImage();
                Check(shot.SavePng("res://docs/美术规范/器蕴接入_20261024/装备页.png") == Error.Ok, "器蕴激活界面截图");
                Input.SetCustomMouseCursor(null);
            }
            GD.Print("ARTIFACT_BOND_OK " + _checks); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
