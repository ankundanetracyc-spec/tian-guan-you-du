using Godot;

/// <summary>角色立绘层：优先使用模块化渲染器，缺失图层时回退旧贴图。</summary>
public partial class CharacterPortrait : Control
{
    private TextureRect? body_layer;
    private TextureRect? head_layer;
    private GodotObject? modular_renderer;

    public override void _Ready()
    {
        body_layer = GetNodeOrNull<TextureRect>("%BodyLayer");
        head_layer = GetNodeOrNull<TextureRect>("%HeadLayer");
        modular_renderer = GetNodeOrNull("%ModularRenderer");
    }

    public void set_body(string path) => set_layer_texture(body_layer, path);
    public void set_head(string path) => set_layer_texture(head_layer, path);

    public void load_from_character(GodotObject? character)
    {
        if (!GodotObject.IsInstanceValid(character)) return;
        if (GodotObject.IsInstanceValid(modular_renderer))
        {
            modular_renderer!.Call("show_character", character);
            var hasCompletePortrait = modular_renderer.Call("has_required_layers").AsBool();
            (modular_renderer as CanvasItem)!.Visible = hasCompletePortrait;
            if (body_layer is not null) body_layer.Visible = !hasCompletePortrait;
            if (head_layer is not null) head_layer.Visible = !hasCompletePortrait;
            if (hasCompletePortrait) return;
        }
        set_body("res://textures/角色/棋子/衣服1.png");
        set_head("res://textures/角色/棋子/头部1.png");
    }

    private static void set_layer_texture(TextureRect? layer, string path)
    {
        if (layer is null || string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path)) return;
        layer.Texture = ResourceLoader.Load<Texture2D>(path);
    }
}
