using Godot;
using System;

/// <summary>药材拖拽卡片与主药、辅药投药区，共用Godot原生拖拽。</summary>
public partial class AlchemyDragTile : PanelContainer
{
    public string HerbName = "";
    public string Kind = "";
    public Texture2D? Icon;
    public Action<string, string>? Dropped;
    public Func<bool>? CanInteract;
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (string.IsNullOrEmpty(HerbName) || CanInteract?.Invoke() == false) return default;
        var preview = new VBoxContainer();
        preview.AddChild(new TextureRect { Texture = Icon, CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        preview.AddChild(new Label { Text = HerbName + " ×1", Modulate = new Color(.9f, .83f, .62f) });
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary { ["alchemy_herb"] = HerbName };
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data) => !string.IsNullOrEmpty(Kind)
        && CanInteract?.Invoke() != false && data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey("alchemy_herb");
    public override void _DropData(Vector2 atPosition, Variant data) => Dropped?.Invoke(data.AsGodotDictionary()["alchemy_herb"].AsString(), Kind);
}
