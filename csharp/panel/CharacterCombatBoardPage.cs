using Godot;

/// <summary>人物战斗目录只列学习与持有事实；配置放独立全屏暗幕。</summary>
public partial class CharacterCombatBoardPage : PanelContainer
{
    private Character _actor=null!;
    private GridContainer _cards=null!;
    private LineEdit _search=null!;
    private Label _summary=null!;
    private Button _prepare=null!;
    private readonly Dictionary<string,Button> _tabs=[];
    private string _category="招式";
    private List<CombatBoardOwnedSources.Entry> _entries=[];
    private CanvasLayer? _overlay;
    private CombatBoardLayout? _draft;
    private bool _editable;
    public Character Actor=>_actor;
    public void Setup(Character actor,bool editable,StyleBox paper)
    {
        Name="CharacterCombatBoardPage";_actor=actor;_editable=editable;Theme=CraftingPaperStyle.MakeTheme();
        AddThemeStyleboxOverride("panel",paper);
        var root=new VBoxContainer();root.AddThemeConstantOverride("separation",18);AddChild(root);
        var header=new HBoxContainer();header.AddThemeConstantOverride("separation",20);root.AddChild(header);
        header.AddChild(CombatBoardUi.Text("已学与持有",29));
        _prepare=CombatBoardUi.Button("配招",OpenEditor,true,24);_prepare.Name="OpenCharacterPreparation";
        _prepare.CustomMinimumSize=new(140,50);_prepare.Disabled=!editable;header.AddChild(_prepare);
        var categories=new HBoxContainer();categories.AddThemeConstantOverride("separation",12);root.AddChild(categories);
        foreach(var name in new[]{"招式","化法","丹药","符箓","法宝"})
        {
            var tab=CombatBoardUi.Button(name,()=>SelectCategory(name),true,24);tab.Name="OwnedCategory"+name;
            tab.SizeFlagsHorizontal=SizeFlags.ExpandFill;tab.CustomMinimumSize=new(0,52);categories.AddChild(tab);_tabs[name]=tab;
        }
        var filters=new HBoxContainer();filters.AddThemeConstantOverride("separation",18);root.AddChild(filters);
        _search=new LineEdit{Name="OwnedSourceSearch",PlaceholderText="查找名称或来源…",CustomMinimumSize=new(370,46)};
        CraftingPaperStyle.ApplyInput(_search,21);filters.AddChild(_search);_search.TextChanged+=_=>Render();
        _summary=CombatBoardUi.Text("",20,true);filters.AddChild(_summary);
        var scroll=new ScrollContainer{Name="OwnedSourceScroll",SizeFlagsVertical=SizeFlags.ExpandFill,
            HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};root.AddChild(scroll);
        _cards=new GridContainer{Name="OwnedSourceCards",Columns=3,SizeFlagsHorizontal=SizeFlags.ExpandFill};
        _cards.AddThemeConstantOverride("h_separation",16);_cards.AddThemeConstantOverride("v_separation",16);scroll.AddChild(_cards);
        Resized+=()=>{if(_cards is not null)_cards.Columns=Size.X<850?2:3;};
        VisibilityChanged+=()=>{if(!IsVisibleInTree()&&GodotObject.IsInstanceValid(_overlay))_overlay!.GetChildren().OfType<CharacterCombatBoardEditor>().FirstOrDefault()?.Close();};Refresh();
    }
    public void Refresh()
    {
        if(!GodotObject.IsInstanceValid(_actor))return;
        _entries=CombatBoardOwnedSources.Read(_actor);
        foreach(var (name,button) in _tabs)
        {
            button.Text=$"{name} {_entries.Count(e=>e.Category==name)}";
            PaperButtonFeedback.Apply(button,CraftingPaperStyle.ButtonKind.Primary,24,selected:name==_category);
        }
        Render();
    }
    private void SelectCategory(string category){_category=category;Refresh();}
    private void Render()
    {
        CombatBoardUi.Clear(_cards);
        var query=_search.Text.Trim();var shown=_entries.Where(e=>e.Category==_category)
            .Where(e=>e.Matches(query)).ToArray();
        _summary.Text=$"共{shown.Length}项";
        foreach(var entry in shown)
        {
            var card=new PanelContainer{Name="OwnedCard",CustomMinimumSize=new(0,162),SizeFlagsHorizontal=SizeFlags.ExpandFill};
            card.SetMeta("source_key",entry.Key);card.AddThemeStyleboxOverride("panel",CraftingPaperStyle.Paper(padding:17,brightness:1));_cards.AddChild(card);
            var row=new HBoxContainer();row.AddThemeConstantOverride("separation",16);card.AddChild(row);
            row.AddChild(new CombatBoardOwnedIcon{Icon=CombatBoardOwnedSources.Load(entry.Icon),Shape=entry.Shape,
                CustomMinimumSize=new(94,94),SizeFlagsVertical=SizeFlags.ShrinkCenter,MouseFilter=MouseFilterEnum.Ignore});
            var content=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};content.AddThemeConstantOverride("separation",8);row.AddChild(content);
            content.AddChild(CombatBoardUi.Text(entry.Name,25,wrap:true));content.AddChild(CombatBoardUi.Text(entry.Source,18,true));
            content.AddChild(CombatBoardUi.Text(entry.Detail,18,true,true));
            if(!entry.Configurable&&!entry.Detail.Contains("暂不可配招"))content.AddChild(CombatBoardUi.Text("当前不可配招",17,true));
            HoverInfoManager.SetInfo(card,entry.Hover);
        }
        if(shown.Length==0)
        {
            var empty=CombatBoardUi.Text(query.Length>0?"没有符合条件的内容。":$"暂无已{(_category is "招式" or "化法"?"学":"持有")}的{_category}。",23,true,true);
            empty.CustomMinimumSize=new(300,150);_cards.AddChild(empty);
        }
    }
    public void OpenEditor()
    {
        if(!_editable||!CombatBoardPreparationService.CanManage(_actor)||GodotObject.IsInstanceValid(_overlay))return;
        _overlay=new CanvasLayer{Name="CharacterPreparationOverlay",Layer=100};AddChild(_overlay);
        var editor=new CharacterCombatBoardEditor();_overlay.AddChild(editor);
        editor.Setup(_actor,_draft,()=>
        {
            if(GetTree().CurrentScene is MainScene main)main.notify_combat_board_configuration_changed(_actor);
            Refresh();
        },draft=>{_draft=draft;_overlay?.QueueFree();_overlay=null;Refresh();if(IsVisibleInTree())_prepare.GrabFocus();});
    }
    public override void _ExitTree(){if(GodotObject.IsInstanceValid(_overlay))_overlay!.QueueFree();}
}
