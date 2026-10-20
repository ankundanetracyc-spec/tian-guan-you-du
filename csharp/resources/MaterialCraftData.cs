using Godot;
using Godot.Collections;

/// <summary>材料的静态性质与跨制作系统共用的功用等级。</summary>
public partial class MaterialCraftData : Resource
{
    [Export] public string element = "";
    [Export(PropertyHint.Range, "-3,3,1")] public int polarity;
    /// <summary>材料百科与通用词缀使用的主导加工类型。</summary>
    [Export] public string texture = "";
    /// <summary>兼容旧材类筛选与炼丹测试参数索引；不显示为当前材料分类。</summary>
    [Export] public string legacy_texture = "";
    [Export] public Array<MaterialUtilityGrade> utilities = [];

    public float material_score()
    {
        var grades = utilities.Where(utility => utility is not null && !string.IsNullOrWhiteSpace(utility.utility_id))
            .Select(utility => Mathf.Clamp(utility.grade, 1, 9)).OrderByDescending(grade => grade).ToList();
        if (grades.Count == 0) return 0f;
        var score = grades[0] * 2f;
        if (grades.Count > 1) score += grades[1] * .5f;
        for (var index = 2; index < grades.Count; index++) score += grades[index] * .25f;
        return score;
    }

    /// <summary>材评每满一分升一个材料小阶；0 分表示无品，最高为一品上。</summary>
    public int material_grade_value()
    {
        var score = material_score();
        return score <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(score), 1, 27);
    }

    public string material_grade_name() => ItemAffixCatalog.grade_to_string(material_grade_value());

    public string describe()
    {
        var values = utilities.Where(utility => utility is not null && !string.IsNullOrWhiteSpace(utility.utility_id))
            .Select(utility => $"{utility.utility_id}{utility.grade_name()}");
        var score = material_score();
        return $"功用：{(values.Any() ? string.Join("、", values) : "未定")}";
    }

    public static string utility_grade_name(int grade) => Mathf.Clamp(grade, 1, 9) switch
    {
        1 => "九品", 2 => "八品", 3 => "七品", 4 => "六品", 5 => "五品",
        6 => "四品", 7 => "三品", 8 => "二品", _ => "一品"
    };
}
