using Godot;
using System.Text.Json;

/// <summary>真实招式盘转牌后，验证抽牌、弃洗及两条队列的实体守恒。</summary>
public partial class BoardHandStateSmoke : Node
{
    private int _checks;

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    public override void _Ready() => CallDeferred(nameof(Run));

    private void Run()
    {
        try
        {
            var catalog = new CombatBoardCatalog();
            var layout = new CombatBoardLayout { size = new(64, 4), confirmed = true };
            using var report = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
                "res://docs/美术规范/人类招式占格审阅_20261107/几何检查结果.json"));
            var x = 0;
            foreach (var row in report.RootElement.GetProperty("skills").EnumerateArray().Take(8))
            {
                var path = row.GetProperty("resource").GetString()!;
                var skill = GD.Load<ActiveSkillData>(path);
                Check(skill is not null && skill.has_board_definition(), "真实招式存在盘面定义 " + path);
                catalog.skills.Add(skill!);
                layout.placements.Add(new() { placement_id = "card-" + x, source_key = path, cell = new(x, 0) });
                x += skill!.board_size.X;
            }
            Check(layout.placements.Count == 8 && CombatBoardGeometry.Validate(layout, catalog.FindSkill).Count == 0,
                "八个正式招式通过真实盘面几何校验");
            var deck = BoardDeckFactory.Build(123, layout, catalog);
            Check(deck.Success && deck.Cards.Count == 8, "从摆盘建立八张独立实体牌");
            var state = new BoardHandState(deck.Cards, new Random(42));
            var sameSeed = new BoardHandState(deck.Cards, new Random(42));
            Check(state.DrawPile.Select(card => card.Key).SequenceEqual(sameSeed.DrawPile.Select(card => card.Key)),
                "固定种子生成可复现洗牌顺序");
            Check(state.DrawOpeningHand() == 4 && state.Hand.Count == 4 && state.DrawOpeningHand() == 0,
                "起手抽四张，重复调用不再次抽牌");
            var hand = state.Hand.Select(card => card.Key).ToArray();
            Check(state.TryEnqueue(hand[0], 0, 3, out _) && state.TryEnqueue(hand[1], 0, 2, out _), "第一队列按调用方给出的容量排两张牌");
            Check(state.GetQueueUsed(0) == 5 && !state.TryEnqueue(hand[2], 0, 1, out _), "容量满时拒绝加入且牌保留在手中");
            Check(!state.TryEnqueue(hand[2], 0, -1, out _) && !state.TryEnqueue(hand[2], 2, 1, out _), "非法成本和队列均被拒绝");
            Check(state.TryEnqueue(hand[2], 1, 5, out _) && !state.TryEnqueue(hand[2], 1, 0, out _),
                "第二队列独立计算容量，已排队的牌不能再次排入");
            Check(state.TryReorder(0, hand[1], 0) && state.GetQueue(0)[0].Card.Key == hand[1], "同一队列可调整顺序");
            Check(state.TryWithdraw(hand[1]) && !state.TryWithdraw(hand[1]) && state.GetQueueUsed(0) == 3
                && state.Hand.Any(card => card.Key == hand[1]), "撤回释放容量并回到手牌，不能重复撤回");
            Check(state.TryEnqueue(hand[1], 0, 2, out _) && state.GetQueueUsed(0) == 5, "撤回后可重新入队");
            var retained = state.Hand.Select(card => card.Key).ToHashSet();
            Check(state.DrawForRound(1) == 2 && state.DrawForRound(1) == 0
                && retained.IsSubsetOf(state.Hand.Select(card => card.Key)), "每回合补二、重复同回合不抽，未使用手牌保留");
            Check(state.TryDiscardQueued(hand[1]) && state.TryDiscardQueued(hand[2])
                && !state.TryDiscardQueued(hand[2]) && state.DiscardPile.Count == 2, "已结算队列牌进入弃牌且不会重复弃置");
            Check(state.DrawForRound(2) == 2 && state.DrawPile.Count == 0 && state.DiscardPile.Count == 2,
                "仍有抽牌堆时不提前洗弃牌");
            Check(state.DrawForRound(3) == 2 && state.DiscardPile.Count == 0, "抽牌堆为空时洗弃牌再抽");
            var all = state.Hand.Concat(state.DrawPile).Concat(state.DiscardPile)
                .Concat(state.GetQueue(0).Select(item => item.Card)).Concat(state.GetQueue(1).Select(item => item.Card)).ToArray();
            Check(all.Length == 8 && all.Select(card => card.Key).Distinct().Count() == 8
                && all.Select(card => card.Key).ToHashSet().SetEquals(deck.Cards.Select(card => card.Key)),
                "抽牌、弃洗和队列之间所有实体牌身份守恒");
            Check(state.DrawForRound(4) == 0 && state.GetQueue(0).Count == 1, "无可抽牌时队列中的牌不会被洗入或复制");
            Check(state.TryWithdraw(hand[0]) && state.Hand.Count == 8, "全部牌可通过撤回归还手牌");
            Check(state.TryDiscardHand(hand[0]) && state.DrawForRound(5) == 1 && state.Hand.Count == 8,
                "明确弃手牌后也能在抽空时重新洗入");
            var rejected = false;
            try { _ = new BoardHandState([deck.Cards[0], deck.Cards[0]], new Random(1)); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "拒绝重复实体身份的初始牌组");

            GD.Print($"BOARD_HAND_STATE_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("BOARD_HAND_STATE_SMOKE_FAILED " + error);
            GetTree().Quit(1);
        }
    }
}
