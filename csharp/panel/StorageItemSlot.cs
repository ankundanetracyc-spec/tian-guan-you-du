using Godot;

/// <summary>行囊物品格的展示与延迟信息请求；支持普通堆叠物品和唯一法宝实例。</summary>
public partial class StorageItemSlot : Control
{
    [Signal] public delegate void info_requestedEventHandler(StorageItemSlot slot, bool immediate);
    [Signal] public delegate void info_clearedEventHandler(StorageItemSlot slot);
    [Signal] public delegate void clickedEventHandler(StorageItemSlot slot);
    [Signal] public delegate void slot_movedEventHandler(int fromIndex, int toIndex);

    private const double InfoHoverDelay = 0.45;
    private TextureRect? icon_rect;
    private Label? name_label;
    private Label? count_label;
    private Godot.Timer? hover_timer;
    private bool is_trade_slot;

    public string item_path = "";
    public int item_count;
    public int slot_index = -1;
    public bool is_artifact;
    public bool is_empty;
    public ArtifactInstanceData? artifact_data;
    public bool inventory_reorder_enabled;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        icon_rect = GetNodeOrNull<TextureRect>("Frame/ItemIcon");
        name_label = GetNodeOrNull<Label>("ItemName");
        count_label = GetNodeOrNull<Label>("Frame/ItemCount");
        for (var ancestor = GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is not TradePanel) continue;
            is_trade_slot = true;
            count_label?.QueueFree();
            count_label = null;
            break;
        }
        hover_timer = GetNodeOrNull<Godot.Timer>("HoverTimer");
        if (hover_timer is not null) hover_timer.Timeout += on_hover_timer_timeout;
        MouseEntered += on_mouse_entered;
        MouseExited += on_mouse_exited;
        GuiInput += on_gui_input;
    }

    public void set_item(int new_slot_index, string new_item_path, Resource item_data, int count)
    {
        slot_index = new_slot_index;
        item_path = new_item_path;
        item_count = count;
        is_artifact = false;
        is_empty = false;
        artifact_data = null;
        MouseFilter = MouseFilterEnum.Stop;
        if (name_label is not null)
        {
            var item_name = item_data.Get("entity_name").AsString();
            name_label.Text = is_trade_slot ? $"{item_name} x{count}" : item_name;
        }
        if (count_label is not null) count_label.Text = $"x{count}";
        if (icon_rect is not null) icon_rect.Texture = load_icon(item_data);
    }

    public void set_artifact(ArtifactInstanceData artifact)
    {
        slot_index = -1;
        item_path = "";
        item_count = 1;
        is_artifact = true;
        is_empty = false;
        artifact_data = artifact;
        MouseFilter = MouseFilterEnum.Stop;
        if (name_label is not null) name_label.Text = string.IsNullOrWhiteSpace(artifact.artifact_name) ? artifact.form?.form_name ?? "未命名法宝" : artifact.artifact_name;
        if (count_label is not null) count_label.Text = artifact.max_durability > 0 ? $"耐久 {Math.Max(0, artifact.current_durability)}/{artifact.max_durability}" : artifact.ritual_mode;
        if (icon_rect is not null)
        {
            var path = artifact.form?.icon_path ?? "";
            icon_rect.Texture = string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path) ? null : ResourceLoader.Load<Texture2D>(path);
        }
    }

    public void set_empty(int new_slot_index)
    {
        slot_index = new_slot_index;
        item_path = "";
        item_count = 0;
        is_artifact = false;
        is_empty = true;
        artifact_data = null;
        if (icon_rect is not null) icon_rect.Texture = null;
        if (name_label is not null) name_label.Text = "";
        if (count_label is not null) count_label.Text = "";
        Modulate = Colors.White;
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
        Modulate = new Color(1.08f, 1.04f, 0.9f, 1f);
        hover_timer?.Start(InfoHoverDelay);
    }
    private void on_mouse_exited()
    {
        Modulate = Colors.White;
        hover_timer?.Stop();
        EmitSignal(SignalName.info_cleared, this);
    }

    private void on_gui_input(InputEvent @event)
    {
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
