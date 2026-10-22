using Godot;

public partial class InkVfxShowcase
{
    private static readonly (string Name,string Icon,Vector2 Grip,Vector2 Tip)[] SwordAppearances=
    [
        ("细剑","res://textures/ui/forging_20261021/slender_sword_ink_v2.png",new(.900f,.517f),new(.018f,.517f)),
        ("阔剑","res://textures/ui/forging_20261021/broad_sword_ink_v2.png",new(.865f,.510f),new(.010f,.510f)),
        ("脊剑","res://textures/ui/forging_20261021/spined_sword_ink_v2.png",new(.865f,.505f),new(.015f,.505f))
    ];
    private int _swordAppearance;
    private OptionButton? _swordChoice;
    private HBoxContainer? _swordControls;
    private Button? _casterButton;
    private bool IsSwordIdle => _current.Preview==PreviewType.SwordIdle;

    public void PreviewSwordIdle(int appearance=0,int direction=0)
    {
        _swordAppearance=Mathf.Clamp(appearance,0,SwordAppearances.Length-1);
        _previewDirection=direction==1?1:0;
        var index=System.Array.FindIndex(_effects,e=>e.Id=="sword_idle");
        if(index>=0) SelectEffect(index);
    }
    private void BuildSwordIdleControls(Control stage)
    {
        _swordControls=new HBoxContainer {Position=new Vector2(506,76),Visible=false};
        _swordControls.AddThemeConstantOverride("separation",12);
        _swordControls.AddChild(MakeLabel("手持剑",16,new Color("615645")));
        _swordChoice=new OptionButton {CustomMinimumSize=new Vector2(150,36)};
        StyleChoiceButton(_swordChoice,false);
        _swordChoice.AddThemeFontSizeOverride("font_size",16);
        foreach(var state in new[]{"font_color","font_hover_color","font_pressed_color","font_focus_color"})
            _swordChoice.AddThemeColorOverride(state,new Color("352c20"));
        _swordChoice.AddThemeStyleboxOverride("hover",ButtonStyle(new Color(.92f,.84f,.66f,.92f),new Color("896c3f")));
        _swordChoice.AddThemeStyleboxOverride("pressed",ButtonStyle(new Color(.80f,.75f,.63f,.96f),new Color("7b6c53")));
        foreach(var appearance in SwordAppearances) _swordChoice.AddItem(appearance.Name);
        _swordChoice.ItemSelected+=selected=>
        {
            _swordAppearance=(int)selected;
            // Swap only the sword sprite. Keep the figure, facing and standing point.
            ApplySwordAppearance();
        };
        _swordControls.AddChild(_swordChoice);
        stage.AddChild(_swordControls);
    }
    private void ApplySwordAppearance()
    {
        if(!IsSwordIdle||_battleAttacker is null) return;
        var appearance=SwordAppearances[_swordAppearance];
        var icon=GD.Load<Texture2D>(appearance.Icon);
        var doll=_battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        if(icon is not null&&doll?.show_sword_idle(icon,appearance.Grip,appearance.Tip)==true)
        {
            doll.face_direction(_previewDirection==1?Vector2.Left:Vector2.Right);
            if(_status is not null) _status.Text="持剑待机 · "+appearance.Name;
        }
        _swordChoice?.Select(_swordAppearance);
    }
    private void StartSwordIdlePreview()
    {
        if(_stage is null||_battleAttacker is null||_battleTarget is null) return;
        if(_femaleCaster)
        {
            _femaleCaster=false;
            _battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.Configure(new BattleEntityData {entity_name="男修",gender=0,faction=1});
            _battleTarget.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.Configure(new BattleEntityData {entity_name="目标",gender=1,faction=(int)WorldEntity.Faction.ENEMY});
        }
        var size=_stage.Size;
        _battleAttacker.Position=new Vector2(size.X*.5f+(_previewDirection==1?40:-40),size.Y*.5f+192);
        _battleTarget.Visible=false;
        _battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.SetDisplayHeight(Mathf.Min(320,size.Y*.60f));
        ApplySwordAppearance();
    }
}
