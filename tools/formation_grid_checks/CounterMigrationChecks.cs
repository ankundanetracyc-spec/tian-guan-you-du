using System.Text.Json.Nodes;
using Grid = FormationGridModel;
using Logic = FormationLogicCircuit;
using Cell = FormationGridModel.Cell;
using Kind = FormationGridModel.Kind;
using NodeKind = FormationLogicCircuit.NodeKind;

internal static class CounterMigrationChecks
{
    public static void Run(Action<string, Action> run, Action<bool, string> check)
    {
        run("legacy counter aliases preserve numeric identity and geometry", () => Aliases(check));
        run("legacy and explicit start modes emit identical occurrences", () => EquivalentPhases(check));
        run("old draft modes migrate once without losing interval or identity", () => DraftMigration(check));
        run("new counter mode persistence rejects malformed flags atomically", () => ModePersistence(check));
    }

    private static Logic Circuit(NodeKind kind, int interval, bool firstImmediate = false) => new(new[]
    {
        new Logic.Node("input", NodeKind.Input),
        new Logic.Node("count", kind) { Interval = interval, FirstImmediate = firstImmediate },
        new Logic.Node("output", NodeKind.Output)
    }, new[] { new Logic.Edge("input", "count"), new Logic.Edge("count", "output") });

    private static JsonObject Component(JsonObject root, string id) => root["components"]!.AsArray()
        .Select(node => node!.AsObject()).Single(node => node["id"]!.GetValue<string>() == id);

    private static void Aliases(Action<bool, string> check)
    {
        check((int)NodeKind.Counter == 2 && (int)NodeKind.Divider == 3 && (int)NodeKind.Output == 6,
            "logic enum numbers remain compatible with old resource records");
        check((int)Kind.Counter == 4 && (int)Kind.Rhythm == 6 && (int)Kind.Merge == 8,
            "grid enum numbers remain unchanged");
        check(Grid.Name(Kind.Counter) == "计数器" && Grid.Name(Kind.Rhythm) == "计数器", "legacy type no longer advertises a separate player item");
        foreach (var rotation in Enumerable.Range(0, 4))
        {
            var legacy = new Grid.Component("old", Kind.Rhythm, new Cell(2, 1), rotation);
            var canonical = new Grid.Component("new", Kind.Counter, new Cell(2, 1), rotation);
            check(legacy.Cells.SequenceEqual(canonical.Cells) && legacy.Ports.SequenceEqual(canonical.Ports) && legacy.Complexity == canonical.Complexity,
                "old and canonical types use exactly the same rotated physical part");
            var model = new Grid();
            check(model.TryPlaceComponent(Kind.Rhythm, new Cell(2, 1), rotation, "old-part"), "legacy placement remains accepted");
            var part = model.Components["old-part"];
            check(part.Kind == Kind.Counter && part.Settings.FirstImmediate && part.Settings.Interval == 2,
                "legacy placement is immediately canonical with old default interval and first emission");
        }
        var example = Grid.CreateLogicExample(Kind.Rhythm);
        check(example.IsCircuitConnected && example.Components["logic"].Kind == Kind.Counter && example.Components["logic"].Settings.FirstImmediate,
            "legacy example request makes one canonical counter rather than another item");
        var oldLogic = Circuit(NodeKind.Divider, 3);
        check(oldLogic.Nodes["count"].Kind == NodeKind.Counter && oldLogic.Nodes["count"].FirstImmediate,
            "compiled legacy divider nodes become canonical counter nodes");
    }

    private static void EquivalentPhases(Action<bool, string> check)
    {
        foreach (var interval in Enumerable.Range(1, 12))
        {
            var old = Circuit(NodeKind.Divider, interval);
            var migrated = Circuit(NodeKind.Counter, interval, true);
            for (var occurrence = 1; occurrence <= 2 * interval + 1; occurrence++)
            {
                var signal = new Logic.Signal("event-" + occurrence, "spell_played", "paid", occurrence);
                var previous = old.Emit(signal);
                var current = migrated.Emit(signal);
                check(previous.OutputNodeIds.SequenceEqual(current.OutputNodeIds) && previous.Signal == current.Signal,
                    "legacy divider and first-immediate counter emit the same observed occurrences");
                check(old.GetCount("count") == migrated.GetCount("count"), "legacy and new mode retain equal progress between emissions");
                check(old.Emit(signal).Duplicate && migrated.Emit(signal).Duplicate, "migration preserves duplicate occurrence suppression");
            }
            old.Reset(); migrated.Reset();
            var restart = new Logic.Signal("event-1", "spell_played", "paid", 1);
            check(old.Emit(restart).OutputNodeIds.Count == 1 && migrated.Emit(restart).OutputNodeIds.Count == 1,
                "reset restores the same legacy/new first-immediate phase");
        }
    }

    private static void DraftMigration(Action<bool, string> check)
    {
        foreach (var rotation in Enumerable.Range(0, 4))
            foreach (var interval in new[] { 2, 3, 12 })
                foreach (var legacyKind in new[] { "Counter", "Rhythm" })
                {
                    var source = new Grid();
                    check(source.TryPlaceComponent(Kind.Counter, new Cell(2, 1), rotation, "stable-old-id"), "make valid old draft geometry");
                    check(source.TryConfigureComponent("stable-old-id", new Grid.Settings { Interval = interval }), "make an explicit nondefault interval when needed");
                    var root = JsonNode.Parse(source.Serialize())!.AsObject();
                    var part = Component(root, "stable-old-id");
                    part["kind"] = legacyKind;
                    if (part["settings"] is JsonObject settings) settings.Remove("first_immediate");
                    check(Grid.TryDeserialize(root.ToJsonString(), out var migrated) && migrated is not null, "old optional settings record remains readable");
                    var component = migrated!.Components["stable-old-id"];
                    check(component.Kind == Kind.Counter && component.Settings.FirstImmediate == (legacyKind == "Rhythm"), "old kind determines its original start mode");
                    check(component.Id == "stable-old-id" && component.Settings.Interval == interval && component.Rotation == rotation && component.Origin == new Cell(2, 1),
                        "migration preserves component identity, configured interval, position and rotation");
                    var canonical = migrated.Serialize();
                    var savedPart = Component(JsonNode.Parse(canonical)!.AsObject(), "stable-old-id");
                    check(savedPart["kind"]!.GetValue<string>() == "Counter", "all newly written draft types use Counter");
                    check(Grid.TryDeserialize(canonical, out var again) && again!.Serialize() == canonical, "migration is idempotent on subsequent save/load");
                }
        var legacyWithFalse = JsonNode.Parse(Grid.CreateLogicExample(Kind.Counter, true).Serialize())!.AsObject();
        var legacyPart = Component(legacyWithFalse, "logic");
        legacyPart["kind"] = "Rhythm";
        legacyPart["settings"]!["first_immediate"] = false;
        check(Grid.TryDeserialize(legacyWithFalse.ToJsonString(), out var alias) && alias!.Components["logic"].Settings.FirstImmediate,
            "legacy Rhythm always means its historical first-immediate mode, even with a contradictory optional flag");
    }

    private static void ModePersistence(Action<bool, string> check)
    {
        foreach (var firstImmediate in new[] { false, true })
        {
            var model = Grid.CreateLogicExample(Kind.Counter, firstImmediate);
            var settings = new Grid.Settings { Interval = 4, FirstImmediate = firstImmediate };
            check(model.TryConfigureComponent("logic", settings), "both modes can be selected on one actual counter");
            check(Grid.TryDeserialize(model.Serialize(), out var restored) && restored!.Components["logic"].Settings == settings,
                "saved counter restores the configured start mode and interval");
            var simulation = new FormationGridSimulation(restored!);
            var outputs = new List<int>();
            for (var occurrence = 1; occurrence <= 8; occurrence++)
                if (simulation.Send("spell_played", "paid", 1).OutputNodeIds.Count > 0) outputs.Add(occurrence);
            check(outputs.SequenceEqual(firstImmediate ? new[] { 1, 5 } : new[] { 4, 8 }), "restored mode drives the actual rotated physical circuit");
            var before = restored!.Serialize();
            var settingsBefore = restored.Components["logic"].Settings;
            check(!restored.TryMoveComponent("logic", new Cell(0, 1)) && restored.Serialize() == before && restored.Components["logic"].Settings == settingsBefore,
                "a refused geometry edit does not erase mode or interval");
            simulation.Send("spell_played", "paid", 1);
            check(restored.Serialize() == before, "trial progress is never written as a new start mode");
        }

        var valid = Grid.CreateLogicExample(Kind.Counter, true);
        var original = valid.Serialize();
        string Change(JsonNode? value)
        {
            var root = JsonNode.Parse(original)!.AsObject();
            Component(root, "logic")["settings"]!["first_immediate"] = value;
            return root.ToJsonString();
        }
        var invalid = new[]
        {
            Change(null), Change(JsonValue.Create(1)), Change(JsonValue.Create("true")), Change(new JsonObject()),
            original.Replace("\"first_immediate\":true", "\"first_immediate\":true,\"first_immediate\":true", StringComparison.Ordinal)
        };
        foreach (var json in invalid)
        {
            Grid? candidate = valid;
            check(!Grid.TryDeserialize(json, out candidate) && candidate is null, "malformed or repeated mode field is refused before exposing a model");
            check(valid.Serialize() == original, "bad mode record cannot mutate an existing draft");
        }
        var ordinary = Grid.CreateStarter();
        var beforeOrdinary = ordinary.Serialize();
        check(!ordinary.TryConfigureComponent("sensor", new Grid.Settings { FirstImmediate = true }) && ordinary.Serialize() == beforeOrdinary,
            "unrelated components cannot acquire an undeclared counting mode");
    }
}
