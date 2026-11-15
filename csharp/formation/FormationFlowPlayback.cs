using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 从一次已完成的真实求值、付款差额与物理路线生成展示计划。
/// 秒数只属于视觉播放；本类型不调用Apply、不扣费，也不延后或重做业务结果。
/// </summary>
public static class FormationFlowPlayback
{
    public const float NodePause = .12f;

    public sealed record Item(FormationGridFlowRoutes.Route Route, float Delay, float Duration,
        bool IsLow, FormationControlValue? Value, bool UsesHeldPermit = false, int PaymentCount = 0)
    {
        public bool IsEnergy => Route.Kind == FormationGridModel.PortKind.Energy;
        public float End => Delay + Duration;
        public string DisplayValue => Value?.Display ?? "";
        public string OccurrenceId { get; init; } = "";
    }

    public sealed record Plan(IReadOnlyList<Item> Items, float Duration, bool Duplicate)
    {
        public string OccurrenceId { get; init; } = "";
        public IReadOnlyList<string> PaidOutputIds { get; init; } = Array.Empty<string>();
    }

    public static Plan Build(FormationGridModel model, FormationGridControlCompiler.Compilation compilation,
        FormationControlCircuit.Evaluation evaluation,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> before,
        FormationGuardAssaultRuntime.Result result,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> after, string? occurrenceId = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(result);
        return BuildCore(model, compilation.Circuit, evaluation, before, result.Duplicate, after,
            compilation.SourceId, new HashSet<string>(compilation.PoweredOutputIds, StringComparer.Ordinal), occurrenceId);
    }

    public static Plan Build(FormationGridModel model, FormationControlCircuit circuit,
        FormationControlCircuit.Evaluation evaluation,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> before,
        FormationGuardAssaultRuntime.Result result,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> after, string? occurrenceId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Build(model, circuit, evaluation, before, result.Duplicate, after, occurrenceId);
    }

    public static Plan Build(FormationGridModel model, FormationControlCircuit circuit,
        FormationControlCircuit.Evaluation evaluation,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> before, bool duplicate,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> after, string? occurrenceId = null)
        => BuildCore(model, circuit, evaluation, before, duplicate, after, null, null, occurrenceId);

    private static Plan BuildCore(FormationGridModel model, FormationControlCircuit circuit,
        FormationControlCircuit.Evaluation evaluation,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> before, bool duplicate,
        IReadOnlyDictionary<string, FormationGuardAssaultRuntime.OutputState> after,
        string? sourceId, IReadOnlySet<string>? poweredOutputs, string? occurrenceId)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        occurrenceId ??= evaluation.Values.Values.FirstOrDefault(value => value.Type == FormationSignalType.Event && value.HasEvent)?.EventId ?? "";
        // 同次重发仍保留运行器的上一次读数，不能因此再播放旧事件或旧付款。
        if (duplicate || evaluation.Duplicate) return Empty(true, occurrenceId);

        var routes = FormationGridFlowRoutes.Build(model);
        var links = circuit.Links.Select(link => (link.From, link.Output, link.To, link.Input)).ToHashSet();
        var signals = routes.Where(route => route.Kind == FormationGridModel.PortKind.Signal &&
            links.Contains((route.FromComponentId, route.FromPortName, route.ToComponentId, route.ToPortName)))
            .GroupBy(route => route.FromComponentId).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var arrivals = new Dictionary<string, List<float>>(StringComparer.Ordinal);
        var ready = new Dictionary<string, float>(StringComparer.Ordinal);
        var items = new List<Item>();

        foreach (var id in circuit.EvaluationOrder)
        {
            // 合流收到任一路即可转发，其余节点等本次有效输入到达后短暂停；未知不伪造到达。
            var departure = arrivals.TryGetValue(id, out var arrival) ?
                (circuit.Nodes[id].Kind == FormationControlCircuit.NodeKind.EventMerge ? arrival.Min() : arrival.Max()) + NodePause : 0;
            ready[id] = departure;
            if (!signals.TryGetValue(id, out var outgoing) || !evaluation.Values.TryGetValue(id, out var value) || !Transmits(value)) continue;
            foreach (var route in outgoing)
            {
                var item = new Item(route, departure, RouteDuration(route),
                    value.Type == FormationSignalType.Switch && value.State == FormationTruth.False, value) { OccurrenceId = occurrenceId };
                items.Add(item);
                if (!arrivals.TryGetValue(route.ToComponentId, out var incoming)) arrivals[route.ToComponentId] = incoming = new List<float>();
                incoming.Add(item.End);
            }
        }

        var paid = after.Where(pair => circuit.Nodes.ContainsKey(pair.Key) && pair.Value.Payments >
            (before.TryGetValue(pair.Key, out var previous) ? previous.Payments : 0)).Select(pair => pair.Key).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        foreach (var route in routes.Where(route => route.Kind == FormationGridModel.PortKind.Energy))
        {
            if (sourceId is not null && route.FromComponentId != sourceId ||
                poweredOutputs is not null && !poweredOutputs.Contains(route.ToComponentId) ||
                !circuit.Nodes.TryGetValue(route.ToComponentId, out var node) ||
                node.Kind is not (FormationControlCircuit.NodeKind.GuardOutput or FormationControlCircuit.NodeKind.AttackOutput or FormationControlCircuit.NodeKind.ShieldOutput) ||
                !after.TryGetValue(route.ToComponentId, out var state)) continue;
            var priorPayments = before.TryGetValue(route.ToComponentId, out var prior) ? prior.Payments : 0;
            var payments = (long)state.Payments - priorPayments;
            if (payments <= 0) continue;
            // 有实际付款才有供能展示。续费可沿已持有的许可付款，不为此伪造一次新红线。
            var held = node.Kind == FormationControlCircuit.NodeKind.GuardOutput &&
                !arrivals.ContainsKey(node.Id) && prior?.Running == true && state.Running;
            var delay = ready.GetValueOrDefault(node.Id);
            if (!arrivals.ContainsKey(node.Id)) delay += NodePause;
            items.Add(new Item(route, delay, RouteDuration(route), false, null, held,
                (int)Math.Min(int.MaxValue, payments)) { OccurrenceId = occurrenceId });
        }

        return Finish(items, occurrenceId, paid);
    }

    /// <summary>旧信号页只展示实际通过的事件；不把控制请求冒充付款或供能。</summary>
    public static Plan BuildLegacy(FormationGridModel model, FormationLogicCircuit.RunResult result)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Duplicate) return Empty(true, result.Signal.EventId);
        if (result.Signal.Amplitude <= 0 || string.IsNullOrEmpty(result.Signal.EventId)) return Empty(false, result.Signal.EventId);
        var routes = FormationGridFlowRoutes.Build(model).Where(route => route.Kind == FormationGridModel.PortKind.Signal).ToArray();
        var passed = result.Steps.Where(step => step.Passed).Select(step => step.NodeId).ToHashSet(StringComparer.Ordinal);
        var nodes = routes.SelectMany(route => new[] { route.FromComponentId, route.ToComponentId }).Distinct(StringComparer.Ordinal).ToArray();
        var indegree = nodes.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var route in routes) indegree[route.ToComponentId]++;
        var outgoing = routes.GroupBy(route => route.FromComponentId).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var ready = new Queue<string>(nodes.Where(id => indegree[id] == 0).OrderBy(id => id, StringComparer.Ordinal));
        var order = new List<string>();
        while (ready.Count > 0)
        {
            var id = ready.Dequeue(); order.Add(id);
            foreach (var route in outgoing.GetValueOrDefault(id) ?? Array.Empty<FormationGridFlowRoutes.Route>())
                if (--indegree[route.ToComponentId] == 0) ready.Enqueue(route.ToComponentId);
        }
        if (order.Count != nodes.Length) return Empty(false, result.Signal.EventId);
        var arrivals = new Dictionary<string, List<float>>(StringComparer.Ordinal);
        var items = new List<Item>();
        var value = FormationControlValue.Event(result.Signal.EventId, result.Signal.SourceTag, result.Signal.Amplitude);
        foreach (var id in order)
        {
            if (!passed.Contains(id) || !outgoing.TryGetValue(id, out var next)) continue;
            var merge = model.Components.TryGetValue(id, out var component) && component.Kind == FormationGridModel.Kind.Merge;
            var departure = arrivals.TryGetValue(id, out var incoming) ? (merge ? incoming.Min() : incoming.Max()) + NodePause : 0;
            foreach (var route in next)
            {
                var item = new Item(route, departure, RouteDuration(route), false, value) { OccurrenceId = result.Signal.EventId };
                items.Add(item);
                if (!arrivals.TryGetValue(route.ToComponentId, out var target)) arrivals[route.ToComponentId] = target = new List<float>();
                target.Add(item.End);
            }
        }
        return Finish(items, result.Signal.EventId, Array.Empty<string>());
    }

    private static Plan Empty(bool duplicate, string occurrenceId) => new(Array.Empty<Item>(), 0, duplicate) { OccurrenceId = occurrenceId };

    private static Plan Finish(IEnumerable<Item> items, string occurrenceId, string[] paid)
    {
        var ordered = items.OrderBy(item => item.Delay).ThenBy(item => item.IsEnergy)
            .ThenBy(item => item.Route.FromComponentId, StringComparer.Ordinal)
            .ThenBy(item => item.Route.FromPortName, StringComparer.Ordinal)
            .ThenBy(item => item.Route.ToComponentId, StringComparer.Ordinal).ToArray();
        return new Plan(Array.AsReadOnly(ordered), ordered.Length == 0 ? 0 : ordered.Max(item => item.End), false)
        { OccurrenceId = occurrenceId, PaidOutputIds = Array.AsReadOnly(paid) };
    }

    private static bool Transmits(FormationControlValue value) => value.Known && (value.Type switch
    {
        FormationSignalType.Event => value.HasEvent,
        FormationSignalType.Switch => value.State is FormationTruth.True or FormationTruth.False,
        FormationSignalType.Number => double.IsFinite(value.Number),
        _ => false
    });

    private static float RouteDuration(FormationGridFlowRoutes.Route route)
        => Math.Clamp(.42f + route.WireCells.Count * .15f, .42f, 1.5f);
}
