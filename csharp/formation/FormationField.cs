using Godot;

/// <summary>阵法在地图上的作用范围；进入/离开信号由 Area2D 原样提供。</summary>
public partial class FormationField : Area2D
{
    [Export] public FormationData? formation_data;
    [Export] public float radius = 100.0f;

    public override void _Ready() => set_radius(radius);

    public void set_radius(float value)
    {
        radius = Mathf.Max(0.0f, value);
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape as CircleShape2D;
        if (shape is not null) shape.Radius = radius;
    }
}
