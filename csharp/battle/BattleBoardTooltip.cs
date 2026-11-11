using Godot;

/// <summary>招式盘内的统一只读悬停，文本仍来自实际技能与本场账本。</summary>
public partial class BattleBoardTooltip : PanelContainer
{
    public BattleBoardTooltip(string text)
    {
        MouseFilter=MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel",new StyleBoxEmpty { ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=15,ContentMarginBottom=15 });
        var label=new Label { Text=text, AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(360,0),MouseFilter=MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",17);label.AddThemeColorOverride("font_color",BattleBoardSkin.Ink);AddChild(label);
    }
    public BattleBoardTooltip() : this("") { }
    public override void _Ready() => Resized+=QueueRedraw;
    public override void _Draw() => BattleBoardSkin.DrawPanel(this,"tooltip_panel",new Rect2(Vector2.Zero,Size));
}
