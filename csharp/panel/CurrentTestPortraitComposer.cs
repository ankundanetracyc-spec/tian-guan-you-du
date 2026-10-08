using Godot;

/// <summary>已退出游戏对话流程的旧素材合成器；新界面统一使用 PortraitRenderer。</summary>
public static class CurrentTestPortraitComposer
{
    private const string MaleRoot = "res://mods/appearance/腰上男修拆件演示_v2/test_assets_green_reference_v1/";
    private const string FemaleRoot = "res://mods/appearance/腰上女修拆件演示_v1/test_assets_green_reference_v1/";
    private const int CanvasWidth = 1024;
    private const int CanvasHeight = 1536;
    private const int SourceImageCacheLimit = 12;
    private const int CompositeCacheLimit = 8;
    private static readonly Rect2I FullCanvas = new(0, 0, CanvasWidth, CanvasHeight);
    private static readonly string[] MaleParts = ["face", "brow", "eye", "nose", "mouth", "beard", "hair_back", "hair_front", "clothes", "ear"];
    private static readonly string[] FemaleParts = ["face", "brow", "eye", "nose", "mouth", "makeup", "hair_back", "hair_front", "clothes", "ear"];
    private static readonly string[] BlendOrder = ["hair_back", "face", "ear", "clothes", "eye", "brow", "nose", "mouth", "facial_extra", "hair_front"];
    private static readonly System.Collections.Generic.Dictionary<string, Image> ImageCache = [];
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> TextureCache = [];
    private static readonly System.Collections.Generic.Queue<string> TextureCacheOrder = new();
    private static readonly System.Collections.Generic.Queue<string> ImageCacheOrder = new();

    public static Texture2D Compose(bool female, int[] indices, bool showFrontHair = true, bool showBackHair = true)
    {
        string[] parts = female ? FemaleParts : MaleParts;
        int[] safeIndices = new int[parts.Length];
        for (int i = 0; i < safeIndices.Length; i++)
            safeIndices[i] = 0; // One review set per gender until visual approval.

        string cacheKey = (female ? "female" : "male") + ":" + string.Join(",", safeIndices)
            + ":" + showFrontHair + ":" + showBackHair;
        if (TextureCache.TryGetValue(cacheKey, out var cached)) return cached;

        var partIndices = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < parts.Length; i++) partIndices[parts[i]] = safeIndices[i];
        partIndices["facial_extra"] = partIndices[female ? "makeup" : "beard"];

        var result = Image.CreateEmpty(CanvasWidth, CanvasHeight, false, Image.Format.Rgba8);
        string root = female ? FemaleRoot : MaleRoot;
        foreach (string part in BlendOrder)
        {
            if (part == "hair_front" && !showFrontHair) continue;
            if (part == "hair_back" && !showBackHair) continue;
            string filePart = part == "facial_extra" ? (female ? "makeup" : "beard") : part;
            var source = load_image(root + filePart + "_" + partIndices[part] + ".png");
            result.BlendRect(source, FullCanvas, Vector2I.Zero);
        }

        var texture = ImageTexture.CreateFromImage(result);
        TextureCache[cacheKey] = texture;
        TextureCacheOrder.Enqueue(cacheKey);
        while (TextureCacheOrder.Count > CompositeCacheLimit)
            TextureCache.Remove(TextureCacheOrder.Dequeue());
        return texture;
    }

    public static Texture2D Compose(HumanData data, int age)
    {
        int seed = data.appearance_seed != 0 ? data.appearance_seed : 1;
        bool female = data.gender == 1;
        string[] parts = female ? FemaleParts : MaleParts;
        int[] requested = requested_indices(data, female);
        for (int i = 0; i < requested.Length; i++)
            if (requested[i] < 0)
                requested[i] = choose_index(seed, parts[i]);
            else
                requested[i] = 0;

        if (!female && age < 35) requested[5] = 0;
        return Compose(female, requested);
    }

    public static Texture2D Compose(WorldNpcData npc)
    {
        int seed = npc.appearance_seed > 0 ? npc.appearance_seed : stable_seed(npc.npc_id + "|" + npc.npc_name);
        int gender = npc.gender is 0 or 1 ? npc.gender : seed % 2;
        var data = new HumanData { gender = gender, appearance_seed = seed, faction_name = npc.faction_id };
        return Compose(data, npc.current_age);
    }

    private static int[] requested_indices(EntityData data, bool female) =>
    [
        data.appearance_face_style,
        data.appearance_brow_style,
        data.appearance_eye_style >= 0 ? data.appearance_eye_style : data.appearance_eyebrow_style,
        data.appearance_nose_style,
        data.appearance_mouth_style,
        female ? data.appearance_makeup_style : data.appearance_beard_style,
        data.appearance_hair_back_style >= 0 ? data.appearance_hair_back_style : data.appearance_hair_style,
        data.appearance_hair_style,
        data.appearance_clothes_style,
        data.appearance_ear_style,
    ];

    private static int choose_index(int seed, string part)
    {
        return 0;
    }

    private static int stable_seed(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value) { hash ^= character; hash *= 16777619; }
            int seed = (int)(hash & 0x7fffffff);
            return seed == 0 ? 1 : seed;
        }
    }

    private static Image load_image(string path)
    {
        if (ImageCache.TryGetValue(path, out var cached)) return cached;
        var bytes = Godot.FileAccess.GetFileAsBytes(path);
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        if (bytes.Length == 0 || image.LoadPngFromBuffer(bytes) != Error.Ok || image.IsEmpty())
        {
            GD.PushError("CurrentTestPortraitComposer: 无法读取测试素材 " + path);
            image = Image.CreateEmpty(CanvasWidth, CanvasHeight, false, Image.Format.Rgba8);
        }
        ImageCacheOrder.Enqueue(path);
        while (ImageCacheOrder.Count > SourceImageCacheLimit)
            ImageCache.Remove(ImageCacheOrder.Dequeue());
        ImageCache[path] = image;
        return image;
    }
}
