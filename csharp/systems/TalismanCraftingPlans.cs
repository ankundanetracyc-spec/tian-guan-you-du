using Godot;
using System.Globalization;
using System.Text.Json;
using Request = TalismanCraftingService.Request;
using Selection = TalismanCraftingCatalog.Selection;

/// <summary>人物的三工艺配伍册。操作草稿，不制作、不扣料、不推进历法。</summary>
public static class TalismanCraftingPlans
{
    public const int Version = 1;
    public const int NameLimit = 32;
    private static readonly string[] UtilityNames = ["固", "聚", "通", "化", "引", "破", "护", "镇", "生", "明", "藏", "行"];

    public sealed record Entry(string Id, string Name, string Mode, Request Request, string Summary)
    {
        public IReadOnlyList<string> Warnings { get; init; } = [];
        public bool IsLoadable { get; init; } = true;
    }

    public static IReadOnlyList<Entry> List(Character? owner, string mode)
    {
        var human = Human(owner);
        if (human?.talisman_plans is null || !IsMode(mode)) return [];
        var result = new List<Entry>();
        foreach (var data in human.talisman_plans)
        {
            if (data is null || data.mode != mode) continue;
            bool valid = TryRead(data, out var request, out string reason);
            result.Add(new Entry(data.id, data.plan_name, data.mode, request, valid ? Summary(request) : "记录暂不可载入。")
            {
                IsLoadable = valid,
                Warnings = valid ? Availability(owner!, request) : [reason]
            });
        }
        return result.AsReadOnly();
    }

    public static bool Save(Character? owner, string name, Request request, bool overwrite, out string reason)
    {
        var human = Human(owner);
        if (human is null) { reason = "请先选择制作人物。"; return false; }
        if (!TryName(name, out string normalizedName, out reason) || !TryNormalize(request, out var normalized, out reason)) return false;
        var plans = human.talisman_plans;
        int index = -1;
        if (plans is not null)
            for (int i = 0; i < plans.Count; i++)
                if (plans[i] is { } existing && existing.mode == normalized.Mode && SameName(existing.plan_name, normalizedName))
                { index = i; break; }
        if (index >= 0 && !overwrite) { reason = "本工艺已有同名配伍，请确认覆盖。"; return false; }
        var data = new TalismanCraftingPlanData
        {
            version = Version, id = index >= 0 && !string.IsNullOrWhiteSpace(plans![index].id)
                ? plans[index].id : Guid.NewGuid().ToString("N"),
            plan_name = normalizedName, mode = normalized.Mode, request_json = SerializeActive(normalized)
        };
        human.talisman_plans ??= [];
        if (index >= 0) human.talisman_plans[index] = data; else human.talisman_plans.Add(data);
        reason = "";
        return true;
    }

    public static bool Rename(Character? owner, string id, string name, out string reason)
    {
        var human = Human(owner);
        if (human is null) { reason = "请先选择制作人物。"; return false; }
        if (!TryName(name, out string normalizedName, out reason)) return false;
        int index = Index(human, id);
        if (index < 0) { reason = "这份配伍已不在册中。"; return false; }
        var original = human.talisman_plans[index];
        for (int i = 0; i < human.talisman_plans.Count; i++)
            if (i != index && human.talisman_plans[i] is { } other && other.mode == original.mode && SameName(other.plan_name, normalizedName))
            { reason = "本工艺已有同名配伍。"; return false; }
        // 替换记录资源，避免外部持有的旧引用跟着改名。
        human.talisman_plans[index] = new TalismanCraftingPlanData { version = original.version, id = original.id,
            plan_name = normalizedName, mode = original.mode, request_json = original.request_json };
        reason = "";
        return true;
    }

    public static bool Delete(Character? owner, string id, out string reason)
    {
        var human = Human(owner);
        if (human is null) { reason = "请先选择制作人物。"; return false; }
        int index = Index(human, id);
        if (index < 0) { reason = "这份配伍已不在册中。"; return false; }
        human.talisman_plans.RemoveAt(index);
        reason = "";
        return true;
    }

    public static bool TryLoad(Character? owner, string id, out Request request, out string reason)
    {
        request = new Request();
        var human = Human(owner);
        if (human is null) { reason = "请先选择制作人物。"; return false; }
        int index = Index(human, id);
        if (index < 0) { reason = "这份配伍已不在册中。"; return false; }
        if (!TryRead(human.talisman_plans[index], out request, out reason)) return false;
        // 提醒不改变草稿：旧物路径、缺货份数和未学技艺均原样保留给当前制作检查。
        reason = string.Join("；", Availability(owner!, request).Take(3));
        return true;
    }

    public static Request NormalizeRequest(Request request)
    {
        if (!TryNormalize(request, out var normalized, out string reason)) throw new ArgumentException(reason, nameof(request));
        return normalized;
    }

    private static HumanData? Human(Character? owner) => owner is not null && GodotObject.IsInstanceValid(owner)
        ? owner.entity_data as HumanData : null;
    private static bool IsMode(string? mode) => mode is "paper" or "ink" or "drawing";
    private static bool SameName(string? left, string right) => string.Equals(left?.Trim(), right, StringComparison.Ordinal);
    private static int Index(HumanData human, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || human.talisman_plans is null) return -1;
        for (int i = 0; i < human.talisman_plans.Count; i++) if (human.talisman_plans[i]?.id == id) return i;
        return -1;
    }

    private static bool TryName(string? name, out string normalized, out string reason)
    {
        normalized = name?.Trim() ?? "";
        if (normalized.Length == 0) { reason = "请填写配伍名称。"; return false; }
        if (normalized.Any(char.IsControl)) { reason = "配伍名称不能换行或包含控制字符。"; return false; }
        if (StringInfo.ParseCombiningCharacters(normalized).Length > NameLimit) { reason = $"配伍名称最多{NameLimit}字。"; return false; }
        reason = "";
        return true;
    }

    private static bool TryRead(TalismanCraftingPlanData data, out Request request, out string reason)
    {
        request = new Request { Mode = IsMode(data.mode) ? data.mode : "paper" };
        if (data.version != Version) { reason = "这份配伍的版本暂不支持。"; return false; }
        if (string.IsNullOrWhiteSpace(data.id) || !IsMode(data.mode) || !TryName(data.plan_name, out _, out _))
        { reason = "配伍记录已失效。"; return false; }
        if (string.IsNullOrWhiteSpace(data.request_json)) { reason = "配伍内容已损坏。"; return false; }
        Request? parsed;
        try { parsed = JsonSerializer.Deserialize<Request>(data.request_json); }
        catch (JsonException) { reason = "配伍内容已损坏。"; return false; }
        if (parsed is null || parsed.Mode != data.mode) { reason = "配伍内容与工艺不一致。"; return false; }
        return TryNormalize(parsed, out request, out reason);
    }

    private static bool TryNormalize(Request? source, out Request request, out string reason)
    {
        request = new Request();
        if (source is null || !IsMode(source.Mode)) { reason = "请选择有效的符工工艺。"; return false; }
        if (source.Mode is "paper" or "ink")
        {
            if (string.IsNullOrWhiteSpace(source.TechniqueId)) { reason = "请选择本工艺的技艺。"; return false; }
            if (!Selections(source.Materials, out var materials, out reason)) return false;
            if (materials.Count == 0) { reason = "先选入材料，再保存配伍。"; return false; }
            if (source.Mode == "ink" && (source.RetainedUtilities is null || source.RetainedUtilities.Count > 4
                || source.RetainedUtilities.Any(utility => !UtilityNames.Contains(utility, StringComparer.Ordinal))
                || source.RetainedUtilities.Distinct(StringComparer.Ordinal).Count() != source.RetainedUtilities.Count))
            { reason = "留墨功用记录无效。"; return false; }
            request = new Request { Mode = source.Mode, TechniqueId = source.TechniqueId, Materials = materials,
                RetainedUtilities = source.Mode == "ink" ? new(source.RetainedUtilities!) : [] };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(source.RecipeId) || string.IsNullOrWhiteSpace(source.WritingId)
                || source.PaperPath is null || source.BrushPath is null || source.Quantity <= 0)
            { reason = "画符配伍记录无效。"; return false; }
            if (!Selections(source.InkSelections, out var inks, out reason)) return false;
            if (string.IsNullOrWhiteSpace(source.PaperPath) && string.IsNullOrWhiteSpace(source.BrushPath) && inks.Count == 0)
            { reason = "先选入纸、墨或符笔，再保存配伍。"; return false; }
            request = new Request { Mode = "drawing", RecipeId = source.RecipeId, PaperPath = source.PaperPath,
                InkSelections = inks, BrushPath = source.BrushPath, Quantity = source.Quantity, WritingId = source.WritingId };
        }
        // 跨越JSON边界，调用者后续改列表也不会串改保存内容或另一份载入结果。
        request = JsonSerializer.Deserialize<Request>(SerializeActive(request))!;
        reason = "";
        return true;
    }

    private static bool Selections(IReadOnlyList<Selection>? source, out List<Selection> selections, out string reason)
    {
        selections = [];
        if (source is null || source.Any(item => item is null || string.IsNullOrWhiteSpace(item.Path) || item.Quantity <= 0))
        { reason = "投料记录须包含有效物品路径和正整数份数。"; return false; }
        foreach (var group in source.GroupBy(item => item.Path, StringComparer.Ordinal))
        {
            long count = group.Sum(item => (long)item.Quantity);
            if (count > int.MaxValue) { reason = "投料份数超出记录范围。"; return false; }
            selections.Add(new Selection(group.Key, (int)count));
        }
        reason = "";
        return true;
    }

    private static string SerializeActive(Request request) => request.Mode switch
    {
        "paper" => JsonSerializer.Serialize(new { request.Mode, request.Materials, request.TechniqueId }),
        "ink" => JsonSerializer.Serialize(new { request.Mode, request.Materials, request.TechniqueId, request.RetainedUtilities }),
        _ => JsonSerializer.Serialize(new { request.Mode, request.RecipeId, request.PaperPath, request.InkSelections,
            request.BrushPath, request.Quantity, request.WritingId })
    };

    private static string Summary(Request request)
    {
        if (request.Mode == "drawing")
        {
            string recipe = TalismanCraftingService.Recipes().FirstOrDefault(recipe => recipe.Id == request.RecipeId)?.Name ?? "所存符谱";
            string writing = TalismanCraftingService.Methods("drawing").FirstOrDefault(method => method.Id == request.WritingId)?.Name ?? "所存写法";
            return $"{recipe} · 上限{request.Quantity}张 · {request.InkSelections.Count}种墨 · {writing}";
        }
        string technique = TalismanCraftingService.Methods(request.Mode).FirstOrDefault(method => method.Id == request.TechniqueId)?.Name ?? "所存技艺";
        return $"{request.Materials.Count}味 · {request.Materials.Sum(item => (long)item.Quantity)}份 · {technique}";
    }

    private static IReadOnlyList<string> Availability(Character owner, Request request)
    {
        var warnings = new List<string>();
        string methodId = request.Mode == "drawing" ? request.WritingId : request.TechniqueId;
        var method = TalismanCraftingService.Methods(request.Mode).FirstOrDefault(method => method.Id == methodId);
        if (method is null) warnings.Add("所存技艺已不可用，请重新选择。");
        else if (!TalismanCraftingService.IsMethodKnown(owner, methodId)) warnings.Add("尚未研习" + method.Name + "。");
        if (request.Mode != "drawing")
            warnings.AddRange(TalismanCraftingCatalog.Analyze(request.Mode, request.Materials, owner,
                request.Mode == "ink" ? request.RetainedUtilities : null).Blockers);
        else
        {
            if (!TalismanCraftingService.IsRecipeKnown(owner, request.RecipeId)) warnings.Add("所存符谱尚不可用。");
            CheckProduct(request.PaperPath, "paper", request.Quantity, "符纸");
            foreach (var ink in request.InkSelections) CheckProduct(ink.Path, "ink", ink.Quantity, "符墨");
            CheckProduct(request.BrushPath, "brush", 1, "符笔");
            if (request.InkSelections.Count == 0) warnings.Add("尚未选入符墨。");
        }
        return warnings.Distinct(StringComparer.Ordinal).ToArray();

        void CheckProduct(string path, string kind, int quantity, string name)
        {
            if (string.IsNullOrWhiteSpace(path)) { warnings.Add("尚未选择" + name + "。"); return; }
            if (!ResourceLoader.Exists(path) || (path.StartsWith("user://", StringComparison.Ordinal) && !Godot.FileAccess.FileExists(path))
                || ResourceLoader.Load(path) is not TalismanProductItemData product || product.Snapshot is not { } snapshot
                || !TalismanCraftingService.ValidateSnapshot(snapshot) || snapshot.Kind != kind)
                warnings.Add(name + "资料已不可用，原选择仍保留。");
            if (owner.get_inventory_total_count(path) < quantity) warnings.Add(name + "存量不足，需求份数已保留。");
        }
    }
}
