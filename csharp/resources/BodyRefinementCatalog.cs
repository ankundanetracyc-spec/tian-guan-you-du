using Godot;

/// <summary>首期已开放的淬体图录。学习来源后续接入，当前只负责提供可配置的基础法门。</summary>
public static class BodyRefinementCatalog
{
    public static readonly string[] DefaultPaths =
    [
        "res://resources/body_refinement/砺肤承息淬炼法.tres",
        "res://resources/body_refinement/百络引行淬炼法.tres",
        "res://resources/body_refinement/贯形淬炼法.tres",
        "res://resources/body_refinement/三息周流归藏淬炼法.tres",
        "res://resources/body_refinement/无声守脉淬炼法.tres"
    ];

    public static Godot.Collections.Array<BodyRefinementMethod> LoadDefaultMethods()
    {
        Godot.Collections.Array<BodyRefinementMethod> result = [];
        foreach (var path in DefaultPaths)
        {
            if (!ResourceLoader.Exists(path) || ResourceLoader.Load<BodyRefinementMethod>(path) is not { } method) continue;
            result.Add(method);
        }
        return result;
    }
}
