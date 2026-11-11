using Godot;

/// <summary>符工物品的显示素材；不参与冻结能力、产物身份或制作结算。</summary>
public static class TalismanArtCatalog
{
    public const string PaintedRoot = "res://textures/ui/talisman_crafting_painted_20261109/";
    private const string LegacyCraftRoot = "res://textures/ui/talisman_crafting_20261108/";
    private const string LegacyTalismanRoot = "res://textures/ui/talisman_crafting_20261109/";

    public static string PaperScenePath() => Preferred("scene_paper.png", LegacyCraftRoot + "paper.svg");

    public static string IconPath(TalismanCraftingService.ProductSnapshot snapshot) => snapshot.Kind switch
    {
        "paper" => Preferred("paper.png", LegacyCraftRoot + "paper.svg"),
        "ink" => Preferred("ink.png", LegacyCraftRoot + "ink.svg"),
        "brush" => Preferred(snapshot.BrushId switch
        {
            "jinghao" => "brush_jinghao.png",
            "cangfeng" => "brush_cangfeng.png",
            _ => "brush_qingzhu.png"
        }, LegacyCraftRoot + "brush.svg"),
        "talisman" => TalismanIconPath(snapshot.RecipeId),
        _ => LegacyCraftRoot + "drawing.svg"
    };

    public static string TalismanIconPath(string recipeId)
    {
        string name = recipeId == "yanshi" ? "yanshi" : "hushen";
        return Preferred(name + ".png", LegacyTalismanRoot + name + ".svg");
    }

    /// <summary>已有物品仅派生更新图标，不改能力JSON、路径、库存或品阶。</summary>
    public static void ApplyIcon(TalismanProductItemData item, TalismanCraftingService.ProductSnapshot snapshot)
    {
        string path = IconPath(snapshot);
        if (item.icon_path != path) item.icon_path = path;
    }

    private static string Preferred(string filename, string fallback)
    {
        string path = PaintedRoot + filename;
        return ResourceLoader.Exists(path) ? path : fallback;
    }
}
