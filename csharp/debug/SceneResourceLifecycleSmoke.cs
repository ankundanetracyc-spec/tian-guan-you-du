using Godot;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>检测炼器与正式战斗场景退出后的孤立节点、运行态及资源累积。</summary>
public partial class SceneResourceLifecycleSmoke : Node
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private Character owner = null!;
    private LocationCraftingPanel facilities = null!;
    private string mode = "forge";
    private readonly List<(string Name, WeakRef Reference)> transientResources = [];
    private int assertions;
    private static object? Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden)!.SetValue(target, value);
    private async Task Frames(int n = 3) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static HashSet<ulong> Orphans()
    {
        using var ids = (Godot.Collections.Array)Node.GetOrphanNodeIds();
        return ids.Select(id => id.AsUInt64()).ToHashSet();
    }
    private static string Describe(ulong id)
    {
        if (GodotObject.InstanceFromId(id) is not Node node) return id.ToString();
        var top = node; while (top.GetParent() is { } parent) top = parent;
        return $"{id} {node.GetClass()} name={node.Name} root={top.Name} parent={node.GetParent()?.Name} text={(node is Label label ? label.Text : "")}";
    }
    private static long ResourceCount => (long)Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
    private static ulong TextureBytes => RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed);
    private static string Counts() => $"nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)} orphans={Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)} resources={ResourceCount} texture_bytes={TextureBytes}";
    private void Track(string name, Resource resource) => transientResources.Add((name, GodotObject.WeakRef(resource) ?? throw new Exception("资源弱引用建立失败：" + name)));
    private void Check(bool value, string message) { if (!value) throw new Exception(message); assertions++; }
    private void CheckTransientResources()
    {
        foreach (var entry in transientResources)
        {
            try { Check(entry.Reference.GetRef().VariantType == Variant.Type.Nil, "场景临时资源未释放：" + entry.Name); }
            finally { entry.Reference.Dispose(); }
        }
        transientResources.Clear();
    }
    private async Task Settle()
    {
        await Frames();
        for (int i = 0; i < 2; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); await Frames(); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task OpenAndClose(string? selectedMode = null)
    {
        var selected = selectedMode ?? mode;
        if (selected == "mixed") { await OpenAndClose("forge"); await OpenAndClose("battle"); return; }
        if (selected == "forge")
        {
            facilities.ShowFor(owner, false);
            var room = facilities.GetChildren().OfType<ProcessForgingScene>().Single();
            Track("炼器页面独有主题", room.Theme);
            Call(room, "Examples"); await Frames();
            facilities.ReturnToLocation(); facilities.ReturnToLocation(); await Frames();
            if (IsInstanceValid(room) || facilities.Visible) throw new Exception("器作退出后仍保留页面。");
        }
        else if (selected == "info")
        {
            var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>();
            AddChild(panel); await Frames(); panel.QueueFree(); await Frames();
        }
        else
        {
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            var character = ForgedEquipmentPreview.CharacterScene();
            var player = owner.get_snapshot_data(); player.faction = 1;
            var enemy = ForgedEquipmentPreview.Baseline(); enemy.uid = "lifecycle-enemy"; enemy.faction = 2;
            Set(transition, "pending_attackers", new Godot.Collections.Array<PackedScene> { character });
            Set(transition, "pending_defenders", new Godot.Collections.Array<PackedScene> { character });
            Set(transition, "pending_attacker_data", new Godot.Collections.Array { player });
            Set(transition, "pending_defender_data", new Godot.Collections.Array { enemy });
            Set(transition, "_spectatorBattleActive", true);
            var scene = GD.Load<PackedScene>("res://tscn/battle/BattleScene.tscn").Instantiate<BattleScene>();
            var manager = scene.GetNode<BattleManager>("BattleMgr"); manager.isolated_trial_battle = true;
            AddChild(scene); await Frames(8); manager.SetProcess(false);
            var bar = scene.FindChild("SkillBarPanel", true, false) as PanelContainer;
            if (bar is not null) Track("战斗技能栏独有样式", bar.GetThemeStylebox("panel"));
            var actions = scene.FindChild("ActionPanel", true, false) as Panel;
            if (actions is not null) Track("战斗队列独有样式", actions.GetThemeStylebox("panel"));
            Call(manager, "FinishArtifacts"); scene.QueueFree(); await Frames();
            if (IsInstanceValid(scene) || IsInstanceValid(manager)) throw new Exception("战斗退出后仍保留场景。");
        }
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            mode = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--mode="))?.Split('=', 2)[1] ?? "forge";
            var model = new ProcessForgingTrial();
            owner = ForgingSmokeFixtures.Actor(model, ForgingSmokeFixtures.SmallDraft(model)); AddChild(owner);
            facilities = new LocationCraftingPanel(); AddChild(facilities);
            await Settle(); var start = Orphans(); GD.Print($"LIFECYCLE_START {mode} {Counts()}");
            int rounds = int.Parse(OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--rounds="))?.Split('=', 2)[1] ?? "20");
            long resourceCeiling = 0; ulong textureCeiling = 0;
            for (int i = -3; i < rounds; i++)
            {
                await OpenAndClose(); await Settle();
                CheckTransientResources();
                var extra = Orphans().Except(start).ToArray();
                GD.Print($"LIFECYCLE_ROUND {mode} {i} {Counts()} new_orphans={extra.Length}");
                foreach (var id in extra.Take(60)) GD.Print("LIFECYCLE_ORPHAN " + Describe(id));
                Check(extra.Length == 0, "场景退出后出现新增孤立节点。");
                if (i < 0) { resourceCeiling = Math.Max(resourceCeiling, ResourceCount); textureCeiling = Math.Max(textureCeiling, TextureBytes); }
                else
                {
                    Check(ResourceCount <= resourceCeiling, "热身后资源数量持续增长。");
                    Check(TextureBytes <= textureCeiling, "热身后纹理显存持续增长。");
                }
            }
            if (OS.GetCmdlineUserArgs().Contains("--require-renderer"))
                Check(DisplayServer.GetName() != "headless" && TextureBytes > 0, "窗口验收未建立真实渲染与纹理显存。");
            GD.Print($"SCENE_RESOURCE_LIFECYCLE_OK {mode} rounds={rounds} checks={assertions} resources={ResourceCount} texture_bytes={TextureBytes} renderer={RenderingServer.GetCurrentRenderingMethod()} display={DisplayServer.GetName()}"); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
