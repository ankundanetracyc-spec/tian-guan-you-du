using Godot;

public partial class InkVfxShowcase
{
    private static readonly (string Name,string Icon,Vector2 Grip,Vector2 Tip)[] SwordAppearances=
    [
        ("细剑","res://textures/ui/forged_swords_20261023_v1/slender_sword.png",new(.874f,.512f),new(.024f,.505f)),
        ("阔剑","res://textures/ui/forged_swords_20261023_v1/broad_sword.png",new(.866f,.521f),new(.023f,.522f)),
        ("脊剑","res://textures/ui/forged_swords_20261023_v1/spined_sword.png",new(.854f,.551f),new(.037f,.549f))
    ];
    private int _swordAppearance;
    private OptionButton? _swordChoice;
    private HBoxContainer? _swordControls;
    private Button? _casterButton, _swordPauseButton;
    private bool _swordPreviewPaused;
    private bool IsSwordIdle => _current.Preview==PreviewType.SwordIdle;
    private bool IsSwordHurt => _current.Id=="sword_hurt";
    private bool IsSwordShowcase => IsSwordIdle||IsSwordAction||IsSwordHurt;
    private bool IsSwordOrbitIdle => IsSwordIdle&&_current.Id=="sword_orbit_idle";

    public void PreviewSwordIdle(int appearance=0,int direction=0) => SelectSwordIdle("sword_idle",appearance,direction);
    public void PreviewSwordOrbitIdle(int appearance=0,int direction=0) => SelectSwordIdle("sword_orbit_idle",appearance,direction);
    public void PreviewSwordHurt(int appearance=0,int direction=0) => SelectSwordIdle("sword_hurt",appearance,direction);
    private void SelectSwordIdle(string id,int appearance,int direction)
    {
        _swordAppearance=Mathf.Clamp(appearance,0,SwordAppearances.Length-1);
        _previewDirection=direction==1?1:0;
        var index=System.Array.FindIndex(_effects,e=>e.Id==id);
        if(index>=0) SelectEffect(index);
    }
    public void SetSwordIdlePaused(bool paused)
    {
        _swordPreviewPaused=paused;
        if(IsSwordAction)
        {
            _swordActionPreview?.SetPaused(paused);
            if(_swordPauseButton is not null)_swordPauseButton.Text=paused?"继续演出":"暂停演出";
            return;
        }
        if(IsSwordHurt&&_battleAttacker?.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is { } hurtDoll)
        {
            hurtDoll.SetProcess(!paused);hurtDoll.set_response_time_stop(paused);
            foreach(var effect in GetChildren().OfType<BattleInkCombatVfx>())effect.SetProcess(!paused);
            if(_swordPauseButton is not null)_swordPauseButton.Text=paused?"继续演出":"暂停演出";
            return;
        }
        _battleAttacker?.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.set_sword_idle_paused(paused);
        if(_swordPauseButton is not null) _swordPauseButton.Text=paused?"继续浮动":"暂停浮动";
    }
    private void BuildSwordIdleControls(Control stage)
    {
        _swordControls=new HBoxContainer {Position=new Vector2(506,76),Visible=false};
        _swordControls.AddThemeConstantOverride("separation",12);
        _swordControls.AddChild(MakeLabel("剑图",16,new Color("615645")));
        _swordChoice=new OptionButton {CustomMinimumSize=new Vector2(138,36)};
        StyleChoiceButton(_swordChoice,false);
        _swordChoice.AddThemeFontSizeOverride("font_size",16);
        foreach(var state in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color"})
            _swordChoice.AddThemeColorOverride(state,new Color("352c20"));
        _swordChoice.AddThemeStyleboxOverride("hover",ButtonStyle(new Color(.92f,.84f,.66f,.92f),new Color("896c3f")));
        _swordChoice.AddThemeStyleboxOverride("pressed",ButtonStyle(new Color(.80f,.75f,.63f,.96f),new Color("7b6c53")));
        foreach(var appearance in SwordAppearances) _swordChoice.AddItem(appearance.Name);
        _swordChoice.ItemSelected+=selected=>{_swordAppearance=(int)selected;ApplySwordAppearance();};
        _swordControls.AddChild(_swordChoice);
        _swordPauseButton=MakeButton("暂停浮动",112,36);
        _swordPauseButton.Pressed+=()=>SetSwordIdlePaused(!_swordPreviewPaused);
        _swordControls.AddChild(_swordPauseButton);
        stage.AddChild(_swordControls);
        BuildSwordActionBackground(stage);
    }
    private void ApplySwordAppearance()
    {
        if(IsSwordAction)
        {
            var choice=SwordAppearances[_swordAppearance];var texture=GD.Load<Texture2D>(choice.Icon);
            if(texture is not null)_swordActionPreview?.SetWeapon(texture,choice.Grip,choice.Tip);
            return;
        }
        if((!IsSwordIdle&&!IsSwordHurt)||_battleAttacker is null) return;
        var appearance=SwordAppearances[_swordAppearance];
        var icon=GD.Load<Texture2D>(appearance.Icon);
        var doll=_battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        if(icon is not null&&doll is not null)
        {
            if(IsSwordOrbitIdle) doll.show_sword_orbit_idle(icon,appearance.Grip,appearance.Tip);
            else doll.show_sword_idle(icon,appearance.Grip,appearance.Tip);
            doll.face_direction(_previewDirection==1?Vector2.Left:Vector2.Right);
            if(_status is not null) _status.Text=(IsSwordOrbitIdle?"御剑待机 · ":"持剑待机 · ")+appearance.Name;
        }
        _swordChoice?.Select(_swordAppearance);
        if(_swordPauseButton is not null) _swordPauseButton.Visible=IsSwordOrbitIdle||IsSwordHurt;
        SetSwordIdlePaused(false);
        if(_swordBackdrop is not null)_swordBackdrop.Visible=IsSwordShowcase&&_swordDark;
    }
    private void StartSwordIdlePreview()
    {
        if(_stage is null||_battleAttacker is null||_battleTarget is null) return;
        var size=_stage.Size;
        _battleAttacker.Position=new Vector2(size.X*.5f+(_previewDirection==1?40:-40),size.Y*.5f+192);
        _battleTarget.Visible=false;
        _battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.SetDisplayHeight(Mathf.Min(360,size.Y*.66f));
        ApplySwordAppearance();
    }
    private void StartSwordHurtPreview()
    {
        if(_stage is null||_battleAttacker is null||_battleTarget is null)return;
        _battleTarget.Visible=false;
        _battleAttacker.Position=new Vector2(_stage.Size.X*.5f,_stage.Size.Y*.5f+170);
        var doll=_battleAttacker.GetNode<BattlePaperDoll>("BattlePaperDoll");
        doll.SetDisplayHeight(Mathf.Min(330,_stage.Size.Y*.62f));
        ApplySwordAppearance();
        doll.play_hit(_previewDirection==1?Vector2.Right:Vector2.Left);
    }
}
