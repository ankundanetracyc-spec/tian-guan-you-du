using Godot;
using System.Threading.Tasks;
public partial class BattleManager
{
    private bool FlowTransitionActive=>GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.is_flow_transition_active()==true;
    public async Task PresentBattleEntrance()
    {
        var figures=all_units.OfType<CanvasItem>().Where(IsInstanceValid).ToList();
        var widgets=new CanvasItem?[]{_skillBar,_planHud}.Where(w=>IsInstanceValid(w)).Cast<CanvasItem>().Concat(unitOverlay?.GetChildren().OfType<CanvasItem>()??Enumerable.Empty<CanvasItem>()).ToList();
        foreach(var item in figures.Concat(widgets))item.Modulate=new Color(1,1,1,0);
        var tween=CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        foreach(var item in figures)tween.TweenProperty(item,"modulate",Colors.White,.22);
        foreach(var item in widgets)tween.TweenProperty(item,"modulate",Colors.White,.25).SetDelay(.18);
        await ToSignal(tween,Tween.SignalName.Finished);
    }
    private async Task FadeBattleControls()
    {
        var tween=CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        tween.TweenInterval(.18);
        foreach(var item in new CanvasItem?[]{_skillBar,_planHud}.Where(w=>IsInstanceValid(w)))tween.TweenProperty(item!,"modulate",new Color(1,1,1,0),.18);
        await ToSignal(tween,Tween.SignalName.Finished);
        _skillBar?.Hide();_planHud?.Hide();_handPanel?.Call("hide_hand");
    }
}
