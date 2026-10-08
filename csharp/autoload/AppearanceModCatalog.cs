using Godot;
using Godot.Collections;

/// <summary>扫描启用的外观 MOD 包，并向头像系统提供标准化部件清单。</summary>
public partial class AppearanceModCatalog : Node
{
	private const string ModRoot = "res://mods/appearance";
	private readonly Array<Dictionary> _parts = [];

	public override void _Ready() => reload();

	public void reload()
	{
		_parts.Clear();
		scan_directory(ModRoot);
	}

	public Array<Dictionary> get_parts(string gender, string part)
	{
		Array<Dictionary> result = [];
		foreach (Dictionary entry in _parts)
		{
			if (entry.GetValueOrDefault("gender", "").AsString() == gender
				&& entry.GetValueOrDefault("part", "").AsString() == part)
				result.Add(entry);
		}
		return result;
	}

	public bool allows_color_tint(string gender, string color_kind)
	{
		string part = color_kind == "skin" ? "face" : color_kind == "hair" ? "hair_front" : "";
		string flag = color_kind == "skin" ? "allow_skin_tone_tint" : color_kind == "hair" ? "allow_hair_color_tint" : "";
		if (part.Length == 0) return false;
		foreach (Dictionary entry in _parts)
			if (entry.GetValueOrDefault("gender", "").AsString() == gender
				&& entry.GetValueOrDefault("part", "").AsString() == part
				&& entry.GetValueOrDefault(flag, true).AsBool()) return true;
		return false;
	}

	public string get_variant_name(string gender, string part, int index)
	{
		foreach (Dictionary entry in get_parts(gender, part))
			if (entry.GetValueOrDefault("variant_index", -1).AsInt32() == index)
				return entry.GetValueOrDefault("display_name", $"款式 {index + 1}").AsString();
		return $"款式 {index + 1}";
	}

	public bool can_wear(Dictionary entry, EntityData? data)
	{
		string sect = entry.GetValueOrDefault("required_sect", "").AsString();
		string rank = entry.GetValueOrDefault("required_rank", "").AsString();
		if (sect.Length == 0 && rank.Length == 0) return true;
		return data is HumanData human && (sect.Length == 0 || human.sect_affiliation_id == sect)
			&& (rank.Length == 0 || human.sect_current_rank == rank);
	}

	private void scan_directory(string path)
	{
		DirAccess? directory = DirAccess.Open(path);
		if (directory is null)
			return;
		foreach (string folder in directory.GetDirectories())
			scan_directory(path.PathJoin(folder));
		if (!directory.FileExists("appearance_parts.json"))
			return;

		string configPath = path.PathJoin("appearance_parts.json");
		Godot.FileAccess? file = Godot.FileAccess.Open(configPath, Godot.FileAccess.ModeFlags.Read);
		if (file is null)
		{
			GD.PushWarning("无法读取外观 MOD：" + configPath);
			return;
		}
		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			GD.PushWarning("外观 MOD JSON 格式错误：" + configPath);
			return;
		}

		Dictionary config = parsed.AsGodotDictionary();
		if (!config.GetValueOrDefault("enabled", false).AsBool())
			return;
		Dictionary scalePivots = read_scale_pivots(config);
		Variant components = config.GetValueOrDefault("components", new Godot.Collections.Array());
		if (components.VariantType != Variant.Type.Array)
			return;
		foreach (Variant rawEntry in components.AsGodotArray())
		{
			if (rawEntry.VariantType != Variant.Type.Dictionary)
				continue;
			Dictionary source = rawEntry.AsGodotDictionary();
			int variantCount = Math.Max(1, source.GetValueOrDefault("variant_count", 1).AsInt32());
			for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
			{
				Dictionary entry = source.Duplicate();
				string packageId = config.GetValueOrDefault("package_id", path.GetFile()).AsString();
				entry["package_id"] = packageId;
				entry["rig_id"] = config.GetValueOrDefault("rig_id", packageId).AsString();
				entry["canvas"] = config.GetValueOrDefault("canvas", new Dictionary { ["width"] = 384, ["height"] = 640 });
				entry["scale_pivots_normalized"] = scalePivots.Duplicate();
				entry["allow_skin_tone_tint"] = source.GetValueOrDefault("allow_skin_tone_tint", config.GetValueOrDefault("allow_skin_tone_tint", true)).AsBool();
				entry["allow_hair_color_tint"] = source.GetValueOrDefault("allow_hair_color_tint", config.GetValueOrDefault("allow_hair_color_tint", true)).AsBool();
				entry["tags"] = entry.GetValueOrDefault("tags", new Godot.Collections.Array());
				entry["weight"] = entry.GetValueOrDefault("weight", 1.0f).AsSingle();
				entry["min_age"] = entry.GetValueOrDefault("min_age", 0).AsInt32();
				entry["max_age"] = entry.GetValueOrDefault("max_age", 999).AsInt32();
				entry["realm_min"] = entry.GetValueOrDefault("realm_min", 0).AsInt32();
				entry["variant_index"] = variantIndex;
				Variant names = source.GetValueOrDefault("variant_names", new Godot.Collections.Array());
				entry["display_name"] = names.VariantType == Variant.Type.Array && variantIndex < names.AsGodotArray().Count
					? names.AsGodotArray()[variantIndex].AsString() : $"款式 {variantIndex + 1}";
				entry["id"] = entry.GetValueOrDefault("id", "part").AsString() + $"_{variantIndex:00}";
				int indexStart = source.GetValueOrDefault("index_start", config.GetValueOrDefault("index_start", 0)).AsInt32();
				int indexPadding = Math.Max(0, source.GetValueOrDefault("index_padding", config.GetValueOrDefault("index_padding", 0)).AsInt32());
				int fileIndex = variantIndex + indexStart;
				Variant indices = source.GetValueOrDefault("variant_indices", new Godot.Collections.Array());
				if (indices.VariantType == Variant.Type.Array && variantIndex < indices.AsGodotArray().Count)
					fileIndex = indices.AsGodotArray()[variantIndex].AsInt32();
				string formattedIndex = indexPadding > 0 ? fileIndex.ToString($"D{indexPadding}") : fileIndex.ToString();
				string relativeFile = entry.GetValueOrDefault("file", "").AsString().Replace("{index}", formattedIndex);
				if (relativeFile.Length == 0)
					continue;
				entry["resource_path"] = path.PathJoin(relativeFile);
				entry["package_path"] = path;
				if (ResourceLoader.Exists(entry["resource_path"].AsString()))
					_parts.Add(entry);
				else
					GD.PushWarning("外观 MOD 部件不存在：" + entry["resource_path"].AsString());
			}
		}
	}

	private static Dictionary read_scale_pivots(Dictionary config)
	{
		string rigPath = config.GetValueOrDefault("rig_file", "").AsString();
		if (rigPath.Length > 0 && Godot.FileAccess.FileExists(rigPath))
		{
			Variant rigParsed = Json.ParseString(Godot.FileAccess.GetFileAsString(rigPath));
			if (rigParsed.VariantType == Variant.Type.Dictionary)
			{
				Variant pivots = rigParsed.AsGodotDictionary().GetValueOrDefault("scale_pivots_normalized", new Dictionary());
				if (pivots.VariantType == Variant.Type.Dictionary)
					return pivots.AsGodotDictionary();
			}
		}
		Variant fallback = config.GetValueOrDefault("scale_pivots_normalized", new Dictionary());
		return fallback.VariantType == Variant.Type.Dictionary ? fallback.AsGodotDictionary() : new Dictionary();
	}
}
