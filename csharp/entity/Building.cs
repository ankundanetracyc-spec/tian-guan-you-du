using Godot;

public partial class Building : WorldEntity
{
    private BuildingData? data => entity_data as BuildingData;
    public bool has_durability { get => data?.has_durability ?? true; set { if(data is not null)data.has_durability=value; } }
    public int max_durability { get => data?.max_durability ?? 5; set { if(data is not null)data.max_durability=value; } }
    public int current_durability { get => data?.current_durability ?? 5; set { if(data is not null)data.current_durability=value; } }
    public bool consume_durability { get => data?.consume_durability ?? true; set { if(data is not null)data.consume_durability=value; } }
    public override void _Ready() { if(Engine.IsEditorHint())return; entity_data ??= new BuildingData(); base._Ready(); load_from_data(entity_data); entity_type=EntityType.BUILDING; update_ui(); }
    public override void load_from_data(EntityData source) { base.load_from_data(source); if(source is BuildingData b && entity_data is BuildingData target){target.max_durability=b.max_durability;target.current_durability=b.current_durability;target.consume_durability=b.consume_durability;} entity_type=EntityType.BUILDING; }
    public override void update_ui() { if(label is not null) label.Text=has_durability&&max_durability>0?$"{entity_name} ({Mathf.Clamp((float)current_durability/max_durability*100,0,100):F0}%)":entity_name; }
}
