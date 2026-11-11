using Godot;

public partial class ItemClaimPanel
{
    private static string withdrawn_group(SpatialInventoryEntry entry) => entry.Artifact is not null || entry.Item is StorageBagItemData
        ? entry.InstanceId : entry.ItemPath;

    private void append_withdrawn_rows()
    {
        if (_withdrawn.Count == 0) return;
        foreach (var group in _withdrawn.Values.GroupBy(withdrawn_group).Where(g => !_items.Any(i => i.Path == g.Key)))
        {
            var entry = group.First();
            int groupCount = group.Sum(e => e.Count);
            var row = new Button { Name = "WithdrawnRow_" + entry.InstanceId, CustomMinimumSize = new(0, 92), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            style_button(row); _rows.AddChild(row); row.SetMeta("withdrawn_instance_id", entry.InstanceId); row.SetMeta("withdrawn_group", group.Key);
            if (entry.ItemPath.Length > 0) row.SetMeta("item_path", entry.ItemPath);
            var icon = entry.Artifact is not null ? ForgedArtifactInventory.IconPath(entry.Artifact) : entry.Item?.icon_path ?? "";
            put(new TextureRect { Texture = load_icon(icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore }, row, 12, 12, 58, 68);
            var name = entry.Item is StorageBagItemData
                ? _recipient?.get_storage_bags().FirstOrDefault(r => r.InstanceId == entry.InstanceId)?.DisplayName ?? entry_name(entry)
                : entry_name(entry);
            put(text(name, 22, Ink), row, 80, 23, 208, 46);
            var quantity = text("×" + groupCount.ToString("N0"), 22, Jade); quantity.HorizontalAlignment = HorizontalAlignment.Right;
            put(quantity, row, 280, 23, 80, 46);
            HoverInfoManager.SetProvider(row, () => entry.Artifact is not null ? EquipmentItemPresentation.Hover(entry.Artifact, _recipient)
                : StoragePanel.ItemHover(entry.Item!, groupCount, entry.ItemPath, _recipient));
            row.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
                { start_withdrawn_drag(entry, row, mouse); row.AcceptEvent(); }
            };
        }
    }

    private void reveal_left_entry(SpatialInventoryEntry entry)
    {
        var owned = _withdrawn.ContainsKey(entry.InstanceId);
        var row = _rows.GetChildren().OfType<Control>().FirstOrDefault(r => owned
            ? r.HasMeta("withdrawn_group") && r.GetMeta("withdrawn_group").AsString() == withdrawn_group(entry)
            : r.HasMeta("item_path") && r.GetMeta("item_path").AsString() == entry.ItemPath);
        if (row is not null) Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(row) && row.IsInsideTree()) _scroll.EnsureControlVisible(row);
        }).CallDeferred();
    }
}
