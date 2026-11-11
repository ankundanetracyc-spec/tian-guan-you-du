using Godot;

/// <summary>38选定样式的原图区域；只做原生绘制，不改写素材像素。</summary>
internal static class BattleBoardSkin
{
    internal const string Root = "res://textures/ui/combat_board_20261107_v1/";
    internal static readonly Color Ink = new("38342e"), MutedInk = new("6e685d"), PaperText = new("eee6d5");
    private static readonly Dictionary<string, AtlasTexture> Parts = [];
    private static readonly Dictionary<string, (float[] Source, float[] Display)> Slices = new()
    {
        ["source_panel"] = ([64,64,64,64], [23,23,23,23]),
        ["tooltip_panel"] = ([48,48,48,48], [12,12,12,12]),
        ["resource_frame"] = ([100,80,100,80], [25,20,25,20]),
        ["primary_normal"] = ([42,24,42,24], [14,8,14,8]),
        ["primary_hover"] = ([42,24,42,24], [14,8,14,8]),
        ["secondary_normal"] = ([42,24,42,24], [14,8,14,8]),
        ["secondary_hover"] = ([42,24,42,24], [14,8,14,8])
    };

    internal static AtlasTexture Part(string name)
    {
        if (Parts.TryGetValue(name, out var texture) && GodotObject.IsInstanceValid(texture)) return texture;
        return Parts[name] = GD.Load<AtlasTexture>(Root + "atlas/" + name + ".tres");
    }

    internal static void DrawPanel(CanvasItem target, string name, Rect2 rect, Color? tint = null)
    {
        var texture = Part(name);
        if (!Slices.TryGetValue(name, out var slice)) { target.DrawTextureRect(texture, rect, false, tint); return; }
        var source = texture.Region;
        var s = slice.Source; var d = slice.Display;
        var k = Mathf.Min(1, Mathf.Min(rect.Size.X / (d[0] + d[2]), rect.Size.Y / (d[1] + d[3])));
        float[] xs = [source.Position.X, source.Position.X+s[0], source.End.X-s[2], source.End.X];
        float[] ys = [source.Position.Y, source.Position.Y+s[1], source.End.Y-s[3], source.End.Y];
        float[] xd = [rect.Position.X, rect.Position.X+d[0]*k, rect.End.X-d[2]*k, rect.End.X];
        float[] yd = [rect.Position.Y, rect.Position.Y+d[1]*k, rect.End.Y-d[3]*k, rect.End.Y];
        for (var row=0; row<3; row++) for (var col=0; col<3; col++)
            target.DrawTextureRectRegion(texture.Atlas,
                new Rect2(xd[col],yd[row],xd[col+1]-xd[col],yd[row+1]-yd[row]),
                new Rect2(xs[col],ys[row],xs[col+1]-xs[col],ys[row+1]-ys[row]), tint);
    }

    internal static void Skin(Button button, bool primary = false)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=4,ContentMarginBottom=4 });
        var backdrop = new BattleBoardSkinPanel { Name="Material", ShowBehindParent=true, MouseFilter=Control.MouseFilterEnum.Ignore };
        button.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        void Refresh()
        {
            var active = primary || button.ButtonPressed;
            backdrop.Part = (active ? "primary" : "secondary") + (button.IsHovered() || button.HasFocus() || button.ButtonPressed ? "_hover" : "_normal");
            backdrop.Modulate = button.Disabled ? new Color(1,1,1,.58f) : Colors.White;
            var ink = active ? PaperText : Ink;
            foreach(var state in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color"}) button.AddThemeColorOverride(state,ink);
            button.AddThemeColorOverride("font_disabled_color",MutedInk); backdrop.QueueRedraw();
        }
        button.MouseEntered += Refresh; button.MouseExited += Refresh;
        button.FocusEntered += Refresh; button.FocusExited += Refresh;
        button.Toggled += _ => Refresh(); button.Resized += Refresh;
        button.AddThemeFontSizeOverride("font_size",20); Refresh();
    }

    internal static void DrawItemFrame(CanvasItem target,Rect2 rect,float edge,Color? tint=null)
    {
        var texture=Part("focus_outline");var source=texture.Region;const float cut=20;
        edge=Mathf.Min(edge,Mathf.Min(rect.Size.X,rect.Size.Y)/2);
        float[] xs=[source.Position.X,source.Position.X+cut,source.End.X-cut,source.End.X];
        float[] ys=[source.Position.Y,source.Position.Y+cut,source.End.Y-cut,source.End.Y];
        float[] dx=[rect.Position.X,rect.Position.X+edge,rect.End.X-edge,rect.End.X];
        float[] dy=[rect.Position.Y,rect.Position.Y+edge,rect.End.Y-edge,rect.End.Y];
        for(var row=0;row<3;row++)for(var col=0;col<3;col++)
        {
            if(row==1&&col==1)continue;
            target.DrawTextureRectRegion(texture.Atlas,new Rect2(dx[col],dy[row],dx[col+1]-dx[col],dy[row+1]-dy[row]),
                new Rect2(xs[col],ys[row],xs[col+1]-xs[col],ys[row+1]-ys[row]),tint);
        }
    }

    internal static void Skin(LineEdit input)
    {
        var empty = new StyleBoxEmpty { ContentMarginLeft=12,ContentMarginRight=12,ContentMarginTop=5,ContentMarginBottom=5 };
        input.AddThemeStyleboxOverride("normal",empty); input.AddThemeStyleboxOverride("focus",empty);
        input.AddThemeStyleboxOverride("read_only",empty);
        input.AddThemeColorOverride("font_color",Ink); input.AddThemeColorOverride("font_placeholder_color",MutedInk);
        input.AddThemeColorOverride("caret_color",Ink); input.AddThemeFontSizeOverride("font_size",20);
        var backdrop = new BattleBoardSkinPanel { Part="tooltip_panel", ShowBehindParent=true, MouseFilter=Control.MouseFilterEnum.Ignore };
        input.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    /// <summary>输入真实可用格；同一份多格对象内部不画接缝。</summary>
    internal static void DrawCells(CanvasItem target, IEnumerable<Vector2I> validCells, float size,
        IReadOnlyDictionary<Vector2I,string>? groups = null)
    {
        var cells = validCells.ToHashSet();
        bool Has(int x,int y) => cells.Contains(new(x,y));
        bool Same(Vector2I a,Vector2I b) => groups is not null && groups.TryGetValue(a,out var id) && groups.GetValueOrDefault(b)==id;
        void Edge(Vector2 at, bool vertical, bool seam=false)
        {
            var thickness=seam?1.1f:Mathf.Max(2, size*.035f);
            target.DrawSetTransform(at,vertical?Mathf.Pi/2:0);
            target.DrawTextureRect(Part("outer_edge"),new Rect2(0,-thickness/2,size,thickness),false,new Color(1,1,1,seam?.38f:1));
            target.DrawSetTransform(Vector2.Zero);
        }
        foreach(var c in cells) target.DrawTextureRect(Part("cell_face"),new Rect2((Vector2)c*size,Vector2.One*size),false);
        var vertices=new HashSet<Vector2I>();
        foreach(var c in cells)
        {
            var p=(Vector2)c*size;
            if(!Has(c.X,c.Y-1))Edge(p,false);
            if(!Has(c.X-1,c.Y))Edge(p,true);
            if(!Has(c.X+1,c.Y))Edge(p+new Vector2(size,0),true);
            else if(!Same(c,c+Vector2I.Right))Edge(p+new Vector2(size,0),true,true);
            if(!Has(c.X,c.Y+1))Edge(p+new Vector2(0,size),false);
            else if(!Same(c,c+Vector2I.Down))Edge(p+new Vector2(0,size),false,true);
            foreach(var offset in new[]{Vector2I.Zero,Vector2I.Right,Vector2I.Down,Vector2I.One})vertices.Add(c+offset);
        }
        foreach(var v in vertices)
        {
            bool[] q=[Has(v.X,v.Y),Has(v.X-1,v.Y),Has(v.X-1,v.Y-1),Has(v.X,v.Y-1)];
            var count=q.Count(b=>b); if(count is not (1 or 3))continue;
            var outer=count==1; var turn=outer?Array.IndexOf(q,true):new[]{1,2,3,0}[Array.IndexOf(q,false)];
            var cornerSize=size*.19f;var anchor=outer?new Vector2(.095f,.095f):new Vector2(.095f,.905f);
            target.DrawSetTransform((Vector2)v*size,turn*Mathf.Pi/2);
            target.DrawTextureRect(Part(outer?"outer_corner":"inner_corner"),new Rect2(-anchor*cornerSize,Vector2.One*cornerSize),false);
            target.DrawSetTransform(Vector2.Zero);
        }
    }
}
