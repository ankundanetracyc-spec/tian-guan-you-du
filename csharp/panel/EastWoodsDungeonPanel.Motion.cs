using Godot;

public partial class EastWoodsDungeonPanel
{
    private const double PawnStepSeconds = .42;
    private Control? _travelPawn;
    private Tween? _pawnTween;
    private bool _playerFacingLeft;
    private readonly System.Collections.Generic.Dictionary<string, bool> _npcFacingLeft = new(System.StringComparer.Ordinal);

    private static void face_pawn(Control pawn, bool left)
    {
        foreach (var child in pawn.GetChildren())
            if (child is TextureRect figure) figure.FlipH = left;
    }

    private void animate_player_step(string previous, Vector2 from)
    {
        var destination = _pawnLayers[_current].GetNodeOrNull<Control>("PlayerPawn");
        if (destination is null) return;
        var to = pawn_map_position(destination);
        var half = destination.Size * .5f;
        var fromCentre = node_center(EastWoodsDungeonRules.get_node(previous)!) - half;
        var toCentre = node_center(EastWoodsDungeonRules.get_node(_current)!) - half;
        _travelPawn = create_pawn_token(true, null, destination.Size);
        face_pawn(_travelPawn, _playerFacingLeft);
        _travelPawn.Name = "TravelPawn";
        _travelPawn.Position = from;
        _travelPawn.Visible = true;
        _travelPawn.PivotOffset = new Vector2(destination.Size.X * .5f, destination.Size.Y);
        _mapRoot!.AddChild(_travelPawn);
        destination.Hide();
        clear_hover();
        refresh_action_bar();
        _pawnTween = CreateTween();
        _pawnTween.TweenMethod(Callable.From<float>(t =>
        {
            if (_travelPawn is null) return;
            apply_pawn_gait(_travelPawn, from, fromCentre, toCentre, to, t);
            _travelCameraPosition = (fromCentre + half).Lerp(toCentre + half, t);
        }), 0f, 1f, PawnStepSeconds);
        _pawnTween.TweenCallback(Callable.From(finish_pawn_step));
    }

    private void finish_pawn_step()
    {
        _pawnTween?.Kill();
        _pawnTween = null;
        _travelCameraPosition = null;
        if (_travelPawn is not null)
        {
            _travelPawn.Hide();
            _travelPawn.QueueFree();
            _travelPawn = null;
        }
        if (_pawnLayers.TryGetValue(_current, out var layer)) layer.GetNodeOrNull<Control>("PlayerPawn")?.Show();
        refresh_action_bar();
    }
}
