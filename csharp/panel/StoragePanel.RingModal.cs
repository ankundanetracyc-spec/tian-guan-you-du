using Godot;

public partial class StoragePanel
{
    private ColorRect? _nativeRingShade, _nativeRingPaper, _nativePageRule;
    private Control? _nativeParentUnderlay;
    private NativeInventorySurface? _nativeParentBody;
    private Button? _nativeMainBack;
    private bool _nativeShadePressed;

    private void build_ring_modal()
    {
        _nativeParentUnderlay = new Control { Name = "ParentInventoryUnderlay", Size = DesignSize, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 1, Visible = false };
        _canvas!.AddChild(_nativeParentUnderlay);
        var backTexture = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/gongfa_back_button_v1.png"), MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        place(backTexture, _nativeParentUnderlay, 48, 32, 54, 54);
        var arrow = text("‹", 39, Ink); arrow.HorizontalAlignment = HorizontalAlignment.Center; place(arrow, _nativeParentUnderlay, 48, 32, 54, 54);
        place(text("行囊", 44, Ink), _nativeParentUnderlay, 124, 26, 200, 70);
        _nativeParentBody = new NativeInventorySurface { Name = "ParentBodyStorage", MouseFilter = MouseFilterEnum.Ignore, DrawContents = draw_parent_inventory };
        place(_nativeParentBody, _nativeParentUnderlay, 486, 258, 1350, 698);
        foreach (var region in Character.InventoryRegions)
        {
            var rect = _nativeBodyRegions![region.Id];
            place(text(region.Name, 25, new("52664a")), _nativeParentBody, rect.Position.X, rect.Position.Y - 43, rect.Size.X, 36);
        }
        _nativeRingShade = new ColorRect { Name = "StorageRingShade", Color = new Color(0, 0, 0, .55f), MouseFilter = MouseFilterEnum.Stop, ZIndex = 10, Visible = false };
        place(_nativeRingShade, _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        _nativeRingShade.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;
            if (mouse.Pressed) _nativeShadePressed = true;
            else
            {
                var global = _nativeRingShade.GetGlobalTransformWithCanvas() * mouse.Position;
                bool close = _nativeShadePressed && _nativeContainerId.Length > 0 && _nativeRingFrame?.GetGlobalRect().HasPoint(global) == false;
                _nativeShadePressed = false;
                if (close) open_native_container("");
            }
            _nativeRingShade.AcceptEvent();
        };
        _nativeRingPaper = new ColorRect { Name = "StorageRingPaperBase", Color = new("ebe1c9"), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 11, Visible = false };
        _canvas.AddChild(_nativeRingPaper); _nativeRingFrame!.ZIndex = 12;
    }

    private void layout_body_after_ring_modal()
    {
        _nativeRingShade?.Hide(); _nativeParentUnderlay?.Hide(); _nativeRingPaper?.Hide(); _nativeShadePressed = false;
        _nativeMainBack!.Position = new(48, 32); _nativeMainBack.ZIndex = 0;
        _nativePageTitle!.Position = new(124, 26); _nativePageTitle.Size = new(1380, 70); _nativePageTitle.ZIndex = 0;
        _nativePageTitle.AddThemeFontSizeOverride("font_size", 44);
        _nativePageRule!.Position = new(48, 102); _nativePageRule.Size = new(1824, 1); _nativePageRule.ZIndex = 0;
        _nativeSpaceScroll!.ZIndex = 0; _nativeStatus!.ZIndex = 0; _nativeStatus.Size = new(1300, 38);
        if (_nativeCount is not null) { _nativeCount.ZIndex = 0; _nativeCount.Size = new(384, 32); }
        _nativeBodyReturn!.ZIndex = 0; _nativeRingFrame!.ZIndex = 12;
    }

    private Vector2 layout_ring_modal(StorageRingInfo ring, Vector2 grid)
    {
        var viewport = new Vector2(Mathf.Min(RingViewportSize.X, Mathf.Max(696, grid.X)), Mathf.Min(RingViewportSize.Y, grid.Y));
        var frameSize = viewport + new Vector2(24, 168);
        var origin = (DesignSize - frameSize) * .5f;
        _nativeRingFrame!.Position = origin; _nativeRingFrame.Size = frameSize; _nativeRingFrame.Show();
        _nativeRingPaper!.Position = origin + new Vector2(6, 6); _nativeRingPaper.Size = frameSize - new Vector2(12, 12); _nativeRingPaper.Show();
        _nativeIndexPanel!.Show(); _nativeSpacePanel!.Show(); _nativeParentUnderlay!.Show(); _nativeParentBody!.QueueRedraw(); _nativeRingShade!.Show();
        _nativeMainBack!.Position = origin + new Vector2(12, 12); _nativeMainBack.ZIndex = 13;
        bool wide = frameSize.X >= 1200;
        float returnWidth = wide ? 232 : 160;
        _nativePageTitle!.Text = ring.DisplayName; _nativePageTitle.Position = origin + new Vector2(88, 4);
        _nativePageTitle.Size = new(frameSize.X - returnWidth - 124, 70); _nativePageTitle.ZIndex = 13;
        _nativePageTitle.AddThemeFontSizeOverride("font_size", wide ? 44 : 32);
        _nativePageRule!.Position = origin + new Vector2(12, 92); _nativePageRule.Size = new(frameSize.X - 24, 1); _nativePageRule.ZIndex = 13;
        _nativeBodyReturn!.Position = origin + new Vector2(frameSize.X - returnWidth - 12, 12); _nativeBodyReturn.Size = new(returnWidth, 48); _nativeBodyReturn.ZIndex = 13;
        _nativeSpaceScroll!.Position = origin + new Vector2(12, 108); _nativeSpaceScroll.Size = viewport; _nativeSpaceScroll.ZIndex = 13;
        _nativeStatus!.Position = origin + new Vector2(12, frameSize.Y - 44); _nativeStatus.Size = new(Mathf.Max(200, frameSize.X - 430), 38); _nativeStatus.ZIndex = 13;
        if (_nativeCount is not null) { _nativeCount.Position = origin + new Vector2(frameSize.X - 396, frameSize.Y - 40); _nativeCount.Size = new(384, 32); _nativeCount.ZIndex = 13; }
        // GUI picking follows scene order, so foreground controls must follow the shade too.
        foreach (var control in new Control?[] { _nativeRingPaper, _nativeRingFrame, _nativePageRule, _nativeMainBack,
            _nativePageTitle, _nativeBodyReturn, _nativeStatus, _nativeCount, _nativeSpaceScroll, _detail_panel })
            if (control is not null) _canvas!.MoveChild(control, _canvas.GetChildCount() - 1);
        return viewport;
    }

    private void fit_ring_shade(Vector2 viewport, float scale)
    {
        if (_nativeRingShade is null || _canvas is null || scale <= 0) return;
        _nativeRingShade.Position = -_canvas.Position / scale;
        _nativeRingShade.Size = viewport / scale;
    }

    private void set_parent_inventory_focus(bool enabled)
    {
        if (_nativeIndexPanel is null) return;
        if (!enabled && GetViewport().GuiGetFocusOwner() is { } focused && _nativeIndexPanel.IsAncestorOf(focused)) GetViewport().GuiReleaseFocus();
        foreach (var control in _nativeIndexPanel.FindChildren("*", "Control", true, false).OfType<Control>())
            if (control is BaseButton or LineEdit) control.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
    }

    private void draw_parent_inventory(NativeInventorySurface surface)
    {
        foreach (var region in Character.InventoryRegions)
        {
            var rect = _nativeBodyRegions![region.Id];
            surface.DrawRect(rect, new(.84f, .88f, .75f, .25f));
            for (int x = 0; x <= region.Width; x++) surface.DrawLine(rect.Position + new Vector2(x * NativeCell, 0), rect.Position + new Vector2(x * NativeCell, rect.Size.Y), new(.55f, .62f, .47f, .5f));
            for (int y = 0; y <= region.Height; y++) surface.DrawLine(rect.Position + new Vector2(0, y * NativeCell), rect.Position + new Vector2(rect.Size.X, y * NativeCell), new(.55f, .62f, .47f, .5f));
        }
        foreach (var entry in _player?.get_spatial_inventory().Where(e => e.IsPlaced) ?? [])
        {
            var path = entry.Artifact is not null ? ForgedArtifactInventory.IconPath(entry.Artifact) : entry.Item?.icon_path ?? "";
            if (path.Length == 0 || !ResourceLoader.Exists(path)) continue;
            var texture = GD.Load<Texture2D>(path); if (texture is null) continue;
            var rect = _nativeBodyRegions![entry.RegionId]; var size = new Vector2(entry.Width, entry.Height) * NativeCell;
            var original = (entry.Rotation & 1) == 0 ? size : new Vector2(size.Y, size.X);
            var center = rect.Position + new Vector2(entry.X, entry.Y) * NativeCell + size * .5f;
            surface.DrawSetTransform(center, entry.Rotation * Mathf.Pi * .5f);
            surface.DrawTextureRect(texture, new Rect2(-original * .5f, original), false, native_matches(entry) ? Colors.White : new Color(1, 1, 1, .25f));
            surface.DrawSetTransform(Vector2.Zero);
        }
    }
}
