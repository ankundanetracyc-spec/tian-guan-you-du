using Godot;

/// <summary>Battlefield closeup on the visual clock, without changing actor scale.</summary>
public partial class BattleContactFocus : Node
{
    private BattleLocalFocusView _view = null!;
    private Node2D? _source, _target;
    private float _elapsed, _strength, _entryZoom = 1;
    private bool _awaitingContact;
    public float FrameZoom => _view.FrameZoom;

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
        focus._source = source; focus._target = target; focus._elapsed = 0; focus._awaitingContact = false;
        focus._strength = Mathf.Clamp(strength, .6f, 1.5f);
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
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (!IsInstanceValid(_target) || _elapsed >= .72f) { QueueFree(); return; }
        if (_awaitingContact)
        {
            _view.PresentBattle(_target!, _source, _entryZoom, .12f);
            if (_elapsed < .35f) return;
            _awaitingContact = false; _elapsed = 0;
        }
        var intro = Mathf.SmoothStep(0, 1, Mathf.Clamp(_elapsed / .065f, 0, 1));
        var release = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((_elapsed - .19f) / .53f, 0, 1));
        var zoom = 1 + Mathf.Lerp(_entryZoom - 1, .25f * _strength, intro) * release;
        var kick = _elapsed < .075f ? new Vector2(Mathf.Sin(_elapsed * 155), Mathf.Cos(_elapsed * 187) * .45f) * (6 * _strength * (1 - _elapsed / .075f)) : Vector2.Zero;
        _view.PresentBattle(_target!, _source, zoom, .12f * release * Mathf.Max(intro, _entryZoom>1.01f?1:0), kick);
    }
}
