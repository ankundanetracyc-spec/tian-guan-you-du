using Godot;
using System;

/// <summary>Runs a current-character drill while retaining the world scene and progress in memory.</summary>
public partial class ActionCharacterBattleSession : Node
{
    private Node _world = null!;
    private Node2D _arena = null!;
    private bool _wasPaused, _worldVisible;
    private Control? _panel;
    private bool _panelVisible;
    private bool _closed;

    public static ActionCharacterBattleSession? Open(Character character, Control? panel = null)
    {
        var tree = character.GetTree();
        if (tree.GetFirstNodeInGroup("current_character_action_drill") is not null || character.current_health <= 0) return null;
        var session = new ActionCharacterBattleSession { Name = "CurrentCharacterActionDrill", ProcessMode = ProcessModeEnum.Always };
        session.AddToGroup("current_character_action_drill");
        tree.Root.AddChild(session);
        try { session.Start(character, panel); return session; }
        catch { session.Close(); throw; }
    }

    private void Start(Character character, Control? panel)
    {
        var tree = GetTree();
        _world = tree.CurrentScene;
        _worldVisible = (_world as CanvasItem)?.Visible == true;
        _wasPaused = tree.Paused;
        _panel = panel; _panelVisible = panel?.Visible == true;
        var wolf = new Beast { Visible = false, beast_template = GD.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres") };
        AddChild(wolf);
        var enemy = wolf.get_snapshot_data();
        enemy.uid = "action-drill-wolf:" + Guid.NewGuid().ToString("N");
        wolf.QueueFree();
        var attributes = new ActionBattleAttributeRuntime();
        attributes.Configure(character.get_snapshot_data(), enemy);
        attributes.CloseRequested = Close;
        _arena = GD.Load<PackedScene>("res://tscn/scene/ActionBattleOriginalWandererScene.tscn").Instantiate<Node2D>();
        _arena.Name = "CurrentCharacterActionArena";
        _arena.ProcessMode = ProcessModeEnum.Always;
        _arena.Set("attribute_runtime", attributes);
        var selfSkills = new Godot.Collections.Array<string>();
        foreach (var skill in character.get_available_battle_skills())
            if (skill.ResourcePath is "res://resources/activeskills/催血.tres" or "res://resources/activeskills/聚炁.tres")
                if (!selfSkills.Contains(skill.skill_name)) selfSkills.Add(skill.skill_name);
        _arena.Set("available_self_skills", selfSkills);
        _arena.Set("skill_slots", ActionCombatLoadout.ReadNames(character));
        _arena.AddChild(attributes);
        if (_world is CanvasItem worldCanvas) worldCanvas.Hide();
        panel?.Hide();
        tree.Paused = true;
        tree.Root.AddChild(_arena);
        tree.CurrentScene = _arena;
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        var tree = GetTree();
        if (GodotObject.IsInstanceValid(_arena))
        {
            // ExitTree restores window/camera configuration before the world is displayed again.
            _arena.GetParent()?.RemoveChild(_arena);
            _arena.QueueFree();
        }
        if (GodotObject.IsInstanceValid(_world))
        {
            tree.CurrentScene = _world;
            if (_world is CanvasItem canvas) canvas.Visible = _worldVisible;
            if (GodotObject.IsInstanceValid(_panel)) _panel!.Visible = _panelVisible;
        }
        tree.Paused = _wasPaused;
        QueueFree();
    }
}
