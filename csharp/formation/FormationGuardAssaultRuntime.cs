using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>控制运行器的真实效果宿主；Godot与纯检查共用同一个账本及操作协议。</summary>
public interface IFormationGuardActor
{
    int Health { get; set; }
    int MaxHealth { get; }
    int Mana { get; }
    bool IsAlive { get; }
    FormationShieldLedger Shields { get; }
    int EnemyHealth { get; }
    bool TryAttack(int amount);
}

/// <summary>实际控制图的效果事务；所有出口共用真实阵源与同波峰值，来源与额度按实际节点分别保存。</summary>
public sealed class FormationGuardAssaultRuntime : IDisposable
{
    // 无正式平衡默认值；调用者须明确给定成器参数或标明试运行fixture。
    public sealed record Settings(int SourceCapacity, int InitialEnergy, int RechargePerRound,
        int SourcePeak, int LinkPeak, int BoardPeak, int GuardCost, int GuardCapacity, int GuardLoad,
        int AttackCost, int AttackDamage, int AttackLoad, int AttackLimitPerRound,
        double LowHealth, double HighHealth, int AttackReserve = 0);
    public enum ActionKind { Refresh, Spell, Damage, Heal, OrdinaryShield, Energy, NextRound, SetHealth, Observation, Supply, Enable, Signal }
    public sealed record Input(string Id, ActionKind Kind, int Value = 0, bool Flag = true,
        string EventName = "", string SourceTag = "", int Amplitude = 1);
    public sealed record Snapshot(int Round, int Health, int MaxHealth, int Mana, int EnemyHealth,
        int Shield, int GuardRemaining, bool GuardRunning, bool GuardPaidThisRound, int SourceEnergy,
        FormationTruth Mode, FormationTruth AttackAllowed, bool Enabled, bool ObservationKnown,
        bool SupplyValid, int GuardPayments, int Attacks, long EnergySpent)
    {
        public string ModeText => Mode == FormationTruth.True ? "守" : Mode == FormationTruth.False ? "攻" : "未知";
    }
    public sealed record Result(Snapshot Snapshot, IReadOnlyList<ControlStep> Steps, IReadOnlyList<string> Messages, bool Duplicate);
    public sealed record OutputState(string NodeId, FormationControlCircuit.NodeKind Kind, bool Powered,
        FormationTruth Request, bool Running, bool PaidThisRound, int Remaining, int UsedThisRound,
        int Payments, int Executions, string SourceId);

    private sealed class ActuatorState(string id, FormationControlCircuit.NodeKind kind, string sourceId)
    {
        public readonly string Id = id, SourceId = sourceId;
        public readonly FormationControlCircuit.NodeKind Kind = kind;
        public bool Running, Paid, StartedThisWave;
        public int UsedThisRound, Payments, Executions;
    }
    private readonly IFormationGuardActor _actor;
    private readonly Settings _settings;
    private readonly Dictionary<string, Input> _seen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActuatorState> _outputs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _poweredOutputIds;
    private readonly bool _sourcePresent, _legacyExample;
    private readonly string _sourcePrefix;
    private FormationControlCircuit.Evaluation _evaluation = null!;
    private int _round = 1, _energy, _guardPayments, _attacks;
    private long _energySpent, _wavePeak, _baseLoad;
    private bool _enabled = true, _observationKnown = true, _supplyValid = true, _applying, _disposed;
    public FormationControlCircuit Circuit { get; }
    public Settings Configuration => _settings;
    public FormationControlCircuit.Evaluation CurrentEvaluation => _evaluation;
    public IReadOnlyDictionary<string, OutputState> OutputStates => GetOutputStates();
    public string GuardSourceId => _outputs.Values.FirstOrDefault(s => s.Kind == FormationControlCircuit.NodeKind.GuardOutput)?.SourceId ?? _sourcePrefix + ":guard";
    public bool SourcePresent => _sourcePresent;

    // 保留历史固定守攻示例。自由编辑器必须走下方显式Circuit/供能拓扑构造，不能失败后退回此图。
    public FormationGuardAssaultRuntime(Settings settings, IFormationGuardActor actor, string? instanceId = null)
        : this(settings, actor, CreateCircuit(settings), null, true, instanceId, true) { }

    public FormationGuardAssaultRuntime(Settings settings, IFormationGuardActor actor,
        FormationControlCircuit circuit, IEnumerable<string> poweredOutputIds,
        bool sourcePresent = true, string? instanceId = null)
        : this(settings, actor, circuit, poweredOutputIds ?? throw new ArgumentNullException(nameof(poweredOutputIds)), sourcePresent, instanceId, false) { }

    private FormationGuardAssaultRuntime(Settings settings, IFormationGuardActor actor,
        FormationControlCircuit circuit, IEnumerable<string>? poweredOutputIds,
        bool sourcePresent, string? instanceId, bool legacyExample)
    {
        Validate(settings);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(circuit);
        if (actor.MaxHealth <= 0) throw new ArgumentException("人物生命上限必须为正数。", nameof(actor));
        instanceId ??= Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(instanceId) || instanceId.Length > 80 || instanceId.Any(char.IsControl))
            throw new ArgumentException("阵器实例标识无效。", nameof(instanceId));
        _settings = settings; _actor = actor; _sourcePresent = sourcePresent; _legacyExample = legacyExample;
        _energy = sourcePresent ? settings.InitialEnergy : 0;
        _sourcePrefix = "formation:guard:" + instanceId;
        // 编译产物只借定义，不共享别次试验的计数、区间记忆或去重历史。
        Circuit = new FormationControlCircuit(circuit.Nodes.Values, circuit.Links);
        foreach (var node in Circuit.Nodes.Values.Where(n => n.Kind == FormationControlCircuit.NodeKind.StateInput))
        {
            var expected = node.Key switch
            {
                "health" => FormationValueUnit.Percent, "shield" => FormationValueUnit.Shield,
                "mana" => FormationValueUnit.Mana, "energy" => FormationValueUnit.Energy,
                _ => FormationValueUnit.None
            };
            if (node.SignalType != FormationSignalType.Number || expected == FormationValueUnit.None || node.Unit != expected)
                throw new ArgumentException("当前试演只授权生命比例、公共护盾、法力与阵能的对应单位输入。");
        }
        foreach (var id in Circuit.EvaluationOrder)
        {
            var node = Circuit.Nodes[id];
            if (node.Kind is not (FormationControlCircuit.NodeKind.GuardOutput or FormationControlCircuit.NodeKind.AttackOutput or FormationControlCircuit.NodeKind.ShieldOutput)) continue;
            var source = node.Kind == FormationControlCircuit.NodeKind.GuardOutput
                ? _sourcePrefix + ":" + Uri.EscapeDataString(id) : "";
            _outputs.Add(id, new ActuatorState(id, node.Kind, source));
        }
        _poweredOutputIds = new HashSet<string>(poweredOutputIds ?? _outputs.Keys, StringComparer.Ordinal);
        if (_poweredOutputIds.Any(id => !_outputs.ContainsKey(id)) || !sourcePresent && _poweredOutputIds.Count > 0)
            throw new ArgumentException("供能可达集合包含不存在的出口或没有真实阵源。", nameof(poweredOutputIds));
        var messages = new List<string>();
        _applying = true;
        try
        {
            using var notifications = actor.Shields.DeferNotifications();
            BeginWave(); EvaluateAndCommit(null, messages, new HashSet<string>(StringComparer.Ordinal));
        }
        finally { _applying = false; }
    }

    public static void Validate(Settings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.SourceCapacity < 0 || s.InitialEnergy < 0 || s.InitialEnergy > s.SourceCapacity || s.RechargePerRound < 0 ||
            s.SourcePeak < 0 || s.LinkPeak < 0 || s.BoardPeak < 0 || s.GuardCost < 0 || s.GuardCapacity < 1 || s.GuardLoad < 0 ||
            s.AttackCost < 0 || s.AttackDamage < 1 || s.AttackLoad < 0 || s.AttackLimitPerRound < 1 || s.AttackReserve < 0 ||
            !double.IsFinite(s.LowHealth) || !double.IsFinite(s.HighHealth) || s.LowHealth < 0 || s.HighHealth > 100 || s.LowHealth >= s.HighHealth ||
            (long)s.AttackCost + s.AttackReserve > int.MaxValue)
            throw new ArgumentException("守攻参数无效：数值须合法，生命下限须小于上限。");
    }

    public static FormationControlCircuit CreateCircuit(Settings s)
    {
        Validate(s);
        // 这里的具名端口连接是实际解释的控制图，不是另写一份if分支的画面。
        var nodes = new List<FormationControlCircuit.Node>
        {
            new("health", FormationControlCircuit.NodeKind.StateInput, "生命输入") { Key = "health", Unit = FormationValueUnit.Percent },
            new("compare", FormationControlCircuit.NodeKind.Compare, "比较器") { Unit = FormationValueUnit.Percent, Comparison = FormationControlCircuit.CompareMode.Band, Low = s.LowHealth, High = s.HighHealth },
            new("branch", FormationControlCircuit.NodeKind.Split, "分流") { SignalType = FormationSignalType.Switch },
            new("guard", FormationControlCircuit.NodeKind.GuardOutput, "护罩"),
            new("invert", FormationControlCircuit.NodeKind.Not, "取反"),
            new("cast", FormationControlCircuit.NodeKind.EventInput, "施法完成") { Key = "spell_completed" },
            new("switch", FormationControlCircuit.NodeKind.Switch, "开关"),
            new("attack", FormationControlCircuit.NodeKind.AttackOutput, "火击")
        };
        var links = new List<FormationControlCircuit.Link>
        {
            new("health", "value", "compare", "value"), new("compare", "state", "branch", "value"),
            new("branch", "a", "guard", "permit"), new("branch", "b", "invert", "state"),
            new("cast", "event", "switch", "event"), new("switch", "event", "attack", "event")
        };
        if (s.AttackReserve > 0)
        {
            nodes.Add(new("energy", FormationControlCircuit.NodeKind.StateInput, "阵能输入") { Key = "energy", Unit = FormationValueUnit.Energy });
            nodes.Add(new("reserve", FormationControlCircuit.NodeKind.Compare, "留能比较") { Unit = FormationValueUnit.Energy, Low = s.AttackCost + s.AttackReserve, Comparison = FormationControlCircuit.CompareMode.AtLeast });
            nodes.Add(new("allow", FormationControlCircuit.NodeKind.And, "同时满足"));
            links.Add(new("energy", "value", "reserve", "value")); links.Add(new("reserve", "state", "allow", "b"));
            links.Add(new("invert", "state", "allow", "a")); links.Add(new("allow", "state", "switch", "permit"));
        }
        else links.Add(new("invert", "state", "switch", "permit"));
        return new FormationControlCircuit(nodes, links);
    }

    private static FormationTruth AnyTruth(IEnumerable<FormationTruth> values)
    {
        var result = FormationTruth.False;
        foreach (var value in values) result = FormationControlCircuit.Or(result, value);
        return result;
    }

    public Snapshot GetSnapshot()
    {
        SyncExhaustion();
        var mode = _legacyExample ? _evaluation.Values["compare"].State : AnyTruth(_evaluation.GuardRequests.Values);
        var permit = _legacyExample ? _evaluation.Values[_settings.AttackReserve > 0 ? "allow" : "invert"].State :
            _evaluation.SwitchPermits.Count > 0 ? AnyTruth(_evaluation.SwitchPermits.Values) :
            _outputs.Values.Any(s => s.Kind == FormationControlCircuit.NodeKind.AttackOutput) ? FormationTruth.True : FormationTruth.False;
        var guards = _outputs.Values.Where(s => s.Kind == FormationControlCircuit.NodeKind.GuardOutput).ToArray();
        return new Snapshot(_round, _actor.Health, _actor.MaxHealth, _actor.Mana, _actor.EnemyHealth,
            _actor.Shields.Total, guards.Sum(s => _actor.Shields.GetRemaining(s.SourceId)), guards.Any(s => s.Running),
            guards.Any(s => s.Paid), _energy, mode, permit, _enabled, _observationKnown, _supplyValid,
            _guardPayments, _attacks, _energySpent);
    }

    public IReadOnlyDictionary<string, OutputState> GetOutputStates()
    {
        SyncExhaustion();
        return new ReadOnlyDictionary<string, OutputState>(_outputs.ToDictionary(pair => pair.Key, pair =>
        {
            var state = pair.Value;
            var request = state.Kind == FormationControlCircuit.NodeKind.GuardOutput
                ? _evaluation.GuardRequests.GetValueOrDefault(state.Id, FormationTruth.Unknown)
                : _evaluation.Values[state.Id].HasEvent ? FormationTruth.True : FormationTruth.False;
            return new OutputState(state.Id, state.Kind, Powered(state), request, state.Running,
                state.Kind == FormationControlCircuit.NodeKind.GuardOutput ? state.Paid : state.UsedThisRound > 0,
                state.SourceId.Length > 0 ? _actor.Shields.GetRemaining(state.SourceId) : 0,
                state.Kind == FormationControlCircuit.NodeKind.GuardOutput ? state.Paid ? 1 : 0 : state.UsedThisRound,
                state.Payments, state.Executions, state.SourceId);
        }, StringComparer.Ordinal));
    }

    public Result Apply(Input input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_applying) throw new InvalidOperationException("当前阵法结算尚未完成，不能同步插入另一次操作。");
        _applying = true;
        try
        {
            using var notifications = _actor.Shields.DeferNotifications();
            return ApplyCore(input);
        }
        finally { _applying = false; }
    }

    private Result ApplyCore(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.Id) || input.Id.Length > 128 || input.Id.Any(char.IsControl) ||
            !Enum.IsDefined(input.Kind) || input.Value < 0 || input.Amplitude < 0 || input.SourceTag is null ||
            input.SourceTag.Length > 128 || input.SourceTag.Any(char.IsControl) ||
            input.Kind == ActionKind.Signal && input.EventName is not ("spell_played" or "spell_completed" or "card_played" or "turn_start" or "cycle_completed"))
            throw new ArgumentException("操作身份、类别、来源、事件或数值无效。");
        if (_seen.TryGetValue(input.Id, out var old))
        {
            if (old != input) throw new ArgumentException("同一次操作不能改写内容。");
            return new Result(GetSnapshot(), Array.Empty<ControlStep>(), new[] { "重复通知：不重放操作、不扣费、不刷新回合。" }, true);
        }
        _seen.Add(input.Id, input);
        var messages = new List<string>(); BeginWave();
        var renewing = new HashSet<string>(StringComparer.Ordinal);
        var boundary = input.Kind == ActionKind.NextRound;
        switch (input.Kind)
        {
            case ActionKind.Damage:
                var absorbed = _actor.Shields.Absorb(input.Value);
                var healthLoss = Math.Min(Math.Max(0, _actor.Health), input.Value - absorbed);
                _actor.Health -= healthLoss;
                SyncExhaustion(); messages.Add($"伤害已结算：护盾吸收{absorbed}，生命减少{healthLoss}。后开的罩只防之后的伤害。"); break;
            case ActionKind.Heal:
                if (_actor.IsAlive) _actor.Health = (int)Math.Min(_actor.MaxHealth, (long)_actor.Health + input.Value);
                messages.Add("治疗已结算。"); break;
            case ActionKind.SetHealth:
                _actor.Health = Math.Max(0, Math.Min(input.Value, _actor.MaxHealth)); messages.Add("已设置试验生命；此项是测试输入。"); break;
            case ActionKind.OrdinaryShield:
                _actor.Shields.AddOrdinary(input.Value); messages.Add("普通护盾实际增加。"); break;
            case ActionKind.Energy:
                if (_sourcePresent)
                {
                    _energy = (int)Math.Min(_settings.SourceCapacity, (long)_energy + input.Value);
                    messages.Add("阵源获得真实补给，次数与护罩额度保持。");
                }
                else messages.Add("草图没有真实阵源，本次没有增加阵能。");
                break;
            case ActionKind.Observation:
                _observationKnown = input.Flag;
                messages.Add(input.Flag ? "状态输入恢复，保留区间记忆。" : "状态输入未知，不按0或安全处理。"); break;
            case ActionKind.Supply:
                _supplyValid = input.Flag; messages.Add(input.Flag ? "供能来源恢复。" : "供能来源失效，撤去本器护罩。"); break;
            case ActionKind.Enable:
                _enabled = input.Flag; messages.Add(input.Flag ? "重新启用，不重置源、区间记忆或本轮额度。" : "阵法停用，只撤本器护罩，已付费用不退。"); break;
            case ActionKind.NextRound:
                _round = checked(_round + 1);
                foreach (var state in _outputs.Values)
                {
                    if (state.Kind == FormationControlCircuit.NodeKind.GuardOutput)
                    {
                        if (state.Running && _actor.Shields.GetRemaining(state.SourceId) > 0) renewing.Add(state.Id);
                        state.Paid = false; state.Running = false;
                        // 旧段先到期，再读取本段状态；整个Apply仍只通知一次最终总量。
                        _actor.Shields.RemoveSource(state.SourceId);
                    }
                    state.UsedThisRound = 0;
                }
                _baseLoad = 0;
                if (_sourcePresent && _supplyValid) _energy = (int)Math.Min(_settings.SourceCapacity, (long)_energy + _settings.RechargePerRound);
                messages.Add(_sourcePresent && _supplyValid ? "进入新的共同回合：仅一次补给与次数更新，旧护罩段到期。" : "进入新的共同回合：源不存在或已失效，不补能；次数切换，旧护罩到期。"); break;
        }
        FormationControlCircuit.EventFact? fact = input.Kind switch
        {
            ActionKind.Spell => new(input.Id, "spell_completed", input.SourceTag.Length == 0 ? "spell" : input.SourceTag, input.Amplitude),
            ActionKind.Signal => new(input.Id, input.EventName, input.SourceTag, input.Amplitude),
            _ => null
        };
        EvaluateAndCommit(fact, messages, renewing, boundary);
        if (messages.Count == 0) messages.Add("状态已更新，本次没有新增付款或攻击。");
        return new Result(GetSnapshot(), _evaluation.Steps, messages.AsReadOnly(), false);
    }

    private bool Powered(ActuatorState state) => _sourcePresent && _supplyValid && _poweredOutputIds.Contains(state.Id);

    private void BeginWave()
    {
        SyncExhaustion(); _wavePeak = 0;
        foreach (var state in _outputs.Values) state.StartedThisWave = false;
        _baseLoad = _outputs.Values.Where(s => s.Running).Sum(_ => (long)_settings.GuardLoad);
    }

    private void EvaluateAndCommit(FormationControlCircuit.EventFact? fact, List<string> messages,
        IReadOnlySet<string> renewing, bool boundary = false)
    {
        var observations = new Dictionary<string, FormationControlValue>
        {
            ["health"] = _observationKnown && _actor.MaxHealth > 0 ? FormationControlValue.Numeric(100d * _actor.Health / _actor.MaxHealth, FormationValueUnit.Percent) : FormationControlValue.Unknown(FormationSignalType.Number, FormationValueUnit.Percent),
            ["energy"] = _sourcePresent && _supplyValid ? FormationControlValue.Numeric(_energy, FormationValueUnit.Energy) : FormationControlValue.Unknown(FormationSignalType.Number, FormationValueUnit.Energy),
            ["shield"] = FormationControlValue.Numeric(_actor.Shields.Total, FormationValueUnit.Shield),
            ["mana"] = _observationKnown ? FormationControlValue.Numeric(_actor.Mana, FormationValueUnit.Mana) : FormationControlValue.Unknown(FormationSignalType.Number, FormationValueUnit.Mana)
        };
        _evaluation = Circuit.Evaluate(observations, fact);
        var guards = _outputs.Values.Where(s => s.Kind == FormationControlCircuit.NodeKind.GuardOutput).ToArray();
        if (!_enabled || !_actor.IsAlive)
        {
            foreach (var guard in guards) StopGuard(guard);
            messages.Add("阵法或人物资格无效，本次没有输出。"); return;
        }
        // 所有需要停止的服务先撤回，才检查本波的新消费。
        foreach (var guard in guards)
        {
            var permit = _evaluation.GuardRequests[guard.Id];
            if (!Powered(guard) || permit == FormationTruth.False) StopGuard(guard);
        }
        foreach (var guard in guards)
        {
            var permit = _evaluation.GuardRequests[guard.Id];
            if (!Powered(guard)) { messages.Add(Label(guard) + "未接通有效供能，本次没有输出。"); continue; }
            if (!guard.Running && (permit == FormationTruth.True || boundary && renewing.Contains(guard.Id) && permit == FormationTruth.Unknown))
                TryStartGuard(guard, messages, boundary);
        }
        foreach (var request in _evaluation.PulseRequests)
        {
            var output = _outputs[request.NodeId];
            if (!Powered(output)) { messages.Add(Label(output) + "未接通有效供能，本次没有输出。"); continue; }
            if (output.Kind == FormationControlCircuit.NodeKind.AttackOutput) TryPulse(output, messages, false);
            else if (output.Kind == FormationControlCircuit.NodeKind.ShieldOutput) TryPulse(output, messages, true);
        }
        if (fact is not null && _evaluation.PulseRequests.Count == 0)
            messages.Add("本次事件未抵达瞬发出口；已拦的旧事件不会补发。");
    }

    private string Label(ActuatorState state) => Circuit.Nodes[state.Id].Label;
    private bool HasPeak(int demand) => _baseLoad + _wavePeak + demand <= _settings.SourcePeak &&
        _baseLoad + _wavePeak + demand <= _settings.LinkPeak && _baseLoad + _wavePeak + demand <= _settings.BoardPeak;

    private void TryStartGuard(ActuatorState state, List<string> messages, bool replacingExpired)
    {
        if (state.Paid) { messages.Add(Label(state) + "本轮已经取得过护罩，关闭或耗尽后不能刷新容量。"); return; }
        var demand = Math.Max(_settings.GuardCost, _settings.GuardLoad);
        if (_energy < _settings.GuardCost) { messages.Add(Label(state) + "缺能：未付款、未占成功额度。"); return; }
        if (!HasPeak(demand)) { messages.Add(Label(state) + "承载不足：未付款、未占成功额度。"); return; }
        _energy -= _settings.GuardCost; _wavePeak += demand;
        try { _actor.Shields.ReplaceSource(state.SourceId, _settings.GuardCapacity); }
        catch { _energy += _settings.GuardCost; _wavePeak -= demand; throw; }
        state.Paid = true; state.Running = true; state.StartedThisWave = true;
        state.Payments++; state.Executions++; _guardPayments++; _energySpent += _settings.GuardCost;
        messages.Add(replacingExpired ? $"{Label(state)}续段成功，支付{_settings.GuardCost}阵能；新容量{_settings.GuardCapacity}，不叠加。" :
            $"{Label(state)}实际开启，支付{_settings.GuardCost}阵能，取得{_settings.GuardCapacity}防护。");
    }

    private void StopGuard(ActuatorState state)
    {
        _actor.Shields.RemoveSource(state.SourceId);
        if (state.Running && !state.StartedThisWave) _baseLoad = Math.Max(0, _baseLoad - _settings.GuardLoad);
        state.Running = false;
    }

    private void SyncExhaustion()
    {
        foreach (var state in _outputs.Values.Where(s => s.Kind == FormationControlCircuit.NodeKind.GuardOutput && s.Running))
            if (_actor.Shields.GetRemaining(state.SourceId) == 0)
            {
                state.Running = false;
                if (!state.StartedThisWave) _baseLoad = Math.Max(0, _baseLoad - _settings.GuardLoad);
            }
    }

    private void TryPulse(ActuatorState state, List<string> messages, bool shield)
    {
        // 旧内置护持的独立试演profile，不能套用持续罩的容量或费用；不改任何正式资源。
        var cost = shield ? 2 : _settings.AttackCost;
        var effect = shield ? 2 : _settings.AttackDamage;
        var demand = shield ? 2 : Math.Max(_settings.AttackCost, _settings.AttackLoad);
        var limit = shield ? 1 : _settings.AttackLimitPerRound;
        var reserve = shield ? 0 : _settings.AttackReserve;
        if (state.UsedThisRound >= limit) { messages.Add(Label(state) + "本轮次数已用，未扣费。"); return; }
        if (_energy < cost || (long)_energy - cost < reserve)
        { messages.Add(Label(state) + "费用或保底余能不足，未扣费。"); return; }
        if (!HasPeak(demand)) { messages.Add(Label(state) + "共享峰值不足，未扣费。"); return; }
        _energy -= cost; _wavePeak += demand; state.UsedThisRound++;
        var committed = false;
        try
        {
            if (shield) { _actor.Shields.AddOrdinary(effect); committed = true; }
            else committed = _actor.TryAttack(effect);
        }
        finally
        {
            if (!committed) { _energy += cost; _wavePeak -= demand; state.UsedThisRound--; }
        }
        if (!committed) { messages.Add(Label(state) + "未实际生效，已退回预留，不占次数。"); return; }
        state.Payments++; state.Executions++; _energySpent += cost;
        if (!shield) _attacks++;
        messages.Add($"{Label(state)}实际提交，支付{cost}阵能。");
    }

    /// <summary>仅停止本实例的持续来源；不拥有或释放人物，不撤普通盾、不返已付阵能。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_applying) throw new InvalidOperationException("当前阵法结算尚未完成，不能同步销毁运行实例。");
        _applying = true;
        try
        {
            using var notifications = _actor.Shields.DeferNotifications();
            _disposed = true;
            foreach (var state in _outputs.Values.Where(s => s.Kind == FormationControlCircuit.NodeKind.GuardOutput)) StopGuard(state);
        }
        finally { _applying = false; }
    }
}
