using Godot;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Real SkillExecutor and auto-response presentation, in an isolated sandbox.</summary>
public partial class SwordBattlePresentationSmoke : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string Output="res://docs/美术规范/持剑受击与女修剑招_20261025/";
    private string _sexPrefix="male_";
    private Node2D _scene=null!,_actor=null!,_target=null!;
    private BattleManager _manager=null!;
    private SkillExecutor _executor=null!;
    private readonly Dictionary<Node2D,BattleEntityData> _snapshots=[];
    private int _checks;
    private void Check(bool condition,string label){if(!condition)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Wait(double seconds,bool always=false)=>await ToSignal(GetTree().CreateTimer(seconds,processAlways:always),SceneTreeTimer.SignalName.Timeout);
    private static ActiveSkillData Skill(string name)=>GD.Load<ActiveSkillData>("res://resources/activeskills/剑修基础/"+name+".tres");
    private BattlePaperDoll Doll(Node2D unit)=>unit.GetNode<BattlePaperDoll>("BattlePaperDoll");
    private IEnumerable<AnimatedSprite2D> Sequences()=>_scene.GetChildren().OfType<BattleInkCombatVfx>()
        .Select(e=>e.GetNodeOrNull<AnimatedSprite2D>("SequenceFrames")).OfType<AnimatedSprite2D>();
    private bool Busy()=>GetTree().GetNodesInGroup("battle_action_visuals").Any(n=>IsInstanceValid(n)&&n.IsInsideTree()&&!n.IsQueuedForDeletion())
        ||Doll(_actor).is_action_animation_playing()||Doll(_target).is_action_animation_playing();
    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(Output+name+".png");
    }
    public override async void _Ready()
    {
        try
        {
            Engine.MaxFps=60;
            if(OS.GetCmdlineUserArgs().Contains("--offscreen"))GetWindow().Position=new(-4096,-4096);
            GetWindow().Size=new(1440,810);GetWindow().GuiDisableInput=true;
            GetNodeOrNull<Node>("/root/GameplayInputRouter")?.SetProcessInput(false);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _scene=GD.Load<PackedScene>("res://tscn/battle/BattleSandbox.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_scene);GetTree().CurrentScene=_scene;
            await Wait(.8);
            _manager=_scene.GetNode<BattleManager>("BattleMgr");_manager.SetProcess(false);_manager.SetProcessInput(false);_manager.SetProcessUnhandledInput(false);
            ((CanvasItem)typeof(BattleManager).GetField("_planHud",Hidden)!.GetValue(_manager)!).Hide();
            _executor=_manager.GetChildren().OfType<SkillExecutor>().Single();
            _actor=(Node2D)_manager.all_units[0];_target=(Node2D)_manager.all_units[3];
            foreach(var unit in new[]{_actor,_target})
            {
                var snapshot=((Creature)unit).get_snapshot_data();snapshot.gender=0;
                _snapshots[unit]=snapshot;
                snapshot.active_skill_paths=new() { "res://resources/activeskills/剑修基础/斩剑.tres" };
                snapshot.combat_skill_slots_configured=false;Doll(unit).Configure(snapshot);Doll(unit).SetDisplayHeight(300);
                unit.Set("precision",100);unit.Set("critical",0);unit.Set("dodge",0);unit.Set("max_health",1000);unit.Set("current_health",1000);
            }
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output));
            Check(Doll(_actor).has_sword_idle(),"正式快照进入右手持剑待机");
            var before=Doll(_actor).GetNode<Sprite2D>("InkPresentation/Portrait").Texture;
            Check(!Doll(_actor).play_sword_action("missing_pose",Vector2.Right)&&Doll(_actor).GetNode<Sprite2D>("InkPresentation/Portrait").Texture==before,"缺图保留本衣装待机");
            await Capture("male_held_idle_formal");
            await Verify("斩剑","cut",_target,pause:true);
            var intent=((Creature)_actor).get_status_stacks("剑意");
            await Verify("养锋","nurture",_actor);
            Check(((Creature)_actor).get_status_stacks("剑意")==intent+1,"养锋仍按原规则获得剑意");
            await Verify("尽锋","finish",_target);
            Check(((Creature)_actor).get_status_stacks("剑意")==0,"尽锋命中仍消耗剑意");
            var defenderIntent=((Creature)_target).get_status_stacks("剑意");
            await Verify("斩剑","cut",_target,response:true);
            Check(((Creature)_target).get_status_stacks("剑意")==defenderIntent+1,"格剑应对仍获得一次剑意");
            Check(_target.Get("current_stamina").AsInt32()==5,"格剑原体力费用只扣一次");
            await VerifySwordHurt(Doll(_actor),"male");
            _sexPrefix="female_";
            foreach(var unit in new[]{_actor,_target})
            {
                var snapshot=_snapshots[unit];snapshot.gender=1;
                snapshot.active_skill_paths=new() { "res://resources/activeskills/剑修基础/斩剑.tres" };
                snapshot.combat_skill_slots_configured=false;Doll(unit).Configure(snapshot);Doll(unit).SetDisplayHeight(300);
            }
            Check(Doll(_actor).has_sword_idle()&&Doll(_actor).GetNode<Sprite2D>("InkPresentation/Portrait").Texture.ResourcePath.StartsWith(BattleSwordArt.FemaleArtRoot),"女修正式快照使用女修持剑待机");
            await Capture("female_held_idle_formal");
            await Verify("斩剑","cut",_target,pause:true);
            await Verify("养锋","nurture",_actor);
            await Verify("尽锋","finish",_target);
            await Verify("斩剑","cut",_target,response:true);
            await VerifySwordHurt(Doll(_actor),"female");
            var forge=new ProcessForgingTrial();var draft=forge.ExampleDraft(0);draft.Proficiency=60;
            var product=forge.Evaluate(draft).Product??throw new Exception("缺少合法成器测试快照");
            product.InstanceId="sword-art-isolated-check";
            // Keep the legal generated snapshot; vary only the visual shape
            // in these isolated fixtures to exercise all three registrations.
            foreach(var gender in new[]{0,1})
            foreach(var (shape,icon) in new[]{("细身","slender"),("宽展","broad"),("厚脊","spined")})
            {
                var sample=ProcessForgingTrial.Clone(product);sample.Shape=shape;
                var snapshot=new BattleEntityData{gender=gender,equipped_artifacts=new() { ForgedArtifactInventory.Create(sample) }};
                Doll(_actor).Configure(snapshot);
                var sword=(Sprite2D)Doll(_actor).FindChild("ReplaceableSwordIcon",true,false);
                var fingers=(Sprite2D)Doll(_actor).FindChild("RightHandNearFingers",true,false);
                Check(sword.Texture.ResourcePath.EndsWith(icon+"_sword.png")&&fingers.Visible&&fingers.ZIndex>sword.ZIndex,"实际装备快照选择剑图并保留握柄遮挡："+gender+" / "+icon);
            }
            var female=new BattlePaperDoll();_scene.AddChild(female);female.Configure(new BattleEntityData{gender=1});
            var femaleIdle=female.GetNode<Sprite2D>("InkPresentation/Portrait").Texture;
            Check(!female.play_sword_action("missing_pose",Vector2.Right)&&female.GetNode<Sprite2D>("InkPresentation/Portrait").Texture==femaleIdle,"女修缺图时保留自身外观");
            var defaultWeapon=BattleSwordArt.DefaultWeapon();
            Check(female.show_sword_orbit_idle(defaultWeapon.Icon,defaultWeapon.Grip,defaultWeapon.Tip)&&female.GetNode<Sprite2D>("InkPresentation/Portrait").Texture.ResourcePath.Contains("female_idle"),"女修御剑预览保留女修空手外观");
            female.Configure(new BattleEntityData{gender=0});female.show_sword_idle(defaultWeapon.Icon,defaultWeapon.Grip,defaultWeapon.Tip);
            Check(female.GetNode<Sprite2D>("InkPresentation/Portrait").Texture.ResourcePath.StartsWith(BattleSwordArt.IdleArtRoot),"同一节点切回男修时不复用女修缓存");female.QueueFree();
            var extra=new Node2D();_scene.AddChild(extra);var extraDoll=new BattlePaperDoll();extra.AddChild(extraDoll);
            extraDoll.Configure(new BattleEntityData{gender=0});extraDoll.play_sword_action("cut",Vector2.Right);
            var effect=BattleInkCombatVfx.PlayAttack(extra,_target,Skill("斩剑"));extra.QueueFree();await Wait(.1);
            Check(!IsInstanceValid(effect)||effect!.IsQueuedForDeletion(),"出手角色移除后清理剑招特效");
            GD.Print($"SWORD FORMAL PRESENTATION: {_checks} CHECKS PASSED");
            _scene.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);GetTree().Quit();
        }
        catch(Exception error){GD.PrintErr(error);GetTree().Paused=false;GetTree().Quit(1);}
    }
    private async Task Verify(string name,string pose,Node2D target,bool pause=false,bool response=false)
    {
        var home=_actor.GlobalPosition;var z=_actor.ZIndex;
        target.Set("current_health",1000);target.Set("current_stamina",6);
        var queue=_manager.get_battle_unit(_target);queue.skill_queue.Clear();queue.segment_action_index=0;
        ((HashSet<ulong>)typeof(BattleManager).GetField("_queuedHandledTargets",Hidden)!.GetValue(_manager)!).Clear();
        typeof(BattleManager).GetField("_segmentPhase",Hidden)!.SetValue(_manager,response?BattleManager.SegmentPhase.Executing:BattleManager.SegmentPhase.Planning);
        if(response)queue.skill_queue.Add(new BattleQueuedSkill(Skill("格剑"),_target));
        bool poseSeen=false,sequenceSeen=false,contactSeen=false,guardSeen=false,pauseSeen=false,captured=false,hurtSeen=false;
        var scale=Doll(_actor).Scale;var deadline=Time.GetTicksMsec()+12000;
        _executor.execute_skill(_actor,Skill(name),target,!response);
        while(Time.GetTicksMsec()<deadline)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var body=Doll(_actor).GetNode<Sprite2D>("InkPresentation/Portrait");
            poseSeen|=body.Texture?.ResourcePath.EndsWith(pose+"_body_back.png")==true;
            var sequence=Sequences().FirstOrDefault(s=>s.Animation.ToString()==pose);
            sequenceSeen|=sequence is not null;
            if(sequence is not null&&pause&&!pauseSeen)
            {
                pauseSeen=true;GetTree().Paused=true;var frame=sequence.Frame;var position=_actor.GlobalPosition;
                await Wait(.14,true);Check(sequence.Frame==frame&&_actor.GlobalPosition==position,"暂停同时冻结动作与PNG序列");GetTree().Paused=false;
            }
            if(response&&Doll(_target).is_sword_guard_presenting())guardSeen=true;
            contactSeen|=Sequences().Any(s=>s.Animation.ToString()==(response?"parry":"contact"));
            hurtSeen|=Doll(_target).GetNode<Sprite2D>("InkPresentation/Portrait").Texture.ResourcePath.EndsWith("hurt_body_back.png")&&((CanvasItem)Doll(_target).FindChild("ReplaceableSwordIcon",true,false)).IsVisibleInTree();
            if(Doll(_actor).Scale!=scale)throw new Exception("局部放大改变了人物自身比例");
            if(response&&contactSeen&&Sequences().Any(s=>s.Animation.ToString() is "hurt" or "impact"))throw new Exception("格剑后叠加了第二次受击特效");
            var bodyActive=body.Texture?.ResourcePath.EndsWith(pose+"_body_back.png")==true;
            var captureReady=pose=="nurture"?sequence is {Frame:>=5}
                :response?Doll(_target).is_sword_guard_presenting()&&Sequences().Any(s=>s.Animation.ToString()=="parry"&&s.Frame>=1)
                :bodyActive&&Sequences().Any(s=>s.Animation.ToString()=="contact"&&s.Frame>=1);
            if(!captured&&captureReady){await Capture(_sexPrefix+(response?"parry_formal":pose+"_formal"));captured=true;}
            if((sequenceSeen||poseSeen)&&!Busy())break;
        }
        Check(poseSeen&&sequenceSeen,"正式执行播放姿势与序列："+name);
        if(!captured)throw new Exception("没有捕捉到人物与接触特效同帧的有效画面："+name);
        Check(!Busy(),"正式执行结束清理演出："+name);
        Check(_actor.GlobalPosition.DistanceTo(home)<.01f&&_actor.ZIndex==z,"原站位和层级准确恢复："+name);
        Check(Doll(_actor).has_sword_idle(),"行动后回到持剑待机："+name);
        if(pose is "cut" or "finish")
        {
            Check(contactSeen,"命中接触序列接入："+name);
            if(!response)Check(hurtSeen,"正式命中受方仍握剑受击："+_sexPrefix+name);
        }
        if(response)Check(guardSeen,"实际应对插入使用格剑姿势");
    }
    private async Task VerifySwordHurt(BattlePaperDoll doll,string sex)
    {
        foreach(var mirrored in new[]{false,true})
        {
            doll.reset_presentation();doll.face_direction(mirrored?Vector2.Left:Vector2.Right);
            var scale=doll.Scale;
            doll.play_hit(mirrored?Vector2.Right:Vector2.Left,playHurtFx:false);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var body=doll.GetNode<Sprite2D>("InkPresentation/Portrait");
            var sword=(Sprite2D)doll.FindChild("ReplaceableSwordIcon",true,false);
            var front=(Sprite2D)doll.FindChild("RightHandNearFingers",true,false);
            Check(body.Texture.ResourcePath.EndsWith("hurt_body_back.png")&&sword.IsVisibleInTree()&&front.IsVisibleInTree()&&front.ZIndex>sword.ZIndex,"持剑受击保留剑和前景指节："+sex+" / "+mirrored);
            Check(((Node2D)sword.GetParent()).Scale.X==(mirrored?-1:1),"受击握点与剑随同一原点镜像："+sex+" / "+mirrored);
            GetTree().Paused=true;var position=doll.Position;var angle=sword.Rotation;
            await Wait(.12,true);Check(doll.Position==position&&sword.Rotation==angle,"受击暂停冻结身体与剑："+sex);GetTree().Paused=false;
            if(!mirrored)await Capture(sex+"_hurt_formal");
            await Wait(1.7);
            Check(doll.Position.Length()<.01f&&Mathf.Abs(doll.Rotation)<.001f&&doll.Scale==scale&&body.Texture.ResourcePath.EndsWith("idle_body_back.png")&&sword.IsVisibleInTree(),"受击恢复持剑待机、原比例与站位："+sex+" / "+mirrored);
        }
    }
}
