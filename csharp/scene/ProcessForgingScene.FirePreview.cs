using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class ProcessForgingScene
{
    private ForgingNativeFirePresentation? _nativeFirePreview;
    private TextureRect? _nativeFireBackdrop;
    private Texture2D? _nativeOriginalBackdrop;

    /// <summary>独立预览复用原控件；没有绑定人物，开炼只读取当前配料的演出结果。</summary>
    public ForgingNativeFirePresentation EnableNativeFirePreview()
    {
        if (_forgingOwner is not null) throw new InvalidOperationException("原界面演出预览不能绑定人物。");
        if (_nativeFirePreview is not null) return _nativeFirePreview;
        _nativeFireBackdrop = GetChildren().OfType<TextureRect>().First();
        _nativeOriginalBackdrop = _nativeFireBackdrop.Texture;
        _nativeFirePreview = new ForgingNativeFirePresentation { Name = "NativeFirePresentation" };
        _canvas.AddChild(_nativeFirePreview);
        // 原页面仍在上层；所有原字体、纸面、描边、间距和输入事件不另造一套。
        _canvas.MoveChild(_nativeFirePreview, 0);
        _nativeFirePreview.Returned += () =>
        {
            _page.Visible = true;
            _nativeFireBackdrop.Texture = _nativeOriginalBackdrop;
        };
        LoadExample(1);
        return _nativeFirePreview;
    }

    private void RefreshNativeFirePreview()
    {
        if (_nativeFirePreview is null) return;
        var craft = _buttons["craft"];
        craft.Pressed -= Craft;
        craft.Pressed += PlayNativeFirePreview;
    }

    private void PlayNativeFirePreview()
    {
        if (_nativeFirePreview is null || _forgingOwner is not null) return;
        var result = _model.Evaluate(_draft);
        if (!result.Success) { Message("本次未成器", string.Join("\n", result.Errors.Concat(result.Failures))); return; }
        HoverInfoManager.HideAll();
        _nativeFireBackdrop!.Texture = GD.Load<Texture2D>("res://textures/ui/forging_fire_20261107/workshop_unlit_v1.png");
        _page.Visible = false;
        _nativeFirePreview.Play(_model, _draft, result.Product!);
    }

    internal void PressNativeFireCraft() => _buttons["craft"].EmitSignal(BaseButton.SignalName.Pressed);
    internal bool NativeFireInputsVisible => _page.Visible;
    internal (int Available, int Total) NativePreviewSpirit => (AvailableSpirit, SpiritCapacity);
    internal float NativePreviewEnergy => _forgingOwner?.current_world_stamina ?? WorldStaminaRules.InitialCapacity;
    internal string NativeFireLedger => JsonSerializer.Serialize(new { _session.Stock, Count = _session.Artifacts.Count,
        Attempts = _session.Attempts.Count, _session.Hours, _session.Energy, Draft = _model.Fingerprint(_draft), Revision = _revision });
}
