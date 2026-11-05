using Godot;
using System.Runtime.CompilerServices;

/// <summary>反复发现材料、炼制与释放运行态，并在批次间强制完成托管资源回收。</summary>
public partial class ForgingResourceLifetimeSmoke : Node
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Batch(int pass, bool forge)
    {
        var model = new ProcessForgingTrial();
        var draft = model.ExampleDraft(0);
        foreach (var input in draft.Inputs) input.Quantity = 1;
        if (!model.Evaluate(draft).Success) throw new Exception("合法小炉不能成器");
        if (forge)
        {
            var actor = new Character { entity_data = new HumanData { base_forging_proficiency = draft.Proficiency,
                forging_proficiency = draft.Proficiency, current_world_stamina = 100,
                max_spirit_perception = 10000, max_health = 100, current_health = 100 } };
            try
            {
                var incoming = draft.Inputs.GroupBy(i => i.Material).ToDictionary(g => model.Materials[g.Key].ResourcePath, g => g.Sum(i => i.Quantity));
                if (!actor.try_exchange_inventory(new Dictionary<string, int>(), incoming)) throw new Exception("真实投料摆放失败");
                if (!actor.TryForge(model, draft, out var result) || !result.Success) throw new Exception("真实成器失败：" + string.Join("；", result.Errors));
                if (actor.get_spatial_inventory().Any(i => !i.IsPlaced)) throw new Exception("成器后有未摆放物品");
            }
            finally { actor.Free(); }
        }
        GD.Print($"FORGING_LIFETIME_BATCH {pass} materials={model.Materials.Count} forge={forge}");
    }
    public override async void _Ready()
    {
        try
        {
            bool forge = !OS.GetCmdlineUserArgs().Contains("--catalog-only");
            bool natural = OS.GetCmdlineUserArgs().Contains("--natural-gc");
            for (int pass = 0; pass < 40; pass++)
            {
                Batch(pass, forge);
                if (!natural) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            GD.Print("FORGING_RESOURCE_LIFETIME_OK 40"); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
