using Godot;
using System.Threading.Tasks;

/// <summary>战斗切场使用共用水墨加载层，保留原 Ink 调用约定。</summary>
public partial class BattleSceneTransition : SceneLoadingScreen
{
    public Task Ink(float amount, double seconds) => amount > 0
        ? ShowAsync("进入战斗", "", seconds)
        : HideAsync(seconds);
}
