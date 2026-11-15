using Godot;

/// <summary>百工阵法的全屏操作内页，复用原草图与信号试运行接口。</summary>
public partial class FormationPaperWorkspace : Control
{
    public event Action? Closed;
    public FormationWorkbench Workbench { get; private set; } = null!;
    public string DraftPath { get; set; } = FormationWorkbench.DefaultDraftPath;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; ZIndex = 150;
        var design = FormationPaperChrome.Mount(this);
        var margin = new MarginContainer { Name = "FormationWorkspaceMargin" };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 24); margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 10); margin.AddThemeConstantOverride("margin_bottom", 20);
        design.AddChild(margin);
        Workbench = new FormationWorkbench { Name = "FormationWorkbench", DraftPath = DraftPath };
        Workbench.Closed += Close; margin.AddChild(Workbench);
        if (OS.GetCmdlineUserArgs().Contains("--formation-paper-standalone")) Closed += () => GetTree().Quit();
    }

    public void Close() { Closed?.Invoke(); QueueFree(); }
}
