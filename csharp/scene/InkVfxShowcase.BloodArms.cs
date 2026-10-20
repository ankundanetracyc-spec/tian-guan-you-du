using Godot;

public partial class InkVfxShowcase
{
    private BattleBloodQiArms? _bloodArms;
    private Button? _bloodCountButton;
    private int _bloodPreviewCount=2;
    private int _bloodQiLevel=4,_bloodOnset=4;
    private HBoxContainer? _bloodGrowthControls;
    private HSlider? _bloodQiSlider;
    private SpinBox? _bloodOnsetBox;
    private Label? _bloodGrowthLabel;
    private bool IsBloodPreview => _current.Preview is PreviewType.BloodOne or PreviewType.BloodTwo
        or PreviewType.BloodFollow or PreviewType.BloodGuard or PreviewType.BloodFade;
    private bool IsBloodPersistent => _current.Preview is PreviewType.BloodOne or PreviewType.BloodTwo;

    public void PreviewBloodArms(int mode=1,int direction=0,int arms=2)
    {
        _previewDirection=Mathf.PosMod(direction,4);
        _bloodPreviewCount=Mathf.Clamp(arms,1,2);
        var ids=new[]{"blood_one","blood_two","blood_follow","blood_guard","blood_fade"};
        var id=ids[Mathf.Clamp(mode,0,4)];
        var index=System.Array.FindIndex(_effects,effect=>effect.Id==id);
        if(index>=0) SelectEffect(index);
    }
    public void SetPreviewBloodQi(int current,int onset=4)
    {
        _bloodQiLevel=Mathf.Max(0,current); _bloodOnset=Mathf.Max(0,onset);
        _bloodArms?.SetQiVisualLevel(_bloodQiLevel,_bloodOnset);
        if(_bloodQiSlider is not null) _bloodQiSlider.SetValueNoSignal(_bloodQiLevel);
        if(_bloodOnsetBox is not null) _bloodOnsetBox.SetValueNoSignal(_bloodOnset);
        if(_bloodGrowthLabel is not null) _bloodGrowthLabel.Text=$"气血 {_bloodQiLevel}  ·  {_bloodOnset}层显臂，{_bloodOnset+16}层最大";
    }
    // Art review entry: same runtime rig and fixed anchors, held on one authored key.
    public async void PreviewBloodActionKey(int mode)
    {
        PreviewBloodArms(mode==0?0:1,0,mode==0?1:2);
        SetPreviewBloodQi(12,4);
        var generation=_previewGeneration;
        await BloodWait(.65f);
        if(!BloodPreviewCurrent(generation)) return;
        var doll=_battleAttacker!.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        if(mode==2) {doll?.play_guard();_bloodArms!.PlayAutoGuard();}
        else {doll?.return_to_blood_arm_idle();_bloodArms!.PlayFollowUp();}
        await BloodWait(.16f);
        if(BloodPreviewCurrent(generation)) _bloodArms!.HoldContact(30);
    }
    private void BuildBloodGrowthControls(Control stage)
    {
        _bloodGrowthControls=new HBoxContainer { Position=new Vector2(22,122),Visible=false };
        _bloodGrowthControls.AddThemeConstantOverride("separation",10);
        _bloodGrowthControls.AddChild(MakeLabel("显臂层数",14,new Color("615645")));
        _bloodOnsetBox=new SpinBox { MinValue=0,MaxValue=64,Step=1,Value=4,CustomMinimumSize=new Vector2(75,32) };
        _bloodGrowthControls.AddChild(_bloodOnsetBox);
        _bloodQiSlider=new HSlider { MinValue=0,MaxValue=80,Step=1,Value=4,CustomMinimumSize=new Vector2(190,32) };
        _bloodGrowthControls.AddChild(_bloodQiSlider);
        _bloodGrowthLabel=MakeLabel("",14,new Color("615645"));
        _bloodGrowthControls.AddChild(_bloodGrowthLabel);
        _bloodQiSlider.ValueChanged+=value=>SetPreviewBloodQi((int)value,_bloodOnset);
        _bloodOnsetBox.ValueChanged+=value=>SetPreviewBloodQi(_bloodQiLevel,(int)value);
        stage.AddChild(_bloodGrowthControls);
        SetPreviewBloodQi(_bloodQiLevel,_bloodOnset);
    }
    private void ResetBloodPreview()
    {
        if(IsInstanceValid(_bloodArms))
        {
            _bloodArms!.GetParent()?.RemoveChild(_bloodArms);
            _bloodArms.QueueFree();
        }
        _bloodArms=null;
        if(_bloodGrowthControls is not null) _bloodGrowthControls.Visible=IsBloodPreview;
        if(_bloodCountButton is not null)
        {
            _bloodCountButton.Visible=_current.Preview==PreviewType.BloodFollow;
            _bloodCountButton.Text=_bloodPreviewCount==2?"追击：双臂":"追击：单臂";
        }
    }
    private void PositionBloodPreview(Vector2 center,Vector2 size)
    {
        _stage?.SetMeta("battle_focus_top",IsBloodPreview?168:108);
        if(!IsBloodPreview||_battleAttacker is null||_battleTarget is null) return;
        var height=Mathf.Min(240,size.Y*.48f);
        _battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.SetDisplayHeight(height);
        _battleTarget.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.SetDisplayHeight(height);
        if(IsBloodPersistent||_current.Preview==PreviewType.BloodFade)
        {
            // Reserve the space above the figure for the raised hands at maximum growth.
            _battleAttacker.Position=center+new Vector2(0,192);
            _battleTarget.Visible=false;
            _battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.face_direction(_previewDirection==1?Vector2.Left:Vector2.Right);
        }
        else
        {
            _battleAttacker.Position+=new Vector2(0,60);
            _battleTarget.Position+=new Vector2(0,60);
        }
    }
    private void StartBloodPreview()
    {
        if(_battleAttacker is null) return;
        _bloodArms=BattleBloodQiArms.ForSubject(_battleAttacker);
        _bloodArms.SetQiVisualLevel(_bloodQiLevel,_bloodOnset);
        _bloodArms.SetArmCount(_current.Preview==PreviewType.BloodOne?1:
            _current.Preview==PreviewType.BloodFollow?_bloodPreviewCount:2);
        if(_current.Preview==PreviewType.BloodFollow) PlayBloodFollowPreview(_previewGeneration);
        if(_current.Preview==PreviewType.BloodGuard) PlayBloodGuardPreview(_previewGeneration);
        if(_current.Preview==PreviewType.BloodFade) FadeBloodPreview(_previewGeneration);
    }
    private bool BloodPreviewCurrent(int generation) => IsInsideTree()&&generation==_previewGeneration
        &&IsInstanceValid(_bloodArms)&&IsInstanceValid(_battleAttacker)&&IsInstanceValid(_battleTarget);
    private async System.Threading.Tasks.Task BloodWait(float seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds,processAlways:false),SceneTreeTimer.SignalName.Timeout);
    private async void FadeBloodPreview(int generation)
    {
        await BloodWait(1.15f);
        if(BloodPreviewCurrent(generation)) _bloodArms!.SetArmCount(0);
    }
    private async System.Threading.Tasks.Task MoveBloodActor(Node2D actor,Vector2 destination,float seconds)
    {
        _previewMotion=actor.CreateTween();
        _previewMotion.TweenProperty(actor,"global_position",destination,seconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        await BloodWait(seconds);
    }
    private async void PlayBloodFollowPreview(int generation)
    {
        await BloodWait(.85f);
        if(!BloodPreviewCurrent(generation)) return;
        var actor=_battleAttacker!; var target=_battleTarget!; var home=actor.GlobalPosition;
        await MoveBloodActor(actor,BattleAttackMotion.ContactPosition(actor,target,home),.18f);
        if(!BloodPreviewCurrent(generation)) return;
        var skill=GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres");
        var direction=(target.GlobalPosition-actor.GlobalPosition).Normalized();
        var doll=actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        BattleInkCombatVfx.PresentAttackPose(actor,direction,skill);
        BattleInkCombatVfx.PlayAttack(actor,target,skill);
        await BloodWait(.30f);
        if(!BloodPreviewCurrent(generation)) return;
        BattleInkCombatVfx.PlayImpact(target,skill,actor);
        target.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_hit(direction,playHurtFx:false);
        doll?.hold_attack_contact(true);
        await BloodWait(.12f);
        if(!BloodPreviewCurrent(generation)) return;
        doll?.return_to_blood_arm_idle();
        await BloodWait(.08f);
        if(!BloodPreviewCurrent(generation)) return;
        // The owner's lead strike has ended. Settle into a ready stance at the
        // distance reached by this authored fist, rather than stretching the arm.
        var fist=_bloodArms!.FollowUpContactPoint();
        var contactX=target.GlobalPosition.X-direction.X*28;
        var correction=Mathf.Clamp(contactX-fist.X,-160,100);
        if(Mathf.Abs(correction)>3)
        {
            await MoveBloodActor(actor,actor.GlobalPosition+new Vector2(correction,0),.12f);
            if(!BloodPreviewCurrent(generation)) return;
        }
        _bloodArms!.PlayFollowUp(target);
        await BloodWait(.14f);
        if(!BloodPreviewCurrent(generation)) return;
        BattleInkCombatVfx.EmphasizeContact(actor,target,.8f);
        target.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_hit(direction,.65f,playHurtFx:false);
        _bloodArms!.HoldContact(.08f);
        await BloodWait(.80f);
        if(!BloodPreviewCurrent(generation)) return;
        doll?.hold_attack_contact(false);
        doll?.play_retreat(direction);
        await MoveBloodActor(actor,home,.22f);
    }
    private async void PlayBloodGuardPreview(int generation)
    {
        await BloodWait(.85f);
        if(!BloodPreviewCurrent(generation)) return;
        var defender=_battleAttacker!; var incoming=_battleTarget!; var home=incoming.GlobalPosition;
        await MoveBloodActor(incoming,BattleAttackMotion.ContactPosition(incoming,defender,home),.18f);
        if(!BloodPreviewCurrent(generation)) return;
        var direction=(defender.GlobalPosition-incoming.GlobalPosition).Normalized();
        var skill=GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres");
        BattleInkCombatVfx.PresentAttackPose(incoming,direction,skill);
        BattleInkCombatVfx.PlayAttack(incoming,defender,skill);
        await BloodWait(.16f);
        if(!BloodPreviewCurrent(generation)) return;
        defender.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_guard();
        _bloodArms!.PlayAutoGuard(incoming);
        await BloodWait(.14f);
        if(!BloodPreviewCurrent(generation)) return;
        BattleInkCombatVfx.EmphasizeContact(incoming,defender,1f);
        _bloodArms!.HoldContact(.12f);
        incoming.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.hold_attack_contact(true);
        await BloodWait(.17f);
        if(!BloodPreviewCurrent(generation)) return;
        incoming.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.hold_attack_contact(false);
        incoming.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_retreat(direction);
        await MoveBloodActor(incoming,home,.22f);
        await BloodWait(.35f);
        if(BloodPreviewCurrent(generation)) defender.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.return_to_blood_arm_idle();
    }
}
