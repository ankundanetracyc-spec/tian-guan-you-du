using Godot;

/// <summary>短暂显示行动结果；记录本身仍由各玩法系统负责持久化。</summary>
public partial class GameNotificationFeed : Control
{
    private const int MaxVisibleNotices = 3;
    private const double NoticeLifetimeSeconds = 5.2;
    private VBoxContainer _stack = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 120;

        _stack = new VBoxContainer
        {
            Name = "NoticeStack",
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -230,
            OffsetRight = 230,
            OffsetTop = 58,
            OffsetBottom = 400,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _stack.AddThemeConstantOverride("separation", 8);
        AddChild(_stack);
        Resized += fit_stack;
        fit_stack();
    }

    public void push(string title, string detail, string category = "见闻")
    {
        if (_stack is null || string.IsNullOrWhiteSpace(title)) return;
        while (_stack.GetChildCount() >= MaxVisibleNotices)
        {
            var oldest = _stack.GetChild(_stack.GetChildCount() - 1);
            _stack.RemoveChild(oldest);
            oldest.QueueFree();
        }

        var card = build_notice(title.Trim(), detail?.Trim() ?? "", category?.Trim() ?? "");
        card.Modulate = new Color(1, 1, 1, 0);
        card.Scale = new Vector2(.985f, .985f);
        _stack.AddChild(card);
        _stack.MoveChild(card, 0);

        var enter = CreateTween().SetParallel();
        enter.TweenProperty(card, "modulate:a", 1f, .18);
        enter.TweenProperty(card, "scale", Vector2.One, .2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        expire_notice_async(card);
    }

    private PanelContainer build_notice(string title, string detail, string category)
    {
        var card = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 0)
        };
        card.AddThemeStyleboxOverride("panel", WorldHudStyle.Paper());

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 13);
        margin.AddThemeConstantOverride("margin_right", 13);
        margin.AddThemeConstantOverride("margin_top", 7);
        margin.AddThemeConstantOverride("margin_bottom", 7);
        card.AddChild(margin);

        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 2);
        margin.AddChild(content);

        var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeConstantOverride("separation", 8);
        content.AddChild(heading);
        var seal = WorldHudStyle.Seal(string.IsNullOrWhiteSpace(category) ? "记" : category[..Math.Min(1, category.Length)]);
        seal.CustomMinimumSize = new Vector2(20, 20);
        heading.AddChild(seal);
        var titleLabel = WorldHudStyle.Text(title, 19);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(titleLabel);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryLabel = WorldHudStyle.Text(category, 14, true);
            categoryLabel.HorizontalAlignment = HorizontalAlignment.Right;
            heading.AddChild(categoryLabel);
        }

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var detailLabel = WorldHudStyle.Text(detail, 16, true);
            detailLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            detailLabel.MaxLinesVisible = 2;
            detailLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            content.AddChild(detailLabel);
        }
        return card;
    }

    private async void expire_notice_async(Control card)
    {
        await ToSignal(GetTree().CreateTimer(NoticeLifetimeSeconds), SceneTreeTimer.SignalName.Timeout);
        if (!GodotObject.IsInstanceValid(card) || card.IsQueuedForDeletion()) return;
        var fade = CreateTween();
        fade.TweenProperty(card, "modulate:a", 0f, .35);
        await ToSignal(fade, Tween.SignalName.Finished);
        if (GodotObject.IsInstanceValid(card)) card.QueueFree();
    }

    private void fit_stack()
    {
        if (_stack is null) return;
        var width = Math.Min(460f, Math.Max(240f, Size.X - 36f));
        _stack.OffsetLeft = -width / 2;
        _stack.OffsetRight = width / 2;
        _stack.OffsetBottom = 58 + Math.Min(390, Math.Max(210, Size.Y - 100));
    }
}
