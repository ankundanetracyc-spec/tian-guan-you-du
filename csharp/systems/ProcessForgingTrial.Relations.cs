public sealed partial class ProcessForgingTrial
{
    public IReadOnlyList<string> MaterialRelations(Draft draft)
    {
        var inputs = Normalize(draft.Inputs).Where(input => _facts.ContainsKey(input.Material))
            .OrderBy(input => input.Role, StringComparer.Ordinal).ThenBy(input => _facts[input.Material].DefinitionId, StringComparer.Ordinal).ThenBy(input => input.Material, StringComparer.Ordinal).ToList();
        var roots = inputs.Where(input => input.Role == "质材").ToList();
        if (roots.Count == 0) roots = inputs.Where(input => input.Role == "主体").ToList();
        if (roots.Count == 0) return ["先放入主体，再查看材料之间的关系。"];
        var pool = inputs.Where(input => input.Role == "和材").SelectMany(input => Enumerable.Repeat(input.Material, Math.Max(0, input.Quantity))).ToList();
        var lines = new List<string> { "当前参照：" + string.Join("、", roots.Select(root => root.Material + "（" + _facts[root.Material].Element + "）")) };
        var same = new List<string>(); const string cycle = "木火土金水";
        foreach (var input in inputs.Where(input => input.Role != "和材"))
        foreach (var root in roots.Where(root => root != input))
        {
            var r = _facts[root.Material]; var s = _facts[input.Material];
            Relation(r.Element, s.Element, out var bridge);
            int a = r.Element.Length == 1 ? cycle.IndexOf(r.Element, StringComparison.Ordinal) : -1;
            int b = s.Element.Length == 1 ? cycle.IndexOf(s.Element, StringComparison.Ordinal) : -1;
            var label = a < 0 || b < 0 ? "无直接生克" : a == b ? "同属" : (a + 1) % 5 == b ? r.Element + "生" + s.Element :
                (b + 1) % 5 == a ? s.Element + "生" + r.Element : (a + 2) % 5 == b ? r.Element + "克" + s.Element : s.Element + "克" + r.Element;
            if (label == "同属") { same.Add(input.Material + "（" + input.Role.Replace("复合身材", "辅体") + "）"); continue; }
            var text = input.Material + "（" + input.Role.Replace("复合身材", "辅体") + "） ／ " + root.Material + "\n" + label;
            if (input.Role == "性材" && bridge.Length > 0)
            {
                var proposed = new List<string>(pool); var picked = new List<string>(); var missing = new List<string>();
                foreach (var element in bridge)
                {
                    var index = proposed.FindIndex(name => _facts[name].Element == element);
                    if (index < 0) missing.Add(element); else { picked.Add(proposed[index]); proposed.RemoveAt(index); }
                }
                if (missing.Count == 0) { pool = proposed; text += " · 调和相克：" + string.Join("、", picked); }
                else text += " · 尚未调和 · 所需和材：" + string.Join("、", missing.Select(element => element + "行和材1份"));
            }
            lines.Add(text);
        }
        if (same.Count > 0) lines.Add("与参照同属：" + string.Join("、", same.Distinct()));
        if (pool.Count > 0) lines.Add("其余和材：" + string.Join("、", pool.GroupBy(name => name).Select(group => group.Key + "×" + group.Count())) + "；仍参与炼制。");
        return lines;
    }
}
