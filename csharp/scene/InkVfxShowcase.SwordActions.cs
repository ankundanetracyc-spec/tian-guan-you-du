using Godot;
public partial class InkVfxShowcase
{
    private SwordActionSequencePreview? _swordActionPreview;
    private ColorRect? _swordBackdrop;
    private bool _swordDark;
    private bool IsSwordAction=>_current.Preview==PreviewType.SwordAction;
    public void PreviewSwordAction(string pose="cut",int appearance=0,int direction=0)
    {
        _swordAppearance=Mathf.Clamp(appearance,0,SwordAppearances.Length-1);_previewDirection=direction==1?1:0;
        var index=System.Array.FindIndex(_effects,e=>e.Id=="sword_action_"+pose);
        if(index>=0)SelectEffect(index);
    }
    public void SeekSwordAction(float time){_elapsed=time;_swordActionPreview?.Seek(time);}
    public void SetSwordPreviewDark(bool dark){_swordDark=dark;if(_swordBackdrop is not null)_swordBackdrop.Visible=IsSwordShowcase&&dark;}
    private void BuildSwordActionBackground(Control stage)
    {
        _swordBackdrop=new ColorRect{Name="SwordDarkBackdrop",Color=new Color("24302f"),Visible=false,MouseFilter=MouseFilterEnum.Ignore,Position=new Vector2(0,124),Size=new Vector2(stage.Size.X,Mathf.Max(0,stage.Size.Y-250))};
        stage.AddChild(_swordBackdrop);stage.MoveChild(_swordBackdrop,2);
        stage.Resized+=()=>{if(_swordBackdrop is not null)_swordBackdrop.Size=new Vector2(stage.Size.X,Mathf.Max(0,stage.Size.Y-250));};
        var background=MakeButton("切换背景",106,36);
        background.Pressed+=()=>SetSwordPreviewDark(!_swordDark);
        _swordControls?.AddChild(background);
    }
    private void ResetSwordActionPreview()
    {
        if(IsInstanceValid(_swordActionPreview)){
            _swordActionPreview!.GetParent()?.RemoveChild(_swordActionPreview);
            _swordActionPreview.QueueFree();
        }
        _swordActionPreview=null;
        if(_swordBackdrop is not null)_swordBackdrop.Visible=false;
    }
    private void StartSwordActionPreview()
    {
        if(_stage is null||_battleAttacker is null||_battleTarget is null||_battleLayer is null)return;
        var doll=_battleAttacker.GetNode<BattlePaperDoll>("BattlePaperDoll");
        doll.Configure(new BattleEntityData{entity_name=_femaleCaster?"女修":"男修",gender=_femaleCaster?1:0,faction=1});
        var appearance=SwordAppearances[_swordAppearance];var icon=GD.Load<Texture2D>(appearance.Icon);
        if(icon is null)return;
        var size=_stage.Size;var height=Mathf.Min(310,size.Y*.58f);
        _battleAttacker.Position=new Vector2(size.X*.5f-150*(_previewDirection==1?-1:1),size.Y*.5f+170);
        _battleTarget.Position=new Vector2(size.X*.5f+150*(_previewDirection==1?-1:1),size.Y*.5f+170);
        doll.SetDisplayHeight(height);doll.show_sword_idle(icon,appearance.Grip,appearance.Tip);
        doll.face_direction(_previewDirection==1?Vector2.Left:Vector2.Right);
        _battleTarget.GetNode<BattlePaperDoll>("BattlePaperDoll").face_direction(_previewDirection==1?Vector2.Right:Vector2.Left);
        _swordActionPreview=new SwordActionSequencePreview{Name="SwordActionSequencePreview"};
        _battleLayer.AddChild(_swordActionPreview);
        _swordActionPreview.Configure(_current.Id.Replace("sword_action_",""),_battleAttacker,_battleTarget,_stage,height,_previewDirection==1,icon,appearance.Grip,appearance.Tip);
        if(_swordBackdrop is not null)_swordBackdrop.Visible=_swordDark;
        _swordChoice?.Select(_swordAppearance);_swordPreviewPaused=false;
        if(_swordPauseButton is not null){_swordPauseButton.Visible=true;_swordPauseButton.Text="暂停演出";}
    }
}
