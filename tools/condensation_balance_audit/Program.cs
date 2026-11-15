using System.Text.Json;

// Links the production rules. Policies use visible hand/recoils only, with no items or deck-order inspection.
Console.WriteLine($"CONDENSATION_RULE_CHECKS_PASS: {RuleChecks.Run()}");
if (args.Contains("--checks")) return;
var samples = args.Length > 0 ? int.Parse(args[0]) : 200;
var baseline = new[] { 4, 3, 3, 2, 1, 4, 2, 2, 1, 2, 2, 2, 2, 2, 2, 1, 0, 1 };
var steady = new[] { 4, 2, 2, 4, 3, 4, 1, 1, 0, 2, 1, 2, 2, 2, 3, 2, 0, 1 };
var outputs = new List<object>();
foreach (var (deckName, counts) in new[] { ("previous_default", baseline), ("steady_candidate", steady) })
foreach (var (profile, comprehension, intelligence, root) in new[]
{
    ("low", 30, 8, 20), ("ordinary", 60, 15, 34), ("high", 95, 25, 100)
})
foreach (var depth in new[] { 1, 3 })
{
    var scores = new List<int>(); var failures = 0; var instability = 0;
    var deck = CondensationCardGame.Pool.Select((card, i) => (card.Name, counts[i])).ToDictionary(pair => pair.Name, pair => pair.Item2);
    for (var seed = 0; seed < samples; seed++)
    {
        var game = new CondensationCardGame(comprehension, intelligence, root, deck, [], seed);
        CondensationBalanceAuditPolicy.Run(game, depth);
        if (game.Failed) failures++;
        if (game.Disorder > 10) instability++;
        scores.Add(game.Score);
    }
    scores.Sort();
    var output = new
    {
        deck = deckName, profile, depth, samples, successPercent = 100d * (samples - failures) / samples,
        instability, minimum = scores[0], p10 = scores[(samples - 1) / 10], median = scores[(samples - 1) / 2],
        p90 = scores[(samples - 1) * 9 / 10], maximum = scores[^1], mean = Math.Round(scores.Average(), 2)
    };
    outputs.Add(output); Console.WriteLine(JsonSerializer.Serialize(output));
}
if (args.Length > 1) File.WriteAllText(args[1], JsonSerializer.Serialize(outputs, new JsonSerializerOptions { WriteIndented = true }));

