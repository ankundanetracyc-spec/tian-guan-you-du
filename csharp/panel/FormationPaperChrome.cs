using Godot;

/// <summary>阵法操作页显式复用百工纸墨材质，不改全局主题。</summary>
public static partial class FormationPaperChrome
{
    public static readonly Vector2 DesignSize = new(1672, 941);
    public const string DeskPath = "res://textures/ui/formation_crafting_20261109/desk_clean_v3.png";

    public static Control Mount(Control owner)
    {
        owner.Theme = CraftingPaperStyle.MakeTheme();
        var backdrop = new TextureRect
        {
            Name = "FormationDesk", Texture = FormationBoardCanvas.LoadArt(DeskPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); owner.AddChild(backdrop);
        var design = new Control { Name = "FormationDesign", Size = DesignSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        owner.AddChild(design);
        void Fit()
        {
            var scale = Mathf.Max(.01f, Mathf.Min(owner.Size.X / DesignSize.X, owner.Size.Y / DesignSize.Y));
            design.Scale = Vector2.One * scale; design.Position = (owner.Size - DesignSize * scale) / 2;
        }
        owner.Resized += Fit; Fit(); return design;
    }

    public static PanelContainer Paper(string name, int padding = 18, bool bordered = true)
    {
        var paper = new PanelContainer { Name = name };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(padding, bordered, 1f));
        return paper;
    }

    public static Label Text(string text, int size = 22, bool muted = false)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", muted ? CraftingPaperStyle.Muted : CraftingPaperStyle.Ink);
        return label;
    }

    public static Button Return(string name, Action action)
    {
        var button = new Button { Name = name, Text = "返回", CustomMinimumSize = new Vector2(114, 46) };
        CraftingPaperStyle.ApplyButton(button); button.Pressed += action;
        var marker = new ReturnMarker { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(8, 10) };
        button.AddChild(marker);
        void Fit() => marker.Position = new Vector2(button.Size.X - 22, (button.Size.Y - 10) / 2);
        button.Resized += Fit; Fit(); return button;
    }

    private partial class ReturnMarker : Control
    {
        public override void _Draw() => DrawColoredPolygon([new(0, 0), new(0, 10), new(7, 5)], CraftingPaperStyle.Ink);
    }
}
