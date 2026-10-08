using Godot;

public partial class AppearancePreview : Control
{
    private Node2D? _portraitToken, _battleToken; private PortraitRenderer? _renderer; private bool _battleForm, _hasPortrait;
    private static readonly PackedScene TokenScene = GD.Load<PackedScene>("res://tscn/entity/character/CharacterToken.tscn");
    public override void _Ready() { MouseFilter=MouseFilterEnum.Stop; TooltipText="点击切换局外立绘 / 局内形象"; GuiInput+=OnGuiInput; Resized+=LayoutPreview; EnsureChildren(); }
    public void show_character(GodotObject character)
    {
        if(!GodotObject.IsInstanceValid(character) || !GodotObject.IsInstanceValid(character.Get("entity_data").AsGodotObject())) { Hide(); return; }
        Show(); EnsureChildren(); var data=character.Get("entity_data").AsGodotObject()!; _renderer!.Call("show_character",character); _hasPortrait=_renderer.Call("has_required_layers").AsBool();
        var seed=data.Get("appearance_seed").AsInt32(); if(seed==0)seed=(int)character.GetInstanceId();
        foreach(var token in new[]{_portraitToken,_battleToken}) token?.Call("configure_appearance",seed,data.Get("appearance_face_style"),data.Get("appearance_hair_style"),data.Get("appearance_clothes_style"),data.Get("appearance_accessory_style"),character.Get("gender"),data.Get("appearance_eyebrow_style"),data.Get("appearance_eye_style"),data.Get("appearance_nose_style"),data.Get("appearance_mouth_style"),data.Get("appearance_hair_color"));
        _battleToken?.Call("set_faction_marker",character.Get("faction")); ApplyMode(); QueueRedraw();
    }
    public void hide_preview()=>Hide();
    private void EnsureChildren() { if(_portraitToken is null && TokenScene.Instantiate() is Node2D token){_portraitToken=token;AddChild(token);} if(_battleToken is null && TokenScene.Instantiate() is Node2D token2){_battleToken=token2;AddChild(token2);} if(_renderer is null){_renderer=new PortraitRenderer();AddChild(_renderer);} LayoutPreview(); }
    private void LayoutPreview() { var width=Mathf.Max(Size.X,680);var height=Mathf.Max(Size.Y,620);var center=new Vector2(width*.5f,height*.61f); if(_portraitToken is not null){_portraitToken.Position=center;_portraitToken.Scale=new Vector2(4.15f,4.15f);} if(_battleToken is not null){_battleToken.Position=center+new Vector2(0,26);_battleToken.Scale=new Vector2(2.85f,2.85f);} if(_renderer is not null){var card=Mathf.Min(width-32,(height-58)*.75f);_renderer.Position=new Vector2((width-card)*.5f+10,40);_renderer.Size=new Vector2(card-20,card/.75f-12);} ApplyMode(); }
    private void ApplyMode(){if(_portraitToken is not null)_portraitToken.Visible=!_battleForm&&!_hasPortrait;if(_renderer is not null)_renderer.Visible=!_battleForm&&_hasPortrait;if(_battleToken is not null)_battleToken.Visible=_battleForm;}
    private void OnGuiInput(InputEvent input){if(input is InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left}){_battleForm=!_battleForm;ApplyMode();QueueRedraw();AcceptEvent();}}
}
