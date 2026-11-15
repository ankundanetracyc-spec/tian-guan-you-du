using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class SwordCraftingScene
{
    private async System.Threading.Tasks.Task CheckControls(Action<bool, string> check)
    {
        _form = "细身剑"; _methods = false; LoadReference();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _inputScroll!.ScrollVertical = 100;
        var plus = _inputRows["剑身"].FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "+");
        plus.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        check(Draft.Inputs[0].Quantity == 3 && _inputScroll!.ScrollVertical == 100, "投料按钮与滚动保留");
        var minus = _inputRows["剑身"].FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "−");
        minus.EmitSignal(BaseButton.SignalName.Pressed);
        _methods = true; Refresh();
        var toggle = _content.FindChildren("*", "CheckButton", true, false).OfType<CheckButton>().First(b => b.Text == "召出御使");
        toggle.ButtonPressed = true;
        check(Draft.Methods.Contains("破障御使") && !_model.Evaluate(Draft).Valid, "方法开关触发承载重算");
        toggle = _content.FindChildren("*", "CheckButton", true, false).OfType<CheckButton>().First(b => b.Text == "召出御使");
        toggle.ButtonPressed = false;
        check(_model.Evaluate(Draft).Valid && Draft.Inputs.First(x => x.Group == "和材").Material == "风纹石英", "删除方法保留共用材料");
        _content.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "厚脊剑").EmitSignal(BaseButton.SignalName.Pressed);
        check(Draft.Form == "厚脊剑" && Draft.Inputs.Count(x => x.Group == "身材") == 3, "器形按钮切换真实部件");
        _content.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "细身剑").EmitSignal(BaseButton.SignalName.Pressed);
        check(Draft.Inputs[0].Quantity == 2 && Draft.Inputs[0].Material == "玄铁", "器形按钮恢复旧草稿");
        _methods = false; _scrolls["细身剑"] = 0; if (_inputScroll is not null) _inputScroll.ScrollVertical = 0; Refresh();
    }
    private async System.Threading.Tasks.Task CapturePage(string file)
    {
        Input.WarpMouse(new Vector2(1660, 940));
        HoverInfoManager.HideAll();
        await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
        HoverInfoManager.HideAll();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        const string folder = "res://docs/美术规范/炼器工坊_20261021";
        DirAccess.MakeDirRecursiveAbsolute(folder);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(ProjectSettings.GlobalizePath(folder + "/" + file.Replace(".png", "_v3.png")));
    }
    private void CheckReferenceFixtures(Action<bool, string> check)
    {
        using var fixtures = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://tools/fixtures/sword_crafting_ui.json"));
        int index = 0;
        foreach (var entry in fixtures.RootElement.EnumerateArray())
        {
            index++;
            var config = entry.GetProperty("config"); var expected = entry.GetProperty("expected");
            string form = SwordCraftTrial.S(config, "form"); var d = _model.Create(form);
            d.Primary = "";
            if (config.TryGetProperty("utility_priorities", out var priorities))
                d.Primary = priorities.EnumerateObject().FirstOrDefault(p => p.Value.GetString() == "主用").Name ?? "";
            if (config.TryGetProperty("method_priorities", out priorities))
                foreach (var p in priorities.EnumerateObject()) d.Priorities[p.Name] = p.Value.GetString()!;
            d.Proficiency = SwordCraftTrial.N(config, "proficiency");
            foreach (var m in config.GetProperty("methods").EnumerateArray()) d.Methods.Add(m.GetString()!);
            foreach (var input in d.Inputs.Where(x => x.Group == "身材"))
            {
                input.Material = _model.Choices(d, input)[0];
                if (config.TryGetProperty("extras", out var extras) && extras.TryGetProperty(input.Key, out var extra)) input.Quantity += extra.GetInt32();
            }
            var root = d.Inputs.First(x => x.Key == "root"); root.Material = SwordCraftTrial.S(config, "root"); root.Quantity = (int)SwordCraftTrial.N(config, "root_quantity");
            foreach (var s in config.GetProperty("sources").EnumerateObject()) { var input = d.Inputs.First(x => x.Key == s.Name); input.Material = s.Value[0].GetString()!; input.Quantity = s.Value[1].GetInt32(); }
            int slot = 0;
            foreach (var a in entry.GetProperty("auxiliary").EnumerateObject()) { string key = "aux" + slot++; var input = d.Inputs.First(x => x.Key == key); input.Material = a.Name; input.Quantity = a.Value.GetInt32(); }
            if (expected.GetProperty("valid").GetBoolean())
                foreach (var fuel in expected.GetProperty("fuel").EnumerateObject()) { var input = d.Inputs.First(x => x.Key == fuel.Name); input.Material = fuel.Name; input.Quantity = fuel.Value.GetInt32(); }
            var r = _model.Evaluate(d);
            void Assert(bool condition, string field) => check(condition, $"试算对照{index}:{field} {string.Join("、", r.Issues)}");
            Assert(r.Valid == expected.GetProperty("valid").GetBoolean(), "合法性");
            if (!r.Valid) continue;
            Assert(r.Load == SwordCraftTrial.N(expected, "load") && r.Capacity == SwordCraftTrial.N(expected, "capacity"), "承载");
            Assert(r.Difficulty == SwordCraftTrial.N(expected, "difficulty"), "造诣门槛");
            Assert(r.Hours == SwordCraftTrial.N(expected, "hours") && r.Energy == SwordCraftTrial.N(expected, "energy"), "工时精力");
            Assert(r.Heat == SwordCraftTrial.N(expected, "heat"), "热量");
            Assert(SwordCraftTrial.Polarities[r.Polarity + 3] == SwordCraftTrial.S(expected, "polarity"), "阴阳");
            Assert(Math.Abs(r.Growth - SwordCraftTrial.N(expected, "proficiency_gain")) < 1e-6, "造诣成长");
            Assert(r.Spirit == SwordCraftTrial.N(expected, "spirit"), "灵识");
            if (r.Spirit > 0) Assert(r.BodyHp == SwordCraftTrial.N(expected, "body_hp"), "器体");
            foreach (var material in expected.GetProperty("materials").EnumerateObject())
                Assert(r.Materials[material.Name] == material.Value.GetInt32(), "投料合并");
            foreach (var m in expected.GetProperty("shares").EnumerateObject())
            {
                double factor = d.Methods.Count == 1 ? SwordCraftTrial.N(_model.Form(form), "single_method_factor") : 1;
                if (SwordCraftTrial.S(_model.Method(m.Name), "kind") is "direct_attack" or "drive_attack") factor *= SwordCraftTrial.N(_model.Form(form), "physical_active_factor");
                Assert(Math.Abs(r.Effects[m.Name] - m.Value.GetDouble() * factor) < 1e-6, "效力分配");
            }
        }
        GD.Print($"FORGING_REFERENCE_CASES {index}");
    }
}
