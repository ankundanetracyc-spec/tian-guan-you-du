using Godot;

/// <summary>启动时配置项目光标；资源缺失时保留系统光标。</summary>
public partial class CursorSetup : Node
{
    private ImageTexture? _cursor;
    public override void _Ready()
    {
        var texture = GD.Load<Texture2D>("res://textures/cursor.png");
        if (texture is null)
        {
            GD.PushWarning("未找到自定义光标图片");
            return;
        }

        using var image = texture.GetImage();
        if (image is null)
            return;

        image.Resize(48, 48, Image.Interpolation.Lanczos);
        _cursor = ImageTexture.CreateFromImage(image);
        Input.SetCustomMouseCursor(_cursor, Input.CursorShape.Arrow, Vector2.Zero);
    }

    public override void _ExitTree()
    {
        // DisplayServer 持有光标纹理；须在 RenderingServer 关闭前先解除全局引用。
        Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
        _cursor?.Dispose();
        _cursor = null;
    }
}
