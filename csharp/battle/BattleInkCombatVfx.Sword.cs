using Godot;

public partial class BattleInkCombatVfx
{
    public static bool UsesSwordEffects(GodotObject? attacker,ActiveSkillData skill)=>attacker is Node2D actor
        && BattleSwordArt.PoseKey(skill) is "cut" or "finish"
        && SideDoll(actor)?.can_present_sword_action(BattleSwordArt.PoseKey(skill))==true;

    private static BattleInkCombatVfx? PlaySwordAttack(Node2D actor,ActiveSkillData skill)
    {
        if(SideDoll(actor) is not { } doll)return null;
        var pose=BattleSwordArt.PoseKey(skill);var grip=doll.sword_action_grip(pose);
        var at=grip+(pose=="cut"?new Vector2(310,-80):new Vector2(370,0));
        var size=pose=="cut"?new Vector2(1310,1250):new Vector2(1500,560);
        var effect=SpawnSequence(doll,false,pose,at,size,30,BattleSwordArt.Frames());
        if(effect is null)return null;
        // Preserve the real attack's existing contact time. Only sequence
        // registration changes; the preview's contact frame arrives at that beat.
        var contact=AttackContactSeconds(actor);
        effect._delay=Mathf.Max(0,contact-(pose=="cut"?5f:4f)/30);
        effect._holdAt=contact-effect._delay;
        effect._hold=.035f+ContactHoldSeconds(HitWeight(skill));
        effect._followUntil=contact+effect._hold;effect._cancelOnDeath=true;
        return effect;
    }
    private static bool PlaySwordUtility(Node2D actor,ActiveSkillData skill)
    {
        var pose=BattleSwordArt.PoseKey(skill);
        if(pose is not ("parry" or "nurture")||SideDoll(actor) is not { } doll)return false;
        if(!doll.play_sword_action(pose,doll.side_ink_faces_left()?Vector2.Left:Vector2.Right,CardRules.IsResponse(skill)))return false;
        if(pose=="nurture")
        {
            var effect=SpawnSequence(doll,false,"nurture",doll.sword_action_grip(pose)+new Vector2(390,-8),new Vector2(1110,360),30,BattleSwordArt.Frames());
            if(effect is not null)effect._cancelOnDeath=true;
        }
        // Parry's contact light belongs to the arriving attack, not to the
        // response introduction. There is no second hurt/response burst.
        return true;
    }
    private static void PlaySwordGuardContact(BattlePaperDoll doll)
    {
        if(!doll.take_sword_guard_contact())return;
        var at=doll.sword_action_grip("parry")+Vector2.Right.Rotated(doll.sword_action_angle("parry"))*360;
        var effect=SpawnSequence(doll,false,"parry",at,Vector2.One*650,30,BattleSwordArt.Frames());
        if(effect is not null){effect._holdAt=1f/30;effect._hold=ContactHoldSeconds(.9f);effect._cancelOnDeath=true;}
    }
    private static void PlaySwordContact(Node2D actor,Node2D target,ActiveSkillData skill,bool critical,int segment)
    {
        if(SideDoll(actor) is not { } doll)return;
        var native=doll.side_ink_visual_transform().AffineInverse()*Anchor(target);
        if(doll.side_ink_faces_left())native.X=-native.X;
        var effect=SpawnSequence(doll,false,"contact",native,Vector2.One*(critical?470:430),30,BattleSwordArt.Frames());
        if(effect is null)return;
        effect._followUntil=0;effect._delay=Mathf.Clamp(segment,0,6)*.055f;
        effect._holdAt=1f/30;effect._hold=ContactHoldSeconds(HitWeight(skill),critical);
    }
}
