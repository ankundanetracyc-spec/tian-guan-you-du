using Godot;
using System.Reflection;

public partial class ForgingWorkspaceSmoke : Node
{
    private int _checks;
    private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private static object? Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden)!.Invoke(target, args);
    private async System.Threading.Tasks.Task Frames() { for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng("res://docs/美术规范/炼器界面改版_20261024/" + name + ".png") == Error.Ok, "截图 " + name);
    }
    public override async void _Ready()
    {
        try
        {
            var model = new ProcessForgingTrial(); var draft = model.ExampleDraft(1); draft.Proficiency = 60;
            var demand = model.SpiritDemand(draft);
            Check(demand > 0 && model.SpiritDemand(new()) == 0, "已投才计需求");
            Check(model.Inspect(draft, null, demand + 1, 1).CanSubmit, "需求等于可用允许开炼");
            Check(!model.Inspect(draft, null, demand + 1, 2).CanSubmit && !model.Inspect(draft, null, 0).CanSubmit, "扣其他占用及零容量拒绝");
            var loads = model.SpiritLoads(draft); Check(demand == (int)Math.Ceiling(Math.Round(loads.Sum(load => load.Total), 8)), "逐材合计仅最终取整");
            var large = ProcessForgingTrial.Clone(draft); foreach (var input in large.Inputs) input.Quantity = 8;
            Check(model.Inspect(large, null, 100000).Units > 32 && model.Inspect(large, null, 100000).CanSubmit, "大于32份以灵识限制而非固定总量拒绝");
            var session = new ProcessForgingTrial.Session(model.Parameters.Profiles.Keys); int stock = session.Stock.Values.Sum();
            Check(!session.Submit(model, draft, 0, 0).CanSubmit && session.Stock.Values.Sum() == stock && session.Hours == 0, "超灵识不扣料不推进工时");
            foreach (var example in Enumerable.Range(0, model.Parameters.Examples.Count))
            {
                var sample = model.ExampleDraft(example); var evaluated = model.Evaluate(sample);
                if (!evaluated.Success) continue;
                Check(model.MaterialRelations(sample).Count(line => line.Contains("调和相克")) == evaluated.Product!.Calculation.Ledger.Count(line => line.Contains("支路通关使用")), "通关展示与真实逐份分配一致 " + example);
            }
            var scene = new ProcessForgingScene(); AddChild(scene); Call(scene, "LoadExample", 1); await Frames();
            var buttons = (Dictionary<string, Button>)typeof(ProcessForgingScene).GetField("_buttons", Hidden)!.GetValue(scene)!;
            var plus = buttons.First(pair => pair.Key.StartsWith("quantity-plus-主体-")).Value;
            var before = ((ProcessForgingTrial.Draft)typeof(ProcessForgingScene).GetField("_draft", Hidden)!.GetValue(scene)!).Inputs.Single(input => input.Role == "主体").Quantity;
            plus.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(((ProcessForgingTrial.Draft)typeof(ProcessForgingScene).GetField("_draft", Hidden)!.GetValue(scene)!).Inputs.Single(input => input.Role == "主体").Quantity == before + 1, "加号只增加当前岗位数量");
            Call(scene, "LoadExample", 1); await Capture("01_投料");
            Call(scene, "Picker", "性材", null!);
            var overlay = (Control)typeof(ProcessForgingScene).GetField("_overlay", Hidden)!.GetValue(scene)!;
            var selectors = overlay.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().ToArray();
            selectors[0].Select(2); selectors[0].EmitSignal(OptionButton.SignalName.ItemSelected, 2); await Frames();
            Check(overlay.FindChildren("*", "Button", true, false).OfType<Button>().Where(button => button.Icon is not null).All(button => button.Text.Contains("木")), "五行筛选只保留匹配材料");
            selectors[0].Select(0); selectors[0].EmitSignal(OptionButton.SignalName.ItemSelected, 0);
            await Capture("02_选材"); Call(scene, "CloseOverlay");
            var zeroOwner = new Character(); AddChild(zeroOwner); zeroOwner.max_spirit_perception = 0; scene.BindOwner(zeroOwner); Call(scene, "Refresh"); await Frames();
            buttons = (Dictionary<string, Button>)typeof(ProcessForgingScene).GetField("_buttons", Hidden)!.GetValue(scene)!;
            Check(buttons["craft"].Disabled, "实际角色零灵识禁用开炼");
            GD.Print("FORGING_WORKSPACE_OK " + _checks); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
