using Godot;
using System;
using D = Godot.Collections.Dictionary;

public partial class YuxuMarketDragRow : Button
{
    public D Payload { get; set; } = [];
    public Texture2D? ItemTexture { get; set; }
    public string ItemName { get; set; } = "";

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Disabled || Payload.Count == 0) return default;
        HoverInfoManager.HideAll();
        var preview = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        preview.AddThemeConstantOverride("separation", 8);
        preview.AddChild(new TextureRect { Texture = ItemTexture, CustomMinimumSize = new(38, 38),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore });
        preview.AddChild(CraftingPaperStyle.Text(ItemName, 22));
        SetDragPreview(preview);
        return Payload;
    }
}

public partial class YuxuMarketDropArea : ScrollContainer
{
    public string AcceptKind { get; set; } = "";
    public Action<D>? OnDrop { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().GetValueOrDefault("kind", "").AsString() == AcceptKind;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data)) OnDrop?.Invoke(data.AsGodotDictionary());
    }
}

/// <summary>数量槽两侧的减一、加一，图形与数值槽共用中心线。</summary>
public partial class YuxuMarketQuantityStep : Button
{
    public bool Increase { get; set; }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = Increase ? "增加数量" : "减少数量";
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
    }

    public override void _Draw()
    {
        var middle = Size / 2;
        var color = IsPressed() ? new Color("8d6650") : new Color("38433a");
        DrawLine(middle + new Vector2(-6, 0), middle + new Vector2(6, 0), color, 2);
        if (Increase) DrawLine(middle + new Vector2(0, -6), middle + new Vector2(0, 6), color, 2);
    }
}
