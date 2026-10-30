using Godot;

/// <summary>材料自身的物理加工属性；不指定成品、配方或技能。</summary>
public partial class MaterialForgingData : Resource
{
    [Export] public string[] roles = [];
    [Export] public int difficulty;
    [Export] public double retention;
    [Export] public double volume;
    [Export] public double extension;
    [Export] public double thickness;
    [Export] public double edge;
    [Export] public double strength;
    [Export] public double connection;

    public ProcessForgingTrial.Profile Snapshot() => new()
    {
        Roles = (string[])roles.Clone(), Difficulty = difficulty, Retention = retention,
        Volume = volume, Extension = extension, Thickness = thickness,
        Edge = edge, Strength = strength, Connection = connection
    };
}
