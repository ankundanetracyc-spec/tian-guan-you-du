using Godot;

/// <summary>人物页心境盘，只表现既有落点，不生成情绪、迁移旧轴或结算效果。</summary>
public partial class MoodDiscControl : Control
{
    // 圆周名称表示情绪最强时的形态；内侧仍属于同一情绪，程度逐渐减轻。
    // 这套分类目前只用于界面提案，不据此生成真实心绪或玩法效果。
    public static readonly string[] MoodNames = ["狂喜", "渴求", "焦灼", "惊惧", "悲恸", "暴怒", "怨恨", "绝望"];
    private static readonly string[] InnerNames = ["欣喜", "渴望", "焦虑", "恐惧", "悲伤", "愤怒", "怨恨", "消沉"];
    private static readonly string[] Notes = ["持续的欣喜，向圆周渐强，表现为狂喜。", "持续渴望达成或得到某事，向外渐成强烈渴求。", "对尚未发生的风险持续不安，向外渐成焦灼。", "面对明确危险或创伤持续害怕，向外渐成惊惧。", "对失去与离别持续难过，向外渐成悲恸。", "对不公或阻碍持续不平，向外渐成暴怒。", "持续无法释怀的怨愤，向外渐成深重怨恨。", "对未来持续低落、失去盼望，向外渐成绝望。"];
    public event Action<string>? HoverTextChanged;
    public event Action? HoverEnded;
    public string StateReason { get; set; } = "暂无已确认的长期心绪记录。";
    private readonly Label[] _names = new Label[8];
    private ColorRect? _wash;
    private Label? _centerLabel;
    private ShaderMaterial? _material;
    private Vector2? _position;
    private float _phase, _redrawClock;
    private Vector2 _displayPosition;
    private Tween? _moveTween;
    private int _hover = -1;
    private Vector2 Center => Size * .5f;
    private float Radius => Mathf.Max(20, Mathf.Min(Size.X-110, Size.Y-90)*.5f);
    public string CurrentName => _position is null ? "未详" : _position.Value.Length() < .001f ? "无心绪" : InnerNames[Sector(_position.Value)];

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(420, 440);
        MouseFilter = MouseFilterEnum.Pass;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/mood_ink_disc.gdshader") };
        _wash = new ColorRect { Name="WatercolorWash", Color=Colors.White, Material=_material, MouseFilter=MouseFilterEnum.Ignore, ShowBehindParent=true };
        AddChild(_wash);
        for (int i=0;i<8;i++)
        {
            _names[i] = new Label { Text=MoodNames[i], HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, MouseFilter=MouseFilterEnum.Ignore };
            _names[i].AddThemeFontSizeOverride("font_size",22);
            _names[i].AddThemeColorOverride("font_color",new Color("#494439"));
            AddChild(_names[i]);
        }
        _centerLabel = new Label { Text="无心绪", HorizontalAlignment=HorizontalAlignment.Center, MouseFilter=MouseFilterEnum.Ignore };
        _centerLabel.AddThemeFontSizeOverride("font_size",18);
        _centerLabel.AddThemeColorOverride("font_color",new Color("#756c59"));
        AddChild(_centerLabel);
        Resized += LayoutDisc;
        MouseExited += ClearHover;
        VisibilityChanged += () => { if (!IsVisibleInTree()) ClearHover(); };
        LayoutDisc();
    }

    public void ShowState(Vector2? position)
    {
        if (position is Vector2 supplied && !supplied.IsFinite()) position=null;
        bool animate=_position is not null && position is not null && IsInsideTree() && IsVisibleInTree();
        _moveTween?.Kill();
        _position = position?.LimitLength(1);
        if (animate)
        {
            _moveTween=CreateTween();
            _moveTween.TweenMethod(Callable.From<Vector2>(p=>{ _displayPosition=p; QueueRedraw(); }),_displayPosition,_position!.Value,.8)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        else _displayPosition=_position ?? Vector2.Zero;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _position is null) return;
        _phase += (float)delta;
        _redrawClock += (float)delta;
        if (_redrawClock < .05f) return;
        _redrawClock=0; QueueRedraw();
    }

    private void LayoutDisc()
    {
        if (_wash is null || _centerLabel is null) return;
        float radius=Radius;
        _wash.Position=Center-Vector2.One*radius;
        _wash.Size=Vector2.One*radius*2;
        for(int i=0;i<8;i++)
        {
            float angle=-Mathf.Pi*.5f+i*Mathf.Tau/8;
            _names[i].Size=new Vector2(76,34);
            _names[i].Position=Center+Vector2.FromAngle(angle)*(radius+24)-_names[i].Size*.5f;
        }
        _centerLabel.Size=new Vector2(90,30);
        _centerLabel.Position=Center+new Vector2(-45,25);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent ev)
    {
        if(ev is not InputEventMouseMotion motion) return;
        var p=(motion.Position-Center)/Radius;
        bool marker=_position is not null && motion.Position.DistanceTo(Center+_displayPosition*Radius*.93f)<22;
        bool center=motion.Position.DistanceTo(Center)<22;
        int sector=!marker && !center && p.Length()<1.16f ? Sector(p) : -1;
        SetHover(sector);
        string text=marker ? $"当前心境：{CurrentName}\n{StateReason}" : center ? "无心绪\n没有持续占主导的情绪，当前心境稳定。" : sector>=0 ? InnerNames[sector]+"\n"+Notes[sector]+"\n越靠近圆心，这份心绪越轻。" : "";
        // 页面统一使用自己的悬停框，避免原生提示框与宣纸页面风格割裂。
        TooltipText="";
        if(text.Length>0) HoverTextChanged?.Invoke(text); else HoverEnded?.Invoke();
    }

    private void ClearHover() { SetHover(-1); HoverEnded?.Invoke(); }

    private void SetHover(int sector)
    {
        if(_hover==sector) return;
        _hover=sector;
        _material?.SetShaderParameter("hover_sector",(float)sector);
        for(int i=0;i<8;i++)
            if(_names[i] is not null) _names[i].AddThemeColorOverride("font_color",i==sector ? new Color("#23544b") : new Color("#494439"));
    }

    private static int Sector(Vector2 p) => Mathf.PosMod(Mathf.FloorToInt((p.Angle()+Mathf.Pi*.5f)/ (Mathf.Tau/8)+.5f),8);

    public override void _Draw()
    {
        float radius=Radius;
        // 不规则的断墨外缘、细金短笔，避免仪表盘式的实线圆框。
        for(int i=0;i<176;i++)
        {
            float a=i*Mathf.Tau/176;
            if(Mathf.Sin(i*1.73f)>.66f) continue;
            float r=radius*(.985f+.007f*Mathf.Sin(a*11)+.004f*Mathf.Sin(a*29));
            DrawLine(Center+Vector2.FromAngle(a)*r,Center+Vector2.FromAngle(a+.026f)*r,new Color(.39f,.34f,.24f,.15f+.12f*Mathf.Abs(Mathf.Sin(a*4))),.8f,true);
        }
        if(_position is null) return;
        Vector2 point=_displayPosition;
        Vector2 at=Center+point*radius*.93f;
        float breath=.5f+.5f*Mathf.Sin(_phase*.8f);
        for(int i=12;i>0;i--) DrawCircle(at,8+i*1.2f,new Color(.30f,.41f,.36f,.007f*(1+breath)),true,-1,true);
        // 墨珠由错开的柔墨笔触相拥，位置始终固定；只有明暗轻微呼吸。
        for(int i=0;i<42;i++)
        {
            float a=i*Mathf.Tau/42;
            float r=10+.9f*Mathf.Sin(a*3+.4f)+.6f*Mathf.Sin(a*7);
            var color=new Color(.20f,.29f,.26f,.40f+.22f*Mathf.Sin(a+1)*Mathf.Sin(a+1));
            DrawLine(at+Vector2.FromAngle(a)*r,at+Vector2.FromAngle(a+.16f)*(r-.4f),color,2.0f,true);
        }
        DrawCircle(at,6,new Color("#3c5147"),true,-1,true);
        DrawArc(at+new Vector2(-.7f,-.7f),5.5f,3.6f,5.5f,20,new Color(.80f,.72f,.48f,.66f+breath*.14f),1.3f,true);
        DrawCircle(at+new Vector2(-1.9f,-2.4f),1.5f,new Color("#d7c99c"),true,-1,true);
    }
}
