using Godot;

public partial class BattlePaperDoll
{
    public const string SwordIdleArtRoot="res://textures/characters/side_ink_sword_idles_20261023_v2/";
    private Texture2D? _swordIdleTexture, _swordOrbitTexture, _swordHandFrontTexture;
    private Node2D? _heldSwordPivot;
    private Sprite2D? _heldSwordSprite, _swordHandFrontOverlay;

    private Vector2 _swordIconAxis;
    private float _swordIdleAngle=34.6f, _swordOrbitClock, _swordGripToTip=780;
    private bool _swordIdleEnabled, _swordOrbitEnabled, _swordIdlePaused;
    private Vector2 _swordHeldGrip;
    public bool has_sword_idle() => _swordIdleEnabled;
    public void set_sword_idle_paused(bool paused) => _swordIdlePaused=paused;

    /// <summary>Independent weapon registration only. Never equips an item or changes battle data.</summary>
    public bool show_sword_idle(Texture2D icon,Vector2 gripUv,Vector2 tipUv,float gripToTip=780f,float bladeDegrees=float.NaN)
    {
        if (_queueAvatar is not null) return false;
        if(!_usesSideInk||_sideDead||_sidePresentation is null||!BattleSwordArt.TryPose("idle",out var idle,_sideFemale)) return false;
        _swordIdleTexture=idle!.Body;_swordHandFrontTexture=idle.HandFront;_swordHeldGrip=idle.Grip;
        var pixels=icon.GetSize();
        var grip=gripUv*pixels;var axis=(tipUv-gripUv)*pixels;
        if(axis.LengthSquared()<1) return false;
        _heldSwordPivot??=new Node2D {Name="SwordIdleRegistration",ZIndex=0};
        if(_heldSwordPivot.GetParent() is null) _sidePresentation.AddChild(_heldSwordPivot);
        _heldSwordSprite??=new Sprite2D {Name="ReplaceableSwordIcon",Centered=false,TextureFilter=TextureFilterEnum.Linear};
        if(_heldSwordSprite.GetParent() is null) _heldSwordPivot.AddChild(_heldSwordSprite);
        _heldSwordSprite.Texture=icon;
        _swordIconAxis=axis;_swordIdleAngle=float.IsNaN(bladeDegrees)?Mathf.RadToDeg(idle.Angle):bladeDegrees;_swordGripToTip=Mathf.Max(1,gripToTip);
        _heldSwordSprite.Offset=-grip;
        _heldSwordSprite.Scale=Vector2.One*_swordGripToTip/axis.Length();
        _swordIdleEnabled=true;_swordOrbitEnabled=false;_swordIdlePaused=false;_swordOrbitClock=0;
        hold_attack_contact(false);EndContactPoseHold();_actionTween?.Kill();
        Position=Vector2.Zero;Rotation=0;
        if(_sideIdle is not null) _sideIdle.Texture=_swordIdleTexture;
        ResetSideInkPose();
        return true;
    }
    public bool show_sword_orbit_idle(Texture2D icon,Vector2 gripUv,Vector2 tipUv,float gripToTip=780f)
    {
        if(!_usesSideInk||_sideDead) return false;
        _swordOrbitTexture=GD.Load<Texture2D>(_sideFemale?SideInkArtRoot+"female_idle.png":SwordIdleArtRoot+"male_orbit_idle.png");
        if(_swordOrbitTexture is null||!show_sword_idle(icon,gripUv,tipUv,gripToTip)) return false;
        _swordOrbitEnabled=true;
        if(_sideIdle is not null) _sideIdle.Texture=_swordOrbitTexture;
        ResetSideInkPose();
        return true;
    }
    public void clear_sword_idle()
    {
        _swordIdleEnabled=false;_swordOrbitEnabled=false;_swordIdlePaused=false;_swordOrbitClock=0;
        if(_heldSwordPivot is not null) _heldSwordPivot.Visible=false;
        if(_swordHandFrontOverlay is not null) _swordHandFrontOverlay.Visible=false;

        var idle=_sideTextures.GetValueOrDefault("idle");
        if(_sideIdle is not null) _sideIdle.Texture=idle;
        if(_sidePose=="idle"&&_portrait is not null&&idle is not null) _portrait.Texture=idle;
    }
    private Texture2D? SideIdleTexture() => _swordIdleEnabled?(_swordOrbitEnabled?_swordOrbitTexture:_swordIdleTexture):_sideTextures.GetValueOrDefault("idle");
    private void UpdateSwordIdleClock(float delta)
    {
        if(_swordIdleEnabled&&_swordOrbitEnabled&&!_swordIdlePaused&&!_contactPoseHeld&&_sidePose=="idle") _swordOrbitClock+=delta;
    }
    private void UpdateHeldSword()
    {
        if(_heldSwordPivot is null||_heldSwordSprite is null) return;
        var swordPose=_sidePose.StartsWith("sword_",StringComparison.Ordinal)?_sidePose[6..]:"";
        _heldSwordPivot.Visible=_swordIdleEnabled&&_usesSideInk&&!_sideDead&&(_sidePose=="idle"||swordPose.Length>0);
        _heldSwordPivot.Scale=new Vector2(_portrait?.FlipH==true?-1:1,1);
        if(_swordHandFrontOverlay is not null)_swordHandFrontOverlay.Visible=_heldSwordPivot.Visible&&!_swordOrbitEnabled;
        if(!_heldSwordPivot.Visible) return;
        if(_swordOrbitEnabled&&swordPose.Length==0)
        {
            if(_swordHandFrontOverlay is not null) _swordHandFrontOverlay.Visible=false;
    
            var t=_swordOrbitClock;
            var tremble=new Vector2(Mathf.Sin(t*Mathf.Tau/6.0f)*1.2f,
                Mathf.Sin(t*Mathf.Tau/4.8f)*3.5f);
            _heldSwordSprite.Position=new Vector2(410-_swordGripToTip*.42f,-1140)+tremble;
            // Lock the blade to horizontal; the common facing pivot mirrors it toward the enemy.
            _heldSwordSprite.Rotation=-_swordIconAxis.Angle();
            _heldSwordSprite.ZIndex=0;
            _heldSwordSprite.Modulate=Colors.White;
            return;
        }
        _heldSwordSprite.Position=_swordHeldGrip-SideInkGround;
        _heldSwordSprite.Rotation=Mathf.DegToRad(_swordIdleAngle)-_swordIconAxis.Angle();
        _heldSwordSprite.Modulate=Colors.White;_heldSwordSprite.ZIndex=2;
        _swordHandFrontOverlay??=new Sprite2D {Name="RightHandNearFingers",Centered=false,Position=-SideInkGround,ZIndex=3,TextureFilter=TextureFilterEnum.Linear};
        if(_swordHandFrontOverlay.GetParent() is null) _heldSwordPivot.AddChild(_swordHandFrontOverlay);
        _swordHandFrontOverlay.Texture=_swordHandFrontTexture;
        if(swordPose.Length>0&&BattleSwordArt.TryPose(swordPose,out var art,_sideFemale))
        {
            _heldSwordSprite.Position=art!.Grip-SideInkGround;
            _heldSwordSprite.Rotation=art.Angle-_swordIconAxis.Angle();
            _swordHandFrontOverlay.Texture=art.HandFront;
        }
        _swordHandFrontOverlay.Visible=true;
    }
}
