using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Display-only adapter. Inventory, board occupancy and coverage remain separate coordinate sets.</summary>
public static class ItemLayoutPresentation
{
    public static Vector2I Rotate(Vector2I cell, int quarterTurns)
    {
        for (var i = 0; i < ((quarterTurns % 4) + 4) % 4; i++) cell = new(-cell.Y, cell.X);
        return cell;
    }

    public static IReadOnlyList<HoverGridDiagram> Diagrams(ItemLayoutPreviewData? layout, int quarterTurns = 0)
    {
        if (layout is null) return [];
        if (layout.validate_definition().Count != 0)
            return [new("战斗占格", HoverGridKind.Combat, [], "未定义")];
        var result = new List<HoverGridDiagram>
        {
            new("战斗占格", HoverGridKind.Combat, layout.board_cells.Select(cell => Rotate(cell, quarterTurns)).ToArray(),
                layout.board_cells.Count == 0 ? "未定义" : "", RotationAllowed: layout.board_rotation_allowed)
        };
        foreach (var range in layout.ranges)
        {
            var turns = range.direction + quarterTurns;
            var origin = Rotate(range.source_anchor, turns);
            result.Add(new(range.title, HoverGridKind.Range,
                range.offsets.Select(offset => origin + Rotate(offset, turns)).ToArray(), range.caption,
                origin, Rotate(Vector2I.Right, turns), range.rotation_allowed, range.source_label));
        }
        return result;
    }

    public static HoverInfoManager.HoverInfoContent Attach(HoverInfoManager.HoverInfoContent content, ItemLayoutPreviewData? layout) =>
        layout is null ? content : content with { GridDiagrams = Diagrams(layout) };

    public static HoverInfoManager.HoverInfoContent AttachItem(HoverInfoManager.HoverInfoContent content, ItemData item, int? count = null)
    {
        content = AttachStorageVolume(content, item.get_storage_volume(), item.can_stack ? count : null);
        if (!EquipmentItemPresentation.IsEquipment(item))
            content = content with { CardWidth = 340f, IconSize = 96f };
        if (item.layout_preview is not null) return Attach(content, item.layout_preview);
        // Read the same eligibility and footprint as the actual board. World-only pills do not acquire a board diagram.
        if (item is PillItemData && BattleSupplyRules.Load(item.ResourcePath) is { } pill)
        {
            var size = pill.get_inventory_size();
            var cells = (from y in Enumerable.Range(0, size.Y) from x in Enumerable.Range(0, size.X) select new Vector2I(x, y)).ToArray();
            return content with { GridDiagrams = [new("战斗占格", HoverGridKind.Combat, cells, "每块一颗", RotationAllowed: pill.inventory_can_rotate)] };
        }
        return content;
    }

    public static HoverInfoManager.HoverInfoContent AttachStorageVolume(HoverInfoManager.HoverInfoContent content, int volume, int? count = null)
    {
        string[] fields = count is > 1
            ? [$"单件体积：{volume} 容量", $"当前堆叠容量：{(long)Math.Max(0, volume) * count.Value} 容量"]
            : [$"单件体积：{volume} 容量"];
        string fieldText = string.Join("\n", fields);
        string body = PrependStorageFields(content.Body, fieldText);
        IReadOnlyList<HoverInfoManager.HoverInfoSection>? sections = content.Sections;
        if (content.Sections is { Count: > 0 } source)
        {
            var updated = source.ToArray();
            updated[0] = updated[0] with { Body = PrependStorageFields(updated[0].Body, fieldText) };
            sections = updated;
        }
        var subtitle = string.Join(" · ", content.Subtitle.Split('·', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !value.StartsWith("单件体积", StringComparison.Ordinal)
                && !value.StartsWith("当前堆叠容量", StringComparison.Ordinal)));
        var diagrams = content.GridDiagrams?.Where(diagram => diagram.Kind != HoverGridKind.Inventory).ToArray();
        return content with
        {
            Body = body,
            Sections = sections,
            Subtitle = subtitle,
            Footprint = null,
            GridDiagrams = diagrams,
            InventoryFootprintBelowTitle = false
        };
    }

    private static string PrependStorageFields(string body, string fields)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        while (lines.Count > 0 && (lines[0].StartsWith("单件体积", StringComparison.Ordinal)
            || lines[0].StartsWith("当前堆叠容量", StringComparison.Ordinal)))
            lines.RemoveAt(0);
        var remainder = string.Join("\n", lines).TrimStart('\n');
        return string.IsNullOrWhiteSpace(remainder) ? fields : fields + "\n" + remainder;
    }

    public static string SharpnessDescription(ItemLayoutPreviewData? layout) => layout is not null && layout.validate_definition().Count == 0
        ? string.Join("\n", layout.ranges.Where(range => range.purpose == ItemLayoutRangeData.Purpose.Sharpness)
            .Select(range => range.title + "：" + range.description).Where(text => !string.IsNullOrWhiteSpace(text))) : "";
}
