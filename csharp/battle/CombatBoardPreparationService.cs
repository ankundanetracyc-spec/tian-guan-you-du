using Godot;
using System.Text.Json;

/// <summary>人物的默认开局及命名库。所有读取返回副本，全部通过校验才写入人物。</summary>
public static class CombatBoardPreparationService
{
    public static bool CanManage(Character actor) => GodotObject.IsInstanceValid(actor)
        && actor.entity_data is HumanData && !actor.HasMeta("artifact_battle_clone");
    public static bool RequiresIndicator(Character actor) => actor.get_combat_passive_paths().Any(path =>
        ResourceLoader.Exists(path) && ResourceLoader.Load<ActiveSkillData>(path)?.skill_name == "玄枢含章");
    public static CombatBoardCatalog Catalog(Character actor) => actor.HasMeta("artifact_battle_clone")
        && actor.HasMeta("combat_board_catalog") && actor.GetMeta("combat_board_catalog").AsGodotObject() is CombatBoardCatalog frozen
            ? frozen : CombatBoardCatalog.FromCharacter(actor);
    public static CombatBoardLayout GetDraft(Character actor)
    {
        if (actor.entity_data is not HumanData human) return new();
        var catalog = Catalog(actor);
        if (human.combat_board_preparation is { } applied)
            return CombatBoardUnlockRules.Normalize(applied, actor.cultivation_realm, catalog.FindSkill);
        if (human.combat_board_layout is { } previous)
            return CombatBoardUnlockRules.Normalize(previous, actor.cultivation_realm, catalog.FindSkill);
        var keys = human.combat_skill_references.Count > 0
            ? human.combat_skill_references.Select(item => item?.Key ?? "") : human.combat_skill_slot_paths;
        return CombatBoardUnlockRules.CreatePreview(keys, human.skill_rewrite_bindings, catalog.FindSkill,
            RequiresIndicator(actor), actor.cultivation_realm);
    }
    public static CombatBoardPresetLibrary GetLibrary(Character actor)
    {
        var source = (actor.entity_data as HumanData)?.combat_board_presets;
        const string legacy = "user://combat_board_presets.tres";
        if (source is null && ResourceLoader.Exists(legacy))
        {
            try { source = ResourceLoader.Load<CombatBoardPresetLibrary>(legacy, cacheMode: ResourceLoader.CacheMode.Ignore); }
            catch { GD.PushWarning("旧配招库无法读取，原文件已保留。"); }
        }
        var copy = source?.CopyConfiguration() ?? new();
        for (int i = 0; i < copy.layouts.Count; i++)
            if (copy.layouts[i].preset_id.Length == 0) copy.layouts[i].preset_id = "legacy-plan-" + i;
        for (int i = 0; i < copy.combinations.Count; i++)
            if (copy.combinations[i].preset_id.Length == 0) copy.combinations[i].preset_id = "legacy-combo-" + i;
        return copy;
    }
    public static string Signature(CombatBoardLayout layout) => JsonSerializer.Serialize(new
    {
        size = new[] { layout.size.X, layout.size.Y },
        unlocked = layout.unlocked_cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Select(cell => new[] { cell.X, cell.Y }),
        placements = layout.placements.OrderBy(p => p.cell.Y).ThenBy(p => p.cell.X).Select(p =>
            new { p.kind, p.source_key, x = p.cell.X, y = p.cell.Y, p.quarter_turns, p.rewrite_direction, p.locked, p.expended })
    });
    public static List<string> Validate(Character actor, CombatBoardLayout layout, bool combination = false)
    {
        var catalog = Catalog(actor);
        var normalized = combination ? layout.CopyConfiguration()
            : CombatBoardUnlockRules.Normalize(layout, actor.cultivation_realm, catalog.FindSkill);
        var size = GetDraft(actor).size;
        var issues = CombatBoardGeometry.Validate(normalized, catalog.FindSkill, key => catalog.FindSkill(key) is not null,
            key => catalog.FindRewrite(key)?.max_placements ?? 0, combination ? null : RequiresIndicator(actor), actor.get_inventory_total_count)
            .Select(issue => issue.Message).ToList();
        if (!combination && normalized.size != size) issues.Add("方案与当前已开放的盘面不兼容。");
        if (normalized.placements.Any(item => item.expended)) issues.Add("请先移出空瓶或符痕，再按真实库存配置。");
        return issues.Distinct().ToList();
    }
    public static bool TryApply(Character actor, CombatBoardLayout draft, out string reason)
    {
        reason = "请在人物页配置默认开局。";
        if (!CanManage(actor)) return false;
        var copy = CombatBoardUnlockRules.Normalize(draft, actor.cultivation_realm, Catalog(actor).FindSkill);
        var issues = Validate(actor, copy); if (issues.Count > 0) { reason = string.Join("\n", issues); return false; }
        copy.confirmed = true;
        var named = GetLibrary(actor).layouts.FirstOrDefault(p => p.preset_id == copy.preset_id && Signature(p) == Signature(copy));
        if (named is null) { copy.preset_id = ""; copy.Title = "当前配招"; }
        ((HumanData)actor.entity_data!).combat_board_preparation = copy; Changed(actor); reason = ""; return true;
    }
    public static bool TrySave(Character actor, CombatBoardLayout layout, string name, bool combination, bool overwrite,
        out string id, out string reason)
    {
        id = ""; reason = "请在人物页保存方案。"; name = name.Trim();
        if (!CanManage(actor)) return false;
        if (name.Length == 0) { reason = "请填写名称。"; return false; }
        var savedLayout = combination ? layout.CopyConfiguration()
            : CombatBoardUnlockRules.Normalize(layout, actor.cultivation_realm, Catalog(actor).FindSkill);
        var issues = Validate(actor, savedLayout, combination); if (issues.Count > 0) { reason = string.Join("\n", issues); return false; }
        var library = GetLibrary(actor); var items = combination ? library.combinations : library.layouts;
        var previous = items.FirstOrDefault(item => item.Title == name);
        if (previous is not null && !overwrite) { reason = "已有同名记录，请确认覆盖。"; return false; }
        var saved = savedLayout; saved.Title = name; saved.confirmed = true;
        saved.preset_id = id = previous?.preset_id ?? Guid.NewGuid().ToString("N");
        if (previous is not null) items[items.IndexOf(previous)] = saved; else items.Add(saved);
        ((HumanData)actor.entity_data!).combat_board_presets = library; Changed(actor); reason = ""; return true;
    }
    public static bool TryRename(Character actor, string id, string name, bool combination, out string reason)
    {
        reason = "请在人物页改名。"; name = name.Trim(); if (!CanManage(actor)) return false;
        if (name.Length == 0) { reason = "请填写名称。"; return false; }
        var library = GetLibrary(actor); var items = combination ? library.combinations : library.layouts;
        var record = items.FirstOrDefault(item => item.preset_id == id);
        if (record is null) { reason = "未找到此记录。"; return false; }
        if (items.Any(item => item.preset_id != id && item.Title == name)) { reason = "已有同名记录，请换一个名称。"; return false; }
        record.Title = name;
        var human = (HumanData)actor.entity_data!;
        if (!combination && human.combat_board_preparation is { } applied && applied.preset_id == id)
        { var copy = applied.CopyConfiguration(); copy.Title = name; human.combat_board_preparation = copy; }
        human.combat_board_presets = library; Changed(actor); reason = ""; return true;
    }
    public static bool TryDeleteCombination(Character actor, string id, out string reason)
    {
        reason = "请在人物页管理组合。"; if (!CanManage(actor)) return false;
        var library = GetLibrary(actor); var record = library.combinations.FirstOrDefault(item => item.preset_id == id);
        if (record is null) { reason = "未找到此组合。"; return false; }
        library.combinations.Remove(record); ((HumanData)actor.entity_data!).combat_board_presets = library;
        Changed(actor); reason = ""; return true;
    }
    public static CombatBoardPlacement? CreatePlacement(CombatBoardCatalog catalog, string key, Vector2I cell, int turns = 0)
    {
        var result = new CombatBoardPlacement { placement_id = Guid.NewGuid().ToString("N"), source_key = key, cell = cell };
        if (catalog.FindSkill(key) is { } skill) result.quarter_turns = skill.board_rotation_allowed ? turns % 2 : 0;
        else if (catalog.FindRewrite(key) is not null) { result.kind = CombatBoardPlacement.ObjectKind.Rewrite; result.rewrite_direction = turns % 4; }
        else if (BattleSupplyRules.Load(key) is { } pill) { result.kind = CombatBoardPlacement.ObjectKind.Pill; result.quarter_turns = pill.inventory_can_rotate ? turns % 2 : 0; }
        else if (TalismanCombatCatalog.Load(key) is { } talisman) { result.kind = CombatBoardPlacement.ObjectKind.Talisman; result.quarter_turns = talisman.inventory_can_rotate ? turns % 2 : 0; }
        else if (key == "heart:xuan_shu") { result.kind = CombatBoardPlacement.ObjectKind.ZhouTianIndicator; result.locked = true; result.rewrite_direction = turns % 4; }
        else return null;
        return result;
    }
    private static void Changed(Character actor) => ((HumanData)actor.entity_data!).EmitChanged();
}

/// <summary>人物灵台成长坐标与旧盘迁移；不影响生灵固定盘和单招定义。</summary>
public static class CombatBoardUnlockRules
{
    private static readonly Vector2I[] InitialCells = Enumerable.Range(0, 4)
        .SelectMany(y => Enumerable.Range(0, 2).Select(x => new Vector2I(x, y))).ToArray();

    public static int CloudLayer(string realm) => realm switch
    {
        "云息一层" => 1, "云息二层" => 2, "云息三层" => 3, "云息四层" => 4,
        "云息五层" => 5, "云息六层" => 6, "云息七层" => 7, "云息八层" => 8,
        "云息九层" => 9, "云息十层" => 10, _ => 0
    };

    public static Vector2I SizeForRealm(string realm)
    {
        var cells = CellsForRealm(realm);
        return new(cells.Max(cell => cell.X) + 1, cells.Max(cell => cell.Y) + 1);
    }

    public static Vector2I[] CellsForRealm(string realm)
    {
        var cells = new HashSet<Vector2I>(InitialCells);
        var layer = CloudLayer(realm);
        if (layer >= 1) cells.Add(new(2, 0));
        if (layer >= 2) cells.Add(new(2, 2));
        if (layer >= 4) { cells.Add(new(0, 4)); }
        if (layer >= 5) cells.Add(new(1, 4));
        if (layer >= 7) cells.Add(new(2, 1));
        if (layer >= 9) cells.Add(new(2, 3));
        return cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
    }

    public static CombatBoardLayout CreatePreview(IEnumerable<string> legacyKeys,
        IEnumerable<KeyValuePair<string, string>> legacyBindings, Func<string, ActiveSkillData?> resolve,
        bool requiresZhouTianIndicator, string realm)
    {
        var cells = CellsForRealm(realm);
        return CombatBoardMigration.CreatePreview(legacyKeys, legacyBindings, resolve, requiresZhouTianIndicator,
            SizeForRealm(realm), cells);
    }

    public static CombatBoardLayout Normalize(CombatBoardLayout source, string realm,
        Func<string, ActiveSkillData?> resolve)
    {
        var cells = CellsForRealm(realm);
        var size = SizeForRealm(realm);
        var sameUnlocks = source.unlocked_cells.Count > 0 && source.size == size
            && source.unlocked_cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).SequenceEqual(cells);
        if (source.layout_version == CombatBoardLayout.CurrentLayoutVersion && sameUnlocks)
            return source.CopyConfiguration();

        var result = new CombatBoardLayout
        {
            layout_version = CombatBoardLayout.CurrentLayoutVersion,
            size = size,
            unlocked_cells = new(cells),
            confirmed = source.confirmed,
            migrated_from_legacy = source.migrated_from_legacy || source.layout_version < CombatBoardLayout.CurrentLayoutVersion,
            preset_id = source.preset_id,
            preset_name = source.Title,
            pending_entries = new(source.pending_entries.Select(item => new CombatBoardPendingEntry
            {
                source_key = item.source_key, original_target_key = item.original_target_key,
                legacy_slot_index = item.legacy_slot_index, reason = item.reason
            })),
            legacy_skill_keys = source.legacy_skill_keys.Duplicate(),
            legacy_rewrite_bindings = source.legacy_rewrite_bindings.Duplicate()
        };

        var occupied = new HashSet<Vector2I>();
        var ordered = source.placements.OrderBy(item => item.kind == CombatBoardPlacement.ObjectKind.Rewrite ? 1 : 0)
            .ThenBy(item => item.cell.Y).ThenBy(item => item.cell.X).ToArray();
        var pendingIndex = 0;
        foreach (var original in ordered)
        {
            var item = original.CopyConfiguration();
            if (!TryPlace(item, item.cell, result.size, cells, occupied, resolve))
            {
                var placed = false;
                var turns = AllowedTurns(item, resolve);
                foreach (var cell in cells)
                {
                    foreach (var turn in turns)
                    {
                        item.quarter_turns = turn;
                        if (item.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator)
                            item.rewrite_direction = turn;
                        if (!TryPlace(item, cell, result.size, cells, occupied, resolve)) continue;
                        placed = true; break;
                    }
                    if (placed) break;
                }
                if (!placed)
                {
                    result.pending_entries.Add(new CombatBoardPendingEntry
                    {
                        source_key = item.source_key, legacy_slot_index = pendingIndex++,
                        reason = "云息灵台当前已解锁格位放不下，请调整后再使用。"
                    });
                    continue;
                }
            }
            result.placements.Add(item);
        }
        foreach (var rewrite in result.placements.Where(item => item.kind == CombatBoardPlacement.ObjectKind.Rewrite).ToArray())
            if (CombatBoardGeometry.PointedSkill(result, rewrite, resolve) is null)
            {
                result.placements.Remove(rewrite);
                result.pending_entries.Add(new CombatBoardPendingEntry
                { source_key = rewrite.source_key, reason = "迁移后化法未指向招式，请在灵台中重新连接。" });
            }
        if (result.pending_entries.Count > 0) result.confirmed = false;
        return result;
    }

    private static int[] AllowedTurns(CombatBoardPlacement item, Func<string, ActiveSkillData?> resolve) => item.kind switch
    {
        CombatBoardPlacement.ObjectKind.Skill when resolve(item.source_key) is { board_rotation_allowed: true } => [0, 1],
        CombatBoardPlacement.ObjectKind.Pill when BattleSupplyRules.Load(item.source_key) is { inventory_can_rotate: true } => [0, 1],
        CombatBoardPlacement.ObjectKind.Talisman when TalismanCombatCatalog.Load(item.source_key) is { } talisman
            && talisman.inventory_can_rotate => [0, 1],
        CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator => [0, 1, 2, 3],
        _ => [0]
    };

    private static bool TryPlace(CombatBoardPlacement item, Vector2I origin, Vector2I size,
        IReadOnlyCollection<Vector2I> unlocked, ISet<Vector2I> occupied, Func<string, ActiveSkillData?> resolve)
    {
        item.cell = origin;
        var footprint = CombatBoardGeometry.Footprint(item, resolve);
        var cells = CombatBoardGeometry.Cells(item, footprint).ToArray();
        if (!CombatBoardGeometry.Fits(size, origin, footprint, occupied) || cells.Any(cell => !unlocked.Contains(cell))) return false;
        foreach (var cell in cells) occupied.Add(cell);
        return true;
    }
}
