using Godot;

public partial class BattlePaperDoll
{
    /// <summary>Settle the living caster after its own strike; the manifested arm
    /// performs the follow-up while the body keeps its normal ready stance.</summary>
    public void return_to_blood_arm_idle()
    {
        if(!_usesSideInk||_sideDead) return;
        hold_attack_contact(false);
        EndContactPoseHold();
        _actionTween?.Kill();
        Position=Vector2.Zero; Rotation=0; Modulate=Colors.White;
        ResetSideInkPose();
    }
    /// <summary>Shoulder positions in the shared 2048 canvas, relative to ground.
    /// Both sides use the unmirrored right-facing view; the arm rig mirrors once.</summary>
    public Vector2 side_ink_blood_shoulder(bool near)
    {
        var idle = near ? new Vector2(_sideFemale ? -110 : -85, -1020)
            : new Vector2(_sideFemale ? 85 : 65, -1080);
        var pose = _sidePose;
        if (pose == "chop" && _sideElapsed < .08f) return idle;
        var posed = pose switch
        {
            "chop" => near ? new Vector2(-110, -885) : new Vector2(180, -810),
            "guard" => near ? new Vector2(-100, -1040) : new Vector2(100, -1090),
            _ => idle
        };
        // Follow the figure's pose crossfade instead of sliding at a different time.
        var weight = _portrait?.Modulate.A ?? 1f;
        return idle.Lerp(posed, weight);
    }
}
