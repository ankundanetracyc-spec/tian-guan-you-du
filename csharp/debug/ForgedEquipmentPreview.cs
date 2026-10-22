using Godot;
using System;
using System.Linq;

/// <summary>单器试用的可操作对练；返回工坊保留本页库存，不读写正式旅程。</summary>
public partial class ForgedEquipmentPreview : Node2D
{
    public Action? Closed;
    private ForgedEquipmentLoadout? _loadout;
    private readonly Godot.Collections.Array<ActiveSkillData> _skills = [];
    private bool _equipped = true;
    private Label _status = null!;
    // 重开期间旧单位及演出可能仍在延迟释放，显式保活各轮生成的 C# Resource。
    private readonly System.Collections.Generic.List<Resource> _battleResources = [];
    private PackedScene? _characterScene;
    public static readonly string[] SkillPaths = ["res://resources/activeskills/应敌.tres", "res://resources/activeskills/痛击.tres",
        "res://resources/activeskills/百骸衍枝化生宝册/摧山.tres", "res://resources/activeskills/架招.tres"];
    public void Configure(ProcessForgingTrial.Artifact item) => _loadout = new(item);
    public override void _Ready()
    {
        if (_loadout is null) { QueueFree(); return; }
        GetNode<BattleManager>("BattleMgr").isolated_trial_battle = true;
        _characterScene = CharacterScene();
        foreach (string path in SkillPaths) _skills.Add(GD.Load<ActiveSkillData>(path));
        var ui = GetNode<CanvasLayer>("UILayer");
        GetNode<Label>("UILayer/Title").Hide(); GetNode<Label>("UILayer/Hint").Hide();
        var background = new ColorRect { Color = new Color("ddd8c6"), MouseFilter = Control.MouseFilterEnum.Ignore, Size = new(1920, 1080) };
        var backdrop = new CanvasLayer { Layer = -10 }; AddChild(backdrop); backdrop.AddChild(background);
        var back = new Button { Text = "返回炼器工坊", Position = new(32, 112), Size = new(200, 42), ZIndex = 100, Theme = QuietRoomStyle.MakeTheme() }; ForgingInkStyle.Button(back); ui.AddChild(back);
        back.Pressed += Close;
        var paper = new PanelContainer { Name = "EquipmentSetup", Position = new(580, 165), Size = new(760, 450), Visible = false, ZIndex = 200, Theme = QuietRoomStyle.MakeTheme() }; paper.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(padding: 16)); ui.AddChild(paper);
        var setup = new Button { Name = "OpenEquipmentSetup", Text = "试器装配", Position = new(245, 112), Size = new(180, 42), ZIndex = 100, Theme = QuietRoomStyle.MakeTheme() }; ForgingInkStyle.Button(setup); ui.AddChild(setup); setup.Pressed += () => paper.Visible = !paper.Visible;
        var list = new VBoxContainer(); list.AddThemeConstantOverride("separation", 7); paper.AddChild(list);
        list.AddChild(new Label { Text = "单器试用 · 修改后点击下方按钮重开对练" });
        var equipment = new CheckBox { Name = "EquipSingle", Text = "装备：" + _loadout.Item.Name + " · " + _loadout.Item.Tier, ButtonPressed = true }; list.AddChild(equipment);
        equipment.Toggled += enabled => _equipped = enabled;
        foreach (var skill in _skills.Where(ForgedEquipmentLoadout.IsPhysicalAttack))
        {
            var toggle = new CheckBox { Name = "SwordUse" + _skills.IndexOf(skill), Text = "用这把剑施展：" + skill.skill_name }; list.AddChild(toggle);
            toggle.Toggled += enabled => _loadout.SetSwordUse(skill, enabled);
        }
        foreach (var effect in _loadout.Item.Effects.Where(x => x.CarrierId == "binding"))
        {
            var row = new HBoxContainer(); list.AddChild(row); row.AddChild(new Label { Text = "化法 · " + effect.Description });
            var options = _skills.Where(s => _loadout.CanBind(effect, s)).ToList(); var pick = new OptionButton { Name = "Binding_" + effect.AtomId }; pick.AddItem("不绑定");
            foreach (var skill in options) pick.AddItem(skill.skill_name); row.AddChild(pick);
            pick.ItemSelected += index => { if (index == 0) _loadout.Bind(effect.EffectId, null); else if (!_loadout.Bind(effect.EffectId, options[(int)index - 1])) { pick.Select(0); _status.Text = "该技能已有其他器物化法，请先解除原绑定。"; } };
        }
        var restart = new Button { Name = "ApplyEquipment", Text = "按当前装配重开对练" }; ForgingInkStyle.Button(restart, true); list.AddChild(restart); restart.Pressed += () => { StartBattle(); paper.Hide(); };
        _status = new Label { Position = new(450, 112), Size = new(1410, 57), Theme = QuietRoomStyle.MakeTheme(), AutowrapMode = TextServer.AutowrapMode.WordSmart }; _status.AddThemeFontSizeOverride("font_size", 17); ui.AddChild(_status);
        CallDeferred(nameof(StartBattle));
    }
    public static BattleEntityData Baseline()
    {
        var skills = new Godot.Collections.Array<ActiveSkillData>(SkillPaths.Select(path => GD.Load<ActiveSkillData>(path)));
        while (skills.Count < 10) skills.Add(ArtifactSkillFactory.EmptySlot());
        return new BattleEntityData { uid = "forged-single-player", entity_name = "试器修士", faction = 1, cultivation_realm = "云息三层",
            max_health = 250, current_health = 250, max_mana = 3, max_stamina = 6, max_spirit_perception = 5,
            physique = 20, soul = 10, precision = 100, dodge = 0, crit_resist = 500, physical_defense = 5,
            initiative_speed = 150, test_stamina_cap_override = 6, test_mana_cap_override = 3,
            combat_skill_slots_configured = true, combat_resolved_skills = skills };
    }
    public static PackedScene CharacterScene()
    {
        return GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
    }
    public void StartBattle()
    {
        if (_loadout is null) return;
        var baseline = Baseline(); var player = _equipped ? _loadout.BuildSnapshot(baseline) : baseline;
        KeepSnapshot(baseline);
        var enemy = new BattleEntityData { uid = "forged-single-enemy", entity_name = "试器陪练", faction = 2, cultivation_realm = "云息三层",
            max_health = 800, current_health = 800, max_stamina = 6, physique = 12, soul = 4, physical_defense = 10,
            precision = 100, dodge = 0, crit_resist = 500, initiative_speed = 100, combat_skill_slots_configured = true,
            combat_skill_slot_paths = [SkillPaths[0]] };
        KeepSnapshot(player); KeepSnapshot(enemy);
        var manager = GetNode<BattleManager>("BattleMgr"); manager.SetMeta("isolated_trial_finished", false); manager.setup_team_battle([_characterScene!], [_characterScene!], [player], [enemy]);
        var owner = manager.player_units[0];
        _status.Text = (_equipped ? "已装备一件器" : "未装备器物") + $" · 当前物防 {CardRules.Number(owner, "physical_defense"):0} · 物理增伤 {CardRules.Number(owner, "forged_physical_bonus") * 100:0.##}%\n锋锐只用于已勾选的用剑攻击。关键词常驻、触发和主动获授能力暂未接入。";
    }
    private void KeepSnapshot(BattleEntityData snapshot)
    {
        _battleResources.Add(snapshot);
        foreach (var skill in snapshot.combat_resolved_skills) KeepSkill(skill);
    }
    private void KeepSkill(ActiveSkillData skill)
    {
        _battleResources.Add(skill);
        foreach (var bonus in skill.damage_bonuses) if (bonus is not null) _battleResources.Add(bonus);
        if (skill.non_response_skill is { } normal && normal != skill) KeepSkill(normal);
    }
    public void Close()
    {
        GetNode<BattleManager>("BattleMgr").battle_running = false;
        HoverInfoManager.HideAll(); _skills.Clear(); _loadout = null; _battleResources.Clear(); _characterScene = null;
        GetNode<Camera2D>("Camera2D").Enabled = false;
        GetViewport().CanvasTransform = Transform2D.Identity;
        Closed?.Invoke(); QueueFree();
    }
}
