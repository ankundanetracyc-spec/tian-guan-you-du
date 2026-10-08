using Godot;
using System.Collections.Generic;

/// <summary>功法页的传承墨线层：以中央经卷为主脉，左右法门为旁支。</summary>
public partial class GongfaTreeFlow : Control
{
    private float _time;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double delta) { _time += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        var panel = GetParent<Control>();
        if (panel is null) return;
        var rows = panel.GetNodeOrNull<Control>("TreeRows");
        if (rows is null) return;
        Vector2? previousSealBottom = null;
        var previousLearned = false;
        var activeSeals = new List<(Vector2 center, Vector2 size)>();
        var activeStrokes = new List<(Vector2 from, Vector2 to)>();
        for (var index = 1; index <= 5; index++)
        {
            var row = panel.GetNodeOrNull<Control>($"TreeRows/Row{index}");
            var seal = row?.GetNodeOrNull<Control>("Chapter");
            if (row is null || seal is null || !row.IsVisibleInTree()) continue;
            // Row 的坐标相对 TreeRows，而笔墨层相对 TreePanel；补上容器偏移后，线端才会严格落在节点中心。
            var rowOrigin = rows.Position + row.Position;
            var sealRect = new Rect2(rowOrigin + seal.Position, seal.Size);
            var sealCenter = sealRect.GetCenter();
            var learned = seal.HasMeta("gongfa_learned") && seal.GetMeta("gongfa_learned").AsBool();
            if (previousSealBottom is not null)
            {
                var strokeEnd = new Vector2(sealCenter.X, sealRect.Position.Y - 5f);
                var strokeActive = previousLearned && learned;
                DrawInheritanceStroke(previousSealBottom.Value, strokeEnd, strokeActive);
                if (strokeActive) activeStrokes.Add((previousSealBottom.Value, strokeEnd));
            }
            DrawChapterInkRing(sealCenter, seal.Size, learned);
            if (learned) activeSeals.Add((sealCenter, seal.Size));
            previousSealBottom = new Vector2(sealCenter.X, sealRect.End.Y + 5f);
            previousLearned = learned;

            foreach (var side in new[] { "Left", "Right" })
            {
                var node = row.GetNodeOrNull<Button>(side);
                if (node is null || !node.IsVisibleInTree()) continue;
                var center = rowOrigin + node.Position + node.Size * .5f;
                var source = new Vector2(side == "Left" ? sealRect.Position.X - 5f : sealRect.End.X + 5f, sealCenter.Y);
                var endpoint = new Vector2(center.X + (side == "Left" ? node.Size.X * .34f : -node.Size.X * .34f), center.Y);
                var active = node.HasMeta("gongfa_active") && node.GetMeta("gongfa_active").AsBool();
                var available = !node.Disabled;
                var ink = active ? new Color(.27f, .23f, .16f, .72f) : new Color(.20f, .18f, .14f, available ? .40f : .18f);
                DrawLine(source, endpoint, ink, active ? 2.0f : 1.45f, true);
                if (!active) continue;
                var phase = Mathf.PosMod(_time * .38f + (side == "Right" ? .45f : 0f), 1f);
                var dot = source.Lerp(endpoint, phase);
                DrawCircle(dot, 4.5f, new Color(.79f, .61f, .27f, .15f));
                DrawCircle(dot, 1.9f, new Color(.80f, .66f, .38f, .84f));
                DrawArc(endpoint, 26f, 0f, Mathf.Tau, 20, new Color(.67f, .49f, .23f, .15f), 1.0f, true);
            }
        }
        if (activeStrokes.Count > 0) DrawWholeInheritanceFlow(activeStrokes);
        else if (activeSeals.Count == 1) DrawSingleSealFlow(activeSeals[0].center, activeSeals[0].size);
    }

    private void DrawInheritanceStroke(Vector2 from, Vector2 to, bool active)
    {
        // 所有卷章间都有一条静墨主线；只有前后两卷都参悟后，才浮出缓慢游走的湿墨。
        DrawLine(from, to, active ? new Color(.19f, .17f, .13f, .76f) : new Color(.16f, .15f, .12f, .36f), active ? 3.4f : 2.25f, true);
        if (active) DrawLine(from, to, new Color(.40f, .38f, .30f, .16f), 7.5f, true);
    }

    private void DrawChapterInkRing(Vector2 center, Vector2 size, bool active)
    {
        var radius = Mathf.Max(size.X, size.Y) * .5f + 7f;
        var stillInk = active ? new Color(.24f, .22f, .17f, .72f) : new Color(.19f, .18f, .15f, .34f);
        DrawArc(center, radius, .18f, Mathf.Tau - .32f, 28, stillInk, active ? 2.1f : 1.35f, true);
    }

    private void DrawWholeInheritanceFlow(List<(Vector2 from, Vector2 to)> strokes)
    {
        var totalLength = 0f;
        foreach (var stroke in strokes) totalLength += stroke.from.DistanceTo(stroke.to);
        if (totalLength <= 0.01f) return;
        var travel = Mathf.PosMod(_time * 42f, totalLength);
        foreach (var stroke in strokes)
        {
            var length = stroke.from.DistanceTo(stroke.to);
            if (travel > length) { travel -= length; continue; }
            var direction = (stroke.to - stroke.from).Normalized();
            var brush = stroke.from + direction * travel;
            DrawLine(brush - direction * 13f, brush + direction * 16f, new Color(.54f, .51f, .40f, .40f), 5.0f, true);
            DrawLine(brush - direction * 5f, brush + direction * 8f, new Color(.20f, .19f, .15f, .70f), 2.1f, true);
            break;
        }
    }

    private void DrawSingleSealFlow(Vector2 center, Vector2 size)
    {
        var radius = Mathf.Max(size.X, size.Y) * .5f + 10f;
        var spin = _time * .55f;
        DrawArc(center, radius, spin, spin + 1.45f, 14, new Color(.55f, .50f, .38f, .42f), 2.8f, true);
        DrawArc(center, radius - 3f, spin + Mathf.Pi, spin + Mathf.Pi + .9f, 10, new Color(.20f, .19f, .15f, .52f), 1.7f, true);
    }
}
