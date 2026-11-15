using Godot;

/// <summary>Manually playable planning example. Every combat value is a local demonstration input.</summary>
public partial class BoardHandPlanningDemo : Control
{
    private BoardHandPlanningView? _view;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        StartExample();
    }

    private void StartExample()
    {
        if (_view is not null) { RemoveChild(_view); _view.QueueFree(); }
        var samples = new[]
        {
            ("res://resources/activeskills/应敌.tres", 1),
            ("res://resources/activeskills/痛击.tres", 2),
            ("res://resources/activeskills/剑修基础/格剑.tres", 1),
            ("res://resources/activeskills/聚炁.tres", 1)
        };
        var cards = samples.Select((sample, index) => new BoardCard(new BoardCardKey(1, "sample-" + index),
            sample.Item1, GD.Load<ActiveSkillData>(sample.Item1)
                ?? throw new InvalidOperationException("无法载入试玩招式：" + sample.Item1))).ToArray();
        var points = cards.Select((card, index) => (card.Key, samples[index].Item2))
            .ToDictionary(item => item.Key, item => item.Item2);
        var state = new BoardHandState(cards, new Random(15));
        state.DrawOpeningHand();
        var enemies = new[] { new BoardPublicEnemyQueue("sample-a", "试演对手 · 一路", 10),
            new BoardPublicEnemyQueue("sample-b", "试演对手 · 二路", 7) };
        _view = new BoardHandPlanningView { Name = "PlanningView" };
        _view.Configure(state, enemies, 4, 6, 1, 2, card => points.TryGetValue(card.Key, out var cost) ? cost : null);
        _view.PlanConfirmed += plan => GD.Print("BOARD_HAND_PLANNING_DEMO_SUBMITTED queues=" + plan.Queues.Count);
        AddChild(_view);
        var layout = _view.GetNode<VBoxContainer>("Content/Layout");
        var banner = new Label { Name = "DemoNotice", Text = "排牌体验：招式点数和敌方速度仅为样例，未接入正式战斗。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        banner.AddThemeFontSizeOverride("font_size", 17);
        banner.AddThemeColorOverride("font_color", new Color("e8c995"));
        layout.AddChild(banner); layout.MoveChild(banner, 1);
        var reset = new Button { Name = "RestartExample", Text = "重新体验", CustomMinimumSize = new(160, 46),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        BattleBoardSkin.Skin(reset); reset.Pressed += StartExample; layout.AddChild(reset);
        GD.Print("BOARD_HAND_PLANNING_DEMO_READY sample_values_only");
    }
}
