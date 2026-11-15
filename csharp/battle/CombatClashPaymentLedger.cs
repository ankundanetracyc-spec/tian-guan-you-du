using System;
using System.Collections.Generic;

/// <summary>
/// Legacy experiment: charges both participants again on every new clash, which
/// conflicts with the revised rule that an action pays only for its first clash.
/// Retained only as historical test material; do not integrate into live combat.
/// It owns its balances;
/// it does not read or mutate the live battle manager or resolve skill costs.
/// One instance represents one battle and all calls on it are synchronized.
/// </summary>
public sealed class CombatClashPaymentLedger
{
    public readonly record struct Balance(int Stamina, int Mana, int ManaCapacity);
    public readonly record struct Cost(int Stamina, int Mana, int ManaCapacityBurn);
    public readonly record struct Payment(string ParticipantId, string ActionId, Cost Cost, bool UsePreviousPayment = false);
    public readonly record struct Charge(Payment Payment, Balance Before, Balance After, bool UsedPreviousPayment);
    public sealed record Receipt(string EventId, Charge First, Charge? Second, bool PreviouslyPaidAction);

    public enum Outcome
    {
        Paid,
        AlreadyPaid,
        InsufficientResources,
        ParticipantNotFound,
        InvalidRequest,
        EventConflict,
        ActionConflict,
        ActionNotPaid,
        PaymentMismatch,
        StartupPaymentRequired,
        StartupAlreadyPaid,
        StartupPaymentConsumed,
        ActionClosed
    }

    private enum Operation { Clash, Startup, Unopposed }
    private readonly record struct Request(Operation Operation, Payment First, Payment? Second);
    private sealed class ActionState(string participantId)
    {
        public string ParticipantId { get; } = participantId;
        public Cost? StartupCost { get; init; }
        public bool HasClashed { get; set; }
        public bool Closed { get; set; }
    }
    private sealed record Entry(Request Request, Receipt Receipt);
    private readonly object _sync = new();
    private readonly Dictionary<string, Balance> _balances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionState> _actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _events = new(StringComparer.Ordinal);

    public bool Register(string participantId, Balance balance)
    {
        if (string.IsNullOrWhiteSpace(participantId) || !Valid(balance)) return false;
        lock (_sync) return _balances.TryAdd(participantId, balance);
    }

    /// <summary>Synchronize a battle-owned balance at an explicit boundary, such as regeneration.</summary>
    public bool SetBalance(string participantId, Balance balance)
    {
        if (string.IsNullOrWhiteSpace(participantId) || !Valid(balance)) return false;
        lock (_sync)
        {
            if (!_balances.ContainsKey(participantId)) return false;
            _balances[participantId] = balance;
            return true;
        }
    }

    public bool TryGetBalance(string participantId, out Balance balance)
    {
        if (string.IsNullOrWhiteSpace(participantId)) { balance = default; return false; }
        lock (_sync) return _balances.TryGetValue(participantId, out balance);
    }

    /// <summary>
    /// Charge both participants for a clash. An explicitly flagged participant can use
    /// a prior action payment, for example when startup and first clash are the same payment.
    /// Repeated clashes otherwise receive a fresh charge.
    /// </summary>
    public Outcome TryPayClash(string eventId, Payment first, Payment second, out Receipt? receipt)
    {
        receipt = null;
        if (!Valid(eventId, first) || !Valid(eventId, second)
            || first.ParticipantId == second.ParticipantId || first.ActionId == second.ActionId)
            return Outcome.InvalidRequest;
        var request = new Request(Operation.Clash, first, second);
        lock (_sync)
        {
            if (_events.TryGetValue(eventId, out var previous))
                return Replay(previous, request, out receipt);
            if (!_balances.TryGetValue(first.ParticipantId, out var firstBefore)
                || !_balances.TryGetValue(second.ParticipantId, out var secondBefore))
                return Outcome.ParticipantNotFound;
            var firstCheck = CheckClashAction(first);
            if (firstCheck != Outcome.Paid) return firstCheck;
            var secondCheck = CheckClashAction(second);
            if (secondCheck != Outcome.Paid) return secondCheck;
            var firstAfter = firstBefore;
            var secondAfter = secondBefore;
            if ((!first.UsePreviousPayment && !TryApply(firstBefore, first.Cost, out firstAfter))
                || (!second.UsePreviousPayment && !TryApply(secondBefore, second.Cost, out secondAfter)))
                return Outcome.InsufficientResources;

            receipt = new Receipt(eventId, new Charge(first, firstBefore, firstAfter, first.UsePreviousPayment),
                new Charge(second, secondBefore, secondAfter, second.UsePreviousPayment), false);
            _balances[first.ParticipantId] = firstAfter;
            _balances[second.ParticipantId] = secondAfter;
            MarkClash(first);
            MarkClash(second);
            _events.Add(eventId, new Entry(request, receipt));
            return Outcome.Paid;
        }
    }

    /// <summary>Pay an action's startup once; the first clash can explicitly reuse this payment.</summary>
    public Outcome TryPayStartup(string eventId, Payment payment, out Receipt? receipt) =>
        TryPaySingle(eventId, payment, Operation.Startup, out receipt);

    /// <summary>
    /// Charge an unopposed action, unless that same action already paid for startup or a clash.
    /// Guard standby has no payment call; callers invoke this only when an action lands.
    /// </summary>
    public Outcome TryPayUnopposed(string eventId, Payment payment, out Receipt? receipt)
        => TryPaySingle(eventId, payment, Operation.Unopposed, out receipt);

    private Outcome TryPaySingle(string eventId, Payment payment, Operation operation, out Receipt? receipt)
    {
        receipt = null;
        if (!Valid(eventId, payment)) return Outcome.InvalidRequest;
        if (operation == Operation.Startup && payment.UsePreviousPayment) return Outcome.InvalidRequest;
        var request = new Request(operation, payment, null);
        lock (_sync)
        {
            if (_events.TryGetValue(eventId, out var previous))
                return Replay(previous, request, out receipt);
            if (!_balances.TryGetValue(payment.ParticipantId, out var before)) return Outcome.ParticipantNotFound;
            _actions.TryGetValue(payment.ActionId, out var action);
            if (action is not null && action.ParticipantId != payment.ParticipantId) return Outcome.ActionConflict;
            if (action is { Closed: true }) return Outcome.ActionClosed;
            if (operation == Operation.Startup && action is not null) return Outcome.StartupAlreadyPaid;
            var wasPaid = action is not null;
            if (payment.UsePreviousPayment && !wasPaid) return Outcome.ActionNotPaid;
            var after = before;
            if (!wasPaid && !TryApply(before, payment.Cost, out after)) return Outcome.InsufficientResources;
            receipt = new Receipt(eventId, new Charge(payment, before, after, wasPaid), null, wasPaid);
            _balances[payment.ParticipantId] = after;
            if (action is null)
            {
                action = new ActionState(payment.ParticipantId)
                {
                    StartupCost = operation == Operation.Startup ? payment.Cost : null
                };
                _actions.Add(payment.ActionId, action);
            }
            if (operation == Operation.Unopposed) action.Closed = true;
            _events.Add(eventId, new Entry(request, receipt));
            return Outcome.Paid;
        }
    }

    /// <summary>End a paid action after a terminal clash or cancellation. Repeated closes are safe.</summary>
    public bool CloseAction(string participantId, string actionId)
    {
        if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(actionId)) return false;
        lock (_sync)
        {
            if (!_actions.TryGetValue(actionId, out var action) || action.ParticipantId != participantId) return false;
            action.Closed = true;
            return true;
        }
    }

    private Outcome CheckClashAction(Payment payment)
    {
        if (!_actions.TryGetValue(payment.ActionId, out var action))
            return payment.UsePreviousPayment ? Outcome.ActionNotPaid : Outcome.Paid;
        if (action.ParticipantId != payment.ParticipantId) return Outcome.ActionConflict;
        if (action.Closed) return Outcome.ActionClosed;
        if (payment.UsePreviousPayment)
        {
            if (action.HasClashed) return Outcome.StartupPaymentConsumed;
            if (action.StartupCost is null) return Outcome.ActionNotPaid;
            return action.StartupCost.Value == payment.Cost ? Outcome.Paid : Outcome.PaymentMismatch;
        }
        return action.StartupCost is not null && !action.HasClashed ? Outcome.StartupPaymentRequired : Outcome.Paid;
    }

    private void MarkClash(Payment payment)
    {
        if (!_actions.TryGetValue(payment.ActionId, out var action))
        {
            action = new ActionState(payment.ParticipantId);
            _actions.Add(payment.ActionId, action);
        }
        action.HasClashed = true;
    }

    private static Outcome Replay(Entry entry, Request request, out Receipt? receipt)
    {
        receipt = entry.Request == request ? entry.Receipt : null;
        return receipt is null ? Outcome.EventConflict : Outcome.AlreadyPaid;
    }

    private static bool Valid(string eventId, Payment payment) =>
        !string.IsNullOrWhiteSpace(eventId) && !string.IsNullOrWhiteSpace(payment.ParticipantId)
        && !string.IsNullOrWhiteSpace(payment.ActionId) && payment.Cost.Stamina >= 0
        && payment.Cost.Mana >= 0 && payment.Cost.ManaCapacityBurn >= 0;

    private static bool Valid(Balance balance) => balance.Stamina >= 0 && balance.Mana >= 0
        && balance.ManaCapacity >= 0 && balance.Mana <= balance.ManaCapacity;

    private static bool TryApply(Balance balance, Cost cost, out Balance result)
    {
        result = balance;
        if (balance.Stamina < cost.Stamina || balance.Mana < cost.Mana
            || (cost.ManaCapacityBurn > 0 && balance.ManaCapacity - cost.ManaCapacityBurn < 1))
            return false;
        var capacity = balance.ManaCapacity - cost.ManaCapacityBurn;
        result = new Balance(balance.Stamina - cost.Stamina,
            Math.Min(balance.Mana - cost.Mana, capacity), capacity);
        return true;
    }
}
