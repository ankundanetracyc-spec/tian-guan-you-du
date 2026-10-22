using Godot;

public partial class BattlePaperDoll
{
    public const string SwordIdleArtRoot="res://textures/characters/side_ink_sword_idle_20261022_v1/";
    private Texture2D? _swordIdleTexture;
    private Node2D? _heldSwordPivot;
    private Sprite2D? _heldSwordSprite;
    private bool _swordIdleEnabled;
    public bool has_sword_idle() => _swordIdleEnabled;

    /// <summary>Icon anchors use normalized source-image coordinates. Grip position
    /// stays fixed when replacing images with different canvas sizes or padding.</summary>
    public bool show_sword_idle(Texture2D icon,Vector2 gripUv,Vector2 tipUv,float gripToTip=780f,float bladeDegrees=-60f)
    {
        if(!_usesSideInk||_sideFemale||_sideDead||_sidePresentation is null) return false;
        _swordIdleTexture??=GD.Load<Texture2D>(SwordIdleArtRoot+"male_idle.png");
        if(_swordIdleTexture is null) return false;
        var pixels=icon.GetSize();
        var grip=gripUv*pixels;var axis=(tipUv-gripUv)*pixels;
        if(axis.LengthSquared()<1) return false;
        _heldSwordPivot??=new Node2D {Name="HeldSwordRegistration",ZIndex=0};
        if(_heldSwordPivot.GetParent() is null) _sidePresentation.AddChild(_heldSwordPivot);
        _heldSwordSprite??=new Sprite2D {Name="ReplaceableSwordIcon",Centered=false,TextureFilter=TextureFilterEnum.Linear};
        if(_heldSwordSprite.GetParent() is null) _heldSwordPivot.AddChild(_heldSwordSprite);
        _heldSwordSprite.Texture=icon;
        _heldSwordSprite.Offset=-grip;
        _heldSwordSprite.Position=new Vector2(1466,994)-SideInkGround;
        _heldSwordSprite.Rotation=Mathf.DegToRad(bladeDegrees)-axis.Angle();
        _heldSwordSprite.Scale=Vector2.One*Mathf.Max(1,gripToTip)/axis.Length();
        _swordIdleEnabled=true;
        hold_attack_contact(false);
        EndContactPoseHold();
        _actionTween?.Kill();
        Position=Vector2.Zero;Rotation=0;
        if(_sideIdle is not null) _sideIdle.Texture=_swordIdleTexture;
        ResetSideInkPose();
        UpdateHeldSword();
        return true;
    }

    public void clear_sword_idle()
    {
        _swordIdleEnabled=false;
        if(_heldSwordPivot is not null) _heldSwordPivot.Visible=false;
        var idle=_sideTextures.GetValueOrDefault("idle");
        if(_sideIdle is not null) _sideIdle.Texture=idle;
        if(_sidePose=="idle"&&_portrait is not null&&idle is not null) _portrait.Texture=idle;
    }
    private Texture2D? SideIdleTexture() => _swordIdleEnabled?_swordIdleTexture:_sideTextures.GetValueOrDefault("idle");
    private void UpdateHeldSword()
    {
        if(_heldSwordPivot is null) return;
        _heldSwordPivot.Visible=_swordIdleEnabled&&_usesSideInk&&!_sideDead&&_sidePose=="idle";
        _heldSwordPivot.Scale=new Vector2(_portrait?.FlipH==true?-1:1,1);
    }
}
