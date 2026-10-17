using Godot;

/// <summary>单名角色技能队列中的一条指令。所有时间均以战斗模拟帧计。</summary>
public class BattleQueuedSkill
{
    public enum ActionKind { Skill, Rest, Breathe, Retreat, Empty }
    public enum Phase { Waiting, Preparing, Resolving, Recovering, HoldingResponse }

    public ActionKind kind = ActionKind.Skill;
    public ActiveSkillData skill = null!;
    public GodotObject? target;
    public Phase phase = Phase.Waiting;
    public int preparation_frames;
    public int recovery_frames;
    public float elapsed_frames;
    public int paid_energy;
    public int paid_mana;
    public bool intent_revealed;
    public float intent_elapsed;
    public bool wound_timing_adjusted;
    public bool completed;
    public bool skipped;
    public string artifact_instance_id = "";
    public ArtifactBattleRuntime.Intent artifact_intent = ArtifactBattleRuntime.Intent.None;
    public ActiveSkillData execution_skill => artifact_intent == ArtifactBattleRuntime.Intent.Summon ? skill.artifact_summon_skill ?? skill : skill;

    public BattleQueuedSkill(ActiveSkillData source, GodotObject? chosenTarget)
    {
        kind = ActionKind.Skill;
        skill = source;
        artifact_instance_id = source.artifact_instance_id;
        target = chosenTarget;
        preparation_frames = Mathf.Max(1, source.preparation_frames);
        recovery_frames = Mathf.Max(0, source.recovery_frames);
    }

    private BattleQueuedSkill(ActionKind actionKind)
    {
        kind = actionKind;
        phase = Phase.Waiting;
    }

    public static BattleQueuedSkill Retreat() => new(ActionKind.Retreat);
    public static BattleQueuedSkill Empty() => new(ActionKind.Empty);
    public static BattleQueuedSkill Rest() => new(ActionKind.Rest);
    public static BattleQueuedSkill Breathe() => new(ActionKind.Breathe);
    public string get_display_name() => kind switch
    {
        ActionKind.Empty => "空位",
        ActionKind.Retreat => "撤退",
        ActionKind.Rest => "休整",
        ActionKind.Breathe => "吐纳",
        _ => (skill?.skill_name ?? "技能") + (artifact_instance_id.Length == 0 ? "" : artifact_intent == ArtifactBattleRuntime.Intent.Summon ? "〔召唤〕" : artifact_intent == ArtifactBattleRuntime.Intent.Use ? "〔御使〕" : "〔不可用〕")
    };

    public float progress() => phase switch
    {
        Phase.Preparing => Mathf.Clamp(elapsed_frames / Mathf.Max(1, preparation_frames), 0, 1),
        Phase.Recovering => Mathf.Clamp(elapsed_frames / Mathf.Max(1, recovery_frames), 0, 1),
        _ => 0
    };
}
