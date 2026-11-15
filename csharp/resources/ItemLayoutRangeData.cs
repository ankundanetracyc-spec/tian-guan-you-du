using Godot;
using Godot.Collections;

/// <summary>Explicit coverage coordinates for the item reader; never attack reach or a combat effect.</summary>
[GlobalClass]
public partial class ItemLayoutRangeData : Resource
{
    public enum Purpose { Coverage, Sharpness }

    [Export] public Purpose purpose;
    [Export] public string title = "作用范围";
    [Export] public string source_label = "起点";
    [Export] public Vector2I source_anchor;
    [Export] public Array<Vector2I> offsets = [];
    // Same clockwise convention as the combat board: right, down, left, up.
    [Export(PropertyHint.Enum, "右,下,左,上")] public int direction;
    [Export] public bool rotation_allowed;
    [Export] public string caption = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
}
