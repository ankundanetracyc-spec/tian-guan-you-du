using Godot;

/// <summary>Battlefield closeup on the visual clock, without changing actor scale.</summary>
public partial class BattleContactFocus : Node
{
    private const float PeakZoomAdd = .38f;
    private const float ContactSlowScale = .22f;
    private const ulong ContactSlowMilliseconds = 340;
    private BattleLocalFocusView _view = null!;
    private Node2D? _source, _target;
    private float _elapsed, _strength, _entryZoom = 1, _kickElapsed;
    private bool _awaitingContact;
    private bool _impactStopActive;
    private bool _slowMotionActive;
    private double _timeScaleBeforeSlowMotion = 1, _appliedSlowTimeScale = 1;
    private ulong _impactStopUntilMsec, _slowMotionUntilMsec, _lastProcessMsec;
    private readonly List<BattlePaperDoll> _impactStoppedActors = [];
    private readonly List<(Node node, ProcessModeEnum mode)> _impactStoppedVisuals = [];
    public float FrameZoom => _view.FrameZoom;
    public bool ImpactStopActive => _impactStopActive;
    public bool SlowMotionActive => _slowMotionActive;
    public float StageDimStrength => _view.StageDimStrength;

    public static void Play(Node2D? source, Node2D target, float strength)
    {
        var tree = target.GetTree();
        if (tree.CurrentScene is null) return;
        var focus = tree.GetNodesInGroup("battle_contact_focus").OfType<BattleContactFocus>().FirstOrDefault(f=>!f.IsQueuedForDeletion());
        if (focus is null)
        {
            focus = new BattleContactFocus { Name = "ContactFocus", ProcessMode = ProcessModeEnum.Pausable };
            tree.CurrentScene.AddChild(focus);
            focus.AddToGroup("battle_contact_focus");
            focus.AddToGroup("battle_action_visuals");
        }
        focus._entryZoom = focus._view.FrameZoom;
        focus._source = source; focus._target = target; focus._elapsed = 0; focus._kickElapsed = 0; focus._awaitingContact = false;
        focus._strength = Mathf.Clamp(strength, .6f, 1.5f);
    }

    /// <summary>Brief visual-only hit stop after contact; gameplay resolution continues unchanged.</summary>
    public static void HoldImpactFrame(Node2D target, float seconds = .055f)
    {
        var focus = target.GetTree().GetNodesInGroup("battle_contact_focus").OfType<BattleContactFocus>()
            .FirstOrDefault(candidate => !candidate.IsQueuedForDeletion());
        focus?.BeginImpactStop(seconds);
    }

    public static void HoldForContact(Node2D? source, Node2D target, float zoom)
    {
        Play(source, target, 1);
        var focus = target.GetTree().GetNodesInGroup("battle_contact_focus").OfType<BattleContactFocus>().First(f=>!f.IsQueuedForDeletion());
        focus._entryZoom = zoom; focus._awaitingContact = true;
        focus._view.PresentBattle(target, source, zoom, .12f);
    }

    public override void _Ready()
    {
        _view = new BattleLocalFocusView();
        AddChild(_view);
        _lastProcessMsec = Time.GetTicksMsec();
    }

    public override void _Process(double delta)
    {
        var step = (float)delta;
        var now = Time.GetTicksMsec();
        var realStep = Mathf.Max(0, (now - _lastProcessMsec) / 1000f);
        _lastProcessMsec = now;
        if (!IsInstanceValid(_target) || _elapsed >= 1.02f) { QueueFree(); return; }
        if (_slowMotionActive && now >= _slowMotionUntilMsec) ReleaseSlowMotion();
        if (_impactStopActive)
        {
            _kickElapsed += realStep;
            _view.PresentBattle(_target!, _source, 1 + PeakZoomAdd * _strength, .76f, ImpactKick());
            if (now < _impactStopUntilMsec) return;
            ReleaseImpactStop();
        }
        _elapsed += step;
        _kickElapsed += step;
        if (_awaitingContact)
        {
            _view.PresentBattle(_target!, _source, _entryZoom, .70f);
            if (_elapsed < .35f) return;
            _awaitingContact = false; _elapsed = 0;
        }
        var intro = Mathf.SmoothStep(0, 1, Mathf.Clamp(_elapsed / .085f, 0, 1));
        var release = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((_elapsed - .25f) / .77f, 0, 1));
        var zoom = 1 + Mathf.Lerp(_entryZoom - 1, PeakZoomAdd * _strength, intro) * release;
        _view.PresentBattle(_target!, _source, zoom, .74f * release * Mathf.Max(intro, _entryZoom>1.01f?1:0), ImpactKick());
    }

    private void BeginImpactStop(float seconds)
    {
        var now = Time.GetTicksMsec();
        var stopMilliseconds = (ulong)Mathf.CeilToInt(Mathf.Clamp(seconds, .035f, .09f) * 1000f);
        _impactStopUntilMsec = Math.Max(_impactStopUntilMsec, now + stopMilliseconds);
        _slowMotionUntilMsec = Math.Max(_slowMotionUntilMsec, now + ContactSlowMilliseconds);
        _elapsed = .085f;
        _kickElapsed = 0;
        if (!_slowMotionActive)
        {
            _timeScaleBeforeSlowMotion = Engine.TimeScale;
            _appliedSlowTimeScale = Math.Min(_timeScaleBeforeSlowMotion, (double)ContactSlowScale);
            Engine.TimeScale = _appliedSlowTimeScale;
            _slowMotionActive = true;
        }
        if (_impactStopActive) return;
        _impactStopActive = true;
        if (IsInstanceValid(_target))
            _view.PresentBattle(_target!, _source, 1 + PeakZoomAdd * _strength, .76f, Vector2.Zero);
        var manager = GetTree().GetNodesInGroup("battle_mgr").OfType<BattleManager>().FirstOrDefault();
        foreach (var actor in manager?.all_units.OfType<Node>() ?? [])
            if (actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is { } doll)
            {
                doll.set_response_time_stop(true);
                _impactStoppedActors.Add(doll);
            }
        foreach (var visual in GetTree().GetNodesInGroup("battle_action_visuals").OfType<Node>().Distinct())
        {
            if (visual == this || !GodotObject.IsInstanceValid(visual) || visual.IsQueuedForDeletion()) continue;
            _impactStoppedVisuals.Add((visual, visual.ProcessMode));
            visual.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    private Vector2 ImpactKick()
    {
        if (_kickElapsed >= .18f) return Vector2.Zero;
        var decay = Mathf.Exp(-_kickElapsed * 23f);
        return new Vector2(Mathf.Cos(_kickElapsed * 50f), Mathf.Sin(_kickElapsed * 72f) * .32f)
            * (16f * _strength * decay);
    }

    private void ReleaseImpactStop()
    {
        if (!_impactStopActive) return;
        _impactStopActive = false;
        foreach (var actor in _impactStoppedActors.Where(GodotObject.IsInstanceValid)) actor.set_response_time_stop(false);
        _impactStoppedActors.Clear();
        foreach (var (node, mode) in _impactStoppedVisuals)
            if (GodotObject.IsInstanceValid(node)) node.ProcessMode = mode;
        _impactStoppedVisuals.Clear();
    }

    private void ReleaseSlowMotion()
    {
        if (!_slowMotionActive) return;
        _slowMotionActive = false;
        if (Math.Abs(Engine.TimeScale - _appliedSlowTimeScale) < .0001)
            Engine.TimeScale = _timeScaleBeforeSlowMotion;
    }

    public override void _ExitTree()
    {
        ReleaseImpactStop();
        ReleaseSlowMotion();
    }
}
