using Godot;

/// <summary>世界可玩区域边界及其背景绘制。</summary>
[Tool]
public partial class BaseArea : Node2D
{
    [Export] public Rect2 area_bounds = new(0, 0, 5000, 3500);
    [Export] public Texture2D? background_texture;

    public override void _Ready()
    {
        AddToGroup("base_area");
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (background_texture is not null)
            DrawTextureRect(background_texture, area_bounds, false);
    }

    public void set_background_texture(Texture2D texture)
    {
        background_texture = texture;
        QueueRedraw();
    }

    public void set_area_bounds(Rect2 bounds)
    {
        area_bounds = bounds;
        QueueRedraw();
    }

    public bool is_within_bounds(Vector2 world_pos, float margin = 0.0f)
    {
        var bounds = new Rect2(area_bounds.Position + Vector2.One * margin, area_bounds.Size - Vector2.One * margin * 2.0f);
        return bounds.HasPoint(world_pos);
    }

    public Vector2 clamp_to_bounds(Vector2 world_pos, float margin = 0.0f) => new(
        Mathf.Clamp(world_pos.X, area_bounds.Position.X + margin, area_bounds.End.X - margin),
        Mathf.Clamp(world_pos.Y, area_bounds.Position.Y + margin, area_bounds.End.Y - margin));

    public Rect2 get_global_bounds() => new(GlobalPosition + area_bounds.Position, area_bounds.Size);
}
