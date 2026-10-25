using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Read-only catalogue loading, cancellation, thumbnail and row-reuse regression.</summary>
public partial class EncyclopediaLoadingSmoke : Node
{
    private int _processFrames;
    public override void _Process(double delta) => _processFrames++;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 1) { for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static object? Read(object value, string member)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return value.GetType().GetField(member, flags)?.GetValue(value) ?? value.GetType().GetProperty(member, flags)?.GetValue(value);
    }
    private static IDictionary Buttons(EncyclopediaPanel panel) => (IDictionary)Read(panel, "_entryButtons")!;
    private static IDictionary Catalog(EncyclopediaPanel panel) => (IDictionary)Read(panel, "_entries")!;
    private static List<object> Entries(EncyclopediaPanel panel, string section)
    {
        foreach (DictionaryEntry pair in Catalog(panel))
            if (pair.Key.ToString() == section) return ((IEnumerable)pair.Value!).Cast<object>().ToList();
        return [];
    }
    private static void Open(EncyclopediaPanel panel, string section)
    {
        if (section == "Statuses") panel.show_panel();
        else if (section == "Beasts") panel.show_beasts();
        else panel.FindChild("TabItems", true, false)!.EmitSignal(BaseButton.SignalName.Pressed);
    }
    private static ulong[] RowIds(EncyclopediaPanel panel) => Buttons(panel).Values.Cast<Button>().Select(button => button.GetInstanceId()).OrderBy(id => id).ToArray();
    private static int VisibleRows(EncyclopediaPanel panel) => Buttons(panel).Values.Cast<Button>().Count(button => button.Visible);
    private static string PresentationStamp(EncyclopediaPanel panel) =>
        string.Join("|", Catalog(panel).Keys.Cast<object>().Select(key => key.ToString()).OrderBy(key => key)) + ";" +
        string.Join(",", RowIds(panel)) + ";" + ((Label)Read(panel, "_detailTitle")!).Text + ";" + ((Label)Read(panel, "_entryCount")!).Text;
    private EncyclopediaPanel NewPanel() { var panel = new EncyclopediaPanel(); AddChild(panel); return panel; }
    private async Task<int> WaitLoaded(EncyclopediaPanel panel)
    {
        var frames = 0;
        while (panel.IsCatalogLoading && frames < 3000)
        {
            Check(panel.Visible, "Loading unexpectedly hid the encyclopedia");
            await Frames(); frames++;
        }
        Check(!panel.IsCatalogLoading, "Encyclopedia loading timed out");
        return frames;
    }
    private static IEnumerable<string> ResourcePaths(string directory)
    {
        using var access = DirAccess.Open(directory);
        if (access is null) yield break;
        foreach (var file in access.GetFiles()) if (file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)) yield return directory + "/" + file;
        foreach (var child in access.GetDirectories()) foreach (var path in ResourcePaths(directory + "/" + child)) yield return path;
    }

    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            var npcs = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            var initial = (clock.year, clock.month, clock.day, clock.hour, clock.get_pending_hours(), clock.is_paused,
                saves.active_slot_id, saves.active_slot_name, npcs.npcs.Count, npcs.get_last_processed_month(), npcs.population_version, npcs.simulation_enabled);
            var timeSignals = 0; var npcSignals = 0;
            void TimeChanged() => timeSignals++;
            void NpcsChanged() => npcSignals++;
            clock.time_changed += TimeChanged; npcs.world_npcs_changed += NpcsChanged;

            var panel = NewPanel();
            foreach (var section in new[] { "Statuses", "Items", "Beasts" })
            {
                var ticks = _processFrames;
                var watch = Stopwatch.StartNew();
                Open(panel, section);
                var immediateMs = watch.Elapsed.TotalMilliseconds;
                Check(panel.Visible && panel.IsCatalogLoading, "Cold open must display before loading: " + section);
                Check(immediateMs < 100, "Cold open blocked before first frame: " + section + " " + immediateMs);
                var frames = await WaitLoaded(panel);
                Check(frames >= 1 && _processFrames > ticks, "Loading failed to yield responsive process frames: " + section);
                var count = Entries(panel, section).Count;
                Check(count > 0 && VisibleRows(panel) == count, "Loaded row count mismatch: " + section);
                GD.Print($"ENCYCLOPEDIA_LOADING section={section} immediate_ms={immediateMs:F2} ready_ms={watch.Elapsed.TotalMilliseconds:F2} frames={frames} max_slice_ms={panel.LastCatalogMaxSliceMilliseconds:F2} entries={count}");
            }
            var statusCount = Entries(panel, "Statuses").Count;
            var itemCount = Entries(panel, "Items").Count;
            var beastCount = Entries(panel, "Beasts").Count;
            GD.Print($"ENCYCLOPEDIA_COUNTS statuses={statusCount} items={itemCount} beasts={beastCount} baseline=23/69/59");
            Check(statusCount > 0 && beastCount > 0, "Catalog unexpectedly empty");
            var statusEntries = Entries(panel, "Statuses");
            Check(!statusEntries.Any(entry => (string)Read(entry, "Title")! == "散血"), "Mechanic-only 散血 must not appear in the encyclopedia");
            foreach (var (title, expectedIcon) in new[] { ("疲倦", "疲倦_v1.png"), ("剑意", "剑意_v1.png") })
            {
                var statusEntry = statusEntries.SingleOrDefault(entry => (string)Read(entry, "Title")! == title);
                Check(statusEntry is not null, "Missing encyclopedia status: " + title);
                var icon = Read(statusEntry!, "Icon") as Texture2D;
                Check(icon is not null && icon.ResourcePath.EndsWith(expectedIcon, StringComparison.Ordinal), "Wrong status icon: " + title);
                Check(icon!.GetWidth() == 1254 && icon.GetHeight() == 1254, "Unexpected status icon dimensions: " + title);
            }
            GD.Print("ENCYCLOPEDIA_STATUS fatigue/sword_intent_icons and scattered_blood_excluded PASS");

            var eligibleItems = new HashSet<string>(StringComparer.Ordinal);
            var excluded = 0;
            foreach (var path in ResourcePaths("res://resources/entities/items"))
            {
                var item = ResourceLoader.Load<ItemData>(path);
                if (item is null || string.IsNullOrWhiteSpace(item.entity_name)) continue;
                if (!string.IsNullOrWhiteSpace(item.icon_path) && ResourceLoader.Exists(item.icon_path) && ResourceLoader.Load<Texture2D>(item.icon_path) is not null) eligibleItems.Add(path);
                else excluded++;
            }
            var actualItems = Entries(panel, "Items").Select(entry => ((ItemData)Read(entry, "Item")!).ResourcePath).ToHashSet(StringComparer.Ordinal);
            Check(actualItems.SetEquals(eligibleItems) && itemCount == eligibleItems.Count, "Item catalog differs from real dedicated-icon eligibility");
            foreach (var entry in Entries(panel, "Items")) Check(Read(entry, "Icon") is Texture2D, "Item without dedicated icon was admitted");
            GD.Print($"ENCYCLOPEDIA_ITEMS eligible={eligibleItems.Count} excluded_missing_icon={excluded} PASS");

            foreach (var entry in Entries(panel, "Beasts"))
            {
                var icon = (Texture2D)Read(entry, "Icon")!;
                Check(icon.GetWidth() <= 144 && icon.GetHeight() <= 96, "Oversized catalogue portrait: " + Read(entry, "Title"));
                var path = (string)Read(entry, "PortraitPath")!;
                var thumbPath = $"res://textures/ui/encyclopedia/portraits/{path.Sha256Text()[..16]}.png";
                Check(ResourceLoader.Exists(thumbPath), "Pre-generated portrait missing: " + path);
                ((Button)Buttons(panel)[entry]!).EmitSignal(BaseButton.SignalName.Pressed);
                var detail = ((TextureRect)Read(panel, "_detailIcon")!).Texture;
                var preview = (Sprite2D?)Read(panel, "_beastPreviewFallback");
                Check(detail is not null && detail.ResourcePath == path, "Detail did not load source portrait: " + path);
                Check(preview?.Texture?.ResourcePath == path, "Beast preview used thumbnail instead of source: " + path);
            }
            GD.Print($"ENCYCLOPEDIA_PORTRAITS entries={beastCount} thumbnail_bounds=144x96 original_detail=all PASS");

            var stableRows = RowIds(panel);
            foreach (var section in new[] { "Statuses", "Items", "Beasts", "Statuses", "Beasts" })
            { Open(panel, section); await WaitLoaded(panel); Check(stableRows.SequenceEqual(RowIds(panel)), "Category switch rebuilt catalog buttons"); }
            var wolf = Entries(panel, "Beasts").First(entry => ((string)Read(entry, "Title")!).Contains("狼", StringComparison.Ordinal));
            ((Button)Buttons(panel)[wolf]!).EmitSignal(BaseButton.SignalName.Pressed);
            var search = (LineEdit)Read(panel, "_search")!;
            foreach (var query in new[] { "狼", "__百科无匹配的测试词__", "" })
            {
                search.Text = query;
                search.EmitSignal(LineEdit.SignalName.TextChanged, query);
                await Frames();
                Check(stableRows.SequenceEqual(RowIds(panel)) && Buttons(panel).Values.Cast<Button>().All(button => GodotObject.IsInstanceValid(button) && !button.IsQueuedForDeletion()), "Search added or destroyed existing row buttons");
                if (query == "狼") Check(VisibleRows(panel) > 0 && ReferenceEquals(Read(panel, "_selectedEntry"), wolf), "Chinese search lost retained selection");
                if (query.StartsWith("__", StringComparison.Ordinal)) Check(VisibleRows(panel) == 0 && ((Label)Read(panel, "_detailTitle")!).Text == "暂无生灵", "Empty search did not clear results");
                if (query.Length == 0) Check(VisibleRows(panel) == beastCount && ReferenceEquals(Read(panel, "_selectedEntry"), wolf), "Clearing search lost retained selection");
            }
            panel.hide_panel();
            panel.show_beasts();
            await WaitLoaded(panel); await Frames(45);
            Check(panel.Visible && !panel.IsCatalogLoading, "Old close tween hid a rapidly reopened panel");
            panel.hide_panel(); await Frames(45); Check(!panel.Visible, "Animated close did not hide encyclopedia");
            panel.QueueFree(); await Frames(2);
            GD.Print("ENCYCLOPEDIA_REUSE search_chinese/empty/clear category_switch selection close_reopen PASS");

            var rapid = NewPanel();
            rapid.show_panel(); rapid.show_beasts(); Open(rapid, "Items");
            await WaitLoaded(rapid); await Frames(4);
            Check(Read(rapid, "_current")!.ToString() == "Items" && Entries(rapid, "Items").Count == itemCount, "Rapid category switch committed stale catalog");
            Check(Catalog(rapid).Count == 1 && Entries(rapid, "Statuses").Count == 0 && Entries(rapid, "Beasts").Count == 0, "Cancelled category loader wrote back after replacement");
            rapid.QueueFree(); await Frames(2);

            foreach (var cancellation in new[] { "animated_close", "direct_hide", "exit_tree" })
            {
                var cancelled = NewPanel(); cancelled.show_beasts();
                Check(cancelled.IsCatalogLoading, "Cancellation fixture completed before first yield");
                if (cancellation == "animated_close") cancelled.hide_panel();
                else if (cancellation == "direct_hide") cancelled.Hide();
                else RemoveChild(cancelled);
                Check(!cancelled.IsCatalogLoading, "Cancel did not clear loading flag: " + cancellation);
                var stamp = PresentationStamp(cancelled);
                await Frames(45);
                Check(PresentationStamp(cancelled) == stamp && Catalog(cancelled).Count == 0 && Buttons(cancelled).Count == 0, "Cancelled loader wrote into abandoned presentation: " + cancellation);
                if (cancellation != "exit_tree") Check(!cancelled.Visible, "Cancelled panel became visible: " + cancellation);
                if (cancelled.IsInsideTree()) cancelled.QueueFree(); else cancelled.Free();
                await Frames(2);
            }
            // Cancel after the real resource pass has finished and row construction
            // has started, as well as the immediate pre-resource cancellations above.
            foreach (var cancellation in new[] { "animated_close", "direct_hide", "exit_tree" })
            {
                var cancelled = NewPanel(); Open(cancelled, "Items");
                var loadingFrames = 0;
                while (cancelled.IsCatalogLoading && Buttons(cancelled).Count == 0 && loadingFrames++ < 3000) await Frames();
                Check(cancelled.IsCatalogLoading && Buttons(cancelled).Count > 0, "Partial-row cancellation fixture did not yield during row construction");
                if (cancellation == "animated_close") cancelled.hide_panel();
                else if (cancellation == "direct_hide") cancelled.Hide();
                else RemoveChild(cancelled);
                var stamp = PresentationStamp(cancelled);
                await Frames(45);
                Check(!cancelled.IsCatalogLoading && PresentationStamp(cancelled) == stamp, "Partial row builder wrote back after cancellation: " + cancellation);
                if (cancelled.IsInsideTree()) cancelled.QueueFree(); else cancelled.Free();
                await Frames(2);
            }
            GD.Print("ENCYCLOPEDIA_CANCEL rapid_category/animated_close/direct_hide/exit_tree PASS");

            clock.time_changed -= TimeChanged; npcs.world_npcs_changed -= NpcsChanged;
            var final = (clock.year, clock.month, clock.day, clock.hour, clock.get_pending_hours(), clock.is_paused,
                saves.active_slot_id, saves.active_slot_name, npcs.npcs.Count, npcs.get_last_processed_month(), npcs.population_version, npcs.simulation_enabled);
            Check(initial.Equals(final) && timeSignals == 0 && npcSignals == 0, "Read-only encyclopedia changed calendar/save/NPC state");
            GD.Print("ENCYCLOPEDIA_READ_ONLY calendar/save_slot/npc_state unchanged PASS");
            GD.Print("ENCYCLOPEDIA_LOADING_SMOKE PASS");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
