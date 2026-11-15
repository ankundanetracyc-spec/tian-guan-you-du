using Godot;

public partial class StoragePanel
{
    private Action<string, string>? _spatialPreviewFilter;
    private Action? _spatialPreviewOrganize;

    /// <summary>布局试阅复用正式外壳，只替换物品区；不绑定人物或修改库存。</summary>
    internal void attach_spatial_layout_preview(Control board, Action<string, string> filter, Action organize)
    {
        _spatialPreviewFilter = filter;
        _spatialPreviewOrganize = organize;
        _grid_scroll?.Hide();
        _empty?.Hide();
        if (_volumeCards is not null)
            foreach (var child in _canvas!.GetChildren().OfType<Control>())
                if (child.Name != "StorageReturn") child.Hide();
        _canvas!.AddChild(board);
        board.Position = new(284, 156);
        board.Size = new(1348, 704);
        show_panel();
        filter(_category_filter, _search_text);
    }

    internal void set_spatial_preview_summary(string summary) { if (_summary is not null) _summary.Text = summary; }
    internal void add_body_storage_preview_names()
    {
        if (_canvas is null) return;
        place(text("胸前", 25, Ink), _canvas, 740, 184, 240, 40);
        place(text("左袖", 23, Ink), _canvas, 468, 320, 160, 40);
        place(text("右袖", 23, Ink), _canvas, 1156, 320, 160, 40);
        place(text("腰间", 25, Ink), _canvas, 740, 528, 240, 40);
        place(text("背囊", 25, Ink), _canvas, 1324, 288, 240, 40);
    }
    internal void set_spatial_preview_hint(string hint)
    {
        if (_canvas is null) return;
        foreach (var label in _canvas.GetChildren().OfType<Label>())
            if (Mathf.IsEqualApprox(label.Position.X, 378) && Mathf.IsEqualApprox(label.Position.Y, 881)) label.Text = hint;
    }
}
