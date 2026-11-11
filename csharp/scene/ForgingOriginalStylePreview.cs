using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>原炼器界面的独立只读演出预览；直接实例化正式页面，保持原控件与字体。</summary>
public partial class ForgingOriginalStylePreview : Control
{
    private ProcessForgingScene _ui = null!;
    private ForgingNativeFirePresentation _fire = null!;
    private string _capture = "";
    private int _frame;
    private double _nextCapture;
    private double _elapsed;
    private bool _recording;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _ui = new ProcessForgingScene(); AddChild(_ui);
        _fire = _ui.EnableNativeFirePreview();
        var args = OS.GetCmdlineUserArgs();
        _capture = args.FirstOrDefault(x => x.StartsWith("--native-fire-capture="))?.Split('=', 2)[1] ?? "";
        if (_capture.Length > 0)
        {
            DirAccess.MakeDirRecursiveAbsolute(_capture);
            Engine.MaxFps = 60;
            GetWindow().Size = new(1280, 720); GetWindow().Unfocusable = true;
            _ = CaptureAndCheck();
        }
    }

    public override void _Process(double delta)
    {
        if (!_recording) return;
        _elapsed += delta;
        if (_elapsed < _nextCapture) return;
        _nextCapture = _elapsed + 1.0 / 30;
        using var image = GetViewport().GetTexture().GetImage();
        image.SaveJpg($"{_capture}/frame{_frame++:D4}.jpg", .94f);
    }

    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private void Save(string file)
    {
        using var image = GetViewport().GetTexture().GetImage(); image.SavePng(_capture + "/" + file);
    }

    private async Task CaptureAndCheck()
    {
        try
        {
            await Frames(4); _fire.Visible = false; await Frames(); Save("baseline.png");
            _fire.Visible = true; await Frames(); Save("ready.png");
            string ledger = _ui.NativeFireLedger;
            int checks = 0;
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
            _recording = true; await Frames(35);
            _ui.PressNativeFireCraft(); await Frames();
            Check(!_ui.NativeFireInputsVisible, "演出没有收起原页面");
            while (_fire.Clock < 1.5f) await Frames(1); Save("materials.png");
            _fire.Skip(); await Frames(); Check(_fire.Finished, "跳过没有进入成器");
            _fire.ReturnToInputs(); await Frames(); Check(_ui.NativeFireInputsVisible, "返回没有恢复原页面");
            Check(_ui.NativeFireLedger == ledger, "预览修改了材料或器物记录");
            _ui.PressNativeFireCraft();
            while (_fire.Clock < 3.5f) await Frames(1); Save("forming.png");
            while (!_fire.Finished) await Frames(1); await Frames(); Save("result.png");
            Check(_ui.NativeFireLedger == ledger, "自然完成修改了材料或器物记录");
            await Frames(35); _fire.ReturnToInputs(); await Frames(); Save("restored.png");
            Check(_ui.NativeFireInputsVisible, "自然完成后页面未恢复");
            await Frames(20); _recording = false;
            GD.Print($"NATIVE_FORGING_STYLE_PASS checks={checks} original_controls=true read_only=true frames={_frame}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
