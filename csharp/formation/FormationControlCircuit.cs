using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public enum FormationTruth { Unknown, False, True }
public enum FormationSignalType { Event, Switch, Number }
public enum FormationValueUnit { None, Percent, Shield, Mana, Energy, Count }
public sealed record ControlStep(string NodeId, string Label, string Value, bool Passed);

/// <summary>有类型的信息。没有事件是空事件值，不能作为布尔假；未知数值不等于0。</summary>
public sealed record FormationControlValue
{
    public FormationSignalType Type { get; init; }
    public FormationValueUnit Unit { get; init; }
    public bool Known { get; init; } = true;
    public double Number { get; init; }
    public FormationTruth State { get; init; } = FormationTruth.Unknown;
    public string EventId { get; init; } = "";
    public string SourceTag { get; init; } = "";
    public int Amplitude { get; init; } = 1;
    public bool HasEvent => Type == FormationSignalType.Event && EventId.Length > 0 && Amplitude > 0;
    public static FormationControlValue Numeric(double value, FormationValueUnit unit) => new() { Type = FormationSignalType.Number, Unit = unit, Number = value };
    public static FormationControlValue Boolean(FormationTruth state) => new() { Type = FormationSignalType.Switch, State = state, Known = state != FormationTruth.Unknown };
    public static FormationControlValue Event(string id = "", string sourceTag = "", int amplitude = 1) => new() { Type = FormationSignalType.Event, EventId = id, SourceTag = sourceTag, Amplitude = amplitude };
    public static FormationControlValue Unknown(FormationSignalType type, FormationValueUnit unit = FormationValueUnit.None) => new() { Type = type, Unit = unit, Known = false };
    public string Display => !Known ? "未知" : Type switch
    {
        FormationSignalType.Switch => State == FormationTruth.True ? "开" : "关",
        FormationSignalType.Event => HasEvent ? "本次通过" : "本次无事件",
        _ => Number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + (Unit == FormationValueUnit.Percent ? "%" : "")
    };
}

/// <summary>状态、数值、事件共用的有向控制图。只生成明确请求，不在求值中消耗能源或产生效果。</summary>
public sealed class FormationControlCircuit
{
    public enum NodeKind { StateInput, EventInput, Compare, Not, And, Or, Split, Switch, GuardOutput, AttackOutput, EventFilter, EventCounter, EventMerge, ShieldOutput }
    public enum CompareMode { AtLeast, AtMost, Band }
    public sealed record Node(string Id, NodeKind Kind, string Label)
    {
        public string Key { get; init; } = "";
        public FormationSignalType SignalType { get; init; } = FormationSignalType.Number;
        public FormationValueUnit Unit { get; init; } = FormationValueUnit.None;
        public CompareMode Comparison { get; init; }
        public double Low { get; init; }
        public double High { get; init; }
        public bool InitiallyOn { get; init; } = true;
        public bool NormallyClosed { get; init; }
        public int Interval { get; init; } = 2;
        public bool FirstImmediate { get; init; }
        public int MinimumAmplitude { get; init; } = 1;
        public string SourceTag { get; init; } = "";
    }
    public sealed record Link(string From, string Output, string To, string Input);
    public sealed record EventFact(string Id, string Name, string SourceTag = "", int Amplitude = 1);
    public sealed record OutputRequest(string NodeId, NodeKind Kind, FormationControlValue Event);
    public sealed record Evaluation(IReadOnlyList<ControlStep> Steps,
        IReadOnlyDictionary<string, FormationControlValue> Values,
        IReadOnlyDictionary<string, FormationTruth> GuardRequests,
        IReadOnlyList<string> AttackRequests, bool Duplicate)
    {
        public IReadOnlyList<OutputRequest> PulseRequests { get; init; } = Array.Empty<OutputRequest>();
        public IReadOnlyDictionary<string, FormationTruth> SwitchPermits { get; init; } = new ReadOnlyDictionary<string, FormationTruth>(new Dictionary<string, FormationTruth>());
    }

    private readonly Dictionary<string, Node> _nodes;
    private readonly Dictionary<(string Node, string Port), Link> _inputs = new();
    private readonly Dictionary<string, bool> _memory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventFact> _seen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _counterPhases = new(StringComparer.Ordinal);
    private readonly string[] _order;
    public IReadOnlyDictionary<string, Node> Nodes { get; }
    public IReadOnlyList<Link> Links { get; }
    public IReadOnlyDictionary<string, int> CounterPhases => new ReadOnlyDictionary<string, int>(_counterPhases);
    public IReadOnlyList<string> EvaluationOrder => Array.AsReadOnly(_order);

    public FormationControlCircuit(IEnumerable<Node> nodes, IEnumerable<Link> links)
    {
        var parts = nodes.ToArray(); var wires = links.ToArray();
        if (parts.Length is < 1 or > 128 || parts.Any(n => n is null || !Id(n.Id) || !Enum.IsDefined(n.Kind)))
            throw new ArgumentException("控制图元件无效或超过128件。");
        _nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in parts)
        {
            if (!_nodes.TryAdd(node.Id, node)) throw new ArgumentException("控制元件标识重复。");
            if (!Enum.IsDefined(node.SignalType) || !Enum.IsDefined(node.Unit) || !Enum.IsDefined(node.Comparison) ||
                !double.IsFinite(node.Low) || !double.IsFinite(node.High)) throw new ArgumentException("控制元件参数无效。");
            if (node.Kind == NodeKind.StateInput && (!Id(node.Key) || node.SignalType == FormationSignalType.Event))
                throw new ArgumentException("状态输入须选择明确的数值或开关来源。");
            if ((node.Kind == NodeKind.StateInput && node.SignalType == FormationSignalType.Switch ||
                node.Kind == NodeKind.Split && node.SignalType != FormationSignalType.Number) && node.Unit != FormationValueUnit.None)
                throw new ArgumentException("事件与开关口不能附带数值单位。");
            if (node.Kind == NodeKind.EventInput && !Id(node.Key)) throw new ArgumentException("事件输入没有合法事件名。");
            if (node.SourceTag is null || node.SourceTag.Length > 128 || node.SourceTag.Any(char.IsControl))
                throw new ArgumentException("事件来源标签无效。");
            if (node.Kind == NodeKind.EventCounter && node.Interval is < 1 or > 12)
                throw new ArgumentException("计数器间隔须在1至12之间。");
            if (node.Kind is NodeKind.EventInput or NodeKind.EventFilter && node.MinimumAmplitude is < 1 or > 9)
                throw new ArgumentException("事件最小幅值须在1至9之间。");
            if ((node.Kind == NodeKind.StateInput && node.SignalType == FormationSignalType.Number || node.Kind == NodeKind.Compare ||
                node.Kind == NodeKind.Split && node.SignalType == FormationSignalType.Number) && node.Unit == FormationValueUnit.None)
                throw new ArgumentException("数值口必须有单位。");
            if (node.Kind == NodeKind.Compare && node.Comparison == CompareMode.Band && node.Low >= node.High)
                throw new ArgumentException("区间下限必须小于上限。");
        }
        var outgoing = new Dictionary<(string Node, string Port), int>();
        var degrees = parts.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var next = parts.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var wire in wires)
        {
            if (wire is null || !_nodes.TryGetValue(wire.From, out var from) || !_nodes.TryGetValue(wire.To, out var to) ||
                !Ports(from, false).TryGetValue(wire.Output, out var output) || !Ports(to, true).TryGetValue(wire.Input, out var input))
                throw new ArgumentException("控制线连接了不存在的元件或端口。");
            if (output != input) throw new ArgumentException("端口类型或数值单位不匹配。");
            if (!_inputs.TryAdd((wire.To, wire.Input), wire)) throw new ArgumentException("输入口不能由多个来源直接写入。");
            var key = (wire.From, wire.Output);
            outgoing[key] = outgoing.GetValueOrDefault(key) + 1;
            if (outgoing[key] > 1) throw new ArgumentException("一个出口只能接一条线路，多路请使用分流。");
            degrees[wire.To]++; next[wire.From].Add(wire.To);
        }
        foreach (var node in parts)
        {
            if (Ports(node, true).Keys.Any(port => !_inputs.ContainsKey((node.Id, port))) ||
                Ports(node, false).Keys.Any(port => !outgoing.ContainsKey((node.Id, port))))
                throw new ArgumentException(node.Label + "有未连接端口。");
        }
        var queue = new Queue<string>(parts.Where(n => degrees[n.Id] == 0).Select(n => n.Id));
        var order = new List<string>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue(); order.Add(id);
            foreach (var target in next[id]) if (--degrees[target] == 0) queue.Enqueue(target);
        }
        if (order.Count != parts.Length) throw new ArgumentException("组合控制线路不能存在回路。");
        _order = order.ToArray(); Nodes = new ReadOnlyDictionary<string, Node>(_nodes); Links = Array.AsReadOnly(wires);
    }

    private static Dictionary<string, (FormationSignalType Type, FormationValueUnit Unit)> Ports(Node node, bool input)
    {
        var result = new Dictionary<string, (FormationSignalType, FormationValueUnit)>(StringComparer.Ordinal);
        void Add(string name, FormationSignalType type, FormationValueUnit unit = FormationValueUnit.None) => result.Add(name, (type, unit));
        switch (node.Kind)
        {
            case NodeKind.StateInput: if (!input) Add("value", node.SignalType, node.SignalType == FormationSignalType.Number ? node.Unit : FormationValueUnit.None); break;
            case NodeKind.EventInput: if (!input) Add("event", FormationSignalType.Event); break;
            case NodeKind.Compare: if (input) Add("value", FormationSignalType.Number, node.Unit); else Add("state", FormationSignalType.Switch); break;
            case NodeKind.Not: Add("state", FormationSignalType.Switch); break;
            case NodeKind.And:
            case NodeKind.Or: if (input) { Add("a", FormationSignalType.Switch); Add("b", FormationSignalType.Switch); } else Add("state", FormationSignalType.Switch); break;
            case NodeKind.Split: if (input) Add("value", node.SignalType, node.SignalType == FormationSignalType.Number ? node.Unit : FormationValueUnit.None);
                else { Add("a", node.SignalType, node.SignalType == FormationSignalType.Number ? node.Unit : FormationValueUnit.None); Add("b", node.SignalType, node.SignalType == FormationSignalType.Number ? node.Unit : FormationValueUnit.None); } break;
            case NodeKind.Switch: if (input) { Add("event", FormationSignalType.Event); Add("permit", FormationSignalType.Switch); } else Add("event", FormationSignalType.Event); break;
            case NodeKind.GuardOutput: if (input) Add("permit", FormationSignalType.Switch); break;
            case NodeKind.AttackOutput: if (input) Add("event", FormationSignalType.Event); break;
            case NodeKind.ShieldOutput: if (input) Add("event", FormationSignalType.Event); break;
            case NodeKind.EventFilter:
            case NodeKind.EventCounter: Add("event", FormationSignalType.Event); break;
            case NodeKind.EventMerge: if (input) { Add("a", FormationSignalType.Event); Add("b", FormationSignalType.Event); } else Add("event", FormationSignalType.Event); break;
        }
        return result;
    }

    public Evaluation Evaluate(IReadOnlyDictionary<string, FormationControlValue> observations, EventFact? fact = null)
    {
        // 先核实所有观测与事件，再写任何区间记忆，拒绝坏数据时保持原状态。
        foreach (var node in _nodes.Values.Where(n => n.Kind == NodeKind.StateInput))
            if (observations.TryGetValue(node.Key, out var sample) &&
                (sample is null || sample.Type != node.SignalType || sample.Unit != (node.SignalType == FormationSignalType.Number ? node.Unit : FormationValueUnit.None) ||
                 sample.Type == FormationSignalType.Number && sample.Known && !double.IsFinite(sample.Number) ||
                 sample.Type == FormationSignalType.Switch && (!Enum.IsDefined(sample.State) || sample.Known != (sample.State != FormationTruth.Unknown))))
                throw new ArgumentException("状态观测类型、单位或有效性错误。");
        var duplicate = false;
        if (fact is not null)
        {
            if (!Id(fact.Id) || !Id(fact.Name) || fact.SourceTag is null || fact.SourceTag.Length > 128 || fact.SourceTag.Any(char.IsControl) || fact.Amplitude < 0)
                throw new ArgumentException("事件身份、来源或幅值无效。");
            if (_seen.TryGetValue(fact.Id, out var old))
            { if (old != fact) throw new ArgumentException("同一事件身份不能改写内容。"); duplicate = true; }
            else _seen.Add(fact.Id, fact);
        }
        var ports = new Dictionary<(string Node, string Port), FormationControlValue>();
        var values = new Dictionary<string, FormationControlValue>(StringComparer.Ordinal);
        var guards = new Dictionary<string, FormationTruth>(StringComparer.Ordinal);
        var attacks = new List<string>(); var steps = new List<ControlStep>();
        var requests = new List<OutputRequest>();
        var switchPermits = new Dictionary<string, FormationTruth>(StringComparer.Ordinal);
        FormationControlValue Read(string id, string input)
        {
            var wire = _inputs[(id, input)]; return ports[(wire.From, wire.Output)];
        }
        foreach (var id in _order)
        {
            var node = _nodes[id]; FormationControlValue value; string? description = null;
            switch (node.Kind)
            {
                case NodeKind.StateInput:
                    value = observations.GetValueOrDefault(node.Key) ?? FormationControlValue.Unknown(node.SignalType, node.Unit); break;
                case NodeKind.EventInput:
                    value = !duplicate && fact?.Name == node.Key && fact.Amplitude >= node.MinimumAmplitude &&
                        (node.SourceTag.Length == 0 || node.SourceTag == fact.SourceTag)
                        ? FormationControlValue.Event(fact.Id, fact.SourceTag, fact.Amplitude) : FormationControlValue.Event(); break;
                case NodeKind.Compare:
                    var number = Read(id, "value");
                    if (!number.Known) value = FormationControlValue.Boolean(FormationTruth.Unknown);
                    else
                    {
                        var state = node.Comparison switch
                        {
                            CompareMode.AtLeast => number.Number >= node.Low,
                            CompareMode.AtMost => number.Number <= node.Low,
                            _ => number.Number <= node.Low || number.Number < node.High && _memory.GetValueOrDefault(id, node.InitiallyOn)
                        };
                        if (node.Comparison == CompareMode.Band) _memory[id] = state;
                        value = FormationControlValue.Boolean(state ? FormationTruth.True : FormationTruth.False);
                    }
                    break;
                case NodeKind.Not: value = FormationControlValue.Boolean(Not(Read(id, "state").State)); break;
                case NodeKind.And: value = FormationControlValue.Boolean(And(Read(id, "a").State, Read(id, "b").State)); break;
                case NodeKind.Or: value = FormationControlValue.Boolean(Or(Read(id, "a").State, Read(id, "b").State)); break;
                case NodeKind.Split:
                    value = Read(id, "value"); ports[(id, "a")] = value; ports[(id, "b")] = value; break;
                case NodeKind.Switch:
                    var permit = Read(id, "permit").State;
                    if (node.NormallyClosed) permit = Not(permit);
                    switchPermits[id] = permit;
                    value = permit == FormationTruth.True ? Read(id, "event") : FormationControlValue.Event(); break;
                case NodeKind.EventFilter:
                    var filtered = Read(id, "event");
                    value = filtered.HasEvent && filtered.Amplitude >= node.MinimumAmplitude &&
                        (node.SourceTag.Length == 0 || node.SourceTag == filtered.SourceTag) ? filtered : FormationControlValue.Event(); break;
                case NodeKind.EventCounter:
                    var counted = Read(id, "event");
                    var phase = _counterPhases.GetValueOrDefault(id);
                    var passes = counted.HasEvent && (node.FirstImmediate ? phase == 0 : phase + 1 == node.Interval);
                    if (counted.HasEvent) _counterPhases[id] = phase = (phase + 1) % node.Interval;
                    value = passes ? counted : FormationControlValue.Event();
                    description = $"计数{phase}/{node.Interval} · {(passes ? "本次放行" : "本次未放行")}"; break;
                case NodeKind.EventMerge:
                    var first = Read(id, "a"); var second = Read(id, "b");
                    value = first.HasEvent ? first : second; break;
                case NodeKind.GuardOutput:
                    value = Read(id, "permit"); guards[id] = value.State; break;
                case NodeKind.AttackOutput:
                    value = Read(id, "event"); if (value.HasEvent) { attacks.Add(id); requests.Add(new(id, node.Kind, value)); } break;
                case NodeKind.ShieldOutput:
                    value = Read(id, "event"); if (value.HasEvent) requests.Add(new(id, node.Kind, value)); break;
                default: throw new InvalidOperationException("未知控制元件。");
            }
            if (node.Kind != NodeKind.Split)
                foreach (var port in Ports(node, false).Keys) ports[(id, port)] = value;
            values[id] = value;
            var passed = value.Known && (value.Type == FormationSignalType.Number || value.Type == FormationSignalType.Switch && value.State == FormationTruth.True || value.HasEvent);
            steps.Add(new ControlStep(id, node.Label, description ?? value.Display, passed));
        }
        return new Evaluation(steps.AsReadOnly(), new ReadOnlyDictionary<string, FormationControlValue>(values),
            new ReadOnlyDictionary<string, FormationTruth>(guards), attacks.AsReadOnly(), duplicate)
        { PulseRequests = requests.AsReadOnly(), SwitchPermits = new ReadOnlyDictionary<string, FormationTruth>(switchPermits) };
    }

    public static FormationTruth Not(FormationTruth a) => a == FormationTruth.Unknown ? a : a == FormationTruth.True ? FormationTruth.False : FormationTruth.True;
    public static FormationTruth And(FormationTruth a, FormationTruth b) => a == FormationTruth.False || b == FormationTruth.False ? FormationTruth.False : a == FormationTruth.True && b == FormationTruth.True ? FormationTruth.True : FormationTruth.Unknown;
    public static FormationTruth Or(FormationTruth a, FormationTruth b) => a == FormationTruth.True || b == FormationTruth.True ? FormationTruth.True : a == FormationTruth.False && b == FormationTruth.False ? FormationTruth.False : FormationTruth.Unknown;
    private static bool Id(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 && !id.Any(char.IsControl);
}
