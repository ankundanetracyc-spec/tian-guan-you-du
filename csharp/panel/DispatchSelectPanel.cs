using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;
using Checks = System.Collections.Generic.Dictionary<string, Godot.CheckBox>;

public partial class DispatchSelectPanel : Control
{
    [Signal] public delegate void dispatch_confirmedEventHandler(GArray members);
    [Signal] public delegate void cancelledEventHandler();
    private VBoxContainer? container; private Label? title,info; private Button? confirm,cancel; private Godot.Collections.Array<GodotObject> characters=[]; private readonly HashSet<string> selected=[]; private readonly Checks checks=[];
    public override void _Ready(){AddToGroup("map_overlay_blocker");Hide();MouseFilter=MouseFilterEnum.Stop;ZIndex=4096;title=GetNodeOrNull<Label>("PanelBg/TitleLabel");container=GetNodeOrNull<VBoxContainer>("PanelBg/CharContainer/ScrollContainer/VBox");info=GetNodeOrNull<Label>("PanelBg/InfoLabel");confirm=GetNodeOrNull<Button>("PanelBg/ConfirmBtn");cancel=GetNodeOrNull<Button>("PanelBg/CancelBtn");if(confirm is not null)confirm.Pressed+=on_confirm;if(cancel is not null)cancel.Pressed+=on_cancel;}
    public void open(GArray members,string panel_title="选择派遣队伍",string confirm_text="出发",string info_hint="请选择要派遣的角色"){characters=[];foreach(var value in members){var character=value.AsGodotObject();if(GodotObject.IsInstanceValid(character))characters.Add(character!);}selected.Clear();checks.Clear();if(title is not null)title.Text=panel_title;if(confirm is not null){confirm.Text=confirm_text;confirm.Disabled=true;}if(info is not null)info.Text=info_hint;if(container is not null){foreach(var child in container.GetChildren())child.QueueFree();foreach(var character in characters)container.AddChild(make_row(character));}update_info();Show();MoveToFront();}
    private HBoxContainer make_row(GodotObject character){var uid=character.Get("unique_id").AsString();var row=new HBoxContainer{CustomMinimumSize=new(0,52),SizeFlagsHorizontal=SizeFlags.ExpandFill};var check=new CheckBox{CustomMinimumSize=new(24,24)};check.Toggled+=enabled=>toggle(uid,enabled);checks[uid]=check;row.AddChild(check);foreach(var (text,width,color) in new[]{(character.Get("entity_name").AsString(),100,Colors.White),(character.Get("cultivation_realm").AsString(),60,new Color(.98f,.88f,.48f)),($"精力上限:{character.Get("max_stamina").AsInt32()}",105,new Color(.88f,.84f,.72f)),($"HP:{character.Get("current_health").AsInt32()}/{character.Get("max_health").AsInt32()}",80,new Color(1,.78f,.72f))})row.AddChild(new Label{Text=text,CustomMinimumSize=new(width,0),Modulate=color});return row;}
    private void toggle(string uid,bool enabled){if(enabled)selected.Add(uid);else selected.Remove(uid);update_info();}
    private void update_info(){var chosen=characters.Where(c=>selected.Contains(c.Get("unique_id").AsString())).ToArray();if(info is not null)info.Text=chosen.Length==0?"请选择角色":$"已选 {chosen.Length} 人 | 技能槽将按角色配置载入";if(confirm is not null)confirm.Disabled=chosen.Length==0;}
    private void on_confirm(){var members=new GArray();foreach(var c in characters)if(selected.Contains(c.Get("unique_id").AsString()))members.Add(c);if(members.Count>0){EmitSignal(SignalName.dispatch_confirmed,members);Hide();}}
    private void on_cancel(){EmitSignal(SignalName.cancelled);Hide();}
}
