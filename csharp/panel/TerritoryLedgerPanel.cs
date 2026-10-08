using Godot;

/// <summary>人事、案卷与见闻的独立页面，避免把具体内容挤回辖地总览。</summary>
public partial class TerritoryLedgerPanel : Control
{
    public event Action? Closed;
    public event Action<string, bool>? SituationResolved;
    public event Action? HireRequested;
    private QingxiTerritoryState? _state;
    private string _mode = "personnel";
    private VBoxContainer? _body;
    private HBoxContainer? _recordTabs;
    private readonly Dictionary<string, Button> _recordButtons = [];
    public bool IsPersonnel => _mode == "personnel";
    public void close() { Hide(); Closed?.Invoke(); }
    public override void _Ready(){MouseFilter=MouseFilterEnum.Stop;ZIndex=100;Build();Hide();}
    public void bind_state(QingxiTerritoryState state){if(_state is not null)_state.TerritoryChanged-=Refresh;_state=state;_state.TerritoryChanged+=Refresh;}
    public void open(string mode){_mode=mode;Show();MoveToFront();Refresh();}
    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new TextureRect{Texture=GD.Load<Texture2D>("res://textures/ui/territory/territory_ink_backdrop_v1.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=MouseFilterEnum.Ignore};backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(backdrop);
        var shade=new ColorRect{Color=new Color(.03f,.025f,.02f,.38f),MouseFilter=MouseFilterEnum.Stop};shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(shade);
        var panel=new PanelContainer();panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);panel.OffsetLeft=18;panel.OffsetTop=16;panel.OffsetRight=-18;panel.OffsetBottom=-16;panel.AddThemeStyleboxOverride("panel",Style(new Color(.115f,.1f,.07f,.99f),new Color(.7f,.56f,.28f,.9f)));AddChild(panel);var parchment=new TextureRect{Texture=GD.Load<Texture2D>("res://textures/ui/territory/territory_parchment_card_v1.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=MouseFilterEnum.Ignore,Modulate=new Color(1,1,1,.2f)};panel.AddChild(parchment);
        var margin=new MarginContainer();foreach(var p in new[]{"margin_left","margin_right","margin_top","margin_bottom"})margin.AddThemeConstantOverride(p,42);panel.AddChild(margin);var box=new VBoxContainer();box.AddThemeConstantOverride("separation",18);margin.AddChild(box);
        var head=new HBoxContainer();box.AddChild(head);var title=new Label{Name="Title",SizeFlagsHorizontal=SizeFlags.ExpandFill};title.AddThemeFontSizeOverride("font_size",38);title.Modulate=new Color(.96f,.88f,.66f);head.AddChild(title);var close=new Button{Text="返回",CustomMinimumSize=new Vector2(110,52)};close.AddThemeFontSizeOverride("font_size",20);close.Pressed+=()=>{Hide();Closed?.Invoke();};head.AddChild(close);
        _recordTabs = new HBoxContainer(); _recordTabs.AddThemeConstantOverride("separation", 12); box.AddChild(_recordTabs);
        foreach (var (id, label) in new[] { ("journal", "山川见闻"), ("cases", "案卷台") })
        {
            var tab = Button(label); tab.ToggleMode = true; tab.CustomMinimumSize = new Vector2(180, 48);
            tab.Pressed += () => open(id); _recordTabs.AddChild(tab); _recordButtons[id] = tab;
        }
        var scroll=new ScrollContainer{SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};box.AddChild(scroll);_body=new VBoxContainer();_body.AddThemeConstantOverride("separation",12);_body.SizeFlagsHorizontal=SizeFlags.ExpandFill;scroll.AddChild(_body);
    }
    private void Refresh()
    {
        if (_recordTabs is not null) _recordTabs.Visible = !IsPersonnel;
        foreach (var (id, tab) in _recordButtons) tab.SetPressedNoSignal(id == _mode);
        if(_body is null)return;foreach(var child in _body.GetChildren())child.QueueFree();var title=GetNodeOrNull<Label>("PanelContainer/MarginContainer/VBoxContainer/HBoxContainer/Title");if(title is not null)title.Text=_mode=="cases"?"青溪里 · 案卷台":_mode=="journal"?"山川见闻":"青溪里 · 人事";
        if(_mode=="cases")Cases();else if(_mode=="journal")Journal();else Personnel();
    }
    private void Personnel()
    {
        Add("名册　｜　招募　｜　派任　｜　家计",22,new Color(.96f,.84f,.53f));Add("外姓人会与族人一样进入角色名册；此页负责招募、查看属性、安排去向与维持家计。",16,Colors.White);
        var hire=Button("招募外姓人（安家物资：粮 12、木 2）");hire.Pressed+=()=>HireRequested?.Invoke();_body!.AddChild(hire);
        Add("当前常驻岗位",21,new Color(.96f,.84f,.53f));if(_state is not null)foreach(var office in _state.offices)Add($"{office.title}　主事：{(string.IsNullOrEmpty(office.lead)?"待派任":office.lead)}　｜　副手：{(string.IsNullOrEmpty(office.deputy)?"待派任":office.deputy)}\n{office.note}",17,new Color(.9f,.86f,.73f));
    }
    private void Cases()
    {
        Add("待办案卷　｜　处理中　｜　已归档",22,new Color(.96f,.84f,.53f));var banner=new TextureRect{Texture=GD.Load<Texture2D>("res://textures/ui/territory/territory_case_banner_v1.png"),CustomMinimumSize=new Vector2(0,120),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=MouseFilterEnum.Ignore,Modulate=new Color(1,1,1,.65f)};_body!.AddChild(banner);if(_state is null||_state.situations.Count==0){Add("暂无待决案卷。寻常事务正由常驻体系处理。",18,Colors.White);return;}foreach(var item in _state.situations){var frame=new VBoxContainer();frame.AddChild(Label(item.title,22,new Color(.97f,.8f,.52f)));frame.AddChild(Label(item.detail,17,Colors.White));var row=new HBoxContainer();var left=Button(item.primary);left.SizeFlagsHorizontal=SizeFlags.ExpandFill;left.Pressed+=()=>SituationResolved?.Invoke(item.id,true);row.AddChild(left);var right=Button(item.secondary);right.SizeFlagsHorizontal=SizeFlags.ExpandFill;right.Pressed+=()=>SituationResolved?.Invoke(item.id,false);row.AddChild(right);frame.AddChild(row);_body.AddChild(frame);}
    }
    private void Journal()
    {
        Add("传闻　｜　异象　｜　人物　｜　物产",22,new Color(.96f,.84f,.53f));Add("兽潮吞村",25,new Color(.97f,.8f,.52f));var art=new TextureRect{Texture=GD.Load<Texture2D>("res://textures/ui/territory/rumor_beast_swarmed_hamlet_v1.png"),CustomMinimumSize=new Vector2(0,250),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=MouseFilterEnum.Ignore};_body!.AddChild(art);Add("来源：过路樵夫口述　｜　可信度：中　｜　关联地点：青溪里西北\n有小聚居地疑为兽群所吞没，暂未见幸存者。此事已录入见闻簿，可在往后外务中标记追查。",18,Colors.White);Add("见闻只记录线索与来源；真正需要家主取舍的风险，会另行进入案卷台。",16,new Color(.72f,.86f,.74f));
    }
    private void Add(string text,int size,Color color)=>_body!.AddChild(Label(text,size,color));
    private static Label Label(string text,int size,Color color){var label=new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart};label.AddThemeFontSizeOverride("font_size",size);label.Modulate=color;return label;}
    private static Button Button(string text)=>new(){Text=text,CustomMinimumSize=new Vector2(0,46)};
    private static StyleBoxFlat Style(Color bg,Color border)=>new(){BgColor=bg,BorderColor=border,BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=9,CornerRadiusTopRight=9,CornerRadiusBottomLeft=9,CornerRadiusBottomRight=9};
}
