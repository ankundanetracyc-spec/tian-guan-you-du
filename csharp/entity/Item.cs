using Godot;
using Godot.Collections;

public partial class Item : WorldEntity
{
    public StringName definition_id = new("");
    public string item_category = "材料", item_subcategory = "";
    public Array<string> tags = [];
    public int grade_value, usage_type, source_pack_index = -1;
    public override void _Ready() { if (Engine.IsEditorHint()) return; if (entity_data is not null) load_from_data(entity_data); entity_type=EntityType.ITEM; base._Ready(); update_ui(); }
    public override void load_from_data(EntityData data) { base.load_from_data(data); if(data is ItemData item){definition_id=item.definition_id;item_category=item.item_category;item_subcategory=item.item_subcategory;tags=item.tags.Duplicate();grade_value=item.grade_value;usage_type=item.usage_type;if(!string.IsNullOrEmpty(data.ResourcePath)) SetMeta("data_path",data.ResourcePath);} entity_type=EntityType.ITEM; }
    public override void update_ui() { if(label is not null) label.Text=stack_count>1 ? $"{entity_name} x{stack_count}" : entity_name; }
    public int get_grade_value() => grade_value;
    public string get_grade_string() => ItemAffixCatalog.grade_to_string(grade_value);
    public bool has_tag(string dimension,string value) => tags.Contains($"{dimension}[{value}]");
    public Array<string> get_tag_values(string dimension) => ItemAffixCatalog.get_tag_values(tags,dimension);
    public bool matches_recipe_requirements(Array<string> required_tags) => required_tags.All(tags.Contains);
}
