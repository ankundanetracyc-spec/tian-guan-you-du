public enum QueueActionKind { Melee, Ranged, Guard, Dodge, Operation }
public enum QueueStepKind { PrepareDefense, Operation, UnopposedAttack, AttackClash, DefenseClash }
public enum QueueClashWinner { None, Actor, Defender, Tie }

/// <summary>调度身份只表示一次行动；追加段必须提供新的行动编号。</summary>
public sealed record QueueCombatAction(string Id, QueueActionKind Kind, string TargetQueueId = "");
public sealed record QueueScheduleInput(string QueueId, int Speed, int TieOrder);
public sealed record QueueCombatStep(QueueStepKind Kind, string QueueId, QueueCombatAction Actor,
    string? TargetQueueId, QueueCombatAction? Defender, bool DefenderWasWaiting);
/// <summary>只转交调用方检查所得的可用情况，不表示付款已经发生。</summary>
public sealed record QueueParticipantAvailability(string QueueId, string ActionId, bool CanPay);
public sealed record QueueAvailabilityReport(QueueParticipantAvailability Actor, QueueParticipantAvailability? Defender);

/// <summary>所有成功、命中、是否结束及追加结果由执行方明确给出，不在调度器中投骰或付费。</summary>
public sealed record QueueStepResolution(bool IsLegal, QueueClashWinner Winner,
    bool ActorHit, bool DefenderHit, bool ActorFinished, bool DefenderFinished,
    bool ActorStillValid, bool DefenderStillValid,
    QueueCombatAction? ActorFollowUp = null, QueueCombatAction? DefenderFollowUp = null);

public sealed class CombatQueueScheduler
{
    private sealed class QueueState(string id, string team, IEnumerable<QueueCombatAction> actions)
    {
        public string Id { get; } = id;
        public string Team { get; } = team;
        public List<QueueCombatAction> Actions { get; } = new(actions);
        public List<QueueCombatAction> Waiting { get; } = [];
    }

    private readonly Dictionary<string, QueueState> _queues = new(StringComparer.Ordinal);
    private readonly HashSet<string> _allActionIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _participated = new(StringComparer.Ordinal);
    private List<QueueState> _order = [];
    private int _cursor;
    private bool _roundActive;
    private QueueCombatStep? _pending;

    public void AddQueue(string id, string team, IEnumerable<QueueCombatAction> actions)
    {
        if (_roundActive) throw new InvalidOperationException("小轮期间不能增加队列。");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(team) || _queues.ContainsKey(id))
            throw new ArgumentException("队列编号或阵营无效，或队列编号重复。");
        ArgumentNullException.ThrowIfNull(actions);
        var list = actions.ToArray();
        var newIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in list)
            if (action is null || string.IsNullOrWhiteSpace(action.Id) || !Enum.IsDefined(action.Kind)
                || _allActionIds.Contains(action.Id) || !newIds.Add(action.Id))
                throw new ArgumentException("行动为空、类型无效或行动编号重复。", nameof(actions));
        _queues.Add(id, new(id, team, list));
        _allActionIds.UnionWith(newIds);
    }

    public IReadOnlyList<QueueCombatAction> GetActions(string queueId) => Queue(queueId).Actions.ToArray();
    public IReadOnlyList<QueueCombatAction> GetWaitingDefenses(string queueId) => Queue(queueId).Waiting.ToArray();

    /// <summary>每队列一次；相同速度的先后必须由调用方提供不同 TieOrder。</summary>
    public void BeginSubround(IEnumerable<QueueScheduleInput> inputs)
    {
        if (_roundActive || _pending is not null) throw new InvalidOperationException("上一小轮尚未结束。");
        ArgumentNullException.ThrowIfNull(inputs);
        var list = inputs.ToArray();
        if (list.Length != _queues.Count || list.Any(item => item is null || !_queues.ContainsKey(item.QueueId))
            || list.Select(item => item.QueueId).Distinct(StringComparer.Ordinal).Count() != list.Length
            || list.GroupBy(item => (item.Speed, item.TieOrder)).Any(group => group.Count() > 1))
            throw new ArgumentException("必须为每条队列提供速度与同速先后。", nameof(inputs));
        _order = list.OrderByDescending(item => item.Speed).ThenBy(item => item.TieOrder)
            .Select(item => _queues[item.QueueId]).ToList();
        _cursor = 0;
        _participated.Clear();
        _roundActive = true;
    }

    public QueueCombatStep? Next()
    {
        if (!_roundActive) throw new InvalidOperationException("请先开始小轮。");
        if (_pending is not null) throw new InvalidOperationException("须先提交当前行动结果。");
        while (_cursor < _order.Count)
        {
            var actorQueue = _order[_cursor++];
            if (_participated.Contains(actorQueue.Id) || actorQueue.Actions.Count == 0) continue;
            var actor = actorQueue.Actions[0];
            if (IsDefense(actor.Kind))
            {
                if (Queue(actor.TargetQueueId).Team == actorQueue.Team)
                    throw new InvalidOperationException("守招必须指定敌方交互队列。");
                return _pending = new(QueueStepKind.PrepareDefense, actorQueue.Id, actor, null, null, false);
            }
            if (actor.Kind == QueueActionKind.Operation)
                return _pending = new(QueueStepKind.Operation, actorQueue.Id, actor, null, null, false);

            var target = Queue(actor.TargetQueueId);
            if (target.Team == actorQueue.Team) throw new InvalidOperationException("攻击必须指定敌方队列。");
            if (!_participated.Contains(target.Id))
            {
                // 已待命的守招优先；尚未行动的队首攻守招也可被提前拉入。
                var waiting = target.Waiting.FirstOrDefault(item => item.TargetQueueId == actorQueue.Id);
                if (waiting is not null)
                    return _pending = new(QueueStepKind.DefenseClash, actorQueue.Id, actor, target.Id, waiting, true);
                if (target.Actions.Count > 0)
                {
                    var defending = target.Actions[0];
                    if (IsDefense(defending.Kind) && defending.TargetQueueId == actorQueue.Id)
                        return _pending = new(QueueStepKind.DefenseClash, actorQueue.Id, actor, target.Id, defending, false);
                    if (IsAttack(defending.Kind))
                        return _pending = new(QueueStepKind.AttackClash, actorQueue.Id, actor, target.Id, defending, false);
                }
            }
            // 对运转只打人，不把运转拉入或消费。
            return _pending = new(QueueStepKind.UnopposedAttack, actorQueue.Id, actor, target.Id, null, false);
        }
        _roundActive = false;
        return null;
    }

    /// <summary>记录交锋前由外部计算的双方支付资格；不扣费、不推进、不选择失败策略。</summary>
    public QueueAvailabilityReport InspectPaymentAvailability(QueueCombatStep step, bool actorCanPay, bool? defenderCanPay)
    {
        if (_pending is null || !ReferenceEquals(step, _pending)) throw new InvalidOperationException("不是当前待结算行动。");
        if ((step.Defender is not null) != defenderCanPay.HasValue)
            throw new ArgumentException("交锋须明确提供双方支付资格；单方行动不能提供防守方资格。");
        var actor = new QueueParticipantAvailability(step.QueueId, step.Actor.Id, actorCanPay);
        var defender = step.Defender is null ? null
            : new QueueParticipantAvailability(step.TargetQueueId!, step.Defender.Id, defenderCanPay!.Value);
        return new(actor, defender);
    }

    /// <summary>调用方已确认目标招式无资格参与时，移除该招并转为单方行动；不付款、不结算伤害。</summary>
    public QueueCombatStep RemoveIneligibleDefender(QueueCombatStep step)
    {
        if (_pending is null || !ReferenceEquals(step, _pending) || step.Defender is null || step.TargetQueueId is null)
            throw new InvalidOperationException("当前行动没有可移除的对方招式。");
        var target = Queue(step.TargetQueueId);
        if (step.DefenderWasWaiting)
        {
            if (!target.Waiting.Remove(step.Defender)) throw new InvalidOperationException("待命守招已变化。");
        }
        else
        {
            if (target.Actions.Count == 0 || !ReferenceEquals(target.Actions[0], step.Defender))
                throw new InvalidOperationException("对方队首已变化。");
            target.Actions.RemoveAt(0);
        }
        _participated.Add(target.Id);
        return _pending = new(QueueStepKind.UnopposedAttack, step.QueueId, step.Actor, step.TargetQueueId, null, false);
    }

    public void Complete(QueueCombatStep step, QueueStepResolution result)
    {
        if (_pending is null || !ReferenceEquals(step, _pending)) throw new InvalidOperationException("不是当前待结算行动。");
        ArgumentNullException.ThrowIfNull(result);
        var actorQueue = Queue(step.QueueId);
        var targetQueue = step.TargetQueueId is null ? null : Queue(step.TargetQueueId);
        ValidateResult(step, result);

        if (!result.IsLegal)
        {
            if (result.ActorFinished) actorQueue.Actions.RemoveAt(0);
            _participated.Add(actorQueue.Id);
            _pending = null;
            return;
        }
        if (step.Kind == QueueStepKind.PrepareDefense)
        {
            actorQueue.Actions.RemoveAt(0);
            actorQueue.Waiting.Add(step.Actor);
            // 待命本身不代表一次已交锋；若此小轮被攻击，守招才占用该队列的交锋机会。
            _pending = null;
            return;
        }

        _participated.Add(actorQueue.Id);
        if (result.ActorFinished) actorQueue.Actions.RemoveAt(0);
        if (step.Defender is not null)
        {
            _participated.Add(targetQueue!.Id);
            if (result.DefenderFinished)
            {
                if (step.DefenderWasWaiting) targetQueue.Waiting.Remove(step.Defender);
                else targetQueue.Actions.RemoveAt(0);
            }
        }
        // 近战破远术没有命中：同一原式转至自己的队尾，不在本小轮再出手。
        if (step.Kind == QueueStepKind.AttackClash && step.Defender is { } opposed)
        {
            if (result.Winner == QueueClashWinner.Actor
                && step.Actor.Kind == QueueActionKind.Melee && opposed.Kind == QueueActionKind.Ranged)
                MoveFrontToBack(actorQueue);
            else if (result.Winner == QueueClashWinner.Defender
                && opposed.Kind == QueueActionKind.Melee && step.Actor.Kind == QueueActionKind.Ranged)
                MoveFrontToBack(targetQueue!);
        }
        if (result.ActorFollowUp is { } actorFollowUp)
        {
            actorQueue.Actions.Insert(0, actorFollowUp);
            _allActionIds.Add(actorFollowUp.Id);
        }
        if (result.DefenderFollowUp is { } defenderFollowUp)
        {
            targetQueue!.Actions.Insert(0, defenderFollowUp);
            _allActionIds.Add(defenderFollowUp.Id);
        }
        _pending = null;
    }

    private void ValidateResult(QueueCombatStep step, QueueStepResolution result)
    {
        if (!Enum.IsDefined(result.Winner)) throw new ArgumentException("未知交锋结果。");
        if (!result.IsLegal)
        {
            if (result.Winner != QueueClashWinner.None || result.ActorHit || result.DefenderHit
                || result.DefenderFinished || result.ActorFollowUp is not null || result.DefenderFollowUp is not null)
                throw new ArgumentException("非法行动不能产生交锋、命中或追加。");
            return;
        }
        var clash = step.Kind is QueueStepKind.AttackClash or QueueStepKind.DefenseClash;
        if (clash == (result.Winner == QueueClashWinner.None)) throw new ArgumentException("交锋结果与行动类型不一致。");
        if (!clash && result.DefenderFinished || step.Kind != QueueStepKind.AttackClash && result.DefenderHit
            || step.Kind is QueueStepKind.Operation or QueueStepKind.PrepareDefense && result.ActorHit)
            throw new ArgumentException("本行动不能产生所提供的结算。");
        if (step.Kind == QueueStepKind.AttackClash && step.Defender is { } defender)
        {
            if (result.Winner == QueueClashWinner.Actor && result.DefenderHit
                || result.Winner == QueueClashWinner.Defender && result.ActorHit
                || result.Winner == QueueClashWinner.Tie && (result.ActorHit || result.DefenderHit))
                throw new ArgumentException("攻击命中结果与交锋胜负不一致。");
            if (result.Winner == QueueClashWinner.Actor && step.Actor.Kind == QueueActionKind.Melee && defender.Kind == QueueActionKind.Ranged
                && (result.ActorFinished || result.ActorHit || !result.DefenderFinished)) throw new ArgumentException("近招破术后应转至队尾，远术结束且尚未命中。");
            if (result.Winner == QueueClashWinner.Defender && defender.Kind == QueueActionKind.Melee && step.Actor.Kind == QueueActionKind.Ranged
                && (result.DefenderFinished || result.DefenderHit || !result.ActorFinished)) throw new ArgumentException("近招破术后应转至队尾，远术结束且尚未命中。");
        }
        if (step.Kind == QueueStepKind.DefenseClash && step.Defender?.Kind == QueueActionKind.Dodge
            && result.Winner == QueueClashWinner.Defender && (result.DefenderFinished || result.ActorHit))
            throw new ArgumentException("闪避获胜后应保留且本次攻击未命中。");

        if (result.ActorFollowUp is { } actorFollowUp)
        {
            if (!IsAttack(step.Actor.Kind) || !result.ActorFinished
                || !CombatClashOutcome.GeneratesContinuation(clash, result.Winner == QueueClashWinner.Actor,
                    result.ActorHit, result.ActorStillValid, result.DefenderStillValid))
                throw new ArgumentException("行动未真实成功命中，不能追加。");
            ValidateFollowUp(actorFollowUp);
        }
        if (result.DefenderFollowUp is { } defenderFollowUp)
        {
            if (step.Kind != QueueStepKind.AttackClash || !result.DefenderFinished
                || !CombatClashOutcome.GeneratesContinuation(true, result.Winner == QueueClashWinner.Defender,
                    result.DefenderHit, result.DefenderStillValid, result.ActorStillValid))
                throw new ArgumentException("对方行动未真实成功命中，不能追加。");
            ValidateFollowUp(defenderFollowUp);
        }
        if (result.ActorFollowUp is { } a && result.DefenderFollowUp is { } b && a.Id == b.Id)
            throw new ArgumentException("追加行动编号重复。");
    }

    private void ValidateFollowUp(QueueCombatAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Id) || _allActionIds.Contains(action.Id) || !IsAttack(action.Kind))
            throw new ArgumentException("追加须为具有新编号的单次攻击。");
    }

    private QueueState Queue(string id) => _queues.TryGetValue(id, out var queue)
        ? queue : throw new ArgumentException("队列不存在：" + id);
    private static void MoveFrontToBack(QueueState queue)
    {
        var action = queue.Actions[0];
        queue.Actions.RemoveAt(0);
        queue.Actions.Add(action);
    }
    private static bool IsAttack(QueueActionKind kind) => kind is QueueActionKind.Melee or QueueActionKind.Ranged;
    private static bool IsDefense(QueueActionKind kind) => kind is QueueActionKind.Guard or QueueActionKind.Dodge;
}
