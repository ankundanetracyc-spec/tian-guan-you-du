using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>共用的物品结算与满载领取入口。</summary>
public partial class VolumeItemClaimPanel : CanvasLayer
{
    private sealed class Reward(string path, ItemData item)
    {
        public string Path { get; } = path;
        public ItemData Item { get; } = item;
        public int Count { get; set; }
        public List<GDictionary> Sources { get; } = [];
        public string Caption => $"{Item.entity_name} ×{Count}";
    }

    private readonly List<Reward> _pending = [];
    private readonly List<Button> _rows = [];
    private Character _recipient = null!;
    private Action<GArray>? _claimed, _abandoned;
    private Func<GArray, string?>? _beforeCommit;
    private bool _previousPause, _active, _receivedAny, _result, _busy;
    private Control _float = null!;
    private Label _status = null!;
    private Tween? _scroll, _feedback;
    private float _center;
    private int _target;
    private const int RowHeight = 52, FloatWidth = 300, FloatHeight = 366;
    public event Action? Closed;

    public bool Present(Character recipient, GArray rewards, Action<GArray>? claimed,
        Action<GArray>? abandoned, Func<GArray, string?>? beforeCommit)
    {
        foreach (var value in rewards)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var source = value.AsGodotDictionary();
            string path = source.GetValueOrDefault("item_path", "").AsString();
            int count = source.GetValueOrDefault("count", 0).AsInt32();
            if (count <= 0 || string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path)
                || ResourceLoader.Load<ItemData>(path) is not { } item
                || !GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item)) continue;
            var reward = _pending.FirstOrDefault(entry => entry.Path == path);
            if (reward is null) { reward = new Reward(path, item); _pending.Add(reward); }
            count = Math.Min(count, int.MaxValue - reward.Count);
            if (count == 0) continue;
            var copy = source.Duplicate();
            copy["count"] = count;
            reward.Sources.Add(copy);
            reward.Count += count;
        }
        if (_pending.Count == 0) return false;

        _recipient = recipient;
        _claimed = claimed;
        _abandoned = abandoned;
        _beforeCommit = beforeCommit;
        Layer = 125;
        ProcessMode = ProcessModeEnum.Always;
        _previousPause = GetTree().Paused;
        GetTree().Paused = true;
        _active = true;

        var quantities = _pending.ToDictionary(reward => reward.Path, reward => reward.Count);
        string? error = null;
        if (recipient.can_add_inventory_batch(quantities) && commit(quantities, receipt(quantities), out error))
        {
            build_result();
            _pending.Clear();
        }
        else build_overflow(error);
        return true;
    }

    private GArray receipt(IReadOnlyDictionary<string, int> quantities)
    {
        var result = new GArray();
        foreach (var reward in _pending)
        {
            int remaining = quantities.GetValueOrDefault(reward.Path);
            foreach (var source in reward.Sources)
            {
                if (remaining <= 0) break;
                int taken = Math.Min(remaining, source["count"].AsInt32());
                if (taken == 0) continue;
                var copy = source.Duplicate();
                copy["count"] = taken;
                result.Add(copy);
                remaining -= taken;
            }
        }
        return result;
    }

    private bool commit(IReadOnlyDictionary<string, int> quantities, GArray entries, out string? error)
    {
        string? rejected = null;
        bool success = _recipient.try_exchange_inventory(new Dictionary<string, int>(), quantities,
            announceAcquisition: true, beforeCommit: () =>
            {
                rejected = _beforeCommit?.Invoke(entries);
                return string.IsNullOrEmpty(rejected);
            });
        error = rejected;
        if (success)
        {
            _receivedAny = true;
            notify(_claimed, entries);
        }
        return success;
    }

    private void build_result()
    {
        _result = true;
        var shade = new ColorRect { Color = new(0.14f, 0.10f, 0.07f, .44f) };
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer();
        shade.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var paper = new PanelContainer { CustomMinimumSize = new(460, 0) };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(24));
        center.AddChild(paper);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 16);
        paper.AddChild(content);
        var title = CraftingPaperStyle.Text("获得物品", 30);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(title);
        int columns = Math.Min(_pending.Count, 4);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new(202 * columns + 16, 112 * Math.Min(3, (_pending.Count + columns - 1) / columns))
        };
        content.AddChild(scroll);
        var tilesCenter = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(tilesCenter);
        var tiles = new GridContainer { Columns = columns };
        tilesCenter.AddChild(tiles);
        foreach (var reward in _pending)
        {
            var tile = StoragePanel.make_volume_tile(reward.Item.entity_name, reward.Count, reward.Item.icon_path, new(190, 108));
            foreach (var state in new[] { "normal", "hover", "pressed" })
                tile.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            tiles.AddChild(tile);
            HoverInfoManager.SetProvider(tile, () => StoragePanel.ItemHover(reward.Item, reward.Count, reward.Path, _recipient));
        }
        var confirm = new Button { Text = "确认", CustomMinimumSize = new(122, 44), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        PaperButtonFeedback.Apply(confirm);
        content.AddChild(confirm);
        confirm.Pressed += finish;
    }

    private void build_overflow(string? error)
    {
        var inventory = new StoragePanel { Name = "ClaimStoragePanel" };
        AddChild(inventory);
        inventory.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        inventory.bind_player(_recipient);
        inventory.show_panel();
        inventory.VolumeClosed += finish;
        var host = inventory.UseVolumeForReceiving();
        var title = CraftingPaperStyle.Text("待领取", 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        place(title, host, 1556, 297, FloatWidth, 40);
        _float = new Control { Name = "PendingRewards", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Stop };
        place(_float, host, 1556, 339, FloatWidth, FloatHeight);
        _float.GuiInput += scroll_input;
        _status = CraftingPaperStyle.Text(error ?? "", 21);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        place(_status, host, 1556, 718, FloatWidth, 62);
        _target = _pending.Count / 2;
        rebuild_rows();
    }

    private void rebuild_rows()
    {
        _scroll?.Kill();
        foreach (var row in _rows) { _float.RemoveChild(row); row.QueueFree(); }
        _rows.Clear();
        _target = Math.Clamp(_target, 0, Math.Max(0, _pending.Count - 1));
        foreach (var reward in _pending)
        {
            var row = new Button { Text = reward.Caption, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                FocusMode = Control.FocusModeEnum.None, Size = new(FloatWidth, RowHeight) };
            row.AddThemeFontOverride("font", YuxuLibraryArt.Font());
            row.AddThemeFontSizeOverride("font_size", 25);
            row.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
            row.AddThemeColorOverride("font_hover_color", new Color("362719"));
            row.AddThemeColorOverride("font_pressed_color", new Color("8a5134"));
            foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
                row.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            row.PivotOffset = new(FloatWidth / 2f, RowHeight / 2f);
            _float.AddChild(row);
            _rows.Add(row);
            row.GuiInput += scroll_input;
            row.Pressed += () => claim_reward(reward, row);
        }
        layout_rows(_target);
    }

    private void layout_rows(float center)
    {
        _center = center;
        for (int index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            float distance = index - center;
            row.Position = new(0, (FloatHeight - RowHeight) / 2f + RowHeight * distance);
            row.Scale = Vector2.One * Mathf.Max(.74f, 1 - Mathf.Abs(distance) * .09f);
            row.SelfModulate = new Color(1, 1, 1, Mathf.Max(.42f, 1 - Mathf.Abs(distance) * .17f));
        }
    }

    private void scroll_input(InputEvent input)
    {
        if (_busy || _rows.Count < 2 || input is not InputEventMouseButton { Pressed: true } mouse) return;
        int direction = mouse.ButtonIndex switch { MouseButton.WheelDown => 1, MouseButton.WheelUp => -1, _ => 0 };
        if (direction == 0) return;
        GetViewport().SetInputAsHandled();
        int previous = _target;
        _target = Math.Clamp(_target + direction, 0, _rows.Count - 1);
        _scroll?.Kill();
        _scroll = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        if (previous == _target)
            _scroll.TweenMethod(Callable.From<float>(layout_rows), _center, _target + direction * .22f, .08);
        _scroll.TweenMethod(Callable.From<float>(layout_rows), previous == _target ? _target + direction * .22f : _center, _target, .18);
    }

    private void claim_reward(Reward reward, Button row)
    {
        if (_busy || !_pending.Contains(reward)) return;
        int volume = Math.Max(1, InventoryCapacityRules.ItemVolume(reward.Item));
        int count = reward.Count;
        // A batch larger than the whole bag must remain claimable in several visits.
        if ((long)count * volume > Character.InventoryCapacity)
            count = Math.Min(count, _recipient.get_inventory_free_capacity() / volume);
        _busy = true;
        try
        {
            var quantities = new Dictionary<string, int> { [reward.Path] = count };
            string? error = null;
            if (count <= 0 || !commit(quantities, receipt(quantities), out error))
            {
                _status.Text = error ?? "随身行囊容量不足";
                _feedback?.Kill();
                _feedback = CreateTween();
                _feedback.TweenProperty(row, "position:x", 7f, .055);
                _feedback.TweenProperty(row, "position:x", -5f, .055);
                _feedback.TweenProperty(row, "position:x", 0f, .065);
                return;
            }
            reward.Count -= count;
            foreach (var source in reward.Sources)
            {
                int used = Math.Min(count, source["count"].AsInt32());
                source["count"] = source["count"].AsInt32() - used;
                count -= used;
                if (count == 0) break;
            }
            if (reward.Count == 0) _pending.Remove(reward);
            _status.Text = _pending.Count == 0 ? "已全部领取" : $"已领取 {reward.Item.entity_name}";
            row.MouseFilter = Control.MouseFilterEnum.Ignore;
            _scroll?.Kill();
            _feedback?.Kill();
            _feedback = CreateTween();
            _feedback.TweenProperty(row, "self_modulate:a", .15f, .13);
            _feedback.TweenCallback(Callable.From(() => { rebuild_rows(); _busy = false; }));
        }
        finally
        {
            if (row.MouseFilter != Control.MouseFilterEnum.Ignore) _busy = false;
        }
    }

    public override void _Input(InputEvent input)
    {
        if (!_active || !_result || input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        GetViewport().SetInputAsHandled();
        finish();
    }

    private void finish()
    {
        if (!_active) return;
        _active = false;
        var remainder = receipt(_pending.ToDictionary(reward => reward.Path, reward => reward.Count));
        if (!_receivedAny) notify(_claimed, new GArray());
        if (remainder.Count > 0) notify(_abandoned, remainder);
        HoverInfoManager.HideAll();
        GetTree().Paused = _previousPause;
        Closed?.Invoke();
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (_active && GetTree() is { } tree) tree.Paused = _previousPause;
    }

    private static void notify(Action<GArray>? callback, GArray entries)
    {
        try { callback?.Invoke(entries); }
        catch (Exception error) { GD.PushError($"物品结算回调失败：{error}"); }
    }

    private static void place(Control child, Control parent, float x, float y, float width, float height)
    {
        parent.AddChild(child);
        child.Position = new(x, y);
        child.Size = new(width, height);
    }
}


