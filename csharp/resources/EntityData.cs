using Godot;

/// <summary>所有世界实体共用的可序列化基础数据。</summary>
public partial class EntityData : Resource
{
    [Export] public string entity_name = "未命名";
    [Export] public int entity_type = 3;
    [Export] public int faction;
    [Export] public string icon_path = "";
    /// <summary>战斗掉落配置：item_path、chance、min_count、max_count；仅引用已有物品。</summary>
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> battle_loot = [];
    [Export(PropertyHint.File, "*.png,*.webp,*.jpg")] public string battle_portrait_path = "";
    [Export] public int appearance_seed;
    [Export] public int appearance_face_style = -1;
    [Export] public int appearance_eyebrow_style = -1;
    [Export] public int appearance_eye_style = -1;
    [Export] public int appearance_brow_style = -1;
    [Export] public int appearance_nose_style = -1;
    [Export] public int appearance_mouth_style = -1;
    [Export] public int appearance_makeup_style = -1;
    [Export] public int appearance_skin_tone = -1;
    [Export] public int appearance_hair_color = -1;
    [Export] public int appearance_hair_style = -1;
    [Export] public int appearance_hair_back_style = -1;
    [Export] public int appearance_ear_style = 0;
    [Export] public int appearance_clothes_style = -1;
    [Export] public int appearance_accessory_style = -1;
    [Export] public int appearance_beard_style = -1;
    [Export] public string unique_id = "";
    [Export] public bool face_camera;
    [Export] public string faction_name = "";
    [Export] public bool known_to_player;
    [Export] public int stack_count = 1;
    [Export] public bool can_stack = true;
    [Export] public int max_stack = 999;
}
