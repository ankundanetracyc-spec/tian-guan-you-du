using Godot;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>
/// Source-identified front/back hair layers shared by idle and every combat pose.
/// No baked hairstyle substitutions: a missing layer keeps this renderer unavailable.
/// Authored layer canvas is 1024x1536; this node's origin is the neck at (500,245).
/// </summary>
public partial class QueueAvatarHair : Node2D
{
    public const string AssetRoot = "res://textures/characters/queue_avatar_design_20261103/hair_v1/";
    public const string HeadPath = AssetRoot + "shared_head.png";
    public static readonly Vector2 NeckOrigin = new(500, 245);
    private const string Package = "ink_three_variants_20261004";

    public sealed record Selection(string FrontId, string BackId, string FrontPath, string BackPath,
        string FrontSourcePath, string BackSourcePath);

    public bool IsReady { get; private set; }
    public string FrontId { get; private set; } = "";
    public string BackId { get; private set; } = "";

    public static bool TryResolve(Node owner, BattleEntityData data, out Selection? selection)
    {
        selection = null;
        if (data.gender != 0 || data.portrait_data is not HumanData { gender: 0 } source) return false;
        var catalog = owner.GetNodeOrNull<PortraitCatalog>("/root/PortraitCatalog");
        if (catalog is null) return false;
        var requested = catalog.get_requested_appearance(source);
        // Match BattlePortraitView's independent deterministic rear-hair resolution.
        if (source.appearance_hair_back_style < 0) requested["hair_back"] = "";
        var context = catalog.get_context(source, "male", data.current_age, source.faction_name, data.cultivation_realm);
        return TryResolveSelection(catalog.select_portrait(context, requested), out selection);
    }

    public static bool TryResolveSelection(GDictionary selected, out Selection? selection)
    {
        selection = null;
        if (!selected.ContainsKey("hair_front") || !selected.ContainsKey("hair_back")) return false;
        var front = selected["hair_front"].AsGodotDictionary();
        var back = selected["hair_back"].AsGodotDictionary();
        if (front.GetValueOrDefault("package_id", "").AsString() != Package
            || back.GetValueOrDefault("package_id", "").AsString() != Package) return false;
        var frontId = front.GetValueOrDefault("id", "").AsString();
        var backId = back.GetValueOrDefault("id", "").AsString();
        var frontFile = frontId switch
        {
            "male_front_hair_05" => "front_shouzhen.png",
            "male_front_hair_04" => "front_shufeng.png",
            _ => ""
        };
        var backFile = backId switch
        {
            "male_back_hair_06" => "back_shouzhen.png",
            "male_back_hair_05" => "back_shufeng.png",
            _ => ""
        };
        if (frontFile.Length == 0 || backFile.Length == 0) return false;
        var frontSource = $"res://mods/appearance/{Package}/male/hair_front/{(frontId.EndsWith("05") ? "07" : "06")}.png";
        var backSource = $"res://mods/appearance/{Package}/male/hair_back/{(backId.EndsWith("06") ? "07" : "06")}.png";
        // Source files must still be active and present. Retired source08 is never mapped.
        if (!Godot.FileAccess.FileExists(frontSource) || !Godot.FileAccess.FileExists(backSource)
            || !ResourceLoader.Exists(HeadPath) || !ResourceLoader.Exists(AssetRoot + frontFile)
            || !ResourceLoader.Exists(AssetRoot + backFile)) return false;
        selection = new Selection(frontId, backId, AssetRoot + frontFile, AssetRoot + backFile, frontSource, backSource);
        return true;
    }

    public bool Configure(Selection selection)
    {
        var head = GD.Load<Texture2D>(HeadPath);
        var front = GD.Load<Texture2D>(selection.FrontPath);
        var back = GD.Load<Texture2D>(selection.BackPath);
        if (head is null || front is null || back is null) return false;
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        AddLayer("HairBack", back, -2);
        AddLayer("FeaturelessHead", head, -1);
        AddLayer("HairFront", front, 2);
        Name = "SharedHeadHair";
        FrontId = selection.FrontId;
        BackId = selection.BackId;
        IsReady = true;
        return true;
    }

    private void AddLayer(string name, Texture2D texture, int zIndex)
    {
        AddChild(new Sprite2D
        {
            Name = name, Texture = texture, Centered = false, Position = -NeckOrigin,
            ZIndex = zIndex, TextureFilter = TextureFilterEnum.LinearWithMipmaps
        });
    }
}
