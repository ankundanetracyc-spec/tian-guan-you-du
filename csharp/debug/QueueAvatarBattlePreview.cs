using Godot;

/// <summary>Original queue battle with the approved small skeletal character. No save mutation.</summary>
public partial class QueueAvatarBattlePreview : Node2D
{
    private BattleManager _manager = null!;
    private readonly List<Button> _demoButtons = [];
    private readonly List<BattleEntityData> _previewSources = [];
    private string _sourceAppearanceSignature = "";
    private bool _demoRunning;
    private OptionButton? _outfitChoice;
    private int _selectedClothes = 5;
    public override void _Ready() { AddToGroup("gameplay_scene"); CallDeferred(nameof(StartPreview)); }
    private void StartPreview()
    {
        _manager = GetNode<BattleManager>("BattleMgr");
        _manager.isolated_trial_battle = true;
        _manager.distant_avatar_preview = true;
        SetupOutfits();
        // Only this isolated scene reuses the original small-avatar stone arena.
        // Ordinary queue encounters keep their own background selection.
        var background = GetNode("BattleBackgroundArtwork").GetChildren().OfType<TextureRect>().Single();
        // The original trial assets live under an editor-ignored tools folder,
        // so load the source image exactly as the original trial does.
        using var ground = Image.LoadFromFile("res://tools/action_battle_test/art/battle_ground_stone_v1.png");
        background.Texture = ImageTexture.CreateFromImage(ground);
        background.SetMeta("small_avatar_stone_ground", true);
        background.Modulate = new Color(.93f, .96f, 1f);
        var controls = new CanvasLayer { Name="QueueAvatarPreviewControls", Layer=120, ProcessMode=ProcessModeEnum.Always };
        AddChild(controls);
        var bar = new HBoxContainer { Position=new(20,66) };
        bar.AddThemeConstantOverride("separation",8); controls.AddChild(bar);
        var back=new Button { Text="返回首页" }; back.Pressed+=ReturnHome; bar.AddChild(back);
        var restart=new Button { Text="重新试演" }; restart.Pressed+=()=> { GetTree().Paused=false; GetTree().ReloadCurrentScene(); }; bar.AddChild(restart);
        bar.AddChild(new Label { Text="动作预览（不结算）", VerticalAlignment=VerticalAlignment.Center });
        foreach (var (key,label) in new[] { ("idle","待机"),("attack","出招"),("hurt","受击"),("guard","架招"),("dodge","闪避"),("combat","攻受闪轮播"),("all","完整轮播") })
        {
            var button=new Button { Text=label, Name="Demo_"+key };
            button.Pressed+=()=>demonstrate_action(key); bar.AddChild(button); _demoButtons.Add(button);
        }
        _outfitChoice = new OptionButton { Position=new(20,112), TooltipText="更换我方首位服装并重新试演；发型固定为已支持组合。" };
        foreach(var (id,label) in new[]{(3,"玉虚·杂役常服"),(4,"玉虚·外门常服"),(5,"玉虚·内门常服"),(6,"玉虚·真传常服")}) _outfitChoice.AddItem(label,id);
        _outfitChoice.Select(2); controls.AddChild(_outfitChoice);
        _outfitChoice.ItemSelected += index => { if(_demoRunning || !_manager.can_player_plan()) return; _selectedClothes=_outfitChoice.GetItemId((int)index); SetupOutfits(); CollapseInitialPlanning(); };
        CollapseInitialPlanning();
        if (OS.GetCmdlineUserArgs().Contains("--queue-avatar-smoke") || OS.GetCmdlineUserArgs().Contains("--queue-small-avatar-smoke")) RunSmoke();
    }
    public override void _Process(double delta)
    {
        if (_manager is null) return;
        var busy=_demoRunning || !_manager.can_player_plan() || PreviewDolls().Any(d=>d.is_action_animation_playing());
        foreach(var button in _demoButtons) button.Disabled=busy;
        if(_outfitChoice is not null) _outfitChoice.Disabled=busy;
    }
    private void SetupOutfits()
    {
        var scene=GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
        var allies=new[]{MakeUnit(1,member:0,clothes:_selectedClothes),MakeUnit(1,member:1,clothes:3),MakeUnit(1,member:2,clothes:4)};
        var enemies=new[]{MakeUnit(2,member:0,clothes:6),MakeUnit(2,member:1,clothes:4),MakeUnit(2,member:2,clothes:5)};
        _previewSources.Clear(); _previewSources.AddRange(allies); _previewSources.AddRange(enemies);
        _sourceAppearanceSignature=SourceAppearanceSignature();
        _manager.setup_team_battle([scene,scene,scene],[scene,scene,scene],[allies[0],allies[1],allies[2]],[enemies[0],enemies[1],enemies[2]]);
    }
    private BattlePaperDoll[] PreviewDolls()=>GetNode("DeployLayer").FindChildren("BattlePaperDoll","",true,false).OfType<BattlePaperDoll>().ToArray();
    private static QueueSmallAvatarBridge Rig(BattlePaperDoll doll)=>doll.FindChildren("QueueSmallAvatarBridge","",true,false).OfType<QueueSmallAvatarBridge>().Single();
    private static double Clock(QueueSmallAvatarBridge rig)=>rig.DebugState["clock"].AsDouble();
    private static string Action(QueueSmallAvatarBridge rig)=>rig.DebugState["action"].AsString();
    private async System.Threading.Tasks.Task Frames(int count) { for(var i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    public bool is_demo_running()=>_demoRunning;
    public void demonstrate_palm()=>demonstrate_action("attack");
    public void demonstrate_punch()=>demonstrate_action("attack");
    public async void demonstrate_action(string action)
    {
        if (_demoRunning || !_manager.can_player_plan() || PreviewDolls().Any(d=>d.is_action_animation_playing())) return;
        if (!new[]{"idle","attack","hurt","guard","dodge","combat","all"}.Contains(action)) return;
        _demoRunning=true; _manager.SetProcess(false);
        try
        {
            var allDolls=PreviewDolls();
            // The outfit selector controls the main player, so demonstrations
            // must show that same actor rather than whichever ally is closest.
            var selectedActor=((Node)_manager.get_main_player_unit()!).GetNode<BattlePaperDoll>("BattlePaperDoll");
            var nearestEnemy=allDolls.Where(d=>d.side_ink_faces_left())
                .OrderBy(d=>d.GlobalPosition.DistanceSquaredTo(selectedActor.GlobalPosition)).First();
            var dolls=new[]{selectedActor,nearestEnemy};
            foreach(var kind in action=="all"?new[]{"idle","attack","hurt","guard","dodge"}:action=="combat"?new[]{"attack","hurt","dodge"}:new[]{action})
            {
                if(kind=="attack")
                {
                    var attacker=dolls.First(d=>!d.side_ink_faces_left());
                    var defender=dolls.First(d=>d.side_ink_faces_left());
                    var actor=(Node2D)attacker.GetParent(); var target=(Node2D)defender.GetParent();
                    var home=actor.GlobalPosition; var contact=BattleAttackMotion.ContactPosition(actor,target,home);
                    GD.Print($"QUEUE_AVATAR_TRAVEL: approach={BattleAttackMotion.TravelSeconds(actor,home,contact):F3}s return={BattleAttackMotion.TravelSeconds(actor,contact,home,true):F3}s");
                    attacker.face_direction(target.GlobalPosition-home);
                    attacker.set_queue_travel_mode("approach");
                    var advance=actor.CreateTween();
                    advance.TweenMethod(Callable.From<float>(p=>BattleAttackMotion.ApplySmallAvatarTravel(actor,home,contact,p)),0f,1f,BattleAttackMotion.TravelSeconds(actor,home,contact));
                    await ToSignal(advance,Tween.SignalName.Finished);
                    attacker.set_queue_travel_mode("idle");
                    Trigger(attacker,"attack");
                    await ToSignal(GetTree().CreateTimer(.30,false),SceneTreeTimer.SignalName.Timeout);
                    attacker.hold_attack_contact(true);
                    await ToSignal(GetTree().CreateTimer(.175,false),SceneTreeTimer.SignalName.Timeout);
                    attacker.hold_attack_contact(false);
                    await ToSignal(GetTree().CreateTimer(.30,false),SceneTreeTimer.SignalName.Timeout);
                    attacker.play_retreat(contact-home);
                    attacker.set_queue_travel_mode("retreat");
                    var retreat=actor.CreateTween();
                    retreat.TweenMethod(Callable.From<float>(p=>BattleAttackMotion.ApplySmallAvatarTravel(actor,contact,home,p)),0f,1f,BattleAttackMotion.TravelSeconds(actor,contact,home,true));
                    await ToSignal(retreat,Tween.SignalName.Finished);
                    attacker.set_queue_travel_mode("idle");
                }
                else foreach(var doll in dolls) Trigger(doll,kind);
                await ToSignal(GetTree().CreateTimer(kind=="attack"?.75:1.65,false),SceneTreeTimer.SignalName.Timeout);
            }
        }
        finally { _demoRunning=false; if(IsInstanceValid(_manager)) _manager.SetProcess(true); }
    }
    private static void Trigger(BattlePaperDoll doll,string action)
    {
        var facing=doll.side_ink_faces_left()?Vector2.Left:Vector2.Right;
        switch(action)
        {
            case "attack": doll.play_attack(facing,true); break;
            case "hurt": doll.play_hit(-facing,1,false,false); break;
            case "guard": doll.play_guard(); break;
            case "dodge": doll.play_dodge(); break;
            default: doll.reset_presentation(); break;
        }
    }
    private string SourceAppearanceSignature()=>string.Join("|",_previewSources.Select(d=>$"{d.uid}:{d.gender}:{d.portrait_data?.appearance_clothes_style}:{d.portrait_data?.appearance_hair_style}:{d.portrait_data?.appearance_hair_back_style}"));
    private async void CollapseInitialPlanning()
    {
        await Frames(4);
        if(FindChild("PlanningToggle",true,false) is Button {Text:"隐藏队列"} toggle) toggle.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private static BattleEntityData MakeUnit(int faction,int frontHairStyle=5,int backHairStyle=6,int member=0,int clothes=5)
    {
        using var generator=new CharacterGenerator();
        var data=generator.generate("云息",faction); data.gender=0;
        data.entity_name=faction==1?new[]{"玉虚修士","青岚","玄舟"}[member]:$"对练·{new[]{"甲","乙","丙"}[member]}";
        data.appearance_seed=61103+faction*10+member; data.appearance_clothes_style=clothes;
        data.appearance_hair_style=frontHairStyle; data.appearance_hair_back_style=backHairStyle;
        var character=new Character(); character.load_from_data(data); character.entity_type=WorldEntity.EntityType.CHARACTER;
        character.faction=faction; character.unique_id=$"queue-avatar-preview-{faction}-{member}";
        character.set_combat_skill_slot_paths(["res://resources/activeskills/应敌.tres","res://resources/activeskills/痛击.tres","res://resources/activeskills/架招.tres"]);
        var snapshot=character.get_snapshot_data(); character.Free(); return snapshot;
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey {Pressed:true,Echo:false,Keycode:Key.Escape}) { ReturnHome(); GetViewport().SetInputAsHandled(); }
    }
    private void ReturnHome() { GetTree().Paused=false; GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"); }
    private void ConfirmSmokeQueue()
    {
        _manager.confirm_player_queue();
        for(var i=0;i<10&&_manager.is_selecting_queue_targets();i++)
        {
            var target=_manager.enemy_units.OfType<Node2D>().First();
            _manager._UnhandledInput(new InputEventMouseButton { Pressed=true,ButtonIndex=MouseButton.Left,Position=_manager.get_unit_screen_rect(target).GetCenter() });
        }
        if(_manager.is_selecting_queue_targets()) throw new Exception("Target selection did not accept the enemy click.");
    }
    private async void RunSmoke()
    {
        try
        {
            await Frames(20);
            var background=GetNode("BattleBackgroundArtwork").GetChildren().OfType<TextureRect>().Single();
            if(background.Texture is null || background.Texture.GetWidth()!=1672 || !background.GetMeta("small_avatar_stone_ground", false).AsBool())
                throw new Exception("Trial must display the original small-avatar stone ground.");
            var dolls=PreviewDolls(); var rigs=dolls.Select(Rig).ToArray();
            if(dolls.Length!=6 || dolls.Any(d=>!d.uses_queue_avatar()) || dolls.Count(d=>d.side_ink_faces_left())!=3) throw new Exception("Six supported inward-facing small rigs required.");
            if(_manager.player_units.Count!=3 || _manager.enemy_units.Count!=3) throw new Exception("Real 3v3 required.");
            var outfits=rigs.Select(r=>r.DebugState["outfit_id"].AsString()).Distinct().Order().ToArray();
            if(!outfits.SequenceEqual(new[]{"male_clothes_03","male_clothes_04","male_clothes_05","male_clothes_06"})) throw new Exception("Trial must resolve four real outfit catalog identities.");
            foreach(var rig in rigs)
            {
                if(rig.DebugState["bone_count"].AsInt32()!=21) throw new Exception("Approved outfit skeleton must retain its 21 bones.");
            }
            var ratio=_manager.get_unit_display_height()/(BattleUiArt.Canvas.Y*BattleUiArt.Scale(GetViewport()));
            if(ratio<.16 || ratio>.18) throw new Exception("Distant scale outside 16–18%.");
            var clocks=rigs.Select(Clock).ToArray(); _manager.toggle_battle_pause(); await Frames(6);
            if(rigs.Where((r,i)=>Clock(r)!=clocks[i]).Any()) throw new Exception("Tree pause did not freeze rigs.");
            _manager.toggle_battle_pause(); await Frames(3);
            if(rigs.Where((r,i)=>Clock(r)<=clocks[i]).Any()) throw new Exception("Idle did not resume.");
            foreach(var doll in dolls.Take(1))
            {
                var rig=Rig(doll);
                foreach(var action in new[]{"attack","hurt","guard","dodge"})
                {
                    Trigger(doll,action); await Frames(4);
                    if(Action(rig)!=action) throw new Exception("Missing routed action: "+action);
                    doll.set_response_time_stop(true); var frozen=Clock(rig); await Frames(4);
                    if(Clock(rig)!=frozen) throw new Exception("Response stop advanced rig.");
                    doll.set_response_time_stop(false);
                    if(action=="attack")
                    {
                        doll.hold_attack_contact(true); frozen=Clock(rig); await Frames(5);
                        if(Clock(rig)!=frozen) throw new Exception("Contact hold advanced rig.");
                        doll.hold_attack_contact(false);
                    }
                    await Frames(110);
                    if(doll.is_action_animation_playing() || Action(rig)!="idle") throw new Exception("Action did not return to idle: "+action);
                }
                doll.play_death(); var deadClock=Clock(rig); await Frames(5);
                if(Clock(rig)!=deadClock) throw new Exception("Dead actor kept breathing.");
                doll.reset_presentation(); await Frames(5);
                if(Clock(rig)<=deadClock || Action(rig)!="idle" || doll.Modulate!=Colors.White || doll.Rotation!=0)
                    throw new Exception("Reset retained death/action residue.");
            }
            foreach(var clothes in new[]{3,4,5,6})
            foreach(var part in new[]{"front","back"})
            {
                var other=MakeUnit(1,clothes:clothes); var appearance=(HumanData)other.portrait_data!;
                if(part=="front") appearance.appearance_hair_style=0; else appearance.appearance_hair_back_style=0;
                var probe=new BattlePaperDoll(); AddChild(probe); probe.Configure(other);
                if(probe.uses_queue_avatar()) throw new Exception("Unsupported hair replaced with fixed sample."); probe.Free();
            }
            // Grounded travel progresses immediately and freezes with the battle.
            var travelDoll=dolls[0]; var travelRig=Rig(travelDoll); var travelActor=(Node2D)travelDoll.GetParent();
            var travelHome=travelActor.GlobalPosition; var travelEnd=travelHome+new Vector2(80,0);
            travelDoll.set_queue_travel_mode("approach");
            var flight=travelActor.CreateTween();
            flight.TweenMethod(Callable.From<float>(p=>BattleAttackMotion.ApplySmallAvatarTravel(travelActor,travelHome,travelEnd,p)),0f,1f,.8);
            await Frames(5);
            if(travelActor.GlobalPosition==travelHome) throw new Exception("Grounded walk did not begin moving.");
            await Frames(18);
            var mid=travelActor.GlobalPosition; var progress=travelRig.DebugState["travel_progress"].AsDouble();
            if(travelRig.DebugState["flight_height"].AsDouble()!=0) throw new Exception("Walking must stay on the ground.");
            _manager.toggle_battle_pause(); await Frames(5);
            if(travelActor.GlobalPosition!=mid || travelRig.DebugState["travel_progress"].AsDouble()!=progress) throw new Exception("Walking pause lost synchronization.");
            _manager.toggle_battle_pause(); await ToSignal(flight,Tween.SignalName.Finished);
            travelDoll.set_queue_travel_mode("idle"); await Frames(2);
            if(travelRig.DebugState["flight_height"].AsDouble()!=0) throw new Exception("Landing left actor hovering.");
            travelActor.GlobalPosition=travelHome; travelDoll.reset_presentation(); await Frames(2);
            var player=_manager.get_main_player_unit()!;
            var skills=player.GetMeta("combat_resolved_skills").AsGodotArray().Select(v=>v.AsGodotObject()).OfType<ActiveSkillData>();
            _manager.on_skill_selected(skills.First(s=>s.skill_name=="应敌"),0);
            var queued=_manager.get_battle_unit(player).skill_queue.First(a=>a.kind==BattleQueuedSkill.ActionKind.Skill);
            var playerRig=Rig(((Node)player).GetNode<BattlePaperDoll>("BattlePaperDoll"));
            ConfirmSmokeQueue(); var seen=false; var gaits=new HashSet<string>();
            for(var i=0;i<18000;i++)
            {
                await Frames(1); seen|=Action(playerRig)=="attack";
                gaits.Add(playerRig.DebugState["gait"].AsString());
                if(playerRig.DebugState["flight_height"].AsDouble()!=0) throw new Exception("Actual queue walk left the ground.");
                if(queued.completed) break;
            }
            if(!seen || !queued.completed || queued.skipped) throw new Exception("Actual queue attack did not animate and complete.");
            if(!gaits.Contains("forward") || !gaits.Contains("backward")) throw new Exception("Actual queue travel must animate forward and backward steps.");
            if(SourceAppearanceSignature()!=_sourceAppearanceSignature) throw new Exception("Appearance source mutated.");
            GD.Print("QUEUE_SMALL_AVATAR_OK: original rig, actual 3v3, distant scale, idle/attack/hurt/guard/dodge, contact/response/tree pause, restored idle, unsupported hair fallback, real queued attack completed, source snapshots unchanged.");
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError("QUEUE_SMALL_AVATAR_FAIL: "+error); GetTree().Quit(1); }
    }
}
