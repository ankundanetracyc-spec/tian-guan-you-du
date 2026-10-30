using Godot;
using System;

/// <summary>Illustrated event reader. Only the controller commits costs and results.</summary>
public partial class ForestWolfEventView : Control
{
    public event Action<string>? ChoiceSelected;
    public event Action? CloseRequested;
    private Panel _paper = null!;
    private TextureRect _picture = null!;
    private Control _art = null!;
    private VBoxContainer _right = null!, _choices = null!;
    private ScrollContainer _choiceScroll = null!;
    private ScrollContainer _storyScroll = null!;
    private Button _close = null!;
    private Label _title = null!, _body = null!, _note = null!;
    private ForestWolfEvent.Page? _page;
    private bool _locked;
    private float _lastBodyMinimum;
    private Texture2D? _wolfTexture, _quietTexture;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        // Keep the paper independent of the minimum size of each event page.
        _paper = new Panel { Name = "EventPaper", MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
        _paper.AddThemeStyleboxOverride("panel", WorldHudStyle.Paper()); AddChild(_paper);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 16);
        _paper.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var paperStyle = _paper.GetThemeStylebox("panel");
        margin.OffsetLeft = paperStyle.GetContentMargin(Side.Left);
        margin.OffsetTop = paperStyle.GetContentMargin(Side.Top);
        margin.OffsetRight = -paperStyle.GetContentMargin(Side.Right);
        margin.OffsetBottom = -paperStyle.GetContentMargin(Side.Bottom);
        var columns = new HBoxContainer(); columns.AddThemeConstantOverride("separation", 28); margin.AddChild(columns);
        _art = new Control { Name = "SceneIllustration", ClipContents = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddChild(_art);
        _picture = new TextureRect
        {
            Name = "Illustration", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
        };
        _picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); _art.AddChild(_picture);
        _right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _right.AddThemeConstantOverride("separation", 12); columns.AddChild(_right);
        var header = new HBoxContainer(); _right.AddChild(header);
        _title = WorldHudStyle.Text("林中遭遇", 32); _title.Name = "EventTitle"; _title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(_title);
        _close = new Button { Name = "CloseEvent", Text = "×", CustomMinimumSize = new Vector2(36, 36), FocusMode = FocusModeEnum.None };
        WorldHudStyle.StyleSmallButton(_close, 28); _close.Pressed += () => { if (!_locked) CloseRequested?.Invoke(); }; AddChild(_close);
        _right.AddChild(WorldHudStyle.Rule());
        _storyScroll = new ScrollContainer
        {
            Name = "NarrativeScroll", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _right.AddChild(_storyScroll);
        _body = WorldHudStyle.Text("", 21); _body.Name = "Narrative";
        _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.VerticalAlignment = VerticalAlignment.Top; _body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _body.AddThemeConstantOverride("line_spacing", 6); _storyScroll.AddChild(_body);
        _note = WorldHudStyle.Text("", 15, true); _note.Name = "EventNotice";
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart; _right.AddChild(_note);
        _choiceScroll = new ScrollContainer
        {
            Name = "ChoiceScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _right.AddChild(_choiceScroll);
        _choices = new VBoxContainer { Name = "Choices", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _choices.AddThemeConstantOverride("separation", 3); _choiceScroll.AddChild(_choices);
        _wolfTexture = GD.Load<Texture2D>("res://textures/ui/wildlife_encounter_20261027/grass_eyes.png");
        _quietTexture = GD.Load<Texture2D>("res://textures/ui/wildlife_encounter_20261027/grass_quiet.png");
        Resized += Fit;
        Fit();
    }
    public void Display(ForestWolfEvent.Page page)
    {
        _page = page; _locked = false;
        _title.Text = page.Title; _body.Text = page.Body; SetNotice(page.Note);
        _picture.Texture = page.Quiet ? _quietTexture : _wolfTexture;
        foreach (var child in _choices.GetChildren()) { _choices.RemoveChild(child); child.QueueFree(); }
        foreach (var option in page.Choices)
        {
            var button = new Button
            {
                Name = "Choice_" + option.Id, Text = "›  " + option.Text + (option.Explanation.Length > 0 ? "（" + option.Explanation + "）" : ""),
                Alignment = HorizontalAlignment.Left, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseDefaultCursorShape = CursorShape.PointingHand, FocusMode = FocusModeEnum.All,
            };
            WorldHudStyle.StyleSmallButton(button, 20);
            button.AddThemeStyleboxOverride("normal", OptionStyle(false));
            button.AddThemeStyleboxOverride("hover", OptionStyle(true));
            button.AddThemeStyleboxOverride("focus", OptionStyle(true));
            button.AddThemeStyleboxOverride("pressed", OptionStyle(true));
            button.Pressed += () => { if (!_locked) ChoiceSelected?.Invoke(option.Id); };
            if (option.Hint.Length > 0)
            {
                button.MouseEntered += () => HoverInfoManager.SetInfo(button, new(option.Text, option.Hint), true);
                button.MouseExited += () => HoverInfoManager.HideFor(button);
            }
            _choices.AddChild(button);
        }
        Fit(); Show();
    }
    private static StyleBoxFlat OptionStyle(bool hovered) => new()
    {
        BgColor = hovered ? new Color(.38f, .47f, .39f, .14f) : Colors.Transparent,
        BorderColor = new Color(.25f, .32f, .26f, .3f), BorderWidthBottom = 1,
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 7, ContentMarginBottom = 7,
    };
    public void SetLocked(bool locked)
    {
        _locked = locked;
        foreach (var child in _choices.GetChildren()) if (child is Button button) button.Disabled = locked;
    }
    public void SetNotice(string text) { _note.Text = text; _note.Visible = text.Length > 0; }
    public override void _Process(double delta)
    {
        if (Visible && _body is not null && !Mathf.IsEqualApprox(_body.GetMinimumSize().Y, _lastBodyMinimum)) Fit();
    }
    private void Fit()
    {
        if (_paper is null || Size.X < 100) return;
        var bounds = new Vector2(Mathf.Min(1160, Size.X * .68f), Mathf.Min(640, Size.Y * .68f));
        _paper.Size = bounds; _paper.Position = (Size - bounds) * .5f;
        _art.CustomMinimumSize = new Vector2(bounds.X * .43f, 0);
        _title.AddThemeFontSizeOverride("font_size", Size.X < 1500 ? 28 : 32);
        _body.AddThemeFontSizeOverride("font_size", Size.X < 1500 ? 18 : 21);
        var textWidth = Mathf.Max(150, bounds.X * .57f - 88);
        _lastBodyMinimum = _body.GetMinimumSize().Y;
        var textHeight = Mathf.Max(_lastBodyMinimum + 8, _body.GetThemeFont("font").GetMultilineStringSize(_body.Text, HorizontalAlignment.Left,
            textWidth, Size.X < 1500 ? 18 : 21).Y + 30);
        _close.Position = _paper.Position + new Vector2(bounds.X - 48, 12); _close.Size = new Vector2(36, 36);
        var count = _page?.Choices.Length ?? 4;
        var paperStyle = _paper.GetThemeStylebox("panel");
        var available = Mathf.Max(0, bounds.Y - 32 - paperStyle.GetContentMargin(Side.Top)
            - paperStyle.GetContentMargin(Side.Bottom) - _title.GetCombinedMinimumSize().Y - 1
            - (_note.Visible ? _note.GetCombinedMinimumSize().Y : 0)
            - _right.GetThemeConstant("separation") * (_note.Visible ? 4 : 3));
        var storyHeight = Mathf.Min(textHeight, bounds.Y * .42f);
        var choiceHeight = Mathf.Min(count * 46, bounds.Y * .40f);
        var scale = Mathf.Min(1, available / Mathf.Max(1, storyHeight + choiceHeight));
        _storyScroll.CustomMinimumSize = new Vector2(0, storyHeight * scale);
        _choiceScroll.CustomMinimumSize = new Vector2(0, choiceHeight * scale);
        foreach (var child in _choices.GetChildren()) if (child is Button button)
            button.AddThemeFontSizeOverride("font_size", Size.X < 1500 ? 18 : 20);
    }
}
