using System;
using System.Collections.Generic;

/// <summary>
/// 公共护盾的来源账本。来源只用于撤回本器剩余防护，受伤时所有来源仍按取得顺序统一吸收。
/// 不持有战斗时钟、费用或护罩额度；一次成功修改只发布一次最终总量通知。
/// </summary>
public sealed class FormationShieldLedger
{
    public const string OrdinarySourcePrefix = "$ordinary";
    public sealed record Layer(string SourceId, int Remaining, bool IsOrdinary, long Order);

    private readonly List<Layer> _layers = new();
    private readonly List<Exception> _notificationErrors = new();
    private int _total;
    private long _nextOrder;
    private int _notificationDepth;
    private bool _hasDeferredChange;
    private int _deferredOldTotal;

    public int Total => _total;
    public IReadOnlyList<Layer> Snapshot => Array.AsReadOnly(_layers.ToArray());
    public IReadOnlyList<Exception> NotificationErrors => _notificationErrors.AsReadOnly();
    public int NotificationErrorCount => _notificationErrors.Count;
    public event Action<int, int>? Changed;

    /// <summary>只延后通知，来源变更仍即时提交；最外作用域结束后报告一次最终总量。</summary>
    public IDisposable DeferNotifications()
    {
        _notificationDepth = checked(_notificationDepth + 1);
        return new NotificationScope(this);
    }

    public int GetRemaining(string sourceId)
    {
        ValidateSource(sourceId);
        foreach (var layer in _layers)
            if (!layer.IsOrdinary && layer.SourceId == sourceId) return layer.Remaining;
        return 0;
    }

    public void AddOrdinary(int amount)
    {
        ValidateAmount(amount);
        if (amount == 0) return;
        var total = ValidTotal((long)_total + amount);
        var order = checked(_nextOrder + 1);
        var oldTotal = _total;
        _layers.Add(new Layer(OrdinarySourcePrefix + "/" + order, amount, true, order));
        _nextOrder = order;
        _total = total;
        Notify(oldTotal, total);
    }

    /// <summary>替换该来源的旧余量并作为一次新取得排到末尾，失败前不移除旧层。</summary>
    public void ReplaceSource(string sourceId, int amount)
    {
        ValidateSource(sourceId);
        ValidateAmount(amount);
        var index = FindSource(sourceId);
        var previous = index < 0 ? 0 : _layers[index].Remaining;
        if (index < 0 && amount == 0) return;
        var total = ValidTotal((long)_total - previous + amount);
        var order = amount == 0 ? _nextOrder : checked(_nextOrder + 1);
        var oldTotal = _total;
        if (index >= 0) _layers.RemoveAt(index);
        if (amount > 0) _layers.Add(new Layer(sourceId, amount, false, order));
        _nextOrder = order;
        _total = total;
        Notify(oldTotal, total);
    }

    public int RemoveSource(string sourceId)
    {
        ValidateSource(sourceId);
        var index = FindSource(sourceId);
        if (index < 0) return 0;
        var removed = _layers[index].Remaining;
        var oldTotal = _total;
        _layers.RemoveAt(index);
        _total -= removed;
        Notify(oldTotal, _total);
        return removed;
    }

    /// <summary>统一公共盾受伤入口，FIFO消耗，返回真正被护盾挡下的伤害。</summary>
    public int Absorb(int damage)
    {
        ValidateAmount(damage);
        var absorbed = Math.Min(damage, _total);
        if (absorbed == 0) return 0;
        var remaining = absorbed;
        var fullyConsumed = 0;
        while (remaining > 0)
        {
            var layer = _layers[fullyConsumed];
            if (layer.Remaining <= remaining)
            {
                remaining -= layer.Remaining;
                fullyConsumed++;
            }
            else
            {
                _layers[fullyConsumed] = layer with { Remaining = layer.Remaining - remaining };
                remaining = 0;
            }
        }
        if (fullyConsumed > 0) _layers.RemoveRange(0, fullyConsumed);
        var oldTotal = _total;
        _total -= absorbed;
        Notify(oldTotal, _total);
        return absorbed;
    }

    /// <summary>兼容旧shield赋值：增量成为普通来源，减量仍按FIFO消耗；负值清零。</summary>
    public void SetLegacyTotal(int value)
    {
        var target = Math.Max(0, value);
        if (target > _total) AddOrdinary(target - _total);
        else if (target < _total) Absorb(_total - target);
    }

    private int FindSource(string sourceId)
    {
        for (var i = 0; i < _layers.Count; i++)
            if (!_layers[i].IsOrdinary && _layers[i].SourceId == sourceId) return i;
        return -1;
    }

    private void Notify(int oldTotal, int newTotal)
    {
        if (_notificationDepth > 0)
        {
            if (!_hasDeferredChange) _deferredOldTotal = oldTotal;
            _hasDeferredChange = true;
            return;
        }
        Publish(oldTotal, newTotal);
    }

    private void Publish(int oldTotal, int newTotal)
    {
        // 通知发生在提交之后。单个界面或反馈监听失败不得使调用者把已生效的防护当作失败退款。
        foreach (var listener in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((Action<int, int>)listener)(oldTotal, newTotal); }
            catch (Exception error) { _notificationErrors.Add(error); }
        }
    }

    private void EndNotifications()
    {
        _notificationDepth--;
        if (_notificationDepth > 0 || !_hasDeferredChange) return;
        var oldTotal = _deferredOldTotal;
        _hasDeferredChange = false;
        Publish(oldTotal, _total);
    }

    private sealed class NotificationScope : IDisposable
    {
        private FormationShieldLedger? _ledger;
        public NotificationScope(FormationShieldLedger ledger) => _ledger = ledger;
        public void Dispose()
        {
            var ledger = _ledger;
            _ledger = null;
            ledger?.EndNotifications();
        }
    }

    private static int ValidTotal(long value)
    {
        if (value > int.MaxValue) throw new OverflowException("公共护盾总量超出可保存范围。");
        return (int)value;
    }

    private static void ValidateAmount(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "护盾增量或伤害不可为负数。");
    }

    private static void ValidateSource(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.StartsWith(OrdinarySourcePrefix, StringComparison.Ordinal))
            throw new ArgumentException("命名护盾来源不可为空或使用普通来源保留域。", nameof(sourceId));
        foreach (var character in sourceId)
            if (char.IsControl(character)) throw new ArgumentException("护盾来源不可含控制字符。", nameof(sourceId));
    }
}
