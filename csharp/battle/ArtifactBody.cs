using Godot;

/// <summary>可选中的器体；没有角色属性、状态、行动队列或地图占格。</summary>
public partial class ArtifactBody : Node2D
{
    [Export] public int current_health;
    [Export] public int max_health;
    [Export] public int faction;
    [Export] public string entity_name = "";
    [Export] public int shield;
    public string InstanceId = "";
    public GodotObject Caster = null!;
    public bool WasSummoned;
    public bool Collapsed;
    public string DepartureReason = "";
    private Label? _label;

    public override void _Ready()
    {
        _label = new Label { Position = new(-70, 14), Size = new(140, 50), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _label.AddThemeFontSizeOverride("font_size", 15);
        AddChild(_label);
        Refresh();
    }
    public void Refresh()
    {
        if (_label is not null) _label.Text = $"{entity_name}\n器体 {current_health}/{max_health}";
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawLine(new(0, -48), new(0, 8), new Color("b8cfc3"), 7, true);
        DrawLine(new(-13, -3), new(13, -3), new Color("b8cfc3"), 4, true);
        DrawLine(new(0, 8), new(0, 19), new Color("81765e"), 5, true);
    }
}
