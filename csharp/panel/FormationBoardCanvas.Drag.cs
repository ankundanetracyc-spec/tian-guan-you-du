using Godot;
using Kind = FormationGridModel.Kind;

public partial class FormationBoardCanvas
{
    private FormationGridModel.Cell? _dropPreviewCell;
    private Kind _dropPreviewKind;
    private bool _dropPreviewValid;
    private bool _dropExitConnected;

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd) ClearDropPreview();
    }

    private void ClearDropPreview()
    {
        if (_dropPreviewCell is null) return;
        _dropPreviewCell = null;
        QueueRedraw();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!_dropExitConnected) { MouseExited += ClearDropPreview; _dropExitConnected = true; }
        if (data.VariantType != Variant.Type.Dictionary) { ClearDropPreview(); return false; }
        var payload = data.AsGodotDictionary();
        if (!payload.ContainsKey("formation_part_kind")) { ClearDropPreview(); return false; }
        var kind = (Kind)(int)payload["formation_part_kind"];
        var stateKey = payload.ContainsKey(FormationPartDragButton.StateKeyPayload) ? payload[FormationPartDragButton.StateKeyPayload].AsString() : null;
        if (!Enum.IsDefined(kind) || kind is Kind.Eye or Kind.Rhythm or Kind.And or Kind.Or || DropOriginAt(atPosition, kind) is not { } cell)
        { ClearDropPreview(); return false; }
        if (stateKey is not null && (kind != Kind.StateInput || StateInputSettings(stateKey) is null)) { ClearDropPreview(); return false; }
        // 用独立草图验证实际放置，覆盖唯一阵源、足迹与线路约束，不改当前盘。
        var valid = FormationGridModel.TryDeserialize(_model.Serialize(), out var candidate) && candidate is not null && candidate.TryPlaceComponent(kind, cell);
        _dropPreviewCell = cell; _dropPreviewKind = kind; _dropPreviewValid = valid; QueueRedraw();
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_CanDropData(atPosition, data)) { ClearDropPreview(); return; }
        var payload = data.AsGodotDictionary();
        var kind = (Kind)(int)payload["formation_part_kind"];
        var stateKey = payload.ContainsKey(FormationPartDragButton.StateKeyPayload) ? payload[FormationPartDragButton.StateKeyPayload].AsString() : null;
        if (DropOriginAt(atPosition, kind) is not { } cell) { ClearDropPreview(); return; }
        var id = "part_" + Guid.NewGuid().ToString("N");
        var placed = _model.TryPlaceComponent(kind, cell, id: id);
        if (placed && stateKey is not null)
        {
            var settings = StateInputSettings(stateKey);
            placed = settings is not null && _model.TryConfigureComponent(id, settings);
            if (!placed) _model.RemoveComponent(id);
        }
        ClearDropPreview();
        if (placed) { _selected = id; SelectedWireCell = null; _tool = EditTool.Select; SelectionChanged?.Invoke(DescribeSelected()); }
        Changed(placed, "已放置" + (stateKey is not null ? StateInputName(stateKey) : PartName(kind)) + "。");
    }

    private static FormationGridModel.Settings? StateInputSettings(string stateKey)
    {
        var unit = stateKey switch
        {
            "health" => FormationValueUnit.Percent,
            "shield" => FormationValueUnit.Shield,
            "mana" => FormationValueUnit.Mana,
            "energy" => FormationValueUnit.Energy,
            _ => FormationValueUnit.None
        };
        return unit == FormationValueUnit.None ? null : FormationGridModel.DefaultSettingsFor(Kind.StateInput) with
        { StateKey = stateKey, SignalType = FormationSignalType.Number, Unit = unit };
    }

    private void DrawDropPreview()
    {
        if (_dropPreviewCell is not { } cell) return;
        var unit = GridRect.Size.X / _model.BoardSize;
        var shape = PartShape(_dropPreviewKind);
        var rect = new Rect2(GridRect.Position + new Vector2(cell.X, cell.Y) * unit, new Vector2(shape.X, shape.Y) * unit).Grow(-2);
        var color = _dropPreviewValid ? new Color(.55f, .76f, .57f, .28f) : new Color(.9f, .32f, .24f, .35f);
        DrawRect(rect, color);
        DrawRect(rect, new Color(color.R, color.G, color.B, .95f), false, 2, true);
        DrawComponent(new FormationGridModel.Component("preview", _dropPreviewKind, cell, 0), true);
    }

    private FormationGridModel.Cell? DropOriginAt(Vector2 position, Kind kind)
    {
        var grid = GridRect;
        if (!grid.HasPoint(position)) return null;
        var unit = grid.Size.X / _model.BoardSize;
        var local = (position - grid.Position) / unit;
        var shape = PartShape(kind);
        return new FormationGridModel.Cell(
            Mathf.FloorToInt(local.X - shape.X / 2f + .5f),
            Mathf.FloorToInt(local.Y - shape.Y / 2f + .5f));
    }
}
