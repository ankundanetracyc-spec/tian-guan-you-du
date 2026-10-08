using Godot;
using Godot.Collections;

/// <summary>
/// 辖地的抽象经营账本。它只服务旧辖地经营界面的非物品状态；材料、成品与法宝等物品统一归玩家行囊。
/// </summary>
public partial class EstateResourceSystem : Node
{
    public event Action? MonthlySettled;
    public event Action<string, string, string, string, string>? TerritoryEventRaised;
    public const string Food = "food";
    public const string Stone = "stone";
    public const string Herbs = "herbs";
    public const string Ore = "ore";
    public const string SpiritStones = "spirit_stones";

    private static readonly string[] Order = [Food, Stone, Herbs, Ore, SpiritStones];
    private static readonly System.Collections.Generic.Dictionary<string, string> Names = new()
    {
        [Food] = "粮食", [Stone] = "石材", [Herbs] = "药材", [Ore] = "矿材", [SpiritStones] = "灵石",
    };

    [Signal] public delegate void ResourcesChangedEventHandler();
    [Signal] public delegate void SpiritGatheringChangedEventHandler(bool running, int room_count);

    private readonly System.Collections.Generic.Dictionary<string, int> _amounts = new();
    private readonly System.Collections.Generic.Dictionary<string, int> _facilities = new();
    private int _spiritGatheringRooms;
    private bool _spiritGatheringRequested;
    private int _lastSettledPeriod = -1;
    private QingxiTerritoryState? _territoryState;
    private Func<int>? _residentCountProvider;
    private string _pendingEventId = "";

    public bool spirit_gathering_running => _spiritGatheringRequested && _spiritGatheringRooms > 0 && get_amount(SpiritStones) > 0;
    public int spirit_gathering_room_count => _spiritGatheringRooms;
    public float cultivation_environment_bonus => spirit_gathering_running ? 0.25f * _spiritGatheringRooms : 0.0f;

    public override void _Ready()
    {
        foreach (var id in Order) _amounts[id] = 0;
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is not null)
        {
            _lastSettledPeriod = get_month_index(time);
            time.time_changed += process_time;
        }
    }

    public static string get_display_name(string resource_id) => Names.GetValueOrDefault(resource_id, resource_id);
    public static Array<string> get_resource_order() => new(Order);
    public int get_amount(string resource_id) => _amounts.GetValueOrDefault(resource_id, 0);
    public int get_facility_count(string facility_id) => _facilities.GetValueOrDefault(facility_id, 0);
    public bool can_afford(string resource_id, int amount) => amount <= 0 || get_amount(resource_id) >= amount;
    public void configure_settlement(QingxiTerritoryState state, Func<int> resident_count_provider)
    {
        _territoryState = state; _residentCountProvider = resident_count_provider;
        _territoryState.recalculate_auto_labor(get_amount(Food), _residentCountProvider());
    }

    public bool add(string resource_id, int amount)
    {
        if (!Names.ContainsKey(resource_id) || amount <= 0) return false;
        _amounts[resource_id] = get_amount(resource_id) + amount;
        EmitSignal(SignalName.ResourcesChanged);
        refresh_spirit_gathering_state();
        return true;
    }

    public bool spend(string resource_id, int amount)
    {
        if (!can_afford(resource_id, amount) || amount <= 0) return false;
        _amounts[resource_id] = get_amount(resource_id) - amount;
        EmitSignal(SignalName.ResourcesChanged);
        refresh_spirit_gathering_state();
        return true;
    }

    public Array<Dictionary> get_snapshot()
    {
        Array<Dictionary> result = [];
        foreach (var id in Order) result.Add(new Dictionary { ["id"] = id, ["name"] = get_display_name(id), ["amount"] = get_amount(id) });
        return result;
    }

    public void register_spirit_gathering_room()
    {
        _spiritGatheringRooms++;
        _spiritGatheringRequested = true;
        refresh_spirit_gathering_state(true);
    }
    public void register_facility(string facility_id)
    {
        if (string.IsNullOrWhiteSpace(facility_id)) return;
        _facilities[facility_id] = get_facility_count(facility_id) + 1;
        EmitSignal(SignalName.ResourcesChanged);
    }

    public void set_spirit_gathering_requested(bool requested)
    {
        if (_spiritGatheringRequested == requested) return;
        _spiritGatheringRequested = requested;
        refresh_spirit_gathering_state(true);
    }

    /// <summary>每月（三十日）每间聚灵室消耗一枚灵石。灵石耗尽时自动停转，不会透支。</summary>
    private void process_time()
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null) return;
        var month = get_month_index(time);
        if (_lastSettledPeriod < 0) { _lastSettledPeriod = month; return; }
        while (_lastSettledPeriod < month)
        {
            _lastSettledPeriod++;
            settle_territory_period();
            if (spirit_gathering_running) spend(SpiritStones, _spiritGatheringRooms);
            MonthlySettled?.Invoke();
            raise_monthly_event_if_needed();
        }
        refresh_spirit_gathering_state();
    }

    /// <summary>基础月结算。具体药材仍由探索或药圃事件作为实体物品产出。</summary>
    private void settle_territory_period()
    {
        if (_territoryState is null) return;
        _territoryState.recalculate_auto_labor(get_amount(Food), _residentCountProvider?.Invoke() ?? 0);
        var farmers = _territoryState.get_labor(QingxiTerritoryState.Labor.Farming);
        var workers = _territoryState.get_labor(QingxiTerritoryState.Labor.Work);
        var guards = _territoryState.get_labor(QingxiTerritoryState.Labor.Defense);
        var external = _territoryState.get_labor(QingxiTerritoryState.Labor.External);
        var farmBonus = 1f + get_facility_count("farmland") * 0.2f;
        if (farmers > 0) add(Food, Mathf.FloorToInt(farmers * 3 * farmBonus * _territoryState.get_production_multiplier(QingxiTerritoryState.Labor.Farming)));
        if (workers > 0)
        {
            var output = _territoryState.get_production_multiplier(QingxiTerritoryState.Labor.Work);
            add(Stone, Mathf.FloorToInt(workers * (0.5f + get_facility_count("quarry") * 0.25f) * output));
            if (get_facility_count("herb_garden") > 0) add(Herbs, Mathf.Max(1, Mathf.FloorToInt(workers * 0.35f * get_facility_count("herb_garden") * output)));
            if (get_facility_count("mine") > 0 && _territoryState.security >= 35) add(Ore, Mathf.Max(1, Mathf.FloorToInt(workers * 0.35f * get_facility_count("mine") * output)));
        }
        var foodNeed = Math.Max(0, _residentCountProvider?.Invoke() ?? 0);
        var consumed = Math.Min(get_amount(Food), foodNeed);
        if (consumed > 0) spend(Food, consumed);
        _territoryState.apply_stability_delta(consumed < foodNeed ? -Mathf.CeilToInt((foodNeed - consumed) / 2f) : 1);
        var guardGain = get_facility_count("watchtower") > 0 ? 2 : 1;
        if (guards >= 2) _territoryState.apply_security_delta(guardGain);
        else if (external > guards) _territoryState.apply_security_delta(-1);
        if (get_facility_count("kitchen") > 0 && get_amount(Food) >= foodNeed * 2) _territoryState.apply_stability_delta(1);
    }

    private void raise_monthly_event_if_needed()
    {
        if (_territoryState is null || !string.IsNullOrEmpty(_pendingEventId)) return;
        if (_territoryState.security < 55)
        {
            _pendingEventId = "bandits";
            TerritoryEventRaised?.Invoke(_pendingEventId, "盗匪探路", "村外出现可疑行迹。治安不足时，盗匪会试探村中的粮仓与工地。", "增派巡防", "固守村庄");
        }
        else if (get_amount(Food) < Math.Max(24, (_residentCountProvider?.Invoke() ?? 0) * 3))
        {
            _pendingEventId = "lean_month";
            TerritoryEventRaised?.Invoke(_pendingEventId, "储粮见底", "当前粮食储备不足以从容应对下月变故。", "动用药材换粮", "勒紧用度");
        }
        else if (_territoryState.get_labor(QingxiTerritoryState.Labor.External) > 0)
        {
            _pendingEventId = "trader";
            TerritoryEventRaised?.Invoke(_pendingEventId, "行商到访", "外务民力带回一支愿意交换物资的行商队。", "以石材换矿材", "以药材换声望");
        }
    }

    public void resolve_territory_event(string event_id, bool primary)
    {
        if (_territoryState is null || event_id != _pendingEventId) return;
        switch (event_id)
        {
            case "bandits":
                if (primary) { _territoryState.apply_security_delta(4); _territoryState.apply_stability_delta(-1); }
                else { _territoryState.apply_security_delta(-2); }
                break;
            case "lean_month":
                if (primary && spend(Herbs, Math.Min(4, get_amount(Herbs)))) { add(Food, 12); _territoryState.apply_stability_delta(1); }
                else { _territoryState.apply_stability_delta(-3); }
                break;
            case "trader":
                if (primary && spend(Stone, Math.Min(4, get_amount(Stone)))) add(Ore, 5);
                else { spend(Herbs, Math.Min(5, get_amount(Herbs))); _territoryState.apply_stability_delta(2); }
                break;
        }
        _pendingEventId = "";
    }

    private static int get_month_index(GameTimeMgr time) => (time.year - 1) * 12 + time.month - 1;

    private void refresh_spirit_gathering_state(bool force = false)
    {
        var running = spirit_gathering_running;
        if (force || running != _lastReportedSpiritGatheringRunning)
        {
            _lastReportedSpiritGatheringRunning = running;
            EmitSignal(SignalName.SpiritGatheringChanged, running, _spiritGatheringRooms);
        }
    }
    private bool _lastReportedSpiritGatheringRunning;
}
