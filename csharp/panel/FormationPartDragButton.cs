using Godot;
using Kind = FormationGridModel.Kind;

/// <summary>左侧部件目录项：拖出一件部件，不进入持续放置模式。</summary>
public partial class FormationPartDragButton : Button
{
    public const string PayloadKey = "formation_part_kind";
    public const string StateKeyPayload = "formation_state_key";
    public Kind PartKind { get; }
    public string? StateKey { get; }

    public FormationPartDragButton(Kind kind, string? stateKey = null)
    {
        PartKind = kind;
        StateKey = stateKey;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Disabled || PartKind == Kind.Eye) return default;
        var texture = FormationBoardCanvas.LoadArt(FormationBoardCanvas.ArtPath(PartKind));
        if (texture is not null)
        {
            var shape = FormationBoardCanvas.PartShape(PartKind);
            var canvas = GetTree()?.Root.FindChild("FormationBoardCanvas", true, false) as FormationBoardCanvas;
            var unit = canvas is not null && canvas.Model.BoardSize > 0
                ? Mathf.Clamp(canvas.GridRect.Size.X / canvas.Model.BoardSize, 64, 120) : 80;
            var size = new Vector2(shape.X * unit, shape.Y * unit);
            var preview = new Control { Size = Vector2.One, MouseFilter = MouseFilterEnum.Ignore };
            var image = new TextureRect
            {
                Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = size,
                Position = -size / 2,
                Modulate = new Color(1, 1, 1, .82f), MouseFilter = MouseFilterEnum.Ignore
            };
            preview.AddChild(image); SetDragPreview(preview);
        }
        var payload = new Godot.Collections.Dictionary { [PayloadKey] = (int)PartKind };
        if (StateKey is not null) payload[StateKeyPayload] = StateKey;
        return payload;
    }
}
