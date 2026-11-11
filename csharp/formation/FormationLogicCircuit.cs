using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>基础信号逻辑。事件身份、幅值和来源沿线路原样传递；输出只代表控制请求。</summary>
public sealed class FormationLogicCircuit
{
    // Divider保留编号3，只读取旧记录；运行统一为Counter及其起步设置。
    public enum NodeKind { Input, Filter, Counter, Divider, Split, Merge, Output }
    public sealed record Node(string Id, NodeKind Kind)
    {
        public int Interval { get; init; } = 2;
        public bool FirstImmediate { get; init; }
        public int MinimumAmplitude { get; init; } = 1;
        public string EventName { get; init; } = "spell_played";
        public IReadOnlyList<string> AcceptedSourceTags { get; init; } = Array.Empty<string>();
    }
    public sealed record Edge(string FromNodeId, string ToNodeId);
    public sealed record Signal(string EventId, string EventName, string SourceTag, int Amplitude);
    public sealed record Step(string NodeId, bool Passed, string Message);
    public sealed record RunResult(Signal Signal, IReadOnlyList<Step> Steps, IReadOnlyList<string> OutputNodeIds, bool Duplicate);

    private readonly Dictionary<string, Node> _nodes;
    private readonly Dictionary<string, List<string>> _next;
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Signal> _events = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, Node> Nodes { get; }

    public FormationLogicCircuit(IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var nodeArray = nodes.ToArray();
        var edgeArray = edges.ToArray();
        var issues = Validate(nodeArray, edgeArray);
        if (issues.Count > 0) throw new ArgumentException(string.Join("；", issues));
        _nodes = nodeArray.ToDictionary(n => n.Id, n => n with
        {
            Kind = n.Kind == NodeKind.Divider ? NodeKind.Counter : n.Kind,
            FirstImmediate = n.FirstImmediate || n.Kind == NodeKind.Divider,
            AcceptedSourceTags = Array.AsReadOnly(n.AcceptedSourceTags.ToArray())
        }, StringComparer.Ordinal);
        Nodes = new ReadOnlyDictionary<string, Node>(_nodes);
        _next = _nodes.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in edgeArray) _next[edge.FromNodeId].Add(edge.ToNodeId);
    }

    public static IReadOnlyList<string> Validate(IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var parts = nodes.ToArray();
        var links = edges.ToArray();
        var issues = new List<string>();
        var ids = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in parts)
        {
            if (node is null || !Identifier(node.Id)) { issues.Add("逻辑元件缺少有效标识。"); continue; }
            if (!ids.TryAdd(node.Id, node)) issues.Add("逻辑元件标识重复：" + node.Id);
            if (!Enum.IsDefined(node.Kind)) issues.Add("逻辑元件类型无效：" + node.Id);
            if (node.Kind is NodeKind.Counter or NodeKind.Divider && node.Interval is < 1 or > 12)
                issues.Add("计数间隔须为1至12：" + node.Id);
            if (node.Kind is not (NodeKind.Counter or NodeKind.Divider) && node.FirstImmediate)
                issues.Add("只有计数器可设置先发一次：" + node.Id);
            if (node.Kind == NodeKind.Filter && node.MinimumAmplitude is < 1 or > 9)
                issues.Add("筛选幅值须为1至9：" + node.Id);
            if (node.Kind == NodeKind.Input && !Identifier(node.EventName)) issues.Add("感应事件无效：" + node.Id);
            if (node.AcceptedSourceTags is null || node.AcceptedSourceTags.Any(tag => !Identifier(tag)))
                issues.Add("来源标签无效：" + node.Id);
        }
        if (issues.Count > 0) return issues;
        if (!parts.Any(n => n.Kind == NodeKind.Input)) issues.Add("逻辑缺少感应输入。");
        if (!parts.Any(n => n.Kind == NodeKind.Output)) issues.Add("逻辑缺少执行出口。");
        var incoming = ids.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var outgoing = ids.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var next = ids.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in links)
        {
            if (edge is null || !ids.ContainsKey(edge.FromNodeId) || !ids.ContainsKey(edge.ToNodeId))
            { issues.Add("信号线连接了不存在的元件。"); continue; }
            outgoing[edge.FromNodeId]++; incoming[edge.ToNodeId]++;
            next[edge.FromNodeId].Add(edge.ToNodeId);
        }
        foreach (var (id, node) in ids)
        {
            var expectedIn = node.Kind == NodeKind.Input ? 0 : node.Kind == NodeKind.Merge ? 2 : 1;
            var expectedOut = node.Kind == NodeKind.Output ? 0 : node.Kind == NodeKind.Split ? 2 : 1;
            if (incoming[id] != expectedIn || outgoing[id] != expectedOut)
                issues.Add($"{id}需要{expectedIn}个输入连接、{expectedOut}个输出连接。");
        }
        var degree = new Dictionary<string, int>(incoming, StringComparer.Ordinal);
        var ready = new Queue<string>(degree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (ready.Count > 0)
        {
            var id = ready.Dequeue(); visited++;
            foreach (var target in next[id]) if (--degree[target] == 0) ready.Enqueue(target);
        }
        if (visited != ids.Count) issues.Add("基础信号线路不允许反馈回路。");
        return issues;
    }

    public RunResult Emit(Signal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (!Identifier(signal.EventId) || !Identifier(signal.EventName) || signal.SourceTag is null ||
            signal.SourceTag.Length > 128 || signal.SourceTag.Any(char.IsControl))
            throw new ArgumentException("信号身份、事件或来源无效。", nameof(signal));
        if (signal.Amplitude < 0) throw new ArgumentOutOfRangeException(nameof(signal), "信号幅值不能为负数。");
        if (_events.TryGetValue(signal.EventId, out var original))
        {
            if (original != signal) throw new ArgumentException("同一次发生的信号内容不能改写。", nameof(signal));
            return new RunResult(signal, Array.Empty<Step>(), Array.Empty<string>(), true);
        }
        _events.Add(signal.EventId, signal);
        var steps = new List<Step>();
        var outputs = new List<string>();
        var pending = new Queue<string>();
        foreach (var input in _nodes.Values.Where(n => n.Kind == NodeKind.Input))
        {
            var accepts = input.EventName == signal.EventName && AcceptsSource(input, signal);
            steps.Add(new Step(input.Id, accepts, accepts ? "收到一次新信号" : "事件或来源不符"));
            if (accepts) foreach (var target in _next[input.Id]) pending.Enqueue(target);
        }
        // 一次真实发生的所有输入、分支与合流共用此集合，不按路径数重复计次。
        var handled = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (!handled.Add(id)) continue;
            var node = _nodes[id];
            var passes = true;
            string message;
            switch (node.Kind)
            {
                case NodeKind.Filter:
                    passes = AcceptsSource(node, signal) && signal.Amplitude >= node.MinimumAmplitude;
                    message = passes ? "筛选通过" : !AcceptsSource(node, signal) ? "来源不符，停止" : "幅值不足，停止";
                    break;
                case NodeKind.Counter:
                    var phase = GetCount(id);
                    passes = node.FirstImmediate ? phase == 0 : phase + 1 == node.Interval;
                    _counts[id] = (phase + 1) % node.Interval;
                    message = passes
                        ? $"本次放行，每{node.Interval}次一次（{(node.FirstImmediate ? "先发一次" : "数满再发")}）"
                        : $"计数进度{phase + 1}/{node.Interval}，等待下次";
                    break;
                case NodeKind.Split: message = "分给两路，身份和幅值保持"; break;
                case NodeKind.Merge: message = "合为一路，同一发生只通过一次"; break;
                case NodeKind.Output:
                    outputs.Add(id); message = "控制信号已抵达"; break;
                default: throw new InvalidOperationException("信号进入了不允许的元件入口。");
            }
            steps.Add(new Step(id, passes, message));
            if (passes) foreach (var target in _next[id]) pending.Enqueue(target);
        }
        return new RunResult(signal, steps.AsReadOnly(), outputs.AsReadOnly(), false);
    }

    public int GetCount(string nodeId) => _counts.GetValueOrDefault(nodeId);
    public void Reset() { _counts.Clear(); _events.Clear(); }
    private static bool AcceptsSource(Node node, Signal signal) =>
        node.AcceptedSourceTags.Count == 0 || node.AcceptedSourceTags.Contains(signal.SourceTag, StringComparer.Ordinal);
    private static bool Identifier(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && !value.Any(char.IsControl);
}
