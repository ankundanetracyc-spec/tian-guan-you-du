using System.Reflection;
using Godot;

/// <summary>确认无剧情开局秘籍与技能装配目录，不读取或写入正式旅程。</summary>
public partial class InfoPanelGongfaSkillDirectorySmoke : Node
{
    private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static IEnumerable<T> Descendants<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (var child in root.GetChildren())
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }

    private static object InvokeHidden(InfoPanel panel, string name, params object[] args)
    {
        var method = typeof(InfoPanel).GetMethod(name, HiddenInstance)
            ?? throw new InvalidOperationException("缺少角色页方法：" + name);
        return method.Invoke(panel, args) ?? throw new InvalidOperationException("角色页方法未返回目录：" + name);
    }

    private static T Field<T>(InfoPanel panel, string name) where T : class =>
        typeof(InfoPanel).GetField(name, HiddenInstance)?.GetValue(panel) as T
        ?? throw new InvalidOperationException("缺少角色页控件：" + name);

    private static Button SkillEntry(VBoxContainer list, string skillName) =>
        list.GetChildren().OfType<Button>().FirstOrDefault(button =>
            Descendants<Label>(button).Any(label => label.Text == skillName))
        ?? throw new InvalidOperationException("技能目录没有显示：" + skillName);

    private static void RenderPage(InfoPanel panel, int page)
    {
        var method = typeof(InfoPanel).GetMethod("render_new_page", HiddenInstance)
            ?? throw new InvalidOperationException("缺少角色页切换入口");
        method.Invoke(panel, [page]);
    }

    public override async void _Ready()
    {
        try
        {
            var panelScene = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn");
            Check(panelScene is not null, "角色面板场景无法加载");
            var panel = panelScene!.Instantiate<InfoPanel>();
            AddChild(panel);

            var human = new Character();
            AddChild(human);
            human.Hide();
            human.load_from_data(new HumanData
            {
                entity_name = "秘籍技能目录验收",
                cultivation_realm = "云息三层",
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
                spirit_root_name = "三灵根",
                root_weights = [34, 33, 33, 0, 0],
                current_world_stamina = 100
            });

            var introPath = "res://resources/gongfa/黄庭养形篇.tres";
            var intro = GD.Load<GongfaResource>(introPath);
            Check(intro is not null && human.grant_intro_gongfa(intro!), "无剧情开局秘籍未能发给角色");
            Check(human.get_pending_intro_gongfa()?.ResourcePath == introPath, "初始秘籍未处于待研读状态");
            Check(!human.has_learned_gongfa(intro!), "初始待研读秘籍被错误标成已学");

            panel.show_entity(human);
            RenderPage(panel, 3);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var manualPaths = ((IEnumerable<GongfaResource>)InvokeHidden(panel, "character_manuals", human))
                .Select(book => book.ResourcePath).ToList();
            Check(manualPaths.Contains(introPath), "待研读初始秘籍没有进入功法库数据");
            var manualList = Field<VBoxContainer>(panel, "_manualList");
            Check(Descendants<Label>(manualList).Any(label => label.Text == intro!.gongfa_name), "待研读初始秘籍没有显示在功法页面");

            Check(human.start_pending_intro_gongfa_study(), "无法开始初始秘籍研读");
            Check(human.get_pending_intro_gongfa() is null && human.get_current_study_gongfa()?.ResourcePath == introPath,
                "开始研读后没有从待研读状态转为当前读本");
            RenderPage(panel, 3);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            manualPaths = ((IEnumerable<GongfaResource>)InvokeHidden(panel, "character_manuals", human))
                .Select(book => book.ResourcePath).ToList();
            Check(manualPaths.Contains(introPath), "待研读状态结束后，当前研读秘籍从目录消失");
            Check(Descendants<Label>(Field<VBoxContainer>(panel, "_manualList")).Any(label => label.Text == intro!.gongfa_name),
                "当前研读秘籍没有显示在功法页面");

            var skillPath = human.get_default_combat_skill_paths().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(skillPath)) throw new InvalidOperationException("角色没有可用的公共基础技能");
            var skill = GD.Load<ActiveSkillData>(skillPath)
                ?? throw new InvalidOperationException("默认基础技能资源无效");
            Check(Character.is_combat_skill_slot_eligible(skill), "默认基础技能不可装配");
            human.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(new[] { skillPath }));
            RenderPage(panel, 2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var catalog = ((IEnumerable<ActiveSkillData>)InvokeHidden(panel, "character_skill_catalog", human))
                .Select(Character.combat_skill_key).ToList();
            Check(catalog.Contains(skillPath), "默认基础技能没有进入可装配目录");

            var skillList = Field<VBoxContainer>(panel, "_characterSkillList");
            SkillEntry(skillList, skill!.skill_name).EmitSignal(BaseButton.SignalName.Pressed);
            Check(!human.get_combat_skill_slot_paths().Contains(skillPath), "技能目录点击未卸下技能");
            catalog = ((IEnumerable<ActiveSkillData>)InvokeHidden(panel, "character_skill_catalog", human))
                .Select(Character.combat_skill_key).ToList();
            Check(catalog.Contains(skillPath), "卸下后公共基础技能从可装配目录消失");
            skillList = Field<VBoxContainer>(panel, "_characterSkillList");
            SkillEntry(skillList, skill.skill_name).EmitSignal(BaseButton.SignalName.Pressed);
            Check(human.get_combat_skill_slot_paths().Contains(skillPath), "卸下后无法从目录重新装回技能");

            GD.Print("INFO_PANEL_GONGFA_SKILL_DIRECTORY_PASS: pending/current intro book visible; public basic skill remains available and can be re-equipped after unload");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            GetTree().Quit(1);
        }
    }
}
