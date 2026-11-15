using Godot;
using System.Threading.Tasks;

/// <summary>Real queue battle with the provisional male/female appearances; no world save.</summary>
public partial class TurnBasedPairAvatarReview : Node2D
{
    private BattleManager _manager = null!;
    private readonly List<BattleEntityData> _sources = [];
    private string _appearanceBefore = "";
    private const string Folder = "res://docs/美术规范/男女首套回合制人物_20261106/";
    public override void _Ready() { AddToGroup("gameplay_scene"); CallDeferred(nameof(Start)); }
    private async void Start()
    {
        try
        {
            GetWindow().Title="男女首套人物 · 回合制对练";
            _manager = GetNode<BattleManager>("BattleMgr");
            _manager.isolated_trial_battle = true;
            if (OS.GetCmdlineUserArgs().Contains("--pair-smoke"))
            {
                GetWindow().Size = new Vector2I(1440,810);
                GetWindow().Position = new Vector2I(-7000,-7000);
                GetWindow().Unfocusable = true;
                foreach (var count in new[]{1,3})
                {
                    Setup(count);
                    await Frames(16);
                    _manager.SetProcess(false);
                    var dolls = Dolls();
                    Check(dolls.Length==count*2 && dolls.All(d=>d.uses_queue_avatar()),"All humans must use the basic pair");
                    var bridges=dolls.Select(Bridge).ToArray();
                    Check(bridges.All(b=>b.DebugState["source"].AsString()=="turn_based_pair_20261106"),"Wrong appearance adapter");
                    Check(bridges.Count(b=>b.DebugState["gender"].AsInt32()==1)==count,"Female count");
                    Check(bridges.All(b=>b.DebugState["mesh_count"].AsInt32()==(b.DebugState["gender"].AsInt32()==1?8:7)),"Complete body and separate female hair meshes");
                    Check(dolls.All(d=>d.static_screen_rect() is Rect2 rect && rect.End.Y<GetViewportRect().Size.Y*.72f),"Feet overlap the skill HUD");
                    foreach (var doll in new[]{dolls.First(d=>Bridge(d).DebugState["gender"].AsInt32()==0),dolls.First(d=>Bridge(d).DebugState["gender"].AsInt32()==1)})
                    {
                        var bridge=Bridge(doll);
                        var entity=(Creature)doll.GetParent();
                        var resources=(entity.current_health,entity.current_stamina,entity.current_mana);
                        foreach(var action in new[]{"attack","hurt","guard","dodge"})
                        {
                            doll.reset_presentation();
                            Trigger(doll,action);
                            await Frames(4);
                            Check(bridge.DebugState["action"].AsString()==action,"Missing "+action);
                            doll.set_response_time_stop(true);
                            var clock=bridge.DebugState["clock"].AsDouble();
                            await Frames(3);
                            Check(bridge.DebugState["clock"].AsDouble()==clock,"Response freeze "+action);
                            doll.set_response_time_stop(false);
                            // Hurt may still be inside its original contact hold after
                            // response freeze ends; that independent hold must finish first.
                            for(var resumeFrame=0;resumeFrame<60&&bridge.DebugState["clock"].AsDouble()<=clock;resumeFrame++) await Frames(1);
                            Check(bridge.DebugState["clock"].AsDouble()>clock,"Response resume "+action+": "+bridge.DebugState);
                        }
                        doll.reset_presentation();
                        await Frames(2);
                        Check(resources==(entity.current_health,entity.current_stamina,entity.current_mana),"Appearance events changed resources");
                    }
                    Check(Signature()==_appearanceBefore,"Appearance source mutated");
                    if(FindChild("PlanningToggle",true,false) is Button {Text:"隐藏队列"} toggle) toggle.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(24);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var capture=GetViewport().GetTexture().GetImage();
                    Check(capture.SavePng(Folder+$"正式队列_{count}v{count}.png")==Error.Ok,"Screenshot");
                    _manager.SetProcess(true);
                }
                await VerifyActualStrike();
                GD.Print("TURN BASED PAIR: PASS; 1v1/3v3; two genders; four poses; freeze/resume; real strike; source appearance preserved.");
                GetTree().Quit();
            }
            else Setup(1);
        }
        catch(Exception error){ GD.PrintErr(error); GetTree().Paused=false; GetTree().Quit(1); }
    }
    private void Setup(int count)
    {
        var scene=GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
        var playerScenes=new Godot.Collections.Array(); var enemyScenes=new Godot.Collections.Array();
        var allies=new Godot.Collections.Array(); var enemies=new Godot.Collections.Array();
        _sources.Clear();
        for(var i=0;i<count;i++)
        {
            playerScenes.Add(scene); enemyScenes.Add(scene);
            var ally=MakeUnit(1,i%2,i); var enemy=MakeUnit(2,(i+1)%2,i);
            allies.Add(ally); enemies.Add(enemy); _sources.Add(ally); _sources.Add(enemy);
        }
        _appearanceBefore=Signature();
        _manager.setup_team_battle(playerScenes,enemyScenes,allies,enemies);
    }
    private BattlePaperDoll[] Dolls()=>GetNode("DeployLayer").FindChildren("BattlePaperDoll","",true,false).OfType<BattlePaperDoll>().ToArray();
    private static QueueSmallAvatarBridge Bridge(BattlePaperDoll doll)=>doll.FindChildren("QueueSmallAvatarBridge","",true,false).OfType<QueueSmallAvatarBridge>().Single();
    private string Signature()=>string.Join("|",_sources.Select(s=>$"{s.uid}:{s.gender}:{s.portrait_data?.appearance_clothes_style}:{s.portrait_data?.appearance_hair_style}:{s.portrait_data?.appearance_hair_back_style}"));
    private async Task Frames(int count){for(var i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Trigger(BattlePaperDoll doll,string action)
    {
        var direction=doll.side_ink_faces_left()?Vector2.Left:Vector2.Right;
        switch(action){case "attack":doll.play_attack(direction,true);break;case "hurt":doll.play_hit(-direction,1,false,false);break;case "guard":doll.play_guard();break;case "dodge":doll.play_dodge();break;}
    }
    private static BattleEntityData MakeUnit(int faction,int gender,int member)
    {
        using var generator=new CharacterGenerator();
        var data=generator.generate("云息",faction); data.gender=gender;
        data.entity_name=faction==1?new[]{"玉虚修士","青岚","玄舟"}[member]:$"对练·{new[]{"甲","乙","丙"}[member]}";
        data.appearance_clothes_style=gender==1?5:0; data.appearance_hair_style=member%3; data.appearance_hair_back_style=member%3;
        var character=new Character(); character.load_from_data(data); character.entity_type=WorldEntity.EntityType.CHARACTER;
        character.faction=faction; character.unique_id=$"basic-pair-{faction}-{member}";
        character.set_combat_skill_slot_paths(["res://resources/activeskills/应敌.tres","res://resources/activeskills/痛击.tres","res://resources/activeskills/架招.tres"]);
        var snapshot=character.get_snapshot_data(); character.Free(); return snapshot;
    }
    private async Task VerifyActualStrike()
    {
        _manager.SetProcess(false);
        var executor=_manager.GetChildren().OfType<SkillExecutor>().Single();
        var attacker=(Creature)_manager.player_units[0]; var target=(Creature)_manager.enemy_units[0];
        attacker.precision=100; attacker.current_stamina=6; target.dodge=0; target.physical_defense=0;
        var hp=target.current_health;
        var card=GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres");
        executor.execute_skill(attacker,card,target,true);
        var completed=false;
        for(var i=0;i<500;i++)
        {
            await Frames(1);
            if(target.current_health<hp&&!attacker.GetNode<BattlePaperDoll>("BattlePaperDoll").is_action_animation_playing()) { completed=true;break; }
        }
        Check(completed,"Real skill did not finish and deal damage");
        Check(Dolls().All(d=>d.uses_queue_avatar()),"Real attack changed appearance");
        Check(Signature()==_appearanceBefore,"Real attack changed source appearance");
    }
}
