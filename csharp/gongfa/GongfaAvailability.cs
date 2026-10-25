using Godot;

/// <summary>秘籍、专属技能和秘籍物品共用的开放范围，不删除资源或存档进度。</summary>
public static class GongfaAvailability
{
    public const string DefaultBookPath = "res://resources/gongfa/玄枢含章经.tres";
    private static readonly HashSet<string> CurrentBooks = new(StringComparer.Ordinal)
    {
        "玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册",
        "舒卷天章", "玉髓回澜养真宝册", "抱虚养晦通玄录"
    };
    private static List<GongfaResource>? _books;
    private static readonly HashSet<string> DisabledNames = new(StringComparer.Ordinal);
    private static readonly HashSet<string> DisabledSkills = new(StringComparer.Ordinal);
    private static readonly HashSet<string> EnabledSkills = new(StringComparer.Ordinal);
    private static readonly HashSet<string> DisabledSkillFolders = new(StringComparer.Ordinal);
    private static readonly List<Resource> CatalogDefinitions = [];

    public static bool IsEnabled(GongfaResource? book) => book?.gameplay_enabled == true && CurrentBooks.Contains(book.gongfa_name);
    public static bool IsBookNameEnabled(string name) { EnsureCatalog(); return _books!.Any(book=>book.gongfa_name==name&&IsEnabled(book)); }
    public static bool IsItemEnabled(ItemData? item) => item is not null && (item.gongfa is not GongfaResource book || IsEnabled(book));
    public static IEnumerable<GongfaResource> EnabledBooks() { EnsureCatalog(); return _books!.Where(IsEnabled); }
    public static string EnabledPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path)) return "";
        return IsEnabled(ResourceLoader.Load<GongfaResource>(path)) ? path : "";
    }
    public static string InitialPath(string? path) => EnabledPath(path) is { Length: > 0 } enabled ? enabled : DefaultBookPath;

    public static bool IsSkillEnabled(ActiveSkillData? skill)
    {
        if (skill is null) return false;
        EnsureCatalog();
        var path = string.IsNullOrWhiteSpace(skill.combat_source_key) ? skill.ResourcePath : skill.combat_source_key;
        // 共享招式只要仍由开放秘籍授予即可保留。
        if (EnabledSkills.Contains(path)) return true;
        return !DisabledNames.Contains(skill.source_gongfa_name) && IsSkillPathEnabled(path);
    }

    /// <summary>在加载技能定义前排除停用秘籍的整个目录，也覆盖未被卷章引用的旧变体。</summary>
    public static bool IsSkillPathEnabled(string path)
    {
        EnsureCatalog();
        if(EnabledSkills.Contains(path))return true;
        if(DisabledSkills.Contains(path))return false;
        const string prefix="res://resources/activeskills/";
        if(!path.StartsWith(prefix,StringComparison.Ordinal))return true;
        var relative=path[prefix.Length..];var separator=relative.IndexOf('/');
        return separator<0||!DisabledSkillFolders.Contains(relative[..separator]);
    }

    private static void EnsureCatalog()
    {
        if (_books is not null) return;
        _books = [];
        foreach (var file in DirAccess.GetFilesAt("res://resources/gongfa").OrderBy(file => file, StringComparer.Ordinal))
        {
            var name = file.EndsWith(".remap", StringComparison.Ordinal) ? file[..^6] : file;
            if (!name.EndsWith(".tres", StringComparison.Ordinal)) continue;
            var bookFileName=name[..^5];
            if(!CurrentBooks.Contains(bookFileName))
            {
                // 停用秘籍不预载其整棵旧技能资源；按目录拒绝，旧档按需读取原路径。
                DisabledNames.Add(bookFileName);DisabledSkillFolders.Add(bookFileName);
                if(bookFileName=="黄庭养形篇")
                {DisabledNames.Add("胎息经");DisabledSkillFolders.Add("胎息经");}
                continue;
            }
            if (ResourceLoader.Load<GongfaResource>("res://resources/gongfa/" + name) is not { } book) continue;
            _books.Add(book);
            if (!IsEnabled(book))
            {
                DisabledNames.Add(book.gongfa_name);
                DisabledSkillFolders.Add(book.gongfa_name);
                DisabledSkillFolders.Add(name[..^5]);
            }
            var paths = IsEnabled(book) ? EnabledSkills : DisabledSkills;
            void Add(ActiveSkillData? skill)
            {
                if(skill is null)return;
                CatalogDefinitions.Add(skill);
                if(skill.ResourcePath.Length>0)paths.Add(skill.ResourcePath);
            }
            Add(book.heart_method_skill); Add(book.main_heart_method_skill); Add(book.sub_heart_method_skill);
            foreach (var skill in book.granted_active_skills) Add(skill);
            foreach (var chapter in book.get_sorted_chapters())
            {
                CatalogDefinitions.Add(chapter);
                foreach (var skill in chapter.get_learned_skills(true).OfType<ActiveSkillData>()) Add(skill);
                foreach (var rewrite in chapter.get_deck_rewrites(true))
                { CatalogDefinitions.Add(rewrite);Add(rewrite.get_result_skill(true) as ActiveSkillData); Add(rewrite.get_result_skill(false) as ActiveSkillData); }
            }
        }
        if (ResourceLoader.Load<BasicDeckResource>("res://resources/basic_decks/公共基础技能池.tres") is { } pool)
            foreach (var path in pool.get_deck_skills()) EnabledSkills.Add(path);
    }
}
