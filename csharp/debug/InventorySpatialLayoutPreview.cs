using Godot;

/// <summary>复用原行囊外壳，仅替换物品网格的独立试阅。</summary>
public partial class InventorySpatialLayoutPreview : Control
{
    private const string Assets = InventoryIconDensityPack.Root;
    private const string Review = "res://docs/美术规范/行囊细网格样板_20261029/";
    private static readonly Color Teal = new("46685e");
    private StoragePanel _shell = null!;
    private SpatialBoard _board = null!;

    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--pack-inventory-icons")) InventoryIconDensityPack.Pack();
            else InventoryIconDensityPack.Verify();
            if (OS.GetCmdlineUserArgs().Contains("--verify-inventory-icon-bindings")) InventoryIconDensityPack.VerifyBindings();
            _shell = new StoragePanel();
            AddChild(_shell);
            _shell.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _board = new SpatialBoard();
            _board.SelectionChanged = ShowItem;
            _board.HoverChanged = ShowHover;
            _board.Changed = UpdateSummary;
            _board.Status = text => _shell.set_spatial_preview_hint(text);
            _shell.attach_spatial_layout_preview(_board, (category, search) =>
            {
                _board.Category = category == "__artifacts__" ? "器物" : category;
                _board.Search = search;
                _board.QueueRedraw();
            }, () => { _board.Organize(); UpdateSummary(); });
            _board.ResetSamples(Assets);
            _shell.add_body_storage_preview_names();
            UpdateSummary();
            _shell.set_spatial_preview_hint("悬停查看物品详情 · 拖动摆放 · R旋转");
            _shell.VisibilityChanged += () =>
            {
                if (!_shell.Visible && IsInsideTree()) GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
            };
            if (OS.GetCmdlineUserArgs().Contains("--capture-inventory-layout"))
            {
                // Rotation checks should not request a tooltip for the captured
                // idle layout. Keep normal interactive selection unchanged.
                _board.SelectionChanged = null;
                _board.Verify();
                _board.SelectionChanged = ShowItem;
                _board.CancelDrag();
                HoverInfoManager.HideAll();
                _shell.set_spatial_preview_hint("悬停查看物品详情 · 拖动摆放 · R旋转");
                UpdateSummary();
                for (var f = 0; f < 6; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                if (image.SavePng(Review + "行囊_区名与统一图标.png") != Error.Ok)
                    throw new InvalidOperationException("布局截图保存失败");
                GD.Print("INVENTORY_DENSITY_LAYOUT_OK: five region names, no size captions, bottle/manual samples, 120 cells");
                Input.SetCustomMouseCursor(null); GetTree().Quit();
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void UpdateSummary()
    {
        var used = _board.Items.Sum(item => item.Width * item.Height);
        _shell.set_spatial_preview_summary($"占用 {used} / {_board.Columns * _board.Rows} 格");
    }
    private void ShowItem(SpatialBoard.Sample item) => ShowHover(item);
    private void ShowHover(SpatialBoard.Sample? item)
    {
        if (item is null) { HoverInfoManager.HideFor(_board); return; }
        HoverInfoManager.SetInfo(_board, new HoverInfoManager.HoverInfoContent(item.Name,
            $"占地：{item.W} × {item.H}", item.Texture, item.Category, ShowRules: false), immediate: true);
    }
    public override void _ExitTree() { HoverInfoManager.HideAll(); }

    private partial class SpatialBoard : Control
    {
        public sealed class Sample(string name, string category, int width, int height, int x, int y, Texture2D texture)
        {
            public string Name = name, Category = category;
            public int Width = width, Height = height, X = x, Y = y;
            public bool Rotated;
            public Texture2D Texture = texture;
            public int W => Rotated ? Height : Width;
            public int H => Rotated ? Width : Height;
            public Rect2I Bounds => new(X, Y, W, H);
        }
        public int Columns { get; } = 15;
        public int Rows { get; } = 8;
        public string Category = "", Search = "";
        public List<Sample> Items = [];
        public Action<Sample>? SelectionChanged;
        public Action<Sample?>? HoverChanged;
        private Sample? _hovered;
        public Action? Changed;
        public Action<string>? Status;
        private const float Cell = 48;
        private Sample? _selected, _dragging;
        private Vector2I _offset, _candidate;
        private bool _candidateRotated;
        private readonly record struct Region(Rect2I Logical, Vector2 Screen);
        private static readonly Region[] Regions = [
            new(new Rect2I(0, 0, 6, 8), new Vector2(1040, 176)), // 背后
            new(new Rect2I(6, 0, 6, 4), new Vector2(456, 70)),  // 胸前
            new(new Rect2I(6, 4, 6, 4), new Vector2(456, 414)),  // 腰间
            new(new Rect2I(12, 0, 3, 4), new Vector2(184, 208)), // 左袖
            new(new Rect2I(12, 4, 3, 4), new Vector2(872, 208))  // 右袖
        ];
        private Vector2I PointToCell(Vector2 point)
        {
            foreach (var region in Regions)
                if (new Rect2(region.Screen, (Vector2)region.Logical.Size * Cell).HasPoint(point))
                    return region.Logical.Position + (Vector2I)((point - region.Screen) / Cell).Floor();
            return new(-1000, -1000);
        }
        private Vector2 CellToPoint(Vector2I cell)
        {
            foreach (var region in Regions)
                if (region.Logical.HasPoint(cell)) return region.Screen + (Vector2)(cell - region.Logical.Position) * Cell;
            return new(-10000, -10000);
        }
        private static bool WithinRegion(Rect2I box) => Regions.Any(region =>
            box.Position.X >= region.Logical.Position.X && box.Position.Y >= region.Logical.Position.Y
            && box.End.X <= region.Logical.End.X && box.End.Y <= region.Logical.End.Y);
        private bool Matches(Sample item) => (Category.Length == 0 || Category == item.Category)
            && (Search.Length == 0 || item.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        public void ResetSamples(string root)
        {
            CancelDrag(); Items.Clear();
            Texture2D Load(string file)
            {
                using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(root + file));
                if (image.IsEmpty() || image.GetPixel(0, 0).A > .01f) throw new InvalidOperationException("素材无法加载或透明边缘异常: " + file);
                return ImageTexture.CreateFromImage(image);
            }
            var stone = Load("spirit_stone_1x1.png"); var pill = Load("pill_2x2.png");
            var book = Load("manual_generic_2x3.png"); var sword = Load("slender_sword_2x6.png");
            var herb = Load("bitter_moss_root_2x4.png"); var ore = Load("xuantie_3x3.png");
            Items.Add(new("长剑", "器物", 2, 6, 0, 0, sword));
            Items.Add(new("秘籍", "秘籍", 2, 3, 2, 0, book));
            Items.Add(new("秘籍", "秘籍", 2, 3, 4, 0, book));
            Items.Add(new("苦苔根", "材料", 2, 4, 10, 0, herb));
            Items.Add(new("玄铁", "材料", 3, 3, 6, 4, ore));
            Items.Add(new("丹药", "丹药", 2, 2, 12, 0, pill));
            Items.Add(new("丹药", "丹药", 2, 2, 12, 4, pill));
            for (var i = 0; i < 4; i++) Items.Add(new("灵石", "材料", 1, 1, 6 + i, 0, stone));
            for (var i = 0; i < 4; i++) Items.Add(new("灵石", "材料", 1, 1, 14, i, stone));
            for (var i = 0; i < 4; i++) Items.Add(new("灵石", "材料", 1, 1, 14, 4 + i, stone));
            _selected = Items[0]; QueueRedraw();
        }
        public bool CanPlace(Sample item, int x, int y, bool rotated)
        {
            var w = rotated ? item.Height : item.Width; var h = rotated ? item.Width : item.Height;
            var box = new Rect2I(x, y, w, h);
            return x >= 0 && y >= 0 && x + w <= Columns && y + h <= Rows
                && WithinRegion(box)
                && Items.All(other => ReferenceEquals(item, other) || !box.Intersects(other.Bounds));
        }
        public void CancelDrag() { _dragging = null; _hovered = null; HoverInfoManager.HideAll(); QueueRedraw(); }
        public void RotateSelected()
        {
            var item = _dragging ?? _selected; if (item is null) return;
            if (_dragging is not null) { _candidateRotated = !_candidateRotated; QueueRedraw(); return; }
            if (!CanPlace(item, item.X, item.Y, !item.Rotated)) { Status?.Invoke("此处空间不足，无法旋转"); return; }
            item.Rotated = !item.Rotated; SelectionChanged?.Invoke(item); QueueRedraw();
        }
        public void Organize()
        {
            CancelDrag();
            var old = Items.Select(item => (item, item.X, item.Y, item.Rotated)).ToArray();
            var sorted = Items.OrderByDescending(item => item.Width * item.Height).ToList();
            Items = [];
            foreach (var item in sorted)
            {
                var found = false;
                for (var y = 0; y < Rows && !found; y++) for (var x = 0; x < Columns && !found; x++)
                    if (CanPlace(item, x, y, item.Rotated)) { item.X = x; item.Y = y; found = true; }
                if (!found)
                {
                    Items = old.Select(entry => entry.item).ToList();
                    foreach (var entry in old) { entry.item.X = entry.X; entry.item.Y = entry.Y; entry.item.Rotated = entry.Rotated; }
                    Status?.Invoke("未找到完整摆法，保留原有位置"); QueueRedraw(); return;
                }
                Items.Add(item);
            }
            Status?.Invoke("已整理行囊"); Changed?.Invoke(); QueueRedraw();
        }
        public override void _GuiInput(InputEvent input)
        {
            var point = PointToCell(GetLocalMousePosition());
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
            {
                if (mouse.Pressed)
                {
                    var hit = Items.LastOrDefault(item => item.Bounds.HasPoint(point));
                    if (hit is null || !Matches(hit)) return;
                    _selected = _dragging = hit; _offset = point - new Vector2I(hit.X, hit.Y);
                    _candidate = new(hit.X, hit.Y); _candidateRotated = hit.Rotated; SelectionChanged?.Invoke(hit);
                    QueueRedraw(); AcceptEvent();
                }
                else if (_dragging is { } dragged)
                {
                    _candidate = point - _offset;
                    if (CanPlace(dragged, _candidate.X, _candidate.Y, _candidateRotated))
                    { dragged.X = _candidate.X; dragged.Y = _candidate.Y; dragged.Rotated = _candidateRotated; Status?.Invoke("已摆放"); }
                    else Status?.Invoke("位置被占用或超出边界，已放回原处");
                    CancelDrag(); SelectionChanged?.Invoke(dragged); Changed?.Invoke(); AcceptEvent();
                }
            }
            else if (input is InputEventMouseMotion)
            {
                if (_dragging is not null) { HoverInfoManager.HideAll(); _candidate = point - _offset; QueueRedraw(); }
                else
                {
                    var hit = Items.LastOrDefault(item => item.Bounds.HasPoint(point) && Matches(item));
                    if (hit != _hovered) { _hovered = hit; HoverChanged?.Invoke(hit); }
                }
            }
        }
        public override void _Input(InputEvent input)
        {
            if (input is InputEventKey { Pressed: true, Echo: false } key)
            {
                if (key.Keycode == Key.R) { RotateSelected(); GetViewport().SetInputAsHandled(); }
                else if (key.Keycode == Key.Escape && _dragging is not null) { CancelDrag(); GetViewport().SetInputAsHandled(); }
            }
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } && _dragging is not null)
            {
                // A release outside this control still finishes or cancels the drag.
                _GuiInput(input);
            }
        }
        public override void _Draw()
        {
            foreach (var region in Regions)
            {
                var origin = region.Screen;
                var size = region.Logical.Size;
                var area = new Rect2(origin, (Vector2)size * Cell);
                DrawRect(area, new Color("f3eddc"));
                var faint = new Color(.35f, .38f, .30f, .15f);
                var strong = new Color(.35f, .38f, .30f, .28f);
                for (var x = 0; x <= size.X; x++) DrawLine(origin + new Vector2(x * Cell, 0), origin + new Vector2(x * Cell, size.Y * Cell), x % 4 == 0 ? strong : faint);
                for (var y = 0; y <= size.Y; y++) DrawLine(origin + new Vector2(0, y * Cell), origin + new Vector2(size.X * Cell, y * Cell), y % 4 == 0 ? strong : faint);
                DrawRect(area, new Color("90846b"), false, 1.3f);
            }
            foreach (var item in Items)
            {
                var rect = new Rect2(CellToPoint(new Vector2I(item.X, item.Y)), new Vector2(item.W, item.H) * Cell);
                var alpha = Matches(item) ? (_dragging == item ? .3f : 1f) : .17f;
                DrawRect(rect.Grow(-1), new Color(.45f, .48f, .36f, .07f * alpha));
                DrawRect(rect.Grow(-1), new Color(.31f, .39f, .32f, .45f * alpha), false);
                DrawItem(item, rect, item.Rotated, alpha);
                if (_selected == item) DrawRect(rect.Grow(-1), Teal, false, 2);
            }
            if (_dragging is { } drag)
            {
                var rect = new Rect2(CellToPoint(_candidate),
                    new Vector2(_candidateRotated ? drag.Height : drag.Width, _candidateRotated ? drag.Width : drag.Height) * Cell);
                var valid = CanPlace(drag, _candidate.X, _candidate.Y, _candidateRotated);
                DrawRect(rect, valid ? new Color(.2f, .5f, .37f, .22f) : new Color(.7f, .24f, .15f, .25f));
                DrawItem(drag, rect, _candidateRotated, .9f);
            }
        }
        private void DrawItem(Sample item, Rect2 rect, bool rotated, float alpha)
        {
            // Transparent margins belong to the packed PNG, so every cell uses exactly the same scale.
            var target = rotated ? new Vector2(rect.Size.Y, rect.Size.X) : rect.Size;
            var factor = Mathf.Min(target.X / item.Texture.GetWidth(), target.Y / item.Texture.GetHeight());
            var size = item.Texture.GetSize() * factor;
            DrawSetTransform(rect.GetCenter(), rotated ? Mathf.Pi / 2 : 0);
            DrawTextureRect(item.Texture, new Rect2(-size / 2, size), false, new Color(1, 1, 1, alpha));
            DrawSetTransform(Vector2.Zero);
        }
        public void Verify()
        {
            void Check(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
            Check(Items.Count == 19 && Items.Sum(item => item.Width * item.Height) == 65, "样本件数或占地错误");
            var sword = Items[0];
            Check(!CanPlace(sword, -1, 0, false), "负坐标未拦截");
            Check(!CanPlace(sword, Columns - 1, 0, false), "越界未拦截");
            Check(!CanPlace(Items[1], 0, 0, false), "与长剑重叠未拦截");
            Check(CanPlace(sword, 0, 0, false), "原位不合法");
            RotateSelected(); Check(!sword.Rotated, "重叠旋转未拒绝");
            Items = [sword];
            RotateSelected(); Check(sword.Rotated && sword.W == 6 && sword.H == 2, "旋转尺寸错误");
            ResetSamples(Assets);
            Check(Regions.Sum(region => region.Logical.Size.X * region.Logical.Size.Y) == 120, "分区总面积错误");
            var testItem = Items[1];
            Check(!WithinRegion(new Rect2I(5, 0, 2, 3)), "物品跨越背囊与胸前边界");
            Check(!WithinRegion(new Rect2I(6, 3, 2, 3)), "物品跨越胸前与腰间边界");
            Check(!WithinRegion(new Rect2I(12, 3, 1, 2)), "物品跨越两袖逻辑边界");
            Check(PointToCell(new Vector2(500, 300)).X < 0, "分区间留白被当作空格");
            foreach (var region in Regions)
            {
                var cell = region.Logical.Position;
                Check(PointToCell(CellToPoint(cell) + Vector2.One * 3) == cell, "分区坐标映射错误");
            }
            Items.Remove(testItem);
            Check(CanPlace(testItem, 8, 4, false) == false, "腰囊已占位置未拒绝");
            // Empty the destination to verify transfer without introducing a new item.
            var inventory = Items;
            Items = [testItem];
            Check(CanPlace(testItem, 6, 0, false) && CanPlace(testItem, 6, 4, false)
                && CanPlace(testItem, 12, 0, false) && CanPlace(testItem, 12, 4, false), "合法跨分区转移被拒绝");
            Check(!CanPlace(sword, 12, 0, false) && !CanPlace(sword, 12, 0, true), "袖袋尺寸边界失效");
            Items = inventory;
            ResetSamples(Assets);
            var before = Items.Sum(item => item.Width * item.Height);
            Organize(); Check(before == Items.Sum(item => item.Width * item.Height), "整理丢失样本");
            foreach (var item in Items) Check(CanPlace(item, item.X, item.Y, item.Rotated), "整理后重叠或越界");
            ResetSamples(Assets);
            Status?.Invoke("拖动物品摆放 · R旋转 · Esc取消");
        }
    }
}
