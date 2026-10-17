using Godot;

/// <summary>外祭核心规则检查；使用测试ID和容量，不建立正式法器配方、技能或平衡数值。</summary>
public partial class ArtifactBattleRuntimeSmoke : Node
{
    private int _checks;
    private void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++;
    }

    public override void _Ready()
    {
        try
        {
            var runtime = new ArtifactBattleRuntime([("test-a", 2), ("test-b", 1)]);
            var initial = runtime.PreviewQueue(["test-a", null, "test-a", "test-b", "test-b"]);
            Check(initial.SequenceEqual(new[] { ArtifactBattleRuntime.Intent.Summon, ArtifactBattleRuntime.Intent.None,
                ArtifactBattleRuntime.Intent.Use, ArtifactBattleRuntime.Intent.Summon, ArtifactBattleRuntime.Intent.Use }), "两件法器独立预演且普通行动保留位置");
            Check(runtime.UsedSpirit == 0, "预演不占灵识");
            Check(runtime.CanExecute("test-a", ArtifactBattleRuntime.Intent.Summon), "未祭出允许召唤意图");
            Check(!runtime.CanExecute("test-a", initial[2]), "召唤尚未成功时后续御使无效");
            Check(!runtime.TryCommitSummon("test-a", 1, 0), "容量不足召唤失败");
            Check(runtime.UsedSpirit == 0 && !runtime.CanExecute("test-a", initial[2]), "失败不改状态且御使不转召唤");
            Check(!runtime.CanSummon("test-a", 0, 0), "零容量不是无限容量");
            Check(!runtime.CanSummon("test-a", 2, 1), "计入其他持续实体占用");
            Check(runtime.TryCommitSummon("test-a", 3, 1), "恰好达到总容量可召唤");
            Check(runtime.UsedSpirit == 2, "成功召唤才占用");
            Check(!runtime.TryCommitSummon("test-a", 3, 0) && runtime.UsedSpirit == 2, "重复提交不能重复召唤或占用");
            Check(runtime.CanExecute("test-a", ArtifactBattleRuntime.Intent.Use), "在场允许御使");
            Check(runtime.CanExecute("test-a", ArtifactBattleRuntime.Intent.Use) && runtime.UsedSpirit == 2, "多次御使校验不重复占灵识");
            Check(runtime.PreviewQueue([null, "test-a", "test-a"]).Skip(1).All(intent => intent == ArtifactBattleRuntime.Intent.Use), "后续回合在场直接御使");
            Check(runtime.PreviewQueue(["test-b", "test-b"])[0] == ArtifactBattleRuntime.Intent.Summon, "重新排列后第一项重算为召唤");
            Check(runtime.TryCommitSummon("test-b", 3, 0) && runtime.UsedSpirit == 3, "第二件法器独立占用");
            Check(runtime.TryCollapse("test-a") && runtime.UsedSpirit == 1, "崩解只释放该法器占用");
            Check(!runtime.CanExecute("test-a", ArtifactBattleRuntime.Intent.Use) && !runtime.TryCommitSummon("test-a", 3, 0), "崩解后不能御使或重召");
            Check(!runtime.TryCollapse("test-a") && runtime.UsedSpirit == 1, "重复崩解不重复释放");
            Check(runtime.PreviewQueue(["test-a", "missing"] ).All(intent => intent == ArtifactBattleRuntime.Intent.Unavailable), "崩解与无来源法器均不可排用");
            Check(!runtime.CanSummon("missing", 3, 0) && !runtime.CanExecute("missing", ArtifactBattleRuntime.Intent.Use), "未知实例不获得能力");
            Check(!runtime.CanSummon("test-b", -1, 0) && !runtime.CanSummon("test-b", 3, -1), "非法容量和占用被拒绝");
            runtime.FinishBattle();
            Check(runtime.UsedSpirit == 0 && !runtime.CanExecute("test-b", ArtifactBattleRuntime.Intent.Use), "结束战斗释放占用并禁用旧运行态");
            Check(runtime.PreviewQueue(["test-b"])[0] == ArtifactBattleRuntime.Intent.Unavailable, "结束后不能继续预演为可用");
            var nextBattle = new ArtifactBattleRuntime([("test-b", 1)]);
            Check(nextBattle.PreviewQueue(["test-b"])[0] == ArtifactBattleRuntime.Intent.Summon, "下一场重新召唤");
            var large = new ArtifactBattleRuntime([("test-large-a", int.MaxValue), ("test-large-b", int.MaxValue)]);
            Check(large.TryCommitSummon("test-large-a", int.MaxValue, 0) && !large.TryCommitSummon("test-large-b", int.MaxValue, 0), "大数配置不能溢出绕过容量");
            var duplicateRejected = false;
            try { _ = new ArtifactBattleRuntime([("same", 1), ("same", 1)]); }
            catch (ArgumentException) { duplicateRejected = true; }
            Check(duplicateRejected, "重复实例ID拒绝建立运行态");
            GD.Print($"ARTIFACT_BATTLE_RUNTIME_SMOKE: {_checks} checks passed");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
