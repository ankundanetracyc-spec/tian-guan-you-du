using Godot;

public partial class BattlePaperDoll
{
    // Direct outward release. Windup artwork remains archived for future actions.

    public void play_throw(Vector2 direction)
    {
        if (!_usesSideInk) { play_attack(direction, false); return; }
        if (_sideDead) return;
        EndContactPoseHold();
        _actionTween?.Kill();
        face_direction(direction);
        Position = Vector2.Zero; Rotation = 0; Modulate = Colors.White;
        StartSideInkPose("throw", .90f);
    }

    public Vector2 throw_cast_origin()
    {
        var point = _queueAvatar?.HandAnchor ?? (_sideFemale ? new Vector2(710, -1100) : new Vector2(770, -1130));
        if (side_ink_faces_left()) point.X = -point.X;
        return side_ink_visual_transform() * point;
    }
}
