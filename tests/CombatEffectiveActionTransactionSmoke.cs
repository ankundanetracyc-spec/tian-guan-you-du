using System;
using System.Threading.Tasks;
using Tx = CombatEffectiveActionTransaction;

internal static class CombatEffectiveActionTransactionSmoke
{
    private static int _checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        _checks++;
    }
    private static Tx.ActionRequest Action(string owner, string id, int stamina, int mana = 0) => new(owner, id, new(stamina, mana));
    private static Tx.Preview Ready(Tx tx, string id, Tx.ActionRequest first, Tx.ActionRequest? second = null)
    {
        Check(tx.Prepare(id, first, second, out var preview) == Tx.Status.Ready && preview is not null, id + " prepare");
        return preview!;
    }
    private static Tx.Receipt Commit(Tx tx, Tx.Preview preview, Tx.Participation first, Tx.Participation? second = null)
    {
        Check(tx.Commit(preview, first, second, out var receipt) == Tx.Status.Committed && receipt is not null, preview.EventId + " commit");
        return receipt!;
    }
    private static Tx.Balance Balance(Tx tx, string owner)
    {
        Check(tx.TryGetBalance(owner, out var balance), owner + " exists");
        return balance;
    }
    private static void Main()
    {
        var tx = new Tx();
        Check(tx.Register("sword", new(5, 2)) && tx.Register("mage", new(3, 3)), "register");
        var sword = Action("sword", "sword-1", 2);
        var spell = Action("mage", "spell-1", 0, 2);
        var clashPreview = Ready(tx, "break", sword, spell);
        Check(clashPreview.FirstEligibility == Tx.Eligibility.Ready && clashPreview.SecondEligibility == Tx.Eligibility.Ready, "both prechecked");
        Check(Balance(tx, "sword") == new Tx.Balance(5, 2) && Balance(tx, "mage") == new Tx.Balance(3, 3), "prepare never charges");
        Check(tx.Commit(clashPreview, Tx.Participation.Clash, Tx.Participation.None, out _) == Tx.Status.InvalidRequest,
            "clash cannot charge only one side");
        Check(tx.Commit(clashPreview, Tx.Participation.Clash, Tx.Participation.SingleAction, out _) == Tx.Status.InvalidRequest,
            "clash cannot be combined with a single action");
        var clash = Commit(tx, clashPreview, Tx.Participation.Clash, Tx.Participation.Clash);
        Check(clash.First.Charged && clash.Second!.Value.Charged && Balance(tx, "sword").Stamina == 3 && Balance(tx, "mage").Mana == 1,
            "first clash pays both sides regardless of outcome, including melee breaking ranged");
        Check(tx.Commit(clashPreview, Tx.Participation.Clash, Tx.Participation.Clash, out var replay) == Tx.Status.AlreadyCommitted
            && replay == clash, "same event replay is idempotent");
        Check(tx.Commit(clashPreview, Tx.Participation.None, Tx.Participation.None, out _) == Tx.Status.Conflict, "cannot rewrite event result");
        Check(tx.Prepare("break", sword, spell, out _) == Tx.Status.AlreadyCommitted, "prepare completed event is idempotent");
        Check(tx.Prepare("break", sword, Action("mage", "different", 0, 2), out _) == Tx.Status.Conflict, "event identity immutable");

        var hit = Commit(tx, Ready(tx, "later-hit", sword), Tx.Participation.SingleAction);
        Check(!hit.First.Charged && hit.First.PreviouslyPaid && Balance(tx, "sword").Stamina == 3,
            "melee post-break later hit does not charge again");
        Check(tx.Prepare("bad-cost", sword with { Cost = new(1, 0) }, null, out _) == Tx.Status.Conflict, "same action cost immutable");
        Check(tx.Prepare("bad-owner", Action("mage", "sword-1", 2), null, out _) == Tx.Status.Conflict, "action owner immutable");
        Commit(tx, Ready(tx, "new-action", Action("sword", "sword-2", 1)), Tx.Participation.SingleAction);
        Check(Balance(tx, "sword").Stamina == 2, "new play pays independently");

        var notTriggered = Commit(tx, Ready(tx, "untriggered-guard", Action("mage", "guard", 0, 1)), Tx.Participation.None);
        Check(!notTriggered.First.Charged && Balance(tx, "mage").Mana == 1, "untriggered guard is free");
        var firstDodge = Commit(tx, Ready(tx, "dodge-1", Action("mage", "dodge", 0, 1), Action("sword", "attack-1", 1)),
            Tx.Participation.Clash, Tx.Participation.Clash);
        Check(firstDodge.First.Charged && firstDodge.Second!.Value.Charged
            && Balance(tx, "mage").Mana == 0 && Balance(tx, "sword").Stamina == 1, "dodge and attack pay at first clash");
        var secondDodge = Commit(tx, Ready(tx, "dodge-2", Action("mage", "dodge", 0, 1), Action("sword", "attack-2", 1)),
            Tx.Participation.Clash, Tx.Participation.Clash);
        Check(!secondDodge.First.Charged && secondDodge.First.PreviouslyPaid && secondDodge.Second!.Value.Charged
            && Balance(tx, "mage").Mana == 0 && Balance(tx, "sword").Stamina == 0,
            "same dodge can clash again free even at zero remaining resource, new attack pays");

        var lack = Ready(tx, "lack", Action("sword", "expensive", 9), Action("mage", "free-action", 0));
        Check(lack.FirstEligibility == Tx.Eligibility.InsufficientResources && lack.SecondEligibility == Tx.Eligibility.Ready,
            "independent eligibility");
        Check(tx.Commit(lack, Tx.Participation.Clash, Tx.Participation.Clash, out _) == Tx.Status.IneligibleParticipation
            && Balance(tx, "sword").Stamina == 0 && Balance(tx, "mage").Mana == 0,
            "unaffordable side cannot clash and neither side is partially charged");
        Check(tx.Commit(lack, Tx.Participation.SingleAction, Tx.Participation.None, out _) == Tx.Status.IneligibleParticipation,
            "unaffordable side cannot act alone");
        Commit(tx, lack, Tx.Participation.None, Tx.Participation.SingleAction);
        Check(Balance(tx, "sword").Stamina == 0 && Balance(tx, "mage").Mana == 0, "eligible opponent continues alone");

        Check(tx.SetBalance("sword", new(1, 0)) && tx.SetBalance("mage", new(0, 1)), "resources restored");
        var stale = Ready(tx, "stale", Action("sword", "stale-a", 1), Action("mage", "stale-b", 0, 1));
        Check(tx.SetBalance("mage", new(0, 0)), "external balance sync");
        Check(tx.Commit(stale, Tx.Participation.Clash, Tx.Participation.Clash, out _) == Tx.Status.Stale
            && Balance(tx, "sword").Stamina == 1 && Balance(tx, "mage").Mana == 0, "changed snapshot prevents partial commit");
        var refreshed = Ready(tx, "stale", Action("sword", "stale-a", 1), Action("mage", "stale-b", 0, 1));
        Check(refreshed.SecondEligibility == Tx.Eligibility.InsufficientResources, "stale event must be prepared again");
        Commit(tx, refreshed, Tx.Participation.SingleAction, Tx.Participation.None);
        Check(Balance(tx, "sword").Stamina == 0, "eligible side can act after recheck");
        Commit(tx, Ready(tx, "already-paid-empty", sword), Tx.Participation.SingleAction);
        Check(Balance(tx, "sword").Stamina == 0, "paid action remains usable with zero remaining resource");

        Check(!tx.Register("negative", new(-1, 0)) && !tx.SetBalance("mage", new(0, -1)), "negative balances rejected");
        Check(tx.Prepare("negative-cost", Action("mage", "negative-cost", -1), null, out _) == Tx.Status.InvalidRequest,
            "negative cost rejected");
        var max = new Tx(); max.Register("max", new(int.MaxValue, int.MaxValue));
        var maxAction = Action("max", "max-action", int.MaxValue, int.MaxValue);
        Commit(max, Ready(max, "max-payment", maxAction), Tx.Participation.SingleAction);
        Check(Balance(max, "max") == new Tx.Balance(0, 0), "maximum integer cost is checked without overflow");
        var over = Ready(max, "over-balance", Action("max", "over-action", 1));
        Check(over.FirstEligibility == Tx.Eligibility.InsufficientResources, "cost exceeding balance rejected");
        Check(max.Commit(over, Tx.Participation.SingleAction, null, out _) == Tx.Status.IneligibleParticipation,
            "insufficient funds cannot produce an action");

        var segments = new Tx(); segments.Register("cloud", new(6, 0));
        var opening = new Tx.ActionRequest("cloud", "cloud-opening", new(2, 0), "shared-payment");
        var followup = new Tx.ActionRequest("cloud", "cloud-followup", new(2, 0), "shared-payment");
        Commit(segments, Ready(segments, "cloud-first", opening), Tx.Participation.SingleAction);
        var shared = Commit(segments, Ready(segments, "cloud-followup", followup), Tx.Participation.SingleAction);
        Check(!shared.First.Charged && shared.First.PreviouslyPaid && Balance(segments, "cloud").Stamina == 4,
            "different segment action ids may explicitly share a payment identity");
        var independent = new Tx.ActionRequest("cloud", "cloud-other-segment", new(1, 0), "other-payment");
        Commit(segments, Ready(segments, "independent-segment", independent), Tx.Participation.SingleAction);
        Check(Balance(segments, "cloud").Stamina == 3, "caller may instead give segment an independent payment identity");
        Check(segments.Prepare("different-fee", followup with { Cost = new(3, 0) }, null, out _) == Tx.Status.Conflict,
            "shared payment identity cannot change its fee");
        Check(segments.Prepare("rebind", followup with { PaymentIdentity = "other-payment" }, null, out _) == Tx.Status.Conflict,
            "action payment identity cannot be rebound after commit");
        Check(segments.Prepare("empty-fee-identity", followup with { PaymentIdentity = " " }, null, out _) == Tx.Status.InvalidRequest,
            "empty explicit payment identity rejected");

        var cloudEmpty = new Tx(); cloudEmpty.Register("cloud", new(2, 0));
        var cloudOpening = new Tx.ActionRequest("cloud", "opening", new(2, 0), "cloud-card-play");
        var cloudFollowup = new Tx.ActionRequest("cloud", "first-followup", new(2, 0), "cloud-card-play");
        Commit(cloudEmpty, Ready(cloudEmpty, "opening-event", cloudOpening), Tx.Participation.SingleAction);
        Check(Balance(cloudEmpty, "cloud").Stamina == 0, "cloud opening paid its two stamina");
        var cloudZero = Ready(cloudEmpty, "followup-event", cloudFollowup);
        Check(cloudZero.FirstEligibility == Tx.Eligibility.Ready, "shared paid cloud followup remains eligible at zero stamina");
        Check(!Commit(cloudEmpty, cloudZero, Tx.Participation.SingleAction).First.Charged
            && Balance(cloudEmpty, "cloud").Stamina == 0, "cloud followup reuses receipt without further cost");

        var concurrentTx = new Tx(); concurrentTx.Register("a", new(2, 0));
        var concurrent = Ready(concurrentTx, "concurrent", Action("a", "same", 1));
        var first = Task.Run(() => concurrentTx.Commit(concurrent, Tx.Participation.SingleAction, null, out _));
        var second = Task.Run(() => concurrentTx.Commit(concurrent, Tx.Participation.SingleAction, null, out _));
        Task.WaitAll(first, second);
        Check((first.Result == Tx.Status.Committed && second.Result == Tx.Status.AlreadyCommitted
            || second.Result == Tx.Status.Committed && first.Result == Tx.Status.AlreadyCommitted)
            && Balance(concurrentTx, "a").Stamina == 1, "concurrent duplicate is atomic and idempotent");
        Console.WriteLine($"COMBAT_EFFECTIVE_ACTION_TRANSACTION_SMOKE_OK checks={_checks}");
    }
}
