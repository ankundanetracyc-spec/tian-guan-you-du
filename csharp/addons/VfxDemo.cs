using Godot;

/// <summary>保留 VFX 示例场景的 C# 启动入口；可选插件未启用时不影响游戏运行。</summary>
public partial class VfxDemo : Node2D
{
    public override void _Ready() => GD.Print("VFX demo scene ready.");
}
