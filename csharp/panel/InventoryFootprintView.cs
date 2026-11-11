using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A mouse-transparent miniature of explicitly configured local grid cells.</summary>
public partial class InventoryFootprintView : Control
{
    private const float CellSize = 16f;
    private const float MaximumSide = 96f;
    private readonly HashSet<Vector2I> _occupiedCells = new();
    private readonly HashSet<Vector2I> _rangeCells = new();
    private readonly List<HoverGridDiagram> _rangeDiagrams = new();
    private Vector2I _minimumCell;
    private Vector2I _gridDimensions;
    private float _maximumSide = MaximumSide;
    private float _preferredCellSize = CellSize;

    public Vector2I? Footprint { get; private set; }
    public int OccupiedCellCount => _occupiedCells.Count;
    public int RangeCellCount => _rangeCells.Count;
    public float DisplayCellSize { get; private set; } = CellSize;
    public HoverGridDiagram? Diagram { get; private set; }
    public Vector2I GridDimensions => _gridDimensions;
    public Vector2I BaseGridDimensions { get; private set; }
    public IReadOnlyList<HoverGridDiagram> Overlays { get; private set; } = Array.Empty<HoverGridDiagram>();

    public InventoryFootprintView()
    {
        Name = "InventoryFootprint";
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Visible = false;
    }

    /// <summary>The original rectangular inventory API remains available to existing callers.</summary>
    public void SetFootprint(Vector2I? footprint)
    {
        if (footprint is not { X: > 0, Y: > 0 } dimensions)
        {
            ClearDiagram();
            return;
        }

        var cells = new List<Vector2I>();
        for (var y = 0; y < dimensions.Y; y++)
        for (var x = 0; x < dimensions.X; x++)
            cells.Add(new Vector2I(x, y));
        SetDiagram(new HoverGridDiagram("物品体积", HoverGridKind.Inventory, cells));
    }

    public void SetDiagram(HoverGridDiagram diagram) => SetComposite(diagram, null);

    /// <summary>Draw the real body and its ranges on one shared local coordinate axis.</summary>
    public void SetComposite(HoverGridDiagram baseDiagram, IReadOnlyList<HoverGridDiagram>? overlays)
    {
        ArgumentNullException.ThrowIfNull(baseDiagram);
        Diagram = baseDiagram;
        _occupiedCells.Clear();
        _rangeCells.Clear();
        _rangeDiagrams.Clear();
        Overlays = overlays?.Where(diagram => diagram is not null && diagram.Kind == HoverGridKind.Range).ToArray()
            ?? Array.Empty<HoverGridDiagram>();
        if (baseDiagram.Kind == HoverGridKind.Range) _rangeDiagrams.Add(baseDiagram);
        else
            foreach (var cell in baseDiagram.Cells ?? Array.Empty<Vector2I>()) _occupiedCells.Add(cell);
        _rangeDiagrams.AddRange(Overlays);
        foreach (var range in _rangeDiagrams)
        {
            foreach (var cell in range.Cells ?? Array.Empty<Vector2I>())
            {
                // An origin is a reference point; it never adds a beneficiary or a body cell.
                if (cell != range.Origin) _rangeCells.Add(cell);
            }
        }
        if (_occupiedCells.Count == 0 && _rangeCells.Count == 0)
        {
            ClearDiagram();
            return;
        }

        var minimum = new Vector2I(int.MaxValue, int.MaxValue);
        var maximum = new Vector2I(int.MinValue, int.MinValue);
        foreach (var cell in _occupiedCells) IncludeCell(cell, ref minimum, ref maximum);
        BaseGridDimensions = _occupiedCells.Count > 0 ? maximum - minimum + Vector2I.One : Vector2I.Zero;
        foreach (var cell in _rangeCells) IncludeCell(cell, ref minimum, ref maximum);
        foreach (var range in _rangeDiagrams)
            if (HasBeneficiaries(range) && range.Origin is { } source) IncludeCell(source, ref minimum, ref maximum);
        _minimumCell = minimum;
        _gridDimensions = maximum - minimum + Vector2I.One;
        Footprint = baseDiagram.Kind == HoverGridKind.Inventory ? BaseGridDimensions : null;
        Visible = true;
        UpdateMetrics();
    }

    /// <summary>Allow a narrow footer column to fit the same cells without changing their shape.</summary>
    public void ConfigureMaximumSide(float maximumSide)
    {
        ConfigureScale(CellSize, Mathf.Clamp(maximumSide, 1f, MaximumSide));
    }

    /// <summary>Enlarge a display grid without changing its cells, orientation or proportions.</summary>
    public void ConfigureScale(float cellSize, float maximumSide)
    {
        _preferredCellSize = Mathf.Max(1f, cellSize);
        _maximumSide = Mathf.Max(1f, maximumSide);
        UpdateMetrics();
    }

    private static void IncludeCell(Vector2I cell, ref Vector2I minimum, ref Vector2I maximum)
    {
        minimum = new Vector2I(Math.Min(minimum.X, cell.X), Math.Min(minimum.Y, cell.Y));
        maximum = new Vector2I(Math.Max(maximum.X, cell.X), Math.Max(maximum.Y, cell.Y));
    }

    private void ClearDiagram()
    {
        Diagram = null;
        Footprint = null;
        _occupiedCells.Clear();
        _rangeCells.Clear();
        _rangeDiagrams.Clear();
        Overlays = Array.Empty<HoverGridDiagram>();
        _gridDimensions = Vector2I.Zero;
        BaseGridDimensions = Vector2I.Zero;
        DisplayCellSize = _preferredCellSize;
        CustomMinimumSize = Vector2.Zero;
        Visible = false;
        QueueRedraw();
    }

    private void UpdateMetrics()
    {
        DisplayCellSize = _gridDimensions == Vector2I.Zero ? _preferredCellSize
            : Mathf.Min(_preferredCellSize, _maximumSide / Mathf.Max(_gridDimensions.X, _gridDimensions.Y));
        CustomMinimumSize = (Vector2)_gridDimensions * DisplayCellSize;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Diagram is not { } diagram || _gridDimensions == Vector2I.Zero) return;
        var extent = (Vector2)_gridDimensions * DisplayCellSize;
        var origin = ((Size - extent) / 2f).Round();
        var fill = diagram.Kind == HoverGridKind.Inventory ? new Color("947554", .42f) : new Color("457f85", .42f);
        var line = diagram.Kind == HoverGridKind.Inventory ? new Color("d2ad7b", .9f) : new Color("8fc7c9", .92f);
        var rangeFill = new Color("9b7c3f", .38f);
        var rangeLine = new Color("e7cb7c", .95f);

        // Faint unoccupied cells reveal holes instead of filling the shape's bounding rectangle.
        DrawRect(new Rect2(origin, extent), new Color("b7b19b", .045f));
        if (DisplayCellSize >= 3f)
        {
            var ghost = new Color("b7b19b", .17f);
            for (var x = 0; x <= _gridDimensions.X; x++)
            {
                var offset = Mathf.Clamp(x * DisplayCellSize, .5f, Mathf.Max(.5f, extent.X - .5f));
                DrawLine(origin + new Vector2(offset, 0), origin + new Vector2(offset, extent.Y), ghost);
            }
            for (var y = 0; y <= _gridDimensions.Y; y++)
            {
                var offset = Mathf.Clamp(y * DisplayCellSize, .5f, Mathf.Max(.5f, extent.Y - .5f));
                DrawLine(origin + new Vector2(0, offset), origin + new Vector2(extent.X, offset), ghost);
            }
        }

        var outlineInset = Mathf.Min(.5f, DisplayCellSize * .1f);
        foreach (var cell in _occupiedCells)
        {
            var rectangle = CellRectangle(cell, origin);
            DrawRect(rectangle, fill);
            DrawRect(rectangle.Grow(-outlineInset), line, false);
        }
        foreach (var cell in _rangeCells)
        {
            var rectangle = CellRectangle(cell, origin);
            if (!_occupiedCells.Contains(cell)) DrawRect(rectangle, rangeFill);
            DrawRect(rectangle.Grow(-outlineInset), rangeLine, false);
            DrawStar(rectangle.GetCenter(), rangeLine);
        }
        foreach (var range in _rangeDiagrams)
        {
            if (!HasBeneficiaries(range) || range.Origin is not { } source) continue;
            var rectangle = CellRectangle(source, origin);
            if (!_occupiedCells.Contains(source) && !_rangeCells.Contains(source))
            {
                DrawRect(rectangle, new Color("e7cb7c", .07f));
                DrawRect(rectangle.Grow(-outlineInset), new Color("e7cb7c", .45f), false);
            }
        }
    }

    private static bool HasBeneficiaries(HoverGridDiagram range) =>
        range.Cells is not null && range.Cells.Any(cell => cell != range.Origin);

    private Rect2 CellRectangle(Vector2I cell, Vector2 drawingOrigin) =>
        new(drawingOrigin + (Vector2)(cell - _minimumCell) * DisplayCellSize, Vector2.One * DisplayCellSize);

    private void DrawStar(Vector2 center, Color color)
    {
        var points = new Vector2[10];
        for (var index = 0; index < points.Length; index++)
        {
            var angle = -Mathf.Pi / 2f + index * Mathf.Pi / 5f;
            var radius = DisplayCellSize * (index % 2 == 0 ? .31f : .135f);
            points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        DrawColoredPolygon(points, color);
    }

}
