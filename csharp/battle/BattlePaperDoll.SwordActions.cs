using Godot;

public partial class BattlePaperDoll
{
    private BattleSwordArt.Weapon? _battleSword;
    private bool _swordGuardSparkPlayed;
    public bool is_sword_guard_presenting()=>_usesSideInk&&!_sideDead&&_sidePose=="sword_parry";
    public bool take_sword_guard_contact()
    {
        if(!is_sword_guard_presenting()||_swordGuardSparkPlayed)return false;
        _swordGuardSparkPlayed=true;return true;
    }
    private void ConfigureSwordCombatPresentation(BattleEntityData data)
    {
        _battleSword=null;
        var weapon=BattleSwordArt.SelectWeapon(data,out var equipped);
        _battleSword=weapon;
        var swordPaths=data.combat_skill_slot_paths.Concat(data.loaded_skill_paths)
            .Concat(data.combat_skill_slots_configured?[]:data.active_skill_paths);
        var hasSword=equipped||data.combat_resolved_skills.Any(s=>s is not null&&BattleSwordArt.PoseKey(s).Length>0)
            ||swordPaths.Any(path=>new[]{"斩剑","格剑","养锋","尽锋"}.Any(name=>path.EndsWith("/"+name+".tres",StringComparison.Ordinal)));
        if(hasSword)show_sword_idle(weapon.Icon,weapon.Grip,weapon.Tip);
    }
    public bool side_ink_female()=>_sideFemale;
    public bool can_present_sword_action(string pose)=>_queueAvatar is null&&_usesSideInk&&!_sideDead&&BattleSwordArt.TryPose(pose,out _,_sideFemale);
    public bool play_sword_action(string pose,Vector2 direction,bool awaitingContact=false)
    {
        if(!can_present_sword_action(pose)||!BattleSwordArt.TryPose(pose,out var art,_sideFemale))return false;
        if(!_swordIdleEnabled)
        {
            var weapon=_battleSword??BattleSwordArt.DefaultWeapon();
            if(!show_sword_idle(weapon.Icon,weapon.Grip,weapon.Tip))return false;
        }
        EndContactPoseHold();_actionTween?.Kill();
        Position=Vector2.Zero;Rotation=0;Modulate=Colors.White;
        face_direction(direction);_swordGuardSparkPlayed=false;
        _sideTextures["sword_"+pose]=art!.Body;
        StartSideInkPose("sword_"+pose,pose=="nurture"?1.55f:pose=="parry"?1.35f:.95f);
        _guardAwaitingContact=pose=="parry"&&awaitingContact;_guardWaitClock=0;
        return true;
    }
    private bool StartSwordHurt()
    {
        if(!_swordIdleEnabled||_swordOrbitEnabled||_sideDead)return false;
        if(BattleSwordArt.TryPose("hurt",out var art,_sideFemale))_sideTextures["sword_hurt"]=art!.Body;
        else _sideTextures.Remove("sword_hurt");
        // Missing recoil art keeps this character's held idle and weapon visible.
        StartSideInkPose("sword_hurt",1.12f);
        return true;
    }
    public Vector2 sword_action_grip(string pose)=>_queueAvatar?.HandAnchor ?? (BattleSwordArt.TryPose(pose,out var art,_sideFemale)?art!.Grip-SideInkGround:side_ink_hand());
    public float sword_action_angle(string pose)=>BattleSwordArt.TryPose(pose,out var art,_sideFemale)?art!.Angle:0;
}
