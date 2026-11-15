using Godot;

/// <summary>驱动独立试玩的真实界面入口和多个回合，不访问正式存档。</summary>
public partial class CombatClashTrialSmoke : Node
{
    private int _checks;
    private void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); _checks++; }
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        try
        {
            var trial = GD.Load<PackedScene>("res://tscn/scene/CombatClashTrial.tscn").Instantiate<CombatClashTrial>();
            trial.TrialSeed = 42; AddChild(trial);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(trial.IsPlanning && trial.TrialRound == 1 && trial.PlayerHand.Hand.Count == 4
                && trial.EnemyHand.Hand.Count == 4, "第一回合起手4，不额外补牌");
            Check(trial.PlayerStats.cultivation_realm == "云息五层" && trial.PlayerStats.physique == 20
                && Mathf.IsEqualApprox(trial.PlayerStats.intelligence, 11.4f) && trial.PlayerStats.body_proficiency == 5
                && trial.PlayerStats.magic_proficiency == 6 && trial.PlayerStats.max_health == 303,
                "读取已确认的云息五层固定人物快照");
            Check(trial.PlayerStats.max_mana == 1 && trial.Payment.TryGetBalance("我方", out var initial)
                && initial.Stamina == 6 && initial.Mana == 1, "体力和法力上限初始化");
            var allCards = trial.PlayerHand.Hand.Concat(trial.PlayerHand.DrawPile).ToArray();
            Check(allCards.Length == 6 && allCards.Select(card => card.Key).Distinct().Count() == 6,
                "真实5×2摆盘六份实体牌且身份唯一");
            Check(trial.PreviewPlayerDamage(allCards.First(card => card.Skill.skill_name == "应敌")) == 16
                && trial.PreviewPlayerDamage(allCards.First(card => card.Skill.skill_name == "痛击")) == 26
                && trial.PreviewPlayerDamage(allCards.First(card => card.Skill.skill_name == "惊蛰")) == 17,
                "三招伤害读取正式资源公式及人物快照");
            Check(trial.FindChild("CommitPlan", true, false) is Button && trial.FindChild("AdvanceSubround", true, false) is Button,
                "可交互排招与推进按钮已创建");
            var labels = Descendants(trial).OfType<Label>().Select(label => label.Text).ToArray();
            Check(labels.Any(text => text.Contains("对手一队  速度3：招式隐藏"))
                && labels.Any(text => text.Contains("暂无定力、失神、击退")), "敌牌隐藏且明确试玩范围");
            Check(labels.Any(text => text.Contains("灵机 ") && text.Contains("基础伤害")), "攻击手牌公开灵机范围和基础伤害");
            var combatants = Descendants(trial).OfType<TrialCombatantView>().ToArray();
            var leftView = combatants.Single(view => view.IsLeft);
            var rightView = combatants.Single(view => !view.IsLeft);
            Check(combatants.Length == 2 && leftView.CurrentHealth == trial.PlayerStats.current_health
                && leftView.MaximumHealth == trial.PlayerStats.max_health && rightView.CurrentHealth == trial.EnemyStats.current_health
                && rightView.MaximumHealth == trial.EnemyStats.max_health, "双方竖直生命槽绑定真实气血");
            Check(!leftView.ResolveAvailable && !rightView.ResolveAvailable && leftView.Qi == trial.PlayerQi,
                "定力未接入且脚下状态不伪造数值");
            await CheckCardCostLayout();
            await CheckDisabledCardsStayReadable();

            var planned = 0; var sawNextRound = false;
            while (!trial.IsFinished && trial.TrialRound <= 100)
            {
                if (trial.IsPlanning)
                {
                    var available = trial.PlayerHand.Hand.ToArray();
                    foreach (var card in available)
                    {
                        var first = planned++ % 2;
                        if (!trial.QueuePlayerCard(card.Key, first, first)) trial.QueuePlayerCard(card.Key, 1 - first, first);
                    }
                    trial.CommitPlan();
                    Check(!trial.IsPlanning, "排招完成进入结算");
                }
                var originalRound = trial.TrialRound;
                for (var i = 0; i < 30 && !trial.IsPlanning && !trial.IsFinished; i++) trial.AdvanceSubround();
                Check(trial.IsPlanning || trial.IsFinished, "每轮在有限小轮中完成");
                if (trial.IsPlanning && trial.TrialRound > originalRound) sawNextRound = true;
                Check(IdentityCount(trial.PlayerHand) == 6 && IdentityCount(trial.EnemyHand) == 6,
                    "多回合牌堆、手牌、队列与弃牌身份守恒");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Check(sawNextRound && trial.TrialLog.Contains("恢复2体力") && trial.TrialLog.Contains("支付："),
                "完成多回合并显示恢复和真实付款收据");
            Check(trial.TrialLog.Contains("聚炁") && trial.TrialLog.Contains("周天"), "实际执行聚炁及周天回法流程");
            Check(trial.IsFinished && (trial.PlayerStats.current_health == 0 || trial.EnemyStats.current_health == 0)
                && trial.TrialLog.Contains("试剑结束"), "完整对局在有限回合内分出胜负");
            Check(leftView.CurrentHealth == trial.PlayerStats.current_health && rightView.CurrentHealth == trial.EnemyStats.current_health,
                "对局结束后双方生命槽保持实时同步");
            var finalPlayerHealth = trial.PlayerStats.current_health;
            var finalEnemyHealth = trial.EnemyStats.current_health;
            trial.Payment.TryGetBalance("我方", out var finalPlayerCost);
            trial.Payment.TryGetBalance("对手", out var finalEnemyCost);
            trial.CommitPlan(); trial.AdvanceSubround();
            Check(trial.PlayerStats.current_health == finalPlayerHealth && trial.EnemyStats.current_health == finalEnemyHealth
                && trial.Payment.TryGetBalance("我方", out var playerAfterFinish) && playerAfterFinish == finalPlayerCost
                && trial.Payment.TryGetBalance("对手", out var enemyAfterFinish) && enemyAfterFinish == finalEnemyCost,
                "战斗结束后无法继续行动或扣费");
            await CheckResourceFailures();
            GD.Print($"COMBAT_CLASH_TRIAL_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        { GD.PushError("COMBAT_CLASH_TRIAL_SMOKE_FAILED " + error); GetTree().Quit(1); }
    }

    private static int IdentityCount(BoardHandState hand)
    {
        var cards = hand.Hand.Concat(hand.DrawPile).Concat(hand.DiscardPile)
            .Concat(hand.GetQueue(0).Select(item => item.Card)).Concat(hand.GetQueue(1).Select(item => item.Card)).ToArray();
        return cards.Length == cards.Select(card => card.Key).Distinct().Count() ? cards.Length : -1;
    }

    private async Task CheckResourceFailures()
    {
        foreach (var (playerCanPay, enemyCanPay) in new[] { (true, false), (false, true), (false, false) })
        {
            var trial = GD.Load<PackedScene>("res://tscn/scene/CombatClashTrial.tscn").Instantiate<CombatClashTrial>();
            trial.TrialSeed = 42; AddChild(trial);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var playerAttack = trial.PlayerHand.Hand.First(card => card.Skill.skill_name != "聚炁");
            var firstTwo = trial.EnemyHand.Hand.Take(2).ToArray();
            var enemyTarget = Array.FindIndex(firstTwo, card => card.Skill.skill_name != "聚炁");
            Check(enemyTarget >= 0 && trial.QueuePlayerCard(playerAttack.Key, 0, enemyTarget), "准备双侧不足费测试行动");
            if (!playerCanPay) Check(trial.Payment.SetBalance("我方", new(0, 0)), "设置攻击方无资源");
            if (!enemyCanPay) Check(trial.Payment.SetBalance("对手", new(0, 0)), "设置防守方无资源");
            trial.CommitPlan(); trial.AdvanceSubround();
            if (playerCanPay && !enemyCanPay)
                Check(trial.EnemyStats.current_health < 303 && trial.TrialLog.Contains("对手的") && trial.TrialLog.Contains("资源不足"),
                    "防守方付不起时失效，发起方正常付款并攻击");
            else if (!playerCanPay && enemyCanPay)
                Check(trial.PlayerStats.current_health < 303 && trial.TrialLog.Contains("我方的") && trial.TrialLog.Contains("资源不足"),
                    "发起方付不起时失效，对方继续自己的行动");
            else
                Check(trial.PlayerStats.current_health == 303 && trial.EnemyStats.current_health == 303,
                    "双方都付不起时不产生伤害");
            trial.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task CheckDisabledCardsStayReadable()
    {
        var trial = GD.Load<PackedScene>("res://tscn/scene/CombatClashTrial.tscn").Instantiate<CombatClashTrial>();
        trial.TrialSeed = 42; AddChild(trial);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var advance = trial.FindChild("AdvanceSubround", true, false) as Button;
        Check(advance is { Disabled: true } && Descendants(advance).OfType<Label>()
            .Any(label => label.Visible && label.Text == "推进一小轮"), "未结算时推进按钮禁用但保留文字");
        trial.CommitPlan();
        var hand = Descendants(trial).OfType<Button>().FirstOrDefault(button => button.Name.ToString().StartsWith("Hand_"));
        var commit = trial.FindChild("CommitPlan", true, false) as Button;
        Check(hand is { Disabled: true } && Descendants(hand).OfType<Label>().Any(label => label.Visible && label.Text.Contains("占 ")),
            "非排招阶段未用手牌禁用但名称与费用仍可读取");
        Check(commit is { Disabled: true } && Descendants(commit).OfType<Label>().Any(label => label.Visible && label.Text == "确定排招"),
            "禁用确认按钮保留文字");
        trial.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckCardCostLayout()
    {
        var card = new TrialTechniqueCard(new TrialTechniqueCardData("样例", "近攻", 3, 2, 1, "6–11", "基础伤害 16", ""));
        AddChild(card);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var cost = card.GetNode<Label>("Cost");
        var kind = card.GetNode<Label>("Kind");
        var capacity = card.GetNode<Label>("Capacity");
        Check(cost.Text.Contains("体 2") && cost.Text.Contains("法 1") && cost.AnchorLeft < .2f
            && kind.Text == "近攻" && kind.AnchorLeft > .5f, "双资源费用在左上，类别在右上");
        Check(capacity.Text == "占 3" && capacity.AnchorTop > .8f, "队列点数与费用分开显示");
        card.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var free = new TrialTechniqueCard(new TrialTechniqueCardData("样例", "运转", 1, 0, 0, "", "获得炁 2", ""));
        AddChild(free);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(free.GetNode<Label>("Cost").Text == "无消耗", "零费牌明确显示无消耗");
        free.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren()) foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
