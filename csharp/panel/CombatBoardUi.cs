using Godot;

internal static class CombatBoardUi
{
    internal static Font Font => GD.Load<Font>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
    internal static Label Text(string text,int font=22,bool muted=false,bool wrap=false)
    {
        var label=new Label{Text=text,MouseFilter=Control.MouseFilterEnum.Ignore,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        label.AddThemeFontOverride("font",Font);label.AddThemeFontSizeOverride("font_size",font);
        label.AddThemeColorOverride("font_color",muted?BattleBoardSkin.MutedInk:CraftingPaperStyle.Ink);
        if(wrap)label.AutowrapMode=TextServer.AutowrapMode.WordSmart;else {label.ClipText=true;label.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;}
        return label;
    }
    internal static Button Button(string text,Action action,bool primary=false,int font=22,bool battle=false)
    {
        var button=new Button{Text=text,CustomMinimumSize=new(0,46)};
        if(battle){BattleBoardSkin.Skin(button,primary);button.AddThemeFontOverride("font",Font);button.AddThemeFontSizeOverride("font_size",font);}
        else PaperButtonFeedback.Apply(button,primary?CraftingPaperStyle.ButtonKind.Primary:CraftingPaperStyle.ButtonKind.Regular,font);
        button.Pressed+=action;return button;
    }
    internal static void Clear(Node parent){foreach(var child in parent.GetChildren()){parent.RemoveChild(child);child.QueueFree();}}
    internal static void LimitLines(Label label,int count)
    {
        label.MaxLinesVisible=count;label.ClipText=true;label.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
        // 截断文字会取消原生Label的最小高度，容器中的名称必须按实际宽度重新量高。
        void Measure()
        {
            var font=label.GetThemeFont("font");var size=label.GetThemeFontSize("font_size");
            var measured=font.GetMultilineStringSize(label.Text,HorizontalAlignment.Left,Math.Max(1,label.Size.X),size,count,
                TextServer.LineBreakFlag.Mandatory|TextServer.LineBreakFlag.WordBound|TextServer.LineBreakFlag.Adaptive);
            var height=Math.Max(font.GetHeight(size),measured.Y);
            if(Math.Abs(label.CustomMinimumSize.Y-height)>.5f)label.CustomMinimumSize=new(label.CustomMinimumSize.X,height);
        }
        label.Resized+=Measure;Measure();
    }
    internal static Control Mini(CombatBoardLayout layout,CombatBoardCatalog catalog,float extent=76)
    {
        var host=new Control{CustomMinimumSize=new(extent,extent),MouseFilter=Control.MouseFilterEnum.Ignore};
        var cell=extent/Math.Max(1,Math.Max(layout.size.X,layout.size.Y));
        host.AddChild(new BattleBoardView{Layout=layout.CopyConfiguration(),Catalog=catalog,CellSize=cell,Size=(Vector2)layout.size*cell,MouseFilter=Control.MouseFilterEnum.Ignore});return host;
    }
}
