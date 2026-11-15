using Godot;

/// <summary>Only the start-menu portrait test uses this nine-part ink portrait set.</summary>
public static class InkPortraitTestComposer
{
    // Restored previous portrait set; the recovered-reference iteration is suspended.
    private const string Root = "res://mods/appearance/ink_three_variants_20261004/";
    private const string SelectedRigRoot = "res://mods/appearance/new_ink_hair_20261003_v1/rig/";
    private const int Width = 1024;
    private const int Height = 1536;
    private static readonly Rect2I Canvas = new(0, 0, Width, Height);

    // Public order follows the nine slots specified for the test controls.
    public static readonly string[] PartKeys =
        ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"];
    public static readonly string[] PartNames =
        ["前发", "后发", "眉毛", "眼睛", "鼻子", "嘴巴", "脸型", "衣服", "面饰"];

    private static Godot.Collections.Dictionary? _config;
    private static Godot.Collections.Dictionary Component(bool female, int part)
    {
        _config ??= Json.ParseString(Godot.FileAccess.GetFileAsString(Root + "appearance_parts.json")).AsGodotDictionary();
        string sex = female ? "female" : "male";
        foreach (Variant raw in _config["components"].AsGodotArray())
        {
            var entry = raw.AsGodotDictionary();
            if (entry["gender"].AsString() == sex && entry["file"].AsString().Contains("/" + PartKeys[part] + "/")) return entry;
        }
        throw new System.ArgumentOutOfRangeException(nameof(part));
    }

    public static int VariantCount(bool female, int part) => Component(female, part)["variant_count"].AsInt32();
    public static string VariantName(bool female, int part, int variant) => Component(female, part)["variant_names"].AsGodotArray()[variant].AsString();
    public static int FileIndex(bool female, int part, int variant)
    {
        var entry = Component(female, part);
        return entry.ContainsKey("variant_indices") ? entry["variant_indices"].AsGodotArray()[variant].AsInt32() : variant + 1;
    }

    // Source-over order is fixed by portrait_manifest.json, from back to front.
    private static readonly int[] BlendOrder = [1, 6, 7, 4, 3, 2, 5, 8, 0];
    private static readonly System.Collections.Generic.Dictionary<string, Image> Images = [];
    private static readonly System.Collections.Generic.Queue<string> ImageOrder = new();
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Composites = [];
    private static readonly System.Collections.Generic.Queue<string> CompositeOrder = new();

    public static Texture2D Compose(bool female, bool[] visible, bool showAnchors = false, int[]? variants = null)
    {
        if (visible.Length != PartKeys.Length)
            throw new System.ArgumentException("九部位显示状态必须恰好包含九项。", nameof(visible));

        variants ??= new int[PartKeys.Length];
        if (variants.Length != PartKeys.Length)
            throw new System.ArgumentException("九部位选款必须恰好包含九项。", nameof(variants));
        for (int part = 0; part < PartKeys.Length; part++)
            if (variants[part] < 0 || variants[part] >= VariantCount(female, part))
                throw new System.ArgumentOutOfRangeException(nameof(variants), "该部位款式不存在。");
        string sex = female ? "female" : "male";
        string key = sex + ":" + string.Join(",", variants) + ":" + string.Join("", System.Array.ConvertAll(visible, value => value ? "1" : "0")) + (showAnchors ? ":anchors" : "");
        if (Composites.TryGetValue(key, out var cached)) return cached;

        using var result = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        foreach (int part in BlendOrder)
        {
            // Rear fabric belongs to this same clothing choice/visibility; draw it behind skin.
            if (part == 6 && visible[7])
            {
                string rearFile = Component(female, 7).GetValueOrDefault("rear_file", "").AsString();
                if (rearFile.Length > 0)
                    result.BlendRect(LoadImageAt(Root + rearFile.Replace("{index}", FileIndex(female, 7, variants[7]).ToString("00"))), Canvas, Vector2I.Zero);
            }
            if (!visible[part]) continue;
            result.BlendRect(LoadImage(sex, PartKeys[part], variants[part]), Canvas, Vector2I.Zero);
        }
        if (showAnchors)
        {
            string overlayPath = SelectedRigRoot + sex + "_overlay.png";
            Variant overlays = _config!.GetValueOrDefault("anchor_overlays", new Godot.Collections.Dictionary());
            if (overlays.VariantType == Variant.Type.Dictionary)
            {
                string configured = overlays.AsGodotDictionary().GetValueOrDefault(sex, "").AsString();
                if (configured.Length > 0) overlayPath = configured;
            }
            result.BlendRect(LoadImageAt(overlayPath), Canvas, Vector2I.Zero);
        }

        var texture = ImageTexture.CreateFromImage(result);
        Composites[key] = texture;
        CompositeOrder.Enqueue(key);
        while (CompositeOrder.Count > 8)
            if (Composites.Remove(CompositeOrder.Dequeue(), out var removed)) removed.Dispose();
        return texture;
    }

    private static Image LoadImage(string sex, string part, int variant)
    {
        int partIndex = System.Array.IndexOf(PartKeys, part);
        string path = Root + sex + "/" + part + "/" + FileIndex(sex == "female", partIndex, variant).ToString("00") + ".png";
        return LoadImageAt(path);
    }

    private static Image LoadImageAt(string path)
    {
        if (Images.TryGetValue(path, out var cached)) return cached;

        byte[] bytes = Godot.FileAccess.GetFileAsBytes(path);
        var image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        if (bytes.Length == 0 || image.LoadPngFromBuffer(bytes) != Error.Ok ||
            image.GetWidth() != Width || image.GetHeight() != Height)
        {
            GD.PushError("InkPortraitTestComposer: 九部位素材缺失或画布尺寸不符：" + path);
            image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        }
        Images[path] = image;
        ImageOrder.Enqueue(path);
        while (ImageOrder.Count > 18)
            if (Images.Remove(ImageOrder.Dequeue(), out var removed)) removed.Dispose();
        return image;
    }

    public static void ReleaseResources()
    {
        foreach (var texture in Composites.Values) texture.Dispose();
        foreach (var image in Images.Values) image.Dispose();
        Composites.Clear();
        Images.Clear();
        ImageOrder.Clear();
        CompositeOrder.Clear();
        _config = null;
    }
}
