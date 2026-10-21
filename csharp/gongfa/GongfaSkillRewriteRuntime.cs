using Godot;
using Godot.Collections;

/// <summary>将已学化法应用于基础技能库或同一本秘籍授予的技能，不修改共享 Resource。</summary>
public static class GongfaSkillRewriteRuntime
{
    private sealed class SkillEntry(ActiveSkillData skill, string path)
    {
        public ActiveSkillData Skill { get; set; } = skill;
        public string Path { get; set; } = path;
        public HashSet<string> SourcePaths { get; } = string.IsNullOrWhiteSpace(path) ? [] : [path];
    }

    private sealed record RewriteEffectInfo(string Book, string Skill, string Description, string Preview);

    private static readonly System.Collections.Generic.Dictionary<string, RewriteEffectInfo> RewriteEffects = new()
    {
        ["xuan_shu_c1_jingzhe"] = new("玄枢含章经", "惊蛰", "化法追加：本次攻击无视目标护盾。", "本次攻击无视目标护盾"),
        ["xuan_shu_c2_yunpei"] = new("玄枢含章经", "云帔", "化法追加：由技能槽10自动施展时，为当前护盾值最低的一名其他存活队友施加自身本次获得护盾值一半的护盾；并列时随机选择。", "技能槽10自动施展时，为最低护盾队友施加一半护盾"),
        ["xuan_shu_c3_jiazhao"] = new("玄枢含章经", "架招", "化法追加：本次来袭的全部伤害段结算后，按架招实际抵消的物理伤害总点数获得等量护盾；未用于抵消伤害的化解不计入。", "本次来袭结算后，按实际抵消的物理伤害获得护盾"),
        ["xuan_shu_c4_fuming"] = new("玄枢含章经", "复命", "化法追加：复制的技能本次施加护盾后，为当前护盾值最低的一名其他存活队友施加等量护盾；并列时随机选择。", "复制技能施加护盾后，为最低护盾队友施加等量护盾"),
        ["zhuo_ji_c1_zhaoye"] = new("斫基损契玄经", "折锋", "化法追加：施展前法力为0时，命中后获得炁1；每回合最多触发一次。", "法力为0时命中，获得炁1；每回合一次"),
        ["zhuo_ji_c2_jianzhu"] = new("斫基损契玄经", "焚元", "化法追加：本回合下一项仍需支付法力的技能，法力费用改为等量体力费用。", "本回合下一项法力技能改为体力支付"),
        ["zhuo_ji_c3_jiazhao"] = new("斫基损契玄经", "架招", "化法追加：本次架招抵消至少1点物理伤害后，攻击者失去 B 点护盾。B = min(攻击者当前护盾值, 本次架招实际抵消的物理伤害总点数)。", "架招化解伤害后削减攻击者护盾"),
        ["zhuo_ji_c4_zhaoye"] = new("斫基损契玄经", "折锋", "化法追加：法力上限为1时，目标不能以应对技能应对此次攻击。", "法力上限为1时，此次攻击不可应对"),
        ["baihai_c1_cuixue"] = new("百骸衍枝化生宝册", "催血", "化法追加：施展前自身气血层数不高于3层时，额外获得气血1层。", "气血不高于3层时，催血额外获得气血1层"),
        ["baihai_c2_cuishan"] = new("百骸衍枝化生宝册", "摧山", "化法追加：至少一只手臂显化时，本技能额外追加一段与原攻击等量的物理伤害。", "显化手臂时，摧山追加一段同等物理伤害"),
        ["baihai_c3_jiazhao"] = new("百骸衍枝化生宝册", "架招", "化法追加：第二只手臂显化时，本次架招额外获得B层化解。B = min(round((3 + floor(体魄÷4) + floor(min(体魄×0.25, 神魂×0.20)))×0.20), 20)。", "双臂显化时，架招额外获得化解"),
        ["baihai_c4_jiazhao"] = new("百骸衍枝化生宝册", "架招", "化法追加：显化至少一只手臂时，本次架招额外获得化解1层；双臂显化时，再额外获得B层化解。本次架招实际抵消至少1点物理伤害后，若自身气血未满，获得气血1层。", "显臂强化架招；化解成功后可回复气血"),
        ["taihe_c3_jiazhao"] = new("太和感物养元宝册", "架招", "化法追加：应对触发时，若自身至少有1层气血，消耗1层并使本次架招额外获得A层化解；心法仍按本次应对结算气血获得。A=3 + floor(体魄÷4) + floor(min(体魄×0.25,神魂×0.20))。", "有气血时消耗1层，使架招化解翻倍"),
        ["taihe_c4_koushan"] = new("太和感物养元宝册", "叩山", "化法追加：每段由《叩山》追加的攻击命中后，独立判定B；判定通过时使目标获得C层僵直。B=clamp(15% + 施加者体魄×1% − 目标体魄×0.75% + 卡牌境界修正 + 高境界修正, 0%, 99%)×跨境界削减倍率；C=floor(clamp(60 + (施加者体魄−目标体魄)×3, 0, 120)×跨境界削减倍率)。", "叩山追加攻击命中后概率施加僵直"),
        ["zhuanxi_c4_cuixue"] = new("胎息经", "催血", "若本场曾由自身主动使气血下降，本技能结算时获得身法1层。", "催血后，曾主动回落则获得身法1层"),
        ["zhuanxi_c2_pingxi"] = new("胎息经", "平息", "本技能使气血下降后，额外获得身法1层。", "平息气血回落后额外获得身法1层"),
        ["chengwu_c1_cuixue"] = new("乘物游骸真经", "催血", "施放前若自身身法不少于10层且当前没有游骸，额外获得游骸1层。", "身法≥10且无游骸时，催血额外得游骸1层"),
        ["chengwu_c2_fajin"] = new("乘物游骸真经", "发劲", "施放时若自身拥有游骸，消耗1层并计入本场已消耗游骸，本次伤害提高50%。", "有游骸时消耗1层，发劲伤害提高50%"),
        ["chengwu_c3_pingxi"] = new("乘物游骸真经", "平息", "本技能使气血下降后，获得身法1层。", "气血下降后获得身法1层"),
        ["chengwu_c4_fajin"] = new("乘物游骸真经", "发劲", "本场已消耗游骸不少于1层时，对同一目标额外结算1段物理伤害；该段伤害 = round(4 + 体魄×0.35 + 体道造诣×0.15)。", "本场耗过游骸后，发劲追加1段物理伤害"),
        ["buxu_c3_pingxi"] = new("补虚行血诀", "平息", "若本技能使气血由1层降至0层，获得身法1层。", "气血由1降至0时获得身法1层"),
        ["buxu_c4_cuixue"] = new("补虚行血诀", "催血", "若本场已触发补虚行血，本技能结算后获得身法1层。", "触发补虚行血后催血，额外获得身法1层"),
        ["buxu_c4_guiyuan"] = new("补虚行血诀", "归元", "本技能由气血0层起身后，额外获得身法1层。", "气血为0时归元，额外获得身法1层"),
        ["lingshu_c3_pingxi"] = new("灵枢经筋篇", "平息", "可额外消耗内伤或外伤1层（优先内伤）；若如此做，获得借力1层。", "优先消耗1层内伤，否则外伤；成功则获得借力1层"),
        ["neijing_c2_xieli"] = new("内景损益篇", "卸力", "该次攻击结算后，若自身生命实际减少，获得外伤2层。", "受击实际失去生命后，额外获得外伤2层"),
        ["yinfu_c3_xieli"] = new("阴符应物经", "卸力", "若本次化解实际抵消至少1点物理伤害，获得借力1层。", "化解实际抵消物理伤害后，获得借力1层"),
        ["chiyang_c1_pingxi"] = new("赤阳参同经", "平息", "施放时自动消耗当前血炉中至多10点的偶数点；每消耗2点回复最大生命×1%。", "至多耗10点偶数血炉，每2点回复最大生命1%"),
        ["chiyang_c2_xieli"] = new("赤阳参同经", "卸力", "应对触发时自动消耗当前血炉中至多20点的偶数点；每消耗2点，本次化解增加1层。", "应对时至多耗20点偶数血炉，每2点增加1层化解"),
        ["chiyang_c3_fajin"] = new("赤阳参同经", "发劲", "施放时自动消耗当前血炉中至多10点；辅修每点实际消耗使本招伤害倍率提高1%，主修提高1.5%。", "至多消耗10点血炉，按主辅修转为本招伤害倍率"),
        ["neijing_c4_tongji"] = new("内景损益篇", "痛击", "每层当前内伤或外伤使本次伤害倍率额外提高2%，最多提高16%。", "每层伤势额外增伤2%，最多16%"),
        ["lingshu_c4_yinjing"] = new("灵枢经筋篇", "引筋", "本次施展前消耗内伤或外伤1层（优先内伤）；成功消耗时本次伤害倍率提高20%。", "施展前消耗1层伤势；本次伤害提高20%"),
        ["chiyang_c4_zhuming"] = new("赤阳参同经", "朱明", "血炉至少20点时可用并消耗全部血炉；倍率 = 1 + 实际消耗血炉×1%。血炉利用的主修强化由赤阳心法统一结算。", "需至少20点血炉；朱明清炉并按实际消耗提高伤害倍率"),
        ["zhuanxi_c1_fanzhao"] = new("胎息经", "返照", "若本次主动气血回落后气血降至0层，额外获得身法1层。", "气血回落至0时额外获得身法1层"),
        ["zhuanxi_c3_zhouxing"] = new("胎息经", "周行", "本场主动降低气血至少2次时，本技能额外获得身法1层。", "本场主动降低气血至少2次，周行额外获得身法1层"),
        ["buxu_c1_huanyang"] = new("补虚行血诀", "还阳", "施放前气血为0层时，获得气血后额外回复最大生命×3%。", "空血施展还阳，额外回复最大生命3%"),
        ["buxu_c2_xieli"] = new("补虚行血诀", "卸力", "自身气血为0层时，卸力实际触发后，本次化解额外增加1层。", "气血为0时应对，额外获得1层化解"),
        ["neijing_c1_cuili"] = new("内景损益篇", "淬厉", "结算后若当前内伤不少于2层，额外获得借力1层。", "淬厉结算后内伤不少于2层，获得借力1层"),
        ["neijing_c3_fajin"] = new("内景损益篇", "发劲", "每2层当前内伤与外伤使本次伤害倍率额外提高5%，最多提高20%。", "每2层伤势额外增伤5%，最多20%"),
        ["lingshu_c1_qugu"] = new("灵枢经筋篇", "去故", "本次消耗至少2层伤势时，额外获得身法1层。", "消耗至少2层伤势后，额外获得身法1层"),
        ["lingshu_c2_cuixue"] = new("灵枢经筋篇", "催血", "施放前若自身有伤势，自动消耗1层（优先内伤）；成功消耗时回复最大生命×3%。", "催血前有伤势则消耗1层并回复最大生命3%"),
        ["yinfu_c1_chengji"] = new("阴符应物经", "承机", "触发时现有借力每满2层，使本次化解额外增加1层，最多增加2层。", "触发时每2层借力增加1层化解，最多2层"),
        ["yinfu_c2_xieshi"] = new("阴符应物经", "挟势", "每层借力在原有增伤之外，再使本次伤害倍率提高2%。", "每层借力额外提高挟势伤害倍率2%"),
        ["yinfu_c4_hejin"] = new("阴符应物经", "合劲", "施放时若借力不少于3层，本次伤害倍率额外提高10%。", "借力不少于3层时，合劲额外增伤10%")
    };

    public static bool HasEffect(ActiveSkillData skill, string effectId) => skill.gongfa_rewrite_effect_ids.Contains(effectId);

    public static string GetEffectPreview(ActiveSkillData skill) => string.Join("；", skill.gongfa_rewrite_effect_ids
        .Where(RewriteEffects.ContainsKey)
        .Distinct()
        .Select(id => RewriteEffects[id].Preview));

    /// <summary>返回当前技能牌面主文案以外的其他化法效果，用于多本秘籍改写同一招式时完整展示。</summary>
    public static IEnumerable<string> GetAdditionalEffectDescriptions(ActiveSkillData skill, bool excludePrimary = true)
    {
        var represented = GetPrimaryEffectId(skill.source_gongfa_name, skill.skill_name);
        return skill.gongfa_rewrite_effect_ids
            .Where(id => (!excludePrimary || id != represented) && RewriteEffects.ContainsKey(id))
            .Distinct()
            .Select(id => RewriteEffects[id].Description);
    }

    public static Array<string> ApplyBasicSkillRewrites(Character human, Array<string> source)
    {
        var result = new Array<string>();
        foreach (var entry in BuildBasicSkillEntries(human, source)) result.Add(entry.Path);
        return result;
    }

    /// <summary>返回含合并化法效果的运行时副本，避免两槽改写同一技能时后者覆盖前者。</summary>
    public static Array<ActiveSkillData> ApplyBasicSkillRewriteSkills(Character human, Array<string> source)
    {
        var result = new Array<ActiveSkillData>();
        foreach (var entry in BuildBasicSkillEntries(human, source)) result.Add(entry.Skill);
        return result;
    }

    public static Array<ActiveSkillData> ApplyBasicSkillRewriteSkills(Character human, Array<string> source, out Array<string> paths)
    {
        var entries = BuildBasicSkillEntries(human, source);
        var result = new Array<ActiveSkillData>();
        paths = [];
        foreach (var entry in entries)
        {
            result.Add(entry.Skill);
            paths.Add(entry.Path);
        }
        return result;
    }

    public static Array<ActiveSkillData> ApplyBookSkillRewrites(Character human, GongfaResource book, bool isMain)
    {
        var entries = BuildBookSkillEntries(human, book, isMain);
        return new Array<ActiveSkillData>(entries.Select(entry => entry.Skill));
    }

    /// <summary>将存档中的原技能路径解析为当前化法版本。</summary>
    public static string ResolveBookSkillPath(Character human, GongfaResource book, bool isMain, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        var entries = BuildBookSkillEntries(human, book, isMain);
        foreach (var entry in entries)
            if (entry.SourcePaths.Contains(path)) return entry.Path;
        return path;
    }

    public static bool TryResolveBasicSkillPath(Character human, Array<string> source, string path, out string resolved)
    {
        foreach (var entry in BuildBasicSkillEntries(human, source))
        {
            if (!entry.SourcePaths.Contains(path) && entry.Path != path) continue;
            resolved = entry.Path;
            return true;
        }
        resolved = path;
        return false;
    }

    private static List<SkillEntry> BuildBasicSkillEntries(Character human, Array<string> source)
    {
        var entries = new List<SkillEntry>();
        foreach (var path in source)
        {
            var skill = ResourceLoader.Load<ActiveSkillData>(path);
            if (skill is not null) entries.Add(new SkillEntry(skill, path));
        }

        foreach (var (book, isMain) in GetBooks(human))
        foreach (var chapter in book.get_sorted_chapters())
        {
            if (!IsRewriteChapterActive(human, book, chapter, isMain)) continue;
            foreach (var rewrite in chapter.get_deck_rewrites(isMain))
            {
                if (!rewrite.enabled || rewrite.target_scope != GongfaDeckRewriteConfig.RewriteTargetScope.BASIC_SKILL ||
                    rewrite.get_result_skill(isMain) is not ActiveSkillData replacement || string.IsNullOrWhiteSpace(replacement.ResourcePath)) continue;

                var count = Mathf.Max(1, rewrite.count);
                if (rewrite.mode == GongfaDeckRewriteConfig.RewriteMode.ADD)
                {
                    for (var i = 0; i < count; i++) entries.Add(new SkillEntry(CopyWithMergedEffects(replacement, null), replacement.ResourcePath));
                    continue;
                }

                var changed = 0;
                for (var i = 0; i < entries.Count && changed < count; i++)
                {
                    var entry = entries[i];
                    if (!rewrite.matches(entry.Skill)) continue;
                    entry.Skill = CopyWithMergedEffects(replacement, entry.Skill);
                    entry.SourcePaths.Add(replacement.ResourcePath);
                    entry.Path = replacement.ResourcePath;
                    changed++;
                }
            }
        }
        return entries;
    }

    private static List<SkillEntry> BuildBookSkillEntries(Character human, GongfaResource book, bool isMain)
    {
        var entries = new List<SkillEntry>();
        void Add(Resource? resource)
        {
            if (resource is not ActiveSkillData skill || string.IsNullOrWhiteSpace(skill.ResourcePath) ||
                entries.Any(entry => entry.Path == skill.ResourcePath)) return;
            entries.Add(new SkillEntry(skill, skill.ResourcePath));
        }

        foreach (var skill in book.granted_active_skills) Add(skill);
        foreach (var chapter in book.get_sorted_chapters())
        {
            if (!human.is_gongfa_chapter_learned(book, chapter.get_id())) continue;
            foreach (var skill in chapter.get_learned_skills(isMain)) Add(skill);
        }

        foreach (var chapter in book.get_sorted_chapters())
        {
            if (!IsRewriteChapterActive(human, book, chapter, isMain)) continue;
            foreach (var rewrite in chapter.get_deck_rewrites(isMain))
            {
                if (!rewrite.enabled || rewrite.target_scope != GongfaDeckRewriteConfig.RewriteTargetScope.OWN_GONGFA_SKILL ||
                    rewrite.get_result_skill(isMain) is not ActiveSkillData replacement || string.IsNullOrWhiteSpace(replacement.ResourcePath)) continue;

                if (rewrite.mode == GongfaDeckRewriteConfig.RewriteMode.ADD)
                {
                    if (!entries.Any(entry => entry.Path == replacement.ResourcePath))
                        entries.Add(new SkillEntry(CopyWithMergedEffects(replacement, null), replacement.ResourcePath));
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (!rewrite.matches(entry.Skill)) continue;
                    entry.Skill = CopyWithMergedEffects(replacement, entry.Skill);
                    entry.SourcePaths.Add(replacement.ResourcePath);
                    entry.Path = replacement.ResourcePath;
                }
            }
        }
        return entries;
    }

    private static ActiveSkillData CopyWithMergedEffects(ActiveSkillData replacement, ActiveSkillData? previous)
    {
        var copy = replacement.Duplicate() as ActiveSkillData ?? replacement;
        copy.combat_source_key = replacement.ResourcePath;
        var effects = new Array<string>();
        if (previous is not null)
            foreach (var id in previous.gongfa_rewrite_effect_ids)
                if (!effects.Contains(id)) effects.Add(id);
        foreach (var id in replacement.gongfa_rewrite_effect_ids)
            if (!effects.Contains(id)) effects.Add(id);
        copy.gongfa_rewrite_effect_ids = effects;

        var tags = new Array<string>();
        if (previous is not null)
            foreach (var tag in previous.tags)
                if (!tags.Contains(tag)) tags.Add(tag);
        foreach (var tag in replacement.tags)
            if (!tags.Contains(tag)) tags.Add(tag);
        copy.tags = tags;
        return copy;
    }

    private static string? GetPrimaryEffectId(string book, string skill) => RewriteEffects
        .Where(pair => pair.Value.Book == book && pair.Value.Skill == skill)
        .Select(pair => pair.Key)
        .FirstOrDefault();

    private static bool IsRewriteChapterActive(Character human, GongfaResource book, GongfaChapterConfig chapter, bool isMain)
    {
        if (!human.is_gongfa_chapter_learned(book, chapter.get_id())) return false;
        return isMain || human.get_sub_gongfa_chapter_route(book, chapter.get_id()) == "right";
    }

    private static IEnumerable<(GongfaResource book, bool isMain)> GetBooks(Character human)
    {
        if (human.main_gongfa is not null) yield return (human.main_gongfa, true);
        foreach (var book in human.sub_gongfa_list)
            if (book is not null) yield return (book, false);
    }
}
