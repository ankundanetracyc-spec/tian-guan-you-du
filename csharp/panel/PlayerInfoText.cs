using System;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using System.Collections.Generic;

/// <summary>Presentation-only boundary: the original explanation remains available in the rules reader.</summary>
public static class PlayerInfoText
{
    public enum InformationMode { Simple, Detailed }
    private static InformationMode _mode = InformationMode.Detailed;
    private static bool _preferenceLoaded;
    private sealed record TextBinding(WeakReference<Control> Target, string Original);
    private static readonly List<TextBinding> Bindings = new();
    public static event Action? ModeChanged;
    public static InformationMode Mode
    {
        get
        {
            if (!_preferenceLoaded)
            {
                _preferenceLoaded = true;
                var config = new ConfigFile();
                if (config.Load(GameSettingsPanel.SettingsPath) == Error.Ok)
                    _mode = config.GetValue("interface", "information_detail", 1).AsInt32() == 0 ? InformationMode.Simple : InformationMode.Detailed;
            }
            return _mode;
        }
    }
    public static void SetMode(InformationMode mode, bool persist = true)
    {
        _preferenceLoaded = true; _mode = mode;
        if (persist)
        {
            var config = new ConfigFile(); config.Load(GameSettingsPanel.SettingsPath);
            config.SetValue("interface", "information_detail", (int)mode);
            if (config.Save(GameSettingsPanel.SettingsPath) != Error.Ok) GD.PushWarning("信息显示设置未能保存。");
        }
        for (int i = Bindings.Count - 1; i >= 0; i--)
        {
            var binding = Bindings[i];
            if (!binding.Target.TryGetTarget(out var target) || !GodotObject.IsInstanceValid(target)) { Bindings.RemoveAt(i); continue; }
            var text = Display(binding.Original);
            if (string.IsNullOrWhiteSpace(text) && HasDetails(binding.Original)) text = "规则详解";
            if (target is Label label) label.Text = text;
            else if (target is RichTextLabel rich) rich.Text = text;
        }
        ModeChanged?.Invoke();
    }
    private static void Track(Control control, string original)
    {
        for (int i = Bindings.Count - 1; i >= 0; i--)
        {
            if (!Bindings[i].Target.TryGetTarget(out var target) || !GodotObject.IsInstanceValid(target)) { Bindings.RemoveAt(i); continue; }
            if (target == control) { Bindings[i] = new(new(control), original); return; }
        }
        if (HasDetails(original)) Bindings.Add(new(new(control), original));
    }
    public static string Display(string original) => Mode == InformationMode.Simple ? Summary(original) : Detailed(original);
    public static string Detailed(string original)
    {
        if (string.IsNullOrWhiteSpace(original)) return "";
        var formulas = new List<string>();
        var protectedText = Regex.Replace(original,
            @"(?<before>。?)[ \t]*(?:计算公式|计算方式|计算过程)\s*[：:](?<formula>[^。；\r\n]+)(?<end>。?)",
            match =>
            {
                int index = formulas.Count;
                formulas.Add(ReadableFormula(match.Groups["formula"].Value));
                return "（§" + index + "§）" + (match.Groups["before"].Length > 0 || match.Groups["end"].Length > 0 ? "。" : "");
            });
        var lines = protectedText.Split('\n').ToList();
        var equations = lines.Where(line => Regex.IsMatch(line, @"^[^。]*[=＝]")).ToArray();
        if (equations.Length > 0)
        {
            lines.RemoveAll(line => equations.Contains(line));
            int resultLine = lines.FindLastIndex(line => Regex.IsMatch(line, @"当前(?:值|倍率|[：:])"));
            int index = formulas.Count; formulas.Add(ReadableFormula(string.Join("；", equations)));
            if (resultLine >= 0) lines[resultLine] += "（§" + index + "§）";
            else lines.Add("（§" + index + "§）");
            protectedText = string.Join("\n", lines);
        }
        var result = Summary(protectedText);
        for (int i = 0; i < formulas.Count; i++) result = result.Replace("§" + i + "§", formulas[i]);
        return result;
    }
    private static string ReadableFormula(string formula)
    {
        var readable = Regex.Replace(formula.Trim(),
            @"\b(max|min|round|floor|ceil|clamp|abs)\s*\(", match => (match.Groups[1].Value switch
            { "max" => "取较大值", "min" => "取较小值", "round" or "floor" or "ceil" => "", "clamp" => "限定范围", _ => "绝对值" }) + "(");
        // Keep arithmetic grouping and the actual result; rounding is an internal
        // calculation rule rather than another explanation in the hover formula.
        return Regex.Replace(readable, @"(?:四舍五入|向下取整|向上取整)\s*(?=[(（])", "");
    }
    public static bool HasDetails(string original) =>
        Regex.Replace(Summary(original), @"\s+", "") != Regex.Replace(original, @"\s+", "");
    public static void Bind(Label label, string original)
    {
        label.SetMeta("player_information_source_bound", true);
        Track(label, original);
        label.Text = Display(original);
        if (HasDetails(original))
        {
            if (string.IsNullOrWhiteSpace(label.Text)) label.Text = "规则详解";
            label.MouseFilter = Control.MouseFilterEnum.Pass;
            HoverInfoManager.SetInfo(label, new HoverInfoManager.HoverInfoContent("", original));
        }
        else HoverInfoManager.SetTooltip(label, "");
    }
    public static void Bind(RichTextLabel label, string original)
    {
        label.SetMeta("player_information_source_bound", true);
        Track(label, original);
        label.Text = Display(original);
        if (HasDetails(original))
        {
            if (string.IsNullOrWhiteSpace(label.Text)) label.Text = "规则详解";
            label.MouseFilter = Control.MouseFilterEnum.Pass;
            HoverInfoManager.SetInfo(label, new HoverInfoManager.HoverInfoContent("", original));
        }
        else HoverInfoManager.SetTooltip(label, "");
    }
    public static string Summary(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var detailStart = text.IndexOf("\n\n规则详解\n", StringComparison.Ordinal);
        if (detailStart >= 0) text = text[..detailStart];
        // Chinese sentence delimiters deliberately exclude decimal points in numeric formulas.
        text = Regex.Replace(text, @"(?:计算公式|计算方式|计算过程)\s*[：:][^。；\r\n]*(?:。|；)?", "");
        var lines = text.Split('\n').Where(line =>
            !line.Contains("只计当前已生效的自身增伤") &&
            !line.Contains("未计施放时触发的额外效果") &&
            !line.Contains("这些在命中时结算") &&
            !line.Contains("命中、会心与目标防御在攻击时结算") &&
            !line.Contains("瓷瓶仅作图标与演出外观") &&
            !line.Contains("同类丹药合并为一格") &&
            !Regex.IsMatch(line, @"(?:实例\s*ID|资源路径|绑定基础牌|快照|兼容字段|运行时字段)\s*[：:]") &&
            !Regex.IsMatch(line, @"^[^。]*[=＝]"));
        text = string.Join("\n", lines);
        // Old resource descriptions sometimes embed a symbolic assignment after the effect.
        text = Regex.Replace(text, @"(?<![A-Za-z])[A-Z]\s*=\s*[^。；\r\n]*(?:。|；)?", "");
        // A legacy bare expression must never leak into the main card, even without a label.
        text = RemoveFunctionExpressions(text);
        text = Regex.Replace(text, @"\n[ \t]*\n(?:[ \t]*\n)+", "\n\n");
        return text.Trim();
    }

    private static string RemoveFunctionExpressions(string text)
    {
        var match = Regex.Match(text, @"\b(?:max|min|round|floor|ceil|clamp|abs)\s*\(", RegexOptions.IgnoreCase);
        while (match.Success)
        {
            int cursor = match.Index + match.Length, depth = 1;
            for (; cursor < text.Length && depth > 0; cursor++)
            {
                if (text[cursor] == '(') depth++;
                else if (text[cursor] == ')') depth--;
            }
            text = text[..match.Index] + "随属性变化" + text[cursor..];
            match = Regex.Match(text, @"\b(?:max|min|round|floor|ceil|clamp|abs)\s*\(", RegexOptions.IgnoreCase);
        }
        return text;
    }
}
