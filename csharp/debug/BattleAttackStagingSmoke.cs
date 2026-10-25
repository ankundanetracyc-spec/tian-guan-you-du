using Godot;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Isolated staging check using real units, responses and SkillExecutor; no save writes.</summary>
public partial class BattleAttackStagingSmoke : Node
{
    private Node2D _scene = null!;
    private BattleManager _manager = null!;
    private SkillExecutor _executor = null!;
    private const string CaptureFolder = "res://tmp/attack_staging_20261011";
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    private async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds,processAlways:false),SceneTreeTimer.SignalName.Timeout);

    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--offscreen"))
            {
                GetWindow().Mode = Window.ModeEnum.Windowed;
                GetWindow().Position = new Vector2I(-4096, -4096);
                GetWindow().GuiDisableInput = true;
                GetNodeOrNull<Node>("/root/GameplayInputRouter")?.SetProcessInput(false);
            }
            GetWindow().Size=new Vector2I(1280,720);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _scene=GD.Load<PackedScene>("res://tscn/battle/BattleSandbox.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_scene);GetTree().CurrentScene=_scene;
            await Wait(.8f);
            _manager=_scene.GetNode<BattleManager>("BattleMgr");
            _manager.SetProcess(false);
            _manager.SetProcessInput(false);
            _manager.SetProcessUnhandledInput(false);
            ((CanvasItem)typeof(BattleManager).GetField("_planHud",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(_manager)!).Hide();
            _executor=_manager.GetChildren().OfType<SkillExecutor>().Single();
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(CaptureFolder));
            await Verify("melee",0,3,"撼山",true,capture:0);
            await Verify("ranged",0,3,"灵击",false,capture:1);
            await Verify("reverse",3,0,"短打",true);
            await Verify("thunder",3,0,"符箓/雷击符",false);
            await Verify("response",0,3,"撼山",true,response:true,capture:2);
            await VerifyPause();
            await VerifyDefenseAndDodge();
            var beastDoll = ((Node2D)_manager.all_units[3]).GetNode<BattlePaperDoll>("BattlePaperDoll");
            var displayHeight = beastDoll.Scale.Y * (beastDoll.uses_side_ink() ? BattlePaperDoll.SideInkReferenceHeight : 304f);
            beastDoll.Configure(new BattleEntityData {
                entity_name="林地凶狼", entity_type=(int)WorldEntity.EntityType.BEAST,
                faction=((Node2D)_manager.all_units[3]).Get("faction").AsInt32(),
                battle_portrait_path=BattlePaperDoll.ForestWolfPortraitPath
            });
            beastDoll.SetDisplayHeight(displayHeight);
            await Wait(.1f);
            await Verify("human to wolf",0,3,"撼山",true,capture:3);
            await Verify("wolf to human",3,0,"妖兽/林地凶狼/撕咬",true,capture:4);
            await VerifyTargetRemoved();
            GD.Print("ATTACK STAGING: ALL CHECKS PASSED");
            GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Paused=false;GetTree().Quit(1);}
    }

    private async Task Verify(string label,int from,int to,string path,bool close,bool response=false,int capture=-1)
    {
        var attacker=(Node2D)_manager.all_units[from];var target=(Node2D)_manager.all_units[to];
        var skill=GD.Load<ActiveSkillData>($"res://resources/activeskills/{path}.tres");
        attacker.Set("precision",100);target.Set("dodge",0);target.Set("current_health",150);
        target.Set("current_stamina",6);
        _manager.get_battle_unit(target).skill_queue.Clear();
        _manager.get_battle_unit(target).segment_action_index=0;
        var handled=(HashSet<ulong>)typeof(BattleManager).GetField("_queuedHandledTargets",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(_manager)!;
        handled.Clear();
        typeof(BattleManager).GetField("_segmentPhase",BindingFlags.Instance|BindingFlags.NonPublic)!
            .SetValue(_manager,response?BattleManager.SegmentPhase.Executing:BattleManager.SegmentPhase.Planning);
        if(response)_manager.get_battle_unit(target).skill_queue.Add(new BattleQueuedSkill(GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres"),target));
        var home=attacker.GlobalPosition;var z=attacker.ZIndex;
        var targetArtScale=target.GetNode<BattlePaperDoll>("BattlePaperDoll").Scale;
        var contact=BattleAttackMotion.ContactPosition(attacker,target,home);
        var cell=_scene.GetNode<Node>("BattleMap").Call("get_unit_cell",attacker);
        bool attackSeen=false,damageSeen=false,focusSeen=false,returnArcSeen=false,focusCaptured=false;
        float? responseProgress=null;
        _executor.execute_skill(attacker,skill,target,!response);
        for(int frame=0;frame<120;frame++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var attack=_scene.GetChildren().OfType<BattleInkCombatVfx>()
                .FirstOrDefault(IsIncomingStroke);
            if(attack is not null && !attackSeen)
            {
                Check(attacker.GlobalPosition.DistanceTo(close?contact:home)<1,"Attack began before reaching its staging point: "+label);
                Check(!attack.ZAsRelative&&attack.ZIndex>Math.Max(EffectiveZ(attacker.GetNode<BattlePaperDoll>("BattlePaperDoll")),EffectiveZ(target.GetNode<BattlePaperDoll>("BattlePaperDoll"))),"Attack stroke was hidden behind the actors");
                attackSeen=true;
            }
            if(!close)Check(attacker.GlobalPosition.DistanceTo(home)<.01f,"Ranged caster moved: "+label);
            var focus=_scene.GetChildren().OfType<BattleResponseFocus>().FirstOrDefault();
            if(focus is not null&&!focus.IsQueuedForDeletion())
            {
                focusSeen=true;
                Check(target.GetNode<BattlePaperDoll>("BattlePaperDoll").Scale==targetArtScale,"Response focus scaled the actor instead of reframing the battlefield");
                Check(target.Get("current_health").AsInt32()==150,"Damage settled before response ended");
                Check(attacker.GlobalPosition.DistanceTo(contact)<1,"Attacker left during response");
                Check(attack is not null,"Incoming stroke disappeared during response");
                var progress=VisualProgress(attack!);
                responseProgress??=progress;
                Check(Mathf.IsEqualApprox(progress,responseProgress.Value),"Incoming stroke advanced through response");
                if(!focusCaptured && focus.FrameZoom>1.2f && target.GetNode<BattlePaperDoll>("BattlePaperDoll").is_defense_presenting())
                {
                    await CaptureContact("response_closeup.png");
                    focusCaptured=true;
                }
            }
            if(target.Get("current_health").AsInt32()<150)
            {
                if(response && !damageSeen)
                {
                    Check(target.GetNode<BattlePaperDoll>("BattlePaperDoll").is_defense_presenting(),"Response was replaced by hurt pose");
                    Check(!_scene.GetChildren().OfType<BattleInkCombatVfx>().Any(e=>e.GetNodeOrNull<AnimatedSprite2D>("SequenceFrames")?.Animation.ToString() is "hurt" or "impact"),"Guard contact spawned an extra hurt/impact sequence");
                    await CaptureContact("guard_contact.png");
                }
                damageSeen=true;
            }
            if(close&&damageSeen&&Mathf.Abs(home.X-contact.X)>1)
            {
                var progress=(attacker.GlobalPosition.X-contact.X)/(home.X-contact.X);
                if(progress>0&&progress<1&&attacker.GlobalPosition.Y<Mathf.Lerp(contact.Y,home.Y,progress)-2)returnArcSeen=true;
            }
            if(capture>=0&&frame<48&&!OS.GetCmdlineUserArgs().Contains("--no-capture"))
            {
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng($"{CaptureFolder}/{capture:D2}_{frame:D2}.png");
            }
        }
        Check(attackSeen&&damageSeen,"No visible attack or damage: "+label);
        Check(attacker.GlobalPosition.DistanceTo(home)<.01f&&attacker.ZIndex==z,"Attacker did not return exactly: "+label);
        Check(_scene.GetNode<Node>("BattleMap").Call("get_unit_cell",attacker).Equals(cell),"Staging changed the logical cell");
        if(close && attacker.GetNode<BattlePaperDoll>("BattlePaperDoll").uses_forest_wolf_portrait())
            Check(!returnArcSeen,"Wolf return should stay grounded: "+label);
        else if(close)Check(returnArcSeen,"No backward return arc: "+label);
        if(response)
        {
            Check(focusSeen,"Response insert did not trigger");
            Check(target.Get("current_stamina").AsInt32()==5,"Response fee not paid exactly once");
            Check(_manager.get_battle_unit(target).segment_action_index==1,"Response slot not consumed exactly once");
        }
        GD.Print("STAGING PASSED: ",label);
    }

    private async Task VerifyPause()
    {
        typeof(BattleManager).GetField("_segmentPhase",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_manager,BattleManager.SegmentPhase.Planning);
        var attacker=(Node2D)_manager.all_units[0];var target=(Node2D)_manager.all_units[3];
        var home=attacker.GlobalPosition;target.Set("current_health",150);
        _executor.execute_skill(attacker,GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres"),target,true);
        await Wait(.22f);
        GetTree().Paused=true;var held=attacker.GlobalPosition;
        await ToSignal(GetTree().CreateTimer(.2,processAlways:true),SceneTreeTimer.SignalName.Timeout);
        Check(attacker.GlobalPosition.DistanceTo(held)<.01f,"Approach moved while paused");
        GetTree().Paused=false;
        await Wait(1.2f);
        Check(attacker.GlobalPosition.DistanceTo(home)<.01f,"Paused attack failed to return");
        GD.Print("STAGING PASSED: pause/resume");
    }

    private static bool IsIncomingStroke(BattleInkCombatVfx effect) => effect.GetNodeOrNull<AnimatedSprite2D>("SequenceFrames") is { } sequence
        ? sequence.Animation == "slash"
        : effect.GetNodeOrNull<CanvasItem>("InkSurface")?.Material is ShaderMaterial material && Mathf.IsZeroApprox(material.GetShaderParameter("phase").AsSingle());

    private static float VisualProgress(BattleInkCombatVfx effect) => effect.GetNodeOrNull<AnimatedSprite2D>("SequenceFrames") is { } sequence
        ? sequence.Frame : ((ShaderMaterial)effect.GetNode<CanvasItem>("InkSurface").Material).GetShaderParameter("progress").AsSingle();

    private static int EffectiveZ(CanvasItem item) => item.ZIndex + (item.ZAsRelative && item.GetParent() is CanvasItem parent ? EffectiveZ(parent) : 0);

    private async Task VerifyDefenseAndDodge()
    {
        await Wait(.65f);
        var attacker=(Node2D)_manager.all_units[0]; var target=(Node2D)_manager.all_units[3];
        var doll=target.GetNode<BattlePaperDoll>("BattlePaperDoll");
        var skill=GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres");
        var home=target.GlobalPosition; var health=target.Get("current_health").AsInt32();
        BattleInkCombatVfx.PlayUtility(target,target,GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres"));
        await Wait(.38f);
        BattleInkCombatVfx.PlayImpact(target,skill,attacker);
        doll.play_hit(Vector2.Left);
        await Wait(.05f);
        Check(doll.is_defense_presenting(),"Guard contact changed into hurt");
        Check(!_scene.GetChildren().OfType<BattleInkCombatVfx>().Any(e=>e.GetNodeOrNull<AnimatedSprite2D>("SequenceFrames")?.Animation=="guard"),"Removed guard sequence still spawned");
        Check(doll.presentation_zoom()==1 && _scene.GetChildren().OfType<BattleContactFocus>().Single().FrameZoom>1.1f,"Guard battlefield framing missing or actor was scaled");
        Check(!_scene.GetChildren().OfType<BattleInkCombatVfx>().Any(e=>e.GetNodeOrNull<AnimatedSprite2D>("SequenceFrames")?.Animation.ToString() is "hurt" or "impact"),"Guard contact added unwanted defender impact");
        await Wait(1.6f);
        BattleInkCombatVfx.PlayImpact(target,skill,attacker,evaded:true);
        await Wait(.13f);
        var effect=_scene.GetChildren().OfType<BattleInkCombatVfx>().Single(e=>e.GetNodeOrNull<AnimatedSprite2D>("SequenceFrames")?.Animation=="dodge");
        Check(!effect.ZAsRelative && effect.ZIndex>EffectiveZ(doll),"Dodge effect hidden behind actor");
        Check(doll.Position.Length()>50 && doll.presentation_zoom()==1 && _scene.GetChildren().OfType<BattleContactFocus>().Single().FrameZoom>1.1f,"Dodge withdraw/framing missing or actor was scaled");
        await CaptureContact("dodge_closeup.png");
        var held=doll.Transform; var zoom=doll.presentation_zoom(); var frame=VisualProgress(effect);
        GetTree().Paused=true;
        await ToSignal(GetTree().CreateTimer(.18,processAlways:true),SceneTreeTimer.SignalName.Timeout);
        Check(doll.Transform==held && Mathf.IsEqualApprox(doll.presentation_zoom(),zoom) && VisualProgress(effect)==frame,"Dodge pause diverged");
        GetTree().Paused=false;
        await Wait(1.45f);
        Check(doll.Position.IsEqualApprox(Vector2.Zero) && target.GlobalPosition.IsEqualApprox(home) && doll.presentation_zoom()==1,"Dodge did not return to its origin and scale");
        Check(!IsInstanceValid(effect),"Dodge sequence failed to clean up");
        Check(target.Get("current_health").AsInt32()==health,"Presentation changed health");
        GD.Print("STAGING PASSED: guard suppression/layer/zoom and dodge pause/recovery/cleanup");
    }

    private async Task CaptureContact(string filename)
    {
        if(!OS.GetCmdlineUserArgs().Contains("--capture-contact"))return;
        const string directory="res://docs/美术规范/局内水墨动作正式接入_20261016/演出修订/";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(directory+filename);
    }

    private async Task VerifyTargetRemoved()
    {
        var attacker=(Node2D)_manager.all_units[0];var target=(Node2D)_manager.all_units[3];
        var home=attacker.GlobalPosition;var z=attacker.ZIndex;
        _executor.execute_skill(attacker,GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres"),target,true);
        await Wait(.22f);target.QueueFree();
        await Wait(1.1f);
        Check(attacker.GlobalPosition.DistanceTo(home)<.01f&&attacker.ZIndex==z,"Missing target stranded attacker");
        GD.Print("STAGING PASSED: target removed during approach");
    }
}

