using Godot;

public partial class UnifiedLoadingSmoke : Node
{
    public override void _Ready() => Callable.From(() => GetTree().Root.AddChild(new UnifiedLoadingDriver())).CallDeferred();
}

/// <summary>驱动节点跨场景保留，不读取或写入正式旅程。</summary>
public partial class UnifiedLoadingDriver : Node
{
    private const string TargetPath = "res://tools/UnifiedLoadingTarget.tscn";
    private void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            var tree = GetTree();
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var start = clock.get_total_game_hours();
            var initialPause = tree.Paused;
            var action = new ActionTransitionPanel(); AddChild(action);
            var settled = 0;
            action.play("休息", "静室休养，预计耗时 1日。", "休养结束。", () => settled++);
            await ToSignal(tree.CreateTimer(1.2, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            await Capture("通用行动加载");
            while (action.IsPlaying) await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(settled == 1 && tree.Paused == initialPause, "通用行动结算与暂停恢复");
            action.QueueFree();

            var battle = new BattleSceneTransition(); AddChild(battle);
            await battle.Ink(1, .24);
            battle.Report(.64, "正在准备招式与阵容…");
            Check(tree.Paused == initialPause, "战斗加载视觉不接管暂停状态");
            await Capture("战斗切场加载");
            await battle.Ink(0, .3); battle.QueueFree();

            var journey = new JourneyCreationLoadingView(); AddChild(journey);
            await ToSignal(tree.CreateTimer(.3, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            journey.Report(.64, "正在铺展百川山河…");
            await Capture("新旅程创建加载");
            journey.QueueFree();

            var origin = tree.CurrentScene;
            var prepares = 0;
            var failed = SceneLoadingScreen.ChangeSceneAsync(tree, TargetPath, "续接修行", () => { prepares++; return false; });
            Check(SceneLoadingScreen.IsChangingScene, "进入加载状态");
            var duplicate = await SceneLoadingScreen.ChangeSceneAsync(tree, TargetPath, "重复载入", () => { prepares++; return true; });
            Check(duplicate == Error.Busy && await failed == Error.Failed && prepares == 1, "重复请求与读档准备失败处理");
            Check(tree.CurrentScene == origin && !SceneLoadingScreen.IsChangingScene, "失败保留原场景并释放加载状态");

            Check(await SceneLoadingScreen.ChangeSceneAsync(tree, TargetPath, "踏入尘世") == Error.Ok, "资源读取与正式切场");
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(tree.CurrentScene.Name == "UnifiedLoadingTarget" && !GodotObject.IsInstanceValid(origin), "原场景释放且加载层跨场景存续");
            var packed = new PackedScene(); Check(packed.Pack(tree.CurrentScene) == Error.Ok, "准备打包场景");
            tree.Paused = true;
            Check(await SceneLoadingScreen.ChangeSceneAsync(tree, TargetPath, "返回修行", packedScene: packed) == Error.Ok, "战后打包场景路径");
            Check(tree.Paused && !SceneLoadingScreen.IsChangingScene, "暂停中切场保留调用者暂停状态");
            tree.Paused = initialPause;
            Check(await SceneLoadingScreen.ChangeSceneAsync(tree, "res://tools/missing_loading_target.tscn", "返回首页") == Error.CantOpen, "缺失资源失败收尾");
            Check(!SceneLoadingScreen.IsChangingScene && clock.get_total_game_hours() == start, "加载不推进历法且失败后可继续");
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(!tree.Root.GetChildren().OfType<SceneLoadingScreen>().Any(), "无遗留加载遮罩");
            GD.Print("UNIFIED_LOADING_PASS: action, battle view, scene load, packed scene return, duplicate guard, failure cleanup, pause ownership, no calendar changes");
            tree.Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }

    private async System.Threading.Tasks.Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://docs/美术规范/修炼时间流逝_20261015/{name}.png"));
    }
}
