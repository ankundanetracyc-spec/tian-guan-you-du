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

    private static void InvokeHiddenVoid(InfoPanel panel, string name, params object[] args)
    {
        var method = typeof(InfoPanel).GetMethod(name, HiddenInstance)
            ?? throw new InvalidOperationException("缺少角色页方法：" + name);
        method.Invoke(panel, args);
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
                base_physique = 8,
                base_intelligence = 8,
                base_soul = 8,
                base_body_proficiency = 6,
                base_magic_proficiency = 8,
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

            var nodes = human.get_skill_node_configs().ToArray();
            Check(nodes.Length == 3, "首批技能节点配置不是三条根脉");
            Check(nodes.All(node => human.can_unlock_skill_node(node.node_id, out _)), "达到门槛的人物仍无法参悟首批根节点");
            Check(!nodes.Any(node => human.get_available_battle_skills().Any(owned => Character.combat_skill_key(owned) == node.skill_path)),
                "未参悟的个人技能已经进入人物可用技能库");

            var lockedHuman = new Character();
            AddChild(lockedHuman);
            lockedHuman.load_from_data(new HumanData { entity_name = "未达门槛人物" });
            Check(!lockedHuman.can_unlock_skill_node(nodes[0].node_id, out var lockedReason) && lockedReason.Contains("体魄"),
                "未达门槛的人物没有被节点条件拦下或缺少明确说明");

            typeof(InfoPanel).GetField("_showSkillNodeNetwork", HiddenInstance)!.SetValue(panel, true);
            InvokeHiddenVoid(panel, "build_character_battle", human);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var faces = human.get_skill_node_face_configs().ToArray();
            Check(faces.Length == 4 && faces.Any(face => face.face_name == "剑修"), "首批脉面配置不完整");
            Check(Descendants<Button>(panel).Count(button => faces.Any(face => button.Text == face.face_name)) == faces.Length,
                "脉面选择区没有显示全部脉面");

            var swordFace = faces.First(face => face.face_name == "剑修");
            var swordButton = Descendants<Button>(panel).First(button => button.Text == swordFace.face_name);
            swordButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Descendants<Label>(panel).Any(label => label.Text.Contains("此脉面尚无参悟节点")), "空脉面没有显示待扩展说明");

            foreach (var node in nodes)
            {
                var face = faces.FirstOrDefault(candidate => candidate.face_id == node.face_id)
                    ?? throw new InvalidOperationException("节点没有归属脉面：" + node.node_name);
                var faceButton = Descendants<Button>(panel).FirstOrDefault(button => button.Text == face.face_name)
                    ?? throw new InvalidOperationException("脉面选择区缺少入口：" + face.face_name);
                faceButton.EmitSignal(BaseButton.SignalName.Pressed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(Descendants<Label>(panel).Any(label => label.Text == node.node_name), "选中脉面后没有显示对应根节点：" + node.node_name);
                Check(Descendants<Button>(panel).Count(button => button.Text == "参悟此节点") == 1,
                    "单个脉面显示的参悟入口数量不正确");

                var learn = Descendants<Button>(panel).FirstOrDefault(button => button.Text == "参悟此节点")
                    ?? throw new InvalidOperationException("节点网缺少参悟按钮：" + node.node_name);
                learn.EmitSignal(BaseButton.SignalName.Pressed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(human.has_unlocked_skill_node(node.node_id), "节点参悟后没有写入人物状态：" + node.node_name);
                Check(human.get_available_battle_skills().Any(owned => Character.combat_skill_key(owned) == node.skill_path),
                    "参悟技能没有进入人物技能库：" + node.node_name);
                Check(!human.get_combat_skill_slot_paths().Contains(node.skill_path), "参悟技能被自动装配：" + node.node_name);
                var nodeSkill = GD.Load<ActiveSkillData>(node.skill_path)!;
                var sourceName = (string)InvokeHidden(panel, "character_skill_source", human, nodeSkill);
                Check(sourceName == "技能节点 · " + node.node_name, "技能库没有标注个人节点来源：" + node.node_name);
            }

            var savedHumanData = typeof(Character).GetProperty("human", HiddenInstance)?.GetValue(human) as HumanData
                ?? throw new InvalidOperationException("无法读取人物存档数据");
            var reloadedHuman = new Character();
            AddChild(reloadedHuman);
            reloadedHuman.load_from_data(savedHumanData.Duplicate(true) as HumanData
                ?? throw new InvalidOperationException("人物存档数据复制失败"));
            Check(nodes.All(node => reloadedHuman.has_unlocked_skill_node(node.node_id)), "节点参悟记录未随人物数据读档恢复");

            GD.Print("INFO_PANEL_SKILL_NODE_NETWORK_PASS: facets render and switch, roots retain unlock gates, skills stay unequipped, source labels and save data persist");
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
