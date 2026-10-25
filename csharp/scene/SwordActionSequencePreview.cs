using Godot;
using System.Text.Json;

/// <summary>Review-only choreography. Owns no skill, equipment or combat state.</summary>
public partial class SwordActionSequencePreview : Node2D
{
    public const string ArtRoot="res://textures/characters/side_ink_sword_actions_20261024_v5/";
    public const string FxRoot="res://textures/vfx/sword_action_sequences_20261024_v1/";
    private static readonly Dictionary<string,Texture2D> Textures=[];
    private static readonly Dictionary<string,int> Counts=new(){{"cut",24},{"parry",18},{"nurture",36},{"finish",24},{"contact",18}};
    private Node2D _rig=null!,_fxRig=null!,_unit=null!,_target=null!;
    private Sprite2D _body=null!,_front=null!,_sword=null!,_stroke=null!,_contact=null!,_incoming=null!;
    private BattlePaperDoll _doll=null!,_targetDoll=null!;
    private BattleLocalFocusView _focus=null!;
    private Control _stage=null!;
    private string _pose="cut";
    private Vector2 _home,_targetHome,_grip;
    private float _time,_angle,_height,_mirror=1,_targetVisual;
    private bool _paused,_manual,_contactSeen,_incomingSeen,_hasPoseArt;
    public float PreviewTime=>_time;
    public float PreviewLength=>_pose=="nurture"?2.05f:1.85f;
    public bool Paused=>_paused;
    private float ContactTime=>_pose=="cut"?.48f:_pose=="finish"?.45f:.48f;
    private float Hold=>_pose=="nurture"?0:_pose=="finish"?.19f:.14f;

    private static Texture2D? Texture(string path)
    {
        if(Textures.TryGetValue(path,out var cached))return cached;
        if(!Godot.FileAccess.FileExists(path))return null;
        using var image=Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if(image is null||image.IsEmpty())return null;
        return Textures[path]=ImageTexture.CreateFromImage(image);
    }
    private static Sprite2D Sprite(Node parent,string name,int depth=0)
    {
        var sprite=new Sprite2D{Name=name,TextureFilter=TextureFilterEnum.Linear,ZIndex=depth};
        parent.AddChild(sprite);return sprite;
    }
    public void Configure(string pose,Node2D unit,Node2D target,Control stage,float height,bool mirrored,
        Texture2D icon,Vector2 gripUv,Vector2 tipUv)
    {
        _pose=Counts.ContainsKey(pose)?pose:"cut";_unit=unit;_target=target;_stage=stage;
        _doll=unit.GetNode<BattlePaperDoll>("BattlePaperDoll");
        _targetDoll=target.GetNode<BattlePaperDoll>("BattlePaperDoll");
        _home=unit.Position;_targetHome=target.Position;_height=height;_mirror=mirrored?-1:1;
        BattleSwordArt.TryPose(_pose,out var art,_doll.side_ink_female());
        _grip=art is null?Vector2.Zero:art.Grip-BattlePaperDoll.SideInkGround;
        _angle=art?.Angle??0;
        _rig=new Node2D{Name="TwoLayerSwordPose"};AddChild(_rig);
        _body=Sprite(_rig,"BodyAndFarHand",0);_body.Centered=false;_body.Position=-new Vector2(1024,1900);
        _body.Texture=art?.Body;
        _sword=Sprite(_rig,"ReplaceableSword",1);_sword.Centered=false;SetWeapon(icon,gripUv,tipUv);
        _front=Sprite(_rig,"NearFingersAndThumb",2);_front.Centered=false;_front.Position=_body.Position;
        _front.Texture=art?.HandFront;
        _hasPoseArt=_body.Texture is not null&&_front.Texture is not null;
        _fxRig=new Node2D{Name="SwordSequenceRegistration"};AddChild(_fxRig);
        _stroke=Sprite(_fxRig,"PrimarySequence");_contact=Sprite(_fxRig,"ContactSequence");_incoming=Sprite(_fxRig,"IncomingSequence");
        foreach(var s in new[]{_stroke,_contact,_incoming}){s.RegionEnabled=true;s.ZIndex=BattlePresentationLayers.Effects;s.ZAsRelative=false;}
        _focus=new BattleLocalFocusView();AddChild(_focus);
        Sample();
    }
    public void SetWeapon(Texture2D icon,Vector2 gripUv,Vector2 tipUv)
    {
        var axis=(tipUv-gripUv)*icon.GetSize();
        _sword.Texture=icon;_sword.Offset=-gripUv*icon.GetSize();_sword.Position=_grip;
        _sword.Rotation=_angle-axis.Angle();_sword.Scale=Vector2.One*780/axis.Length();
        if(IsInstanceValid(_doll))_doll.show_sword_idle(icon,gripUv,tipUv);
    }
    public void SetPaused(bool paused)
    {
        _paused=paused;_doll.SetProcess(!paused);_targetDoll.SetProcess(false);
        _doll.set_response_time_stop(paused);_targetDoll.set_response_time_stop(paused);
    }
    public void Seek(float time)
    {
        _manual=true;
        if(time<_time){_contactSeen=false;_incomingSeen=false;_targetVisual=0;_targetDoll.set_response_time_stop(false);_targetDoll.reset_presentation();}
        _time=Mathf.Max(0,time);Sample();
    }
    public override void _Process(double delta)
    {
        if(_manual||_paused)return;
        _time=Mathf.Min(PreviewLength,_time+(float)delta);Sample();
    }
    private static float Ease(float t)=>1-Mathf.Pow(1-Mathf.Clamp(t,0,1),3);
    private void Sequence(Sprite2D sprite,string animation,float seconds,Vector2 at,Vector2 dimensions)
    {
        var index=Mathf.FloorToInt(seconds*30);
        sprite.Visible=index>=0&&index<Counts[animation];if(!sprite.Visible)return;
        var page=index/16;var tile=index%16;
        sprite.Texture=Texture(FxRoot+$"{animation}_atlas_{page}.png");
        if(sprite.Texture is null){sprite.Visible=false;return;}
        sprite.RegionRect=new Rect2(tile%4*768,tile/4*768,768,768);
        sprite.Position=at;sprite.Scale=dimensions/768;
    }
    private void Sample()
    {
        if(!IsInstanceValid(_unit)||!IsInstanceValid(_target))return;
        var visual=_time-Mathf.Clamp(_time-ContactTime,0,Hold);
        var attack=_pose is "cut" or "finish";
        var approach=attack?80*Ease((_time-.06f)/.19f)*(1-Ease((_time-1.11f)/.42f)):0;
        _unit.Position=_home+new Vector2(approach*_mirror,0);
        var action=visual>=.25f&&visual<(_pose=="nurture"?1.53f:1.02f);
        _unit.Visible=!action||!_hasPoseArt;_rig.Visible=action&&_hasPoseArt;
        _target.Visible=_pose!="nurture";
        _rig.Position=_unit.Position;_rig.Scale=new Vector2(_mirror,1)*(_height/1375);
        // Auxiliary force translation is small; closeup is a crop of the stage.
        var impulse=attack?Mathf.Sin(Mathf.Clamp((visual-.25f)/.30f,0,1)*Mathf.Pi)*6:0;
        _rig.Position+=new Vector2(impulse*_mirror,0);
        _fxRig.Position=_rig.Position;_fxRig.Scale=_rig.Scale;
        var start=_pose=="parry"?.40f:.29f;
        var at=_pose switch{"cut"=>_grip+new Vector2(310,-80),"parry"=>_grip+Vector2.Right.Rotated(_angle)*360,"nurture"=>_grip+new Vector2(390,-8),_=>_grip+new Vector2(370,0)};
        var size=_pose switch{"cut"=>new Vector2(1310,1250),"parry"=>new Vector2(650,650),"nurture"=>new Vector2(1110,360),_=>new Vector2(1500,560)};
        Sequence(_stroke,_pose,visual-start,at,size);
        if(attack)
        {
            var contactAt=_fxRig.ToLocal(BattleInkCombatVfx.Anchor(_target));
            Sequence(_contact,"contact",visual-ContactTime+2f/30,contactAt,Vector2.One*430);
            _contact.Visible&=_time>=ContactTime;
        }
        else _contact.Visible=false;
        if(_pose=="parry")Sequence(_incoming,"finish",visual-.20f,at+new Vector2(400,0),new Vector2(-1000,320));
        else _incoming.Visible=false;
        if(_pose=="parry"&&!_incomingSeen&&_time>=.20f)
        {
            _incomingSeen=true;_targetDoll.face_direction(new Vector2(-_mirror,0));_targetDoll.show_art_review_pose("chop");_targetVisual=.20f;
        }
        if(!_contactSeen&&_time>=ContactTime)
        {
            _contactSeen=true;
            if(attack){_targetDoll.show_art_review_pose("hurt");_targetVisual=ContactTime;}
            // Parry has one incoming stroke and one contact; never a second hurt event.
        }
        _targetDoll.SetProcess(false);
        if(!_paused&&visual>_targetVisual){_targetDoll._Process(visual-_targetVisual);_targetVisual=visual;}
        var recoil=attack&&_time>=ContactTime?1-Mathf.SmoothStep(0,1,Mathf.Clamp((visual-ContactTime)/.30f,0,1)):0;
        _targetDoll.Position=new Vector2(_mirror*40*recoil,0);
        _doll.SetProcess(!_paused);
        var focus=(_unit.GetGlobalTransformWithCanvas().Origin+_target.GetGlobalTransformWithCanvas().Origin)*.5f+new Vector2(0,-150);
        var region=_stage.GetGlobalRect();region.Position+=new Vector2(0,124);region.Size-=new Vector2(0,250);
        var envelope=_pose=="nurture"?0:Ease((_time-(ContactTime-.12f))/.07f)*(1-Ease((_time-(ContactTime+Hold+.10f))/.47f));
        var kick=(_time>=ContactTime&&_time<ContactTime+.065f)?new Vector2(Mathf.Sin((_time-ContactTime)*140),-.25f)*3:Vector2.Zero;
        _focus.present_region(region,focus,1+envelope*(_pose=="finish"?.30f:.22f),envelope*.08f,kick);
    }
    public override void _ExitTree()
    {
        if(IsInstanceValid(_unit)){_unit.Visible=true;_unit.Position=_home;_doll.set_response_time_stop(false);_doll.SetProcess(true);}
        if(IsInstanceValid(_target)){_target.Visible=true;_target.Position=_targetHome;_targetDoll.set_response_time_stop(false);_targetDoll.reset_presentation();_targetDoll.SetProcess(true);}
    }
}
