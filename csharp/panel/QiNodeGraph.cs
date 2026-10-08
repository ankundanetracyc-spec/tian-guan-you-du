using Godot;

/// <summary>五行参悟网盘的水墨绘制层。节点只负责选择，具体文案由右侧详情栏承载。</summary>
public partial class QiNodeGraph : Control
{
    [Signal] public delegate void NodeSelectedEventHandler(string node_id);

    private IReadOnlyList<QiNodeDefinition> _nodes = Array.Empty<QiNodeDefinition>();
    private readonly Dictionary<string, int> _levels = [];
    private readonly Dictionary<string, Vector2> _positions = [];
    private readonly List<(string Id, Vector2 Position, float Radius)> _hits = [];
    private string _selectedId = "wood_root";
    private Vector2 _lastSize;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(700, 700);
        MouseFilter = MouseFilterEnum.Stop;
        GuiInput += on_gui_input;
        Resized += () => QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Size != _lastSize)
        {
            _lastSize = Size;
            QueueRedraw();
        }
    }

    public void configure(IReadOnlyList<QiNodeDefinition> nodes, Godot.Collections.Dictionary? levels = null)
    {
        _nodes = nodes;
        _levels.Clear();
        if (levels is not null)
            foreach (Variant key in levels.Keys) _levels[key.AsString()] = Mathf.Max(0, levels[key].AsInt32());
        if (_nodes.All(node => node.Id != _selectedId)) _selectedId = _nodes.FirstOrDefault()?.Id ?? "";
        QueueRedraw();
    }

    public void select(string nodeId, bool emit = false)
    {
        if (_nodes.All(node => node.Id != nodeId)) return;
        _selectedId = nodeId;
        QueueRedraw();
        if (emit) EmitSignal(SignalName.NodeSelected, nodeId);
    }

    public override void _Draw()
    {
        _positions.Clear();
        _hits.Clear();
        if (Size.X < 20 || Size.Y < 20) return;

        var center = Size * .5f;
        var unit = Mathf.Min(Size.X, Size.Y);
        var outer = unit * .46f;
        DrawCircle(center, outer, new Color(.82f, .77f, .66f, .16f));
        DrawArc(center, outer, 0f, Mathf.Tau, 96, new Color(.32f, .27f, .19f, .52f), 2.2f, true);
        DrawArc(center, outer * .81f, 0f, Mathf.Tau, 96, new Color(.46f, .37f, .24f, .20f), 1.2f, true);
        DrawArc(center, outer * .48f, 0f, Mathf.Tau, 72, new Color(.46f, .37f, .24f, .18f), 1f, true);

        foreach (var node in _nodes) _positions[node.Id] = node_position(node, center, unit);
        draw_connections();
        foreach (var node in _nodes) draw_node(node, _positions[node.Id]);

        DrawString(ThemeDB.FallbackFont, center + new Vector2(-96, 5), "五行流炁", HorizontalAlignment.Center, 192, 18, new Color(.27f, .22f, .15f, .68f));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-124, 27), "点击节点查看参悟效果", HorizontalAlignment.Center, 248, 12, new Color(.30f, .25f, .18f, .52f));
    }

    private Vector2 node_position(QiNodeDefinition node, Vector2 center, float unit)
    {
        var elementIndex = element_index(node.Element);
        var baseAngle = -Mathf.Pi * .5f + elementIndex * Mathf.Tau / 5f;
        var angle = node.Kind switch
        {
            "生" => baseAngle + Mathf.Tau / 10f,
            "克" => baseAngle - Mathf.Tau / 10f,
            "合" => baseAngle + Mathf.Tau / 10f,
            _ => baseAngle
        };
        var ring = node.Kind switch
        {
            "根" => .37f,
            "生" => .245f,
            "克" => .305f,
            "用" => .145f,
            "合" => .09f,
            _ => .2f
        };
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * unit * ring;
    }

    private void draw_connections()
    {
        foreach (var node in _nodes)
        {
            if (!_positions.TryGetValue(node.Id, out var destination)) continue;
            foreach (var prerequisite in node.Prerequisites)
            {
                if (!_positions.TryGetValue(prerequisite, out var origin)) continue;
                var color = QiNodeCatalog.KindColor(node.Kind);
                var alpha = get_level(node) > 0 ? .55f : .20f;
                var width = node.Kind switch { "生" => 3.1f, "克" => 1.8f, "合" => 3.2f, _ => 2.2f };
                var line = new Color(color.R, color.G, color.B, alpha);
                DrawLine(origin, destination, line, width, true);
                var direction = (destination - origin).Normalized();
                var marker = destination - direction * 24f;
                var wing = direction.Rotated(Mathf.Pi * .78f) * 8f;
                DrawLine(marker, marker + wing, line, Mathf.Max(1f, width * .72f), true);
                DrawLine(marker, marker - wing, line, Mathf.Max(1f, width * .72f), true);
            }
        }
    }

    private void draw_node(QiNodeDefinition node, Vector2 position)
    {
        const float radius = 23f;
        var element = QiNodeCatalog.TraitColor(node.Element);
        var selected = node.Id == _selectedId;
        var currentLevel = get_level(node);
        var unlocked = currentLevel > 0;
        var fill = unlocked ? element : new Color(element.R, element.G, element.B, .24f);
        if (selected)
        {
            DrawCircle(position, radius + 10f, new Color(.88f, .68f, .30f, .22f));
            DrawArc(position, radius + 8f, 0f, Mathf.Tau, 32, new Color(.72f, .48f, .17f, .82f), 2.4f, true);
        }
        DrawCircle(position, radius, fill);
        DrawArc(position, radius, 0f, Mathf.Tau, 28, unlocked ? new Color(.23f, .18f, .12f, .84f) : new Color(.28f, .24f, .19f, .36f), 1.7f, true);
        DrawString(ThemeDB.FallbackFont, position + new Vector2(-18, 5), node.Kind, HorizontalAlignment.Center, 36, 16, unlocked ? Colors.White : new Color(1f, 1f, 1f, .48f));
        var labelColor = unlocked ? new Color(.20f, .16f, .11f, .95f) : new Color(.24f, .20f, .15f, .48f);
        DrawString(ThemeDB.FallbackFont, position + new Vector2(-62, radius + 18), node.Name, HorizontalAlignment.Center, 124, 12, labelColor);
        if (!unlocked) DrawString(ThemeDB.FallbackFont, position + new Vector2(-24, -radius - 7), "锁", HorizontalAlignment.Center, 48, 11, new Color(.30f, .24f, .17f, .58f));
        else
        {
            var dotStart = position.X - (node.MaxLevel - 1) * 5f;
            for (var index = 0; index < node.MaxLevel; index++)
                DrawCircle(new Vector2(dotStart + index * 10f, position.Y - radius - 7f), 3.1f, index < currentLevel ? new Color(.39f, .28f, .16f, .92f) : new Color(.39f, .28f, .16f, .24f));
        }
        _hits.Add((node.Id, position, radius + 10f));
    }

    private int get_level(QiNodeDefinition node) => _levels.GetValueOrDefault(node.Id, 0);

    private void on_gui_input(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        foreach (var hit in _hits)
        {
            if (hit.Position.DistanceTo(click.Position) > hit.Radius) continue;
            select(hit.Id, true);
            AcceptEvent();
            return;
        }
    }

    private static int element_index(string element) => element switch
    {
        "木" => 0,
        "火" => 1,
        "土" => 2,
        "金" => 3,
        "水" => 4,
        _ => 0
    };
}
