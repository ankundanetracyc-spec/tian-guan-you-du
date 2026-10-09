using Godot;

/// <summary>世界实体的共享交互、显示、六边形位置与战斗快照逻辑。</summary>
public partial class WorldEntity : Area2D
{
    public enum EntityType { CHARACTER, BEAST, BUILDING, ITEM, EVENT }
    public enum Faction { NEUTRAL, PLAYER, ENEMY }
    public enum Element { NONE, METAL, WOOD, WATER, FIRE, EARTH }
    public enum SpiritRootType { NONE, METAL, WOOD, WATER, FIRE, EARTH, METAL_WOOD, METAL_WATER, METAL_FIRE, METAL_EARTH, WOOD_WATER, WOOD_FIRE, WOOD_EARTH, WATER_FIRE, WATER_EARTH, FIRE_EARTH, METAL_WOOD_WATER, METAL_WOOD_FIRE, METAL_WOOD_EARTH, METAL_WATER_FIRE, METAL_WATER_EARTH, METAL_FIRE_EARTH, WOOD_WATER_FIRE, WOOD_WATER_EARTH, WOOD_FIRE_EARTH, WATER_FIRE_EARTH, METAL_WOOD_WATER_FIRE, METAL_WOOD_WATER_EARTH, METAL_WOOD_FIRE_EARTH, METAL_WATER_FIRE_EARTH, WOOD_WATER_FIRE_EARTH, METAL_WOOD_WATER_FIRE_EARTH }
    [Export] public string entity_name = "未命名";
    [Export] public EntityData? entity_data;
    [Export] public string unique_id = "";
    public EntityType entity_type = EntityType.ITEM;
    public int stack_count = 1, max_stack = 999, faction, family_gen = 1;
    public bool face_camera, is_family, combat_drafted, can_stack = true, initialized_from_data, is_on_hex_map, is_item_on_map;
    public string family_gender = "";
    public Vector2I hex_coord = new(-9999, -9999);
    protected CollisionShape2D? collision_shape;
    protected Label? label;
    protected Control? bg_rect;
    protected TextureRect? entity_image_rect;
    private Tween? tween;
    private float hex_click_time;
    protected static readonly Vector2 BASE_SCALE = Vector2.One, HOVER_SCALE = new(1.1f, 1.1f), MAP_CHARACTER_SCALE = new(.15f,.15f), MAP_ITEM_SCALE = new(.25f,.25f), MAP_BUILDING_SCALE = Vector2.One;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        if (string.IsNullOrEmpty(unique_id)) unique_id = GD.Randi() + "_" + GetInstanceId();
        ensure_visual_nodes(); register(); TreeExiting += unregister; MouseEntered += on_mouse_entered; MouseExited += on_mouse_exited; InputEvent += on_input_event; InputPickable = true; Scale = BASE_SCALE; update_ui(); CallDeferred(nameof(update_collision));
    }
    private void register() { GetNodeOrNull<EntityRegistry>("/root/EntityRegistry")?.register(this); }
    private void unregister() { GetNodeOrNull<EntityRegistry>("/root/EntityRegistry")?.unregister(this); }
    private void update_collision() { if (collision_shape?.Shape is RectangleShape2D shape && bg_rect is not null && bg_rect.Size.LengthSquared() > 1) shape.Size = bg_rect.Size; }
    private void on_mouse_entered() => animate_scale(is_on_hex_map ? map_base_scale() * 1.15f : HOVER_SCALE);
    private void on_mouse_exited() => animate_scale(is_on_hex_map ? map_base_scale() : BASE_SCALE);
    private void on_input_event(Node viewport, InputEvent input, long _) { if (input is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left) return; if (is_on_hex_map) { var now=Time.GetTicksMsec()/1000f; if(now-hex_click_time<.4f) on_double_click(); hex_click_time=now; return; } on_double_click(); }
    public virtual void update_ui() { if (label is not null) label.Text = entity_name; QueueRedraw(); }
    public Texture2D? get_entity_icon() => entity_image_rect?.Texture;
    public virtual string get_detail_text() => entity_name + "\n";
    protected virtual void on_double_click() { if (!is_on_hex_map && entity_type == EntityType.CHARACTER) foreach (var panel in GetTree().GetNodesInGroup("human_info_panel")) if (panel.HasMethod("show_entity")) { panel.Call("show_entity",this); break; } }
    protected void animate_scale(Vector2 target, float duration=.15f) { tween?.Kill(); tween=CreateTween(); tween.TweenProperty(this,"scale",target,duration).SetEase(Tween.EaseType.Out); }
    public void pop_in_animation() { Scale=new(.3f,.3f); animate_scale(Vector2.One,.25f); }
    public virtual void load_from_data(EntityData data) { entity_data=data.Duplicate() as EntityData; entity_name=data.entity_name; can_stack=data.can_stack; max_stack=data.max_stack; stack_count=data.stack_count; face_camera=data.face_camera; if (!string.IsNullOrEmpty(data.icon_path) && entity_image_rect is not null) entity_image_rect.Texture=ResourceLoader.Load<Texture2D>(data.icon_path); update_ui(); }
    public void snap_to_hex_cell(Vector2I coord, Vector2 world_center) { hex_coord=coord; is_on_hex_map=true; GlobalPosition=world_center; InputPickable=true; ZIndex=15; update_map_display(); }
    private Vector2 map_base_scale() => entity_type is EntityType.CHARACTER or EntityType.BEAST ? MAP_CHARACTER_SCALE : entity_type==EntityType.BUILDING ? MAP_BUILDING_SCALE : MAP_ITEM_SCALE;
    private void update_map_display() { animate_scale(is_on_hex_map ? map_base_scale() : BASE_SCALE); if (bg_rect is not null) bg_rect.Visible=!is_on_hex_map || entity_type is not EntityType.CHARACTER and not EntityType.BEAST; }
    public Color get_faction_color() => faction==(int)Faction.PLAYER ? new(.2f,.85f,.3f) : faction==(int)Faction.ENEMY ? new(.9f,.2f,.2f) : new(.9f,.9f,.9f);
    public string get_faction_display_name() => !string.IsNullOrWhiteSpace(entity_data?.faction_name) ? entity_data.faction_name.Trim() : faction==(int)Faction.PLAYER ? "我方" : faction==(int)Faction.ENEMY ? "敌对" : "散修";
    public bool is_info_known_to_player() => faction==(int)Faction.PLAYER || entity_data?.known_to_player == true;
    public void set_info_known_to_player(bool known) { if(entity_data is not null) entity_data.known_to_player=known; }
    public void play_death_animation()
    {
        GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_death();
        InputPickable = false;
        tween?.Kill();
        tween = CreateTween();
        tween.SetParallel();

        // Give the human death_kneel clip time to reach and hold its final
        // pose.  Beasts keep the old short removal timing.
        var isSpineCharacter = entity_type == EntityType.CHARACTER;
        var fadeDelay = isSpineCharacter ? 1.08f : .1f;
        var removeDelay = isSpineCharacter ? 1.46f : .5f;
        tween.TweenProperty(this, "modulate", Colors.Red, .1f);
        tween.TweenProperty(this, "scale", Vector2.Zero, .34f).SetDelay(fadeDelay);
        tween.TweenCallback(Callable.From(QueueFree)).SetDelay(removeDelay);
    }
    public PackedScene? pack_for_battle() { var scene=new PackedScene(); return scene.Pack(this)==Error.Ok ? scene : null; }
    public virtual void apply_battle_data(BattleEntityData data) { unique_id=data.uid;entity_name=data.entity_name;entity_type=(EntityType)data.entity_type;faction=data.faction;stack_count=data.stack_count;max_stack=data.max_stack;face_camera=data.face_camera;update_ui(); }
    public virtual BattleEntityData get_snapshot_data() { var data=new BattleEntityData{uid=unique_id,entity_name=entity_name,entity_type=(int)entity_type,faction=faction,stack_count=stack_count,max_stack=max_stack,face_camera=face_camera}; if(entity_data is not null){data.battle_loot=entity_data.battle_loot.Duplicate(true);data.battle_portrait_path=entity_data.battle_portrait_path;data.appearance_seed=entity_data.appearance_seed;data.appearance_face_style=entity_data.appearance_face_style;data.appearance_hair_style=entity_data.appearance_hair_style;} return data; }
    private void ensure_visual_nodes() { collision_shape=GetNodeOrNull<CollisionShape2D>("CollisionShape2D")??new CollisionShape2D{Name="CollisionShape2D",Shape=new RectangleShape2D{Size=new(120,120)}}; if(collision_shape.GetParent() is null) AddChild(collision_shape); bg_rect=GetNodeOrNull<Control>("Bg")??new Control{Name="Bg",CustomMinimumSize=new(120,120),MouseFilter=Control.MouseFilterEnum.Ignore}; if(bg_rect.GetParent() is null) AddChild(bg_rect); entity_image_rect=bg_rect.GetNodeOrNull<TextureRect>("TextureRect")??new TextureRect{Name="TextureRect",ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore}; if(entity_image_rect.GetParent() is null) bg_rect.AddChild(entity_image_rect); label=bg_rect.GetNodeOrNull<Label>("Label")??new Label{Name="Label",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore}; if(label.GetParent() is null) bg_rect.AddChild(label); }
}
