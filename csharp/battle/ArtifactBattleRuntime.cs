/// <summary>
/// 单名角色、单场战斗的外祭状态。只处理召唤/御使意图与在场灵识，
/// 不扣体力法力、不执行效果，也不修改法器持久实例。
/// 调用方须先完成费用与目标校验，再在同一次行动结算中提交召唤。
/// </summary>
public sealed class ArtifactBattleRuntime
{
    public enum Presence { Unsummoned, Summoned, Collapsed, Departed }
    public enum Intent { None, Summon, Use, Unavailable }

    private sealed class Entry(int spiritCost)
    {
        public int SpiritCost { get; } = spiritCost;
        public Presence State { get; set; } = Presence.Unsummoned;
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private bool _finished;

    public ArtifactBattleRuntime(IEnumerable<(string InstanceId, int SpiritCost)> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        foreach (var (id, cost) in artifacts)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("外祭法器缺少实例ID。", nameof(artifacts));
            if (cost < 0) throw new ArgumentException("外祭灵识占用不能为负数。", nameof(artifacts));
            if (!_entries.TryAdd(id, new Entry(cost))) throw new ArgumentException("外祭法器实例ID重复。", nameof(artifacts));
        }
    }

    // long 避免错误配置的多个大数相加溢出后绕过容量限制。
    public long UsedSpirit => _finished ? 0 : _entries.Values
        .Where(entry => entry.State == Presence.Summoned).Sum(entry => (long)entry.SpiritCost);

    public bool TryGetPresence(string instanceId, out Presence presence)
    {
        if (_entries.TryGetValue(instanceId, out var entry))
        {
            presence = entry.State;
            return true;
        }
        presence = default;
        return false;
    }

    /// <summary>
    /// 仅预演阶段，不承诺费用、容量、目标或次数足够。null/空串代表其他行动，
    /// 每个输入位置均保留。移动或删除行动后用完整队列重新计算；不得修改已锁定意图。
    /// </summary>
    public IReadOnlyList<Intent> PreviewQueue(IEnumerable<string?> sourceInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(sourceInstanceIds);
        var predictedSummoned = _entries.Where(pair => pair.Value.State == Presence.Summoned)
            .Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        var result = new List<Intent>();
        foreach (var id in sourceInstanceIds)
        {
            if (string.IsNullOrWhiteSpace(id)) { result.Add(Intent.None); continue; }
            if (_finished || !_entries.TryGetValue(id, out var entry) || entry.State is Presence.Collapsed or Presence.Departed)
            {
                result.Add(Intent.Unavailable);
                continue;
            }
            result.Add(predictedSummoned.Add(id) ? Intent.Summon : Intent.Use);
        }
        return result;
    }

    /// <summary>检查锁定意图；未召出时的御使不能暗自转换为召唤。</summary>
    public bool CanExecute(string instanceId, Intent intent) => !_finished
        && _entries.TryGetValue(instanceId, out var entry)
        && (intent == Intent.Summon && entry.State == Presence.Unsummoned
            || intent == Intent.Use && entry.State == Presence.Summoned);

    /// <param name="otherUsedSpirit">由调用方提供、未包含本模块在场法器的其他占用。</param>
    public bool CanSummon(string instanceId, int capacity, long otherUsedSpirit)
    {
        if (capacity < 0 || otherUsedSpirit < 0 || !CanExecute(instanceId, Intent.Summon)) return false;
        var required = UsedSpirit + _entries[instanceId].SpiritCost;
        return otherUsedSpirit <= capacity && required <= capacity - otherUsedSpirit;
    }

    /// <summary>成功的召唤结算才调用。资源不足等失败情况不提交，后续御使保持无效。</summary>
    public bool TryCommitSummon(string instanceId, int capacity, long otherUsedSpirit)
    {
        if (!CanSummon(instanceId, capacity, otherUsedSpirit)) return false;
        _entries[instanceId].State = Presence.Summoned;
        return true;
    }

    public bool TryCollapse(string instanceId)
    {
        if (!CanExecute(instanceId, Intent.Use)) return false;
        _entries[instanceId].State = Presence.Collapsed;
        return true;
    }

    public bool TryDepart(string instanceId)
    {
        if (!CanExecute(instanceId, Intent.Use)) return false;
        _entries[instanceId].State = Presence.Departed;
        return true;
    }

    /// <summary>回合切换不清理本对象；战斗结束后关闭，下一场须创建新对象。</summary>
    public void FinishBattle() => _finished = true;
}
