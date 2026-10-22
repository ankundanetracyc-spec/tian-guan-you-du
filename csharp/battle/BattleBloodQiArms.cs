using Godot;

/// <summary>Authored translucent limbs with native, depth-split circulating blood qi.
/// Count and triggers are supplied by a caller;
/// this view does not calculate qi-blood thresholds, damage, chance or block.</summary>
public partial class BattleBloodQiArms : Node2D
{
    private sealed class Arm
    {
        public Node2D Pivot = null!;
        public ShaderMaterial Material = null!;
        public Sprite2D Surface = null!;
        public BattleBloodQiOrbit Orbit = null!;
        public readonly Dictionary<string,Texture2D?> Textures=[];
        public readonly Dictionary<string,Vector2[]> Paths=[];
        public Vector2 Root;
        public string Pose="";
        public float Presence, Clock;
        public string Action = "idle";
    }
    private static Shader? _shader;
    private readonly Arm[] _arms = [new(),new()];
    private readonly Queue<(int Arm,Node2D? Target)> _pendingFollowUps=new();
    private Node2D _subject = null!;
    private BattlePaperDoll? _doll;
    private int _count;
    private float _clock, _hold;
    private int _qiLevel=4,_onset=4;
    private float _visualScale=.95f,_targetScale=.95f;
    private bool _actionGrouped;
    private int _originalSubjectZ,_liftedSubjectZ;
    private bool _depthLifted;
    private int _nextFollowUpArm=1;
    public int ArmCount => _qiLevel>=_onset?_count:0;
    public int QiLevel => _qiLevel;
    public int MaximumSizeLevel => _onset+16;
    /// <summary>常驻待机不计入；包含尚未开始的追加出拳。</summary>
    public bool IsActionPlaying => _pendingFollowUps.Count>0 || _arms.Any(arm=>arm.Action!="idle");
    public const float FollowUpContactSeconds = .14f;

    public static BattleBloodQiArms ForSubject(Node2D subject)
    {
        if (subject.GetNodeOrNull<BattleBloodQiArms>("BloodQiArms") is { } existing) return existing;
        var view = new BattleBloodQiArms { Name="BloodQiArms", _subject=subject };
        subject.AddChild(view);
        return view;
    }

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Pausable;
        AddToGroup("battle_persistent_visuals");
        _doll=_subject.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        _shader ??= GD.Load<Shader>("res://shaders/battle_blood_qi_body.gdshader");
        for(var i=0;i<2;i++)
        {
            var arm=_arms[i];
            arm.Pivot=new Node2D { Name="QiArm"+i };
            arm.Material=new ShaderMaterial { Shader=_shader };
            arm.Material.SetShaderParameter("presence",0f);
            AddChild(arm.Pivot);
            var root=i==0?new Vector2(720,1184):new Vector2(900,1178);
            arm.Root=root;
            var sourceRoot=i==0?new Vector2(590,755):new Vector2(700,750);
            Vector2[] sourcePath=i==0
                ? [new(590,755),new(370,720),new(185,615),new(225,450),new(365,310),new(560,230)]
                : [new(700,750),new(915,725),new(1050,635),new(1000,455),new(900,310),new(800,230)];
            var path=sourcePath.Select(p=>(p-sourceRoot)*1.25f).ToArray();
            arm.Paths["idle"]=path;
            Vector2[] punch=i==0
                ? [new(176,563),new(370,563),new(620,640),new(810,655),new(980,670),new(1125,690)]
                : [new(313,559),new(530,650),new(725,715),new(900,720),new(1019,715),new(1117,754)];
            arm.Paths["punch"]=punch.Select(p=>(p-punch[0])*1.25f).ToArray();
            Vector2[] guard=[new(715,813),new(900,850),new(1060,815),new(1030,650),new(1000,500),new(950,300)];
            if(i==1) arm.Paths["guard"]=guard.Select(p=>(p-guard[0])*1.25f).ToArray();
            foreach(var pose in arm.Paths.Keys)
            {
                var asset="res://textures/vfx/blood_qi_avatar_20261020_v4/"+(i==0?"left_":"right_")+pose+".png";
                arm.Textures[pose]=ResourceLoader.Exists(asset)?GD.Load<Texture2D>(asset):null;
            }
            arm.Surface=new Sprite2D { Name="BloodQiBody",Material=arm.Material,Centered=false,Offset=-root };
            arm.Pivot.AddChild(arm.Surface);
            arm.Orbit=new BattleBloodQiOrbit {Name="CirculatingBloodQi"};
            arm.Pivot.AddChild(arm.Orbit);
            arm.Orbit.Configure(i==0,path);
            SetPose(arm,"idle");
        }
        SyncTransform();
    }

    public override void _EnterTree() => RenderingServer.FramePreDraw+=SyncTransform;
    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw-=SyncTransform;
        RestoreActionDepth();
    }

    public void SetArmCount(int count, bool immediate=false)
    {
        var next=Mathf.Clamp(count,0,2);
        if(!immediate&&next==_count)return;
        _count=next;
        if(immediate)
        {
            RestoreActionDepth();
            _pendingFollowUps.Clear();
            for(var i=0;i<2;i++)
            {
                _arms[i].Action="idle"; _arms[i].Clock=0;
                _arms[i].Presence=i<ArmCount?1:0;
            }
        }
        if(ArmCount==0)_pendingFollowUps.Clear();
        UpdateWaitGroup();
    }
    public Vector2 FollowUpContactPoint()
    {
        var arm=_arms[ArmCount==1?0:1];
        return arm.Pivot.ToGlobal(arm.Paths["punch"][^1]);
    }
    public void PlayFollowUp(Node2D? target=null)
    {
        if(ArmCount==0) return;
        // One arm uses the original left manifestation; after the second appears,
        // only the right arm follows up. Do not turn the preview into two impacts.
        for(var i=0;i<2;i++) _arms[i].Action="idle";
        var chosen=ArmCount==1?0:1;
        _arms[chosen].Action="punch"; _arms[chosen].Clock=0;
        PrepareActionDepth(target);
        UpdateWaitGroup();
    }
    public void QueueFollowUp(Node2D? target=null)
    {
        var count=ArmCount;
        if(count<=0)return;
        var chosen=count==1?0:_nextFollowUpArm;
        if(count>1)_nextFollowUpArm=1-_nextFollowUpArm;
        if(_arms[chosen].Action=="idle")StartFollowUp(chosen,target);
        else _pendingFollowUps.Enqueue((chosen,target));
    }
    public bool PlayAutoGuard(Node2D? incoming=null)
    {
        if(ArmCount<2) return false;
        _arms[1].Action="guard"; _arms[1].Clock=0;
        PrepareActionDepth(incoming);
        UpdateWaitGroup();
        return true;
    }
    public void HoldContact(float seconds) => _hold=Mathf.Max(_hold,seconds);

    public void SetQiVisualLevel(int current,int firstArmOnset)
    {
        _qiLevel=Mathf.Max(0,current);
        _onset=Mathf.Max(0,firstArmOnset);
        var growth=Mathf.Clamp((_qiLevel-_onset)/16f,0,1);
        _targetScale=Mathf.Lerp(.95f,1.65f,growth);
        UpdateWaitGroup();
    }

    public override void _Process(double delta)
    {
        if(!IsInstanceValid(_subject)||!IsInstanceValid(_doll)) { QueueFree(); return; }
        if(!_doll!.uses_side_ink()||_doll.side_ink_dead()) _count=0;
        var dt=(float)delta;
        if(_hold>0) { _hold=Mathf.Max(0,_hold-dt); SyncTransform(); return; }
        _clock+=dt;
        _visualScale=Mathf.Lerp(_visualScale,_targetScale,1-Mathf.Exp(-dt*10));
        for(var i=0;i<2;i++)
        {
            var arm=_arms[i];
            arm.Clock+=dt;
            arm.Presence=Mathf.MoveToward(arm.Presence,i<ArmCount?1:0,dt/.55f);
            if(arm.Clock>=.84f) arm.Action="idle";
            var active=arm.Action!="idle"&&arm.Clock>=0;
            var drive=active ? Mathf.SmoothStep(0,1,Mathf.Clamp(arm.Clock/.14f,0,1))
                *(1-Mathf.SmoothStep(0,1,Mathf.Clamp((arm.Clock-.30f)/.33f,0,1))) : 0;
            // The first manifestation is the left/far arm. Both roots float outside
            // the shoulder silhouette; they do not grow from the figure's anatomy.
            arm.Pivot.Position=_doll.side_ink_blood_shoulder(i==1)+new Vector2(i==0?-465:390,80)
                +new Vector2(Mathf.Sin(_clock*.73f+i*1.7f)*9,Mathf.Sin(_clock*1.1f+i*2.3f)*8);
            arm.Pivot.Scale=Vector2.One*(i==0?1f:.92f)*_visualScale;
            // Same scale and shoulder anchor in every key pose. Reach comes from
            // the drawn elbow/forearm, never an enlarged or stretched idle limb.
            arm.Pivot.Rotation=i==0?.012f:-.015f;
            SetPose(arm,active&&arm.Clock>=.07f&&arm.Clock<.44f?arm.Action:"idle");
            arm.Material.SetShaderParameter("effect_time",_clock+i*.62f);
            arm.Material.SetShaderParameter("presence",arm.Presence);
            arm.Material.SetShaderParameter("activity",drive);
            if(arm.Presence>.001f) arm.Orbit.Advance(_clock+i*.62f,arm.Presence,drive);
            arm.Pivot.Visible=arm.Presence>.001f;
        }
        StartQueuedFollowUps();
        SyncTransform();
        if(_arms.All(arm=>arm.Action=="idle")) RestoreActionDepth();
        UpdateWaitGroup();
    }

    private void StartFollowUp(int index,Node2D? target)
    {
        if(index<0||index>=ArmCount||_arms[index].Action!="idle")return;
        _arms[index].Action="punch";_arms[index].Clock=0;
        PrepareActionDepth(target);
        UpdateWaitGroup();
    }
    private void StartQueuedFollowUps()
    {
        if(_pendingFollowUps.Count==0||ArmCount==0)return;
        var attempts=_pendingFollowUps.Count;
        while(attempts-->0&&_pendingFollowUps.TryDequeue(out var pending))
        {
            var index=pending.Arm;
            if(index>=ArmCount)index=ArmCount-1;
            if(_arms[index].Action!="idle")
            {
                var alternate=ArmCount>1?1-index:index;
                if(alternate>=ArmCount||_arms[alternate].Action!="idle")
                {
                    _pendingFollowUps.Enqueue(pending);
                    continue;
                }
                index=alternate;
            }
            StartFollowUp(index,pending.Target);
        }
    }

    private static int EffectiveDepth(CanvasItem item)
    {
        var depth=0;
        for(Node? node=item;node is not null;node=node.GetParent())
            if(node is CanvasItem canvas) {depth+=canvas.ZIndex;if(!canvas.ZAsRelative)break;}
        return depth;
    }
    private void PrepareActionDepth(Node2D? other)
    {
        RestoreActionDepth();
        if(!IsInstanceValid(other)||!IsInstanceValid(_subject)) return;
        // Target < manifested arms < their owner. Lift the complete owner group,
        // keeping the blood arms behind its clothing while covering the opponent.
        var lift=Mathf.Max(0,EffectiveDepth(other!)+8-EffectiveDepth(_subject));
        _originalSubjectZ=_subject.ZIndex;
        _liftedSubjectZ=Mathf.Clamp(_originalSubjectZ+lift,-4000,4000);
        _subject.ZIndex=_liftedSubjectZ;
        _depthLifted=lift>0;
        SyncTransform();
    }
    private void RestoreActionDepth()
    {
        if(!_depthLifted) return;
        if(IsInstanceValid(_subject)&&_subject.ZIndex==_liftedSubjectZ) _subject.ZIndex=_originalSubjectZ;
        _depthLifted=false;
    }

    private static void SetPose(Arm arm,string pose)
    {
        if(arm.Textures.GetValueOrDefault(pose) is null) pose="idle";
        if(arm.Pose==pose) return;
        arm.Pose=pose;
        arm.Surface.Texture=arm.Textures.GetValueOrDefault(pose);
        var path=arm.Paths[pose];
        arm.Material.SetShaderParameter("flow_path",path.Select(p=>(p+arm.Root)/2048f).ToArray());
        arm.Orbit.SetPath(path);
    }

    private void UpdateWaitGroup()
    {
        // Only transient formation/actions join the completion wait. Idle arms never block turns.
        var busy=_arms.Select((arm,index)=>(arm,index)).Any(pair=>
            pair.arm.Action!="idle" || Mathf.Abs(pair.arm.Presence-(pair.index<ArmCount?1:0))>.001f);
        if(busy==_actionGrouped) return;
        _actionGrouped=busy;
        if(busy) AddToGroup("battle_action_visuals");
        else RemoveFromGroup("battle_action_visuals");
    }
    private void SyncTransform()
    {
        if(!IsInstanceValid(_doll)||!IsInstanceValid(_subject)) return;
        var mirror=_doll!.side_ink_faces_left()?-1f:1f;
        GlobalTransform=_doll.side_ink_visual_transform()*new Transform2D(new Vector2(mirror,0),Vector2.Down,Vector2.Zero);
        Visible=_subject.IsVisibleInTree()&&_doll.uses_side_ink();
        var depth=0;
        for(Node? node=_doll;node is not null;node=node.GetParent())
            if(node is CanvasItem canvas) { depth+=canvas.ZIndex; if(!canvas.ZAsRelative) break; }
        for(var i=0;i<2;i++)
        {
            var arm=_arms[i];
            if(arm.Pivot is null) continue;
            arm.Pivot.ZAsRelative=false;
            // A single arm crosses in front of its owner only on the punch key.
            // Its rear ribbon also clears the owner's portrait (portrait Z + 1).
            // Resting arms and all dual-arm poses retain their rear placement.
            var singlePunch=ArmCount==1&&i==0&&arm.Action=="punch"&&arm.Pose=="punch";
            arm.Pivot.ZIndex=singlePunch?depth+3:i==0?depth-6:depth-3;
        }
    }
}
