using Godot;
using System.Threading.Tasks;

/// <summary>Response time stop and battlefield framing; no actor scaling or decorative response FX.</summary>
public partial class BattleResponseFocus : Node
{
    private const float IntroSeconds = .30f, ReleaseHoldSeconds = .16f, ReturnSeconds = .18f;
    private readonly TaskCompletionSource<bool> _intro = new(), _outro = new();
    private readonly List<BattlePaperDoll> _frozenActors = [];
    private readonly List<(Node node, ProcessModeEnum mode)> _frozenVisuals = [];
    private BattleLocalFocusView _view = null!;
    private Node2D _responder = null!;
    private Node2D? _attacker;
    private float _elapsed;
    private bool _returning, _released, _triggered;
    public Task IntroFinished => _intro.Task;
    public float FrameZoom => _view.FrameZoom;

    public static BattleResponseFocus Begin(BattleManager manager, Node2D responder, ActiveSkillData skill, Node2D? attacker = null)
    {
        var focus = new BattleResponseFocus { Name = "ResponseFocus", ProcessMode = ProcessModeEnum.Pausable, _responder = responder, _attacker = attacker };
        foreach (var actor in manager.all_units.OfType<Node>().Where(GodotObject.IsInstanceValid))
        {
            if (actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is not { } doll) continue;
            doll.set_response_time_stop(true);
            focus._frozenActors.Add(doll);
        }
        foreach (var visual in manager.GetTree().GetNodesInGroup("battle_action_visuals")
            .Concat(manager.GetTree().GetNodesInGroup("battle_persistent_visuals")).Distinct())
        {
            focus._frozenVisuals.Add((visual, visual.ProcessMode));
            visual.ProcessMode = ProcessModeEnum.Disabled;
        }
        manager.GetTree().CurrentScene.AddChild(focus);
        focus.AddToGroup("battle_action_visuals");
        return focus;
    }

    public override void _Ready()
    {
        _view = new BattleLocalFocusView();
        AddChild(_view);
    }

    public void ReleaseTimeStop()
    {
        if (_released) return;
        _released = true;
        foreach (var actor in _frozenActors.Where(GodotObject.IsInstanceValid)) actor.set_response_time_stop(false);
        foreach (var (node, mode) in _frozenVisuals)
            if (GodotObject.IsInstanceValid(node)) node.ProcessMode = mode;
    }

    public Task FinishPresentation(bool triggered)
    {
        ReleaseTimeStop();
        _triggered = triggered;
        _returning = true; _elapsed = 0;
        return _outro.Task;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_responder)) { CancelPresentation(); return; }
        _elapsed += (float)delta;
        if (!_returning)
        {
            var eased = 1 - Mathf.Pow(1 - Mathf.Clamp(_elapsed / .13f, 0, 1), 3);
            _view.PresentBattle(_responder, _attacker, 1 + .30f * eased, .12f * eased);
            if (_elapsed >= IntroSeconds) _intro.TrySetResult(true);
            return;
        }
        var fade = Mathf.Clamp((_elapsed - ReleaseHoldSeconds) / ReturnSeconds, 0, 1);
        var remaining = 1 - fade * fade * (3 - 2 * fade);
        var weight = _triggered ? 1 : remaining;
        _view.PresentBattle(_responder, _attacker, 1 + .30f * weight, .12f * weight);
        if (fade < 1) return;
        if (_triggered) BattleContactFocus.HoldForContact(_attacker, _responder, 1.30f);
        _outro.TrySetResult(true);
        QueueFree();
    }

    public void CancelPresentation()
    {
        ReleaseTimeStop();
        if (IsInsideTree() && !IsQueuedForDeletion()) QueueFree();
    }

    public override void _ExitTree()
    {
        ReleaseTimeStop(); _intro.TrySetResult(false); _outro.TrySetResult(false);
    }
}
