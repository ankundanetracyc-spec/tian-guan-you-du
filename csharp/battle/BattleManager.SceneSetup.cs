using Godot;
using Godot.Collections;

public partial class BattleManager : Node2D
{
    private Node? Map => GetNodeOrNull<Node>(battle_map) ?? GetNodeOrNull<Node>("../BattleMap");
    private Node? DeployLayer => GetNodeOrNull<Node>(deploy_layer) ?? GetNodeOrNull<Node>("../DeployLayer");
    private bool _sceneUiConnected;

    public override void _Ready()
    {
        AddToGroup("battle_mgr"); ProcessMode = ProcessModeEnum.Always;
        _executor = new SkillExecutor(); AddChild(_executor);
        CallDeferred(nameof(connect_skill_bar));
    }

    private void connect_skill_bar()
    {
        if (_sceneUiConnected) return;
        var transition = GetTree().Root.GetNodeOrNull<BattleTransitionManager>("BattleTransitionManager");
        if (!isolated_trial_battle && transition?.is_spectator_battle_active() == true)
        {
            _spectatorBattle = true;
            _sceneUiConnected = true;
            return;
        }
        // 场景切换期间 CurrentScene 的赋值时机与节点 Ready 顺序不同步；
        // BattleMgr 的父节点才是当前实际战斗场景，测试入口和正式入口都从这里取 UI。
        var scene = GetParent() ?? GetTree().CurrentScene;
        if (scene is null) return;
        var uiLayer = scene.GetNodeOrNull<CanvasLayer>("UILayer");
        if (uiLayer is null) return;
        uiLayer.Layer = BattlePresentationLayers.HudCanvas;
        ApplyBattleArtwork(scene);

        _skillBar = scene.GetNodeOrNull<CombatSkillBar>("UILayer/CombatSkillBar");
        // 即时战斗场景只允许有固定技能槽。旧场景/热重载可能还带着
        // SkillHandPanel 或旧版 BattleActionProgress；清掉旧节点，
        // 再由 BattlePlanHud 创建当前版行动进度条，避免重复显示。
        _handPanel = scene.GetNodeOrNull<Node>("UILayer/SkillHandPanel");
        if (_skillBar is not null)
        {
            RemoveLegacyRealtimePanels();
            _handPanel = null;
        }
        _statusPanel = scene.GetNodeOrNull<BattleStatusPanel>("UILayer/BattleStatusPanel");
        _statusPanel?.Hide();
        if(_skillBar is not null)_skillBar.ProcessMode=ProcessModeEnum.Always;
        unitOverlay=new BattleUnitOverlay();unitOverlay.Setup(this);AddChild(unitOverlay);
        _planHud = new BattlePlanHud();
        _planHud.ProcessMode = ProcessModeEnum.Always;
        _planHud.Hide();
        uiLayer.AddChild(_planHud);
        _planHud.Setup(this);
        _supplyHud = new BattleSupplyHud();
        scene.AddChild(_supplyHud);
        _supplyHud.Setup(this);
        if (_skillBar is not null)
        {
            _skillBar.skill_selected += on_skill_selected;
            _skillBar.skill_target_selected += on_skill_target_selected;
            _skillBar.Hide();
        }
        _handPanel?.Call("hide_hand");
        _sceneUiConnected = true;
    }

    private void RemoveLegacyRealtimePanels()
    {
        var scene = GetParent() ?? GetTree().CurrentScene;
        if (scene is null) return;
        foreach (var path in new[] { "UILayer/SkillHandPanel", "UILayer/BattleActionProgress" })
        {
            var legacy = scene.GetNodeOrNull<Node>(path);
            if (legacy is null) continue;
            if (legacy is CanvasItem canvas) canvas.Hide();
            legacy.ProcessMode = ProcessModeEnum.Disabled;
            legacy.QueueFree();
        }
        // 旧场景还可能通过组注册了手牌面板（例如热重载后场景文件已更新，
        // 但运行中的实例仍保留旧节点）。按组清理可避免孤立 BattleCard 留在最上层。
        foreach (var node in GetTree().GetNodesInGroup("skill_hand_panel").OfType<Node>().ToArray())
        {
            if (!GodotObject.IsInstanceValid(node) || !scene.IsAncestorOf(node)) continue;
            if (node is CanvasItem canvas) canvas.Hide();
            node.ProcessMode = ProcessModeEnum.Disabled;
            node.QueueFree();
        }
        // InfoPanel 只在双击角色时打开；即时战斗启动时不允许旧的牌面检查器残留。
        var infoPanel = scene.GetNodeOrNull<CanvasItem>("UILayer/InfoPanel");
        infoPanel?.Hide();
        // 某些旧版本曾把 BattleCard 直接挂到 UILayer，既不在手牌节点下也不带组。
        // 只清理战斗场景中的孤立牌面，保留 InfoPanel 内用于人物档案的牌面预览。
        foreach (var node in EnumerateDescendants(scene).Where(n => n is SkillCard || n.Name == "BattleCard").ToArray())
        {
            if (!GodotObject.IsInstanceValid(node) || infoPanel is not null && infoPanel.IsAncestorOf(node)) continue;
            if (node is CanvasItem canvas) canvas.Hide();
            node.ProcessMode = ProcessModeEnum.Disabled;
            node.QueueFree();
        }
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is not Node node) continue;
            yield return node;
            foreach (var descendant in EnumerateDescendants(node)) yield return descendant;
        }
    }

    public void setup_battle_from_packed(PackedScene attacker, PackedScene defender, BattleEntityData attackerData, BattleEntityData defenderData) =>
        setup_team_battle(new Godot.Collections.Array { attacker }, new Godot.Collections.Array { defender }, new Godot.Collections.Array { attackerData }, new Godot.Collections.Array { defenderData });

    public void setup_team_battle(Godot.Collections.Array attackerScenes, Godot.Collections.Array defenderScenes, Godot.Collections.Array attackerData, Godot.Collections.Array defenderData)
    {
        // 若场景切换时延迟挂载尚未执行，必须先建好队列 HUD，再启动首轮规划。
        connect_skill_bar();
        ResetBattle();
        SpawnTeam(attackerScenes, attackerData); SpawnTeam(defenderScenes, defenderData);
        FitBattleFormation(true);
        battle_running = all_units.Count > 0; turn_state = TurnState.IDLE;
        InitializeRealtimeBattle();
    }

    private void SpawnTeam(Godot.Collections.Array scenes, Godot.Collections.Array data)
    {
        for (var i = 0; i < Mathf.Min(scenes.Count, data.Count); i++)
        {
            if (scenes[i].AsGodotObject() is not PackedScene packed || data[i].AsGodotObject() is not BattleEntityData snapshot) continue;
            // 测试入口和正式入口均允许提供可实例化的角色场景；资源异常时用标准角色兜底，不能把空队伍误判成战斗结束。
            var unit = packed.Instantiate();
            if (unit is not WorldEntity) { GD.PushError($"BattleManager: 战斗单位场景无效，使用标准角色回退：{packed.ResourcePath}"); unit?.QueueFree(); unit = new Character(); }
            // 隔离战斗资源，避免 PackedScene 共用数据污染世界角色或演练存档。
            if(unit is WorldEntity entity && entity.entity_data is not null)entity.entity_data=entity.entity_data.Duplicate(true) as EntityData;
            DeployLayer?.AddChild(unit);
            unit.Set("initialized_from_data", true); unit.SetMeta("artifact_battle_clone", true); unit.Call("apply_battle_data", snapshot);
            unit.SetMeta("battle_dun_speed",snapshot.dun_speed);
            unit.SetMeta("basic_deck_path", snapshot.basic_deck_path);
            unit.SetMeta("active_skill_paths", snapshot.active_skill_paths);
            unit.SetMeta("loaded_skill_paths", snapshot.loaded_skill_paths);
            unit.SetMeta("combat_skill_slot_paths", snapshot.combat_skill_slot_paths);
            unit.SetMeta("combat_resolved_skills", snapshot.combat_resolved_skills.Duplicate(true));
            unit.SetMeta("combat_skill_slots_configured", snapshot.combat_skill_slots_configured);
            unit.SetMeta("combat_passive_paths", snapshot.combat_passive_paths);
            unit.SetMeta("combat_heart_method_upgrades", snapshot.combat_heart_method_upgrades);
            unit.SetMeta("battle_deck_configured", snapshot.battle_deck_configured);
            _resourceSnapshots[unit] = new BattleResourceSnapshot
            {
                CurrentStamina = unit.Get("current_stamina").AsInt32(),
                MaxStamina = unit.Get("max_stamina").AsInt32(),
                CurrentMana = unit.Get("current_mana").AsInt32(),
                MaxMana = unit.Get("max_mana").AsInt32()
            };
            // 体力在战斗中使用独立的 6 点上限；法力上限沿用角色境界快照。
            unit.Set("max_stamina", snapshot.test_stamina_cap_override >= 0 ? snapshot.test_stamina_cap_override : BattleEnergyCap); unit.Set("current_stamina", 0);
            unit.Set("current_mana", 0);
            if (unit is Creature creature)
            {
                // 气血是公共战斗资源；所有修士统一初始化，不依赖秘籍分类或历史存档字段。
                creature.initialize_body_combat_state(unit is Character);
                creature.apply_combat_tempering_stats();
                if (CultivationInspirationSystem.Enabled && snapshot.cultivation_inspiration_starting_shield > 0)
                    creature.shield += snapshot.cultivation_inspiration_starting_shield;
            }
            // 测试专用上限在身体／淬体初始化之后覆盖，避免被原容量推导重写。
            if(snapshot.test_stamina_cap_override>=0)unit.Set("max_stamina",snapshot.test_stamina_cap_override);
            if(snapshot.test_mana_cap_override>=0)unit.Set("max_mana",snapshot.test_mana_cap_override);
            // 战斗由角色身体区域的双击入口处理，避免世界实体单击就打开面板。
            unit.Set("input_pickable", false); unit.Set("visible", true);
            if (!_spectatorBattle) AddPaperDoll(unit, snapshot);
            var cell = FormationCell(snapshot.faction, FactionCount(snapshot.faction));
            Map?.Call("register_unit", unit, cell);
            if (unit is Node2D node) node.GlobalPosition = Map?.Call("get_cell_center", cell).AsVector2() ?? Vector2.Zero;
            all_units.Add(unit);
            if (snapshot.faction == 1) player_units.Add(unit); else enemy_units.Add(unit);

            action_gauges[unit] = GD.RandRange(0, ActionThreshold * .35f);
            _drawElapsedFrames[unit] = 0f;
            // 即时战斗不建立牌堆；凝核牌局使用自己的牌堆节点，不经过 BattleManager。
            BattleDeckManager? deck = null;
            if (!_realtimeMode)
            {
                deck = new BattleDeckManager(); AddChild(deck); deck.setup(unit); _decks[unit] = deck;
            }
            battle_units[unit] = new BattleUnit(); ((BattleUnit)battle_units[unit].AsGodotObject()!).setup(unit, deck);
            initialize_formation_circuits(unit, snapshot);
            InitializeArtifacts(unit, snapshot);
            ForgedEquipmentLoadout.ApplyBattleBonuses(unit, snapshot.trial_forged_equipment_json);
        }
    }
}
