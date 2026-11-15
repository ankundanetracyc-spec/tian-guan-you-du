using Godot;
using Cell = FormationGridModel.Cell;
using Direction = FormationGridModel.Direction;
using Kind = FormationGridModel.Kind;
using PortKind = FormationGridModel.PortKind;

/// <summary>局外阵图画布。只编辑几何草案，不创建器物或启动战斗效果。</summary>
public partial class FormationBoardCanvas : Control
{
    // Connect remains as a compatibility value for older in-project smoke fixtures.
    public enum EditTool { Select, Connect, Signal, Energy, Erase, Component }
    private FormationGridModel _model = FormationGridModel.CreateStarter();
    private EditTool _tool = EditTool.Select;
    private Kind _kind = Kind.Sensor;
    private int _rotation;
    private Cell? _hover;
    private Cell? _lastPaint;
    private string? _selected;
    private string? _moving;
    private Cell _grabOffset;
    private bool _painting;
    private bool _rightPainting;
    private Cell? _wirePortStart;
    private FormationGridModel.Port? _wireStartPort;
    private PortKind _wireKind = PortKind.Signal;
    private Direction _wirePortFacing;
    private Vector2 _wirePointerStart;
    private Vector2 _pointer;
    private readonly System.Collections.Generic.Dictionary<string, Texture2D> _textures = [];
    private sealed record ArtPort(PortKind Kind, Direction Side, bool Input, Vector2 Boundary, Vector2 MouthUv, Vector2 CoreUv, float Width, float Bridge,
        Vector2? BridgeFrom = null, Vector2? BridgeTo = null, float? BridgeWidth = null);
    private sealed record LogicArt(Vector2 Offset, ArtPort[] Ports);
    private readonly System.Collections.Generic.Dictionary<Kind, LogicArt> _logicArt = [];
    private readonly System.Collections.Generic.Dictionary<string, string> _controlReadouts = [];

    public FormationGridModel Model => _model;
    public bool HasPersistentLabels => false;
    public bool GridVisible => true;
    public bool InteractionInProgress => _painting || _moving is not null || _wirePortStart is not null;
    public EditTool Tool => _tool;
    public string? SelectedId => _selected;
    public string? GetControlReadout(string id) => _controlReadouts.TryGetValue(id, out var value) ? value : null;
    public Cell? SelectedWireCell { get; private set; }
    public string DescribeSelected() => _selected is string id && _model.Components.TryGetValue(id, out var component)
        ? Describe(component) : "点选部件改参数 · 拖动移动 · R旋转 · 右键取下";
    public bool GuidesVisible => _painting || _moving is not null ||
        (_hover.HasValue && _tool is EditTool.Connect or EditTool.Signal or EditTool.Energy or EditTool.Erase or EditTool.Component);
    public const string ArtDirectory = "res://docs/美术规范/铜纹阵盘基础元件_20261108/";
    public const string BoardArtPath = "res://docs/美术规范/守攻阵铜纹素材_20261109/board_base.png";
    public const string LogicArtDirectory = "res://docs/美术规范/铜纹阵盘逻辑元件_20261108/";
    public const string ControlArtDirectory = "res://docs/美术规范/守攻阵铜纹素材_20261109/";
    public const float WorkingFraction = 512f / 832f;
    public event Action<string>? Edited;
    public event Action<string>? SelectionChanged;

    public override void _Ready()
    {
        LoadArtCatalog(ArtDirectory); LoadArtCatalog(LogicArtDirectory); LoadArtCatalog(ControlArtDirectory);
        if (_logicArt.TryGetValue(Kind.Ward, out var guardArt)) _logicArt[Kind.Guard] = guardArt;
        CustomMinimumSize = new Vector2(390, 390);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        TextureRepeat = TextureRepeatEnum.Enabled;
        TextureFilter = TextureFilterEnum.Linear;
        MouseExited += () => { SetHoverText(""); if (!_painting && _moving is null) { _hover = null; QueueRedraw(); } };
        Resized += QueueRedraw;
        SetProcess(false);
    }

    public void SetModel(FormationGridModel model)
    {
        HoverInfoManager.SetDragInProgress(this, false); SetHoverText("");
        _model = model; _selected = null; SelectedWireCell = null; _moving = null; _hover = null; _lastPaint = null; _wirePortStart = null; _wireStartPort = null; _controlReadouts.Clear();
        ClearFlow(); SetProblemMarkers(null, null);
        SelectionChanged?.Invoke("草图已载入，点选部件可编辑参数。");
        QueueRedraw();
    }

    public void SetControlReadouts(System.Collections.Generic.IReadOnlyDictionary<string, string>? values)
    {
        _controlReadouts.Clear();
        if (values is not null) foreach (var (id, value) in values) _controlReadouts[id] = value;
        if (IsInsideTree()) UpdateHoverText();
    }

    public void SetTool(EditTool tool, Kind kind = Kind.Sensor)
    {
        HoverInfoManager.SetDragInProgress(this, false); SetHoverText("");
        if (tool == EditTool.Component && kind != _kind) _rotation = 0;
        _tool = tool; _kind = kind; _moving = null; _painting = false; _lastPaint = null; _wirePortStart = null; _wireStartPort = null; _hover = null;
        QueueRedraw();
    }

    public void RotateSelection()
    {
        if (_tool == EditTool.Component) { _rotation = (_rotation + 1) % 4; QueueRedraw(); SelectionChanged?.Invoke($"{PartName(_kind)} · 已旋转{_rotation * 90}°"); return; }
        if (_selected is null) { SelectionChanged?.Invoke("先选择一件部件，再旋转。"); return; }
        var changed = _model.TryRotateComponent(_selected);
        Changed(changed, "已旋转部件。");
    }

    public static string PartName(Kind kind) => kind switch
    {
        Kind.Source => "阵源", Kind.Sensor => "术法感应", Kind.Ward => "护持",
        Kind.Counter => "计数器", Kind.Filter => "筛选器", Kind.Rhythm => "计数器", Kind.Split => "分流器", Kind.Merge => "合流器",
        Kind.StateInput => "状态输入", Kind.Compare => "比较器", Kind.Not => "取反", Kind.Switch => "开关", Kind.Fire => "火击", Kind.Guard => "护罩",
        Kind.And => "与", Kind.Or => "或", _ => "部件"
    };
    public static string StateInputName(string key) => key switch
    {
        "health" => "气血感应", "shield" => "护盾感应", "mana" => "法力感应", "energy" => "阵能感应", _ => "状态感应"
    };
    public static Vector2I PartShape(Kind kind) => kind switch
    {
        Kind.Source or Kind.Counter or Kind.Rhythm or Kind.Compare => new Vector2I(2, 1),
        Kind.Ward or Kind.Guard or Kind.Fire => new Vector2I(1, 2), _ => Vector2I.One
    };
    public static bool IsLogicPart(Kind kind) => kind is Kind.Counter or Kind.Filter or Kind.Rhythm or Kind.Split or Kind.Merge;
    public static string ArtPath(Kind kind) => (kind is Kind.StateInput or Kind.Compare or Kind.Not or Kind.Switch or Kind.Fire ? ControlArtDirectory : IsLogicPart(kind) ? LogicArtDirectory : ArtDirectory) + (kind switch
    {
        Kind.Source => "source_2x1.png", Kind.Sensor => "sensor_1x1.png",
        Kind.Ward => "ward_1x2.png", Kind.Counter => "counter_2x1.png", Kind.Filter => "filter_1x1.png",
        Kind.Rhythm => "counter_2x1.png", Kind.Split => "split_1x1.png", Kind.Merge => "merge_1x1.png",
        Kind.StateInput => "state_1x1.png", Kind.Compare => "compare_2x1.png", Kind.Not => "invert_1x1.png", Kind.Switch => "gate_1x1.png",
        Kind.Fire => "fire_1x2.png", Kind.Guard => "ward_1x2.png", _ => ""
    });
    private void LoadArtCatalog(string directory)
    {
        var path = directory + "components.json";
        if (!Godot.FileAccess.FileExists(path)) return;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
            foreach (var item in document.RootElement.GetProperty("components").EnumerateArray())
            {
                Kind? mapped = item.GetProperty("id").GetString() switch
                {
                    "source" => Kind.Source, "sensor" => Kind.Sensor, "ward" => Kind.Ward,
                    "counter" => Kind.Counter, "rhythm" => Kind.Rhythm, "filter" => Kind.Filter,
                    "split" => Kind.Split, "merge" => Kind.Merge, "state" => Kind.StateInput, "compare" => Kind.Compare,
                    "invert" => Kind.Not, "gate" => Kind.Switch, "fire" => Kind.Fire, _ => null
                };
                if (mapped is not Kind kind) continue;
                static Vector2 Pair(System.Text.Json.JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle());
                var ports = item.GetProperty("ports").EnumerateArray().Select(port => new ArtPort(
                    port.GetProperty("kind").GetString() == "energy" ? PortKind.Energy : PortKind.Signal,
                    port.GetProperty("side").GetString() switch
                    {
                        "north" => Direction.North, "east" => Direction.East,
                        "south" => Direction.South, _ => Direction.West
                    }, port.GetProperty("direction").GetString() == "input", Pair(port.GetProperty("logical_boundary")),
                    Pair(port.TryGetProperty("mouth_uv", out var mouth) ? mouth : port.GetProperty("color_mouth_uv")), Pair(port.GetProperty("slot_center_uv")),
                    (port.TryGetProperty("slot_inner_width_at_128", out var width) ? width : port.GetProperty("slot_inner_width")).GetSingle(),
                    port.TryGetProperty("overlay_bridge", out var bridge) ? bridge.GetSingle() : 0,
                    port.TryGetProperty("overlay_bridge_segment_128", out var segment) ? Pair(segment.GetProperty("from")) : null,
                    port.TryGetProperty("overlay_bridge_segment_128", out segment) ? Pair(segment.GetProperty("to")) : null,
                    port.TryGetProperty("overlay_bridge_segment_128", out segment) ? segment.GetProperty("width").GetSingle() : null)).ToArray();
                var offset = item.TryGetProperty("art_offset_128", out var artOffset) ? Pair(artOffset) : kind switch
                { Kind.Source => new Vector2(0, .4352f), Kind.Sensor => new Vector2(0, -.7168f), Kind.Ward => new Vector2(-.0768f, 0), _ => Vector2.Zero };
                _logicArt[kind] = new LogicArt(offset, ports);
            }
            if (_logicArt.TryGetValue(Kind.Counter, out var counterArt)) _logicArt[Kind.Rhythm] = counterArt;
        }
        catch (System.Text.Json.JsonException) { }
    }
    private Texture2D? Texture(string path)
    {
        if (_textures.TryGetValue(path, out var texture)) return texture;
        texture = LoadArt(path);
        if (texture is not null) _textures[path] = texture;
        return texture;
    }
    public static Texture2D? LoadArt(string path)
    {
        if (Godot.FileAccess.FileExists(path + ".import") && ResourceLoader.Exists(path)) return GD.Load<Texture2D>(path);
        if (!Godot.FileAccess.FileExists(path)) return null;
        using var image = Image.LoadFromFile(path);
        return image is not null && !image.IsEmpty() ? ImageTexture.CreateFromImage(image) : null;
    }

    private Rect2 BoardRect()
    {
        var side = Mathf.Min(Size.X, Size.Y);
        return new Rect2((Size - Vector2.One * side) / 2, Vector2.One * side);
    }
    public Rect2 GridRect
    {
        get
        {
            var board = BoardRect();
            return new Rect2(board.Position + board.Size * ((1f - WorkingFraction) / 2), board.Size * WorkingFraction);
        }
    }
    public Vector2 CellPosition(Cell cell) => GridRect.Position + new Vector2(cell.X + .5f, cell.Y + .5f) * (GridRect.Size.X / _model.BoardSize);
    private Cell? CellAt(Vector2 position)
    {
        var grid = GridRect;
        if (!grid.HasPoint(position)) return null;
        var local = (position - grid.Position) / (grid.Size.X / _model.BoardSize);
        return new Cell(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.R) { RotateSelection(); AcceptEvent(); }
            else if (key.Keycode is Key.Delete or Key.Backspace && _selected is not null)
            {
                var changed = _model.RemoveComponent(_selected);
                Changed(changed, "已取下部件，邻接线路重新检查。");
                if (changed) _selected = null;
                AcceptEvent();
            }
            return;
        }
        if (input is InputEventMouseMotion motion)
        {
            _pointer = motion.Position;
            _hover = CellAt(motion.Position);
            if (_wirePortStart is not null && !_painting && (motion.ButtonMask & MouseButtonMask.Left) != 0 &&
                motion.Position.DistanceTo(_wirePointerStart) >= Mathf.Max(5, GridRect.Size.X / _model.BoardSize * .12f))
                _painting = true;
            if (_painting && _hover is Cell current) PaintTo(current, _rightPainting);
            UpdateHoverText(); QueueRedraw();
            return;
        }
        if (input is not InputEventMouseButton mouse || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
        _pointer = mouse.Position;
        GrabFocus(); _hover = CellAt(mouse.Position);
        if (mouse.Pressed)
        {
            if (_hover is not Cell cell) return;
            if (mouse.ButtonIndex == MouseButton.Right || _tool == EditTool.Erase)
            {
                _painting = true; _rightPainting = true; _lastPaint = null; PaintTo(cell, true);
            }
            else if (_tool is EditTool.Connect or EditTool.Signal or EditTool.Energy)
            {
                var requestedWireKind = _tool switch
                {
                    EditTool.Signal => PortKind.Signal,
                    EditTool.Energy => PortKind.Energy,
                    _ => (PortKind?)null
                };
                if (TryWireStart(mouse.Position, requestedWireKind, out var port))
                { _painting = false; _rightPainting = false; _wireStartPort = port; _wireKind = port.Kind; _wirePortStart = port.Cell; _wirePortFacing = port.Facing; _wirePointerStart = mouse.Position; _lastPaint = null; }
                else if (TryInspectPort(mouse.Position, out var portComponent))
                { _selected = portComponent.Id; SelectedWireCell = null; SelectionChanged?.Invoke(Describe(portComponent)); }
                else if (_model.Wires.TryGetValue(cell, out var existing) && (requestedWireKind is null || existing == requestedWireKind))
                { _selected = null; SelectedWireCell = cell; _wireStartPort = null; _wireKind = existing; SelectionChanged?.Invoke(existing == PortKind.Signal ? "信号线" : "供能线"); _painting = true; _rightPainting = false; _wirePortStart = null; _lastPaint = cell; }
                else if (_model.Wires.TryGetValue(cell, out existing))
                { _selected = null; SelectedWireCell = cell; SelectionChanged?.Invoke(existing == PortKind.Signal ? "信号线" : "供能线"); }
                else if (_model.GetComponentAt(cell) is { } picked)
                { _selected = picked.Id; SelectedWireCell = null; SelectionChanged?.Invoke(Describe(picked)); }
                else Edited?.Invoke(requestedWireKind == PortKind.Energy ? "请从亮起的供能接口或供能线开始。" : "请从亮起的信号接口或信号线开始。");
            }
            else if (_tool == EditTool.Component)
                Changed(_model.TryPlaceComponent(_kind, cell, _rotation), "已放置" + PartName(_kind) + "。");
            else
            {
                var component = _model.GetComponentAt(cell);
                _selected = component?.Id;
                SelectedWireCell = component is null && _model.Wires.ContainsKey(cell) ? cell : null;
                if (component is not null)
                {
                    _moving = component.Id;
                    _grabOffset = new Cell(cell.X - component.Origin.X, cell.Y - component.Origin.Y);
                    SelectionChanged?.Invoke(Describe(component));
                }
                else if (_model.Wires.TryGetValue(cell, out var network))
                    SelectionChanged?.Invoke(network == PortKind.Signal ? "信号线" : "供能线");
            }
        }
        else
        {
            var finishWireStroke = _painting && !_rightPainting && _tool is EditTool.Connect or EditTool.Signal or EditTool.Energy;
            var clickedPortComponent = !_painting && _wireStartPort is FormationGridModel.Port pendingPort ? _model.GetComponentAt(pendingPort.Cell) : null;
            if (_moving is not null && _hover is Cell target)
            {
                var origin = new Cell(target.X - _grabOffset.X, target.Y - _grabOffset.Y);
                if (_model.Components.TryGetValue(_moving, out var moving) && moving.Origin != origin)
                    Changed(_model.TryMoveComponent(_moving, origin), "已移动部件，邻接线路重新检查。");
            }
            _painting = false; _rightPainting = false; _moving = null; _lastPaint = null; _wirePortStart = null; _wireStartPort = null;
            if (clickedPortComponent is not null)
            {
                _selected = clickedPortComponent.Id; SelectedWireCell = null;
                SelectionChanged?.Invoke(Describe(clickedPortComponent));
            }
            if (finishWireStroke && _model.RemoveIncompleteWireRegions(out var removed))
            {
                if (SelectedWireCell is Cell selectedWire && removed.Contains(selectedWire)) SelectedWireCell = null;
                SelectionChanged?.Invoke("未接通的线路已清除。");
                Edited?.Invoke("未接通的线路已清除。");
            }
        }
        HoverInfoManager.SetDragInProgress(this, _painting || _moving is not null);
        UpdateHoverText(); QueueRedraw(); AcceptEvent();
    }

    private void PaintTo(Cell target, bool erase)
    {
        if (!erase && _wirePortStart is Cell start)
        {
            var step = _wirePortFacing switch { Direction.North => new Cell(start.X, start.Y - 1), Direction.East => new Cell(start.X + 1, start.Y), Direction.South => new Cell(start.X, start.Y + 1), _ => new Cell(start.X - 1, start.Y) };
            if (target == start) return;
            var validDirection = _wirePortFacing switch { Direction.North => target.Y < start.Y, Direction.East => target.X > start.X, Direction.South => target.Y > start.Y, _ => target.X < start.X };
            if (!validDirection) return;
            _wirePortStart = null; _lastPaint = null;
            if (!Paint(step, false)) return;
        }
        var current = _lastPaint ?? target;
        if (!Paint(current, erase)) return;
        while (current != target)
        {
            var dx = target.X - current.X; var dy = target.Y - current.Y;
            current = Math.Abs(dx) >= Math.Abs(dy) && dx != 0
                ? new Cell(current.X + Math.Sign(dx), current.Y) : new Cell(current.X, current.Y + Math.Sign(dy));
            if (!Paint(current, erase)) return;
        }
        _lastPaint = current;
    }
    private bool Paint(Cell cell, bool erase)
    {
        bool changed;
        if (erase)
        {
            var component = _model.GetComponentAt(cell);
            changed = component is not null ? _model.RemoveComponent(component.Id) : _model.RemoveWireAndDisconnected(cell, out _);
            if (!changed && component is null && !_model.Wires.ContainsKey(cell)) return true;
        }
        else changed = _model.TryPlaceWire(cell, _wireKind);
        Changed(changed, erase ? "已擦除，邻接线路重新检查。" : "已铺线，形状随连接更新。");
        if (changed) _lastPaint = cell;
        return changed;
    }
    private bool TryWireStart(Vector2 position, PortKind? requestedKind, out FormationGridModel.Port chosen)
    {
        var unit = GridRect.Size.X / _model.BoardSize;
        var candidates = _model.Components.Values.SelectMany(component => component.Ports
            .Where(port => (requestedKind is null || port.Kind == requestedKind) && (_model.GetConnectionMask(port.Cell) & port.Facing) == 0).Select(port =>
        {
            var axis = port.Facing switch { Direction.North => Vector2.Up, Direction.East => Vector2.Right, Direction.South => Vector2.Down, _ => Vector2.Left };
            var boundary = CellPosition(port.Cell) + axis * unit / 2;
            return (port, distance: Math.Min(position.DistanceSquaredTo(boundary), position.DistanceSquaredTo(DisplayPortPoint(component, port, boundary))));
        })).OrderBy(item => item.distance).ToArray();
        if (candidates.Length > 0 && candidates[0].distance <= Mathf.Pow(unit * .24f, 2)) { chosen = candidates[0].port; return true; }
        chosen = default; return false;
    }
    private bool TryInspectPort(Vector2 position, out FormationGridModel.Component chosen)
    {
        var unit = GridRect.Size.X / _model.BoardSize;
        var candidates = _model.Components.Values.SelectMany(component => component.Ports.Select(port =>
        {
            var axis = port.Facing switch { Direction.North => Vector2.Up, Direction.East => Vector2.Right, Direction.South => Vector2.Down, _ => Vector2.Left };
            var boundary = CellPosition(port.Cell) + axis * unit / 2;
            return (component, distance: Math.Min(position.DistanceSquaredTo(boundary), position.DistanceSquaredTo(DisplayPortPoint(component, port, boundary))));
        })).OrderBy(item => item.distance).ToArray();
        if (candidates.Length > 0 && candidates[0].distance <= Mathf.Pow(unit * .24f, 2)) { chosen = candidates[0].component; return true; }
        chosen = null!; return false;
    }
    private void Changed(bool success, string message)
    {
        Edited?.Invoke(success ? message : _model.LastError);
        QueueRedraw();
    }
    private void UpdateHoverText()
    {
        if (_hover is not Cell cell || _painting || _moving is not null || HoverInfoManager.IsDragInProgress() || GetViewport().GuiIsDragging()) { SetHoverText(""); return; }
        var unit = GridRect.Size.X / _model.BoardSize;
        var nearest = _model.Components.Values.SelectMany(component => component.Ports.Select(port =>
        {
            var axis = port.Facing switch
            {
                Direction.North => Vector2.Up, Direction.East => Vector2.Right,
                Direction.South => Vector2.Down, Direction.West => Vector2.Left, _ => Vector2.Zero
            };
            var logical = CellPosition(port.Cell) + axis * unit / 2;
            var point = DisplayPortPoint(component, port, logical);
            return (component, port, distance: Math.Min(_pointer.DistanceSquaredTo(logical), _pointer.DistanceSquaredTo(point)));
        })).OrderBy(item => item.distance).FirstOrDefault();
        if (nearest.component is not null && nearest.distance < Mathf.Pow(Mathf.Max(7, unit * .13f), 2))
        {
            var connected = (_model.GetConnectionMask(nearest.port.Cell) & nearest.port.Facing) != 0;
            SetHoverText(PartName(nearest.component.Kind) + " · " +
                (nearest.port.Kind == PortKind.Signal ? SignalCaption(nearest.port.SignalType) + (nearest.port.Unit == FormationValueUnit.None ? "" : " · " + UnitName(nearest.port.Unit)) : "供能") + (nearest.port.IsInput ? "输入" : "输出") +
                (connected ? " · 已接" : " · 未接"));
            return;
        }
        var component = _model.GetComponentAt(cell);
        SetHoverText(component is not null ? Describe(component)
            : _model.Wires.TryGetValue(cell, out var network) ? (network == PortKind.Signal ? "信号线" : "供能线") + $" · ({cell.X + 1},{cell.Y + 1})"
            : GuidesVisible ? $"落点 · ({cell.X + 1},{cell.Y + 1})" : "");
    }
    private void SetHoverText(string text) => HoverInfoManager.HideFor(this);
    public override void _ExitTree() { HoverInfoManager.SetDragInProgress(this, false); HoverInfoManager.HideFor(this); }
    private string Describe(FormationGridModel.Component component) =>
        PartName(component.Kind) + $" · {component.Width}×{component.Height}" + (component.Kind switch
        {
            Kind.Counter or Kind.Rhythm => $" · N={component.Settings.Interval} · {(component.Settings.FirstImmediate ? "先发一次" : "数满再发")}",
            Kind.Filter => $" · 幅值≥{component.Settings.MinimumAmplitude}",
            Kind.Sensor => " · " + FormationWorkbench.EventDisplay(component.Settings.InputEvent),
            Kind.StateInput => " · " + StateInputName(component.Settings.StateKey),
            Kind.Compare => component.Settings.Comparison == FormationControlCircuit.CompareMode.Band ? $" · ≤{component.Settings.Low}开／≥{component.Settings.High}关" : $" · {(component.Settings.Comparison == FormationControlCircuit.CompareMode.AtLeast ? "≥" : "≤")}{component.Settings.Low}",
            Kind.Switch => component.Settings.NormallyClosed ? " · 关时放行" : " · 开时放行",
            Kind.Split => " · " + SignalCaption(component.Settings.SignalType),
            Kind.Guard => " · 持续防护", Kind.Fire => " · 事件触发", _ => ""
        }) + "\n" + (component.Kind is Kind.Sensor or Kind.StateInput ? "感应来源固定 · 拖动移动 · R旋转" : "点选改参数 · 拖动移动 · R旋转") +
        (_controlReadouts.TryGetValue(component.Id, out var value) ? "\n试演：" + value : "");

    public static string SignalCaption(FormationSignalType type) => type == FormationSignalType.Number ? "数值" : type == FormationSignalType.Switch ? "开关" : "事件";
    public static string UnitName(FormationValueUnit unit) => unit switch { FormationValueUnit.Percent => "百分比", FormationValueUnit.Shield => "护盾", FormationValueUnit.Mana => "法力", FormationValueUnit.Energy => "阵能", _ => "次数" };
    public static string StateName(string key) => key switch { "health" => "气血比例", "shield" => "护盾", "mana" => "法力", "energy" => "阵能", _ => key };

    public override void _Draw()
    {
        var board = BoardRect(); var grid = GridRect; var unit = grid.Size.X / _model.BoardSize;
        var editing = GuidesVisible;
        var background = Texture(BoardArtPath);
        if (background is not null) DrawTextureRect(background, board, false);
        else DrawCircle(board.GetCenter(), board.Size.X * .48f, new Color("333d42"), true, -1, true);
        if (GridVisible) for (var i = 0; i <= _model.BoardSize; i++)
        {
            var color = new Color(.86f, .81f, .70f, .34f);
            DrawLine(grid.Position + new Vector2(i * unit, 0), grid.Position + new Vector2(i * unit, grid.Size.Y), color, 1, true);
            DrawLine(grid.Position + new Vector2(0, i * unit), grid.Position + new Vector2(grid.Size.X, i * unit), color, 1, true);
        }
        if (GridVisible) DrawCoordinateNumbers(grid, unit);
        FormationTraceRenderer.Draw(this, _model, grid, editing);
        DrawArtConnections(false);
        foreach (var component in _model.Components.Values)
        {
            DrawComponent(component, false);
            if (_selected == component.Id)
            {
                var rect = new Rect2(grid.Position + new Vector2(component.Origin.X, component.Origin.Y) * unit, new Vector2(component.Width, component.Height) * unit).Grow(-3);
                DrawRect(rect, new Color("c9c0a2"), false, 1.5f);
            }
        }
        DrawArtConnections(true);
        DrawFlowAndProblems();
        DrawConnectionGuides();
        DrawDropPreview();
        if (editing && _hover is Cell hover)
        {
            var rect = new Rect2(grid.Position + new Vector2(hover.X, hover.Y) * unit, Vector2.One * unit);
            DrawRect(rect.Grow(-1), new Color(1, 1, .9f, .09f));
            if (_tool == EditTool.Component)
                DrawComponent(new FormationGridModel.Component("preview", _kind, hover, _rotation), true);
            else if (_moving is not null && _model.Components.TryGetValue(_moving, out var moving))
            {
                var origin = new Cell(hover.X - _grabOffset.X, hover.Y - _grabOffset.Y);
                if (origin != moving.Origin) DrawComponent(moving with { Origin = origin }, true);
            }
        }
    }

    private void DrawConnectionGuides()
    {
        if (_tool is not (EditTool.Connect or EditTool.Signal or EditTool.Energy) || _rightPainting) return;
        var grid = GridRect; var unit = grid.Size.X / _model.BoardSize;
        var anchor = _wireStartPort;
        PortKind? activeKind = _tool switch
        {
            EditTool.Signal => PortKind.Signal,
            EditTool.Energy => PortKind.Energy,
            EditTool.Connect when _painting || anchor is not null => _wireKind,
            _ => null
        };
        foreach (var component in _model.Components.Values)
        foreach (var port in component.Ports)
        {
            if (activeKind is PortKind selectedKind && port.Kind != selectedKind) continue;
            var connected = (_model.GetConnectionMask(port.Cell) & port.Facing) != 0;
            var isAnchor = anchor is FormationGridModel.Port selected && selected == port;
            var compatible = activeKind is null || port.Kind == activeKind;
            compatible = compatible && (anchor is FormationGridModel.Port start
                ? port.Kind == start.Kind && port.IsInput != start.IsInput &&
                    (port.Kind != PortKind.Signal || port.SignalType == start.SignalType && port.Unit == start.Unit)
                : true);
            if (connected && !isAnchor || !compatible && !isAnchor) continue;
            var direction = port.Facing switch
            {
                Direction.North => Vector2.Up, Direction.East => Vector2.Right,
                Direction.South => Vector2.Down, _ => Vector2.Left
            };
            var point = DisplayPortPoint(component, port, CellPosition(port.Cell) + direction * unit / 2);
            var color = isAnchor ? new Color(.91f, .86f, .62f) : port.Kind == PortKind.Signal
                ? new Color(.88f, .43f, .33f) : new Color(.91f, .77f, .43f);
            DrawCircle(point, Mathf.Max(5, unit * (isAnchor ? .13f : .10f)), new Color(color, .20f));
            DrawArc(point, Mathf.Max(5, unit * (isAnchor ? .13f : .10f)), 0, Mathf.Tau, 28, color, isAnchor ? 3 : 2, true);
            DrawCircle(point, Mathf.Max(2, unit * .035f), color);
        }
        foreach (var (cell, kind) in _model.Wires)
        {
            if (activeKind is PortKind selectedKind && kind != selectedKind) continue;
            if (_painting && anchor is null && kind != _wireKind) continue;
            var color = kind == PortKind.Signal ? new Color(.88f, .43f, .33f) : new Color(.91f, .77f, .43f);
            var rect = new Rect2(grid.Position + new Vector2(cell.X, cell.Y) * unit, Vector2.One * unit).Grow(-unit * .29f);
            DrawRect(rect, new Color(color, .10f));
            DrawRect(rect, new Color(color, .85f), false, 2, true);
        }
    }

    private void DrawComponent(FormationGridModel.Component component, bool ghost)
    {
        var grid = GridRect; var unit = grid.Size.X / _model.BoardSize;
        var position = grid.Position + new Vector2(component.Origin.X, component.Origin.Y) * unit;
        var size = new Vector2(component.Width, component.Height) * unit;
        var rect = new Rect2(position, size).Grow(-5);
        var valid = component.Cells.All(cell => cell.X >= 0 && cell.Y >= 0 && cell.X < _model.BoardSize && cell.Y < _model.BoardSize &&
            (!_model.Wires.ContainsKey(cell) && (_model.GetComponentAt(cell) is not { } at || at.Id == component.Id)));
        var tint = ghost ? (valid ? new Color(1, 1, 1, .55f) : new Color(1, .45f, .4f, .5f)) : Colors.White;
        if (Texture(ArtPath(component.Kind)) is Texture2D texture)
        {
            var original = PartShape(component.Kind);
            DrawSetTransform(position + size / 2, component.Rotation * Mathf.Pi / 2);
            var precise = _logicArt.TryGetValue(component.Kind, out var art);
            var spriteSize = precise ? new Vector2(original.X, original.Y) * unit : new Vector2(original.X * unit - 10, original.Y * unit - 10);
            var offset = precise ? art!.Offset * (unit / 128f) : Vector2.Zero;
            DrawTextureRect(texture, new Rect2(-spriteSize / 2 + offset, spriteSize), false, tint);
            DrawSetTransform(Vector2.Zero);
        }
        else
        {
            DrawRect(rect, new Color(.25f, .32f, .27f, ghost ? .5f : 1));
            DrawRect(rect, new Color("9d9678"), false, 1);
        }
    }
    private static Direction RotatedSide(Direction side, int rotation) =>
        (Direction)((((int)side << rotation) | ((int)side >> (4 - rotation))) & 15);
    private Vector2 ArtPoint(FormationGridModel.Component component, Vector2 point, bool includeOffset)
    {
        var grid = GridRect; var unit = grid.Size.X / _model.BoardSize;
        var original = PartShape(component.Kind);
        var size = new Vector2(component.Width, component.Height) * unit;
        var center = grid.Position + new Vector2(component.Origin.X, component.Origin.Y) * unit + size / 2;
        var offset = includeOffset && _logicArt.TryGetValue(component.Kind, out var art) ? art.Offset : Vector2.Zero;
        var local = (point + offset) * (unit / 128f) - new Vector2(original.X, original.Y) * unit / 2;
        return center + local.Rotated(component.Rotation * Mathf.Pi / 2);
    }
    private Vector2 DisplayPortPoint(FormationGridModel.Component component, FormationGridModel.Port port, Vector2 fallback)
    {
        if (!_logicArt.TryGetValue(component.Kind, out var art)) return fallback;
        var match = art.Ports.FirstOrDefault(item => item.Kind == port.Kind && item.Input == port.IsInput && RotatedSide(item.Side, component.Rotation) == port.Facing);
        if (match is null) return fallback;
        var shape = PartShape(component.Kind);
        return ArtPoint(component, match.CoreUv * new Vector2(shape.X, shape.Y) * 128, true);
    }
    private void DrawArtConnections(bool aboveImage)
    {
        var unit = GridRect.Size.X / _model.BoardSize;
        var artScale = unit / 128f;
        foreach (var component in _model.Components.Values)
        {
            if (!_logicArt.TryGetValue(component.Kind, out var art)) continue;
            var shape = PartShape(component.Kind);
            foreach (var item in art.Ports)
            {
                var side = RotatedSide(item.Side, component.Rotation);
                var matches = component.Ports.Where(port => port.Kind == item.Kind && port.IsInput == item.Input && port.Facing == side).ToArray();
                if (matches.Length == 0) continue;
                var actual = matches[0]; if ((_model.GetConnectionMask(actual.Cell) & side) == 0) continue;
                var boundary = ArtPoint(component, item.Boundary, false);
                var mouth = ArtPoint(component, item.MouthUv * new Vector2(shape.X, shape.Y) * 128, true);
                var core = ArtPoint(component, item.CoreUv * new Vector2(shape.X, shape.Y) * 128, true);
                var main = FormationTraceRenderer.StrokeWidth(item.Kind, unit);
                var narrow = Mathf.Min(main, item.Width * artScale);
                var material = new Color(item.Kind == PortKind.Signal ? "bb6652" : "ceb787");
                if (aboveImage)
                {
                    var inward = (core - mouth).Normalized();
                    if (item.BridgeFrom is Vector2 from && item.BridgeTo is Vector2 to)
                    {
                        var start = ArtPoint(component, from, false); var end = ArtPoint(component, to, false);
                        DrawLine(start, end, material, (item.BridgeWidth ?? item.Width) * artScale, true);
                        DrawConnectionBrush(start, end, item.Kind, narrow, artScale);
                    }
                    else if (IsLogicPart(component.Kind))
                    {
                        var start = mouth - inward * 2 * artScale;
                        DrawLine(start, core, material, Mathf.Max(.7f, narrow * .88f), true);
                        DrawConnectionBrush(start, core, item.Kind, narrow * .88f, artScale);
                    }
                    else if (item.Bridge > 0)
                    {
                        var end = mouth + inward * Mathf.Min(item.Bridge * artScale, mouth.DistanceTo(core));
                        DrawLine(mouth, end, material, Mathf.Max(.7f, narrow - .3f * artScale), true);
                        DrawConnectionBrush(mouth, end, item.Kind, narrow, artScale);
                    }
                }
                else
                {
                    DrawTaper(boundary, mouth, main + 3 * artScale, narrow + 1.2f * artScale, new Color("51463d"));
                    DrawTaper(mouth, core, narrow + 1.2f * artScale, narrow + 1.2f * artScale, new Color("51463d"));
                    DrawTaper(boundary, mouth, main, narrow, material); DrawTaper(mouth, core, narrow, narrow, material);
                    DrawConnectionBrush(boundary, mouth, item.Kind, narrow, artScale); DrawConnectionBrush(mouth, core, item.Kind, narrow, artScale);
                }
            }
        }
    }
    private void DrawConnectionBrush(Vector2 start, Vector2 end, PortKind kind, float width, float scale)
    {
        var offset = new Vector2(-.45f, -.65f) * scale;
        DrawLine(start + offset, end + offset, new Color(kind == PortKind.Signal ? "e0a18b55" : "ead9ad70"), Mathf.Max(.55f, width * .17f), true);
    }
    private void DrawTaper(Vector2 start, Vector2 end, float startWidth, float endWidth, Color color)
    {
        if (start.DistanceSquaredTo(end) < .0001f) return;
        var axis = (end - start).Normalized(); var normal = new Vector2(-axis.Y, axis.X);
        DrawColoredPolygon(new[] { start + normal * startWidth / 2, end + normal * endWidth / 2,
            end - normal * endWidth / 2, start - normal * startWidth / 2 }, color);
    }
}
