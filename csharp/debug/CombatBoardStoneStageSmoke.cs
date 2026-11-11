using Godot;
using System.Text.Json;
using System.Security.Cryptography;

/// <summary>固定石地小人物的真实招式盘显示与执行验收，使用隔离人物。</summary>
public partial class CombatBoardStoneStageSmoke : Node
{
    private readonly List<string> _checks=[];
    private readonly List<string> _captures=[];
    private BattleManager _manager=null!;
    private Node2D _scene=null!;
    private CombatBoardCatalog _catalog=null!;
    private PackedScene _character=null!;
    private string _output="";
    private int _width;
    private void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);_checks.Add(label);GD.Print("STONE_STAGE_PASS "+label);}
    private async Task Frames(int n=3){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private static IEnumerable<T> Nodes<T>(Node root)where T:Node
    {if(root is T found)yield return found;foreach(var child in root.GetChildren())foreach(var next in Nodes<T>(child))yield return next;}
    private string SourceHash(string path)=>Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(path))));
    private BattleBoardHud Hud=>Nodes<BattleBoardHud>(_scene).Single(n=>!n.IsQueuedForDeletion());
    private BattleEntityData Unit(int faction,int member,int gender)
    {
        var pain=_catalog.skills.Single(s=>s.skill_name=="痛击").get_board_skill_key();
        return new BattleEntityData
        {
            uid=$"stage_{faction}_{member}",entity_name=faction==1?$"修士{member+1}":$"对练者{member+1}",gender=gender,
            entity_type=(int)WorldEntity.EntityType.CHARACTER,faction=faction,portrait_data=new HumanData{gender=gender},
            max_health=10000,current_health=10000,max_stamina=6,max_mana=3,test_mana_cap_override=3,
            physique=10,intelligence=10,soul=10,body_proficiency=6,magic_proficiency=6,sword_proficiency=6,
            precision=10000,critical=0,dodge=0,crit_resist=10000,initiative_speed=5000,cultivation_realm="云息三层",
            combat_skill_slots_configured=true,combat_board_catalog=_catalog,
            combat_resolved_skills=new(_catalog.skills),
            combat_board_layout=new CombatBoardLayout{placements=[new CombatBoardPlacement{placement_id=$"pain_{faction}_{member}",source_key=pain}]}
        };
    }
    private async Task Setup(int count)
    {
        var players=Enumerable.Range(0,count).Select(i=>Unit(1,i,i%2)).ToArray();
        var enemies=Enumerable.Range(0,count).Select(i=>Unit(2,i,(i+1)%2)).ToArray();
        var before=players.Concat(enemies).Select(s=>(s.gender,s.entity_type,s.portrait_data,CombatBoardPreparationService.Signature(s.combat_board_layout!))).ToArray();
        _manager.setup_team_battle(new(Enumerable.Repeat(_character,count).Select(value=>Variant.From(value))),new(Enumerable.Repeat(_character,count).Select(value=>Variant.From(value))),
            new(players.Select(value=>Variant.From(value))),new(enemies.Select(value=>Variant.From(value))));await Frames(7);
        Check(_manager.is_board_battle()&&_manager.is_board_editing(),$"{count}对{count}使用真实招式盘入口");
        Check(!_manager.distant_avatar_preview&&_manager.uses_compact_battle_presentation(),"正式招式盘不依赖远景美术预览开关");
        Check(_manager.GetMeta("combat_presentation_profile").AsString()==BattleBoardStoneStage.ProfileId,"人物与场地使用固定版本标识");
        var stage=_scene.GetNode<BattleBoardStoneStage>("BattleBackgroundArtwork/BoardStoneStage");
        Check(stage.IsVisibleInTree()&&stage.GetMeta("ground_path").AsString()==BattleBoardStoneStage.GroundPath,"已选俯视石地实际绘制");
        Check(!_scene.GetNode("BattleBackgroundArtwork").GetChildren().OfType<TextureRect>().Single().Visible,"旧远山背景退出当前绘制");
        Check(Math.Abs(_manager.get_unit_display_height()/BattleUiArt.Scale(GetViewport())-160)<.1f,"小人物统一采用160设计单位高度");
        foreach(var unit in _manager.all_units.OfType<Character>())
        {
            var doll=unit.GetNode<BattlePaperDoll>("BattlePaperDoll");
            Check(doll.uses_queue_avatar()&&doll.uses_basic_pair_avatar()&&!doll.uses_spine_character(),"男女使用原规格骨骼，不回退旧C1纸偶 "+unit.entity_name);
            Check(GetViewport().GetVisibleRect().Encloses(_manager.get_unit_screen_rect(unit)),"完整人物留在视口内 "+unit.entity_name);
        }
        Check(before.SequenceEqual(players.Concat(enemies).Select(s=>(s.gender,s.entity_type,s.portrait_data,CombatBoardPreparationService.Signature(s.combat_board_layout!)))),"显示不修改来源人物性别肖像与招式配置");
    }
    private async Task Capture(string name)
    {
        HoverInfoManager.HideAll();await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.GetWidth()==_width&&image.GetHeight()==_width*9/16,"截图尺寸 "+name);
        Check(image.SavePng(_output+name+".png")==Error.Ok,"实机截图 "+name);_captures.Add(name+".png");
    }
    private async Task CreateScene()
    {
        if(_scene is not null)
        {
            _manager.battle_running=false;_scene.QueueFree();await Frames(8);
        }
        _scene=new Node2D{Name="FixedStoneStage"};AddChild(_scene);_scene.AddChild(new Node2D{Name="DeployLayer"});
        var ui=new CanvasLayer{Name="UILayer"};_scene.AddChild(ui);ui.AddChild(GD.Load<PackedScene>("res://tscn/panel/CombatSkillBar.tscn").Instantiate());
        _manager=new BattleManager{Name="BattleMgr",deploy_layer="../DeployLayer",isolated_trial_battle=true};_scene.AddChild(_manager);await Frames();
    }
    public override void _Ready(){ProcessMode=ProcessModeEnum.Always;CallDeferred(nameof(Run));}
    private async void Run()
    {
        var passed=false;var failure="";
        try
        {
            _width=OS.GetCmdlineUserArgs().Contains("--wide")?1920:1280;
            _output=$"res://docs/美术规范/人物战斗正式实装_20261109/林间石地小人物/{_width}/";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_output));
            GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().ContentScaleSize=Vector2I.Zero;GetWindow().Size=new(_width,_width*9/16);
            GetNode<GameTimeMgr>("/root/GameTimeManager").pause_time();
            var groundHash=SourceHash(BattleBoardStoneStage.GroundPath);
            Check(groundHash==SourceHash("res://tools/action_battle_test/art/battle_ground_stone_v1.png"),"正式战场石地逐字节复用已选原图");
            var actor=new Character();AddChild(actor);actor.load_from_data(NpcCombatBuildPlanner.TestLearning([]));
            _catalog=CombatBoardCatalog.FromCharacter(actor);actor.QueueFree();
            await CreateScene();
            _character=GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            var malformed=new BattlePaperDoll();_scene.AddChild(malformed);
            malformed.Configure(new BattleEntityData{entity_type=(int)WorldEntity.EntityType.ITEM,portrait_data=new HumanData(),gender=0,faction=1});
            Check(malformed.uses_queue_avatar()&&!malformed.uses_spine_character(),"人类肖像快照可纠正查看入口漏填的显示类型");malformed.QueueFree();await Frames();
            await Setup(1);var hud=Hud;hud.CloseEditor();await Frames();
            var initial=_manager.all_units.OfType<Node2D>().Select(n=>n.GlobalPosition).ToArray();var height=_manager.get_unit_display_height();
            await Capture("单挑_查看战场");hud.OpenEditor();await Frames();
            Check(initial.SequenceEqual(_manager.all_units.OfType<Node2D>().Select(n=>n.GlobalPosition))&&Math.Abs(height-_manager.get_unit_display_height())<.1f,"打开配招不改人物比例与站位");
            await Capture("单挑_配招暗幕");hud.CloseEditor();await Frames();
            Check(initial.SequenceEqual(_manager.all_units.OfType<Node2D>().Select(n=>n.GlobalPosition)),"关闭配招保持同一战场坐标");
            hud.OpenEditor();Check(_manager.commit_player_board(hud.Draft,out var reason),"默认配置提交真实开战 "+reason);await Frames(12);
            Check(_manager.all_units.OfType<Character>().All(c=>c.GetNode<BattlePaperDoll>("BattlePaperDoll").uses_queue_avatar()),"实际执行继续使用同一原规格人物");
            Check(_scene.GetNode<BattleBoardStoneStage>("BattleBackgroundArtwork/BoardStoneStage").IsVisibleInTree(),"实际执行保持同一俯视石地");
            await Capture("单挑_战中");
            await CreateScene();await Setup(3);Hud.CloseEditor();await Frames();await Capture("三对三_查看战场");Hud.OpenEditor();await Frames();await Capture("三对三_配招暗幕");
            Check(groundHash==SourceHash(BattleBoardStoneStage.GroundPath),"显示与战斗均不改写战场源图");
            passed=true;
        }
        catch(Exception error){failure=error.ToString();GD.PushError("STONE_STAGE_FAILED "+error);}
        finally
        {
            if(_output.Length>0)System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(_output+"验收.json"),JsonSerializer.Serialize(new{passed,checks=_checks.Count,items=_checks,captures=_captures,failure},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"STONE_STAGE_RESULT passed={passed} checks={_checks.Count}");
            if(_manager is not null)_manager.battle_running=false;
            foreach(var child in GetChildren())child.QueueFree();await Frames(5);GetTree().Quit(passed?0:1);
        }
    }
}
