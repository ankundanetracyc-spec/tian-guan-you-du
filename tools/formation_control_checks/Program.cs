using C = FormationControlCircuit;
using R = FormationGuardAssaultRuntime;
using T = FormationTruth;

// All quantities here are explicit isolated fixtures, not balance or production defaults.
internal static class Program
{
    private static int _checks, _groups;

    private static int Main()
    {
        try
        {
            Run("ledger mixed FIFO and source-only stop", LedgerFifo);
            Run("ledger replacement order and final notifications", LedgerReplacement);
            Run("ledger legacy compatibility and ordinary protection", LegacyCompatibility);
            Run("ledger invalid and overflowing operations are atomic", LedgerInvalid);
            Run("ledger deterministic mixed operation accounting", LedgerSequence);
            Run("deferred notifications and observer failure isolation", DeferredNotifications);
            Run("runtime committed notifications reject reentry without refunds", RuntimeNotifications);
            Run("same health different history and unknown recovery", Hysteresis);
            Run("permit changes never replay old spell", EventSwitch);
            Run("continuous guard payment and equal renewal", Renewal);
            Run("stop/exhaustion preserves ordinary and segment quota", StopAndExhaustion);
            Run("round identity supply restart and no retroactive protection", Lifecycle);
            Run("invalid source skips recharge and independent guard sources", SupplyAndInstances);
            Run("resource and peak failure do not spend or claim quota", FailedTransactions);
            Run("actor rejection rollback and dead actor silence", ActorRollback);
            Run("reserve is a typed threshold not generated energy", Reserve);
            Run("three-valued gates and typed graph event identity", TypedGraph);
            Run("zero amplitude cannot advance edited counters or trigger pulse shield", ZeroAmplitude);
            Run("bad graph topology and bad observations are atomic", InvalidGraph);
            Console.WriteLine($"FORMATION_CONTROL_CHECKS_PASS groups={_groups} checks={_checks}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FORMATION_CONTROL_CHECKS_FAIL " + error);
            return 1;
        }
    }

    private static void Run(string name, Action test)
    {
        test(); _groups++;
        Console.WriteLine("FORMATION_CONTROL_GROUP_PASS " + name);
    }

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { Check(true, message); return; }
        catch (OverflowException) { Check(true, message); return; }
        throw new InvalidOperationException("Expected rejection: " + message);
    }

    private sealed class Actor : IFormationGuardActor
    {
        public int Health { get; set; } = 80;
        public int MaxHealth { get; set; } = 100;
        public int Mana { get; set; } = 9;
        public bool IsAlive => Health > 0;
        public FormationShieldLedger Shields { get; } = new();
        public int EnemyHealth { get; set; } = 200;
        public bool RejectAttack { get; set; }
        public int Calls { get; private set; }
        public bool TryAttack(int amount)
        {
            Calls++;
            if (RejectAttack || !IsAlive || EnemyHealth <= 0 || amount <= 0) return false;
            EnemyHealth = Math.Max(0, EnemyHealth - amount);
            return true;
        }
    }

    private static R.Settings Settings() => new(50, 30, 0, 20, 20, 20, 3, 10, 2, 4, 7, 3, 2, 30, 60);
    private static R.Result Apply(R runtime, string id, R.ActionKind kind, int value = 0, bool flag = true) => runtime.Apply(new(id, kind, value, flag));
    private static R.Snapshot S(R runtime) => runtime.GetSnapshot();
    private static string Shape(FormationShieldLedger ledger) => string.Join("|", ledger.Snapshot.Select(l => $"{l.SourceId}:{l.Remaining}:{l.IsOrdinary}:{l.Order}"));

    private static void LedgerFifo()
    {
        var ledger = new FormationShieldLedger();
        ledger.AddOrdinary(5); ledger.ReplaceSource("guard-a", 7); ledger.AddOrdinary(3); ledger.ReplaceSource("guard-b", 4);
        Check(ledger.Total == 19 && ledger.Absorb(8) == 8, "damage consumes actual public total");
        Check(ledger.GetRemaining("guard-a") == 4 && ledger.GetRemaining("guard-b") == 4, "old ordinary then old guard absorb first");
        Check(ledger.RemoveSource("guard-a") == 4 && ledger.Total == 7, "stop removes only own surviving source");
        Check(ledger.Snapshot[0].IsOrdinary && ledger.Snapshot[0].Remaining == 3, "new ordinary survives stop");
        Check(ledger.Absorb(100) == 7 && ledger.Total == 0 && ledger.Snapshot.Count == 0, "overkill returns actual absorbed and clears exhausted entries");
        Check(ledger.RemoveSource("guard-a") == 0 && ledger.GetRemaining("guard-a") == 0, "removing missing source has no effect");
    }

    private static void LedgerReplacement()
    {
        var ledger = new FormationShieldLedger(); var changes = new List<(int Old, int New)>();
        ledger.Changed += (oldTotal, newTotal) => changes.Add((oldTotal, newTotal));
        ledger.ReplaceSource("guard", 4); ledger.AddOrdinary(5);
        var held = ledger.Snapshot;
        changes.Clear(); ledger.ReplaceSource("guard", 4);
        Check(changes.SequenceEqual(new[] { (9, 9) }), "equal renewal sends one final equal-total notification");
        Check(held[0].SourceId == "guard" && ledger.Snapshot[^1].SourceId == "guard", "replacement moves acquisition order without mutating prior snapshot");
        changes.Clear(); Check(ledger.Absorb(6) == 6, "compound FIFO absorption succeeds");
        Check(changes.SequenceEqual(new[] { (9, 3) }) && ledger.GetRemaining("guard") == 3, "one damage transaction publishes one final decrease");
        changes.Clear(); ledger.ReplaceSource("guard", 2);
        Check(changes.SequenceEqual(new[] { (3, 2) }), "lower renewal never reports an artificial gain");
    }

    private static void LegacyCompatibility()
    {
        var ledger = new FormationShieldLedger();
        ledger.SetLegacyTotal(5); ledger.ReplaceSource("guard", 4); ledger.SetLegacyTotal(12);
        Check(ledger.Snapshot.Select(l => l.IsOrdinary).SequenceEqual(new[] { true, false, true }), "legacy gain remains a new ordinary slice");
        ledger.SetLegacyTotal(6);
        Check(ledger.GetRemaining("guard") == 3 && ledger.Total == 6, "legacy decrease follows mixed FIFO");
        var shape = Shape(ledger); ledger.SetLegacyTotal(6);
        Check(Shape(ledger) == shape, "equal legacy assignment keeps source and order");
        foreach (var layer in ledger.Snapshot.Where(l => l.IsOrdinary))
            Reject(() => ledger.RemoveSource(layer.SourceId), "ordinary reserved source cannot be withdrawn by named API");
        ledger.SetLegacyTotal(-9);
        Check(ledger.Total == 0 && ledger.GetRemaining("guard") == 0, "negative legacy total clears all without negative shield");
        ledger.AddOrdinary(0); ledger.ReplaceSource("absent", 0); ledger.Absorb(0);
        Check(ledger.Total == 0 && ledger.Snapshot.Count == 0, "zero operations do not leave ghost entries");
    }

    private static void LedgerInvalid()
    {
        var ledger = new FormationShieldLedger();
        ledger.ReplaceSource("guard", 4); ledger.AddOrdinary(int.MaxValue - 4);
        var before = Shape(ledger); var notices = 0; ledger.Changed += (_, _) => notices++;
        Reject(() => ledger.AddOrdinary(1), "ordinary overflow");
        Reject(() => ledger.ReplaceSource("guard", 5), "replacement overflow");
        Reject(() => ledger.ReplaceSource("other", 1), "new source overflow");
        foreach (var bad in new[] { "", " ", "$ordinary", "$ordinary/123", "bad\nsource" })
        {
            Reject(() => ledger.GetRemaining(bad), "invalid source lookup");
            Reject(() => ledger.ReplaceSource(bad, 3), "invalid source grant");
            Reject(() => ledger.RemoveSource(bad), "invalid source removal");
        }
        Reject(() => ledger.AddOrdinary(-1), "negative grant");
        Reject(() => ledger.ReplaceSource("guard", -1), "negative replacement");
        Reject(() => ledger.Absorb(-1), "negative damage");
        Check(Shape(ledger) == before && ledger.Total == int.MaxValue && notices == 0, "every rejected operation preserves all sources total order and notifications");
        ledger.ReplaceSource("guard", 4);
        Check(ledger.Total == int.MaxValue && notices == 1, "equal valid replacement at maximum total is permitted");
    }

    private static void LedgerSequence()
    {
        var ledger = new FormationShieldLedger();
        var random = new Random(20261109); var expected = 0;
        for (var index = 0; index < 500; index++)
        {
            var amount = random.Next(0, 31); var id = "source-" + random.Next(0, 4);
            switch (random.Next(0, 5))
            {
                case 0: ledger.AddOrdinary(amount); expected += amount; break;
                case 1: expected += amount - ledger.GetRemaining(id); ledger.ReplaceSource(id, amount); break;
                case 2: expected -= ledger.GetRemaining(id); ledger.RemoveSource(id); break;
                case 3:
                    var absorbed = ledger.Absorb(amount); Check(absorbed == Math.Min(amount, expected), "mixed sequence actual absorption"); expected -= absorbed; break;
                case 4: expected = amount; ledger.SetLegacyTotal(amount); break;
            }
            var snapshot = ledger.Snapshot;
            Check(ledger.Total == expected && snapshot.Sum(l => l.Remaining) == expected, "mixed sequence conserves total");
            Check(snapshot.All(l => l.Remaining > 0) && snapshot.Zip(snapshot.Skip(1)).All(p => p.First.Order < p.Second.Order), "FIFO entries remain positive and ordered");
            Check(snapshot.Where(l => !l.IsOrdinary).Select(l => l.SourceId).Distinct().Count() == snapshot.Count(l => !l.IsOrdinary), "replacement never leaves duplicate named capacity");
        }
    }

    private static void Hysteresis()
    {
        var low = new R(Settings(), new Actor { Health = 20 });
        var high = new R(Settings(), new Actor { Health = 80 });
        Apply(low, "rise", R.ActionKind.SetHealth, 45); Apply(high, "fall", R.ActionKind.SetHealth, 45);
        Check(S(low).Mode == T.True && S(high).Mode == T.False && S(low).Health == S(high).Health, "45 percent retains distinct prior threshold history");
        Apply(low, "lost", R.ActionKind.Observation, flag: false); Apply(high, "lost", R.ActionKind.Observation, flag: false);
        Check(S(low).Mode == T.Unknown && S(high).Mode == T.Unknown && S(low).AttackAllowed == T.Unknown, "invalid observations are unknown rather than safe zero");
        Apply(low, "restore", R.ActionKind.Observation); Apply(high, "restore", R.ActionKind.Observation);
        Check(S(low).Mode == T.True && S(high).Mode == T.False, "unknown interval does not overwrite stored history");
        Check(S(new R(Settings(), new Actor { Health = 45 })).Mode == T.True, "first valid middle observation starts defensive");
    }

    private static void DeferredNotifications()
    {
        var ledger = new FormationShieldLedger(); var changes = new List<(int, int)>();
        ledger.Changed += (_, _) => throw new InvalidOperationException("isolated fixture observer failure");
        ledger.Changed += (oldTotal, newTotal) => changes.Add((oldTotal, newTotal));
        ledger.AddOrdinary(5);
        Check(ledger.Total == 5 && ledger.NotificationErrorCount == 1 && changes.SequenceEqual(new[] { (0, 5) }), "throwing listener never rolls back a committed ordinary grant or skips later listeners");
        changes.Clear();
        var outer = ledger.DeferNotifications();
        ledger.ReplaceSource("guard", 10);
        using (ledger.DeferNotifications()) { ledger.Absorb(3); ledger.ReplaceSource("guard", 10); }
        Check(changes.Count == 0 && ledger.Total == 12 && ledger.NotificationErrorCount == 1, "nested scope applies sources immediately while deferring all observers");
        outer.Dispose(); outer.Dispose();
        Check(changes.SequenceEqual(new[] { (5, 12) }) && ledger.NotificationErrorCount == 2, "outer scope publishes one original-to-final notification and double dispose is harmless");
        changes.Clear();
        using (ledger.DeferNotifications()) ledger.ReplaceSource("guard", 10);
        Check(changes.SequenceEqual(new[] { (12, 12) }) && ledger.NotificationErrorCount == 3, "equal source replacement remains a completed change in a deferred scope");
        changes.Clear(); using (ledger.DeferNotifications()) { }
        Check(changes.Count == 0 && ledger.NotificationErrors.Count == 3, "empty scopes never manufacture notifications or erase diagnostics");
    }

    private static void RuntimeNotifications()
    {
        var badObserver = new Actor { Health = 20 };
        badObserver.Shields.Changed += (_, _) => throw new InvalidOperationException("fixture visual observer");
        var startup = new R(Settings(), badObserver);
        Check(S(startup).GuardPayments == 1 && S(startup).GuardRemaining == 10 && S(startup).SourceEnergy == 27 && badObserver.Shields.NotificationErrorCount == 1,
            "constructor observer failure leaves one paid real guard instead of free protection");

        var actor = new Actor(); var runtime = new R(Settings(), actor); R.Snapshot? observed = null;
        actor.Shields.Changed += (_, _) => runtime.Apply(new("reentrant", R.ActionKind.Energy, 50));
        actor.Shields.Changed += (_, _) => observed = runtime.GetSnapshot();
        var result = Apply(runtime, "enter-guard", R.ActionKind.SetHealth, 20);
        Check(observed is { GuardRunning: true, GuardPayments: 1, GuardRemaining: 10, SourceEnergy: 27 }, "observers see final paid running state and full counters");
        Check(result.Snapshot.GuardPayments == 1 && S(runtime).SourceEnergy == 27 && actor.Shields.NotificationErrorCount == 1,
            "reentry is rejected until all notifications finish, never refilling or paying twice");
        Check(actor.Shields.NotificationErrors[0] is InvalidOperationException, "reentry refusal is retained as inspectable notification diagnostic");
        Apply(runtime, "reentrant", R.ActionKind.Energy, 1);
        Check(S(runtime).SourceEnergy == 28 && S(runtime).GuardPayments == 1, "rejected reentry never claims the external occurrence identity");
    }

    private static void EventSwitch()
    {
        var actor = new Actor { Health = 20 }; var runtime = new R(Settings(), actor);
        Apply(runtime, "blocked-cast", R.ActionKind.Spell);
        Apply(runtime, "open", R.ActionKind.SetHealth, 80);
        Check(actor.Calls == 0 && S(runtime).Attacks == 0, "opening the permit never manufactures an old spell");
        var first = Apply(runtime, "new-cast", R.ActionKind.Spell);
        var energy = S(runtime).SourceEnergy;
        var repeat = Apply(runtime, "new-cast", R.ActionKind.Spell);
        Check(actor.Calls == 1 && first.Snapshot.Attacks == 1 && repeat.Duplicate && S(runtime).SourceEnergy == energy, "same spell occurrence submits and pays once");
        Reject(() => Apply(runtime, "new-cast", R.ActionKind.Damage, 3), "event identity cannot rewrite content");
        Apply(runtime, "unknown", R.ActionKind.Observation, flag: false);
        Apply(runtime, "unknown-cast", R.ActionKind.Spell);
        Apply(runtime, "known", R.ActionKind.Observation);
        Check(actor.Calls == 1, "unknown blocked spell is never replayed after recovery");
        Apply(runtime, "next-cast", R.ActionKind.Spell);
        Check(actor.Calls == 2, "a genuinely new spell may use the restored current permit");
    }

    private static void Renewal()
    {
        var actor = new Actor { Health = 20 }; var runtime = new R(Settings(), actor);
        Check(S(runtime).GuardRunning && S(runtime).GuardPayments == 1 && S(runtime).EnergySpent == 3 && actor.Shields.Total == 10, "startup pays and grants one real source");
        Apply(runtime, "refresh-a", R.ActionKind.Refresh); Apply(runtime, "refresh-b", R.ActionKind.Refresh);
        Check(S(runtime).GuardPayments == 1 && actor.Shields.Total == 10, "repeated true permit does not maintain twice within the segment");
        var changes = new List<(int, int)>(); actor.Shields.Changed += (oldTotal, newTotal) => changes.Add((oldTotal, newTotal));
        Apply(runtime, "round-2", R.ActionKind.NextRound);
        Check(S(runtime).GuardPayments == 2 && S(runtime).EnergySpent == 6 && S(runtime).SourceEnergy == 24, "next segment really pays even when total capacity stays equal");
        Check(changes.SequenceEqual(new[] { (10, 10) }) && actor.Shields.Total == 10, "renewal replaces atomically without a transient removal or duplicate shield");
        Apply(runtime, "round-2", R.ActionKind.NextRound);
        Check(S(runtime).Round == 2 && S(runtime).GuardPayments == 2 && S(runtime).EnergySpent == 6, "repeated boundary never renews twice");
        Apply(runtime, "unknown", R.ActionKind.Observation, flag: false);
        Apply(runtime, "round-3", R.ActionKind.NextRound);
        Check(S(runtime).GuardRunning && S(runtime).GuardPayments == 3 && S(runtime).Mode == T.Unknown, "an actually running permitted service pays to continue through missing data");
    }

    private static void StopAndExhaustion()
    {
        var actor = new Actor { Health = 20 }; actor.Shields.AddOrdinary(5);
        var runtime = new R(Settings(), actor);
        Apply(runtime, "ordinary", R.ActionKind.OrdinaryShield, 7);
        Apply(runtime, "hit", R.ActionKind.Damage, 8);
        Check(S(runtime).GuardRemaining == 7 && S(runtime).Shield == 14 && actor.Health == 20, "damage consumes old ordinary then guard before newer ordinary");
        Apply(runtime, "stop", R.ActionKind.SetHealth, 80);
        Check(S(runtime).Shield == 7 && S(runtime).GuardRemaining == 0 && !S(runtime).GuardRunning, "stopping preserves newer ordinary shield");
        Apply(runtime, "reopen", R.ActionKind.SetHealth, 20);
        Check(!S(runtime).GuardRunning && S(runtime).GuardPaidThisRound && S(runtime).GuardPayments == 1, "same segment cannot close and reopen to refresh capacity");
        Apply(runtime, "next", R.ActionKind.NextRound);
        Check(S(runtime).GuardRunning && S(runtime).Shield == 17, "new real segment can acquire a fresh guard source");
        Apply(runtime, "empty", R.ActionKind.Damage, 17);
        Check(S(runtime).GuardRemaining == 0 && !S(runtime).GuardRunning && S(runtime).Health == 20, "capacity exhaustion stops actual service without recreating it");
        Apply(runtime, "energy", R.ActionKind.Energy, 50);
        Check(S(runtime).GuardPayments == 2 && !S(runtime).GuardRunning, "extra energy does not reset spent segment quota");
        Apply(runtime, "missing", R.ActionKind.Observation, flag: false);
        Apply(runtime, "next-unknown", R.ActionKind.NextRound);
        Check(!S(runtime).GuardRunning && S(runtime).GuardPayments == 2, "unknown cannot restart an exhausted service at the next boundary");
    }

    private static void Lifecycle()
    {
        var actor = new Actor { Health = 80 }; var runtime = new R(Settings() with { RechargePerRound = 5 }, actor);
        Apply(runtime, "hit-before-start", R.ActionKind.Damage, 55);
        Check(actor.Health == 25 && S(runtime).GuardRemaining == 10, "post-hit guard protects future damage, never undoes the triggering hit");
        Apply(runtime, "loss", R.ActionKind.Supply, flag: false);
        Check(S(runtime).GuardRemaining == 0 && !S(runtime).GuardRunning, "supply failure removes only own ongoing shield immediately");
        Apply(runtime, "supply", R.ActionKind.Supply);
        Apply(runtime, "off", R.ActionKind.Enable, flag: false); Apply(runtime, "on", R.ActionKind.Enable);
        Check(S(runtime).SourceEnergy == 27 && S(runtime).GuardPayments == 1 && !S(runtime).GuardRunning, "source restore and restart neither refund energy nor wash segment quota");
        Apply(runtime, "boundary", R.ActionKind.NextRound); var after = S(runtime);
        Apply(runtime, "boundary", R.ActionKind.NextRound);
        Check(after.Round == 2 && S(runtime) == after && after.SourceEnergy == 29, "one boundary recharges once then pays current maintenance once");
        Apply(runtime, "attack-mode", R.ActionKind.SetHealth, 80);
        Apply(runtime, "off2", R.ActionKind.Enable, flag: false); Apply(runtime, "on2", R.ActionKind.Enable);
        Apply(runtime, "middle", R.ActionKind.SetHealth, 45);
        Check(S(runtime).Mode == T.False && S(runtime).GuardPayments == 2, "restart retains attack-side interval history");
    }

    private static void FailedTransactions()
    {
        var actor = new Actor { Health = 20 }; var poor = new R(Settings() with { InitialEnergy = 2 }, actor);
        Check(!S(poor).GuardRunning && !S(poor).GuardPaidThisRound && S(poor).SourceEnergy == 2, "insufficient energy never claims guard success");
        Apply(poor, "fund", R.ActionKind.Energy, 1);
        Check(S(poor).GuardPayments == 1 && S(poor).SourceEnergy == 0, "a new genuine opportunity may pay an earlier uncompleted request");
        var weak = new R(Settings() with { BoardPeak = 2 }, new Actor { Health = 20 });
        Check(!S(weak).GuardPaidThisRound && S(weak).EnergySpent == 0 && S(weak).SourceEnergy == 30, "insufficient guard peak never deducts energy");
        var attacker = new Actor(); var capped = new R(Settings() with { LinkPeak = 3 }, attacker);
        Apply(capped, "too-large", R.ActionKind.Spell);
        Check(attacker.Calls == 0 && S(capped).SourceEnergy == 30 && S(capped).Attacks == 0, "attack must fit all source link board peak budgets before any host effect");
        var poorAttack = new R(Settings() with { InitialEnergy = 3 }, new Actor());
        Apply(poorAttack, "poor-cast", R.ActionKind.Spell);
        Check(S(poorAttack).SourceEnergy == 3 && S(poorAttack).Attacks == 0, "attack fee failure does not spend or consume success limit");
        Apply(poorAttack, "fund", R.ActionKind.Energy, 1);
        Check(S(poorAttack).Attacks == 0, "energy recovery never replays the old rejected cast");
        Apply(poorAttack, "new", R.ActionKind.Spell);
        Check(S(poorAttack).Attacks == 1 && S(poorAttack).SourceEnergy == 0, "new cast may commit after energy recovery");
        Reject(() => new R(Settings() with { GuardCost = -1 }, new Actor()), "negative guard fee rejected");
        Reject(() => new R(Settings() with { AttackReserve = int.MaxValue }, new Actor()), "overflowing reserve budget rejected");
    }

    private static void SupplyAndInstances()
    {
        var actor = new Actor();
        var runtime = new R(Settings() with { InitialEnergy = 5, RechargePerRound = 4 }, actor);
        Apply(runtime, "source-lost", R.ActionKind.Supply, flag: false);
        Apply(runtime, "lost-round", R.ActionKind.NextRound);
        Check(S(runtime).SourceEnergy == 5 && S(runtime).Round == 2, "an invalid source receives no automatic boundary energy");
        Apply(runtime, "source-restored", R.ActionKind.Supply);
        Check(S(runtime).SourceEnergy == 5, "source recovery never catches up missed recharge");
        Apply(runtime, "valid-round", R.ActionKind.NextRound);
        Check(S(runtime).SourceEnergy == 9, "next actual valid boundary may recharge once");
        Apply(runtime, "disable", R.ActionKind.Enable, flag: false);
        Apply(runtime, "disabled-round", R.ActionKind.NextRound);
        Check(!S(runtime).Enabled && S(runtime).SourceEnergy == 13, "disabling control does not make a valid physical source invalid");
        Apply(runtime, "enable", R.ActionKind.Enable);
        Check(S(runtime).SourceEnergy == 13 && S(runtime).Attacks == 0, "reenabling control neither refills source nor replays events");

        var shared = new Actor { Health = 20 }; shared.Shields.AddOrdinary(5);
        var first = new R(Settings(), shared);
        var second = new R(Settings(), shared);
        Check(first.GuardSourceId != second.GuardSourceId && shared.Shields.Total == 25, "two physical runtime instances receive distinct sources");
        Apply(first, "stop-first", R.ActionKind.Enable, flag: false);
        Check(shared.Shields.Total == 15 && shared.Shields.GetRemaining(second.GuardSourceId) == 10, "one runtime cannot stop another runtime's protection");
        Apply(second, "stop-second", R.ActionKind.Enable, flag: false);
        Check(shared.Shields.Total == 5 && shared.Shields.Snapshot.All(l => l.IsOrdinary), "both stops preserve the preexisting ordinary source");
        actor.MaxHealth = 0; Apply(runtime, "invalid-max", R.ActionKind.Refresh);
        Check(S(runtime).Mode == T.Unknown && S(runtime).AttackAllowed == T.Unknown, "invalid current maximum never divides by zero or opens attack");
    }

    private static void ActorRollback()
    {
        var actor = new Actor { RejectAttack = true }; var runtime = new R(Settings() with { AttackLimitPerRound = 1 }, actor);
        Apply(runtime, "reject", R.ActionKind.Spell);
        Check(actor.Calls == 1 && S(runtime).SourceEnergy == 30 && S(runtime).Attacks == 0 && S(runtime).EnergySpent == 0, "actual host rejection rolls fee peak and success quota back");
        actor.RejectAttack = false; Apply(runtime, "commit", R.ActionKind.Spell);
        Check(S(runtime).Attacks == 1 && S(runtime).SourceEnergy == 26, "failed host submission leaves later success opportunity");
        Apply(runtime, "limited", R.ActionKind.Spell);
        Check(actor.Calls == 2 && S(runtime).SourceEnergy == 26, "success limit blocks before host or fee");
        Apply(runtime, "death", R.ActionKind.SetHealth, 0);
        Apply(runtime, "dead-cast", R.ActionKind.Spell); Apply(runtime, "dead-round", R.ActionKind.NextRound);
        Check(!S(runtime).GuardRunning && S(runtime).GuardPayments == 0 && actor.Calls == 2, "dead actors cannot attack or begin a new guard");
        var low = new Actor { Health = 20 }; var guarded = new R(Settings(), low);
        low.Health = 0; Apply(guarded, "dead-refresh", R.ActionKind.Refresh);
        Check(S(guarded).Shield == 0 && !S(guarded).GuardRunning, "death withdraws its remaining guard source");
    }

    private static void Reserve()
    {
        var actor = new Actor(); var runtime = new R(Settings() with { InitialEnergy = 7, AttackReserve = 4 }, actor);
        Apply(runtime, "blocked", R.ActionKind.Spell);
        Check(S(runtime).Attacks == 0 && S(runtime).SourceEnergy == 7 && S(runtime).AttackAllowed == T.False, "reserve compares fee plus required remainder");
        Apply(runtime, "fund", R.ActionKind.Energy, 1); Apply(runtime, "new", R.ActionKind.Spell);
        Check(S(runtime).Attacks == 1 && S(runtime).SourceEnergy == 4, "exactly sufficient budget pays once and preserves required energy");
        Apply(runtime, "again", R.ActionKind.Spell);
        Check(S(runtime).Attacks == 1 && S(runtime).SourceEnergy == 4, "a true previous comparison cannot bypass the atomic current balance check");
    }

    private static C BasicSwitchCircuit() => new(new C.Node[]
    {
        new("state", C.NodeKind.StateInput, "state") { Key = "state", SignalType = FormationSignalType.Switch },
        new("event", C.NodeKind.EventInput, "event") { Key = "cast" },
        new("switch", C.NodeKind.Switch, "switch"), new("attack", C.NodeKind.AttackOutput, "attack")
    }, new C.Link[] { new("state", "value", "switch", "permit"), new("event", "event", "switch", "event"), new("switch", "event", "attack", "event") });

    private static void TypedGraph()
    {
        Check(C.And(T.False, T.Unknown) == T.False && C.And(T.True, T.Unknown) == T.Unknown, "three-valued AND short-circuits only definite false");
        Check(C.Or(T.True, T.Unknown) == T.True && C.Or(T.False, T.Unknown) == T.Unknown && C.Not(T.Unknown) == T.Unknown, "unknown never becomes safe by inversion or OR");
        var graph = BasicSwitchCircuit();
        var off = new Dictionary<string, FormationControlValue> { ["state"] = FormationControlValue.Boolean(T.False) };
        var on = new Dictionary<string, FormationControlValue> { ["state"] = FormationControlValue.Boolean(T.True) };
        Check(graph.Evaluate(off, new("old", "cast")).AttackRequests.Count == 0, "closed switch consumes no new output request");
        Check(graph.Evaluate(on).AttackRequests.Count == 0, "switch change without event never manufactures a pulse");
        Check(graph.Evaluate(on, new("old", "cast")).Duplicate, "opening a switch cannot replay a previously observed event");
        Check(graph.Evaluate(on, new("fresh", "cast")).AttackRequests.Count == 1, "new event passes an explicitly true switch");
        Reject(() => graph.Evaluate(on, new("fresh", "other")), "event name cannot change under a reused occurrence identity");
        Check(graph.Evaluate(new Dictionary<string, FormationControlValue>(), new("missing", "cast")).AttackRequests.Count == 0, "missing switch observation remains unknown");
        var split = new C(new C.Node[]
        {
            new("event", C.NodeKind.EventInput, "event") { Key = "cast" },
            new("split", C.NodeKind.Split, "split") { SignalType = FormationSignalType.Event },
            new("a", C.NodeKind.AttackOutput, "a"), new("b", C.NodeKind.AttackOutput, "b")
        }, new C.Link[] { new("event", "event", "split", "value"), new("split", "a", "a", "event"), new("split", "b", "b", "event") });
        var facts = new Dictionary<string, FormationControlValue>(); var evaluation = split.Evaluate(facts, new("split-event", "cast"));
        Check(evaluation.AttackRequests.Count == 2 && evaluation.Values["a"].EventId == evaluation.Values["b"].EventId, "explicit splitter retains one event identity in two destinations");
        Check(split.Evaluate(facts, new("split-event", "cast")).AttackRequests.Count == 0, "duplicate event cannot repeat either split output");
    }

    private static void InvalidGraph()
    {
        var graph = BasicSwitchCircuit();
        Reject(() => graph.Evaluate(new Dictionary<string, FormationControlValue> { ["state"] = FormationControlValue.Numeric(1, FormationValueUnit.Count) }, new("invalid", "cast")), "numeric values cannot masquerade as switch state");
        Check(graph.Evaluate(new Dictionary<string, FormationControlValue> { ["state"] = FormationControlValue.Boolean(T.True) }, new("invalid", "cast")).AttackRequests.Count == 1, "rejected invalid observation does not claim event identity");
        var nodes = new C.Node[]
        {
            new("n", C.NodeKind.StateInput, "n") { Key = "energy", Unit = FormationValueUnit.Energy },
            new("c", C.NodeKind.Compare, "c") { Unit = FormationValueUnit.Mana },
            new("g", C.NodeKind.GuardOutput, "g")
        };
        Reject(() => new C(nodes, new C.Link[] { new("n", "value", "c", "value"), new("c", "state", "g", "permit") }), "energy and mana numeric units cannot be connected");
        Reject(() => new C(new C.Node[] { new("a", C.NodeKind.Not, "a"), new("b", C.NodeKind.Not, "b") }, new C.Link[] { new("a", "state", "b", "state"), new("b", "state", "a", "state") }), "combination cycle rejected");
        Reject(() => new C(new C.Node[]
        {
            new("a", C.NodeKind.StateInput, "a") { Key = "a", SignalType = FormationSignalType.Switch },
            new("b", C.NodeKind.StateInput, "b") { Key = "b", SignalType = FormationSignalType.Switch },
            new("g", C.NodeKind.GuardOutput, "g")
        }, new C.Link[] { new("a", "value", "g", "permit"), new("b", "value", "g", "permit") }), "direct multiple drivers rejected");
        Reject(() => new C(new C.Node[]
        {
            new("a", C.NodeKind.StateInput, "a") { Key = "a", SignalType = FormationSignalType.Switch },
            new("g1", C.NodeKind.GuardOutput, "g1"), new("g2", C.NodeKind.GuardOutput, "g2")
        }, new C.Link[] { new("a", "value", "g1", "permit"), new("a", "value", "g2", "permit") }), "hidden output branching requires a physical splitter");
        var numeric = R.CreateCircuit(Settings());
        Reject(() => numeric.Evaluate(new Dictionary<string, FormationControlValue> { ["health"] = FormationControlValue.Numeric(double.NaN, FormationValueUnit.Percent) }), "non-finite observation rejected before interval memory changes");
    }

    private static void ZeroAmplitude()
    {
        Check(!FormationControlValue.Event("zero-value", "spell", 0).HasEvent, "event identity with zero amplitude is not an executable pulse");
        var circuit = new C(new C.Node[]
        {
            new("sense", C.NodeKind.EventInput, "sense") { Key = "spell_played" },
            new("tally", C.NodeKind.EventCounter, "tally") { Interval = 2 },
            new("ward", C.NodeKind.ShieldOutput, "ward")
        }, new C.Link[] { new("sense", "event", "tally", "event"), new("tally", "event", "ward", "event") });
        var actor = new Actor(); using var runtime = new R(Settings(), actor, circuit, new[] { "ward" });
        R.Input Signal(string id, int amplitude, string source = "spell") => new(id, R.ActionKind.Signal, EventName: "spell_played", SourceTag: source, Amplitude: amplitude);
        runtime.Apply(Signal("zero-before", 0));
        Check(runtime.Circuit.CounterPhases.GetValueOrDefault("tally") == 0 && actor.Shields.Total == 0 && S(runtime).SourceEnergy == 30, "zero before first pulse neither counts outputs nor pays");
        runtime.Apply(Signal("positive-one", 1)); runtime.Apply(Signal("zero-between", 0));
        Check(runtime.Circuit.CounterPhases["tally"] == 1 && actor.Shields.Total == 0 && S(runtime).SourceEnergy == 30, "zero between two real events cannot complete the counter");
        runtime.Apply(Signal("positive-two", 1));
        Check(runtime.Circuit.CounterPhases["tally"] == 0 && actor.Shields.Total == 2 && S(runtime).SourceEnergy == 28, "second positive event alone commits the explicit 2-energy 2-shield profile");

        var filtered = new C(new C.Node[]
        {
            new("sense", C.NodeKind.EventInput, "sense") { Key = "spell_played" },
            new("filter", C.NodeKind.EventFilter, "filter") { SourceTag = "spell", MinimumAmplitude = 2 },
            new("ward", C.NodeKind.ShieldOutput, "ward")
        }, new C.Link[] { new("sense", "event", "filter", "event"), new("filter", "event", "ward", "event") });
        var filteredActor = new Actor(); using var filteredRuntime = new R(Settings(), filteredActor, filtered, new[] { "ward" });
        filteredRuntime.Apply(Signal("zero", 0)); filteredRuntime.Apply(Signal("small", 1)); filteredRuntime.Apply(Signal("wrong-source", 2, "card"));
        Check(filteredActor.Shields.Total == 0 && S(filteredRuntime).SourceEnergy == 30, "zero low amplitude and wrong source cannot bypass the event filter");
        filteredRuntime.Apply(Signal("eligible", 2));
        Check(filteredActor.Shields.Total == 2 && filteredRuntime.OutputStates["ward"].Executions == 1 && S(filteredRuntime).SourceEnergy == 28, "eligible new filtered pulse submits once with its real source and amplitude");
    }
}
