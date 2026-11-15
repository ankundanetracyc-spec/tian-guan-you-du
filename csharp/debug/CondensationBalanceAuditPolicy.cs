internal static class CondensationBalanceAuditPolicy
{
    private sealed record Move(string Name, int Index, int[] Targets);
    private sealed record Candidate(CondensationCardGame Game, Move First);

    public static string NextActionFingerprint(CondensationCardGame game, int depth)
    {
        var move = Plan(game, depth);
        return move is null ? "end" : $"{move.Name}:{move.Index}:{string.Join(',', move.Targets)}";
    }

    public static void Run(CondensationCardGame game, int depth)
    {
        while (!game.Finished)
        {
            Swap(game);
            game.StartActions();
            while (game.Actions > 0 && !game.Finished)
            {
                var move = Plan(game, depth);
                if (move is null) break;
                var error = game.Play(move.Name, move.Index, move.Targets);
                if (error.Length > 0) throw new Exception(error);
                ResolveVisibleDraw(game);
            }
            if (!game.Finished && game.EndRound().Length > 0) throw new Exception("Unresolved hand.");
        }
    }

    private static void Swap(CondensationCardGame game)
    {
        var ranked = game.Hand.Select((name, index) => (index, value: CardValue(game, name)))
            .OrderBy(pair => pair.value).ToArray();
        if (game.Round == 1)
        {
            var indices = ranked.Where(pair => pair.value < 4).Take(2).Select(pair => pair.index).ToArray();
            if (indices.Length > 0 && game.Swap(indices).Length > 0) throw new Exception("Opening swap failed.");
        }
        else if (ranked.Length > 0 && ranked[0].value < 6)
        {
            game.Swap([ranked[0].index]);
            // Decide from the newly visible hand, not the unseen next card.
            if (game.ExtraSwaps > 0 && game.Hand.Count(name => CondensationCardGame.Definition(name).Kind == "压聚") < 2)
            {
                var worst = game.Hand.Select((name, index) => (index, value: CardValue(game, name))).MinBy(pair => pair.value);
                if (worst.value < 4) game.Swap([worst.index], true);
            }
        }
    }

    private static Move? Plan(CondensationCardGame game, int depth)
    {
        var bestValue = Evaluate(game); Move? best = null;
        var beam = new List<Candidate>();
        foreach (var move in Moves(game))
        {
            var copy = game.Copy(); copy.Play(move.Name, move.Index, move.Targets);
            if (copy.Failed) continue;
            var value = EvaluateMove(game, move, copy);
            if (value > bestValue + .001) { bestValue = value; best = move; }
            // A lookahead must stop at an information reveal, even though the cloned engine knows the deck.
            if (!copy.Busy && !RevealsCards(move.Name)) beam.Add(new(copy, move));
        }
        for (var step = 1; step < depth; step++)
        {
            var next = new List<Candidate>();
            foreach (var candidate in beam.OrderByDescending(c => Evaluate(c.Game)).Take(12))
            foreach (var move in Moves(candidate.Game))
            {
                var copy = candidate.Game.Copy(); copy.Play(move.Name, move.Index, move.Targets);
                if (copy.Failed) continue;
                var value = EvaluateMove(candidate.Game, move, copy);
                if (value > bestValue + .001) { bestValue = value; best = candidate.First; }
                if (!copy.Busy && !RevealsCards(move.Name)) next.Add(new(copy, candidate.First));
            }
            beam = next;
        }
        return best;
    }

    private static bool RevealsCards(string name) => name is "匀气" or "观气";
    private static double EvaluateMove(CondensationCardGame before, Move move, CondensationCardGame after)
    {
        if (!RevealsCards(move.Name)) return Evaluate(after);
        // Expected value of an unknown draw; never use the actual newly drawn cards in action ranking.
        var knownHandValue = before.Hand.Where((_, index) => index != move.Index).Sum(name => CardValue(before, name));
        return Evaluate(after, false) + (before.Round < 8 ? .22 * (knownHandValue + 4) : 0);
    }
    private static IEnumerable<Move> Moves(CondensationCardGame game)
    {
        if (game.Actions <= 0 || game.Finished || game.Busy) yield break;
        var names = game.Hand.Distinct().Append("调息");
        foreach (var name in names)
        {
            var index = name == "调息" ? -1 : game.Hand.IndexOf(name);
            var count = game.TargetCount(name);
            var legal = game.LegalTargets(name).Select(recoil => recoil.Id).ToArray();
            IEnumerable<int[]> targets = count == 0 ? new[] { Array.Empty<int>() } : count == 1
                ? legal.Select(id => new[] { id })
                : legal.SelectMany((id, i) => legal.Skip(i + 1).Select(other => new[] { id, other }));
            foreach (var selected in targets)
                if (game.CanPlay(name, index, selected).Length == 0) yield return new(name, index, selected);
        }
    }

    private static double Evaluate(CondensationCardGame game, bool includeHand = true)
    {
        if (game.Failed) return -10000;
        // Resolve score-changing settlement on a copy; don't examine any new hand drawn by EndRound.
        var preview = game.Copy();
        while (preview.Busy)
        {
            if (preview.Choices.Count > 0) preview.Choose(0);
            else preview.Drop(preview.Hand.Count - 1);
        }
        preview.EndRound();
        if (preview.Finished) return preview.Score;
        return preview.Score - .65 * preview.Recoils.Sum(recoil => recoil.Strength)
            - .35 * preview.Disorder * preview.Disorder
            + (game.ChargeActive ? 2.5 : 0)
            + (includeHand ? .22 * game.Hand.Sum(name => CardValue(game, name)) : 0);
    }

    private static double CardValue(CondensationCardGame game, string name) => name switch
    {
        "轻压" => 8 + game.RootBonus, "重压" => 11 + game.RootBonus, "叠压" => 9 + game.RootBonus,
        "徐压" => 8 + game.RootBonus, "息压" => 12 + game.RootBonus,
        "引回" => game.Hand.Count(card => card == "引回") > 1 ? 4 : 8,
        "理气" => game.Disorder >= 3 ? 7 : 4, "回旋" => 6, "归拢" => 4, "护气" => 3,
        "蓄劲" => 5, "匀气" => 5, "观气" => 4,
        "随势" => game.Hand.Contains("引回") ? 3 : 1, _ => 1
    };

    private static void ResolveVisibleDraw(CondensationCardGame game)
    {
        while (game.Busy && !game.Finished)
        {
            if (game.Choices.Count > 0)
                game.Choose(game.Choices.Select((name, index) => (index, value: CardValue(game, name))).MaxBy(pair => pair.value).index);
            else game.Drop(game.Hand.Select((name, index) => (index, value: CardValue(game, name))).MinBy(pair => pair.value).index);
        }
    }
}
