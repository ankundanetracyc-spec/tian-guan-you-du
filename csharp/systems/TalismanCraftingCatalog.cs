using Godot;

/// <summary>
/// 符工的原料目录与投料检查。这里只读现有材性、真实库存，不生成产物或估算费用。
/// </summary>
public static class TalismanCraftingCatalog
{
    private const string MaterialDirectory = "res://resources/entities/items";
    private static readonly string[] UtilityOrder = ["固", "聚", "通", "化", "引", "破", "护", "镇", "生", "明", "藏", "行"];
    private static readonly string[] ElementOrder = ["金", "木", "水", "火", "土"];
    private static readonly Dictionary<string, Resource> Definitions = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ProductDefinitions = new(StringComparer.Ordinal);
    private static IReadOnlyList<MaterialEntry>? _materials;

    public sealed record UtilityLayer(string Name, int Grade, string GradeDisplay);
    public sealed record UtilityOption(string Name, IReadOnlyList<int> Grades, string GradeDisplay);
    public sealed record Selection(string Path, int Quantity);
    public sealed record Note(string Tone, string Text);
    public sealed record Relation(string FromElement, string ToElement, string Kind, string Text);

    public sealed record MaterialEntry(string Path, ItemData Item, MaterialCraftData Craft)
    {
        public string Id => Item.definition_id.ToString();
        public string Name => Item.entity_name;
        public string Texture => Craft.texture;
        public string Icon => Item.icon_path;
        public string IconPath => Item.icon_path;
        public IReadOnlyList<string> Elements => ReadElements(Item, Craft);
        public string ElementText => Elements.Count == 0 ? "无五行" : string.Join("、", Elements);
        public IReadOnlyList<UtilityLayer> UtilityLayers => Craft.utilities
            .Where(value => value is not null)
            .OrderBy(value => UtilityIndex(value.utility_id))
            .ThenBy(value => value.utility_id, StringComparer.Ordinal)
            .Select(value => new UtilityLayer(value.utility_id, value.grade, GradeName(value.grade))).ToArray();
        public string UtilityText => UtilityLayers.Count == 0 ? "无功用" :
            string.Join("、", UtilityLayers.Select(value => value.Name + value.GradeDisplay));
        public IReadOnlyList<string> ValidationErrors => ValidateMaterial(this);
        public int Stock(Character? owner) => owner is null || !GodotObject.IsInstanceValid(owner)
            ? 0 : owner.get_inventory_total_count(Path);
    }

    public sealed class Analysis
    {
        public bool IsValid => Blockers.Count == 0;
        public List<string> Blockers { get; } = [];
        public List<Note> Notes { get; } = [];
        public List<Relation> Relations { get; } = [];
        public List<UtilityOption> AvailableUtilities { get; } = [];
        public int TotalKinds { get; internal set; }
        public long TotalQuantity { get; internal set; }
    }

    /// <summary>登记已有用途合同的产品，防止按裸原料返工。按定义路径登记，不按材名判断。</summary>
    public static void RegisterProductDefinition(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && ProductDefinitions.Add(path)) _materials = null;
    }

    /// <summary>新原料沿物品资源目录自动发现；刷新会重新扫描，也适用于尚未导入的新资源。</summary>
    public static IReadOnlyList<MaterialEntry> DiscoverMaterials(bool refresh = false)
    {
        if (!refresh && _materials is not null) return _materials;
        var paths = ResourcePaths(MaterialDirectory).ToHashSet(StringComparer.Ordinal);
        foreach (var removed in Definitions.Keys.Where(path => !paths.Contains(path)).ToArray()) Definitions.Remove(removed);
        var result = new List<MaterialEntry>();
        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (ProductDefinitions.Contains(path)) continue;
            if (!Definitions.TryGetValue(path, out var definition) || !GodotObject.IsInstanceValid(definition))
            {
                definition = ResourceLoader.Load(path);
                if (definition is null) continue;
                Definitions[path] = definition;
            }
            if (definition is ItemData { item_category: "材料", craft_data: MaterialCraftData craft } item)
                result.Add(new MaterialEntry(path, item, craft));
        }
        return _materials = result.AsReadOnly();
    }

    public static bool IsEligible(MaterialEntry material, string mode) =>
        material.Item.item_category == "材料" && !ProductDefinitions.Contains(material.Path) && material.ValidationErrors.Count == 0 &&
        (mode switch
        {
            "paper" => material.Texture is "纤维" or "液体" or "黏胶",
            "ink" => material.Texture is "致密" or "颗粒" or "液体" or "黏胶",
            _ => false
        });

    public static string EligibilityReason(MaterialEntry material, string mode)
    {
        if (ProductDefinitions.Contains(material.Path)) return "已有成品用途，不能作为原料返工。";
        if (material.Item.item_category != "材料") return "此物不是通用原料。";
        if (material.ValidationErrors.Count > 0) return string.Join("；", material.ValidationErrors);
        return (mode, material.Texture) switch
        {
            ("paper", "纤维") => "形成纸基。",
            ("paper", "液体") => "调和纸基，也保留自身功用来源。",
            ("paper", "黏胶") => "可选结合料，不能代替纤维与液体。",
            ("ink", "致密") => "主墨源，需解料并配入液体。",
            ("ink", "颗粒") => "主墨源，需分散并配入液体。",
            ("ink", "液体") => "可直接作液墨源，无须另加固体。",
            ("ink", "黏胶") => "可选定墨辅料，不能独立作墨源。",
            ("paper", _) => "此质地不用于制符纸。",
            ("ink", _) => "此质地不用于研墨。",
            _ => "请选择制符纸或研墨。"
        };
    }

    public static Analysis Analyze(string mode, IEnumerable<Selection> selections, Character? owner,
        IEnumerable<string>? retained = null) =>
        Analyze(mode, selections, DiscoverMaterials(), path => owner is null || !GodotObject.IsInstanceValid(owner)
            ? 0 : owner.get_inventory_total_count(path), retained);

    /// <summary>同一检查也可用于隔离测试。库存由调用者提供，不自动补料。</summary>
    public static Analysis Analyze(string mode, IEnumerable<Selection> selections, IReadOnlyList<MaterialEntry> catalog,
        Func<string, int> stock, IEnumerable<string>? retained = null)
    {
        var result = new Analysis();
        if (mode is not ("paper" or "ink"))
        {
            result.Blockers.Add("请选择制符纸或研墨。");
            return result;
        }
        var quantities = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var selection in selections)
        {
            if (string.IsNullOrWhiteSpace(selection.Path))
            {
                result.Blockers.Add("材料定义缺失，请重新选择。");
                continue;
            }
            if (selection.Quantity <= 0)
            {
                result.Blockers.Add("投料份数须为正整数。");
                continue;
            }
            quantities[selection.Path] = quantities.GetValueOrDefault(selection.Path) + selection.Quantity;
        }
        result.TotalKinds = quantities.Count;
        result.TotalQuantity = quantities.Values.Sum();
        if (quantities.Count == 0)
        {
            result.Blockers.Add("先选本批材料。");
            return result;
        }
        var definitions = catalog.GroupBy(entry => entry.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var materials = new List<MaterialEntry>();
        foreach (var (path, quantity) in quantities.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!definitions.TryGetValue(path, out var material))
            {
                result.Blockers.Add("有材料定义已不可用，请重新选择。");
                continue;
            }
            if (!IsEligible(material, mode))
                result.Blockers.Add(material.Name + "：" + EligibilityReason(material, mode));
            else materials.Add(material);
            if (quantity > Math.Max(0, stock(path))) result.Blockers.Add(material.Name + "存量不足。");
        }

        var hasLiquid = materials.Any(entry => entry.Texture == "液体");
        var hasGlue = materials.Any(entry => entry.Texture == "黏胶");
        if (mode == "paper")
        {
            if (!materials.Any(entry => entry.Texture == "纤维")) result.Blockers.Add("缺少纤维纸基。");
            if (!hasLiquid) result.Blockers.Add("缺少调和用的液体。");
            if (hasGlue) result.Notes.Add(new Note("neutral", "胶料帮助结合，也会改变纸的渗墨表现。"));
        }
        else
        {
            if (!materials.Any(entry => entry.Texture is "致密" or "颗粒" or "液体"))
                result.Blockers.Add("缺少主墨源；胶料不能独立成墨。");
            if (!hasLiquid && materials.Any(entry => entry.Texture is "致密" or "颗粒"))
                result.Blockers.Add("固体墨源需要配入液体。");
            if (hasLiquid && !materials.Any(entry => entry.Texture is "致密" or "颗粒"))
                result.Notes.Add(new Note("good", "液料可直接调墨。"));
            if (hasGlue) result.Notes.Add(new Note("neutral", "胶料帮助附着与定墨，也会影响流动。"));
        }

        foreach (var utility in UtilityOrder)
        {
            var grades = materials.SelectMany(entry => entry.UtilityLayers).Where(layer => layer.Name == utility)
                .Select(layer => layer.Grade).Distinct().OrderDescending().ToArray();
            if (grades.Length > 0) result.AvailableUtilities.Add(new UtilityOption(utility, grades,
                string.Join("／", grades.Select(GradeName))));
        }
        if (retained is not null)
        {
            var chosen = retained.Distinct(StringComparer.Ordinal).ToArray();
            if (chosen.Length > 4) result.Blockers.Add("最多保留四项功用。");
            foreach (var utility in chosen)
                if (!result.AvailableUtilities.Any(value => value.Name == utility))
                    result.Blockers.Add("所选材料没有「" + utility + "」来源。");
        }

        // 只列实际来源之间的有向关系，不预发全批倍率或声称已经保留了功用。
        var signatures = materials.GroupBy(Signature, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        var relations = new Dictionary<string, Relation>(StringComparer.Ordinal);
        for (var from = 0; from < signatures.Length; from++)
        for (var to = 0; to < signatures.Length; to++)
        {
            if (from == to) continue;
            foreach (var source in signatures[from].Elements)
            foreach (var target in signatures[to].Elements)
            {
                var kind = ElementRelation(source, target);
                if (kind.Length == 0) continue;
                var text = source + kind + target + (kind == "生" ? "，可助本次调和与保留。" : "，留意" + target + "方的处理与保留。");
                relations.TryAdd(source + kind + target, new Relation(source, target, kind, text));
            }
        }
        result.Relations.AddRange(relations.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value));
        return result;
    }

    /// <summary>归并与重演仅用材性，不含名称、身份、相性或材料总品阶。</summary>
    public static string Signature(MaterialEntry material) => material.Texture + "|" +
        string.Join(",", material.Elements) + "|" + string.Join(",", material.UtilityLayers.Select(layer => layer.Name + ":" + layer.Grade));

    public static IReadOnlyList<string> ValidateMaterial(MaterialEntry material)
    {
        var errors = new List<string>();
        if (!ItemAffixCatalog.is_material_texture(material.Texture)) errors.Add("质地定义无效");
        foreach (var element in material.Elements)
            if (!ElementOrder.Contains(element, StringComparer.Ordinal)) errors.Add("五行定义无效：" + element);
        var taggedElements = ItemAffixCatalog.get_tag_values(material.Item.tags, "五行");
        if (taggedElements.Count > 0 && !string.IsNullOrEmpty(material.Craft.element) && !taggedElements.Contains(material.Craft.element))
            errors.Add("五行字段与词缀不一致");
        if (material.Craft.utilities.Count > 4) errors.Add("单材功用超过四项");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var utility in material.Craft.utilities)
        {
            if (utility is null) { errors.Add("功用定义缺失"); continue; }
            if (!UtilityOrder.Contains(utility.utility_id, StringComparer.Ordinal)) errors.Add("功用定义无效：" + utility.utility_id);
            if (!seen.Add(utility.utility_id)) errors.Add("功用重复：" + utility.utility_id);
            if (utility.grade is < 1 or > 9) errors.Add("功用品级无效：" + utility.utility_id);
        }
        return errors;
    }

    public static string GradeName(int grade) => grade switch
    {
        1 => "九品", 2 => "八品", 3 => "七品", 4 => "六品", 5 => "五品", 6 => "四品", 7 => "三品", 8 => "二品", 9 => "一品",
        _ => "品级无效（" + grade + "）"
    };

    private static int UtilityIndex(string utility)
    {
        var index = Array.IndexOf(UtilityOrder, utility);
        return index < 0 ? int.MaxValue : index;
    }

    private static IReadOnlyList<string> ReadElements(ItemData item, MaterialCraftData craft)
    {
        var elements = ItemAffixCatalog.get_tag_values(item.tags, "五行").ToList();
        if (!string.IsNullOrWhiteSpace(craft.element)) elements.Add(craft.element);
        return elements.Distinct(StringComparer.Ordinal).OrderBy(element =>
        {
            var index = Array.IndexOf(ElementOrder, element);
            return index < 0 ? int.MaxValue : index;
        }).ThenBy(element => element, StringComparer.Ordinal).ToArray();
    }

    private static string ElementRelation(string source, string target) => (source, target) switch
    {
        ("木", "火") or ("火", "土") or ("土", "金") or ("金", "水") or ("水", "木") => "生",
        ("木", "土") or ("土", "水") or ("水", "火") or ("火", "金") or ("金", "木") => "克",
        _ => ""
    };

    private static IEnumerable<string> ResourcePaths(string directory)
    {
        var entries = new HashSet<string>(ResourceLoader.ListDirectory(directory), StringComparer.Ordinal);
        using var access = DirAccess.Open(directory);
        if (access is not null)
        {
            entries.UnionWith(access.GetFiles());
            entries.UnionWith(access.GetDirectories().Select(name => name + "/"));
        }
        foreach (var file in entries.OrderBy(value => value, StringComparer.Ordinal))
        {
            var path = directory + "/" + file.TrimEnd('/');
            if (file.EndsWith('/'))
            {
                foreach (var nested in ResourcePaths(path)) yield return nested;
            }
            else if (file.EndsWith(".tres", StringComparison.Ordinal) || file.EndsWith(".res", StringComparison.Ordinal)) yield return path;
        }
    }
}
