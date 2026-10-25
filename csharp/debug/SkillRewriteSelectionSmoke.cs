using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

/// <summary>化法显式绑定、原技能身份、来源变化与角色页真实按钮的回归检查。</summary>
public partial class SkillRewriteSelectionSmoke : Node
{
    private const string Guard = "res://resources/activeskills/架招.tres";
    private const string Attack = "res://resources/activeskills/应敌.tres";
    private const string BookPath = "res://resources/gongfa/玄枢含章经.tres";
    private const string LegacyGuard = "res://resources/activeskills/玄枢含章经/化法/架招.tres";
    private readonly List<Resource> _keep = [];
    private int _checks;
    private void Check(bool ok, string text)
    { if (!ok) throw new InvalidOperationException(text); _checks++; GD.Print("REWRITE_CHECK_OK " + text); }

    public override async void _Ready()
    {
        try
        {
            var book = GD.Load<GongfaResource>(BookPath); _keep.Add(book);
            var sub = GD.Load<GongfaResource>("res://resources/gongfa/太和感物养元宝册.tres"); _keep.Add(sub);
            var data = new HumanData { entity_name = "化法界面验收", cultivation_realm = "云息九层",
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER, spirit_root_name = "三灵根", root_weights = [34,33,33,0,0],
                main_gongfa_path = BookPath, sub_gongfa_paths = [sub.ResourcePath],
                learned_gongfa_paths = [BookPath, sub.ResourcePath], base_physique = 12, base_intelligence = 12, base_soul = 12 };
            data.learned_gongfa_chapters[BookPath] = new Godot.Collections.Array<string>(book.get_sorted_chapters().Select(c => c.get_id()));
            data.learned_gongfa_chapters[sub.ResourcePath] = new Godot.Collections.Array<string>(sub.get_sorted_chapters().Select(c => c.get_id()));
            foreach (var chapter in sub.get_sorted_chapters()) data.equipped_gongfa_chapter_routes[sub.ResourcePath + ":" + chapter.get_id()] = "right";
            _keep.Add(data);
            var owner = new Character(); AddChild(owner); owner.load_from_data(data); owner.Hide();
            var human = (HumanData)owner.entity_data!; _keep.Add(human);
            var strike = book.get_sorted_chapters()[0].get_learned_skills(true).OfType<ActiveSkillData>().First().ResourcePath;
            owner.set_combat_skill_slot_paths([Attack, Guard, strike]);
            var choices = owner.get_skill_rewrite_choices();
            Check(choices.Count(c => c.Available && c.Source.StartsWith("玄枢含章经")) == 4 &&
                choices.Count(c => c.Available && c.Source.StartsWith("太和感物")) == 2, "按真实配置统一读出主修及辅修化法，不补造空卷章");
            var guardChoice = choices.Single(c => c.Source.StartsWith("玄枢含章经") && c.Title == "架招化法");
            var strikeChoice = choices.Single(c => c.Source.StartsWith("玄枢含章经") && c.Title == "惊蛰化法");
            var subGuard = choices.Single(c => c.Source.StartsWith("太和感物") && c.Title == "架招化法");
            Check(!owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "学会化法不会自动赋给已装配的架招");
            Check(owner.get_skill_rewrite_candidates(guardChoice.Id).Count == 1, "固定架招化法仍提供显式选择");
            Check(!owner.set_skill_rewrite_binding(guardChoice.Id, Attack, out _), "拒绝不符合条件的已装配技能");
            var shieldChoice = choices.Single(c => c.Source.StartsWith("玄枢含章经") && c.Title == "云帔化法");
            var shield = book.get_sorted_chapters()[1].get_learned_skills(true).OfType<ActiveSkillData>().First().ResourcePath;
            Check(!owner.set_skill_rewrite_binding(shieldChoice.Id, shield, out _), "拒绝已学但未装配的技能");
            Check(owner.set_skill_rewrite_binding(guardChoice.Id, Guard, out _), "确认绑定架招成功");
            Check(owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "已绑定化法进入技能结算副本");
            Check(owner.resolve_combat_skill(Guard)!.combat_source_key == Guard &&
                !GD.Load<ActiveSkillData>(Guard).gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "保留原技能身份且不污染共享资源");
            Check(owner.set_skill_rewrite_binding(strikeChoice.Id, strike, out _) &&
                owner.resolve_combat_skill(strike)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c1_jingzhe"), "秘籍自身招式同样需选择并生效");
            Check(owner.set_skill_rewrite_binding(subGuard.Id, Guard, out _) &&
                owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("taihe_c3_jiazhao") &&
                owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "兼容的多来源化法保持合并");
            owner.set_combat_skill_slot_paths([Attack, "", strike, "", Guard]);
            Check(owner.get_skill_rewrite_candidates(guardChoice.Id).Single().Slot == 4 &&
                owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Count >= 2, "换位后绑定跟随技能");
            owner.set_combat_skill_slot_paths([Attack, "", strike]);
            Check(owner.get_skill_rewrite_candidates(guardChoice.Id).Count == 0 &&
                !owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "卸下技能时暂停生效");
            owner.set_combat_skill_slot_paths([Attack, Guard, strike]);
            Check(owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "重新装配原技能后恢复原绑定");
            owner.set_sub_gongfa_chapter_route(sub, sub.get_sorted_chapters()[2].get_id(), "left");
            Check(!owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("taihe_c3_jiazhao"), "辅修切换修持后不再应用化法");
            owner.set_sub_gongfa_chapter_route(sub, sub.get_sorted_chapters()[2].get_id(), "right");
            var legacy = ArtifactCombatSamples.Create(true, true, "rewrite-guard"); _keep.Add(legacy);
            legacy.bound_basic_card_path = ""; human.artifact_inventory.Add(legacy); human.equipped_artifact_ids.Add(legacy.instance_id);
            var itemChoice = owner.get_skill_rewrite_choices().Single(c => c.Id == "artifact:" + legacy.instance_id);
            Check(owner.get_skill_rewrite_binding(itemChoice) == "" && owner.set_skill_rewrite_binding(itemChoice.Id, Guard, out _), "器物化法通过同一入口选择已装配技能");
            Check(owner.resolve_combat_skill(Guard)!.artifact_block_bonus == legacy.combat_profile!.inner_block_bonus &&
                owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "器物与秘籍化法共同进入副本");
            human.equipped_artifact_ids.Remove(legacy.instance_id);
            Check(owner.resolve_combat_skill(Guard)!.artifact_block_bonus == 0 &&
                !owner.get_skill_rewrite_choices().Single(c => c.Id == itemChoice.Id).Available, "卸下器物暂停来源而保留绑定信息");
            var model = new ProcessForgingTrial();
            var product = new ProcessForgingTrial.Session(model.Parameters.Profiles.Keys).Submit(model, model.ExampleDraft(1), 1).Product!;
            var forged = ForgedArtifactInventory.Create(product); _keep.Add(forged); human.artifact_inventory.Add(forged);
            Check(product.Effects.Any(e => e.CarrierId == "binding") &&
                owner.get_skill_rewrite_choices().Any(c => c.Id == "forged:" + forged.instance_id && !c.Available), "读取新成器快照中的条件化法，不冒充已经装备");
            var snapshot = owner.get_snapshot_data(); _keep.Add(snapshot);
            Check(snapshot.combat_resolved_skills[1].gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "战斗快照使用已确认的化法版本");

            var save = new PersistentGameSave { player_data = human }; _keep.Add(save);
            var path = "user://skill_rewrite_selection_smoke.tres";
            Check(ResourceSaver.Save(save, path) == Error.Ok, "正式存档资源写入绑定");
            var restored = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!; _keep.Add(restored);
            Check(restored.player_data!.skill_rewrite_bindings[guardChoice.Id] == Guard, "正式存档读回稳定来源与原技能绑定");
            var restoredOwner = new Character(); AddChild(restoredOwner); restoredOwner.load_from_data(restored.player_data); restoredOwner.Hide();
            Check(restoredOwner.get_combat_skill_slot_paths()[1] == Guard &&
                restoredOwner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"),
                "读档后的角色恢复原技能槽与已确认化法效果");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));

            var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(panel);
            panel.show_entity(owner);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(InfoPanel).GetMethod("render_new_page", flags)!.Invoke(panel, [2]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!Descendants(panel).OfType<Button>().Any(b => b.Text == "化法") &&
                Descendants(panel).OfType<Label>().Any(l => l.Text == "已学技能"), "化法和技能库同屏，不新增化法标签页");
            var sourcePicker = Descendants(panel).OfType<OptionButton>().Single(b => b.Name == "RewriteSourcePicker");
            var sourceIndex = Enumerable.Range(0, sourcePicker.ItemCount).Single(i => sourcePicker.GetItemMetadata(i).AsString() == guardChoice.Id);
            sourcePicker.Select(sourceIndex); sourcePicker.EmitSignal(OptionButton.SignalName.ItemSelected, sourceIndex);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var candidatePicker = Descendants(panel).OfType<OptionButton>().Single(b => b.Name == "RewriteTargetPicker");
            Check(Enumerable.Range(0, candidatePicker.ItemCount).Any(i => candidatePicker.GetItemMetadata(i).AsString() == Guard), "技能库侧栏显示合格技能候选");
            Check(Descendants(panel).OfType<Label>().Single(l => l.Name == "RewriteDescription").Size.X > 180,
                "化法正文占据侧栏宽度，不按单字竖排");
            var sidebar = (VBoxContainer)typeof(InfoPanel).GetField("_characterSkillRewritePanel", flags)!.GetValue(panel)!;
            Check(sidebar.Size.X <= 270 && sidebar.Size.Y <= 625,
                "化法侧栏宽高保持在技能库预留区域内，长来源名称不撑破页面");
            var clear = Descendants(panel).OfType<Button>().First(b => b.Text == "解除绑定"); clear.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!human.skill_rewrite_bindings.ContainsKey(guardChoice.Id), "界面解除按钮写回角色配置");
            candidatePicker = Descendants(panel).OfType<OptionButton>().Single(b => b.Name == "RewriteTargetPicker");
            var candidateIndex = Enumerable.Range(0, candidatePicker.ItemCount).Single(i => candidatePicker.GetItemMetadata(i).AsString() == Guard);
            candidatePicker.Select(candidateIndex); candidatePicker.EmitSignal(OptionButton.SignalName.ItemSelected, candidateIndex);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Descendants(panel).OfType<Button>().First(b => b.Name == "ConfirmRewriteBinding").EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(human.skill_rewrite_bindings[guardChoice.Id] == Guard, "界面选择并确认后真实保存绑定");
            if (OS.GetCmdlineUserArgs().Contains("--rewrite-ui-capture"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var texture = GetViewport().GetTexture();
                using var capture = texture.GetImage();
                capture.SavePng("res://docs/美术规范/化法选择_20261024/化法选择.png");
            }
            owner.unequip_main_gongfa();
            Check(!owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "主修卸下时暂停该来源化法");
            owner.main_gongfa = book; human.main_gongfa_path = BookPath;
            human.skill_rewrite_bindings.Clear();
            owner.set_combat_skill_slot_paths([Attack, LegacyGuard]);
            Check(owner.get_combat_skill_slot_paths()[1] == Guard &&
                !owner.resolve_combat_skill(Guard)!.gongfa_rewrite_effect_ids.Contains("xuan_shu_c3_jiazhao"), "旧自动版本迁回原技能且不擅自确认化法");
            GD.Print($"SKILL_REWRITE_SELECTION_OK checks={_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("SKILL_REWRITE_SELECTION_FAIL " + error); GetTree().Quit(1); }
    }

    private static IEnumerable<Node> Descendants(Node root)
    { foreach (var child in root.GetChildren()) { yield return child; foreach (var node in Descendants(child)) yield return node; } }
}
