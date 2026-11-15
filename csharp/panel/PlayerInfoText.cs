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
    private static readonly HashSet<string> ProseFields = new(StringComparer.Ordinal)
    {
        "效果", "化法追加", "追加效果", "原技能效果", "基础效果", "主动效果", "被动效果",
        "装备效果", "手持效果", "御使效果", "技能效果", "心法效果", "特殊效果", "化法", "技能",
        "药效", "功用", "作用", "简介", "介绍", "说明", "特性", "特效",
    };
    private static readonly HashSet<string> CompactFields = new(StringComparer.Ordinal)
    {
        "类型", "关键词", "消耗", "耗费", "费用", "目标", "使用次数", "次数", "冷却", "持续",
        "整局限制", "每回合", "出手方式", "品阶", "丹阶", "丹效", "器类", "器蕴", "锋锐", "灵识占用",
    };
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
        var (body, formulas) = SeparateCalculations(original);
        var result = CleanSummary(body);
        return formulas.Count == 0 ? result : result + "\n\n计算来源\n" + string.Join("\n\n", formulas);
    }

    /// <summary>Explicit source formulas, kept separate so a structured card adds one final calculation block.</summary>
    public static IReadOnlyList<string> CalculationSources(string original) => SeparateCalculations(original).Formulas;

    private static (string Body, List<string> Formulas) SeparateCalculations(string original)
    {
        var formulas = new List<string>();
        if (string.IsNullOrWhiteSpace(original)) return ("", formulas);
        var footer = Regex.Match(original, @"(?:^|\n\n)计算来源\r?\n");
        var existing = footer.Success ? original[(footer.Index + footer.Length)..] : "";
        var body = footer.Success ? original[..footer.Index] : original;
        body = Regex.Replace(body, @"(?:计算公式|计算方式|计算过程)\s*[：:](?<formula>[^。；\r\n]*)(?:。|；)?", match =>
        {
            if (!string.IsNullOrWhiteSpace(match.Groups["formula"].Value)) formulas.Add(ReadableFormula(match.Groups["formula"].Value));
            return "";
        });
        // Some existing callers pass an already formatted explanation. Only
        // arithmetic parentheses are moved; actual conditions remain in place.
        for (var start = 0; start < body.Length; start++)
        {
            if (body[start] is not ('(' or '（')) continue;
            var depth = 1;
            var end = start + 1;
            for (; end < body.Length && depth > 0; end++)
            {
                if (body[end] is '(' or '（') depth++;
                else if (body[end] is ')' or '）') depth--;
            }
            if (depth != 0) continue;
            var inner = body[(start + 1)..(end - 1)];
            if (inner.Contains('。') || Regex.IsMatch(inner, @"造成|获得|回复|消耗|触发|命中后")
                || !Regex.IsMatch(inner, @"[=＝≈]\s*[-+]?\d") || !Regex.IsMatch(inner, @"[+＋−\-×÷*/%]|\[[\d.]+\]")) continue;
            formulas.Add(ReadableFormula(inner));
            body = body[..start] + body[end..];
            start--;
        }
        var lines = body.Split('\n');
        body = string.Join("\n", lines.Where(line =>
        {
            var plain = Regex.Replace(line, @"\[/?(?:b|i|u|s|color|font|font_size|bgcolor|fgcolor|url)(?:=[^\]]*)?\]", "");
            if (!Regex.IsMatch(plain, @"^[^。]*[=＝≈]")) return true;
            formulas.Add(ReadableFormula(line));
            return false;
        }));
        formulas.AddRange(existing.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).Select(ReadableFormula));
        return (body, formulas.Distinct(StringComparer.Ordinal).ToList());
    }
    private static string ReadableFormula(string formula)
    {
        var readable = Regex.Replace(formula.Trim(),
            @"\b(max|min|round|floor|ceil|clamp|abs)\s*\(", match => (match.Groups[1].Value.ToLowerInvariant() switch
            { "max" => "max", "min" => "min", "round" or "floor" or "ceil" => "", "clamp" => "限定范围", _ => "绝对值" }) + "(", RegexOptions.IgnoreCase);
        readable = Regex.Replace(readable, @"取(?:较|最)大值\s*(?=[(（])", "max");
        readable = Regex.Replace(readable, @"取(?:较|最)小值\s*(?=[(（])", "min");
        // Keep arithmetic grouping and the actual result; rounding is an internal
        // calculation rule rather than another explanation in the hover formula.
        readable = Regex.Replace(readable, @"(?:四舍五入|向下取整|向上取整)\s*(?=[(（])", "");
        var name = Regex.Match(readable, @"^(?<label>[^：:()（）\[\]，,=＝≈]+)[：:](?<formula>.+)$");
        var prefix = name.Success ? name.Groups["label"].Value + "：" : "";
        var expression = name.Success ? name.Groups["formula"].Value.Trim() : readable;
        while (expression.Length > 1 && expression[0] is '(' or '（')
        {
            var depth = 0; var closesAtEnd = false;
            for (var index = 0; index < expression.Length; index++)
            {
                if (expression[index] is '(' or '（') depth++;
                else if (expression[index] is ')' or '）') depth--;
                if (depth != 0) continue;
                closesAtEnd = index == expression.Length - 1;
                break;
            }
            if (!closesAtEnd) break;
            expression = expression[1..^1].Trim();
        }
        // Accept the previous result-first presentation when reading saved or cached text.
        var previous = Regex.Match(expression, @"^(?<result>.+?)[，,]\s*计算\s*[：:]\s*(?<calculation>.+)$");
        if (previous.Success)
            return CalculationLine(prefix, previous.Groups["result"].Value, previous.Groups["calculation"].Value);
        // A canonical line must survive mode switches and repeated formatting unchanged.
        var canonical = Regex.Match(expression,
            @"^(?<result>[-+]?\d+(?:\.\d+)?%?(?:\s*[\u4e00-\u9fff]+)?|[A-Z](?:\d+)?|[\u4e00-\u9fff]{1,32})\s*[=＝]\s*(?<calculation>.+)$");
        if (canonical.Success)
            return CalculationLine(prefix, canonical.Groups["result"].Value, canonical.Groups["calculation"].Value);
        var equality = expression.LastIndexOfAny(['=', '＝', '≈']);
        if (equality < 0) return readable;
        var result = expression[(equality + 1)..].Trim();
        var calculation = expression[..equality].Trim();
        // An intermediate displayed subtotal is an annotation, not another
        // operand. Keep its expression and omit the approximate subtotal.
        calculation = Regex.Replace(calculation, @"≈(?:[\u4e00-\u9fff]+)?[-+]?\d+(?:\.\d+)?%?", "");
        return CalculationLine(prefix, result, calculation);
    }

    private static string CalculationLine(string prefix, string result, string calculation) =>
        prefix + result.Trim() + " = "
        + Regex.Replace(calculation.Trim(), @"≈(?:[\u4e00-\u9fff]+)?[-+]?\d+(?:\.\d+)?%?", "");

    /// <summary>Keep prose under its heading without adding blank lines to compact data fields.</summary>
    public static string LayoutLongParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var inCalculations = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var plain = Regex.Replace(lines[index], @"\[/?(?:b|i|u|s|color|font|font_size|bgcolor|fgcolor|url)(?:=[^\]]*)?\]", "");
            if (plain.Trim() == "计算来源") inCalculations = true;
            if (inCalculations) continue;
            var field = Regex.Match(lines[index],
                @"^(?<prefix>(?:\[(?:b|i|u|s|color|font|font_size|bgcolor|fgcolor)(?:=[^\]]*)?\])*)(?<label>[^：:\[\]\r\n]{1,16})[：:](?<suffix>(?:\[/(?:b|i|u|s|color|font|font_size|bgcolor|fgcolor)\])*)[ \t]*(?<body>\S.*)$");
            if (!field.Success) continue;
            var label = field.Groups["label"].Value.Trim();
            var body = field.Groups["body"].Value;
            if (CompactFields.Contains(label)
                || !(ProseFields.Contains(label) || body.Length >= 40 || body.Contains('。') || body.Contains('；'))) continue;
            lines[index] = field.Groups["prefix"].Value + label + "：" + field.Groups["suffix"].Value + "\n" + body;
        }
        return string.Join("\n", lines);
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
        return CleanSummary(SeparateCalculations(text).Body);
    }

    private static string CleanSummary(string text)
    {
        text = text.Replace("预估", "");
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
            !Regex.IsMatch(line, @"(?:实例\s*ID|资源路径|绑定基础牌|快照|兼容字段|运行时字段)\s*[：:]"));
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
