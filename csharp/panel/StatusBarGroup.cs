using Godot;

public partial class StatusBarGroup : Control
{
    private GodotObject? _unit; private Label? _name;
    private TextureProgressBar? _health, _healthTrail, _mana, _manaTrail, _stamina, _staminaTrail;
    private Label? _healthText, _manaText, _staminaText;
    public override void _Ready() { _name=GetNodeOrNull<Label>("NameLabel"); _health=GetNodeOrNull<TextureProgressBar>("HealthBar"); _healthTrail=GetNodeOrNull<TextureProgressBar>("HealthBar2"); _mana=GetNodeOrNull<TextureProgressBar>("ManaBar"); _manaTrail=GetNodeOrNull<TextureProgressBar>("ManaBar2"); _stamina=GetNodeOrNull<TextureProgressBar>("StaminaBar"); _staminaTrail=GetNodeOrNull<TextureProgressBar>("StaminaBar2"); _healthText=GetNodeOrNull<Label>("HealthValueLabel"); _manaText=GetNodeOrNull<Label>("ManaValueLabel"); _staminaText=GetNodeOrNull<Label>("StaminaValueLabel"); }
    public void setup(GodotObject entity) { _unit=entity; refresh(entity); }
    public void refresh(GodotObject entity)
    { if (!GodotObject.IsInstanceValid(entity)) return; _unit=entity; var dead=entity.Get("current_health").AsInt32()<=0; if(_name is not null) _name.Text=entity.Get("entity_name").AsString()+(dead?" [阵亡]":""); Update(_health,_healthTrail,_healthText,entity,"current_health","max_health"); Update(_mana,_manaTrail,_manaText,entity,"current_mana","max_mana"); Update(_stamina,_staminaTrail,_staminaText,entity,"current_stamina","max_stamina"); }
    private void Update(TextureProgressBar? bar, TextureProgressBar? trail, Label? label, GodotObject unit, string current, string maximum) { if(bar is null)return; var max=Mathf.Max(1,unit.Get(maximum).AsInt32()); var value=unit.Get(current).AsInt32(); bar.MaxValue=max; bar.Value=value; if(trail is not null){trail.MaxValue=max; var tween=CreateTween(); tween.TweenProperty(trail,"value",value,1); } if(label is not null) label.Text=$"{value}/{max}"; }
}
