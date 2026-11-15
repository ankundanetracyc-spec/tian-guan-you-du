using Godot;
using Godot.Collections;

public partial class PackSlot : Control
{
    [Signal] public delegate void item_retrievedEventHandler(string item_path);
    [Signal] public delegate void slot_swapEventHandler(PackSlot from_slot, PackSlot to_slot);
    private TextureRect? icon_rect; private Label? name_label; private Button? action_btn;
    public string stored_path = ""; public int slot_index = -1; public string slot_group = "";
    public override void _Ready() { icon_rect=GetNodeOrNull<TextureRect>("VBoxContainer/SlotIcon"); name_label=GetNodeOrNull<Label>("VBoxContainer/SlotName"); action_btn=GetNodeOrNull<Button>("VBoxContainer/UnequipBtn"); if(action_btn is not null){action_btn.Pressed+=on_action;action_btn.Hide();} MouseFilter=MouseFilterEnum.Stop; }
    public void set_empty(){stored_path="";if(icon_rect is not null){icon_rect.Texture=null;HoverInfoManager.SetTooltip(icon_rect,"");}if(name_label is not null){name_label.Text="（空）";name_label.AddThemeColorOverride("font_color",new Color(.35f,.3f,.25f));}action_btn?.Hide();}
    public void set_item(string item_path,string item_name,Texture2D? icon=null){stored_path=item_path;var item=ResourceLoader.Load<ItemData>(item_path);if(icon_rect is not null){icon_rect.Texture=icon??(item is not null&&!string.IsNullOrWhiteSpace(item.icon_path)&&ResourceLoader.Exists(item.icon_path)?ResourceLoader.Load<Texture2D>(item.icon_path):null);if(item is not null){HoverInfoManager.SetInfo(icon_rect,StoragePanel.ItemHover(item,path:item_path) with { Icon=icon_rect.Texture });}else HoverInfoManager.SetInfo(icon_rect,new HoverInfoManager.HoverInfoContent(item_name,"行囊物品",icon_rect.Texture));}if(name_label is not null){name_label.Text=item_name;name_label.AddThemeColorOverride("font_color",new Color(.2f,.18f,.15f));}if(action_btn is not null){action_btn.Text="取出";action_btn.Show();}}
    private void on_action(){if(!string.IsNullOrEmpty(stored_path))EmitSignal(SignalName.item_retrieved,stored_path);}
    public override Variant _GetDragData(Vector2 position){if(string.IsNullOrEmpty(stored_path))return default;if(icon_rect?.Texture is not null)SetDragPreview(make_drag_preview());return new Dictionary{{"from_slot",this},{"slot_group",slot_group},{"slot_index",slot_index},{"item_path",stored_path}};}
    private Control make_drag_preview(){var preview=new TextureRect{Texture=icon_rect?.Texture,ExpandMode=TextureRect.ExpandModeEnum.FitWidthProportional,CustomMinimumSize=new Vector2(64,64),Modulate=new Color(1,1,1,.7f)};return preview;}
    public override bool _CanDropData(Vector2 position,Variant data){if(data.VariantType!=Variant.Type.Dictionary)return false;var values=data.AsGodotDictionary();var source=values.TryGetValue("from_slot",out var value)?value.AsGodotObject() as PackSlot:null;return source is not null&&source!=this&&GodotObject.IsInstanceValid(source);}
    public override void _DropData(Vector2 position,Variant data){if(data.VariantType!=Variant.Type.Dictionary)return;var values=data.AsGodotDictionary();var source=values.TryGetValue("from_slot",out var value)?value.AsGodotObject() as PackSlot:null;if(source is not null&&source!=this&&GodotObject.IsInstanceValid(source))EmitSignal(SignalName.slot_swap,source,this);}
}
