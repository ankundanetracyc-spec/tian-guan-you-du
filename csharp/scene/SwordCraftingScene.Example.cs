using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class SwordCraftingScene
{
    private const string ExampleFolder = "res://docs/美术规范/炼器完整样例_20261022";
    private void LoadCompleteExample()
    {
        CloseModal();
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://resources/crafting/sword_trial_examples.json"));
        var example = json.RootElement[0];
        string sampleName = SwordCraftTrial.S(example, "sample_name");
        var sample = _model.Parameters.GetProperty("samples").EnumerateArray().Single(x => SwordCraftTrial.S(x, "name") == sampleName);
        _form = SwordCraftTrial.S(sample, "form");
        var draft = _model.Create(_form);
        draft.Proficiency = SwordCraftTrial.N(sample, "proficiency");
        foreach (var input in draft.Inputs.Where(x => x.Group == "身材"))
        {
            input.Material = _model.Choices(draft, input)[0];
            if (sample.TryGetProperty("extras", out var extras) && extras.TryGetProperty(input.Key, out var value)) input.Quantity += value.GetInt32();
        }
        var root = draft.Inputs.Single(x => x.Key == "root"); root.Material = SwordCraftTrial.S(sample, "root"); root.Quantity = (int)SwordCraftTrial.N(sample, "root_quantity");
        foreach (var source in sample.GetProperty("sources").EnumerateObject())
        {
            var input = draft.Inputs.Single(x => x.Key == source.Name); input.Material = source.Value[0].GetString()!; input.Quantity = source.Value[1].GetInt32();
        }
        foreach (var method in sample.GetProperty("methods").EnumerateArray()) draft.Methods.Add(method.GetString()!);
        if (sample.TryGetProperty("utility_priorities", out var priorities))
            draft.Primary = priorities.EnumerateObject().Single(x => x.Value.GetString() == "主用").Name;
        int index = 0;
        foreach (var a in example.GetProperty("auxiliary").EnumerateArray())
        {
            string key = "aux" + index++;
            var input = draft.Inputs.Single(x => x.Key == key); input.Material = a[0].GetString()!; input.Quantity = a[1].GetInt32();
        }
        foreach (var fuel in example.GetProperty("fuel").EnumerateObject())
        {
            var input = draft.Inputs.Single(x => x.Key == fuel.Name); input.Material = fuel.Name; input.Quantity = fuel.Value.GetInt32();
        }
        foreach (var d in _drafts.Values) d.Proficiency = draft.Proficiency;
        _drafts[_form] = draft; _methods = false; _selected = draft.Inputs[0].Key;
        if (_inputScroll is not null) _inputScroll.ScrollVertical = 0;
        _scrolls[_form] = 0;
        Refresh();
        _notice.Text = "旧模型留档：厚脊双功 · 本例预选功用与器形，不代表材料与过程量驱动的新规则";
    }
    private async Task CaptureExample(string filename)
    {
        Input.WarpMouse(new Vector2(1660, 940)); HoverInfoManager.HideAll();
        await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
        HoverInfoManager.HideAll();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ExampleFolder);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(ProjectSettings.GlobalizePath(ExampleFolder + "/" + filename));
    }
    private async void RunCompleteExample()
    {
        try
        {
            LoadCompleteExample();
            var result = _model.Evaluate(Draft);
            void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException("FORGING_EXAMPLE_FAIL " + label); }
            Require(result.Valid && StockIssues(result).Count == 0, string.Join("、", result.Issues));
            Require(result.Load == 10 && result.Capacity == 12 && result.Heat == 22 && result.OfferedHeat == 24, "结构与供热");
            Require(result.Hours == 11 && result.Energy == 22 && result.Materials.Values.Sum() == 17, "实际用度");
            Require(_model.DescribeEffect("破障化法", result.Effects["破障化法"]).Contains("12%") && _model.DescribeEffect("固结御使", result.Effects["固结御使"]).Contains("5 护盾"), "候选效果");
            Require(result.BodyHp == 20 && result.Spirit == 2, "共用器体");
            await CaptureExample("01_投料与开炼条件.png");
            _methods = true; Refresh(); await CaptureExample("02_功用与方法.png");
            _methods = false; Refresh();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _inputScroll!.ScrollVertical = (int)_inputScroll.GetVScrollBar().MaxValue;
            await CaptureExample("03_性材和材与燃料.png");
            _inputScroll.ScrollVertical = 0;
            int ironBefore = _stock["玄铁"], craftedBefore = _crafted;
            _content.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "开炼 · 试炼").EmitSignal(BaseButton.SignalName.Pressed);
            Require(_crafted == craftedBefore + 1 && _stock["玄铁"] == ironBefore - 6 && Math.Abs(Draft.Proficiency - 12.1) < 1e-6, "实际开炼扣料与造诣");
            Craft(); Require(_crafted == craftedBefore + 1, "重复提交");
            await CaptureExample("04_炼成效果.png");
            foreach (var pair in result.Materials) Require(_stock[pair.Key] == 30 - pair.Value, "材料记账 " + pair.Key);
            using var report = Godot.FileAccess.Open(ExampleFolder + "/运行结算.json", Godot.FileAccess.ModeFlags.Write);
            report.StoreString(JsonSerializer.Serialize(new { sample = "厚脊双功", load = result.Load, capacity = result.Capacity, materials = result.Materials, remaining_stock = result.Materials.Keys.ToDictionary(k => k, k => _stock[k]), hours = result.Hours, energy = result.Energy, polarity = SwordCraftTrial.Polarities[result.Polarity + 3], effects = result.Effects.ToDictionary(x => x.Key, x => _model.DescribeEffect(x.Key, x.Value)), body_hp = result.BodyHp, spirit = result.Spirit, proficiency_after = Draft.Proficiency }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            GD.Print("FORGING_COMPLETE_EXAMPLE_OK"); Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); Input.SetCustomMouseCursor(null); GetTree().Quit(1); }
    }
}
