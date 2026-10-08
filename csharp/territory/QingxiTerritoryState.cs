using Godot;

/// <summary>
/// 青溪里常驻经营状态。民力是结算结果，不要求玩家按月逐项分配；
/// 玩家只调整长期章程与常驻岗位的授权边界。
/// </summary>
public partial class QingxiTerritoryState : Node
{
    // 保留旧枚举，供现有结算、建筑和 HUD 使用。实际数值由自治规则生成。
    public enum Policy { SecureLivelihood, CultivationFirst, ExternalAdvance }
    public enum Labor { Farming, Work, Transport, Construction, Defense, External }
    public enum ExternalAuthority { Closed, Routine, Broad }

    public sealed class Office(string id, string title, string lead, string deputy, string note)
    {
        public readonly string id = id;
        public readonly string title = title;
        public string lead = lead;
        public string deputy = deputy;
        public readonly string note = note;
        public bool covered => !string.IsNullOrWhiteSpace(lead) || !string.IsNullOrWhiteSpace(deputy);
    }

    public sealed class Situation(string id, string title, string detail, string primary, string secondary)
    {
        public readonly string id = id;
        public readonly string title = title;
        public readonly string detail = detail;
        public readonly string primary = primary;
        public readonly string secondary = secondary;
    }

    [Signal] public delegate void TerritoryChangedEventHandler();

    private readonly int[] _labor = [5, 3, 1, 0, 2, 1];
    private readonly List<Office> _offices =
    [
        new("village", "村务", "", "", "账务、民生与日常调度"),
        new("farmland", "田畴", "", "", "耕作、收成与佃户协作"),
        new("defense", "守备", "", "", "巡查、预警与护送"),
    ];
    private readonly List<Situation> _situations = [];
    public int total_labor { get; private set; } = 12;
    public Policy current_policy { get; private set; } = Policy.SecureLivelihood;
    public int stability { get; private set; } = 72;
    public int security { get; private set; } = 68;
    public int food_reserve_target { get; private set; } = 72;
    public int minimum_guard { get; private set; } = 2;
    public int training_slots { get; private set; } = 1;
    public ExternalAuthority external_authority { get; private set; } = ExternalAuthority.Routine;
    private int _emergency_labor_lock;

    public IReadOnlyList<Office> offices => _offices;
    public IReadOnlyList<Situation> situations => _situations;
    public int get_labor(Labor labor) => _labor[(int)labor];
    public int get_assigned_labor() => _labor.Sum();
    public int get_disrupted_labor() => _emergency_labor_lock + (stability < 60 ? Mathf.CeilToInt((60 - stability) / 10f) : 0) + (security < 45 ? Mathf.CeilToInt((45 - security) / 15f) : 0);
    public int get_available_labor() => Mathf.Max(0, total_labor - get_disrupted_labor());
    public int get_idle_labor() => get_available_labor() - get_assigned_labor();

    public void update_food_reserve_target(int value) { food_reserve_target = Mathf.Clamp(value, 30, 240); EmitSignal(SignalName.TerritoryChanged); }
    public void update_minimum_guard(int value) { minimum_guard = Mathf.Clamp(value, 0, 6); EmitSignal(SignalName.TerritoryChanged); }
    public void update_training_slots(int value) { training_slots = Mathf.Clamp(value, 0, 4); EmitSignal(SignalName.TerritoryChanged); }
    public void update_external_authority(ExternalAuthority value) { external_authority = value; EmitSignal(SignalName.TerritoryChanged); }
    public void add_labor(int amount)
    {
        total_labor = Mathf.Max(0, total_labor + amount);
        EmitSignal(SignalName.TerritoryChanged);
    }
    public bool try_assign_office(string officeId, bool isLead, string name)
    {
        var office = _offices.FirstOrDefault(item => item.id == officeId);
        if (office is null) return false;
        name = name.Trim();
        // 常驻职责占用一个人的主要精力；同一人不能同时担任两处不同岗位。
        if (!string.IsNullOrEmpty(name) && _offices.Any(item => item != office && (item.lead == name || item.deputy == name))) return false;
        if (isLead) { if (office.deputy == name) office.deputy = ""; office.lead = name; }
        else { if (office.lead == name) office.lead = ""; office.deputy = name; }
        EmitSignal(SignalName.TerritoryChanged);
        return true;
    }

    /// <summary>按底线自动排布民力；劳力不足时优先保粮、守备，再保留最小的村务运转。</summary>
    public void recalculate_auto_labor(int currentFood, int residentCount)
    {
        var available = get_available_labor();
        Array.Fill(_labor, 0);
        var farmers = Mathf.Clamp(2 + (currentFood < food_reserve_target ? 2 : 0) + (residentCount > 14 ? 1 : 0), 0, available);
        _labor[(int)Labor.Farming] = farmers; available -= farmers;
        var guards = Mathf.Min(minimum_guard, available); _labor[(int)Labor.Defense] = guards; available -= guards;
        if (available > 0) { _labor[(int)Labor.Transport] = 1; available--; }
        if (external_authority != ExternalAuthority.Closed && available > 0) { _labor[(int)Labor.External] = 1; available--; }
        _labor[(int)Labor.Work] = available;
        EmitSignal(SignalName.TerritoryChanged);
    }

    public void record_situation(string id, string title, string detail, string primary, string secondary)
    {
        if (_situations.Any(item => item.id == id)) return;
        _situations.Add(new Situation(id, title, detail, primary, secondary));
        EmitSignal(SignalName.TerritoryChanged);
    }

    public void resolve_situation(string id)
    {
        _situations.RemoveAll(item => item.id == id);
        EmitSignal(SignalName.TerritoryChanged);
    }

    /// <summary>切换只影响之后的结算与事件，不回滚已经发生的项目或损失。</summary>
    public void set_policy(Policy next)
    {
        if (current_policy == next) return;
        current_policy = next;
        EmitSignal(SignalName.TerritoryChanged);
    }

    public void set_labor(Labor labor, int amount)
    {
        amount = Mathf.Max(0, amount);
        var other = get_assigned_labor() - get_labor(labor);
        _labor[(int)labor] = Mathf.Min(amount, Mathf.Max(0, get_available_labor() - other));
        EmitSignal(SignalName.TerritoryChanged);
    }

    public void apply_stability_delta(int delta) { stability = Mathf.Clamp(stability + delta, 0, 100); normalize_assignments(); EmitSignal(SignalName.TerritoryChanged); }
    public void apply_security_delta(int delta) { security = Mathf.Clamp(security + delta, 0, 100); normalize_assignments(); EmitSignal(SignalName.TerritoryChanged); }
    public void set_emergency_labor_lock(int amount) { _emergency_labor_lock = Mathf.Clamp(amount, 0, total_labor); normalize_assignments(); EmitSignal(SignalName.TerritoryChanged); }
    public float get_production_multiplier(Labor labor) => 1.0f;
    private void normalize_assignments()
    {
        var overflow = Mathf.Max(0, get_assigned_labor() - get_available_labor());
        foreach (var labor in new[] { Labor.External, Labor.Work, Labor.Transport, Labor.Construction, Labor.Farming, Labor.Defense })
        {
            if (overflow <= 0) break;
            var taken = Mathf.Min(overflow, get_labor(labor));
            _labor[(int)labor] -= taken;
            overflow -= taken;
        }
    }
}
