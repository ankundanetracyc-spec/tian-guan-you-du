using Godot;
using Godot.Collections;

/// <summary>战斗手牌与凝核牌面：战斗态负责交互，凝核态只复用视觉结构。</summary>
public partial class SkillCard : Control
{
    [Signal] public delegate void card_selectedEventHandler(SkillCard card); [Signal] public delegate void card_drag_playedEventHandler(SkillCard card,Vector2 drop_viewport_pos); [Signal] public delegate void detail_requestedEventHandler(SkillCard card); [Signal] public delegate void detail_dismissedEventHandler(SkillCard card);
    public ActiveSkillData? skill_data; public GodotObject? owner_unit; public string card_name=""; public int card_index=-1; public bool is_selected,can_play=true,is_move_card,is_wait_card,is_move_restricted,is_momentum_active;
    private Control? badge,highlight,staminaBadge,manaBadge,spiritBadge; private Label? cost,name,description,tags,stamina,mana,spirit; private TextureRect? icon; private bool dragging,pressing,longPress,externalDrop; private Vector2 dragStart,dragPos,handOrigin,dragVisualPosition; private const float Threshold=15;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Stop;badge=GetNodeOrNull<Control>("%CostBadge");cost=GetNodeOrNull<Label>("%CostLabel");icon=GetNodeOrNull<TextureRect>("%IconRect")??GetNodeOrNull<TextureRect>("IconArea/CardIcon");name=GetNodeOrNull<Label>("%NameLabel")??GetNodeOrNull<Label>("TitleStrip/Title");description=GetNodeOrNull<Label>("%DescLabel")??GetNodeOrNull<Label>("EffectShade/Effect");highlight=GetNodeOrNull<Control>("%SelectHighlight")??GetNodeOrNull<Control>("SelectHighlight");tags=GetNodeOrNull<Label>("%TagLabel")??GetNodeOrNull<Label>("%KeywordLabel");staminaBadge=GetNodeOrNull<Control>("%StaminaBadge")??GetNodeOrNull<Control>("CostBadges/StaminaBadge");stamina=GetNodeOrNull<Label>("%StaminaCostLabel")??GetNodeOrNull<Label>("CostBadges/StaminaBadge/Value");manaBadge=GetNodeOrNull<Control>("%ManaBadge")??GetNodeOrNull<Control>("CostBadges/ManaBadge");mana=GetNodeOrNull<Label>("%ManaCostValueLabel")??GetNodeOrNull<Label>("CostBadges/ManaBadge/Value");spiritBadge=GetNodeOrNull<Control>("%SpiritBadge");spirit=GetNodeOrNull<Label>("%SpiritCostLabel");if(highlight is not null)highlight.Hide();}
    public void setup(ActiveSkillData skill,GodotObject unit,int index){skill_data=skill;owner_unit=unit;card_index=index;is_move_card=false;is_wait_card=false;if(skill is null){Hide();return;}card_name=skill.skill_name;if(name is not null)name.Text=skill.skill_name;if(description is not null){description.Text=CardPresentation.Underline(CardPresentation.Face(skill, owner_unit));description.Show();}GetNodeOrNull<Control>("EffectShade")?.Show();if(icon is not null)icon.Texture=string.IsNullOrEmpty(skill.icon_path)?null:ResourceLoader.Load<Texture2D>(skill.icon_path);if(tags is not null){tags.Text=string.Join("  ",skill.tags);tags.Visible=skill.tags.Count>0;}update_resource_state();FitDescription();Show();}
    /// <summary>凝核牌局复用战斗牌面，但不创建战斗卡数据、费用或拖拽交互。</summary>
    public void setup_condensation(string cardTitle,string effectText,Texture2D? cardIcon,int index,string cardType="凝核")
    {
        skill_data=null; owner_unit=null; card_index=index; card_name=cardTitle; can_play=true; is_move_card=false; is_wait_card=false;
        if(name is not null) name.Text=cardTitle;
        if(description is not null){description.Text=effectText;description.Show();}
        GetNodeOrNull<Control>("EffectShade")?.Show();
        if(icon is not null) icon.Texture=cardIcon;
        if(tags is not null){tags.Text="";tags.Hide();}
        badge?.Hide(); cost?.Hide(); staminaBadge?.Hide(); manaBadge?.Hide(); spiritBadge?.Hide();
        GetNodeOrNull<Control>("Cost")?.Hide(); GetNodeOrNull<Control>("CostBadges")?.Hide();
        if(GetNodeOrNull<Label>("TypeBadge/Label") is {} type) type.Text=cardType;
        var length=effectText.Length; var height=length>100?220f:length>65?180f:145f;
        description?.AddThemeFontSizeOverride("font_size",length>100?13:15);
        if(GetNodeOrNull<Control>("EffectShade") is {} shade) shade.OffsetTop=-height;
        if(GetNodeOrNull<Control>("TypeBadge") is {} typeBadge){typeBadge.OffsetTop=360-height-11;typeBadge.OffsetBottom=360-height+14;}
        if(GetNodeOrNull<Control>("ArtDivider") is {} divider){divider.OffsetTop=360-height-1;divider.OffsetBottom=360-height+2;}
        highlight?.Hide(); Modulate=Colors.White; MouseFilter=MouseFilterEnum.Ignore; SetProcess(false); Show();
    }
    private void FitDescription()
    {
        if(skill_data is null||description is null)return;
        if(GetNodeOrNull<Label>("TypeBadge/Label") is {} type)type.Text=skill_data.get_card_category();
        // 长牌文扩大文字区，不裁切条件、目标和效果。
        var length=CardPresentation.Face(skill_data,owner_unit).Length;
        var height=length>100?220f:length>65?180f:145f;
        description.AddThemeFontSizeOverride("font_size",length>100?13:15);
        if(GetNodeOrNull<Control>("EffectShade") is {} shade)shade.OffsetTop=-height;
        if(GetNodeOrNull<Control>("TypeBadge") is {} badge){badge.OffsetTop=360-height-11;badge.OffsetBottom=360-height+14;}
        if(GetNodeOrNull<Control>("ArtDivider") is {} divider){divider.OffsetTop=360-height-1;divider.OffsetBottom=360-height+2;}
        if(icon is not null && GetNodeOrNull<Control>("IconArea") is not null)
        {
            // 图标内缩到圆框以内，不依赖 Shader，避免出现背景露角或图标消失。
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            icon.OffsetLeft=7;icon.OffsetTop=7;icon.OffsetRight=-7;icon.OffsetBottom=-7;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            icon.Material=null;
        }
    }
    public string get_detail_text()
    {
        if (skill_data is null) return "";
        return CardPresentation.Details(skill_data, owner_unit);
    }
    public void update_resource_state(){if(skill_data is not null && description is not null){var face=CardPresentation.Underline(CardPresentation.Face(skill_data,owner_unit));if(description.Text!=face)description.Text=face;}var values=costs();set_costs(values);if(!GodotObject.IsInstanceValid(owner_unit)){can_play=true;Modulate=Colors.White;return;}can_play=owner_unit!.Get("current_stamina").AsInt32()>=values["energy"].AsInt32()&&owner_unit.Get("current_mana").AsInt32()>=values["mana"].AsInt32();if(skill_data is not null && GetTree()?.GetFirstNodeInGroup("battle_mgr") is BattleManager manager)can_play=manager.CanPay(owner_unit!,skill_data);if(skill_data?.skill_type==ActiveSkillData.SkillType.CURSE||is_move_restricted)can_play=false;var affordable=can_play;Modulate=affordable?Colors.White:new Color(1,1,1,.3f);}
    public void set_move_restricted(bool restricted){is_move_restricted=restricted;update_resource_state();} public void set_momentum_active(bool active){is_momentum_active=active;if(highlight is not null)highlight.Visible=active||is_selected;} public void set_selected(bool selected){is_selected=selected;if(highlight is not null)highlight.Visible=selected;}
    public bool is_dragging()=>dragging; public void cancel_drag(){dragging=false;externalDrop=true;pressing=false;dragStart=Vector2.Zero;ZIndex=1;Modulate=Colors.White;} public void finish_drag_in_hand(){dragging=false;externalDrop=false;pressing=false;dragStart=Vector2.Zero;ZIndex=1;Modulate=Colors.White;}
    public override void _GuiInput(InputEvent input){if(input is InputEventMouseButton mouse&&mouse.ButtonIndex==MouseButton.Left){if(mouse.Pressed){dragging=false;externalDrop=false;pressing=true;longPress=false;dragStart=GetGlobalMousePosition();dragPos=Position;dragVisualPosition=GlobalPosition;GetTree().CreateTimer(.45f).Timeout+=()=>{if(IsInstanceValid(this)&&IsInsideTree()&&pressing&&!dragging){longPress=true;EmitSignal(SignalName.detail_requested,this);}};AcceptEvent();return;}pressing=false;if(dragging)EmitSignal(SignalName.card_drag_played,this,GetViewport().GetMousePosition());else if(longPress)EmitSignal(SignalName.detail_dismissed,this);else if(!externalDrop&&(can_play||is_move_card||is_wait_card))EmitSignal(SignalName.card_selected,this);dragging=false;longPress=false;dragStart=Vector2.Zero;AcceptEvent();}else if(input is InputEventMouseMotion&&Input.IsMouseButtonPressed(MouseButton.Left)&&dragStart!=Vector2.Zero&&GetGlobalMousePosition().DistanceTo(dragStart)>Threshold){pressing=false;dragging=true;dragVisualPosition=GlobalPosition;handOrigin=GetParent<Control>()?.GlobalPosition??Vector2.Zero;}}
    // 手动指定目标时才进入箭头瞄准态；无需指定目标的牌仍可自由拖离手牌栏。
    public bool is_aiming()=>dragging && skill_data is not null && !CardRules.IsResponse(skill_data) && (skill_data.target_type is 2 or 3 || skill_data.aoe_range>0 || skill_data.can_target_empty) && GetGlobalMousePosition().Y<handOrigin.Y-70;
    public bool is_target_previewing()=>dragging && skill_data is not null && !CardRules.IsResponse(skill_data) && GetGlobalMousePosition().Y<handOrigin.Y-70;
    public override void _Process(double delta)
    {
        if(!dragging)return;
        var mouse=GetGlobalMousePosition();ZIndex=1000;Rotation=0;
        if(is_aiming()){Position=dragPos;Scale=Vector2.One*.72f;Modulate=Colors.White;}
        else{Scale=Vector2.One*.94f;var target=mouse-Size*Scale*new Vector2(.5f,.42f);var blend=1f-Mathf.Exp(-(float)delta*24f);dragVisualPosition=dragVisualPosition.Lerp(target,blend);GlobalPosition=dragVisualPosition;Modulate=Colors.White;}
        if(highlight is not null)highlight.Show();
    }
    private Dictionary costs(){var result=new Dictionary{{"energy",skill_data is not null && owner_unit is not null && GetTree()?.GetFirstNodeInGroup("battle_mgr") is BattleManager bm?bm.gongfa_combat.Cost(owner_unit,skill_data):skill_data?.get_energy_cost()??0},{"mana",skill_data?.mana_cost??0}};return result;}
    private void set_costs(Dictionary values){var e=values["energy"].AsInt32();var n=values["mana"].AsInt32();if(badge is not null)badge.Visible=e>0;if(cost is not null)cost.Text=format_cost(e);if(staminaBadge is not null)staminaBadge.Visible=e>0;if(stamina is not null)stamina.Text=format_cost(e);if(manaBadge is not null)manaBadge.Visible=n>0;if(mana is not null)mana.Text=format_cost(n);if(spiritBadge is not null)spiritBadge.Hide();}
    private string settlement_text()
    {
        if (skill_data is null) return "暂无结算信息。";
        var lines = new List<string>();
        if (!Mathf.IsZeroApprox(skill_data.base_damage) || skill_data.damage_bonuses.Count > 0)
        {
            var terms = new List<string> { skill_data.base_damage.ToString("0.##") };
            foreach (var bonus in skill_data.damage_bonuses)
                if (bonus is not null && !string.IsNullOrEmpty(bonus.attribute) && !Mathf.IsZeroApprox(bonus.multiplier)) terms.Add($"{attribute_name(bonus.attribute)}×{bonus.multiplier:0.##}");
            lines.Add($"造成{(skill_data.damage_type == "magic" ? "法术" : "物理")}伤害：{string.Join(" + ", terms)} = {skill_data.calculate_damage(owner_unit):0.##}");
        }
        foreach (var effect in skill_data.special_effects)
        {
            var name = effect_name(effect.GetValueOrDefault("effect", "").AsString());
            var formula = effect.GetValueOrDefault("formula", "").AsString();
            if (!string.IsNullOrEmpty(formula)) lines.Add($"{name}：{formula_text(formula)}");
            else if (effect.ContainsKey("count")) lines.Add($"{name}：{effect["count"].AsInt32()}");
            else lines.Add(name);
        }
        foreach (var effect in skill_data.status_effects)
        {
            var status = effect.GetValueOrDefault("status_id", "状态").AsString();
            var stacks = effect.GetValueOrDefault("stacks", 1).AsInt32();
            lines.Add($"施加状态：{status} ×{stacks}");
        }
        return lines.Count == 0 ? "本技能效果按技能说明结算。" : string.Join("\n", lines);
    }

    private string formula_text(string formula)
    {
        var terms = new List<string>(); float total = 0;
        foreach (var piece in formula.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = piece.Split(':', 2); if (pair.Length != 2 || !float.TryParse(pair[1], out var multiplier)) continue;
            if (pair[0] == "base") { terms.Add(multiplier.ToString("0.##")); total += multiplier; continue; }
            terms.Add($"{attribute_name(pair[0])}×{multiplier:0.##}");
            if (GodotObject.IsInstanceValid(owner_unit)) { var value = owner_unit!.Get(pair[0]); if (value.VariantType is Variant.Type.Int or Variant.Type.Float) total += value.AsSingle() * multiplier; }
        }
        return $"{string.Join(" + ", terms)} = {total:0.##}";
    }

    private static string attribute_name(string id) => id switch { "physique" => "体魄", "agility" => "敏捷", "intelligence" => "灵慧", "soul" => "神魂", "comprehension" => "悟性", "body_proficiency" => "体道造诣", "magic_proficiency" => "术法造诣", "sword_proficiency" => "剑道造诣", _ => "相关属性" };
    private static string effect_name(string id) => id switch { "shield" => "获得护持", "restore_stamina" => "恢复体力", "restore_mana" => "恢复法力", "draw" => "抽取卡牌", "move" => "移动", _ => "特殊效果" };
    private static string format_cost(int value)=>value>=1000?$"{value/1000f:0.#}千":value.ToString();
    private string skill_type()=>skill_data is null ? "术诀" : skill_data.get_card_category();
    private string target_type()=>skill_data?.target_type switch { 0=>"自身",1=>"自身",2=>"指定敌方",3=>"指定任意目标",_=>"自身"};
}
