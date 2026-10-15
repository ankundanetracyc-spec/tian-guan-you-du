using Godot;
using System.Threading.Tasks;

/// <summary>新旅程按真实创建阶段更新，显示层复用全局水墨加载画面。</summary>
public partial class JourneyCreationLoadingView : Control
{
    public const string BackgroundPath = "res://textures/ui/cultivation_transition_20261015/ink_background_v1.png";
    private ActionTransitionPanel _view = null!;
    private Task _shown = Task.CompletedTask;
    private double _ratio;
    private static JourneyCreationLoadingView? _active;
    public static bool IsLoading => _active is not null;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;
        _view = new ActionTransitionPanel { RegisterAsAction = false };
        AddChild(_view);
        _shown = _view.show_loading_async("踏入尘世", "", .25);
    }

    public void Report(double ratio, string state)
    {
        _ratio = Math.Max(_ratio, Math.Clamp(ratio, 0, 1));
        _view.update_loading(_ratio, state);
    }

    public static async Task StageAsync(SceneTree tree, double ratio, string state)
    {
        if (_active is null) return;
        _active.Report(ratio, state);
        // Give the engine a rendered frame before a synchronous initialization batch.
        if (DisplayServer.GetName() == "headless") await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        else await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    public static async Task<Error> EnterAsync(SceneTree tree, string name)
    {
        if (IsLoading || SceneLoadingScreen.IsChangingScene) return Error.Busy;
        var layer = new CanvasLayer { Layer = 240, ProcessMode = ProcessModeEnum.Always };
        _active = new JourneyCreationLoadingView();
        tree.Root.AddChild(layer);
        layer.AddChild(_active);
        var keepFailurePage = false;
        var completed = false;
        try
        {
            await _active._shown;
            HoverInfoManager.HideAll();
            await StageAsync(tree, .03, "正在落笔此生…");
            tree.Root.GetNodeOrNull<GameSaveManager>("GameSaveManager")?.begin_new_game(name);
            await StageAsync(tree, .08, "正在铺展百川山河…");
            var error = tree.ChangeSceneToFile("res://tscn/scene/MainScene.tscn");
            if (error != Error.Ok) return error;
            await tree.ToSignal(tree, SceneTree.SignalName.SceneChanged);
            if (tree.CurrentScene is MainScene main)
            {
                while (!main.StartupComplete)
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                if (main.StartupError is not null) throw main.StartupError;
            }
            // Deferred landing and NPC activation finish under the loading page.
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await StageAsync(tree, 1, "山河已定，即刻入世");
            completed = true;
            return Error.Ok;
        }
        catch (Exception ex)
        {
            GD.PushError($"新旅程创建失败：{ex}");
            if (tree.CurrentScene is MainScene)
            {
                keepFailurePage = true;
                _active.Report(0, "入世未成，请返回后重试");
                _active._view.SetProcessInput(false);
                var back = new Button { Text = "返回首页", ZIndex = 1000 };
                WorldHudStyle.StyleSmallButton(back);
                foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                    back.AddThemeColorOverride(key, new Color("d9ceba"));
                _active.AddChild(back);
                back.SetAnchorsPreset(LayoutPreset.CenterBottom);
                back.OffsetLeft = -100; back.OffsetRight = 100;
                back.OffsetTop = -130; back.OffsetBottom = -80;
                back.Pressed += async () =>
                {
                    var error = await SceneLoadingScreen.ChangeSceneAsync(tree, "res://tscn/scene/StartMenu.tscn", "返回首页");
                    if (error == Error.Ok) layer.QueueFree();
                };
            }
            return Error.Failed;
        }
        finally
        {
            var view = _active;
            try
            {
                if (!keepFailurePage && view is not null)
                    await view._view.hide_loading_async(.2, completed);
            }
            finally { _active = null; if (!keepFailurePage) layer.QueueFree(); }
        }
    }
}
