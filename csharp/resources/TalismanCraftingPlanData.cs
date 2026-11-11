using Godot;

/// <summary>人物持久配伍草稿，只保存选择，不保存报价或结算结果。</summary>
public partial class TalismanCraftingPlanData : Resource
{
    [Export] public int version = TalismanCraftingPlans.Version;
    [Export] public string id = "";
    [Export] public string plan_name = "";
    [Export] public string mode = "paper";
    [Export(PropertyHint.MultilineText)] public string request_json = "";
}
