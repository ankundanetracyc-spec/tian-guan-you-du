using Godot;

/// <summary>全量物品图标分页审阅及离线打包入口，不改玩家库存或存档。</summary>
public partial class InventoryIconCatalogPreview : Control
{
    private const string Review = "res://docs/美术规范/行囊全物品图标与占地_20261029/";
    private const int PerPage = 8;
    private Control _canvas = null!;
    private Control? _page;
    private int _index;
    private InventoryIconDensityPack.Spec[] _specs = [];
    private readonly Dictionary<string, string> _names = [];

    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            InventoryIconDensityPack.ValidateManifest();
            if (args.Contains("--validate-inventory-manifest-only")) { GetTree().Quit(); return; }
            if (args.Contains("--pack-inventory-icons")) InventoryIconDensityPack.Pack();
            else InventoryIconDensityPack.Verify();
            if (args.Contains("--verify-inventory-icon-bindings")) InventoryIconDensityPack.VerifyBindings();
            if (args.Contains("--inventory-icons-check-only")) { GetTree().Quit(); return; }
            _specs = InventoryIconDensityPack.Specs;
            foreach (var value in Json.ParseString(Godot.FileAccess.GetFileAsString(InventoryIconDensityPack.Tasks)).AsGodotArray())
            {
                var row = value.AsGodotDictionary(); _names[row["file"].AsString()] = row["name"].AsString();
            }
            Theme = QuietRoomStyle.MakeTheme();
            var background = new ColorRect { Color = new Color("eae2cc"), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _canvas = QuietRoomStyle.Canvas(this);
            if (args.Contains("--capture-inventory-icon-gallery"))
            {
                for (_index = 0; _index < PageCount; _index++)
                {
                    ShowPage();
                    for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = GetViewport().GetTexture().GetImage();
                    if (image.SavePng(Review + $"全图标审阅_{_index + 1:00}.png") != Error.Ok)
                        throw new InvalidOperationException("全图标审阅截图失败");
                }
                GD.Print($"INVENTORY_ICON_GALLERY_OK: {_specs.Length} inventory icons, {PageCount} pages, 48 design units per cell (scaled with UI)");
                Input.SetCustomMouseCursor(null); GetTree().Quit(); return;
            }
            ShowPage();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private int PageCount => (_specs.Length + PerPage - 1) / PerPage;
    private void Text(Control parent, string value, Vector2 position, int size = 22)
    {
        var label = QuietRoomStyle.Text(value, size); parent.AddChild(label); label.Position = position;
    }
    private void ShowPage()
    {
        if (_page is not null) { _canvas.RemoveChild(_page); _page.QueueFree(); }
        _page = new Control(); _canvas.AddChild(_page);
        Text(_page, "行囊 · 全物品图标", new(64, 34), 32);
        Text(_page, "统一水墨淡彩 · 每格128源像素 · 每格48设计单位（随界面等比缩放）", new(64, 83), 19);
        for (var local = 0; local < PerPage && _index * PerPage + local < _specs.Length; local++)
        {
            var spec = _specs[_index * PerPage + local];
            var origin = new Vector2(64 + local % 4 * 396, 124 + local / 4 * 354);
            var panel = new Panel { Position = origin, Size = new(372, 338) };
            panel.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(padding: 0, plain: true)); _page.AddChild(panel);
            Text(panel, _names.GetValueOrDefault(spec.File, spec.File), new(14, 8), 21);
            Text(panel, $"{spec.Width} × {spec.Height} · {spec.Width * spec.Height}格", new(244, 12), 17);
            using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(InventoryIconDensityPack.Root + spec.File));
            var texture = ImageTexture.CreateFromImage(image);
            var footprint = new Vector2(spec.Width, spec.Height) * 48;
            // Set IgnoreSize before assigning Texture; otherwise its original
            // dimensions clamp Size before the smaller footprint is applied.
            var view = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Texture = texture };
            panel.AddChild(view);
            view.Position = new Vector2((372 - footprint.X) / 2, 40);
            view.Size = footprint;
        }
        Text(_page, $"第 {_index + 1} / {PageCount} 页 · 左右方向键翻页 · Esc返回", new(64, 865), 20);
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key || _specs.Length == 0) return;
        if (key.Keycode == Key.Right) { _index = (_index + 1) % PageCount; ShowPage(); }
        else if (key.Keycode == Key.Left) { _index = (_index + PageCount - 1) % PageCount; ShowPage(); }
        else if (key.Keycode == Key.Escape) GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
    }
}
