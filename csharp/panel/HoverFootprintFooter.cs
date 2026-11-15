using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Mouse-transparent battle footer, retaining the object's body and ranges on one diagram.</summary>
public partial class HoverFootprintFooter : VBoxContainer
{
    private const float RowGap = 6f;
    private const float ItemGap = 8f;
    private const float LegendGap = 4f;
    private readonly ColorRect _separator;
    private readonly Font _font;
    private readonly DiagramRow _inventoryRow;
    private readonly DiagramRow _combatRow;
    private readonly List<DiagramRow> _activeRows = new();
    private float _availableWidth = 304f;
    private bool _combatOnly;

    public float ContentHeight { get; private set; }

    public HoverFootprintFooter()
    {
        Name = "HoverFootprintFooter";
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", (int)RowGap);
        _font = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");

        _separator = new ColorRect
        {
            Name = "BattleFootprintDivider",
            Color = new Color(.61f, .58f, .47f, .38f),
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        AddChild(_separator);

        // Existing smoke callers cache this exact child before the first SetDiagrams call.
        _inventoryRow = CreateRow("InventoryRow", "InventoryFootprint");
        _combatRow = CreateRow("BattleRow", "CombatFootprint");
        AddChild(_inventoryRow.Container);
        AddChild(_combatRow.Container);
        ClearRow(_inventoryRow);
        ClearRow(_combatRow);
        Visible = false;
    }

    public void SetDiagrams(Vector2I? inventoryFootprint, IReadOnlyList<HoverGridDiagram>? diagrams, bool combatOnly = false)
    {
        _combatOnly = combatOnly;
        ClearRow(_inventoryRow);
        ClearRow(_combatRow);
        _activeRows.Clear();
        var supplied = diagrams?.Where(diagram => diagram is not null).ToArray()
            ?? Array.Empty<HoverGridDiagram>();
        var inventory = supplied.FirstOrDefault(diagram => diagram.Kind == HoverGridKind.Inventory);
        if (!combatOnly && inventory is null && inventoryFootprint is { X: > 0, Y: > 0 } size)
        {
            _inventoryRow.View.SetFootprint(size);
            inventory = _inventoryRow.View.Diagram;
        }
        if (!combatOnly && inventory is not null)
        {
            _inventoryRow.View.SetDiagram(inventory);
            SetRowContent(_inventoryRow, inventory, Array.Empty<HoverGridDiagram>());
            _activeRows.Add(_inventoryRow);
        }

        var combat = supplied.FirstOrDefault(diagram => diagram.Kind == HoverGridKind.Combat);
        var ranges = supplied.Where(diagram => diagram.Kind == HoverGridKind.Range).ToArray();
        if (combat is not null)
        {
            _combatRow.View.SetComposite(combat, ranges);
            SetRowContent(_combatRow, combat, ranges);
            _activeRows.Add(_combatRow);
        }
        else if (ranges.Length > 0)
        {
            var overlays = ranges.Skip(1).ToArray();
            _combatRow.View.SetComposite(ranges[0], overlays);
            SetRowContent(_combatRow, ranges[0], overlays);
            _activeRows.Add(_combatRow);
        }

        Visible = _activeRows.Count > 0;
        _separator.Visible = combatOnly && Visible;
        ConfigureWidth(_availableWidth);
    }

    /// <summary>Use the hover's inner width. Rescaling preserves orientation, cells and holes.</summary>
    public void ConfigureWidth(float availableWidth)
    {
        _availableWidth = float.IsFinite(availableWidth) ? Mathf.Max(24f, availableWidth) : 304f;
        ContentHeight = 0;
        foreach (var row in _activeRows) ContentHeight += ConfigureRowWidth(row, _availableWidth);
        if (_activeRows.Count > 1) ContentHeight += RowGap * (_activeRows.Count - 1);
        if (_separator.Visible) ContentHeight += 1f + RowGap;
        CustomMinimumSize = new Vector2(0, ContentHeight);
        QueueSort();
    }

    private DiagramRow CreateRow(string nodeName, string viewName)
    {
        var container = new HBoxContainer
        {
            Name = nodeName,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        container.AddThemeConstantOverride("separation", (int)ItemGap);
        var title = CreateLabel("Title", 13, new Color("eee7d4"));
        var view = new InventoryFootprintView
        {
            Name = viewName,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        var legend = new HBoxContainer
        {
            Name = "Legend",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        legend.AddThemeConstantOverride("separation", (int)LegendGap);
        var bodySummary = CreateLabel("BodySummary", 12, new Color("8fc7c9"));
        var rangeSummary = CreateLabel("RangeSummary", 12, new Color("e7cb7c"));
        legend.AddChild(bodySummary);
        legend.AddChild(rangeSummary);
        container.AddChild(title);
        container.AddChild(view);
        container.AddChild(legend);
        return new DiagramRow(container, title, view, legend, bodySummary, rangeSummary);
    }

    private Label CreateLabel(string nodeName, int fontSize, Color color)
    {
        var label = new Label
        {
            Name = nodeName,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
        };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static void ClearRow(DiagramRow row)
    {
        row.View.SetFootprint(null);
        row.Container.CustomMinimumSize = Vector2.Zero;
        row.Container.Hide();
        row.BodySummary.Text = "";
        row.RangeSummary.Text = "";
    }

    private static void SetRowContent(DiagramRow row, HoverGridDiagram primary, IReadOnlyList<HoverGridDiagram> overlays)
    {
        row.Title.Text = primary.Kind switch
        {
            HoverGridKind.Inventory => "物品体积",
            HoverGridKind.Combat => "战斗",
            _ => "范围",
        };
        row.BodySummary.AddThemeColorOverride("font_color",
            primary.Kind == HoverGridKind.Inventory ? new Color("d2ad7b") : new Color("8fc7c9"));
        var baseSize = row.View.BaseGridDimensions;
        row.BodySummary.Text = primary.Kind == HoverGridKind.Range ? ""
            : row.View.OccupiedCellCount == 0 ? "未定义"
            : primary.Kind == HoverGridKind.Inventory ? $"{baseSize.X} 容量"
            : $"本体{row.View.OccupiedCellCount}格";
        var ranges = primary.Kind == HoverGridKind.Range ? new[] { primary }.Concat(overlays) : overlays;
        row.RangeSummary.Text = string.Join(" / ", ranges.Select(range =>
        {
            var count = (range.Cells ?? Array.Empty<Vector2I>()).Where(cell => cell != range.Origin).Distinct().Count();
            return ShortRangeTitle(range.Title) + (count > 0 ? $"{count}格" : "未定义");
        }));
        row.Container.Show();
    }

    private static string ShortRangeTitle(string title)
    {
        var text = string.IsNullOrWhiteSpace(title) ? "范围" : title.Trim().Replace('\n', ' ').Replace('\r', ' ');
        foreach (var suffix in new[] { "作用范围", "范围" })
        {
            if (!text.EndsWith(suffix, StringComparison.Ordinal)) continue;
            text = text[..^suffix.Length].Trim();
            break;
        }
        return text.Length > 0 ? text : "范围";
    }

    private float ConfigureRowWidth(DiagramRow row, float width)
    {
        if (_combatOnly)
        {
            row.Title.Text = "战斗占格：";
            row.Title.AddThemeFontSizeOverride("font_size", 18);
            row.Title.Show();
            var captionSize = MeasureLabel(row.Title, 18);
            row.RangeSummary.Hide();
            // Keep the battle caption, without a redundant size or cell-count legend.
            row.BodySummary.Visible = !row.View.Visible;
            row.Legend.Visible = !row.View.Visible;
            var combatHeight = captionSize.Y;
            if (row.View.Visible)
            {
                row.View.ConfigureScale(24f, Mathf.Min(120f, Mathf.Max(1f, width - captionSize.X - ItemGap)));
                combatHeight = Mathf.Max(combatHeight, row.View.CustomMinimumSize.Y);
            }
            else combatHeight = Mathf.Max(combatHeight, MeasureLabel(row.BodySummary, 12).Y);
            row.Container.CustomMinimumSize = new Vector2(0, combatHeight);
            return combatHeight;
        }
        row.Title.AddThemeFontSizeOverride("font_size", 13);
        row.Title.Show();
        var titleSize = MeasureLabel(row.Title, 13);
        var bodySize = MeasureLabel(row.BodySummary, 12);
        var rangeSize = MeasureLabel(row.RangeSummary, 12);
        row.BodySummary.Visible = row.BodySummary.Text.Length > 0;
        row.RangeSummary.Visible = row.RangeSummary.Text.Length > 0;
        var legendWidth = bodySize.X + rangeSize.X
            + (row.BodySummary.Visible && row.RangeSummary.Visible ? LegendGap : 0);
        var showLegend = legendWidth > 0;
        if (row.View.Visible)
        {
            var diagramBudget = width - titleSize.X - ItemGap - legendWidth - (showLegend ? ItemGap : 0);
            if (diagramBudget < 32f && showLegend)
            {
                showLegend = false;
                diagramBudget = width - titleSize.X - ItemGap;
            }
            row.View.ConfigureMaximumSide(Mathf.Min(96f, Mathf.Max(1f, diagramBudget)));
        }
        else if (showLegend)
        {
            // A declared but undefined diagram still keeps its short text in this row.
            var remaining = Mathf.Max(1f, width - titleSize.X - ItemGap);
            if (row.BodySummary.Visible)
            {
                var bodyWidth = Mathf.Min(bodySize.X, remaining);
                row.BodySummary.CustomMinimumSize = new Vector2(bodyWidth, bodySize.Y);
                remaining = Mathf.Max(0, remaining - bodyWidth - (row.RangeSummary.Visible ? LegendGap : 0));
            }
            if (row.RangeSummary.Visible)
                row.RangeSummary.CustomMinimumSize = new Vector2(Mathf.Min(rangeSize.X, remaining), rangeSize.Y);
        }
        row.Legend.Visible = showLegend;
        var height = titleSize.Y;
        if (row.View.Visible) height = Mathf.Max(height, row.View.CustomMinimumSize.Y);
        if (showLegend) height = Mathf.Max(height, Mathf.Max(bodySize.Y, rangeSize.Y));
        row.Container.CustomMinimumSize = new Vector2(0, height);
        return height;
    }

    private Vector2 MeasureLabel(Label label, int fontSize)
    {
        var size = label.Text.Length == 0 ? Vector2.Zero
            : _font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, fontSize).Ceil();
        label.CustomMinimumSize = size;
        return size;
    }

    private sealed record DiagramRow(HBoxContainer Container, Label Title, InventoryFootprintView View,
        HBoxContainer Legend, Label BodySummary, Label RangeSummary);
}
