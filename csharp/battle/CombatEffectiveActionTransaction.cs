using System;
using System.Collections.Generic;

/// <summary>
/// Independent, in-memory experiment: each card pays its cost once when it first
/// participates in a clash or actually acts alone, regardless of clash outcome.
/// This is not connected to battle. It does
/// not model mana conversion, cultivation callbacks, skill limits or cooldowns.
/// The caller resolves and advances queues, validates targets and outcome types,
/// and prevents finished actions from producing further combat effects.
/// Repeated clashes, successful consecutive dodges and the final hit of the same
/// payment identity do not incur another payment. Each action identifies its own
/// play/segment; callers may explicitly share a payment identity across segments.
/// Cloud-sword segments are configured by callers to share one payment identity;
/// other generated actions do not automatically inherit this choice.
/// CombatClashPaymentLedger represents the superseded pay-on-every-clash proposal;
/// callers must not combine the two ledgers for the same battle.
/// </summary>
public sealed class CombatEffectiveActionTransaction
{
    public readonly record struct Balance(int Stamina, int Mana);
    public readonly record struct Cost(int Stamina, int Mana);
    public readonly record struct ActionRequest(string ParticipantId, string ActionId, Cost Cost, string? PaymentIdentity = null)
    {
        public string PaymentKey => PaymentIdentity ?? ActionId;
    }
    public enum Eligibility { Ready, InsufficientResources }
    public enum Participation { None, Clash, SingleAction }
    public enum Status { Ready, Committed, AlreadyCommitted, InvalidRequest, NotFound, Conflict, Stale, IneligibleParticipation }
    public sealed record Preview(string EventId, ActionRequest First, ActionRequest? Second,
        Eligibility FirstEligibility, Eligibility? SecondEligibility, long Version);
    public readonly record struct ActionReceipt(ActionRequest Action, Participation Participation, Balance Before, Balance After,
        bool Charged, bool PreviouslyPaid);
    public sealed record Receipt(string EventId, ActionReceipt First, ActionReceipt? Second);

    private sealed record ActionState(string ParticipantId, Cost Cost, bool Paid);
    private sealed record ActionBinding(string ParticipantId, string PaymentKey, Cost Cost);
    private sealed record Completed(Preview Preview, Participation First, Participation? Second, Receipt Receipt);
    private readonly object _sync = new();
    private readonly Dictionary<string, Balance> _balances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionState> _actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionBinding> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Preview> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Completed> _completed = new(StringComparer.Ordinal);
    private long _version;

    public bool Register(string participantId, Balance balance)
    {
        if (string.IsNullOrWhiteSpace(participantId) || !Valid(balance)) return false;
        lock (_sync)
        {
            if (!_balances.TryAdd(participantId, balance)) return false;
            _version++;
            return true;
        }
    }

    /// <summary>Explicitly synchronize the experimental account, e.g. after regeneration.</summary>
    public bool SetBalance(string participantId, Balance balance)
    {
        if (string.IsNullOrWhiteSpace(participantId) || !Valid(balance)) return false;
        lock (_sync)
        {
            if (!_balances.ContainsKey(participantId)) return false;
            _balances[participantId] = balance;
            _version++;
            return true;
        }
    }

    public bool TryGetBalance(string participantId, out Balance balance)
    {
        lock (_sync) return _balances.TryGetValue(participantId, out balance);
    }

    /// <summary>
    /// Reserve an event identity and take a read-only eligibility snapshot. Each side
    /// is checked separately; an ineligible action must have Participation.None at commit.
    /// A paid action need not pay or prove affordability again for the same action.
    /// </summary>
    public Status Prepare(string eventId, ActionRequest first, ActionRequest? second, out Preview? preview)
    {
        preview = null;
        if (!Valid(eventId, first) || (second is { } other && (!Valid(eventId, other)
            || first.ParticipantId == other.ParticipantId || first.ActionId == other.ActionId
            || first.PaymentKey == other.PaymentKey)))
            return Status.InvalidRequest;
        lock (_sync)
        {
            if (_completed.TryGetValue(eventId, out var done))
                return done.Preview.First == first && done.Preview.Second == second ? Status.AlreadyCommitted : Status.Conflict;
            if (_pending.TryGetValue(eventId, out var pending))
            {
                if (pending.First != first || pending.Second != second) return Status.Conflict;
                if (pending.Version != _version) { _pending.Remove(eventId); return Status.Stale; }
                preview = pending;
                return Status.Ready;
            }
            if (!_balances.TryGetValue(first.ParticipantId, out var firstBalance)
                || (second is { } participant && !_balances.ContainsKey(participant.ParticipantId))) return Status.NotFound;
            if (!Compatible(first) || (second is { } requested && !Compatible(requested))) return Status.Conflict;
            var firstEligibility = GetEligibility(first, firstBalance);
            Eligibility? secondEligibility = second is { } s ? GetEligibility(s, _balances[s.ParticipantId]) : null;
            preview = new Preview(eventId, first, second, firstEligibility, secondEligibility, _version);
            _pending.Add(eventId, preview);
            return Status.Ready;
        }
    }

    /// <summary>
    /// External combat logic identifies actual participation, independent of the
    /// winner or skill effect. A clash requires both sides to participate; a single
    /// action requires exactly one side. Check all debits before applying any.
    /// Replay of an event is idempotent. Queue movement and effects are external.
    /// </summary>
    public Status Commit(Preview preview, Participation first, Participation? second, out Receipt? receipt)
    {
        receipt = null;
        if (preview is null || !Enum.IsDefined(first) || (second is { } e && !Enum.IsDefined(e))
            || (preview.Second is null) != (second is null)
            || first == Participation.Clash && second != Participation.Clash
            || second == Participation.Clash && first != Participation.Clash
            || first == Participation.SingleAction && second is not null and not Participation.None
            || second == Participation.SingleAction && first != Participation.None) return Status.InvalidRequest;
        lock (_sync)
        {
            if (_completed.TryGetValue(preview.EventId, out var completed))
            {
                if (completed.Preview != preview || completed.First != first || completed.Second != second)
                    return Status.Conflict;
                receipt = completed.Receipt;
                return Status.AlreadyCommitted;
            }
            if (!_pending.TryGetValue(preview.EventId, out var pending) || pending != preview) return Status.Conflict;
            if (preview.Version != _version) { _pending.Remove(preview.EventId); return Status.Stale; }
            if (!Compatible(preview.First) || (preview.Second is { } request && !Compatible(request))) return Status.Conflict;
            if (first != Participation.None && preview.FirstEligibility != Eligibility.Ready
                || second is not null and not Participation.None && preview.SecondEligibility != Eligibility.Ready)
                return Status.IneligibleParticipation;

            var firstReceipt = Calculate(preview.First, first);
            ActionReceipt? secondReceipt = preview.Second is { } s ? Calculate(s, second!.Value) : null;
            if (firstReceipt is null || (preview.Second is not null && secondReceipt is null)) return Status.Stale;
            // Both debits are known to be possible before either balance changes.
            _balances[preview.First.ParticipantId] = firstReceipt.Value.After;
            if (secondReceipt is { } other) _balances[other.Action.ParticipantId] = other.After;
            Mark(preview.First, firstReceipt.Value.Charged);
            if (secondReceipt is { } secondCharge) Mark(secondCharge.Action, secondCharge.Charged);
            receipt = new Receipt(preview.EventId, firstReceipt.Value, secondReceipt);
            _completed.Add(preview.EventId, new Completed(preview, first, second, receipt));
            _pending.Remove(preview.EventId);
            _version++;
            return Status.Committed;
        }
    }

    private ActionReceipt? Calculate(ActionRequest request, Participation participation)
    {
        var before = _balances[request.ParticipantId];
        var paid = _actions.TryGetValue(request.PaymentKey, out var action) && action.Paid;
        var charge = participation != Participation.None && !paid;
        if (charge && !CanAfford(before, request.Cost)) return null;
        var after = charge ? new Balance(before.Stamina - request.Cost.Stamina, before.Mana - request.Cost.Mana) : before;
        return new ActionReceipt(request, participation, before, after, charge, paid);
    }

    private void Mark(ActionRequest request, bool charged)
    {
        var paid = charged || (_actions.TryGetValue(request.PaymentKey, out var state) && state.Paid);
        _actions[request.PaymentKey] = new ActionState(request.ParticipantId, request.Cost, paid);
        _bindings[request.ActionId] = new ActionBinding(request.ParticipantId, request.PaymentKey, request.Cost);
    }

    private Eligibility GetEligibility(ActionRequest request, Balance balance) =>
        _actions.TryGetValue(request.PaymentKey, out var action) && action.Paid || CanAfford(balance, request.Cost)
            ? Eligibility.Ready : Eligibility.InsufficientResources;
    private bool Compatible(ActionRequest request) =>
        (!_bindings.TryGetValue(request.ActionId, out var binding)
            || binding.ParticipantId == request.ParticipantId && binding.PaymentKey == request.PaymentKey && binding.Cost == request.Cost)
        && (!_actions.TryGetValue(request.PaymentKey, out var action)
            || action.ParticipantId == request.ParticipantId && action.Cost == request.Cost);
    private static bool CanAfford(Balance balance, Cost cost) => balance.Stamina >= cost.Stamina && balance.Mana >= cost.Mana;
    private static bool Valid(Balance balance) => balance.Stamina >= 0 && balance.Mana >= 0;
    private static bool Valid(string eventId, ActionRequest action) => !string.IsNullOrWhiteSpace(eventId)
        && !string.IsNullOrWhiteSpace(action.ParticipantId) && !string.IsNullOrWhiteSpace(action.ActionId)
        && (action.PaymentIdentity is null || !string.IsNullOrWhiteSpace(action.PaymentIdentity))
        && action.Cost.Stamina >= 0 && action.Cost.Mana >= 0;
}
