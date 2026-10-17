using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Reusable banquet UI. It presents a view through the banquet-room window and reports choices; the caller owns
/// invitations, resource costs, calendar time, timeout selection, relationship records, and saves.
/// </summary>
public partial class BanquetPanel : Control
{
    private const string BackgroundPath = "res://textures/ui/banquet/banquet_outside_window_v2.png";

    public event Action<BanquetSetupRequest>? SetupRequested;
    public event Action<BanquetResponseSelection>? ResponseChosen;
    public event Action<string, string>? AmbientBeatFinished;
    public event Action? ActivityExitRequested;

    private enum PanelMode { Hidden, Setup, Activity }

    private PanelMode _mode;
    private BanquetRosterEntry? _host;
    private bool _hostIsPlayer;
    private IReadOnlyList<BanquetVenueOption> _venues = [];
    private IReadOnlyList<BanquetTimeOption> _times = [];
    private IReadOnlyList<BanquetRosterEntry> _roster = [];
    private readonly HashSet<string> _selectedGuestIds = [];
    private readonly List<string> _guestSelectionOrder = [];
    private readonly Dictionary<string, CheckBox> _guestChecks = [];
    private BanquetActivityPresentation? _activity;
    private BanquetBeatPresentation? _activeBeat;
    private double _responseRemaining;
    private bool _responseSent;

    private VBoxContainer? _mainColumn;
    private Label? _headerTitle;
    private Label? _headerSubtitle;
    private Button? _exitButton;
    private Control? _setupScreen;
    private Control? _activityScreen;
    private OptionButton? _venueSelector;
    private Label? _venueDetails;
    private OptionButton? _timeSelector;
    private ScrollContainer? _guestScroll;
    private VBoxContainer? _guestList;
    private Label? _guestCountLabel;
    private Button? _startButton;
    private Control? _bubbleLayer;
    private VBoxContainer? _responseTray;
    private Label? _interludeLabel;
    private GridContainer? _choicesBox;
    private ProgressBar? _responseProgress;
    private Label? _responseTimerLabel;
    private readonly List<Control> _recentBubbles = [];
    private readonly Dictionary<Control, (string SpeakerId, string BeatId)> _bubblePlacementKeys = [];
    private double _ambientRemaining;
    private bool _ambientAdvanceSent;
    private int _bubbleSequence;
    private float _bubbleWidth;

    public bool IsOpen => _mode != PanelMode.Hidden && Visible;
    public bool IsActivityOpen => _mode == PanelMode.Activity && Visible;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 260;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        build_shell();
        Hide();
        _mode = PanelMode.Hidden;
    }

    /// <summary>Opens the player-host setup page. Time options are absolute in-game hours.</summary>
    public void ShowSetup(
        BanquetRosterEntry host,
        IReadOnlyList<BanquetRosterEntry> roster,
        IReadOnlyList<BanquetVenueOption> venues,
        IReadOnlyList<BanquetTimeOption> timeOptions,
        bool hostIsPlayer = true)
    {
        _host = host;
        _hostIsPlayer = hostIsPlayer;
        _roster = roster ?? [];
        _venues = venues ?? [];
        _times = timeOptions ?? [];
        _selectedGuestIds.Clear();
        _guestSelectionOrder.Clear();
        _mode = PanelMode.Setup;
        ensure_built();
        _activityScreen!.Hide();
        _setupScreen!.Show();
        _headerTitle!.Text = "同门雅集";
        _headerSubtitle!.Text = $"主持者　{host.Name}　·　相聚、交谈与结识";
        _exitButton!.Hide();
        rebuild_setup_options();
        Show();
        MoveToFront();
    }

    /// <summary>Opens the saved or newly created activity view; this method does not settle game state.</summary>
    public void StartActivity(BanquetActivityPresentation activity)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _mode = PanelMode.Activity;
        ensure_built();
        _setupScreen!.Hide();
        _activityScreen!.Show();
        _headerTitle!.Text = activity.Title;
        var venueAndHost = string.IsNullOrWhiteSpace(activity.VenueName) ? $"主持：{activity.HostName}" : $"{activity.VenueName}　·　主持：{activity.HostName}";
        _headerSubtitle!.Text = $"{venueAndHost}　·　共 {activity.Participants.Count} 位同门";
        _exitButton!.Show();
        _exitButton.Disabled = false;
        _responseSent = true;
        _ambientAdvanceSent = true;
        _activeBeat = null;
        clear_dialogue_bubbles();
        show_interlude_content("宾客已到，席间寒暄渐起。");
        Show();
        MoveToFront();
    }

    /// <summary>Displays the next conversation moment. The caller supplies actual listeners and saved content.</summary>
    public void ShowBeat(BanquetBeatPresentation beat)
    {
        if (_mode != PanelMode.Activity || _activity is null) return;
        if (_responseSent && _activeBeat?.BeatId == beat.BeatId) return;
        _activeBeat = beat;
        _responseSent = false;
        _ambientAdvanceSent = false;
        _interludeLabel?.Hide();
        append_dialogue_bubble(beat);
        _choicesBox!.GetChildren().ToList().ForEach(child => child.QueueFree());

        if (beat.Choices.Count == 0)
        {
            _responseSent = false;
            _ambientRemaining = Math.Max(1.2, beat.DisplaySeconds);
            _responseTray!.Hide();
            _responseProgress!.Hide();
            _responseTimerLabel!.Hide();
        }
        else
        {
            _responseRemaining = Math.Max(0.1, beat.ResponseSeconds);
            _responseTray!.Show();
            _choicesBox.Show();
            foreach (var choice in beat.Choices)
            {
                var choiceId = choice.Id;
                var button = make_choice_button(choice);
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                button.Pressed += () => submit_response(choiceId, false);
                _choicesBox.AddChild(button);
            }
            _responseProgress!.MaxValue = _responseRemaining;
            _responseProgress.Value = _responseRemaining;
            _responseProgress.Show();
            _responseTimerLabel!.Text = $"{_responseRemaining:0} 秒回应";
            _responseTimerLabel.Show();
        }
    }

    /// <summary>Shows a quiet interval between conversation moments, with no response timer.</summary>
    public void ShowInterlude(string text)
    {
        if (_mode != PanelMode.Activity) return;
        _activeBeat = null;
        _responseSent = true;
        _ambientAdvanceSent = true;
        show_interlude_content(string.IsNullOrWhiteSpace(text) ? "茶香浮动，众人闲谈片刻。" : text);
    }

    /// <summary>Shows an ending/early-departure message while leaving final settlement to the caller.</summary>
    public void ShowConclusion(string text)
    {
        if (_mode != PanelMode.Activity) return;
        _activeBeat = null;
        _responseSent = true;
        _ambientAdvanceSent = true;
        _exitButton!.Hide();
        _responseTray?.Hide();
        _responseProgress?.Hide();
        _responseTimerLabel?.Hide();
        _interludeLabel?.Hide();
        append_notice_bubble("席间将散", string.IsNullOrWhiteSpace(text) ? "此番相聚已结束。" : text);
    }

    /// <summary>Hides the reusable panel without changing the active event.</summary>
    public void Close()
    {
        _mode = PanelMode.Hidden;
        _activity = null;
        _activeBeat = null;
        _ambientAdvanceSent = true;
        Hide();
    }

    public override void _Process(double delta)
    {
        if (!IsActivityOpen || _activeBeat is null || _responseSent) return;
        if (_activeBeat.Choices.Count == 0)
        {
            _ambientRemaining = Math.Max(0, _ambientRemaining - delta);
            if (_ambientRemaining <= 0 && !_ambientAdvanceSent)
            {
                _ambientAdvanceSent = true;
                _responseSent = true;
                AmbientBeatFinished?.Invoke(_activeBeat.EventId, _activeBeat.BeatId);
            }
            return;
        }
        _responseRemaining = Math.Max(0, _responseRemaining - delta);
        if (_responseProgress is not null) _responseProgress.Value = _responseRemaining;
        if (_responseTimerLabel is not null) _responseTimerLabel.Text = $"{_responseRemaining:0} 秒";
        if (_responseRemaining <= 0) submit_response("", true);
    }

    private void build_shell()
    {
        var background = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(BackgroundPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var veil = new ColorRect { Color = new Color(0.07f, 0.075f, 0.065f, 0.20f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 34);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 34);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        _mainColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _mainColumn.AddThemeConstantOverride("separation", 14);
        margin.AddChild(_mainColumn);

        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 62), MouseFilter = MouseFilterEnum.Ignore };
        _mainColumn.AddChild(header);
        var headingColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        header.AddChild(headingColumn);
        _headerTitle = make_label("同门雅集", 31, PaperText);
        headingColumn.AddChild(_headerTitle);
        _headerSubtitle = make_label("", 16, PaperMuted);
        headingColumn.AddChild(_headerSubtitle);
        _exitButton = make_button("提前离席", 44);
        _exitButton.CustomMinimumSize = new Vector2(148, 44);
        _exitButton.Pressed += request_activity_exit;
        header.AddChild(_exitButton);

        var body = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _mainColumn.AddChild(body);

        var setupCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        setupCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        body.AddChild(setupCenter);
        var viewportSize = GetViewportRect().Size;
        var setupCard = new PanelContainer
        {
            CustomMinimumSize = new Vector2(Mathf.Min(780, Mathf.Max(320, viewportSize.X - 90)), Mathf.Min(670, Mathf.Max(420, viewportSize.Y - 150))),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Stop,
        };
        setupCard.AddThemeStyleboxOverride("panel", panel_style(new Color(0.93f, 0.89f, 0.79f, 0.97f), new Color("9b8558"), 9, 22));
        setupCenter.AddChild(setupCard);
        _setupScreen = setupCard;
        build_setup_contents(setupCard);

        _activityScreen = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _activityScreen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _activityScreen.AddThemeConstantOverride("separation", 10);
        body.AddChild(_activityScreen);
        build_activity_contents((VBoxContainer)_activityScreen);
    }

    private void ensure_built()
    {
        if (_mainColumn is null) build_shell();
    }

    private void build_setup_contents(PanelContainer card)
    {
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 10);
        card.AddChild(content);
        content.AddChild(make_label("安排一场相聚", 27, Ink));
        content.AddChild(make_label("选择席面、宾客与时辰。费用与时间会在宴席真正开始时结算。", 15, InkSoft));
        content.AddChild(make_rule());

        content.AddChild(make_label("宴席所在", 18, Ink));
        _venueSelector = new OptionButton { CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(_venueSelector);
        _venueSelector.ItemSelected += _ => update_venue_details();
        _venueDetails = make_label("", 15, InkSoft);
        _venueDetails.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_venueDetails);

        var guestHeader = new HBoxContainer();
        guestHeader.AddChild(make_label("邀请同门", 18, Ink));
        _guestCountLabel = make_label("", 15, InkSoft);
        _guestCountLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _guestCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
        guestHeader.AddChild(_guestCountLabel);
        content.AddChild(guestHeader);
        _guestScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 210), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(_guestScroll);
        _guestList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _guestList.AddThemeConstantOverride("separation", 4);
        _guestScroll.AddChild(_guestList);

        var timeRow = new HBoxContainer();
        timeRow.AddChild(make_label("举办时辰", 18, Ink));
        _timeSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 42) };
        timeRow.AddChild(_timeSelector);
        content.AddChild(timeRow);

        var bottom = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        bottom.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var close = make_button("暂不设宴", 44);
        close.CustomMinimumSize = new Vector2(140, 44);
        close.Pressed += Close;
        bottom.AddChild(close);
        _startButton = make_button("发出邀约", 44);
        _startButton.CustomMinimumSize = new Vector2(156, 44);
        _startButton.Pressed += submit_setup;
        bottom.AddChild(_startButton);
        content.AddChild(bottom);
    }

    private void build_activity_contents(VBoxContainer activity)
    {
        var viewportSize = GetViewportRect().Size;
        var contentWidth = Mathf.Min(820, Mathf.Max(260, viewportSize.X - 68));
        _bubbleWidth = Mathf.Min(590, Mathf.Max(245, contentWidth * 0.74f));
        var stage = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        stage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        activity.AddChild(stage);

        _bubbleLayer = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _bubbleLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _bubbleLayer.Resized += reposition_recent_bubbles;
        stage.AddChild(_bubbleLayer);

        // Keep only the direct-response controls near the lower edge. Conversation bubbles
        // use the rest of the window as a loose stage instead of a centered dialogue column.
        var lowerZone = new VBoxContainer
        {
            AnchorLeft = 0.04f,
            AnchorTop = 1,
            AnchorRight = 0.96f,
            AnchorBottom = 1,
            OffsetTop = -278,
            OffsetBottom = -4,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        lowerZone.AddThemeConstantOverride("separation", 7);
        stage.AddChild(lowerZone);

        _interludeLabel = make_label("", 15, PaperMuted);
        _interludeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _interludeLabel.CustomMinimumSize = new Vector2(contentWidth, 24);
        _interludeLabel.Hide();
        var interludeCenter = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        interludeCenter.AddChild(_interludeLabel);
        lowerZone.AddChild(interludeCenter);

        _responseTray = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(contentWidth, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _responseTray.AddThemeConstantOverride("separation", 8);
        _responseTray.Hide();
        var responseCaption = make_label("你会如何回应？", 15, PaperMuted);
        responseCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _responseTray.AddChild(responseCaption);

        _choicesBox = new GridContainer { Columns = contentWidth < 600 ? 1 : 2, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(Mathf.Min(760, contentWidth), 0), MouseFilter = MouseFilterEnum.Ignore };
        _choicesBox.AddThemeConstantOverride("h_separation", 8);
        _choicesBox.AddThemeConstantOverride("v_separation", 8);
        _responseTray.AddChild(_choicesBox);

        var timerRow = new HBoxContainer();
        _responseProgress = new ProgressBar { ShowPercentage = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 7) };
        _responseProgress.AddThemeStyleboxOverride("background", bar_style(new Color(0.18f, 0.16f, 0.13f, 0.52f)));
        _responseProgress.AddThemeStyleboxOverride("fill", bar_style(new Color("b59a62")));
        timerRow.AddChild(_responseProgress);
        _responseTimerLabel = make_label("12 秒回应", 13, PaperMuted);
        _responseTimerLabel.CustomMinimumSize = new Vector2(84, 0);
        _responseTimerLabel.HorizontalAlignment = HorizontalAlignment.Right;
        timerRow.AddChild(_responseTimerLabel);
        _responseTray.AddChild(timerRow);
        var responseCenter = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        responseCenter.AddChild(_responseTray);
        lowerZone.AddChild(responseCenter);
    }

    private void rebuild_setup_options()
    {
        if (_venueSelector is null || _guestList is null || _timeSelector is null) return;
        _venueSelector.Clear();
        for (var i = 0; i < _venues.Count; i++) _venueSelector.AddItem($"{_venues[i].Name}　·　{_venues[i].CostText}　·　最多 {_venues[i].CapacityIncludingHost} 人", i);
        if (_venues.Count > 0) _venueSelector.Select(0);

        foreach (var child in _guestList.GetChildren()) child.QueueFree();
        _guestChecks.Clear();
        foreach (var guest in _roster.Where(entry => entry.Id != _host?.Id))
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 46), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var check = new CheckBox
            {
                Text = string.IsNullOrWhiteSpace(guest.Subtitle) ? guest.Name : $"{guest.Name}　·　{guest.Subtitle}",
                CustomMinimumSize = new Vector2(0, 38),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None,
            };
            check.AddThemeFontSizeOverride("font_size", 17);
            check.AddThemeColorOverride("font_color", Ink);
            var guestId = guest.Id;
            check.Toggled += value => on_guest_toggled(guestId, value);
            _guestChecks[guestId] = check;
            row.AddChild(check);
            _guestList.AddChild(row);
        }

        _timeSelector.Clear();
        foreach (var time in _times) _timeSelector.AddItem(time.Label);
        if (_times.Count > 0) _timeSelector.Select(0);
        else _timeSelector.AddItem("暂无可选时辰");
        update_venue_details();
        update_guest_capacity();
    }

    private void on_guest_toggled(string guestId, bool selected)
    {
        if (selected)
        {
            var maximumGuests = Math.Max(0, selected_venue()?.CapacityIncludingHost - 1 ?? 0);
            if (_selectedGuestIds.Count >= maximumGuests)
            {
                if (_guestChecks.TryGetValue(guestId, out var check)) check.SetPressedNoSignal(false);
                return;
            }
            _selectedGuestIds.Add(guestId);
            _guestSelectionOrder.Add(guestId);
        }
        else
        {
            _selectedGuestIds.Remove(guestId);
            _guestSelectionOrder.Remove(guestId);
        }
        update_guest_capacity();
    }

    private void update_venue_details()
    {
        if (_venueDetails is null) return;
        var venue = selected_venue();
        if (venue is null)
        {
            _venueDetails.Text = "暂无可用席面。";
            _venueDetails.Modulate = PaperMuted;
        }
        else
        {
            _venueDetails.Text = string.IsNullOrWhiteSpace(venue.Description) ? $"费用：{venue.CostText}　·　席位包含主持者" : $"{venue.Description}　费用：{venue.CostText}　·　席位包含主持者";
            _venueDetails.Modulate = InkSoft;
        }
        update_guest_capacity();
    }

    private void update_guest_capacity()
    {
        var venue = selected_venue();
        var maximumGuests = Math.Max(0, (venue?.CapacityIncludingHost ?? 1) - 1);
        while (_selectedGuestIds.Count > maximumGuests && _guestSelectionOrder.Count > 0)
        {
            var removedId = _guestSelectionOrder[^1];
            _guestSelectionOrder.RemoveAt(_guestSelectionOrder.Count - 1);
            _selectedGuestIds.Remove(removedId);
            if (_guestChecks.TryGetValue(removedId, out var removedCheck)) removedCheck.SetPressedNoSignal(false);
        }
        if (_guestCountLabel is not null) _guestCountLabel.Text = $"已邀 {_selectedGuestIds.Count} / {maximumGuests} 位（主持者计入席位）";
        foreach (var pair in _guestChecks)
            pair.Value.Disabled = !_selectedGuestIds.Contains(pair.Key) && _selectedGuestIds.Count >= maximumGuests;
        if (_startButton is not null)
            _startButton.Disabled = venue is null || _selectedGuestIds.Count == 0 || _times.Count == 0 || _timeSelector?.Selected < 0;
    }

    private BanquetVenueOption? selected_venue()
    {
        if (_venueSelector is null || _venueSelector.Selected < 0 || _venueSelector.Selected >= _venues.Count) return null;
        return _venues[_venueSelector.Selected];
    }

    private void submit_setup()
    {
        var venue = selected_venue();
        if (venue is null || _host is null || _selectedGuestIds.Count == 0 || _times.Count == 0) return;
        var timeIndex = _timeSelector?.Selected ?? -1;
        var startsAt = timeIndex >= 0 && timeIndex < _times.Count ? _times[timeIndex].StartsAtHour : _times[0].StartsAtHour;
        _startButton!.Disabled = true;
        SetupRequested?.Invoke(new BanquetSetupRequest
        {
            HostId = _host.Id,
            VenueId = venue.Id,
            GuestIds = Array.AsReadOnly(_selectedGuestIds.ToArray()),
            StartsAtHour = startsAt,
        });
    }

    private void show_interlude_content(string text)
    {
        if (_interludeLabel is null || _responseTray is null || _responseProgress is null || _responseTimerLabel is null) return;
        _interludeLabel.Text = text;
        _interludeLabel.Show();
        _responseTray.Hide();
        _responseProgress.Hide();
        _responseTimerLabel.Hide();
    }

    private void clear_dialogue_bubbles()
    {
        foreach (var bubble in _recentBubbles)
            if (GodotObject.IsInstanceValid(bubble)) bubble.QueueFree();
        _recentBubbles.Clear();
        _bubblePlacementKeys.Clear();
        if (_bubbleLayer is not null)
            foreach (var child in _bubbleLayer.GetChildren()) child.QueueFree();
        _bubbleSequence = 0;
    }

    private void append_dialogue_bubble(BanquetBeatPresentation beat)
    {
        if (_bubbleLayer is null) return;
        var isPrompt = beat.Choices.Count > 0;
        var isNotice = beat.IsNotice;
        var hasHeader = isPrompt || isNotice;
        var bubbleWidthLimit = Mathf.Min(isPrompt ? 430 : 350, _bubbleWidth);
        var bubbleWidth = Mathf.Clamp(beat.Text.Length * 13 + 46, Mathf.Min(190, bubbleWidthLimit), bubbleWidthLimit);
        var textWidth = Mathf.Max(120, bubbleWidth - 36);
        var charactersPerLine = Mathf.Max(6, textWidth / 18f);
        var estimatedLines = Mathf.Max(1, Mathf.CeilToInt(beat.Text.Length / charactersPerLine));
        var estimatedHeight = (hasHeader ? 70 : 48) + estimatedLines * 27;
        var bubble = new Control
        {
            CustomMinimumSize = new Vector2(bubbleWidth, estimatedHeight),
            Size = new Vector2(bubbleWidth, estimatedHeight),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bubble.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        var background = isPrompt || isNotice ? new Color(0.95f, 0.91f, 0.80f, 0.96f) : new Color(0.90f, 0.86f, 0.75f, 0.86f);
        var border = isPrompt ? new Color("b4985d") : new Color("94866a");
        var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeStyleboxOverride("panel", panel_style(background, border, 13, 0));
        bubble.AddChild(frame);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 17);
        margin.AddThemeConstantOverride("margin_right", 17);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        frame.AddChild(margin);

        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 4);
        margin.AddChild(content);
        if (hasHeader)
        {
            var headerText = isNotice ? beat.SpeakerName : $"{beat.SpeakerName}　·　问你";
            var header = make_label(headerText, 13, new Color("806f4d"));
            header.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(header);
        }
        var text = make_label(beat.Text, 18, Ink);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(text);

        if (!isNotice)
        {
            var tailOnRight = !isPrompt && _bubbleSequence % 2 == 1;
            var tailPoints = tailOnRight
                ? new[] { new Vector2(0, 0), new Vector2(34, 0), new Vector2(25, 14) }
                : new[] { new Vector2(0, 0), new Vector2(34, 0), new Vector2(9, 14) };
            var tail = new Polygon2D { Polygon = tailPoints, Color = background, ZIndex = 1, Name = "SpeechTail" };
            var outline = new Line2D
            {
                Points = tailOnRight
                    ? new[] { new Vector2(0, 0), new Vector2(25, 14), new Vector2(34, 0) }
                    : new[] { new Vector2(0, 0), new Vector2(9, 14), new Vector2(34, 0) },
                Width = 1,
                DefaultColor = border,
                ZIndex = 2,
                Name = "SpeechTailOutline",
            };
            void position_tail()
            {
                var tailX = tailOnRight ? bubble.Size.X - 52 : (isPrompt ? 58 : 26);
                var tailPosition = new Vector2(Mathf.Max(12, tailX), bubble.Size.Y - 2);
                tail.Position = tailPosition;
                outline.Position = tailPosition;
            }
            bubble.AddChild(tail);
            bubble.AddChild(outline);
            bubble.Resized += position_tail;
            position_tail();
        }

        _bubbleLayer.AddChild(bubble);
        bubble.Size = new Vector2(bubbleWidth, estimatedHeight);
        _recentBubbles.Add(bubble);
        _bubblePlacementKeys[bubble] = (beat.SpeakerId, beat.BeatId);
        while (_recentBubbles.Count > 3)
        {
            var oldest = _recentBubbles[0];
            _recentBubbles.RemoveAt(0);
            _bubblePlacementKeys.Remove(oldest);
            oldest.GetParent()?.RemoveChild(oldest);
            oldest.QueueFree();
        }
        place_dialogue_bubble(bubble, beat.SpeakerId, beat.BeatId);
        refresh_bubble_opacity();
        var targetOpacity = bubble.Modulate.A;
        bubble.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(bubble, "modulate", new Color(1, 1, 1, targetOpacity), 0.20);
        _bubbleSequence++;
    }

    private void place_dialogue_bubble(Control bubble, string speakerId, string beatId)
    {
        if (_bubbleLayer is null) return;
        var layerSize = _bubbleLayer.Size;
        if (layerSize.X < 2 || layerSize.Y < 2)
            layerSize = GetViewportRect().Size - new Vector2(68, 118);

        var horizontalInset = Mathf.Clamp(layerSize.X * 0.10f, 18f, 110f);
        var leftBound = horizontalInset;
        var rightBound = Mathf.Max(leftBound, layerSize.X - horizontalInset);
        var minX = leftBound;
        var maxX = Mathf.Max(minX, rightBound - bubble.Size.X);
        var lowerReserved = Mathf.Min(286f, layerSize.Y * 0.28f);
        var usableHeight = Mathf.Max(1f, layerSize.Y - lowerReserved);
        var topInset = Mathf.Clamp(usableHeight * 0.10f, 22f, 76f);
        var bottomInset = Mathf.Clamp(usableHeight * 0.08f, 18f, 64f);
        var minY = topInset;
        var maxY = Mathf.Max(minY, usableHeight - bottomInset - bubble.Size.Y);
        var xPositions = new[] { minX, Mathf.Lerp(minX, maxX, 0.5f), maxX };
        var yPositions = new[]
        {
            minY,
            Mathf.Lerp(minY, maxY, 0.5f),
            maxY,
        };
        var candidates = new List<Vector2>(xPositions.Length * yPositions.Length);
        foreach (var y in yPositions)
            foreach (var x in xPositions)
                candidates.Add(new Vector2(x, y));

        var seed = stable_layout_hash(speakerId) + stable_layout_hash(beatId) + (uint)(_bubbleSequence * 5);
        var first = (int)(seed % (uint)candidates.Count);
        var bestPosition = candidates[first];
        var lowestOverlap = float.MaxValue;
        for (var offset = 0; offset < candidates.Count; offset++)
        {
            var candidate = candidates[(first + offset) % candidates.Count];
            var padded = new Rect2(candidate - new Vector2(20, 16), bubble.Size + new Vector2(40, 32));
            var overlapScore = 0f;
            foreach (var recent in _recentBubbles)
            {
                if (recent == bubble || !GodotObject.IsInstanceValid(recent) || recent.GetParent() != _bubbleLayer) continue;
                var recentRect = new Rect2(recent.Position - new Vector2(20, 16), recent.Size + new Vector2(40, 32));
                if (!padded.Intersects(recentRect)) continue;
                var overlap = padded.Intersection(recentRect).Size;
                overlapScore += overlap.X * overlap.Y + 1;
            }
            if (overlapScore < lowestOverlap)
            {
                lowestOverlap = overlapScore;
                bestPosition = candidate;
            }
            if (overlapScore <= 0) break;
        }
        bubble.Position = bestPosition;
    }

    private static uint stable_layout_hash(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value ?? "")
            hash = (hash ^ character) * 16777619u;
        return hash;
    }

    private void reposition_recent_bubbles()
    {
        if (_bubbleLayer is null || _bubbleLayer.Size.X < 2 || _bubbleLayer.Size.Y < 2) return;
        foreach (var bubble in _recentBubbles)
            if (GodotObject.IsInstanceValid(bubble) && bubble.GetParent() == _bubbleLayer)
                bubble.Position = new Vector2(-10000, -10000);

        foreach (var bubble in _recentBubbles)
            if (GodotObject.IsInstanceValid(bubble) && _bubblePlacementKeys.TryGetValue(bubble, out var key))
                place_dialogue_bubble(bubble, key.SpeakerId, key.BeatId);
    }

    private void append_notice_bubble(string title, string text)
    {
        append_dialogue_bubble(new BanquetBeatPresentation
        {
            EventId = _activity?.EventId ?? "",
            BeatId = $"notice:{_activity?.EventId}",
            SpeakerName = title,
            Text = text,
            IsNotice = true,
        });
    }

    private void refresh_bubble_opacity()
    {
        for (var index = 0; index < _recentBubbles.Count; index++)
        {
            var age = _recentBubbles.Count - index - 1;
            var opacity = age switch { 0 => 1.0f, 1 => 0.68f, _ => 0.44f };
            _recentBubbles[index].Modulate = new Color(1, 1, 1, opacity);
        }
    }

    private static Button make_choice_button(BanquetChoicePresentation choice)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(0, 74),
            FocusMode = FocusModeEnum.None,
            Theme = ResourceLoader.Load<Theme>("res://resources/font/default_theme.tres"),
            Disabled = !choice.IsAvailable,
            MouseFilter = MouseFilterEnum.Stop,
        };
        button.AddThemeStyleboxOverride("normal", button_style(new Color(0.94f, 0.89f, 0.77f, 0.96f), new Color("9c875d")));
        button.AddThemeStyleboxOverride("hover", button_style(new Color(0.99f, 0.95f, 0.85f, 0.98f), new Color("b89a5e")));
        button.AddThemeStyleboxOverride("pressed", button_style(new Color(0.88f, 0.82f, 0.68f, 0.98f), new Color("9d8250")));
        button.AddThemeStyleboxOverride("disabled", button_style(new Color(0.81f, 0.78f, 0.71f, 0.92f), new Color("928a79")));

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        button.AddChild(margin);
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 2);
        margin.AddChild(content);
        var answer = make_label(string.IsNullOrWhiteSpace(choice.Tone) ? choice.Text : $"{choice.Tone}　{choice.Text}", 15, choice.IsAvailable ? Ink : new Color("716b5d"));
        answer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(answer);
        if (!string.IsNullOrWhiteSpace(choice.RequirementText))
        {
            var requirement = make_label(choice.RequirementText, 12, choice.IsAvailable ? InkSoft : new Color("766b58"));
            requirement.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(requirement);
        }
        return button;
    }

    private void submit_response(string choiceId, bool timedOut)
    {
        if (_responseSent || _activeBeat is null || _activity is null) return;
        _responseSent = true;
        foreach (var child in _choicesBox!.GetChildren())
            if (child is Button button) button.Disabled = true;
        ResponseChosen?.Invoke(new BanquetResponseSelection
        {
            EventId = _activity.EventId,
            BeatId = _activeBeat.BeatId,
            ChoiceId = timedOut ? "" : choiceId,
            TimedOut = timedOut,
        });
    }

    private void request_activity_exit()
    {
        if (_mode != PanelMode.Activity || _exitButton is null || _exitButton.Disabled) return;
        _exitButton.Disabled = true;
        ActivityExitRequested?.Invoke();
    }

    private static Label make_label(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button make_button(string text, int height)
    {
        var result = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, height),
            FocusMode = FocusModeEnum.None,
            Theme = ResourceLoader.Load<Theme>("res://resources/font/default_theme.tres"),
        };
        result.AddThemeFontSizeOverride("font_size", 16);
        result.AddThemeColorOverride("font_color", PaperText);
        result.AddThemeColorOverride("font_hover_color", new Color("f4e4b9"));
        result.AddThemeStyleboxOverride("normal", button_style(new Color(0.15f, 0.13f, 0.095f, 0.92f), new Color("756340")));
        result.AddThemeStyleboxOverride("hover", button_style(new Color(0.26f, 0.21f, 0.13f, 0.96f), new Color("c5a15b")));
        result.AddThemeStyleboxOverride("pressed", button_style(new Color(0.10f, 0.09f, 0.07f, 0.96f), new Color("a4864f")));
        return result;
    }

    private static HSeparator make_rule()
    {
        var line = new HSeparator();
        line.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color("a99870"), Thickness = 1 });
        return line;
    }

    private static StyleBoxFlat button_style(Color background, Color border)
    {
        return panel_style(background, border, 5, 6);
    }

    private static StyleBoxFlat bar_style(Color color)
    {
        return new StyleBoxFlat { BgColor = color };
    }

    private static StyleBoxFlat panel_style(Color background, Color border, int radius, float padding)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = padding,
            ContentMarginRight = padding,
            ContentMarginTop = padding,
            ContentMarginBottom = padding,
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }

    private static readonly Color PaperText = new("f0e7cf");
    private static readonly Color PaperMuted = new("cabda0");
    private static readonly Color Ink = new("332d22");
    private static readonly Color InkSoft = new("625841");
    private static readonly Color Gold = new("d9bb78");
}
