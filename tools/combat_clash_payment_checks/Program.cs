using System;
using static CombatClashPaymentLedger;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
Balance Get(CombatClashPaymentLedger ledger, string participant)
{
    Check(ledger.TryGetBalance(participant, out var value), $"missing {participant}");
    return value;
}

var ledger = new CombatClashPaymentLedger();
Check(ledger.Register("sword", new(6, 2, 3)), "register sword");
Check(ledger.Register("caster", new(4, 2, 3)), "register caster");
Check(!ledger.Register("sword", new(5, 0, 0)), "duplicate participant rejected");
Check(!ledger.Register("invalid", new(1, 4, 3)), "invalid balance rejected");

var sword = new Payment("sword", "sword-action", new(2, 0, 0));
var caster = new Payment("caster", "caster-action", new(1, 1, 0));
Check(ledger.TryPayClash("clash-1", sword, caster, out var first) == Outcome.Paid, "first clash paid");
Check(first is not null && first.First.Before == new Balance(6, 2, 3)
    && first.First.After == new Balance(4, 2, 3) && first.Second?.After == new Balance(3, 1, 3), "receipt captures both sides");
Check(ledger.TryPayClash("clash-1", sword, caster, out var replay) == Outcome.AlreadyPaid
    && replay == first, "same event is idempotent");
Check(Get(ledger, "sword") == new Balance(4, 2, 3) && Get(ledger, "caster") == new Balance(3, 1, 3), "replay does not charge");
Check(ledger.TryPayClash("clash-1", sword, caster with { Cost = new(1, 0, 0) }, out _) == Outcome.EventConflict, "event cannot change costs");
Check(ledger.TryPayClash("clash-1", caster, sword, out _) == Outcome.EventConflict, "event cannot change participants");
Check(Get(ledger, "caster") == new Balance(3, 1, 3), "conflicting replay has no charge");

var secondCaster = new Payment("caster", "second-spell", new(1, 1, 0));
Check(ledger.TryPayClash("clash-2", sword, secondCaster, out _) == Outcome.Paid, "new clash charges both again");
Check(Get(ledger, "sword") == new Balance(2, 2, 3) && Get(ledger, "caster") == new Balance(2, 0, 3), "repeated clash has independent costs");
Check(ledger.TryPayUnopposed("final-strike", sword, out var strike) == Outcome.Paid
    && strike is { PreviouslyPaidAction: true } && strike.First.UsedPreviousPayment, "paid action lands without double charge");
Check(Get(ledger, "sword") == new Balance(2, 2, 3), "landing balance unchanged");
Check(ledger.TryPayUnopposed("final-strike", sword, out _) == Outcome.AlreadyPaid, "landing event replay is idempotent");
Check(ledger.TryPayUnopposed("forged-second-landing", sword, out _) == Outcome.ActionClosed,
    "a new event cannot land the same action twice for free");
Check(ledger.TryPayClash("clash-after-landing", sword, secondCaster, out _) == Outcome.ActionClosed,
    "a landed action cannot return to a new clash");

var exhaustedSpell = new Payment("caster", "unaffordable-spell", new(1, 1, 0));
var secondSword = new Payment("sword", "fresh-sword", new(1, 0, 0));
Check(ledger.TryPayClash("clash-fail", secondSword, exhaustedSpell, out _) == Outcome.InsufficientResources, "resource failure");
Check(Get(ledger, "sword") == new Balance(2, 2, 3) && Get(ledger, "caster") == new Balance(2, 0, 3), "failed preflight never half charges");
Check(ledger.SetBalance("caster", new(2, 1, 3)), "regeneration can be synchronized");
Check(ledger.TryPayClash("clash-fail", secondSword, exhaustedSpell, out _) == Outcome.Paid, "failed event can retry after resources change");

var startup = new CombatClashPaymentLedger();
Check(startup.Register("a", new(5, 0, 0)) && startup.Register("b", new(5, 0, 0)), "startup participants");
var startAction = new Payment("a", "action-a", new(2, 0, 0));
var defend = new Payment("b", "action-b", new(1, 0, 0));
Check(startup.TryPayStartup("startup-a", startAction, out _) == Outcome.Paid, "startup payment");
Check(startup.TryPayStartup("new-startup-event", startAction, out _) == Outcome.StartupAlreadyPaid,
    "one action cannot pay a second startup under a new event");
Check(startup.TryPayUnopposed("startup-a", startAction, out _) == Outcome.EventConflict,
    "same event ID cannot change from startup to landing");
Check(startup.TryPayClash("missing-reuse", startAction, defend, out _) == Outcome.StartupPaymentRequired,
    "first clash must use the startup receipt");
Check(startup.TryPayClash("wrong-prior-cost", startAction with { UsePreviousPayment = true, Cost = new(3, 0, 0) }, defend, out _) == Outcome.PaymentMismatch,
    "startup payment cannot cover a different cost");
Check(startup.TryPayClash("first-clash", startAction with { UsePreviousPayment = true }, defend, out var firstClash) == Outcome.Paid, "first clash uses startup payment");
Check(firstClash is not null && firstClash.First.UsedPreviousPayment && firstClash.Second is { UsedPreviousPayment: false }, "receipt records payment reuse");
Check(Get(startup, "a").Stamina == 3 && Get(startup, "b").Stamina == 4, "startup and first clash charge once");
Check(startup.TryPayClash("reuse-again", startAction with { UsePreviousPayment = true }, defend, out _) == Outcome.StartupPaymentConsumed,
    "a new clash cannot reuse the first startup receipt");
Check(Get(startup, "a").Stamina == 3 && Get(startup, "b").Stamina == 4,
    "rejected free retry does not charge the other side");
var discountedRetry = startAction with { Cost = new(1, 0, 0) };
Check(startup.TryPayClash("retry-clash", discountedRetry, defend, out _) == Outcome.Paid, "independent retry uses its own cost");
Check(Get(startup, "a").Stamina == 2 && Get(startup, "b").Stamina == 3, "changed retry cost charged each side");
Check(startup.TryPayClash("reuse-new-cost", discountedRetry with { UsePreviousPayment = true }, defend, out _) == Outcome.StartupPaymentConsumed,
    "retry receipt cannot masquerade as unused startup receipt");
Check(startup.TryPayClash("invalid-reuse", new Payment("a", "never-paid", new(1, 0, 0), true), defend, out _) == Outcome.ActionNotPaid, "cannot reuse absent action payment");
Check(startup.TryPayUnopposed("stolen-action", new Payment("b", "action-a", new(0, 0, 0)), out _) == Outcome.ActionConflict, "action cannot change owner");
Check(startup.TryPayClash("same-participant", startAction, new Payment("a", "other-action", new(0, 0, 0)), out _) == Outcome.InvalidRequest, "self clash rejected");
Check(startup.TryPayClash("negative", startAction with { Cost = new(-1, 0, 0) }, defend, out _) == Outcome.InvalidRequest, "negative cost rejected");
Check(Get(startup, "b").Stamina == 3, "untriggered defense has no charge");
Check(!startup.CloseAction("b", "action-a") && startup.CloseAction("a", "action-a") && startup.CloseAction("a", "action-a"),
    "only owner closes action and repeated close is safe");
Check(startup.TryPayClash("closed-clash", startAction, defend, out _) == Outcome.ActionClosed,
    "terminal clash closure prevents new events");

var delayed = new CombatClashPaymentLedger();
Check(delayed.Register("starter", new(3, 0, 0)) && delayed.Register("guard", new(0, 0, 0)),
    "delayed clash participants");
var delayedStart = new Payment("starter", "delayed-action", new(2, 0, 0));
var delayedGuard = new Payment("guard", "delayed-guard", new(1, 0, 0));
Check(delayed.TryPayStartup("delayed-start", delayedStart, out _) == Outcome.Paid, "delayed action started");
Check(delayed.TryPayClash("delayed-clash", delayedStart with { UsePreviousPayment = true }, delayedGuard, out _)
    == Outcome.InsufficientResources, "unaffordable defense blocks first clash");
Check(Get(delayed, "starter").Stamina == 1 && Get(delayed, "guard").Stamina == 0,
    "failed first clash leaves the existing startup and guard balance intact");
Check(delayed.SetBalance("guard", new(1, 0, 0)), "guard obtains a resource");
Check(delayed.TryPayClash("delayed-clash", delayedStart with { UsePreviousPayment = true }, delayedGuard, out _)
    == Outcome.Paid, "failed first clash did not consume startup receipt");
Check(Get(delayed, "starter").Stamina == 1 && Get(delayed, "guard").Stamina == 0,
    "retry pays guard and does not charge startup twice");

var converted = new CombatClashPaymentLedger();
Check(converted.Register("user", new(3, 3, 3)) && converted.Register("enemy", new(1, 0, 0)), "converted participants");
Check(converted.TryPayClash("burn-too-far", new("user", "burn-action", new(1, 0, 3)), new("enemy", "guard", new(1, 0, 0)), out _) == Outcome.InsufficientResources, "mana capacity must retain one when burned");
Check(Get(converted, "enemy").Stamina == 1, "capacity failure leaves opponent intact");
Check(converted.TryPayClash("burn-one", new("user", "burn-action", new(1, 1, 1)), new("enemy", "guard", new(1, 0, 0)), out _) == Outcome.Paid, "precomputed conversion cost accepted");
Check(Get(converted, "user") == new Balance(2, 2, 2), "stamina mana and capacity all charged");
Check(converted.TryPayUnopposed("zero", new("enemy", "zero-action", new(0, 0, 0)), out _) == Outcome.Paid, "explicit zero effective cost supported");
Check(converted.TryPayUnopposed("missing", new("nobody", "missing-action", new(0, 0, 0)), out _) == Outcome.ParticipantNotFound, "unregistered participant rejected");

var concurrent = new CombatClashPaymentLedger();
Check(concurrent.Register("left", new(10, 0, 0)) && concurrent.Register("right", new(10, 0, 0)), "concurrent participants");
var outcomes = new Outcome[32];
Parallel.For(0, outcomes.Length, i => outcomes[i] = concurrent.TryPayClash("one-event",
    new("left", "left-action", new(2, 0, 0)), new("right", "right-action", new(3, 0, 0)), out _));
Check(outcomes.Count(value => value == Outcome.Paid) == 1 && outcomes.Count(value => value == Outcome.AlreadyPaid) == 31,
    "concurrent duplicate event commits once");
Check(Get(concurrent, "left").Stamina == 8 && Get(concurrent, "right").Stamina == 7,
    "concurrent duplicate events charge each participant once");
var landings = new Outcome[32];
Parallel.For(0, landings.Length, i => landings[i] = concurrent.TryPayUnopposed($"landing-{i}",
    new("left", "left-action", new(2, 0, 0)), out _));
Check(landings.Count(value => value == Outcome.Paid) == 1 && landings.Count(value => value == Outcome.ActionClosed) == 31,
    "concurrent forged landing events settle once");

Console.WriteLine($"CombatClashPaymentChecks: {checks} checks passed");
