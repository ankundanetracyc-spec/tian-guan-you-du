using Godot;

/// <summary>Checks all current fixed creature boards, mixed board battle entry, and the no-board entry gate.</summary>
public partial class CreatureFixedBoardSmoke : Node
{
    private int _checks;
    private Node2D _scene = null!;
    private BattleManager _manager = null!;

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("CREATURE_BOARD_PASS " + message);
    }

    private async Task Frames(int count = 1)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void CheckConfiguration(string name, BasicDeckResource? deck, CombatBoardLayout? layout,
        IReadOnlyDictionary<string, int> expectedCounts, int expectedEmptyCells)
    {
        if (deck is null || layout is null) throw new InvalidOperationException(name + "未配置技能库或固定盘。");
        var catalog = new CombatBoardCatalog();
        foreach (var path in deck.get_deck_skills().Distinct(StringComparer.Ordinal))
        {
            var skill = GD.Load<ActiveSkillData>(path) ?? throw new InvalidOperationException("缺少兽招：" + path);
            Check(skill.has_board_definition(), skill.skill_name + "有明确占格与份数");
            catalog.skills.Add(skill);
        }
        var issues = CombatBoardGeometry.Validate(layout, catalog.FindSkill, key => catalog.FindSkill(key) is not null);
        Check(issues.Count == 0, name + "固定盘无越界、重叠或来源缺失");
        Check(layout.size == new Vector2I(3, 3) && layout.confirmed, name + "采用已确认3×3固定盘");
        var counts = layout.placements.GroupBy(item => catalog.FindSkill(item.source_key)!.skill_name)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Check(expectedCounts.OrderBy(pair => pair.Key).SequenceEqual(counts.OrderBy(pair => pair.Key)), name + "盘上招式与技能库一致");
        var occupied = layout.placements.Sum(item => CombatBoardGeometry.Footprint(item, catalog.FindSkill).X * CombatBoardGeometry.Footprint(item, catalog.FindSkill).Y);
        Check(9 - occupied == expectedEmptyCells, name + "真实空格数与配置一致");
        var snapshot = new BattleEntityData
        {
            entity_type = (int)WorldEntity.EntityType.BEAST, entity_name = name,
            combat_board_layout = layout.CopyConfiguration(),
            active_skill_paths = new Godot.Collections.Array<string>(deck.get_deck_skills().Distinct(StringComparer.Ordinal))
        };
        Check(BeastCombatBoardRules.TryValidateBattleSnapshots([snapshot], out _), name + "快照通过战前参战校验");
    }

    private void CheckSignatureSkill(string name, string path, string formula, string attackStyle)
    {
        var skill = GD.Load<ActiveSkillData>(path) ?? throw new InvalidOperationException("缺少专属兽技：" + path);
        Check(skill.skill_name == name && skill.damage_formula == formula, name + "名称与伤害公式准确");
        Check(skill.energy_cost == 1 && skill.mana_cost == 0 && skill.spirit_occupy == 0,
            name + "仅消耗1体力，不消耗法力或灵识");
        Check(skill.target_type == 2 && skill.range_min == 1 && skill.range_max == 1,
            name + "指定1名合法敌方，不增加范围机制");
        Check(skill.damage_type == "physical" && skill.attack_style == attackStyle,
            name + "伤害类型与出手方式正确");
        Check(skill.tags.Count == 0 && skill.keywords.Count == 0
            && skill.status_effects.Count == 0 && skill.special_effects.Count == 0,
            name + "不添加关键词、状态或额外效果");
        Check(skill.cooldown_rounds == 0 && skill.max_charges == 0
            && skill.uses_per_round == 0 && skill.uses_per_battle == 0,
            name + "不添加回合冷却或额外次数限制");
        Check(skill.has_board_definition() && skill.board_size == new Vector2I(2, 1)
            && skill.board_max_placements == 1 && !skill.board_rotation_allowed,
            name + "采用2×1、单份、不可旋转招式盘定义");
    }

    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }

    private async void Run()
    {
        try
        {
            var wolf = GD.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres")!;
            var boar = GD.Load<BeastTemplate>("res://resources/beast_templates/灰背野猪_试玩.tres")!;
            var bei = GD.Load<BeastTemplate>("res://resources/beast_templates/林地妖狈.tres")!;
            CheckConfiguration(wolf.display_name, wolf.deck, wolf.combat_board_layout, new Dictionary<string, int> { ["撕咬"] = 2, ["游身架招"] = 1 }, 6);
            CheckConfiguration(boar.display_name, boar.deck, boar.combat_board_layout, new Dictionary<string, int> { ["拱撞"] = 2, ["拱身架招"] = 1 }, 4);
            CheckConfiguration(bei.display_name, bei.deck, bei.combat_board_layout, new Dictionary<string, int> { ["狈啸"] = 2, ["唤猎"] = 1 }, 4);
            foreach (var (path, expected, emptyCells) in new[]
            {
                ("res://resources/beast_templates/青尾狐.tres", new Dictionary<string, int> { ["撕咬"] = 1, ["狐火点名"] = 1, ["游身架招"] = 1 }, 5),
                ("res://resources/beast_templates/赤鬃狼.tres", new Dictionary<string, int> { ["撕咬"] = 1, ["裂风扑击"] = 1, ["游身架招"] = 1 }, 5),
                ("res://resources/beast_templates/岩背獾.tres", new Dictionary<string, int> { ["拱撞"] = 1, ["翻土突袭"] = 1, ["拱身架招"] = 1 }, 4),
            })
            {
                var template = GD.Load<BeastTemplate>(path) ?? throw new InvalidOperationException("缺少新增生灵模板：" + path);
                CheckConfiguration(template.display_name, template.deck, template.combat_board_layout, expected, emptyCells);
            }
            CheckSignatureSkill("狐火点名", "res://resources/activeskills/妖兽/青尾狐/狐火点名.tres",
                "round(7 + intelligence*0.8 + soul*0.8)", "ranged");
            CheckSignatureSkill("裂风扑击", "res://resources/activeskills/妖兽/赤鬃狼/裂风扑击.tres",
                "round(9 + physique*1.5)", "melee");
            CheckSignatureSkill("翻土突袭", "res://resources/activeskills/妖兽/岩背獾/翻土突袭.tres",
                "round(12 + physique + soul*0.5)", "melee");
            foreach (var (path, expected) in new[]
            {
                ("res://resources/beast_modifiers/血牙凶狼.tres", new Dictionary<string, int> { ["撕咬"] = 1, ["裂喉"] = 1, ["游身架招"] = 1 }),
                ("res://resources/beast_modifiers/影足凶狼.tres", new Dictionary<string, int> { ["撕咬"] = 1, ["掠影扑杀"] = 1, ["游身架招"] = 1 }),
                ("res://resources/beast_modifiers/迅蹄野猪.tres", new Dictionary<string, int> { ["拱撞"] = 1, ["疾拱"] = 1, ["拱身架招"] = 1 }),
                ("res://resources/beast_modifiers/铁鬃野猪.tres", new Dictionary<string, int> { ["拱撞"] = 1, ["拱身架招"] = 1, ["硬鬃架招"] = 1 })
            })
            {
                var modifier = GD.Load<BeastModifier>(path)!;
                CheckConfiguration(modifier.display_name, modifier.deck_override, modifier.combat_board_layout_override, expected,
                    path.EndsWith("迅蹄野猪.tres", StringComparison.Ordinal) ? 4 : 5);
            }
            var undefinedSkill = GD.Load<ActiveSkillData>("res://resources/activeskills/妖兽/林地凶狼/撕咬.tres")!.Duplicate() as ActiveSkillData
                ?? throw new InvalidOperationException("无法复制撕咬用于缺失盘面定义检查。");
            undefinedSkill.board_definition_version = 0;
            undefinedSkill.board_base_skill_path = "res://resources/activeskills/妖兽/林地凶狼/撕咬.tres";
            Check(!BeastCombatBoardRules.TryPrepare(wolf.combat_board_layout, [undefinedSkill], out _, out _, out var undefinedReason)
                && undefinedReason.Contains("尚无招式盘定义", StringComparison.Ordinal), "缺少招式盘定义的技能无法进入固定盘");

            _scene = new Node2D { Name = "CreatureBoardIsolated" }; AddChild(_scene);
            _scene.AddChild(new Node2D { Name = "DeployLayer" });
            var ui = new CanvasLayer { Name = "UILayer" }; _scene.AddChild(ui);
            ui.AddChild(GD.Load<PackedScene>("res://tscn/panel/CombatSkillBar.tscn")!.Instantiate());
            _manager = new BattleManager { Name = "BattleMgr", deploy_layer = "../DeployLayer", isolated_trial_battle = true };
            _scene.AddChild(_manager); await Frames(2);

            var playerSkill = GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres")!;
            var playerCatalog = new CombatBoardCatalog { skills = [playerSkill] };
            var playerData = new BattleEntityData
            {
                uid = "creature-board-smoke-player", entity_name = "试玩修士", entity_type = (int)WorldEntity.EntityType.CHARACTER,
                faction = 1, cultivation_realm = "云息三层", current_health = 10000, max_health = 10000,
                max_stamina = 6, max_mana = 0, initiative_speed = 100, combat_skill_slots_configured = true,
                combat_board_catalog = playerCatalog, combat_resolved_skills = [playerSkill],
                combat_board_layout = new CombatBoardLayout
                {
                    placements = [new CombatBoardPlacement { placement_id = "player_attack", source_key = playerSkill.get_board_skill_key(), kind = CombatBoardPlacement.ObjectKind.Skill }]
                }
            };
            var beastTemplate = GD.Load<BeastTemplate>("res://resources/beast_templates/林地妖狈.tres")!;
            var beast = new Beast
            {
                Name = "FixedBoardBei", unique_id = "creature-board-smoke-bei", beast_template = beastTemplate,
                entity_data = new LivingData(), faction = (int)WorldEntity.Faction.ENEMY, Visible = false
            };
            AddChild(beast); await Frames(2);
            var beastScene = beast.pack_for_battle() ?? throw new InvalidOperationException("无法构造妖狈测试单位。");
            var beastData = beast.get_snapshot_data();
            Check(beastData.combat_board_layout?.placements.Count == 3, "妖狈固定盘随战斗快照传递");
            Check(beastData.active_skill_paths.Count == 2, "妖狈快照携带全部盘上招式路径");
            RemoveChild(beast); beast.Free();

            var characterScene = GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn")!;
            _manager.setup_team_battle([characterScene], [beastScene], [playerData], [beastData]);
            await Frames(5);
            var battleBeast = _manager.enemy_units.OfType<Beast>().Single();
            var runtimeBoard = _manager.get_board_state(battleBeast);
            Check(_manager.is_board_battle() && _manager.is_board_editing(), "修士与配置过的妖狈进入共同招式盘流程");
            Check(runtimeBoard?.Layout.placements.Count == 3, "战斗中的妖狈实际读取三份固定盘位");
            Check(_manager.get_board_catalog(battleBeast)?.skills.Count == 2, "妖狈只载入本物种已有招式");
            Check(_manager.get_board_state(battleBeast)!.Layout.placements.All(item => item.kind == CombatBoardPlacement.ObjectKind.Skill), "生灵盘不出现人物化法、丹药或其他对象");

            var unknownBeastScene = new PackedScene();
            var unknownBeast = new Beast { Name = "UnconfiguredBeast", species_id = "unconfigured_beast", beast_deck = GD.Load<BasicDeckResource>("res://resources/beast_decks/林地凶狼.tres") };
            Check(unknownBeastScene.Pack(unknownBeast) == Error.Ok, "可构造缺盘生灵拦截用场景"); unknownBeast.Free();
            var rejectedSnapshot = new BattleEntityData
            {
                uid = "creature-board-smoke-unconfigured", entity_name = "未配置生灵", entity_type = (int)WorldEntity.EntityType.BEAST,
                faction = 2, current_health = 100, max_health = 100,
                active_skill_paths = ["res://resources/activeskills/妖兽/林地凶狼/撕咬.tres"]
            };
            Check(!BeastCombatBoardRules.TryValidateBattleSnapshots([rejectedSnapshot], out var rejectionReason)
                && rejectionReason.Contains("没有完整的固定招式盘", StringComparison.Ordinal), "战前校验拒绝缺盘快照");
            _manager.setup_team_battle([characterScene], [unknownBeastScene], [playerData], [rejectedSnapshot]);
            await Frames(4);
            Check(!_manager.is_board_battle() && !_manager.battle_running && _manager.get_board_entry_rejection_reason().Contains("没有完整的固定招式盘", StringComparison.Ordinal),
                "缺少固定盘的生灵被拦截且不回退旧流程");
            GD.Print($"CREATURE_FIXED_BOARD PASS: {_checks} checks");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString()); GetTree().Quit(1);
        }
    }
}
