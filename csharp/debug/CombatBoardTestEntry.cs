using Godot;

/// <summary>独立人类招式盘试演。只构造测试快照，不读写旅程角色。</summary>
public partial class CombatBoardTestEntry : Control
{
    public const string ScenePath="res://tscn/debug/CombatBoardTest.tscn";
    private static readonly string[] SupportedBooks=["玄枢含章经","斫基损契玄经","百骸衍枝化生宝册","太和感物养元宝册","舒卷天章"];
    private GongfaResource[] _books=[];
    private OptionButton _playerMain=null!,_playerSub=null!,_enemyMain=null!,_enemySub=null!,_enemyCount=null!;
    private Button _start=null!;
    private Label _notice=null!;
    private BattleTransitionManager _transition=null!;

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        _books=GongfaAvailability.EnabledBooks().Where(book=>SupportedBooks.Contains(book.gongfa_name)).ToArray();
        _transition=GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
        if(_transition.has_pending_results())_transition.consume_results();
        if(_transition.has_pending_battle_return_data())_transition.consume_battle_return_data();
        _transition.BriefingCancelled+=OnBriefingCancelled;
        Size=BattleUiArt.Canvas;
        var ground=new TextureRect { ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,
            Texture=BattleUiArt.Texture("res://textures/battle/ink_terrace_multiteam_20261009_v2.png"),Size=Size,MouseFilter=MouseFilterEnum.Ignore };
        AddChild(ground);AddChild(new ColorRect { Size=Size,Color=new Color(0,0,0,.52f),MouseFilter=MouseFilterEnum.Ignore });
        var paper=new BattleBoardSkinPanel { Part="tooltip_panel",Position=new(326,170),Size=new(1020,610) };AddChild(paper);
        Label Text(string value,Vector2 pos,Vector2 dimensions,int font=22)
        {
            var label=new Label { Text=value,Position=pos,Size=dimensions,MouseFilter=MouseFilterEnum.Ignore,AutowrapMode=TextServer.AutowrapMode.WordSmart };
            label.AddThemeFontSizeOverride("font_size",font);label.AddThemeColorOverride("font_color",BattleBoardSkin.Ink);AddChild(label);return label;
        }
        Text("招式盘试演",new(370,204),new(600,42),32);
        var back=MakeButton("返回 ▸",new(1150,204),new(150,42));back.Name="ReturnToMenu";back.Pressed+=()=>GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");AddChild(back);
        Text("我方",new(380,280),new(350,40),26);Text("敌方",new(870,280),new(350,40),26);
        OptionButton Selector(string name,string caption,int x,int y,string initial)
        {
            Text(caption,new(x,y),new(100,38),21);
            var select=new OptionButton { Name=name,Position=new(x+110,y),Size=new(302,40),ClipText=true };
            select.AddItem("无");foreach(var book in _books)select.AddItem(book.gongfa_name);
            var index=Array.FindIndex(_books,book=>book.gongfa_name==initial);select.Select(index+1);
            StartMenu.StyleSettingsOption(select);AddChild(select);return select;
        }
        _playerMain=Selector("PlayerMainBook","主修心法",380,340,"玄枢含章经");
        _playerSub=Selector("PlayerSubBook","辅修心法",380,405,"斫基损契玄经");
        _enemyMain=Selector("EnemyMainBook","主修心法",870,340,"舒卷天章");
        _enemySub=Selector("EnemySubBook","辅修心法",870,405,"太和感物养元宝册");
        BindPair(_playerMain,_playerSub);BindPair(_enemyMain,_enemySub);
        Text("敌方人数",new(870,470),new(110,38),21);
        _enemyCount=new OptionButton { Name="EnemyCount",Position=new(980,470),Size=new(302,40) };
        foreach(var text in new[]{"一人","二人","三人"})_enemyCount.AddItem(text);
        StartMenu.StyleSettingsOption(_enemyCount);AddChild(_enemyCount);
        Text("二十招已开放，化法随所选心法提供。进入战场后可自由配招。\n试演行囊备有两颗回春丹；战斗全程自动，共同回合结束后整理。",new(380,548),new(900,80),20);
        _notice=Text("",new(380,710),new(900,34),18);
        _start=MakeButton("进入战场",new(706,650),new(260,48),true);_start.Name="EnterBattle";_start.Pressed+=BeginTrial;AddChild(_start);
        BattleUiArt.Fit(this);
    }
    public override void _ExitTree() { if(GodotObject.IsInstanceValid(_transition))_transition.BriefingCancelled-=OnBriefingCancelled; }
    public override void _Process(double delta)=>BattleUiArt.Fit(this);
    private void OnBriefingCancelled(Godot.Collections.Dictionary _) { _start.Disabled=false;_notice.Text=""; }
    private void BindPair(OptionButton main,OptionButton sub)
    {
        void Refresh(OptionButton changed,OptionButton other)
        {
            if(changed.Selected>0&&changed.Selected==other.Selected)other.Select(0);
            for(var i=1;i<other.ItemCount;i++)other.SetItemDisabled(i,i==changed.Selected);
            for(var i=1;i<changed.ItemCount;i++)changed.SetItemDisabled(i,i==other.Selected);
        }
        main.ItemSelected+=_=>Refresh(main,sub);sub.ItemSelected+=_=>Refresh(sub,main);Refresh(main,sub);
    }
    private string BookPath(OptionButton choice)=>(uint)(choice.Selected-1)<(uint)_books.Length?_books[choice.Selected-1].ResourcePath:"";
    private static Button MakeButton(string text,Vector2 position,Vector2 size,bool primary=false)
    {
        var button=new Button { Text=text,Position=position,Size=size,CustomMinimumSize=size };BattleBoardSkin.Skin(button,primary);return button;
    }
    public void BeginTrial()
    {
        if(_start.Disabled||_transition.has_open_flow_view()||_transition.is_flow_transition_active())return;
        try
        {
            var player=CreateSide("试演修士",1,0,BookPath(_playerMain),BookPath(_playerSub));
            var foes=new Godot.Collections.Array();var scenes=new Godot.Collections.Array<PackedScene>();
            var character=GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            for(var i=0;i<=_enemyCount.Selected;i++)
            {
                foes.Add(CreateSide($"对练者{(i+1)}",2,i+1,BookPath(_enemyMain),BookPath(_enemySub)));scenes.Add(character);
            }
            _start.Disabled=true;_notice.Text="";
            _transition.set_battle_return_context(new Godot.Collections.Dictionary { ["kind"]="combat_board_test",["is_training"]=true,["allow_prebattle_cancel"]=true,["title"]="招式盘试演" });
            _transition.start_team_battle([character],scenes,[player],foes,ScenePath);
        }
        catch(Exception error) { _start.Disabled=false;_notice.Text="暂时无法开始试演。";GD.PushError("CombatBoardTestEntry: "+error); }
    }
    public BattleEntityData CreateSide(string name,int faction,int seed,string main,string sub)
    {
        var data=NpcCombatBuildPlanner.TestLearning(_books);
        data.data_initialized=true;data.entity_name=name;data.main_gongfa_path=main;data.max_sub_slots=1;
        data.sub_gongfa_paths=sub.Length>0&&sub!=main?[sub]:[];
        data.max_health=100;data.current_health=100;data.max_stamina=6;data.current_stamina=0;
        data.appearance_seed=1200+seed;data.current_age=20;data.gender=0;
        data.body_proficiency=data.magic_proficiency=data.sword_proficiency=6;
        data.inventory_item_paths=["res://resources/entities/items/alchemy/huichun_1.tres"];
        data.inventory_item_counts=[2];data.inventory_item_positions=[0];
        foreach(var book in _books.Where(book=>book.ResourcePath==sub))
            foreach(var chapter in book.get_sorted_chapters())data.equipped_gongfa_chapter_routes[book.ResourcePath+":"+chapter.get_id()]="right";
        var actor=new Character { Name="TrialSnapshot" };AddChild(actor);
        try
        {
            actor.load_from_data(data);actor.entity_type=WorldEntity.EntityType.CHARACTER;actor.unique_id="combat_board_test_"+seed;actor.faction=faction;
            actor.current_health=actor.max_health;actor.current_stamina=0;
            var snapshot=actor.get_snapshot_data();
            snapshot.combat_board_layout=DefaultLayout(snapshot.combat_board_catalog!,main,sub);
            snapshot.test_mana_cap_override=3;snapshot.max_mana=snapshot.current_mana=3;
            return snapshot;
        }
        finally { RemoveChild(actor);actor.Free(); }
    }
    private CombatBoardLayout DefaultLayout(CombatBoardCatalog catalog,string main,string sub)
    {
        var xuan=_books.Any(book=>book.gongfa_name=="玄枢含章经"&&(book.ResourcePath==main||book.ResourcePath==sub));
        var sword=!xuan&&_books.Any(book=>book.gongfa_name=="舒卷天章"&&(book.ResourcePath==main||book.ResourcePath==sub));
        var layout=new CombatBoardLayout();
        void Put(string name,int x,int y)
        {
            if(catalog.skills.FirstOrDefault(skill=>skill.skill_name==name)is not {} skill)return;
            layout.placements.Add(new() { placement_id=Guid.NewGuid().ToString("N"),source_key=skill.get_board_skill_key(),cell=new(x,y) });
        }
        Put(sword?"养锋":"应敌",0,0);Put("架招",1,0);Put(sword?"斩剑":"痛击",0,1);Put(sword?"尽锋":"聚炁",0,2);
        if(xuan)layout.placements.Add(new() { placement_id="heart:xuan_shu:indicator",source_key="heart:xuan_shu",kind=CombatBoardPlacement.ObjectKind.ZhouTianIndicator,locked=true,cell=new(2,2),rewrite_direction=2 });
        return layout;
    }
}
