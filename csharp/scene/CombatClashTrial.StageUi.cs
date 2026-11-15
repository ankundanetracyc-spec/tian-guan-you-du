using Godot;

public partial class CombatClashTrial
{
    private static readonly Vector2 StageSize = new(1280, 720);
    private static readonly Color Cream = new("eee4cd"), LightInk = new("b8b8a4"), Edge = new("777765");
    private const string GroundArt = "res://textures/battle/forest_stone_small_20261109/";

    private Control _stage = null!;
    private Label _playerResources = null!, _enemyResources = null!;
    private Label _drawCount = null!, _discardCount = null!;
    private Label _exchangeTitle = null!, _exchangeDetail = null!;
    private Panel _exchangePanel = null!, _report = null!;
    private TrialCombatantView _playerView = null!, _enemyView = null!;
    private string _planningNotice = "";

    private static T Place<T>(Control parent, T control, float x, float y, float width, float height) where T : Control
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
        parent.AddChild(control);
        return control;
    }

    private static Label StageLabel(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center,
            ClipText = true, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static ColorRect StagePlate(Color color) => new() { Color = color, MouseFilter = MouseFilterEnum.Ignore };

    private static Panel StagePanel(Color fill, Color stroke, int radius = 4, int shadow = 0)
    {
        var panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", TrialBattleArt.Frame(fill, stroke, radius, shadow));
        return panel;
    }

    private static TextureRect StageTexture(Texture2D? texture, bool cover = false) => new()
    {
        Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = cover ? TextureRect.StretchModeEnum.KeepAspectCovered : TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private void BuildStageUi()
    {
        var matte = StagePlate(new Color("151d19"));
        AddChild(matte);
        matte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _stage = new Control { Name = "BattleStage", Size = StageSize, MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        _stage.Theme = new Theme { DefaultFont = GD.Load<Font>(CraftingPaperStyle.FontPath) };
        AddChild(_stage);
        Resized += PositionStage;
        PositionStage();

        BuildArena();
        BuildQueues();
        BuildHand();
        BuildReport();
    }

    private void PositionStage()
    {
        var factor = Mathf.Min(Size.X / StageSize.X, Size.Y / StageSize.Y);
        _stage.Scale = Vector2.One * factor;
        _stage.Position = (Size - StageSize * factor) / 2f;
    }

    private void BuildArena()
    {
        var ground = StageTexture(GD.Load<Texture2D>(GroundArt + "ground.png"), true);
        ground.Modulate = new Color("b1b3a3");
        Place(_stage, ground, 0, 0, 1280, 354);
        Place(_stage, StagePlate(new Color("293c35", .18f)), 0, 0, 1280, 354);
        var pines = GD.Load<Texture2D>(GroundArt + "pines.png");
        var rocks = GD.Load<Texture2D>(GroundArt + "rocks.png");
        var leftPines = StageTexture(pines); leftPines.Modulate = new Color(.70f, .74f, .65f, .67f);
        var rightPines = StageTexture(pines); rightPines.Modulate = leftPines.Modulate; rightPines.FlipH = true;
        Place(_stage, leftPines, -47, 53, 236, 173);
        Place(_stage, rightPines, 1091, 52, 236, 173);
        var leftRocks = StageTexture(rocks); leftRocks.Modulate = new Color(.71f, .72f, .63f, .75f);
        var rightRocks = StageTexture(rocks); rightRocks.Modulate = leftRocks.Modulate; rightRocks.FlipH = true;
        Place(_stage, leftRocks, -27, 263, 170, 82);
        Place(_stage, rightRocks, 1137, 263, 170, 82);

        Place(_stage, StagePlate(new Color("18211d")), 0, 0, 1280, 56);
        Place(_stage, StageTexture(TrialBattleArt.InkCloth, true), 0, 0, 1280, 56);
        Place(_stage, StagePlate(new Color("151d19", .38f)), 0, 0, 1280, 56);
        Place(_stage, StagePlate(new Color("9b9377", .66f)), 0, 55, 1280, 1);
        Place(_stage, StageLabel("我方", 20, Cream), 35, 12, 52, 29);
        Place(_stage, StageLabel("对手", 20, Cream, HorizontalAlignment.Right), 1192, 12, 51, 29);
        _playerResources = Place(_stage, StageLabel("", 16, Cream), 95, 14, 270, 25);
        _enemyResources = Place(_stage, StageLabel("", 16, Cream, HorizontalAlignment.Right), 915, 14, 270, 25);
        Place(_stage, StageLabel("灵机试剑", 23, Cream, HorizontalAlignment.Center), 487, 1, 306, 30);
        _phase = Place(_stage, StageLabel("", 14, new Color("c5baa1"), HorizontalAlignment.Center), 430, 29, 420, 22);

        _playerView = new TrialCombatantView { Name = "PlayerCombatant" };
        _playerView.Configure(true);
        Place(_stage, _playerView, 55, 64, 340, 288);
        _enemyView = new TrialCombatantView { Name = "EnemyCombatant" };
        _enemyView.Configure(false);
        Place(_stage, _enemyView, 885, 64, 340, 288);

        _exchangePanel = Place(_stage, StagePanel(new Color("18251e", .93f), new Color("ac9b72", .8f), 3, 5), 438, 165, 404, 94);
        _exchangeTitle = Place(_exchangePanel, StageLabel("", 23, Cream, HorizontalAlignment.Center), 12, 13, 380, 32);
        _exchangeDetail = Place(_exchangePanel, StageLabel("", 16, new Color("d0c4a6"), HorizontalAlignment.Center), 12, 48, 380, 31);
        _exchangePanel.Hide();

        Place(_stage, StagePlate(new Color("202a24")), 0, 352, 1280, 368);
        Place(_stage, StageTexture(TrialBattleArt.InkCloth, true), 0, 352, 1280, 368);
        Place(_stage, StagePlate(new Color("111b16", .44f)), 0, 352, 1280, 368);
        Place(_stage, StagePlate(new Color("887c62")), 0, 352, 1280, 1);
        Place(_stage, StagePlate(new Color("a39b81", .38f)), 36, 493, 1208, 1);
        _selection = Place(_stage, StageLabel("", 14, Cream), 38, 466, 1200, 24);
    }

    private void PresentExchange(string headline, string detail)
    {
        _exchangeTitle.Text = headline;
        _exchangeDetail.Text = detail;
        _exchangePanel.Show();
    }

    private void BuildHand()
    {
        var draw = StageTexture(CondensationUiArt.DrawPile);
        var discard = StageTexture(CondensationUiArt.DiscardPile);
        draw.Modulate = discard.Modulate = new Color(.94f, .94f, .89f, .96f);
        Place(_stage, draw, 43, 542, 101, 105);
        Place(_stage, discard, 1135, 502, 87, 75);
        _drawCount = Place(_stage, StageLabel("", 14, Cream, HorizontalAlignment.Center), 34, 651, 118, 25);
        _discardCount = Place(_stage, StageLabel("", 14, Cream, HorizontalAlignment.Center), 1115, 578, 124, 25);
        _handLayer = Place(_stage, new Control { Name = "HandCards", MouseFilter = MouseFilterEnum.Ignore }, 170, 498, 940, 210);

        _commit = MainAction("CommitPlan", "确定排招", CommitPlan);
        _advance = MainAction("AdvanceSubround", "推进一小轮", AdvanceSubround);
    }

    private Button MainAction(string name, string text, Action pressed)
    {
        var button = new Button { Name = name, MouseDefaultCursorShape = CursorShape.PointingHand };
        CraftingPaperStyle.ApplyButton(button, CraftingPaperStyle.ButtonKind.Primary, 17);
        button.CustomMinimumSize = Vector2.Zero;
        var label = StageLabel(text, 17, CraftingPaperStyle.Ink, HorizontalAlignment.Center);
        button.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        button.Pressed += pressed;
        return Place(_stage, button, 1103, 632, 142, 44);
    }

    private void BuildReport()
    {
        _report = Place(_stage, StagePanel(new Color("1d2922", .98f), new Color("918773"), 4, 6), 419, 69, 442, 271);
        _report.Name = "BattleReport";
        _report.MouseFilter = MouseFilterEnum.Stop;
        _report.ZIndex = 45;
        Place(_report, StageLabel("交锋记录", 18, Cream), 16, 8, 180, 30);
        _log = Place(_report, new RichTextLabel
        {
            Name = "BattleLog", ScrollFollowing = true, FitContent = false,
            SelectionEnabled = true, MouseFilter = MouseFilterEnum.Stop
        }, 16, 43, 410, 211);
        _log.AddThemeFontSizeOverride("normal_font_size", 15);
        _log.AddThemeColorOverride("default_color", Cream);
        _report.Hide();

        var toggle = new Button { Name = "ToggleBattleReport", Text = "交锋记录", MouseDefaultCursorShape = CursorShape.PointingHand };
        CraftingPaperStyle.ApplyButton(toggle, CraftingPaperStyle.ButtonKind.Regular, 14);
        toggle.CustomMinimumSize = Vector2.Zero;
        toggle.Pressed += () =>
        {
            _report.Visible = !_report.Visible;
            toggle.Text = _report.Visible ? "收起记录" : "交锋记录";
        };
        Place(_stage, toggle, 808, 12, 108, 31);
    }

    private void RefreshStage()
    {
        _payment.TryGetBalance(_player.Id, out var player);
        _payment.TryGetBalance(_enemy.Id, out var enemy);
        _playerResources.Text = $"体力 {player.Stamina}/{_player.Stats.max_stamina}  ·  法力 {player.Mana}/{_player.Stats.max_mana}";
        _enemyResources.Text = $"体力 {enemy.Stamina}/{_enemy.Stats.max_stamina}  ·  法力 {enemy.Mana}/{_enemy.Stats.max_mana}";
        _playerView.Refresh(_player.Stats.current_health, _player.Stats.max_health, _player.Qi);
        _enemyView.Refresh(_enemy.Stats.current_health, _enemy.Stats.max_health, _enemy.Qi);
        _phase.Text = _finished ? "试剑结束" : _planning ? $"第 {_round} 回合 · 排招" : $"第 {_round} 回合 · 第 {_subround + 1} 小轮";
        _drawCount.Text = $"抽牌堆 {_player.Hand.DrawPile.Count}";
        _discardCount.Text = $"弃牌堆 {_player.Hand.DiscardPile.Count}";
        _commit.Disabled = _finished || !_planning;
        _advance.Disabled = _finished || _planning;
        _commit.Visible = !_commit.Disabled;
        _advance.Visible = !_advance.Disabled;
    }
}
