using Godot;
using Godot.Collections;

/// <summary>根据角色上下文选择局外立绘部件；绘制工作由 PortraitRenderer 负责。</summary>
public partial class PortraitCatalog : Node
{
    private static readonly string[] LayerOrder = ["hair_back", "face", "ear", "clothes", "eye", "brow", "nose", "mouth", "beard", "makeup", "hair_front"];
    private static readonly Dictionary FactionTagBonuses = new()
    {
        ["玄剑宗"] = new Godot.Collections.Array { "sword", "cold", "faction:玄剑宗", "sharp" },
        ["丹霞谷"] = new Godot.Collections.Array { "alchemy", "light", "faction:丹霞谷", "gentle" },
        ["魔道"] = new Godot.Collections.Array { "demonic", "dark", "mark", "faction:魔道" },
    };

    public Dictionary get_context(EntityData? data, string gender, int age, string faction_name, string realm)
    {
        string faction = faction_name.Trim();
        return new Dictionary
        {
            ["gender"] = gender, ["age"] = age, ["faction"] = faction, ["realm"] = realm,
            ["realm_rank"] = realm_rank(realm),
            ["seed"] = data is not null && data.appearance_seed != 0 ? data.appearance_seed : 1,
            ["preferred_tags"] = FactionTagBonuses.GetValueOrDefault(faction, new Godot.Collections.Array()),
        };
    }

    public Dictionary choose_part(Dictionary context, string part, string requested_id = "")
    {
        Array<Dictionary> candidates = [];
        foreach (Dictionary entry in GetNode<AppearanceModCatalog>("/root/AppearanceModCatalog").get_parts(value(context, "gender", "male").AsString(), part))
        {
            if (matches_context(entry, context)) candidates.Add(entry);
        }
        if (candidates.Count == 0) return [];
        if (requested_id.Length > 0)
        {
            foreach (Dictionary entry in candidates)
                if (value(entry, "id", "").AsString() == requested_id) return entry;
            if (int.TryParse(requested_id, out int requestedVariant))
                foreach (Dictionary entry in candidates)
                    if (value(entry, "variant_index", -1).AsInt32() == requestedVariant) return entry;
            // Retired lookalike styles resolve to their retained component, independent of other slots.
            foreach (Dictionary entry in candidates)
            {
                Variant aliases = value(entry, "variant_aliases", new Dictionary());
                if (aliases.VariantType != Variant.Type.Dictionary) continue;
                Dictionary mapping = aliases.AsGodotDictionary();
                if (mapping.ContainsKey(requested_id) &&
                    value(entry, "variant_index", -1).AsInt32() == mapping[requested_id].AsInt32()) return entry;
            }
        }

        // Explicit choices keep their saved style; the default beard age rule applies to random selection.
        if (part == "beard" && value(context, "age", 0).AsInt32() < 35)
            for (int index = candidates.Count - 1; index >= 0; index--)
                if (value(candidates[index], "variant_index", 0).AsInt32() != 0) candidates.RemoveAt(index);
        if (candidates.Count == 0) return [];

        var rng = new RandomNumberGenerator { Seed = (ulong)(value(context, "seed", 1).AsInt64() ^ (long)GD.Hash(part)) };
        float totalWeight = 0.0f;
        foreach (Dictionary entry in candidates) totalWeight += effective_weight(entry, context);
        float pick = rng.RandfRange(0.0f, totalWeight);
        foreach (Dictionary entry in candidates)
        {
            pick -= effective_weight(entry, context);
            if (pick <= 0.0f) return entry;
        }
        return candidates[^1];
    }

    public Dictionary select_portrait(Dictionary context, Dictionary? requested = null)
    {
        requested ??= [];
        Dictionary result = [];
        Dictionary rigContext = context.Duplicate();
        Dictionary clothes = choose_part(rigContext, "clothes", value(requested, "clothes", "").AsString());
        if (clothes.Count > 0) { result["clothes"] = clothes; rigContext["rig_id"] = value(clothes, "rig_id", ""); }
        foreach (string part in LayerOrder)
            if (part != "clothes")
            {
                Dictionary entry = choose_part(rigContext, part, value(requested, part, "").AsString());
                if (entry.Count > 0) result[part] = entry;
            }
        return result;
    }

    /// <summary>Resolve saved appearance selections to the variant indexes used by the active portrait pack.</summary>
    public Dictionary get_requested_appearance(EntityData? data)
    {
        Dictionary requested = [];
        if (data is null) return requested;
        add_requested(requested, "face", data.appearance_face_style);
        add_requested(requested, "ear", data.appearance_ear_style);
        add_requested(requested, "eye", data.appearance_eye_style >= 0 ? data.appearance_eye_style : data.appearance_eyebrow_style);
        add_requested(requested, "brow", data.appearance_brow_style);
        add_requested(requested, "nose", data.appearance_nose_style);
        add_requested(requested, "mouth", data.appearance_mouth_style);
        add_requested(requested, "beard", data.appearance_beard_style);
        add_requested(requested, "makeup", data.appearance_makeup_style);
        add_requested(requested, "clothes", data.appearance_clothes_style);
        add_requested(requested, "hair_back", data.appearance_hair_back_style >= 0 ? data.appearance_hair_back_style : data.appearance_hair_style);
        add_requested(requested, "hair_front", data.appearance_hair_style);
        return requested;
    }

    public string make_cache_key(EntityData? data, Dictionary context, Dictionary? requested = null)
    {
        requested ??= [];
        string[] fields = [value(context, "gender", "male").AsString(), value(context, "age", 0).AsString(), value(context, "faction", "").AsString(), value(context, "realm", "").AsString(), (data?.appearance_seed ?? 0).ToString(), Json.Stringify(requested)];
        return string.Join("|", fields);
    }

    private static bool matches_context(Dictionary entry, Dictionary context)
    {
        string requestedRig = value(context, "rig_id", "").AsString();
        if (requestedRig.Length > 0 && value(entry, "rig_id", "").AsString() != requestedRig) return false;
        int age = value(context, "age", 0).AsInt32();
        if (age < value(entry, "min_age", 0).AsInt32() || age > value(entry, "max_age", 999).AsInt32()) return false;
        if (value(context, "realm_rank", 0).AsInt32() < value(entry, "realm_min", 0).AsInt32()) return false;
        string faction = value(context, "faction", "").AsString();
        foreach (Variant raw in value(entry, "tags", new Godot.Collections.Array()).AsGodotArray())
        {
            string tag = raw.AsString();
            if (tag.StartsWith("only_faction:") && tag[13..] != faction) return false;
            if (tag.StartsWith("only_realm:") && !value(context, "realm", "").AsString().StartsWith(tag[11..])) return false;
        }
        return true;
    }

    private static float effective_weight(Dictionary entry, Dictionary context)
    {
        float weight = Math.Max(0.01f, value(entry, "weight", 1.0f).AsSingle());
        Godot.Collections.Array tags = value(entry, "tags", new Godot.Collections.Array()).AsGodotArray();
        foreach (Variant preferred in value(context, "preferred_tags", new Godot.Collections.Array()).AsGodotArray()) if (tags.Contains(preferred)) weight *= 3.0f;
        int age = value(context, "age", 0).AsInt32();
        if (age >= 65 && tags.Contains("elder")) weight *= 3.0f;
        else if (age >= 40 && tags.Contains("mature")) weight *= 2.0f;
        else if (age < 40 && tags.Contains("youth")) weight *= 1.5f;
        return weight;
    }

    private static int realm_rank(string realm) => realm.StartsWith("天人") || realm.StartsWith("化神") ? 5 : realm.StartsWith("神胎") || realm.StartsWith("元婴") ? 4 : realm.StartsWith("金丹") ? 3 : realm.StartsWith("玄基") || realm.StartsWith("筑基") ? 2 : realm.StartsWith("云息") || realm.StartsWith("练气") ? 1 : 0;
    private static void add_requested(Dictionary requested, string part, int index)
    {
        if (index >= 0) requested[part] = index.ToString();
    }
    private static Variant value(Dictionary dictionary, Variant key, Variant fallback) => dictionary.GetValueOrDefault(key, fallback);
}
