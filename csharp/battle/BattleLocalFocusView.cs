using Godot;

/// <summary>Reframes the rendered battlefield, including scenery and FX. Never scales a unit.</summary>
public partial class BattleLocalFocusView : CanvasLayer
{
    private ColorRect _surface = null!;
    private ShaderMaterial _material = null!;
    private Rect2 _region;
    private Vector2 _point, _kick;
    public float FrameZoom { get; private set; } = 1;

    public override void _Ready()
    {
        Layer = 0;
        AddToGroup("battle_local_focus_views");
        _material = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type canvas_item;
            render_mode unshaded;
            uniform sampler2D scene_image : hint_screen_texture, repeat_disable, filter_linear;
            uniform vec2 focus_point;
            uniform vec2 view_size;
            uniform vec2 frame_min;
            uniform vec2 frame_max;
            uniform vec2 kick;
            uniform float frame_zoom = 1.0;
            uniform float shade = 0.0;
            void fragment() {
                vec2 sample_uv = focus_point + (SCREEN_UV - focus_point - kick) / frame_zoom;
                sample_uv = clamp(sample_uv, frame_min + SCREEN_PIXEL_SIZE, frame_max - SCREEN_PIXEL_SIZE);
                vec4 scene = textureLod(scene_image, sample_uv, 0.0);
                float distance_from_focus = length((SCREEN_UV - focus_point) * view_size);
                float vignette = smoothstep(180.0, 650.0, distance_from_focus) * shade;
                vec2 border = min(SCREEN_UV - frame_min, frame_max - SCREEN_UV) * view_size;
                float edge = smoothstep(0.0, 18.0, min(border.x, border.y));
                COLOR = vec4(mix(scene.rgb, vec3(.025, .045, .043), vignette), edge);
            }
            """ } };
        _surface = new ColorRect { Name = "BattlefieldCloseup", Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        AddChild(_surface);
    }

    public void present_region(Rect2 region, Vector2 point, float zoom, float shade = 0, Vector2 kick = default)
    {
        var size = GetViewport().GetVisibleRect().Size;
        FrameZoom = zoom;
        _surface.Position = region.Position;
        _surface.Size = region.Size;
        _surface.Visible = zoom > 1.001f || shade > .001f || kick.LengthSquared() > .01f;
        point = point.Clamp(region.Position + region.Size * .18f, region.End - region.Size * .18f);
        _region = region; _point = point; _kick = kick;
        _material.SetShaderParameter("focus_point", point / size);
        _material.SetShaderParameter("view_size", size);
        _material.SetShaderParameter("frame_min", region.Position / size);
        _material.SetShaderParameter("frame_max", region.End / size);
        _material.SetShaderParameter("frame_zoom", zoom);
        _material.SetShaderParameter("shade", shade);
        _material.SetShaderParameter("kick", kick / size);
    }

    public static Rect2 FrameUnitRect(Node2D unit, Rect2 rect)
    {
        // Screen-space unit bars follow the same crop as the world; menu controls remain fixed.
        var view = unit.GetTree().GetNodesInGroup("battle_local_focus_views").OfType<BattleLocalFocusView>()
            .LastOrDefault(v => !v.IsQueuedForDeletion() && v._surface.Visible);
        if (view is null || !view._region.HasPoint(rect.GetCenter())) return rect;
        return new Rect2(view._point + (rect.Position - view._point) * view.FrameZoom + view._kick, rect.Size * view.FrameZoom);
    }

    public void PresentBattle(Node2D target, Node2D? source, float zoom, float shade = 0, Vector2 kick = default)
    {
        var region = GetViewport().GetVisibleRect();
        // Keep laboratory controls outside the reframed stage.
        for (Node? parent = target.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is Control stage && stage.Name == "Stage")
            {
                region = stage.GetGlobalRect();
                region.Position += new Vector2(0, 108);
                region.Size -= new Vector2(0, 238);
                break;
            }
        var to = target.GetGlobalTransformWithCanvas() * target.ToLocal(BattleInkCombatVfx.Anchor(target));
        var from = IsInstanceValid(source) ? source!.GetGlobalTransformWithCanvas() * source.ToLocal(BattleInkCombatVfx.Anchor(source)) : to;
        present_region(region, from.Lerp(to, .65f), zoom, shade, kick);
    }
}
