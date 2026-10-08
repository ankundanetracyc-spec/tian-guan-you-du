/// <summary>保留编辑器当前占用的旧地点面板源码兼容；东林地入口已由 MainScene 路由到专属副本面板。</summary>
public partial class WorldLocationInteriorPanel
{
    private string _east_woods_selected
    {
        get => _east_woods_current;
        set => _east_woods_current = value;
    }
}
