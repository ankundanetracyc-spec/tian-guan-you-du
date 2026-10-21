using Godot;
using System.Reflection;

/// <summary>开放目录、旧秘籍失效与原始存档引用保留的集成检查。</summary>
public partial class GongfaAvailabilitySmoke : Node
{
    private int _checks;
    private void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); _checks++; }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        const string savePath = "user://gongfa_availability_smoke.tres";
        try
        {
            var expected = new HashSet<string> { "玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册" };
            var books = GongfaAvailability.EnabledBooks().ToArray();
            Check(expected.SetEquals(books.Select(book => book.gongfa_name)), "游戏开放目录恰为四本已指定秘籍");
            Check(expected.SetEquals(PrologueText.InitialChoices.Select(choice => choice.name)), "开局选书共用开放目录");
            var choices = ((string name, string path)[])typeof(StartMenu)
                .GetMethod("TestBookOptions", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
            Check(expected.SetEquals(choices.Where(choice => choice.path.Length > 0).Select(choice => choice.name)), "战斗测试只列四本秘籍");

            foreach (var file in DirAccess.GetFilesAt("res://resources/gongfa").Where(file => file.EndsWith(".tres")))
            {
                var book = GD.Load<GongfaResource>("res://resources/gongfa/" + file)!;
                if (expected.Contains(book.gongfa_name))
                {
                    Check(GongfaAvailability.EnabledPath(book.ResourcePath) == book.ResourcePath, "开放路径：" + book.gongfa_name);
                    Check(GongfaAvailability.IsSkillEnabled(book.get_heart_method_skill(true)), "开放心法：" + book.gongfa_name);
                }
                else
                {
                    Check(!book.gameplay_enabled && GongfaAvailability.EnabledPath(book.ResourcePath) == "", "下架路径：" + book.gongfa_name);
                    Check(!GongfaAvailability.IsSkillEnabled(book.get_heart_method_skill(true)), "下架心法：" + book.gongfa_name);
                    foreach (var skill in book.chapters.SelectMany(chapter => chapter.get_learned_skills(true)).OfType<ActiveSkillData>()
                        .Where(skill => skill.source_gongfa_name == book.gongfa_name))
                        Check(!Character.is_combat_skill_slot_eligible(skill), "下架专属技能：" + skill.skill_name);
                }
            }
            foreach (var path in new[] { "应敌", "架招", "痛击", "催血" })
                Check(Character.is_combat_skill_slot_eligible(GD.Load<ActiveSkillData>($"res://resources/activeskills/{path}.tres")), "保留公共或共享招式：" + path);

            var old = GD.Load<GongfaResource>("res://resources/gongfa/黄庭养形篇.tres")!;
            var oldSkill = old.chapters.SelectMany(chapter => chapter.get_learned_skills(true)).OfType<ActiveSkillData>()
                .First(skill => skill.skill_type != ActiveSkillData.SkillType.POWER);
            var data = new HumanData { entity_name = "下架兼容检查", cultivation_realm = "云息九层", main_gongfa_path = old.ResourcePath,
                learned_gongfa_paths = [old.ResourcePath], basic_deck_path = "res://resources/basic_decks/公共基础技能池.tres",
                combat_skill_slots_configured = true, combat_skill_slot_paths = [oldSkill.ResourcePath],
                combat_skill_references = [CombatSkillReference.FromKey(oldSkill.ResourcePath)] };
            data.learned_gongfa_chapters[old.ResourcePath] = new Godot.Collections.Array<string>(old.chapters.Select(chapter => chapter.get_id()));
            var actor = new Character(); AddChild(actor); actor.load_from_data(data);
            Check(actor.main_gongfa is null && actor.get_known_gongfas().Count == 0, "旧主修退出当前装配与可选目录");
            Check(!(bool)typeof(GongfaCombatRuntime).GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [actor, old.gongfa_name])!, "旧存档路径不能绕过主修开放判定");
            Check(actor.get_combat_passive_paths().Count == 0, "旧心法不进入战斗快照");
            Check(actor.get_combat_skill_slot_paths()[0] == "", "旧技能在当前战斗槽中留空");
            Check(data.combat_skill_references[0].Key == oldSkill.ResourcePath, "读取技能槽保留存档原引用");
            Check(data.main_gongfa_path == old.ResourcePath && data.learned_gongfa_chapters.ContainsKey(old.ResourcePath), "旧研读和装配记录保留");
            Check(!actor.select_gongfa_for_study(old) && !actor.switch_main_gongfa(old), "禁止继续研读或装回下架秘籍");
            var oldItem = GD.Load<ItemData>("res://resources/entities/items/黄庭养形篇秘籍.tres")!;
            Check(!GongfaAvailability.IsItemEnabled(oldItem) && !YuxuSectPrototypeBalance.is_exchangeable_item(oldItem), "下架秘籍退出物品和宗门兑换目录");
            Check(!actor.can_add_item_to_inventory(oldItem.ResourcePath), "禁止新增下架秘籍物品");
            Check(ResourceSaver.Save(data, savePath) == Error.Ok, "写入独立兼容检查存档");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var restored = ResourceLoader.Load<HumanData>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(restored.main_gongfa_path == old.ResourcePath && restored.combat_skill_references[0].Key == oldSkill.ResourcePath
                && restored.learned_gongfa_chapters.ContainsKey(old.ResourcePath), "存读后仍保留被下架内容的原始进度和引用");
            actor.QueueFree();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            GD.Print($"GONGFA_AVAILABILITY_SMOKE: {_checks} checks passed; {string.Join("、", books.Select(book => book.gongfa_name))}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("GONGFA_AVAILABILITY_SMOKE: " + error); GetTree().Quit(1); }
    }
}
