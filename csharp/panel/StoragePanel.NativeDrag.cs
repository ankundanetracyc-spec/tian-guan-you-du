using Godot;

public partial class StoragePanel
{
    private sealed class NativeDragState
    {
        public required SpatialInventoryEntry Item;
        public required StorageItemSlot Source;
        public Vector2 Start, Pointer, Grab;
        public Vector2I Size;
        public int Rotation;
        public bool Active, Valid;
        public string Region = "";
        public Vector2I Cell;
        public Control? Ghost;
        public TextureRect? Image;
        public Label? Hint;
    }
    private NativeDragState? _nativeDrag;

    private void native_slot_input(SpatialInventoryEntry entry, StorageItemSlot slot, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse) return;
        clear_active_info();
        var pointer = _canvas!.GetGlobalTransformWithCanvas().AffineInverse() * (slot.GetGlobalTransformWithCanvas() * mouse.Position);
        _nativeDrag = new NativeDragState
        {
            Item = entry, Source = slot, Start = pointer, Pointer = pointer, Rotation = entry.Rotation,
            Size = entry.Size, Grab = mouse.Position / NativeCell
        };
        _nativeSelected = entry.InstanceId; _nativeSurface!.QueueRedraw(); slot.AcceptEvent();
    }
    private bool handle_native_input(InputEvent ev)
    {
        if (_nativeDrag is not { } drag) return false;
        if (ev is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape)
            { cancel_native_drag(); _nativeStatus!.Text = "已取消移动"; GetViewport().SetInputAsHandled(); return true; }
            if (drag.Active && (key.Keycode == Key.R || key.PhysicalKeycode == Key.R))
            {
                if (drag.Item.Item?.inventory_can_rotate != false)
                {
                    drag.Grab = new(drag.Size.Y - drag.Grab.Y, drag.Grab.X);
                    drag.Size = new(drag.Size.Y, drag.Size.X); drag.Rotation = (drag.Rotation + 1) % 4;
                    update_native_drag();
                }
                GetViewport().SetInputAsHandled(); return true;
            }
        }
        if (ev is InputEventMouseMotion motion)
        {
            drag.Pointer = _canvas!.GetGlobalTransformWithCanvas().AffineInverse() * motion.Position;
            if (!drag.Active && drag.Pointer.DistanceTo(drag.Start) * _canvas.Scale.X >= 4) activate_native_drag(drag);
            if (drag.Active) { update_native_drag(); GetViewport().SetInputAsHandled(); return true; }
        }
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        { cancel_native_drag(); GetViewport().SetInputAsHandled(); return true; }
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release)
        {
            if (!drag.Active)
            {
                _nativeDrag = null;
                if (drag.Item.Item is StorageRingItemData) open_native_container(drag.Item.InstanceId);
                else on_slot_info_requested(drag.Source, true);
            }
            else
            {
                drag.Pointer = _canvas!.GetGlobalTransformWithCanvas().AffineInverse() * release.Position; update_native_drag();
                var region = drag.Region; var cell = drag.Cell; var rotation = drag.Rotation; var id = drag.Item.InstanceId;
                var valid = drag.Valid;
                cancel_native_drag();
                var moved = valid && _player?.move_stored_item(id, _nativeContainerId, region, cell.X, cell.Y, rotation) == true;
                _nativeLocated = moved ? id : "";
                _nativeStatus!.Text = moved ? "已放置：" + native_name(drag.Item) : "无法放置，已保留原位";
                _nativeSurface!.QueueRedraw();
            }
            GetViewport().SetInputAsHandled(); return true;
        }
        return false;
    }
    private void activate_native_drag(NativeDragState drag)
    {
        HoverInfoManager.HideAll(); clear_active_info(); drag.Active = true;
        drag.Source.Modulate = new(1, 1, 1, .18f);
        HoverInfoManager.SuspendFor(drag.Source, true);
        drag.Ghost = new Control { Name = "DraggedItem", ZIndex = 55, MouseFilter = MouseFilterEnum.Ignore };
        _canvas!.AddChild(drag.Ghost);
        var icon = drag.Source.GetNodeOrNull<TextureRect>("Frame/ItemIcon")?.Texture;
        drag.Image = new TextureRect
        {
            Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
        };
        drag.Ghost.AddChild(drag.Image);
        drag.Hint = text("", 16, new("466445")); drag.Ghost.AddChild(drag.Hint);
        _nativeStatus!.Text = "R 旋转 · Esc 取消";
    }
    private void update_native_drag()
    {
        if (_nativeDrag is not { Active: true } drag || _canvas is null || _nativeSurface is null) return;
        drag.Ghost!.Position = drag.Pointer - drag.Grab * NativeCell;
        drag.Ghost.Size = new Vector2(drag.Size.X, drag.Size.Y) * NativeCell;
        var original = (drag.Item.Rotation & 1) == 0 ? drag.Item.Size : new Vector2I(drag.Item.Height, drag.Item.Width);
        drag.Image!.Size = new Vector2(original.X, original.Y) * NativeCell;
        drag.Image.Position = (drag.Ghost.Size - drag.Image.Size) * .5f;
        drag.Image.PivotOffset = drag.Image.Size * .5f; drag.Image.RotationDegrees = drag.Rotation * 90;
        var globalPointer = _canvas.GetGlobalTransformWithCanvas() * drag.Pointer;
        var point = _nativeSurface.GetGlobalTransformWithCanvas().AffineInverse() * globalPointer;
        drag.Region = ""; drag.Valid = false;
        foreach (var pair in _nativeRegions.Where(_ => _nativeSpaceScroll?.GetGlobalRect().HasPoint(globalPointer) != false))
        {
            if (!pair.Value.HasPoint(point)) continue;
            drag.Region = pair.Key;
            var cell = (point - pair.Value.Position) / NativeCell - drag.Grab;
            drag.Cell = new(Mathf.RoundToInt(cell.X), Mathf.RoundToInt(cell.Y));
            drag.Valid = _player?.can_move_stored_item(drag.Item.InstanceId, _nativeContainerId, drag.Region, drag.Cell.X, drag.Cell.Y, drag.Rotation) == true;
            break;
        }
        drag.Hint!.Text = drag.Valid ? "松开放置" : "不可放置";
        drag.Hint.AddThemeColorOverride("font_color", new Color(drag.Valid ? "466445" : "985b43"));
        drag.Hint.Position = new(0, drag.Ghost.Size.Y + 4);
        drag.Hint.Size = new(Mathf.Max(100, drag.Ghost.Size.X), 24);
        _nativeSurface.QueueRedraw();
    }
    private void cancel_native_drag()
    {
        _nativeShadePressed = false;
        _nativeCategoryPressed = _nativeCategoryPanning = false; _nativeCategoryButton = null;
        if (_nativeDrag is not { } drag) return;
        _nativeDrag = null;
        if (GodotObject.IsInstanceValid(drag.Source))
        { drag.Source.Modulate = Colors.White; HoverInfoManager.SuspendFor(drag.Source, false); }
        if (drag.Ghost is not null && GodotObject.IsInstanceValid(drag.Ghost)) { drag.Ghost.Hide(); drag.Ghost.QueueFree(); }
        _nativeSurface?.QueueRedraw();
    }
    private void draw_native_drop(NativeInventorySurface surface)
    {
        if (_nativeDrag is not { Active: true } drag || !_nativeRegions.TryGetValue(drag.Region, out var region)) return;
        var rect = new Rect2(region.Position + new Vector2(drag.Cell.X, drag.Cell.Y) * NativeCell, new Vector2(drag.Size.X, drag.Size.Y) * NativeCell);
        var color = new Color(drag.Valid ? "678851" : "ac7057");
        surface.DrawRect(rect, new Color(color, .22f));
        if (drag.Valid) surface.DrawRect(rect, color, false, 2);
        else
        {
            surface.DrawDashedLine(rect.Position, rect.Position + new Vector2(rect.Size.X, 0), color, 2, 7);
            surface.DrawDashedLine(rect.Position + new Vector2(rect.Size.X, 0), rect.End, color, 2, 7);
            surface.DrawDashedLine(rect.End, rect.Position + new Vector2(0, rect.Size.Y), color, 2, 7);
            surface.DrawDashedLine(rect.Position + new Vector2(0, rect.Size.Y), rect.Position, color, 2, 7);
        }
    }
}
