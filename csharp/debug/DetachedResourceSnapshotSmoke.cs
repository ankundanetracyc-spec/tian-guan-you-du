using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class DetachedResourceSnapshotSmoke : Node
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static IEnumerable<(string Name, object? Value)> Exports(Resource resource)
    {
        foreach (var field in resource.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (field.IsDefined(typeof(ExportAttribute), true)) yield return (field.Name, field.GetValue(resource));
        foreach (var property in resource.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.IsDefined(typeof(ExportAttribute), true)) yield return (property.Name, property.GetValue(resource));
    }
    private static void Compare(object? before, object? after, string path, Dictionary<object, object> identities)
    {
        if (before is Variant a) before = a.Obj;
        if (after is Variant b) after = b.Obj;
        if (before is null || after is null) { Check(before is null && after is null, path + " null mismatch"); return; }
        if (before is Resource resource)
        {
            Check(after is Resource && before.GetType() == after.GetType(), path + " resource type mismatch");
            if (identities.TryGetValue(before, out var known)) { Check(ReferenceEquals(known, after), path + " alias identity changed"); return; }
            identities.Add(before, after);
            Check(!ReferenceEquals(before, after), path + " live resource leaked");
            if (resource.GetType().Assembly != typeof(DetachedResourceSnapshot).Assembly && !string.IsNullOrEmpty(resource.ResourcePath) && !resource.ResourcePath.Contains("::") && resource.ResourcePath.StartsWith("res://"))
            { Check(((Resource)after).ResourcePath == resource.ResourcePath, path + " external dependency changed"); return; }
            var otherFields = Exports((Resource)after).ToDictionary(item => item.Name, item => item.Value);
            foreach (var item in Exports(resource)) Compare(item.Value, otherFields[item.Name], path + "." + item.Name, identities);
            return;
        }
        if (before is Godot.Collections.Dictionary dictionary)
        {
            Check(after is Godot.Collections.Dictionary other && dictionary.Count == other.Count, path + " dictionary mismatch");
            var actual = (Godot.Collections.Dictionary)after;
            foreach (var item in dictionary)
            { Check(actual.ContainsKey(item.Key), path + " dictionary key missing"); Compare(item.Value, actual[item.Key], path + "[" + item.Key + "]", identities); }
            return;
        }
        if (before is StringName or NodePath) { Check(before.ToString() == after.ToString(), path + " name mismatch"); return; }
        if (before is IEnumerable enumerable && before is not string)
        {
            Check(after is IEnumerable, path + " sequence type mismatch");
            var left = enumerable.Cast<object?>().ToArray(); var right = ((IEnumerable)after).Cast<object?>().ToArray();
            Check(left.Length == right.Length, path + " sequence length mismatch");
            for (var index = 0; index < left.Length; index++)
            {
                var item = left[index]; var actual = right[index];
                if (item?.GetType().IsGenericType == true && item.GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    foreach (var property in item.GetType().GetProperties())
                        Compare(property.GetValue(item), property.GetValue(actual), path + "[" + index + "]." + property.Name, identities);
                }
                else Compare(item, actual, path + "[" + index + "]", identities);
            }
            return;
        }
        Check(before.Equals(after), path + " value mismatch: " + before + " vs " + after);
    }

    public override async void _Ready()
    {
        try
        {
            var cache = new DetachedResourceSnapshot.CaptureCache();
            var warmupWatch = Stopwatch.StartNew();
            DetachedResourceSnapshot.WarmUp();
            GD.Print($"DETACHED_WARMUP schema_ms={warmupWatch.Elapsed.TotalMilliseconds:F2}");
            var record = new NpcActionRecord { action_id = "work", month_index = 2, outcome = "original", resource_delta = 4 };
            var probe = new DetachedSnapshotProbeData
            {
                records = new() { ["shared"] = record }, payload = Variant.From(record),
                external = GD.Load<Resource>("res://resources/entities/items/灵石.tres"),
                mixed = [Variant.From(record), Variant.From(new NodePath("chapter/one")), Variant.From(new StringName("name")), default],
            };
            Check(probe.external is not null, "Static dependency fixture missing");
            ((ItemData)probe.external!).entity_name = "External custom resource modified before capture";
            probe.mixed.Add(GD.Load<Texture2D>("res://textures/items/item_spirit_stone.png"));
            var npc = new WorldNpcData { npc_id = "snapshot_probe", action_history = [record], player_impression = new() { pending = [record], spar_evidence = record } };
            var source = new PersistentGameSave
            {
                player_data = new HumanData { entity_name = "snapshot", inventory_item_paths = ["herb"], inventory_item_counts = [7],
                    learned_gongfa_chapters = new() { ["book"] = new Godot.Collections.Array { "chapter", 4 } } },
                world_npcs = [npc], scene_progress = new() { ["probe"] = probe }, explored_map_cells = [1, 8, 2],
                dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(72),
            };
            var expected = (PersistentGameSave)source.Duplicate(true);
            var watch = Stopwatch.StartNew();
            var snapshot = DetachedResourceSnapshot.Capture(source, cache);
            GD.Print($"DETACHED_CAPTURE small_after_warmup_ms={watch.Elapsed.TotalMilliseconds:F2}");
            var unmutated = snapshot.Materialize();
            Compare(expected, unmutated, "in_memory", new(ReferenceEqualityComparer.Instance));
            // Keep both old and revised snapshots; a later cache refresh must not rewrite the old one.
            record.outcome = "revised"; record.resource_delta = 9;
            var revised = DetachedResourceSnapshot.Capture(source, cache);
            var revisedExpected = (PersistentGameSave)source.Duplicate(true);
            var directory = "user://detached_snapshot_smoke_" + Guid.NewGuid().ToString("N");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
            var path = directory + "/old.tres";
            var worker = Task.Run(() =>
            {
                var materialized = snapshot.Materialize();
                var error = ResourceSaver.Save(materialized, path);
                if (error != Error.Ok) throw new InvalidOperationException("Worker save failed: " + error);
                return materialized;
            });
            // Mutations happen after capture while the worker rebuilds and serializes its detached graph.
            record.outcome = "live mutation"; record.month_index = 99;
            npc.action_history.Add(new NpcActionRecord { action_id = "new_live_record" });
            probe.bytes[0] = 99; probe.numbers.Add(42); probe.records.Clear();
            source.player_data!.inventory_item_counts[0] = 99;
            var written = await worker;
            Compare(expected, written, "worker", new(ReferenceEqualityComparer.Instance));
            Compare(revisedExpected, revised.Materialize(), "cache_revision", new(ReferenceEqualityComparer.Instance));
            var replacement = new NpcActionRecord { action_id = "same_count_replacement", outcome = "replaced" };
            npc.action_history[0] = replacement;
            var replaced = (PersistentGameSave)DetachedResourceSnapshot.Capture(source, cache).Materialize();
            Check(replaced.world_npcs[0].action_history[0].action_id == "same_count_replacement", "Cache missed same-count record replacement");
            Compare(expected, snapshot.Materialize(), "old_snapshot_after_replacement", new(ReferenceEqualityComparer.Instance));
            var loaded = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep)!;
            Check(loaded is not null, "Real file roundtrip failed");
            Compare(expected, loaded, "disk", new(ReferenceEqualityComparer.Instance));
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(directory));
            cache.Clear();
            GD.Print("DETACHED_ROUNDTRIP PASS: inherited Export fields/properties, typed containers, Variant, null, aliases, packed data, static dependency, cache edits, concurrent live mutation and real disk reload.");

            var population = new PersistentGameSave { player_data = new HumanData() };
            for (var index = 0; index < 580; index++)
            {
                var person = new WorldNpcData { npc_id = "perf_" + index };
                for (var month = 0; month < 120; month++) person.action_history.Add(new NpcActionRecord
                { month_index = month, action_id = "work", date_label = "month " + month, outcome = "2 灵石", resource_delta = 2 });
                population.world_npcs.Add(person);
            }
            for (var pass = 0; pass < 3; pass++)
            {
                watch.Restart();
                var detached = DetachedResourceSnapshot.Capture(population, cache);
                watch.Stop();
                GD.Print($"DETACHED_CAPTURE npcs=580 months=120 records=69600 pass={pass} capture_ms={watch.Elapsed.TotalMilliseconds:F2}");
                Check(detached is not null, "Capture returned null");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var largeSnapshot = DetachedResourceSnapshot.Capture(population, cache);
            var largeDirectory = "user://detached_large_smoke_" + Guid.NewGuid().ToString("N");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(largeDirectory));
            var largePath = largeDirectory + "/large.tres";
            watch.Restart();
            var largeWorker = Task.Run(() =>
            {
                var rebuilt = largeSnapshot.Materialize();
                var error = ResourceSaver.Save(rebuilt, largePath);
                if (error != Error.Ok) throw new InvalidOperationException("Large worker save failed: " + error);
                return rebuilt;
            });
            var frames = 0;
            while (!largeWorker.IsCompleted) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); frames++; }
            var largeResult = await largeWorker;
            watch.Stop();
            Compare(population, largeResult, "large_worker", new(ReferenceEqualityComparer.Instance));
            Check(frames > 1, "Large worker blocked frame processing");
            GD.Print($"DETACHED_WORKER npcs=580 records=69600 total_ms={watch.Elapsed.TotalMilliseconds:F2} main_process_frames={frames}");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(largePath));
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(largeDirectory));
            cache.Clear();
            GD.Print("DETACHED_RESOURCE_SNAPSHOT PASS");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
