using Godot;

public partial class Creature
{
    private int _qiBloodValue, _bloodFurnaceValue;
    private readonly FormationShieldLedger _shieldSources = new();
    private bool _shieldFeedbackSubscribed;

    // 首次取得账本时订阅，未入树的隔离人物和直接来源写入也走同一反馈入口。
    public FormationShieldLedger ShieldSources
    {
        get
        {
            if (!_shieldFeedbackSubscribed)
            {
                _shieldFeedbackSubscribed = true;
                _shieldSources.Changed += OnShieldSourcesChanged;
            }
            return _shieldSources;
        }
    }

    private void OnShieldSourcesChanged(int oldTotal, int newTotal)
    {
        if (GodotObject.IsInstanceValid(this) && newTotal > oldTotal)
            ReportBattleStatusGain("护盾", newTotal - oldTotal);
    }

    // 捕获实际增量，覆盖技能、心法、受击触发与周期效果的所有写入入口。
    public int shield
    {
        get => ShieldSources.Total;
        set => ShieldSources.SetLegacyTotal(value);
    }
    public int qi_blood
    {
        get => _qiBloodValue;
        set
        {
            var change=value-_qiBloodValue;
            _qiBloodValue=value;
            var manager=FeedbackManager();
            if(change>0)manager?.show_status_gain(this,"气血",change);
            if(change!=0&&manager?.is_realtime_battle()==true)manager.SyncBloodArmVisual(this);
        }
    }
    public int blood_furnace_value
    {
        get => _bloodFurnaceValue;
        set { var gain = value - _bloodFurnaceValue; _bloodFurnaceValue = value; ReportBattleStatusGain("血炉", gain); }
    }

    private BattleManager? FeedbackManager() => IsInsideTree() ? GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager : null;
    private void ReportBattleStatusGain(string name, int amount, bool debuff = false)
    {
        if (amount > 0) FeedbackManager()?.show_status_gain(this, name, amount, debuff);
    }
    private void ReportBattleRecovery(string resource, int amount)
    {
        if (amount > 0) FeedbackManager()?.show_recovery_number(this, amount, resource);
    }
}
