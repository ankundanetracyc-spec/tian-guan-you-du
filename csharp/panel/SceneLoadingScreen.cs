using Godot;
using System.Threading.Tasks;

/// <summary>跨场景保留的统一水墨加载层，复用行动过渡的素材和控件。</summary>
public partial class SceneLoadingScreen : CanvasLayer
{
    private ActionTransitionPanel _view = null!;
    public static bool IsChangingScene { get; private set; }

    public override void _Ready()
    {
        Layer = 240;
        ProcessMode = ProcessModeEnum.Always;
        // 战斗退场演出尚未铺墨时，也保留原有的透明输入阻挡。
        var blocker = new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Stop };
        blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(blocker);
        _view = new ActionTransitionPanel { RegisterAsAction = false };
        AddChild(_view);
    }

    public Task ShowAsync(string title, string detail = "", double seconds = .25) => _view.show_loading_async(title, detail, seconds);
    public void Report(double ratio, string state) => _view.update_loading(ratio, state);
    public Task HideAsync(double seconds = .3, bool completed = true) => _view.hide_loading_async(seconds, completed);

    public static async Task<Error> ChangeSceneAsync(SceneTree tree, string path, string title, Func<bool>? prepare = null, PackedScene? packedScene = null)
    {
        if (IsChangingScene) return Error.Busy;
        IsChangingScene = true;
        var loading = new SceneLoadingScreen();
        tree.Root.AddChild(loading);
        var completed = false;
        try
        {
            await loading.ShowAsync(title);
            loading.Report(.1, "正在准备载入…");
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (prepare is not null && !prepare()) return Error.Failed;
            if (packedScene is null && (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))) return Error.CantOpen;
            loading.Report(.3, "正在读取场景…");
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            // 沿用各入口原本的场景读取 API，视觉层不改变 C# 资源缓存及句柄生命周期。
            var error = packedScene is not null ? tree.ChangeSceneToPacked(packedScene) : tree.ChangeSceneToFile(path);
            // 沿用战斗返回的“打包世界优先、源场景兜底”顺序。
            if (error != Error.Ok && packedScene is not null)
            {
                if (!string.IsNullOrWhiteSpace(path)) error = tree.ChangeSceneToFile(path);
            }
            if (error != Error.Ok) return error;
            await tree.ToSignal(tree, SceneTree.SignalName.SceneChanged);
            loading.Report(.85, "正在布置场景…");
            if (tree.CurrentScene is MainScene main)
            {
                while (!main.StartupComplete) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                if (main.StartupError is not null) throw main.StartupError;
            }
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            loading.Report(1, "已就绪");
            completed = true;
            return Error.Ok;
        }
        catch (Exception ex) { GD.PushError($"场景加载失败：{ex}"); return Error.Failed; }
        finally
        {
            try { await loading.HideAsync(completed: completed); }
            finally { loading.QueueFree(); IsChangingScene = false; }
        }
    }
}
