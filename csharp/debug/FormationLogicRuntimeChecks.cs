using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GDictionary = Godot.Collections.Dictionary;
using NodeKind = FormationNodeData.NodeKind;
using Effect = FormationNodeData.ActuatorEffect;

/// <summary>隔离检查真实阵图运行器与Godot效果适配器；fixture数字不是配方或平衡值。</summary>
public partial class FormationLogicRuntimeChecks : Node
{
    private const string OldCircuitPath = "res://resources/formations/云息周天护持阵.tres";
    private int _checks;
    private int _groups;

    public override void _Ready() => CallDeferred(nameof(RunChecks));

    private void RunChecks()
    {
        var exitCode = 0;
        try
        {
            Run("definition field mapping", DefinitionMapping);
            Run("existing three-cast resource and counter carry", ExistingResource);
            Run("same occurrence and auto identity isolation", OccurrenceIdentity);
            Run("split/merge dedup through actual runtime", DiamondDedup);
            Run("divider phase carries across owner turns", DividerCarry);
            Run("input and filter source/zero amplitude", SourcesAndAmplitude);
            Run("source/link/board cumulative event peak", SharedPeaks);
            Run("full mana rollback and success quota", ManaRollback);
            Run("negative costs and cycles rejected before execution", InvalidDefinitions);
            GD.Print($"FORMATION_LOGIC_RUNTIME_CHECKS_PASS groups={_groups} checks={_checks}");
        }
        catch (Exception error)
        {
            exitCode = 1;
            GD.PushError("FORMATION_LOGIC_RUNTIME_CHECKS_FAIL " + error);
        }
        finally
        {
            // 所有测试人物和管理器均未入树，Fixture会显式Free；清理短命Resource包装。
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GetTree().Quit(exitCode);
        }
    }

    private void Run(string label, Action test)
    {
        test();
        _groups++;
        GD.Print("FORMATION_LOGIC_RUNTIME_GROUP_PASS " + label);
    }

    private void Check(bool condition, string label)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(label);
    }

    private static int Energy(FormationCircuitRuntime runtime) => runtime.snapshot()["source_energy"].AsInt32();
    private static bool Valid(FormationCircuitRuntime runtime) => runtime.snapshot()["valid"].AsBool();
    private static int Count(FormationCircuitRuntime runtime, string id) =>
        runtime.snapshot()["logic_counts"].AsGodotDictionary()[id].AsInt32();

    private sealed class Fixture : IDisposable
    {
        private readonly List<Resource> _resources = new();
        public Character Owner { get; }
        public BattleManager Battle { get; }

        public Fixture()
        {
            // 仅测试载体，不调用_Ready、load_from_data、AddChild或任何真实战斗入口。
            var human = Keep(new HumanData
            {
                entity_name = "阵法隔离验证人物", current_health = 100, max_health = 100,
                current_mana = 6, max_mana = 6
            });
            Owner = new Character { entity_data = human };
            Battle = new BattleManager { isolated_trial_battle = true };
        }

        public T Keep<T>(T resource) where T : Resource
        {
            _resources.Add(resource);
            return resource;
        }

        public FormationNodeData Part(string id, NodeKind kind, int interval = 3,
            string eventName = "spell_played", string[]? sources = null, int minimumAmplitude = 1,
            Effect effect = Effect.SHIELD, int cost = 2, int peak = 2, int value = 2, bool once = true)
        {
            var part = Keep(new FormationNodeData
            {
                node_id = id, display_name = id, kind = kind, input_event = eventName,
                interval = interval, minimum_amplitude = minimumAmplitude,
                actuator_effect = effect, energy_cost = kind == NodeKind.ACTUATOR ? cost : 0,
                peak_load = kind == NodeKind.ACTUATOR ? peak : 0, effect_value = value,
                once_per_owner_turn = once
            });
            foreach (var source in sources ?? Array.Empty<string>()) part.accepted_source_tags.Add(source);
            return part;
        }

        public FormationCircuitData Circuit(FormationNodeData[] parts, (string From, string To)[] links,
            int capacity = 12, int sourcePeak = 12, int linkPeak = 12, int boardPeak = 12, int recharge = 0)
        {
            var hardware = Keep(new FormationHardwareData
            {
                component_slots = 16, pulse_layers = 1,
                source_capacity = capacity, source_recharge_per_turn = recharge,
                source_peak_output = sourcePeak, spirit_link_peak_output = linkPeak, board_peak_load = boardPeak
            });
            var data = Keep(new FormationCircuitData
            {
                circuit_id = "formation_runtime_fixture", circuit_name = "隔离fixture，非正式成器",
                hardware = hardware
            });
            foreach (var part in parts) data.nodes.Add(part);
            foreach (var link in links)
                data.pulse_wires.Add(Keep(new FormationWireData { from_node_id = link.From, to_node_id = link.To }));
            return data;
        }

        public FormationCircuitRuntime Runtime(FormationCircuitData data) => new(data, Owner, Battle);

        public void Dispose()
        {
            Owner.Free();
            Battle.Free();
            for (var i = _resources.Count - 1; i >= 0; i--)
                if (GodotObject.IsInstanceValid(_resources[i])) _resources[i].Dispose();
            _resources.Clear();
        }
    }

    private void DefinitionMapping()
    {
        using var fixture = new Fixture();
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            var part = fixture.Part("mapping", kind, interval: 3, eventName: "fixture_event",
                sources: new[] { "fixture_source" }, minimumAmplitude: 4);
            var data = fixture.Keep(new FormationCircuitData());
            data.nodes.Add(part);
            var compiled = data.logic_nodes().Single();
            var expectedKind = kind switch
            {
                NodeKind.INPUT => FormationLogicCircuit.NodeKind.Input,
                NodeKind.THRESHOLD => FormationLogicCircuit.NodeKind.Filter,
                NodeKind.COUNTER or NodeKind.DIVIDER => FormationLogicCircuit.NodeKind.Counter,
                NodeKind.SPLITTER => FormationLogicCircuit.NodeKind.Split,
                NodeKind.MERGER => FormationLogicCircuit.NodeKind.Merge,
                NodeKind.ACTUATOR => FormationLogicCircuit.NodeKind.Output,
                _ => throw new InvalidOperationException("未核验的旧元件类型：" + kind)
            };
            Check(compiled.Kind == expectedKind, "旧定义编译为对应核心元件，DIVIDER迁为Counter：" + kind);
            Check(compiled.FirstImmediate == (kind == NodeKind.DIVIDER), "旧DIVIDER保留先发一次，其余默认数满再发：" + kind);
            Check(compiled.Interval == 3 && compiled.MinimumAmplitude == 4 && compiled.EventName == "fixture_event", "配置数值与事件字段对应：" + kind);
            Check(compiled.AcceptedSourceTags.SequenceEqual(new[] { "fixture_source" }), "来源标签字段对应：" + kind);
        }
        var immediateCounter = fixture.Part("immediate_counter", NodeKind.COUNTER);
        immediateCounter.first_immediate = true;
        var immediateData = fixture.Keep(new FormationCircuitData());
        immediateData.nodes.Add(immediateCounter);
        var immediateCompiled = immediateData.logic_nodes().Single();
        Check(immediateCompiled.Kind == FormationLogicCircuit.NodeKind.Counter && immediateCompiled.FirstImmediate, "新Counter显式先发一次配置不因旧DIVIDER迁移丢失");
        Check(fixture.Part("filter_cost", NodeKind.THRESHOLD).complexity_cost() == 1, "筛选器沿现行复杂度1");
        Check(!fixture.Owner.IsInsideTree() && !fixture.Battle.IsInsideTree(), "人物和管理器均未入树");
    }

    private void ExistingResource()
    {
        using var fixture = new Fixture();
        using var original = GD.Load<FormationCircuitData>(OldCircuitPath);
        Check(original is not null, "旧三候资源可加载");
        var data = fixture.Keep((FormationCircuitData)original!.Duplicate(true));
        Check(data.validate_circuit().Count == 0, "旧资源通过新核心校验");
        Check(data.nodes.Single(part => part.kind == NodeKind.COUNTER).interval == 3, "真实旧资源保留三次计数");
        var runtime = fixture.Runtime(data);
        var initial = data.hardware!.effective_source_capacity();
        Check(Valid(runtime) && Energy(runtime) == initial, "读取真实effective容量，不硬套raw容量");
        runtime.emit("spell_played", "spell", 1, "old-one");
        Check(Count(runtime, "third_spell_counter") == 1 && fixture.Owner.shield == 0, "第一次仅计数");
        runtime.emit("spell_played", "spell", 1, "old-one");
        Check(Count(runtime, "third_spell_counter") == 1 && Energy(runtime) == initial, "同一次观测不重复计数");
        runtime.begin_owner_turn();
        Check(Count(runtime, "third_spell_counter") == 1, "行动开始不清计数");
        runtime.emit("spell_played", "spell", 1, "old-two");
        Check(Count(runtime, "third_spell_counter") == 2 && fixture.Owner.shield == 0, "第二次仍不输出");
        runtime.begin_owner_turn();
        runtime.emit("spell_played", "spell", 1, "old-three");
        Check(Count(runtime, "third_spell_counter") == 0 && fixture.Owner.shield == 2, "第三次真实适配器加2盾并归零");
        Check(Energy(runtime) == initial - 2, "只扣旧资源规定的2阵能");
        runtime.emit("spell_played", "spell", 1, "old-three");
        Check(fixture.Owner.shield == 2 && Energy(runtime) == initial - 2, "成功发生重发不二次扣能");
        runtime.begin_owner_turn();
        Check(Energy(runtime) == Math.Min(initial, initial - 2 + data.hardware.effective_source_recharge()), "旧自身行动回复时钟保持");
    }

    private void OccurrenceIdentity()
    {
        using var fixture = new Fixture();
        var data = fixture.Circuit(new[] { fixture.Part("input", NodeKind.INPUT), fixture.Part("out", NodeKind.ACTUATOR, once: false) }, new[] { ("input", "out") });
        var runtime = fixture.Runtime(data);
        Check(Valid(runtime), "身份fixture合法");
        runtime.emit("spell_played", "spell", 1, "event_1");
        runtime.emit("spell_played", "spell");
        Check(fixture.Owner.shield == 4 && Energy(runtime) == 8, "外传event_1不与自动发生身份碰撞");
        runtime.emit("spell_played", "spell", 1, "event_1");
        Check(fixture.Owner.shield == 4 && Energy(runtime) == 8, "外传同发生ID重发仍去重");
        runtime.begin_owner_turn();
        runtime.emit("spell_played", "spell", 1, "event_1");
        Check(fixture.Owner.shield == 4 && Energy(runtime) == 8, "行动更新不复活旧发生ID");
        runtime.emit("spell_played", "spell", 1, "new-fact");
        Check(fixture.Owner.shield == 6 && Energy(runtime) == 6, "同类型同幅值的新发生仍可执行");
    }

    private void DiamondDedup()
    {
        using var fixture = new Fixture();
        var parts = new[]
        {
            fixture.Part("input", NodeKind.INPUT), fixture.Part("split", NodeKind.SPLITTER),
            fixture.Part("left", NodeKind.THRESHOLD), fixture.Part("right", NodeKind.THRESHOLD),
            fixture.Part("merge", NodeKind.MERGER), fixture.Part("counter", NodeKind.COUNTER, interval: 2),
            fixture.Part("out", NodeKind.ACTUATOR, once: false)
        };
        var links = new[] { ("input", "split"), ("split", "left"), ("split", "right"), ("left", "merge"), ("right", "merge"), ("merge", "counter"), ("counter", "out") };
        var data = fixture.Circuit(parts, links);
        var runtime = fixture.Runtime(data);
        Check(Valid(runtime), "分流合流真实运行图合法");
        runtime.emit("spell_played", "spell", 1, "diamond-one");
        Check(Count(runtime, "counter") == 1 && fixture.Owner.shield == 0, "两路汇回只推进一次计数");
        runtime.emit("spell_played", "spell", 1, "diamond-two");
        Check(Count(runtime, "counter") == 0 && fixture.Owner.shield == 2 && Energy(runtime) == 10, "第二个真实事件才输出且只付一份费用");

        using var multiple = new Fixture();
        var twoInputs = multiple.Circuit(new[]
        {
            multiple.Part("a", NodeKind.INPUT), multiple.Part("b", NodeKind.INPUT),
            multiple.Part("merge", NodeKind.MERGER), multiple.Part("out", NodeKind.ACTUATOR, once: false)
        }, new[] { ("a", "merge"), ("b", "merge"), ("merge", "out") });
        var twoRuntime = multiple.Runtime(twoInputs);
        Check(Valid(twoRuntime), "两个匹配输入共用真实发生");
        twoRuntime.emit("spell_played", "spell", 1, "two-sensors-one-fact");
        Check(multiple.Owner.shield == 2 && Energy(twoRuntime) == 10, "一次emit匹配两输入也只输出一次");
    }

    private void DividerCarry()
    {
        using var fixture = new Fixture();
        var data = fixture.Circuit(new[]
        {
            fixture.Part("input", NodeKind.INPUT), fixture.Part("divider", NodeKind.DIVIDER, interval: 3),
            fixture.Part("out", NodeKind.ACTUATOR, once: false)
        }, new[] { ("input", "divider"), ("divider", "out") });
        var runtime = fixture.Runtime(data);
        runtime.emit("spell_played", "spell", 1, "divider-one");
        Check(fixture.Owner.shield == 2 && Count(runtime, "divider") == 1, "分频器先放第1次");
        runtime.begin_owner_turn();
        runtime.emit("spell_played", "spell", 1, "divider-two");
        Check(fixture.Owner.shield == 2 && Count(runtime, "divider") == 2, "跨行动第2次拦下");
        runtime.begin_owner_turn();
        runtime.emit("spell_played", "spell", 1, "divider-three");
        Check(fixture.Owner.shield == 2 && Count(runtime, "divider") == 0, "第3次拦下并绕回位置0");
        runtime.begin_owner_turn();
        runtime.emit("spell_played", "spell", 1, "divider-four");
        Check(fixture.Owner.shield == 4 && Count(runtime, "divider") == 1 && Energy(runtime) == 8, "第4次再放行，相位未被行动更新洗掉");
    }

    private void SourcesAndAmplitude()
    {
        using var fixture = new Fixture();
        var data = fixture.Circuit(new[]
        {
            fixture.Part("input", NodeKind.INPUT, sources: new[] { "spell", "talisman" }),
            fixture.Part("filter", NodeKind.THRESHOLD, sources: new[] { "talisman" }, minimumAmplitude: 1),
            fixture.Part("out", NodeKind.ACTUATOR, once: false)
        }, new[] { ("input", "filter"), ("filter", "out") });
        var runtime = fixture.Runtime(data);
        runtime.emit("card_played", "talisman", 1, "wrong-type");
        runtime.emit("spell_played", "sword", 1, "wrong-input-source");
        runtime.emit("spell_played", "spell", 1, "wrong-filter-source");
        runtime.emit("spell_played", "talisman", 0, "zero-value");
        Check(fixture.Owner.shield == 0 && Energy(runtime) == 12, "事件、输入来源、筛选来源与原零值均正确拦下");
        runtime.emit("spell_played", "talisman", 1, "threshold-equal");
        Check(fixture.Owner.shield == 2 && Energy(runtime) == 10, "恰好达到门槛允许通过");
        runtime.emit("spell_played", "talisman", -1, "negative-value");
        Check(fixture.Owner.shield == 2 && Energy(runtime) == 10, "负幅值明确拒绝且不改费用");
        runtime.emit("spell_played", "talisman", 8, "strong-value");
        Check(fixture.Owner.shield == 4 && Energy(runtime) == 8, "强信号仍只输出固定2盾");
    }

    private void SharedPeaks()
    {
        foreach (var constrained in new[] { "source", "link", "board" })
        {
            using var fixture = new Fixture();
            var data = SplitOutputs(fixture, constrained == "source" ? 3 : 12, constrained == "link" ? 3 : 12, constrained == "board" ? 3 : 12);
            var runtime = fixture.Runtime(data);
            Check(Valid(runtime), "共享峰值fixture合法：" + constrained);
            runtime.emit("spell_played", "spell", 1, "peak-first");
            Check(fixture.Owner.shield == 2 && Energy(runtime) == 10, "同事件合计超限只成功首件：" + constrained);
            runtime.emit("spell_played", "spell", 1, "peak-first");
            Check(fixture.Owner.shield == 2 && Energy(runtime) == 10, "重发不二次使用峰值或扣能：" + constrained);
            runtime.emit("spell_played", "spell", 1, "peak-second");
            Check(fixture.Owner.shield == 7 && Energy(runtime) == 8, "新事件释放瞬时预算，先前失败件未耗成功次数：" + constrained);
        }
        using var enough = new Fixture();
        var enoughRuntime = enough.Runtime(SplitOutputs(enough, 4, 4, 4));
        enoughRuntime.emit("spell_played", "spell", 1, "both-fit");
        Check(enough.Owner.shield == 7 && Energy(enoughRuntime) == 8, "预算足够的不同输出不被全局去重吞掉");
    }

    private static FormationCircuitData SplitOutputs(Fixture fixture, int sourcePeak, int linkPeak, int boardPeak) => fixture.Circuit(new[]
    {
        fixture.Part("input", NodeKind.INPUT), fixture.Part("split", NodeKind.SPLITTER),
        fixture.Part("a", NodeKind.ACTUATOR, value: 2), fixture.Part("b", NodeKind.ACTUATOR, value: 5)
    }, new[] { ("input", "split"), ("split", "a"), ("split", "b") }, sourcePeak: sourcePeak, linkPeak: linkPeak, boardPeak: boardPeak);

    private void ManaRollback()
    {
        using var fixture = new Fixture();
        var data = fixture.Circuit(new[]
        {
            fixture.Part("input", NodeKind.INPUT), fixture.Part("out", NodeKind.ACTUATOR, effect: Effect.RESTORE_MANA)
        }, new[] { ("input", "out") });
        var runtime = fixture.Runtime(data);
        runtime.emit("spell_played", "spell", 1, "mana-full");
        Check(fixture.Owner.current_mana == 6 && Energy(runtime) == 12, "满法力实际失败回滚费用");
        fixture.Owner.current_mana = 3;
        runtime.emit("spell_played", "spell", 1, "mana-room");
        Check(fixture.Owner.current_mana == 5 && Energy(runtime) == 10, "先前失败没占成功次数，新事件仍可恢复");
        fixture.Owner.current_mana = 3;
        runtime.emit("spell_played", "spell", 1, "mana-quota-used");
        Check(fixture.Owner.current_mana == 3 && Energy(runtime) == 10, "成功后原行动限次仍阻止再次输出");
        runtime.begin_owner_turn();
        runtime.emit("spell_played", "spell", 1, "mana-next-turn");
        Check(fixture.Owner.current_mana == 5 && Energy(runtime) == 8, "旧自身行动开始才清原成功次数");
    }

    private void InvalidDefinitions()
    {
        using var negative = new Fixture();
        var badCost = negative.Circuit(new[] { negative.Part("input", NodeKind.INPUT), negative.Part("out", NodeKind.ACTUATOR, cost: -1) }, new[] { ("input", "out") });
        Check(badCost.validate_circuit().Any(error => error.Contains("费用")), "负费用在定义校验阶段拒绝");
        var negativeRuntime = negative.Runtime(badCost);
        negativeRuntime.emit("spell_played", "spell");
        Check(!Valid(negativeRuntime) && negative.Owner.shield == 0 && Energy(negativeRuntime) == 12, "非法负费用不产生阵能或半份效果");

        using var cyclic = new Fixture();
        var cycle = cyclic.Circuit(new[]
        {
            cyclic.Part("input", NodeKind.INPUT), cyclic.Part("out", NodeKind.ACTUATOR),
            cyclic.Part("left", NodeKind.THRESHOLD), cyclic.Part("right", NodeKind.THRESHOLD)
        }, new[] { ("input", "out"), ("left", "right"), ("right", "left") });
        Check(cycle.validate_circuit().Any(error => error.Contains("回路")), "即使另有可达输出，独立环仍预先拒绝");
        var cyclicRuntime = cyclic.Runtime(cycle);
        cyclicRuntime.emit("spell_played", "spell");
        Check(!Valid(cyclicRuntime) && cyclic.Owner.shield == 0 && Energy(cyclicRuntime) == 12, "非法图不执行前半段或扣费");

        using var interval = new Fixture();
        var badInterval = interval.Circuit(new[]
        {
            interval.Part("input", NodeKind.INPUT), interval.Part("counter", NodeKind.COUNTER, interval: 0), interval.Part("out", NodeKind.ACTUATOR)
        }, new[] { ("input", "counter"), ("counter", "out") });
        Check(badInterval.validate_circuit().Count > 0 && !Valid(interval.Runtime(badInterval)), "零间隔不被悄悄修正为1");
    }
}
