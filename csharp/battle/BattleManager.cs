using Godot;
using Godot.Collections;

/// <summary>
/// C# battle runtime. It owns unit lifecycle, per-unit skill loadouts and
/// independent real-time queues. Condensation card play remains a separate mode.
/// </summary>
public partial class BattleManager : Node2D
{
    public enum TurnState { IDLE, PLAYER_TURN, ENEMY_TURN, PROCESSING }
    public enum SegmentPhase { None, Planning, Executing }
    private const float ActionThreshold = 1000f;
    private const int BattleEnergyCap = 6;
    [Export] public NodePath battle_map = new();
    [Export] public NodePath deploy_layer = new();
    [Export(PropertyHint.Range, "5,10,1")] public int action_slots_per_segment = 5;
    [Export(PropertyHint.Range, "0,180,1")] public float planning_timeout_seconds = 0f;
    [Export] public Dictionary action_slots_by_realm = [];
    public Array<GodotObject> player_units = [], enemy_units = [], all_units = [];
    public Dictionary action_gauges = [], battle_units = [];
    public GodotObject? active_unit;
    public bool battle_running, auto_battle;
    private bool _spectatorBattle;
    /// <summary>独立试器禁止调用世界战后回传，胜负和撤退只结束本地对练。</summary>
    [Export] public bool isolated_trial_battle;
    public TurnState turn_state = TurnState.IDLE;
    private readonly Dictionary _decks = [];
    private readonly Dictionary _firstHands = [], _hands = [];
    private SkillExecutor? _executor;
    private Node? _handPanel;
    private CombatSkillBar? _skillBar;
    private BattlePlanHud? _planHud;
    private BattleStatusPanel? _statusPanel;
    private bool _returning_to_world;
    private GongfaCombatRuntime? _gongfaCombat;
    public GongfaCombatRuntime gongfa_combat => _gongfaCombat ??= new GongfaCombatRuntime(this);
    private readonly HashSet<ulong> _chengjiResponses=[];
    private BattleUnitOverlay? unitOverlay;
    private float battleSpeed=1;
    // 旧回合 AI 的循环保护；实时技能队列不以此限制出招数量。
    private readonly HashSet<string> _enemyDecisionStates=[];
    private ActiveSkillData? _pendingInstantSkill;
    private sealed class QueuedPlayerCard { public ActiveSkillData Skill = null!; public GodotObject? Target; }
    private readonly Queue<QueuedPlayerCard> _queuedPlayerCards = new();
    private readonly System.Collections.Generic.Dictionary<GodotObject, List<FormationCircuitRuntime>> _formationCircuits = [];
    private const float SimulationFps = StatusEffect.CombatFramesPerSecond;
    private const float EnergyBaseInterval = 360f;
    private const int HandLimit = 10, OpeningHand = 5, QueueCapacity = 3, IntentRevealFrames = 96, CombatSlotCapacity = 10;
    private readonly System.Collections.Generic.Dictionary<GodotObject, float> _energyRegen = [];
    private readonly HashSet<GodotObject> _manaInitializedForBattle = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, float> _drawElapsedFrames = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, Array<ActiveSkillData>> _combatSkills = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, System.Collections.Generic.Dictionary<ActiveSkillData, float>> _skillCooldowns = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, System.Collections.Generic.Dictionary<ActiveSkillData, int>> _skillCharges = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, System.Collections.Generic.Dictionary<ActiveSkillData, int>> _skillRoundCharges = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, ActiveSkillData?> _pendingTargetSkills = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, int> _pendingZhouTianAutoCasts = [];
    private readonly HashSet<GodotObject> _runningZhouTianAutoCasts = [];
    private sealed class BattleResourceSnapshot
    {
        public int CurrentStamina, MaxStamina, CurrentMana, MaxMana;
    }
    private readonly System.Collections.Generic.Dictionary<GodotObject, BattleResourceSnapshot> _resourceSnapshots = [];
    private readonly System.Collections.Generic.Dictionary<GodotObject, float> _enemyPlanTimers = [];
    private sealed class EnemyKnowledgeState
    {
        public BattleQueuedSkill? ObservedCard;
        public float RevealElapsed;
        public bool Revealed;
        public ActiveSkillData? KnownSkill;
        public GodotObject? KnownTarget;
    }
    private readonly System.Collections.Generic.Dictionary<GodotObject, EnemyKnowledgeState> _enemyKnowledge = [];
    private bool _realtimeMode = true;
    private bool _battlePaused;
    private SegmentPhase _segmentPhase;
    private float _planningElapsed;
    private bool _playerQueueConfirmed;
    private GodotObject? _mainPlayerUnit;
    private GodotObject? _resolvingUnit;
    private int _segmentNumber;
    private readonly List<GodotObject> _livingUnits = new();
    public float get_battle_speed_scale()=>battleSpeed;
    public float cycle_battle_speed()
    {
        set_battle_speed(battleSpeed switch { 1f => 2f, 2f => 4f, 4f => 8f, _ => 1f });
        return battleSpeed;
    }
    public void set_spectator_battle(bool spectator) => _spectatorBattle = spectator;
    public bool is_spectator_battle() => _spectatorBattle;
    public bool is_realtime_battle()=>_realtimeMode;
    public bool is_battle_paused()=>_battlePaused;
    public SegmentPhase get_segment_phase()=>_segmentPhase;
    public bool is_planning_phase()=>_segmentPhase==SegmentPhase.Planning;
    public bool can_player_plan()=>_segmentPhase==SegmentPhase.Planning&&!_selectingQueueTargets&&!_playerQueueConfirmed&&GodotObject.IsInstanceValid(_mainPlayerUnit);
    public int get_action_slot_capacity()=>Mathf.Clamp(action_slots_per_segment,5,10);
    public bool is_waiting_for_skill_target()=>_pendingInstantSkill is not null;
    public void set_battle_speed(float value)
    {
        battleSpeed=value>=8?8:value>=4?4:value>=2?2:1;
        unitOverlay?.refresh_speed_button();
    }
    public void toggle_battle_pause()
    {
        if(!_realtimeMode||!battle_running)return;
        SetBattlePaused(!_battlePaused);
    }
    private void SetBattlePaused(bool paused)
    {
        _battlePaused=paused;
        GetTree().Paused=paused;
        unitOverlay?.refresh_pause_button();
    }
    public void cancel_skill_targeting(){_pendingInstantSkill=null;unitOverlay?.ClearTargetFrames();}
    public void select_active_unit(GodotObject unit)
    {
        if (!_realtimeMode || unit != _mainPlayerUnit || !Alive(unit) || !can_player_plan()) return;
        active_unit = _mainPlayerUnit; _pendingInstantSkill = null; _skillBar?.clear_targeting(); unitOverlay?.ClearTargetFrames();
        _skillBar?.set_battle_unit(_mainPlayerUnit!); _skillBar?.set_loadout(_combatSkills.GetValueOrDefault(_mainPlayerUnit!, []));
    }
    public void reorder_hand(GodotObject unit,Array<ActiveSkillData> ordered)
    {
        var hand=Hand(unit);var remaining=new Array<ActiveSkillData>(hand);
        foreach(var card in ordered)if(!remaining.Remove(card))return;
        if(remaining.Count==0)_hands[unit]=new Array<ActiveSkillData>(ordered);
    }
}
