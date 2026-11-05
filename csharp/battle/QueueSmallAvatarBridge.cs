using Godot;

/// <summary>Presentation-only adapter for the approved original small-character rig.</summary>
public partial class QueueSmallAvatarBridge : Node2D
{
    public const float AttackDuration = .58f;
    public const float RobedTravelSpeed = 38f;
    private Node2D? _runtime;
    public bool Configure(float visibleHeight, string artDirectory = "res://tools/action_battle_test/art/yuxu_fullbody_v15", string outfitId = "male_clothes_05")
    {
        if (_runtime is null)
        {
            var script = GD.Load<GDScript>("res://tools/queue_small_avatar/queue_small_avatar.gd");
            if (script is null) return false;
            _runtime = script.New().AsGodotObject() as Node2D;
            if (_runtime is null) return false;
            AddChild(_runtime);
        }
        return _runtime.Call("configure", visibleHeight, artDirectory, outfitId).AsBool();
    }
    public Rect2 LocalVisibleBounds => _runtime?.Call("get_local_visible_bounds").AsRect2() ?? new Rect2();
    public Vector2 ChestAnchor => Anchor("chest");
    public Vector2 HeadAnchor => Anchor("head");
    public Vector2 HandAnchor => Anchor("hand");
    public Vector2 PalmStrikeContactAnchor => Anchor("contact");
    public Vector2 ShoulderAnchor(bool near) => Anchor(near ? "shoulder_near" : "shoulder_far");
    private Vector2 Anchor(string key) => _runtime?.Call("get_anchor", key).AsVector2() ?? Vector2.Zero;
    public void SetFacing(bool left) => _runtime?.Call("set_facing", left);
    public void SetPaused(bool paused) => _runtime?.Call("set_paused", paused);
    public void SetMotion(Vector2 localVelocity) => _runtime?.Call("set_motion", localVelocity);
    public void SetTravelMode(string mode) => _runtime?.Call("set_travel_mode", mode);
    public void SetTravelProgress(float progress) => _runtime?.Call("set_travel_progress", progress);
    public void UpdateVisual(float delta, string action, float elapsed, float duration) => _runtime?.Call("update_visual", delta, action, elapsed, duration);
    public Godot.Collections.Dictionary DebugState => _runtime?.Call("get_debug_state").AsGodotDictionary() ?? new();
}
