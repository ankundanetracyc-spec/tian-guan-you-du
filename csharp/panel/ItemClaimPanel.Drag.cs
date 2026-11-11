using Godot;

public partial class ItemClaimPanel
{
    private sealed class ClaimDrag
    {
        public required SpatialInventoryEntry Entry;
        public required Control Source;
        public ClaimItem? Incoming, ClickSelection;
        public Vector2 Start, Pointer, Grab;
        public Vector2I Size, Cell;
        public int Rotation;
        public bool Active, Valid, Discard, FromList;
        public string Region = "";
        public Control? Ghost;
        public TextureRect? Image;
        public Label? Hint;
    }
    private ClaimDrag? _drag;
    private Vector2 local_pointer(Vector2 p) => _canvas.GetGlobalTransformWithCanvas().AffineInverse() * p;
    private void start_incoming_drag(ClaimItem item, Control source, InputEventMouseButton mouse)
    {
        if (!_presented || _leaveDialog?.Visible == true) return;
        var entry = incoming_entry(item); var point = local_pointer(source.GetGlobalTransformWithCanvas() * mouse.Position);
        _drag = new ClaimDrag { Entry = entry, Incoming = item, FromList = true, Source = source, Start = point, Pointer = point,
            Size = entry.Size, Grab = new Vector2(entry.Width, entry.Height) * .5f };
    }
    private void start_pack_drag(SpatialInventoryEntry entry, Control source, InputEventMouseButton mouse)
    {
        if (!_presented || _leaveDialog?.Visible == true) return;
        var point = local_pointer(source.GetGlobalTransformWithCanvas() * mouse.Position);
        _drag = new ClaimDrag { Entry = entry, Source = source, Start = point, Pointer = point,
            Size = entry.Size, Rotation = entry.Rotation, Grab = mouse.Position / Cell };
    }
    private void start_withdrawn_drag(SpatialInventoryEntry entry, Control source, InputEventMouseButton mouse)
    {
        if (!_presented || _leaveDialog?.Visible == true || !_withdrawn.ContainsKey(entry.InstanceId)) return;
        var point = local_pointer(source.GetGlobalTransformWithCanvas() * mouse.Position);
        _drag = new ClaimDrag { Entry = entry, FromList = true, Source = source, Start = point, Pointer = point,
            Size = entry.Size, Rotation = entry.Rotation, Grab = new Vector2(entry.Width, entry.Height) * .5f };
    }
    public override void _Input(InputEvent ev)
    {
        if (!_presented || _leaveDialog?.Visible == true) return;
        if (_drag is not { } d)
        {
            if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
                || ev is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            { request_finish(); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (ev is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape) { cancel_drag(); _status.Text = "已取消拖动"; GetViewport().SetInputAsHandled(); return; }
            if (d.Active && (key.Keycode == Key.R || key.PhysicalKeycode == Key.R))
            {
                if (d.Entry.Item?.inventory_can_rotate != false)
                { d.Grab = new(d.Size.Y - d.Grab.Y, d.Grab.X); d.Size = new(d.Size.Y, d.Size.X); d.Rotation = (d.Rotation + 1) % 4; update_drag(); }
                GetViewport().SetInputAsHandled(); return;
            }
        }
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        { cancel_drag(); GetViewport().SetInputAsHandled(); return; }
        if (ev is InputEventMouseMotion motion)
        {
            d.Pointer = local_pointer(motion.Position);
            if (!d.Active && d.Pointer.DistanceTo(d.Start) * _canvas.Scale.X >= 4) activate_drag(d);
            if (d.Active) { update_drag(); GetViewport().SetInputAsHandled(); }
        }
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release)
        {
            if (!d.Active)
            {
                cancel_drag(); var selection = d.ClickSelection ?? d.Incoming;
                if (selection is not null) { selection.Selected = !selection.Selected; refresh(); }
            }
            else
            {
                d.Pointer = local_pointer(release.Position); update_drag();
                bool success = d.Discard ? discard_entry(d.Entry) : d.Valid && place_entry(d.Entry, d.Incoming, d.Region, d.Cell.X, d.Cell.Y, d.Rotation, d.FromList);
                string message = success ? d.Discard ? "已移到左侧，可拖回：" + entry_name(d.Entry) : "已按容量加入：" + entry_name(d.Entry)
                    : d.Discard && _recipient?.has_nonempty_storage_bag(d.Entry.InstanceId) == true ? "请先取空储物袋，再丢弃"
                    : d.Entry.IsEquipped && d.Discard ? "使用中的物品不能丢弃" : "容量不足或目标无效，已保留原处";
                cancel_drag(); refresh(message);
                if (success && d.Discard) reveal_left_entry(d.Entry);
            }
            GetViewport().SetInputAsHandled();
        }
    }
    private void activate_drag(ClaimDrag d)
    {
        HoverInfoManager.HideAll(); HoverInfoManager.SuspendFor(d.Source, true); d.Active = true;
        if (!d.FromList) d.Source.Hide();
        d.Ghost = new Control { Name = "DraggedItem", ZIndex = 100, MouseFilter = Control.MouseFilterEnum.Ignore }; _canvas.AddChild(d.Ghost);
        d.Image = new TextureRect { Texture = d.Entry.Item is not null ? load_icon(d.Entry.Item.icon_path) : load_icon(ForgedArtifactInventory.IconPath(d.Entry.Artifact!)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        d.Ghost.AddChild(d.Image); d.Hint = text("", 18, Jade); d.Ghost.AddChild(d.Hint);
        _status.Text = "R 旋转 · Esc 取消 · 左侧可暂放";
    }
    private void update_drag()
    {
        if (_drag is not { Active: true } d) return;
        d.Ghost!.Position = d.Pointer - d.Grab * Cell; d.Ghost.Size = new Vector2(d.Size.X, d.Size.Y) * Cell;
        var original = rotated_size(d.Entry, 0); d.Image!.Size = new Vector2(original.X, original.Y) * Cell;
        d.Image.Position = (d.Ghost.Size - d.Image.Size) * .5f; d.Image.PivotOffset = d.Image.Size * .5f; d.Image.RotationDegrees = d.Rotation * 90;
        var point = _surface.GetGlobalTransformWithCanvas().AffineInverse() * (_canvas.GetGlobalTransformWithCanvas() * d.Pointer);
        d.Region = ""; d.Valid = d.Discard = false;
        foreach (var pair in _regions)
        {
            if (!pair.Value.HasPoint(point)) continue;
            d.Region = pair.Key; var cell = (point - pair.Value.Position) / Cell - d.Grab;
            d.Cell = new(Mathf.RoundToInt(cell.X), Mathf.RoundToInt(cell.Y));
            d.Valid = d.FromList
                ? capacity_fits(d.Entry)
                : fits(d.Entry, d.Region, d.Cell.X, d.Cell.Y, d.Rotation);
            break;
        }
        // Empty space between body regions stays invalid. Leaving the right panel stages the item on the left.
        var inventoryArea = new Rect2(504, 124, 1370, 806);
        d.Discard = !d.FromList && !inventoryArea.HasPoint(d.Pointer) && new Rect2(Vector2.Zero, DesignSize).HasPoint(d.Pointer);
        d.Hint!.Text = d.Discard ? _recipient?.has_nonempty_storage_bag(d.Entry.InstanceId) == true ? "袋内有物，不能丢弃"
            : d.Entry.IsEquipped ? "使用中，不能丢弃" : "松开移到左侧" : d.Valid ? "松开放置 · R 旋转" : "不可放置";
        d.Hint.AddThemeColorOverride("font_color", new Color(d.Valid ? "466445" : "985b43"));
        d.Hint.Position = new(0, d.Ghost.Size.Y + 4); d.Hint.Size = new(Mathf.Max(240, d.Ghost.Size.X), 28); _surface.QueueRedraw();
    }
    private void cancel_drag()
    {
        if (_drag is not { } d) return; _drag = null;
        if (GodotObject.IsInstanceValid(d.Source)) { if (!d.FromList) d.Source.Show(); HoverInfoManager.SuspendFor(d.Source, false); }
        if (d.Ghost is not null && GodotObject.IsInstanceValid(d.Ghost)) { d.Ghost.Hide(); d.Ghost.QueueFree(); }
        _surface?.QueueRedraw();
    }
}
