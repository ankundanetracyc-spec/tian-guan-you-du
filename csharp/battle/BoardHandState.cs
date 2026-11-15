/// <summary>队列容量成本由战斗规则的调用方提供，不从招式占格推导。</summary>
public sealed record BoardQueuedCard(BoardCard Card, int CapacityCost);

/// <summary>单个角色本场的实体牌位置；不支付费用，也不处理目标、拼点或技能效果。</summary>
public sealed class BoardHandState
{
    public const int DefaultQueueCapacity = 5;
    public const int OpeningDraw = 4;
    public const int RoundDraw = 2;
    public const int QueueCount = 2;

    private readonly Random _random;
    private readonly int _queueCapacity;
    private readonly List<BoardCard> _drawPile;
    private readonly List<BoardCard> _hand = [];
    private readonly List<BoardCard> _discardPile = [];
    private readonly List<BoardQueuedCard>[] _queues = [new(), new()];
    private bool _opened;
    private int _lastRound;

    public IReadOnlyList<BoardCard> DrawPile => _drawPile.ToArray();
    public IReadOnlyList<BoardCard> Hand => _hand.ToArray();
    public IReadOnlyList<BoardCard> DiscardPile => _discardPile.ToArray();
    public int QueueCapacity => _queueCapacity;

    public BoardHandState(IReadOnlyList<BoardCard> cards, Random? random = null,
        int queueCapacity = DefaultQueueCapacity)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (queueCapacity < 0) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        if (cards.Any(card => card is null) || cards.Select(card => card.Key).Distinct().Count() != cards.Count)
            throw new ArgumentException("牌组存在空牌或重复实体牌身份。", nameof(cards));
        _random = random ?? Random.Shared;
        _queueCapacity = queueCapacity;
        _drawPile = new(cards);
        Shuffle(_drawPile);
    }

    /// <summary>起手只执行一次；回合补牌须由调用方另行决定时序。</summary>
    public int DrawOpeningHand()
    {
        if (_opened) return 0;
        _opened = true;
        return Draw(OpeningDraw);
    }

    /// <summary>同一回合重复调用不会重复补牌；旧回合不能重进。</summary>
    public int DrawForRound(int round)
    {
        if (!_opened) throw new InvalidOperationException("请先抽取起手牌。");
        if (round <= 0 || round < _lastRound) throw new ArgumentOutOfRangeException(nameof(round));
        if (round == _lastRound) return 0;
        _lastRound = round;
        return Draw(RoundDraw);
    }

    public IReadOnlyList<BoardQueuedCard> GetQueue(int queueIndex)
    {
        if (!ValidQueue(queueIndex)) throw new ArgumentOutOfRangeException(nameof(queueIndex));
        return _queues[queueIndex].ToArray();
    }

    public int GetQueueUsed(int queueIndex)
    {
        if (!ValidQueue(queueIndex)) throw new ArgumentOutOfRangeException(nameof(queueIndex));
        return _queues[queueIndex].Sum(item => item.CapacityCost);
    }

    public bool TryEnqueue(BoardCardKey key, int queueIndex, int capacityCost, out string reason)
    {
        if (!ValidQueue(queueIndex)) { reason = "队列不存在。"; return false; }
        if (capacityCost < 0) { reason = "容量成本不能为负。"; return false; }
        var index = _hand.FindIndex(card => card.Key == key);
        if (index < 0) { reason = "这张牌不在手牌中。"; return false; }
        if ((long)GetQueueUsed(queueIndex) + capacityCost > _queueCapacity)
        { reason = "队列剩余容量不足。"; return false; }
        var card = _hand[index];
        _hand.RemoveAt(index);
        _queues[queueIndex].Add(new(card, capacityCost));
        reason = "";
        return true;
    }

    public bool TryWithdraw(BoardCardKey key)
    {
        foreach (var queue in _queues)
        {
            var index = queue.FindIndex(item => item.Card.Key == key);
            if (index < 0) continue;
            _hand.Add(queue[index].Card);
            queue.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>结算调用方确认使用后，才把该队列牌移入弃牌堆。</summary>
    public bool TryDiscardQueued(BoardCardKey key)
    {
        foreach (var queue in _queues)
        {
            var index = queue.FindIndex(item => item.Card.Key == key);
            if (index < 0) continue;
            _discardPile.Add(queue[index].Card);
            queue.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>仅由明确要求弃手牌的调用方使用。</summary>
    public bool TryDiscardHand(BoardCardKey key)
    {
        var index = _hand.FindIndex(card => card.Key == key);
        if (index < 0) return false;
        _discardPile.Add(_hand[index]);
        _hand.RemoveAt(index);
        return true;
    }

    public bool TryReorder(int queueIndex, BoardCardKey key, int destinationIndex)
    {
        if (!ValidQueue(queueIndex)) return false;
        var queue = _queues[queueIndex];
        var index = queue.FindIndex(item => item.Card.Key == key);
        if (index < 0 || destinationIndex < 0 || destinationIndex >= queue.Count) return false;
        var item = queue[index];
        queue.RemoveAt(index);
        queue.Insert(destinationIndex, item);
        return true;
    }

    private static bool ValidQueue(int queueIndex) => queueIndex is >= 0 and < QueueCount;

    private int Draw(int count)
    {
        var drawn = 0;
        while (drawn < count)
        {
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0) break;
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle(_drawPile);
            }
            var index = _drawPile.Count - 1;
            _hand.Add(_drawPile[index]);
            _drawPile.RemoveAt(index);
            drawn++;
        }
        return drawn;
    }

    private void Shuffle(List<BoardCard> cards)
    {
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}
