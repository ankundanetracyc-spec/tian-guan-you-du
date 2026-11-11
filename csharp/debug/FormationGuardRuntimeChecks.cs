using System;
using System.Collections.Generic;
using Godot;
using R = FormationGuardAssaultRuntime;

/// <summary>真实Creature公共护盾与守攻宿主的隔离回归。所有数字仅为fixture，无人物入树或存档读写。</summary>
public partial class FormationGuardRuntimeChecks : Node
{
    private int _checks, _groups;

    public override void _Ready() => CallDeferred(nameof(RunChecks));

    private void RunChecks()
    {
        var exitCode = 0;
        try
        {
            Run("Creature legacy and named source share one FIFO", CreatureLedger);
            Run("Creature equal source renewal publishes final total", FinalTotal);
            Run("real host enemy shield and health damage", RealAttack);
            Run("runtime real Creature shield stop and attack transaction", RealRuntime);
            Run("host invalid and freed actors remain silent", InvalidActors);
            Run("observer failure never refunds committed real shield or attack", ObserverFailures);
            GD.Print($"FORMATION_GUARD_RUNTIME_CHECKS_PASS groups={_groups} checks={_checks}");
        }
        catch (Exception error)
        {
            exitCode = 1;
            GD.PushError("FORMATION_GUARD_RUNTIME_CHECKS_FAIL " + error);
        }
        finally
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            GetTree().Quit(exitCode);
        }
    }

    private void Run(string label, Action test)
    {
        test(); _groups++;
        GD.Print("FORMATION_GUARD_RUNTIME_GROUP_PASS " + label);
    }

    private void Check(bool condition, string label)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(label);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HumanData _ownerData, _enemyData;
        public Character Owner { get; }
        public Character Enemy { get; }
        public FormationGuardActor Host { get; }

        public Fixture(int health = 80, int enemyHealth = 100)
        {
            _ownerData = new HumanData { entity_name = "守攻隔离本人", max_health = 100, current_health = health, max_mana = 9, current_mana = 9 };
            _enemyData = new HumanData { entity_name = "守攻隔离目标", max_health = 100, current_health = enemyHealth, max_mana = 0, current_mana = 0 };
            Owner = new Character { entity_data = _ownerData };
            Enemy = new Character { entity_data = _enemyData };
            // 不调用_Ready、AddChild、load_from_data或任何正式战斗入口。
            Host = new FormationGuardActor(Owner, Enemy);
        }

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(Owner)) Owner.Free();
            if (GodotObject.IsInstanceValid(Enemy)) Enemy.Free();
            _ownerData.Dispose(); _enemyData.Dispose();
        }
    }

    private static R.Settings Settings() => new(50, 30, 0, 20, 20, 20, 3, 10, 2, 4, 7, 3, 2, 30, 60);
    private static R.Snapshot Apply(R runtime, string id, R.ActionKind kind, int value = 0, bool flag = true) => runtime.Apply(new(id, kind, value, flag)).Snapshot;

    private void CreatureLedger()
    {
        using var fixture = new Fixture(); var owner = fixture.Owner;
        owner.shield = 5; owner.ShieldSources.ReplaceSource("test:guard", 7); owner.shield += 3;
        Check(owner.shield == 15 && owner.Get("shield").AsInt32() == 15, "legacy C# and Godot shield getters expose one real total");
        owner.shield -= 8;
        Check(owner.shield == 7 && owner.ShieldSources.GetRemaining("test:guard") == 4, "legacy damage follows oldest ordinary then named source");
        Check(owner.ShieldSources.RemoveSource("test:guard") == 4 && owner.shield == 3, "source stop preserves later ordinary shield");
        owner.Set("shield", 9);
        Check(owner.shield == 9 && owner.ShieldSources.Snapshot.Count == 2, "Godot setter increase becomes another ordinary slice");
        owner.qi_blood = 4; owner.blood_furnace_value = 6;
        owner.ShieldSources.ReplaceSource("test:other", 2); owner.shield = 0;
        Check(owner.ShieldSources.Total == 0 && owner.ShieldSources.GetRemaining("test:other") == 0, "legacy zero clears all shield sources");
        Check(owner.qi_blood == 4 && owner.blood_furnace_value == 6, "shield bridge preserves unrelated feedback fields");
        owner.shield = -99;
        Check(owner.shield == 0, "legacy negative assignment clamps to zero");
    }

    private void FinalTotal()
    {
        using var fixture = new Fixture(); var owner = fixture.Owner; var changes = new List<(int Old, int New)>();
        owner.ShieldSources.Changed += (oldTotal, newTotal) => changes.Add((oldTotal, newTotal));
        owner.ShieldSources.ReplaceSource("test:guard", 10); changes.Clear();
        owner.ShieldSources.ReplaceSource("test:guard", 10);
        Check(changes.Count == 1 && changes[0] == (10, 10) && owner.shield == 10, "equal renewal emits no fake remove/add cycle");
        changes.Clear(); owner.ShieldSources.AddOrdinary(5);
        Check(changes.Count == 1 && changes[0] == (10, 15), "direct source ledger uses final net gain feedback notification");
    }

    private void RealAttack()
    {
        using var fixture = new Fixture(enemyHealth: 30); var target = fixture.Enemy;
        target.shield = 3; target.ShieldSources.ReplaceSource("test:enemy-guard", 2);
        Check(fixture.Host.TryAttack(4), "a real shield absorption counts as a committed attack");
        Check(target.current_health == 30 && target.shield == 1 && target.ShieldSources.GetRemaining("test:enemy-guard") == 1, "real host uses enemy mixed shield FIFO before life");
        Check(fixture.Host.TryAttack(7) && target.current_health == 24 && target.shield == 0, "remaining damage uses existing magic defense and actual enemy life");
        Check(fixture.Owner.current_mana == 9 && fixture.Owner.shield == 0, "host effect never charges mana or changes owner's shield");
        Check(!fixture.Host.TryAttack(0) && !fixture.Host.TryAttack(-4) && target.current_health == 24, "nonpositive attacks never cause minimum magic damage");
        Check(fixture.Host.TryAttack(1000) && target.current_health == 0 && !fixture.Host.TryAttack(7), "overkill clamps life and dead enemy cannot be hit again");
    }

    private void RealRuntime()
    {
        using var fixture = new Fixture(health: 20); var runtime = new R(Settings(), fixture.Host);
        Check(runtime.GetSnapshot().GuardRunning && fixture.Owner.shield == 10 && runtime.GetSnapshot().EnergySpent == 3, "startup creates the actual Creature guard source and pays fixture energy");
        Apply(runtime, "ordinary", R.ActionKind.OrdinaryShield, 4);
        var exhausted = Apply(runtime, "damage", R.ActionKind.Damage, 12);
        Check(fixture.Owner.current_health == 20 && fixture.Owner.shield == 2 && !exhausted.GuardRunning && exhausted.GuardPaidThisRound, "real damage exhausts guard before newer ordinary without refreshing its quota");
        Apply(runtime, "next", R.ActionKind.NextRound);
        Check(fixture.Owner.shield == 12 && runtime.GetSnapshot().GuardPayments == 2, "new segment replaces only own source while ordinary survives");
        Apply(runtime, "healthy", R.ActionKind.SetHealth, 80);
        Check(fixture.Owner.shield == 2 && fixture.Owner.current_health == 80 && !runtime.GetSnapshot().GuardRunning, "real healthy-state stop withdraws only this runtime source");
        fixture.Enemy.shield = 5;
        var cast = Apply(runtime, "cast", R.ActionKind.Spell);
        Check(cast.Attacks == 1 && fixture.Enemy.shield == 0 && fixture.Enemy.current_health == 98 && cast.SourceEnergy == 20, "actual spell transaction pays once and submits enemy shield plus life damage");
        var duplicate = runtime.Apply(new("cast", R.ActionKind.Spell));
        Check(duplicate.Duplicate && fixture.Enemy.current_health == 98 && duplicate.Snapshot.SourceEnergy == 20, "same actual occurrence cannot damage or charge twice");
        Apply(runtime, "low", R.ActionKind.SetHealth, 20); Apply(runtime, "death", R.ActionKind.SetHealth, 0);
        Apply(runtime, "dead-cast", R.ActionKind.Spell); Apply(runtime, "dead-round", R.ActionKind.NextRound);
        Check(fixture.Owner.shield == 2 && fixture.Enemy.current_health == 98 && !runtime.GetSnapshot().GuardRunning, "dead owner emits no outputs and only ordinary shield remains");
    }

    private void InvalidActors()
    {
        using var fixture = new Fixture(); var rejected = false;
        try { _ = new FormationGuardActor(fixture.Owner, fixture.Owner); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "same actor cannot become its own enemy");
        fixture.Host.Health = int.MaxValue;
        Check(fixture.Owner.current_health == 100, "host health setter obeys actual maximum");
        fixture.Host.Health = -1;
        Check(!fixture.Host.IsAlive && !fixture.Host.TryAttack(7), "host dead owner cannot attack");
        fixture.Owner.Free();
        Check(!fixture.Host.IsAlive && fixture.Host.Health == 0 && !fixture.Host.TryAttack(7), "freed owner never invokes Godot combat effects");
        fixture.Host.Shields.ReplaceSource("test:cleanup", 3); fixture.Host.Shields.RemoveSource("test:cleanup");
        Check(fixture.Host.Shields.Total == 0, "cached source cleanup remains safe after owner Free");
    }

    private void ObserverFailures()
    {
        using var fixture = new Fixture(); var runtime = new R(Settings(), fixture.Host);
        fixture.Owner.ShieldSources.Changed += (_, _) => throw new InvalidOperationException("fixture own shield observer");
        var guarded = Apply(runtime, "guard", R.ActionKind.SetHealth, 20);
        Check(guarded.GuardPayments == 1 && fixture.Owner.shield == 10 && guarded.SourceEnergy == 27 && fixture.Owner.ShieldSources.NotificationErrorCount == 1,
            "real own source observer failure cannot leave free paid-capacity rollback");
        Apply(runtime, "attack-mode", R.ActionKind.SetHealth, 80);
        fixture.Enemy.shield = 5;
        fixture.Enemy.ShieldSources.Changed += (_, _) => throw new InvalidOperationException("fixture enemy shield observer");
        var cast = Apply(runtime, "cast", R.ActionKind.Spell);
        Check(cast.Attacks == 1 && cast.SourceEnergy == 23 && fixture.Enemy.shield == 0 && fixture.Enemy.current_health == 98,
            "enemy shield observer failure cannot refund attack after actual shield and HP damage");
        Check(fixture.Enemy.ShieldSources.NotificationErrorCount == 1, "real enemy observer failure is retained for diagnostics");
    }
}
