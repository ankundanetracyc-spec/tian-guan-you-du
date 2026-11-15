using Godot;

/// <summary>固定掷点检查交锋结算，不读取或修改正式技能数据。</summary>
public partial class CombatClashOutcomeSmoke : Node
{
    private int _checks;

    public override void _Ready() => CallDeferred(nameof(Run));

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    private void Run()
    {
        try
        {
            foreach (var firstStyle in Enum.GetValues<CombatClashAttackStyle>())
            foreach (var secondStyle in Enum.GetValues<CombatClashAttackStyle>())
            for (int first = 8; first <= 11; first++)
            for (int second = 8; second <= 11; second++)
            {
                var result = CombatClashOutcome.Attack(firstStyle, first, secondStyle, second);
                var reverse = CombatClashOutcome.Attack(secondStyle, second, firstStyle, first);
                Check(result.FirstHits == reverse.SecondHits && result.SecondHits == reverse.FirstHits
                    && result.FirstRetained == reverse.SecondRetained && result.SecondRetained == reverse.FirstRetained,
                    "交换双方不应改变谁命中或保留");
                Check(!(result.FirstHits && result.SecondHits) && !(result.FirstRetained && result.SecondRetained),
                    "一次交锋不能让双方同时命中或同时保留");
                if (first == second)
                    Check(result == default, "平局时双方结束且无人拼赢");
                else if (firstStyle == CombatClashAttackStyle.Melee && secondStyle == CombatClashAttackStyle.Ranged && first > second)
                    Check(result.FirstWon && result.FirstRetained && !result.FirstHits, "近战先破远程，不提前命中");
                else if (secondStyle == CombatClashAttackStyle.Melee && firstStyle == CombatClashAttackStyle.Ranged && second > first)
                    Check(result.SecondWon && result.SecondRetained && !result.SecondHits, "后手近战同样先破远程");
                else
                    Check(result.FirstHits == (first > second) && result.SecondHits == (second > first), "其余攻击由胜方命中");
            }

            var guardWin = CombatClashOutcome.Guard(8, 10, 16, 8, 4);
            var guardTie = CombatClashOutcome.Guard(9, 9, 16, 8, 4);
            var guardLose = CombatClashOutcome.Guard(10, 8, 16, 8, 4);
            Check(guardWin.GuardWon && !guardWin.AttackWon && guardWin.Block == 16, "化解胜利使用招式提供的胜利值");
            Check(!guardTie.GuardWon && !guardTie.AttackWon && guardTie.Block == 8, "化解平局使用平局值");
            Check(guardLose.AttackWon && !guardLose.GuardWon && guardLose.Block == 4, "化解失败使用失败值");
            Check(CombatClashOutcome.Guard(8, 10, 3, 2, 1).Block == 3, "不同守招可给出不同结果，不硬编码倍率");

            var evadeWin = CombatClashOutcome.Evade(8, 10);
            var evadeTie = CombatClashOutcome.Evade(9, 9);
            var evadeLose = CombatClashOutcome.Evade(10, 8);
            Check(evadeWin.EvasionWon && evadeWin.EvasionRetained && !evadeWin.AttackHits, "闪避胜利并保留");
            Check(!evadeTie.EvasionRetained && evadeTie.AttackHits && !evadeTie.AttackWon, "闪避平局结束且无拼赢效果");
            Check(!evadeLose.EvasionRetained && evadeLose.AttackHits && evadeLose.AttackWon, "闪避失败后攻击命中");

            Check(CombatClashOutcome.GeneratesContinuation(true, true, true, true, true), "拼赢且命中生成下一式");
            Check(!CombatClashOutcome.GeneratesContinuation(true, true, false, true, true), "破术尚未命中不生成下一式");
            Check(!CombatClashOutcome.GeneratesContinuation(true, false, true, true, true), "未拼赢即使命中也不生成下一式");
            Check(CombatClashOutcome.GeneratesContinuation(false, false, true, true, true), "无对抗命中可生成下一式");
            Check(!CombatClashOutcome.GeneratesContinuation(false, false, true, false, true), "施招者失效不生成下一式");
            Check(!CombatClashOutcome.GeneratesContinuation(false, false, true, true, false), "目标离场不生成下一式");

            GD.Print($"COMBAT_CLASH_OUTCOME_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("COMBAT_CLASH_OUTCOME_SMOKE_FAILED " + error);
            GetTree().Quit(1);
        }
    }
}
