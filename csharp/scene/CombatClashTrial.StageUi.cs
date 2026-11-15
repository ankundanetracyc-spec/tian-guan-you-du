using Godot;

public partial class CombatClashTrial
{
    private TrialCombatantView? _playerView;
    private TrialCombatantView? _enemyView;

    private static T Place<T>(Control parent, T control, float x, float y, float width, float height) where T : Control
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
        parent.AddChild(control);
        return control;
    }

    private static Label StageLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static ColorRect StagePlate(Color color)
        => new() { Color = color, MouseFilter = MouseFilterEnum.Ignore };

    private void BuildStageUi()
    {
        var canvas = new Control { Name = "BattleStage", MouseFilter = MouseFilterEnum.Pass };
        canvas.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(canvas);

        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/battle/forest_stone_small_20261109/ground.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(.74f, .76f, .70f)
        };
        Place(canvas, background, 0, 0, 1280, 407);
        Place(canvas, StagePlate(new Color(.10f, .13f, .12f, .25f)), 0, 0, 1280, 407);
        Place(canvas, StagePlate(new Color(.075f, .09f, .085f, .96f)), 0, 0, 1280, 52);
        Place(canvas, StagePlate(new Color(.075f, .09f, .085f, .98f)), 0, 405, 1280, 315);
        Place(canvas, StagePlate(new Color(.12f, .15f, .13f, .82f)), 370, 63, 540, 333);

        _phase = Place(canvas, StageLabel("", 18, new Color("eadfc8")), 22, 12, 210, 28);
        _status = Place(canvas, StageLabel("", 16, new Color("eadfc8")), 220, 14, 850, 24);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        var note = Place(canvas, StageLabel("试玩 · 暂无定力、失神、击退、守御、心法、法宝与暴击", 12, new Color("c9c8b5")), 645, 53, 560, 21);
        note.HorizontalAlignment = HorizontalAlignment.Right;

        _playerView = Place(canvas, new TrialCombatantView { Name = "PlayerCombatant" }, 35, 65, 320, 325);
        _playerView.Configure(true);
        _enemyView = Place(canvas, new TrialCombatantView { Name = "EnemyCombatant" }, 925, 65, 320, 325);
        _enemyView.Configure(false);

        Place(canvas, StageLabel("出手队列", 17, new Color("e8dcc1")), 388, 73, 150, 25);
        _playerQueues = Place(canvas, StageLabel("", 15, new Color("f0ead8")), 388, 102, 505, 66);
        _playerQueues.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var hidden = Place(canvas, StageLabel("对手一队  速度3：招式隐藏\n对手二队  速度1：招式隐藏", 14, new Color("c7c9bc")), 388, 167, 480, 43);
        hidden.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Place(canvas, StagePlate(new Color(.045f, .06f, .055f, .72f)), 382, 213, 516, 172);
        Place(canvas, StageLabel("交锋记录", 15, new Color("d7c7a4")), 392, 219, 140, 22);
        _log = Place(canvas, new RichTextLabel
        {
            Name = "BattleLog", ScrollFollowing = true, FitContent = false,
            SelectionEnabled = true, MouseFilter = MouseFilterEnum.Stop
        }, 392, 245, 494, 132);
        _log.AddThemeFontSizeOverride("normal_font_size", 14);

        var actionRow = Place(canvas, new HBoxContainer { Name = "ActionRow" }, 24, 403, 1232, 43);
        actionRow.AddThemeConstantOverride("separation", 5);
        for (var queue = 0; queue < 2; queue++) for (var target = 0; target < 2; target++)
        {
            var q = queue; var t = target;
            var button = MakeButton($"我{q + 1} → 敌{t + 1}");
            button.CustomMinimumSize = new Vector2(134, 39);
            button.Pressed += () => { if (_selected is { } key) QueuePlayerCard(key, q, t); };
            actionRow.AddChild(button); _planningButtons.Add(button);
        }
        for (var queue = 0; queue < 2; queue++)
        {
            var q = queue;
            var button = MakeButton($"撤回{q + 1}队");
            button.CustomMinimumSize = new Vector2(113, 39);
            button.Pressed += () => WithdrawLast(q);
            actionRow.AddChild(button); _planningButtons.Add(button);
        }
        _commit = MakeButton(""); AddButtonText(_commit, "确定排招"); _commit.Name = "CommitPlan";
        _commit.CustomMinimumSize = new Vector2(125, 39); _commit.Pressed += CommitPlan; actionRow.AddChild(_commit);
        _advance = MakeButton(""); AddButtonText(_advance, "推进一小轮"); _advance.Name = "AdvanceSubround";
        _advance.CustomMinimumSize = new Vector2(145, 39); _advance.Pressed += AdvanceSubround; actionRow.AddChild(_advance);

        _selection = Place(canvas, StageLabel("先点击手牌，再指定队列和目标。", 14, new Color("d5ceb8")), 28, 446, 1000, 21);
        var handTitle = Place(canvas, StageLabel("手牌 · 点击选择", 16, new Color("eee2c9")), 28, 467, 220, 24);
        var scroll = Place(canvas, new ScrollContainer
        {
            Name = "HandScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        }, 25, 490, 1230, 210);
        var handCenter = new CenterContainer { Name = "HandCenter", CustomMinimumSize = new Vector2(1230, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(handCenter);
        _handBox = new HBoxContainer { Name = "HandCards", SizeFlagsVertical = SizeFlags.ExpandFill };
        _handBox.AddThemeConstantOverride("separation", 9); handCenter.AddChild(_handBox);
    }
}
