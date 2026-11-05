using System.Reflection;

internal static class RuleChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        var deck = CondensationCardGame.Pool.ToDictionary(card => card.Name, card => card.Count);
        CondensationCardGame Game(int seed = 0) => new(60, 15, 34, deck, [], seed);
        void Set(CondensationCardGame game, string field, int value) => typeof(CondensationCardGame).GetProperty(field)!.SetValue(game, value);
        CondensationCardGame WithHand(params string[] hand) { var game = Game(); game.Hand.Clear(); game.Hand.AddRange(hand); game.StartActions(); return game; }
        Check(deck.Values.Sum() == 36 && deck.Values.All(count => count is >= 0 and <= 4), "Default deck size/copies.");
        foreach (var score in new[] { int.MinValue, -1, 0, 99, 100, 104, 105, 109, 110, 124, 125, 150, 200, 225, 400 })
        {
            var expected = score <= 100 ? 0 : (score - 100) / 5;
            Check(CondensationOutcomeRules.DifficultySteps(score) == expected, $"Score step {score}.");
            foreach (var key in new[] { "physique", "intelligence", "soul" })
                Check(CondensationOutcomeRules.AttributeFlatBonus(key, score) == expected * .25f, $"Equal reward {score}/{key}.");
            Check(CondensationOutcomeRules.AttributeFlatBonus("comprehension", score) == 0 && CondensationOutcomeRules.AttributeFlatBonus("fortune", score) == 0, "No unrelated attribute reward.");
        }
        Check(CondensationOutcomeRules.SpiritRequirementMultiplier(200) == 1.25 && CondensationOutcomeRules.FoundationCloudBonus(200) == .5, "200-point cost and deferred benefit.");
        Check(CondensationOutcomeRules.FlatAttributeBonus(225) == 6.25f && CondensationOutcomeRules.Describe(125).Contains("1.25"), "No reward truncation or 200-point cap.");
        foreach (var (weight, bonus) in new[] { (20, 0), (33, 0), (34, 1), (66, 1), (67, 2), (99, 2), (100, 3) })
            Check(new CondensationCardGame(60, 15, weight, deck, [], 0).RootBonus == bonus, "Root threshold.");
        foreach (var (value, bonus) in new[] { (9, 0), (10, 1), (14, 1), (15, 2), (19, 2), (20, 3), (24, 3), (25, 4) })
            Check(new CondensationCardGame(60, value, 34, deck, [], 0).InsightBonus == bonus, "Intelligence threshold.");
        foreach (var (value, swaps) in new[] { (39, 0), (40, 1), (69, 1), (70, 2), (89, 2), (90, 3) })
            Check(new CondensationCardGame(value, 15, 34, deck, [], 0).ExtraSwaps == swaps, "Comprehension threshold.");
        var first = Game(42); var repeat = Game(42);
        Check(first.Hand.SequenceEqual(repeat.Hand) && first.Deck.SequenceEqual(repeat.Deck), "Seeded opening reproducible.");
        Check(first.Swap([0, 1]) == "" && repeat.Swap([0, 1]) == "" && first.Deck.SequenceEqual(repeat.Deck), "Opening swap reproducible.");
        for (var seed = 0; seed < 20; seed++)
        {
            var visible = Game(seed); visible.StartActions(); var hidden = visible.Copy(); hidden.Deck.Reverse();
            Check(CondensationBalanceAuditPolicy.NextActionFingerprint(visible, 3) == CondensationBalanceAuditPolicy.NextActionFingerprint(hidden, 3), "Policy must not inspect hidden deck order.");
        }
        var pressure = WithHand("轻压");
        Check(pressure.Play("轻压", 0, []) == "" && pressure.Score == 11 && pressure.Disorder == 1 && pressure.Actions == 2 && pressure.Recoils.Single().Round == 3, "Pressure cost, root gain, delayed recoil.");
        var before = (pressure.Score, pressure.Disorder, pressure.Round, pressure.Hand.Count, pressure.Recoils.Count, pressure.Log.Count);
        pressure.SettlementPreview();
        Check(before == (pressure.Score, pressure.Disorder, pressure.Round, pressure.Hand.Count, pressure.Recoils.Count, pressure.Log.Count), "Preview has no side effects.");
        var targets = WithHand("回旋");
        Check(targets.Play("回旋", 0, [999]) != "" && targets.Actions == 3 && targets.Hand.Count == 1, "Invalid target pays nothing.");
        targets.Recoils.Add(new(1, 1, 4)); Set(targets, "Disorder", 5);
        Check(targets.Play("回旋", 0, [1]) == "" && targets.Score == 4 && targets.Disorder == 1 && targets.Recoils.Count == 0, "Immediate return and insight reduction.");
        var busy = WithHand("匀气", "轻压", "重压", "护气", "归拢", "理气");
        Check(busy.Play("匀气", 0, []) == "" && busy.ForcedDiscards == 1 && busy.EndRound() != "", "Must discard before settlement.");
        busy.Drop(0); Check(!busy.Busy && busy.Hand.Count == 6, "Hand cap after draw.");
        var rest = WithHand();
        Check(rest.Play("调息", -1, []) == "" && rest.Play("调息", -1, []) == "" && rest.Play("调息", -1, []) == "" && rest.Play("调息", -1, []) != "", "Rest consumes one of three actions.");
        var reverse = WithHand("轻压", "理气", "轻压");
        reverse.Play("轻压", 0, []); reverse.Play("理气", 0, []); reverse.Play("轻压", 0, []);
        Check(reverse.Disorder == 2, "Up-down-up adds one disorder.");
        var lost = WithHand("轻压"); Set(lost, "Score", 150); Set(lost, "Disorder", 10);
        lost.Play("轻压", 0, []);
        Check(lost.Failed && lost.Finished && lost.Score > 100 && lost.Play("调息", -1, []) != "", "Disorder overflow overrides score and prevents recovery.");
        var exact = Game(); for (var round = 1; round < 8; round++) exact.EndRound();
        Set(exact, "Score", 100); exact.EndRound();
        Check(exact.Finished && !exact.Failed && exact.Score == 100, "Exactly 100 succeeds only after round eight.");
        var future = Game(); for (var round = 1; round < 8; round++) future.EndRound();
        Set(future, "Score", 105); future.Recoils.Add(new(1, 9, 10)); future.EndRound();
        Check(future.Failed && future.Score == 95 && future.Recoils.Count == 0, "Future recoil can overturn apparent success.");
        var loss = Game(); for (var round = 1; round < 8; round++) loss.EndRound();
        Set(loss, "Score", 125); Set(loss, "Disorder", 10); loss.EndRound();
        Check(!loss.Failed && loss.Score == 100, "Disorder 10 is legal and loses 25.");
        return checks;
    }
}
