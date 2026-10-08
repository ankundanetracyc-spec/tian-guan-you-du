using Godot;

/// <summary>启动时配置项目光标；资源缺失时保留系统光标。</summary>
public partial class CursorSetup : Node
{
    public override void _Ready()
    {
        var texture = GD.Load<Texture2D>("res://textures/cursor.png");
        if (texture is null)
        {
            GD.PushWarning("未找到自定义光标图片");
            return;
        }

        var image = texture.GetImage();
        if (image is null)
            return;

        image.Resize(48, 48, Image.Interpolation.Lanczos);
        Input.SetCustomMouseCursor(ImageTexture.CreateFromImage(image), Input.CursorShape.Arrow, Vector2.Zero);
    }
}
