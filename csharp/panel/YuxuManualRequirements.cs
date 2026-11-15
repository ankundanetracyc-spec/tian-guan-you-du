using Godot;

/// <summary>读取原有资源及实际角色数值，展示入门／逐卷条件；不生成新的修习规则。</summary>
public static class YuxuManualRequirements
{
    public sealed record Row(bool Met, string Text, string Current);
    public static List<Row> For(Character reader, GongfaResource book, GongfaChapterConfig? chapter, bool includeRealm)
    {
        var rows = new List<Row>();
        var rootText = book.wuxing == GongfaResource.Wuxing.NONE ? "灵根：不限" :
            book.wuxing == GongfaResource.Wuxing.ALL ? "具有灵根" :
            string.Join(book.wuxing_requirement_mode == GongfaResource.WuxingRequirementMode.AND ? "、" : "或",
                book.wuxing.ToString().Split('_').Select(root => root switch { "METAL" => "金", "WOOD" => "木", "WATER" => "水", "FIRE" => "火", "EARTH" => "土", _ => root })) + "灵根";
        rows.Add(new(book.is_wuxing_compatible(reader.spirit_root_type), rootText, ""));
        var realms = reader.IsInsideTree() ? reader.GetNodeOrNull<RealmManager>("/root/RealmManager") : null;
        bool AtLeast(string realm) => realms?.is_realm_after_or_equal(reader.cultivation_realm, realm) ?? reader.cultivation_realm == realm;
        foreach (var realm in new[] { book.required_realm, chapter?.required_realm ?? "" }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            if (includeRealm || realm != "凡人") rows.Add(new(AtLeast(realm), "境界 ≥ " + realm, "当前 " + reader.cultivation_realm));
        var attributes = new Dictionary<string, float>();
        void Merge(Godot.Collections.Dictionary values)
        {
            foreach (var key in values.Keys)
            {
                var name = key.AsString(); var value = values[key].AsSingle();
                attributes[name] = attributes.TryGetValue(name, out var existing) ? Math.Max(existing, value) : value;
            }
        }
        Merge(book.required_attributes); if (chapter is not null) Merge(chapter.required_attributes);
        foreach (var (key, required) in attributes)
        {
            var current = CardRules.Number(reader, key);
            rows.Add(new(current >= required, $"{Name(key)} ≥ {required:0.##}", $"当前 {current:0.##}"));
        }
        if (chapter is not null)
        {
            foreach (var id in chapter.required_chapter_ids)
            {
                var prior = book.get_chapter_by_id(id);
                bool met = reader.is_gongfa_chapter_learned(book, id);
                rows.Add(new(met, "已研习" + (prior?.chapter_name ?? "未标注卷章"), met ? "已研习" : "未研习"));
            }
            if (chapter.requires_main_slot)
            {
                bool main = reader.main_gongfa?.ResourcePath == book.ResourcePath;
                rows.Add(new(main, "需主修本秘籍", main ? "已设为主修" : "未设为主修"));
            }
        }
        return rows;
    }
    private static string Name(string key) => key switch
    {
        "physique" => "体魄", "intelligence" => "灵慧", "soul" => "神魂", "body_proficiency" => "体道造诣",
        "magic_proficiency" => "术法造诣", "sword_proficiency" => "剑道造诣", "formation_proficiency" => "阵法造诣",
        "alchemy_proficiency" => "丹道造诣", "forging_proficiency" => "器道造诣", _ => key
    };
}
