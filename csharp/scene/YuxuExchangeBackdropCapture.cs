using Godot;
using System.IO;
using System.Threading.Tasks;

public partial class YuxuExchangeBackdropCapture : Control
{
    public override async void _Ready()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = new Vector2I(1672, 941);
            GetWindow().ContentScaleSize = new Vector2I(1672, 941);
            var page = new YuxuSectExchangePanel();
            AddChild(page);
            page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            page.open_panel(1480);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var path = ProjectSettings.GlobalizePath("res://output/yuxu-page-returns-20261110/01_善功堂.png");
            using var image = GetViewport().GetTexture().GetImage();
            if (image.SavePng(path) != Error.Ok) throw new IOException("善功堂截图保存失败");
            GetTree().Quit();
        }
        catch (System.Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
