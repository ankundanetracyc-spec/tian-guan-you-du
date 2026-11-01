using Godot;

/// <summary>行囊物品格的展示与延迟信息请求；支持普通堆叠物品和唯一法宝实例。</summary>
public partial class StorageItemSlot : Control
{
    [Signal] public delegate void info_requestedEventHandler(StorageItemSlot slot, bool immediate);
    [Signal] public delegate void info_clearedEventHandler(StorageItemSlot slot);
    [Signal] public delegate void clickedEventHandler(StorageItemSlot slot);
    [Signal] public delegate void slot_movedEventHandler(int fromIndex, int toIndex);

    private const double InfoHoverDelay = 0.10;
    private TextureRect? icon_rect;
    private Label? name_label;
    private Godot.Timer? hover_timer;
    private bool is_trade_slot;
    private bool is_pointer_inside;
    private bool is_selected;
    private bool inventory_layout;
    private InventoryPaperFrame? inventory_frame;

    public string item_path = "";
    public int item_count;
    public int slot_index = -1;
    public bool is_artifact;
    public bool is_empty;
    public ArtifactInstanceData? artifact_data;
    public bool inventory_reorder_enabled;
    public bool spatial_drag_enabled;

    public void configure_spatial_layout(Vector2 footprint, Vector2 original, int rotation)
    {
        spatial_drag_enabled = true;
        Size = footprint; CustomMinimumSize = Vector2.Zero;
        if (GetNodeOrNull<Panel>("Frame") is { } frame)
        { frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); frame.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); frame.MouseFilter = MouseFilterEnum.Ignore; }
        name_label?.Hide();
        if (icon_rect is not null)
        {
            icon_rect.SetAnchorsPreset(LayoutPreset.TopLeft);
            icon_rect.Size = original; icon_rect.Position = (footprint - original) * .5f;
            icon_rect.PivotOffset = original * .5f; icon_rect.RotationDegrees = rotation * 90;
            icon_rect.MouseFilter = MouseFilterEnum.Ignore;
            icon_rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }
        Modulate = Colors.White;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        icon_rect = GetNodeOrNull<TextureRect>("Frame/ItemIcon");
        name_label = GetNodeOrNull<Label>("ItemName");
        for (var ancestor = GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is not TradePanel) continue;
            is_trade_slot = true;
            break;
        }
        hover_timer = GetNodeOrNull<Godot.Timer>("HoverTimer");
        if (hover_timer is not null) hover_timer.Timeout += on_hover_timer_timeout;
        MouseEntered += on_mouse_entered;
        MouseExited += on_mouse_exited;
        GuiInput += on_gui_input;
    }

    /// <summary>行囊与交易共用的纸纹物品格；具体尺寸由所在面板安排。</summary>
    public void configure_inventory_layout(float width, float height)
    {
        inventory_layout = true;
        CustomMinimumSize = new Vector2(width, height);
        if (GetNodeOrNull<Panel>("Frame") is { } frame)
        {
            frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            frame.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        }
        if (icon_rect is not null)
        {
            icon_rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            icon_rect.OffsetLeft = 12; icon_rect.OffsetTop = 10;
            icon_rect.OffsetRight = -12; icon_rect.OffsetBottom = -62;
        }
        if (name_label is not null)
        {
            name_label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            name_label.OffsetLeft = 5; name_label.OffsetTop = -59;
            name_label.OffsetRight = -5; name_label.OffsetBottom = -7;
            name_label.AddThemeFontSizeOverride("font_size", 20);
            name_label.MaxLinesVisible = 2;
            name_label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
        inventory_frame = new InventoryPaperFrame();
        AddChild(inventory_frame);
        MoveChild(inventory_frame, 0);
        inventory_frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        update_slot_tint();
    }

    public void set_item(int new_slot_index, string new_item_path, Resource item_data, int count)
    {
        slot_index = new_slot_index;
        is_selected = false;
        item_path = new_item_path;
        item_count = count;
        is_artifact = false;
        is_empty = false;
        artifact_data = null;
        MouseFilter = MouseFilterEnum.Stop;
        if (name_label is not null)
        {
            var item_name = item_data.Get("entity_name").AsString();
            name_label.Text = item_data is PillItemData
                ? $"{item_name} × {count}"
                : is_trade_slot ? $"{item_name} x{count}"
                : count > 1 ? $"{item_name} * {count}" : item_name;
        }
        if (icon_rect is not null)
        {
            icon_rect.Texture = load_icon(item_data);
            icon_rect.Material = null;
        }
        update_slot_tint();
    }

    public void set_artifact(ArtifactInstanceData artifact)
    {
        slot_index = -1;
        is_selected = false;
        item_path = "";
        item_count = 1;
        is_artifact = true;
        is_empty = false;
        artifact_data = artifact;
        MouseFilter = MouseFilterEnum.Stop;
        if (name_label is not null) name_label.Text = string.IsNullOrWhiteSpace(artifact.artifact_name) ? artifact.form?.form_name ?? "未命名法宝" : artifact.artifact_name;
        if (icon_rect is not null)
        {
            icon_rect.Material = null;
            var path = ForgedArtifactInventory.IconPath(artifact);
            icon_rect.Texture = string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path);
        }
        update_slot_tint();
    }

    public void set_empty(int new_slot_index)
    {
        slot_index = new_slot_index;
        is_selected = false;
        item_path = "";
        item_count = 0;
        is_artifact = false;
        is_empty = true;
        artifact_data = null;
        if (icon_rect is not null) { icon_rect.Texture = null; icon_rect.Material = null; }
        if (name_label is not null) name_label.Text = "";
        is_pointer_inside = false;
        update_slot_tint();
        MouseFilter = MouseFilterEnum.Stop;
        hover_timer?.Stop();
    }

    private static Texture2D? load_icon(Resource item_data)
    {
        var path = item_data.Get("icon_path").AsString();
        return string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path);
    }

    private void on_mouse_entered()
    {
        is_pointer_inside = true;
        update_slot_tint();
        hover_timer?.Start(InfoHoverDelay);
    }
    private void on_mouse_exited()
    {
        is_pointer_inside = false;
        update_slot_tint();
        hover_timer?.Stop();
        EmitSignal(SignalName.info_cleared, this);
    }

    public void set_inventory_selected(bool selected)
    {
        is_selected = selected;
        update_slot_tint();
    }

    private void update_slot_tint()
    {
        if (spatial_drag_enabled) { Modulate = Colors.White; return; }
        if (inventory_layout && inventory_frame is not null)
        {
            Modulate = Colors.White;
            inventory_frame.Selected = is_selected || is_pointer_inside;
            inventory_frame.EmptyMountain = is_empty;
            inventory_frame.QueueRedraw();
            return;
        }
        Modulate = is_selected
            ? new Color(1.08f, 0.91f, 0.68f, 1f)
            : is_pointer_inside ? new Color(1.08f, 1.04f, 0.9f, 1f) : Colors.White;
    }

    private void on_gui_input(InputEvent @event)
    {
        if (spatial_drag_enabled) return;
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        hover_timer?.Stop();
        EmitSignal(SignalName.clicked, this);
        EmitSignal(SignalName.info_requested, this, true);
        AcceptEvent();
    }

    private void on_hover_timer_timeout() => EmitSignal(SignalName.info_requested, this, false);
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!inventory_reorder_enabled || is_empty || slot_index < 0) return default;
        if (icon_rect?.Texture is { } texture)
        {
            var preview = new TextureRect
            {
                Texture = texture,
                CustomMinimumSize = new Vector2(72, 72),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            SetDragPreview(preview);
        }
        return slot_index;
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        inventory_reorder_enabled && data.VariantType == Variant.Type.Int && data.AsInt32() >= 0 && data.AsInt32() != slot_index;
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (inventory_reorder_enabled && data.VariantType == Variant.Type.Int && slot_index >= 0)
            EmitSignal(SignalName.slot_moved, data.AsInt32(), slot_index);
    }
}
