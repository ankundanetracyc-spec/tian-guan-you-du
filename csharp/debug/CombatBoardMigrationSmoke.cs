using Godot;
using System.Text.Json;

/// <summary>隔离验证真实招式资源、逐格状态和旧档往返，不访问正式旅程。</summary>
public partial class CombatBoardMigrationSmoke : Node
{
    private int _checks;
    private readonly Dictionary<string, ActiveSkillData> _skills = new(StringComparer.Ordinal);
    private void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); _checks++; }
    private ActiveSkillData Skill(string name) => _skills.Values.First(s => s.skill_name == name);
    private ActiveSkillData? Resolve(string key) => _skills.GetValueOrDefault(key);
    private CombatBoardPlacement Place(string name, int x, int y, string id, int turns = 0) => new()
    { placement_id = id, source_key = Skill(name).get_board_skill_key(), cell = new(x, y), quarter_turns = turns };
    private static void Drain(CombatBoardRoundState state)
    { while (state.TryReadNextCell(out _)) { } }

    public override void _Ready() => CallDeferred(nameof(Run));
    private void Run()
    {
        try
        {
            LoadDefinitions(); CheckTraversal(); CheckResponse(); CheckSharedLimits(); CheckCooldownAndExtra();
            CheckGeometryAndMigration();
            GD.Print($"COMBAT_BOARD_SMOKE_OK checks={_checks}"); GetTree().Quit();
        }
        catch (Exception exception)
        { GD.PushError("COMBAT_BOARD_SMOKE_FAILED " + exception); GetTree().Quit(1); }
    }

    private void LoadDefinitions()
    {
        using var report = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
            "res://docs/美术规范/人类招式占格审阅_20261107/几何检查结果.json"));
        foreach (var row in report.RootElement.GetProperty("skills").EnumerateArray())
        {
            var path = row.GetProperty("resource").GetString()!;
            var skill = GD.Load<ActiveSkillData>(path);
            Check(skill is not null && skill.has_board_definition(), "缺少正式盘面定义 " + path);
            var size = row.GetProperty("size");
            Check(skill!.board_size == new Vector2I(size[0].GetInt32(), size[1].GetInt32()), "占格与已确认表一致");
            Check(skill.board_max_placements == row.GetProperty("max_placements").GetInt32(), "份数与已确认表一致");
            Check(skill.get_board_skill_key() == path, "稳定身份保持原路径");
            _skills.Add(path, skill);
        }
        Check(_skills.Count == 20, "完整20招");
        Check(Skill("痛击").get_board_footprint(1) == new Vector2I(1, 2), "两格横竖旋转");
        Check(Skill("尽锋").get_board_footprint(1) == new Vector2I(1, 3), "三格横竖旋转");
        Check(Skill("复命").get_board_footprint(0) == new Vector2I(2, 2), "复命保持自身四格");
        var rewritten = GD.Load<ActiveSkillData>("res://resources/activeskills/百骸衍枝化生宝册/化法/催血.tres");
        Check(rewritten.get_board_skill_key() == Skill("催血").get_board_skill_key(), "化法副本回到原身份");
    }

    private void CheckTraversal()
    {
        var layout = new CombatBoardLayout { placements = [Place("痛击", 1, 0, "pain", 1)] };
        var state = new CombatBoardRoundState(layout, Resolve);
        Check(state.BeginRound(1), "首轮初始化");
        var visits = 0; var successes = 0;
        while (state.TryReadNextCell(out var visit))
        {
            visits++;
            if (visit.Placement is null) continue;
            if (state.CanUsePlacement(visit.Placement) && state.RecordSuccessfulCast(visit.Placement)) successes++;
        }
        Check(visits == 9 && successes == 1, "多格逐次经过但只成功一次");
        Check(!state.BeginRound(1) && state.HasSucceeded("pain"), "重复进入同轮不刷新机会");
        Check(!state.TryReadNextCell(out _), "最后一格后不补查");

        var retry = new CombatBoardRoundState(layout, Resolve); retry.BeginRound(1);
        var encountered = 0;
        while (retry.TryReadNextCell(out var visit))
        {
            if (visit.Placement is null) continue;
            encountered++;
            // 最新裁定：首次检查失败也整份变暗，后续占格不能补查。
            if (encountered == 2) Check(!retry.RecordSuccessfulCast(visit.Placement), "后续占格不再重试暗招式");
        }
        Check(encountered == 2 && retry.HasBeenChecked("pain") && !retry.HasSucceeded("pain"), "失败后变暗而不结算成功或费用");
    }

    private void CheckResponse()
    {
        var layout = new CombatBoardLayout { placements = [Place("架招", 2, 0, "guard"), Place("格剑", 0, 1, "sword_guard")] };
        var state = new CombatBoardRoundState(layout, Resolve); state.BeginRound(1);
        var guard = state.Layout.placements[0];
        Check(!state.Arm(guard), "扫到之前不能待命");
        while (state.TryReadNextCell(out var visit))
            if (visit.Placement is not null && visit.Skill is not null && CardRules.IsResponse(visit.Skill)) state.Arm(visit.Placement);
        Check(state.IsArmed("guard") && state.IsArmed("sword_guard"), "自己走完整盘仍待命");
        var candidates = state.ArmedInOrder().ToList();
        Check(candidates.Count == 2 && candidates[0].placement_id == "guard", "合格待命按盘序");
        Check(state.RecordSuccessfulCast(candidates[0]) && !state.IsArmed("guard"), "成功应对才消耗该份机会");
        Check(state.IsArmed("sword_guard"), "未选的待命留给后续来袭");
        state.EndCommonRound(); Check(!state.ArmedInOrder().Any(), "全员共同轮末结束待命");
    }

    private void CheckSharedLimits()
    {
        var limited = (ActiveSkillData)Skill("应敌").Duplicate(true); limited.uses_per_battle = 1;
        var layout = new CombatBoardLayout { placements = [Place("应敌", 0, 0, "a"), Place("应敌", 1, 0, "b")] };
        ActiveSkillData? Limited(string key) => key == limited.get_board_skill_key() ? limited : Resolve(key);
        var state = new CombatBoardRoundState(layout, Limited); state.BeginRound(1);
        state.TryReadNextCell(out var first); Check(state.RecordSuccessfulCast(first.Placement!), "第一份成功扣共享次数");
        state.TryReadNextCell(out var second);
        Check(!state.CanUsePlacement(second.Placement!) && state.RemainingBattleUses(limited) == 0, "同招各份共享场限次");
        Drain(state);
        var replacement = layout.CopyConfiguration(); replacement.placements[0].placement_id = "new_id";
        state.ReplaceLayoutAtRoundBoundary(replacement); state.BeginRound(2); state.TryReadNextCell(out var afterMove);
        Check(!state.CanUsePlacement(afterMove.Placement!), "换盘与新摆放ID不刷新共享余次");

        var twice = (ActiveSkillData)Skill("应敌").Duplicate(true); twice.uses_per_battle = 2;
        var battleTwice = new CombatBoardRoundState(layout, key => key == twice.get_board_skill_key() ? twice : Resolve(key));
        battleTwice.BeginRound(1); battleTwice.TryReadNextCell(out var ordinary);
        Check(battleTwice.RecordSuccessfulCast(ordinary.Placement!) && battleTwice.RemainingBattleUses(twice) == 1, "限2普通施放后整场剩一次");
        Check(battleTwice.RecordSuccessfulCast(ordinary.Placement!, zhouTian: true) && battleTwice.RemainingBattleUses(twice) == 0, "限2周天额外施放同样扣整场次数");
        battleTwice.TryReadNextCell(out var another);
        Check(!battleTwice.RecordSuccessfulCast(another.Placement!), "限2多摆一份不能取得第三次生效");
        Drain(battleTwice); battleTwice.ReplaceLayoutAtRoundBoundary(replacement); battleTwice.BeginRound(2);
        battleTwice.TryReadNextCell(out var nextRound);
        Check(!battleTwice.RecordSuccessfulCast(nextRound.Placement!) && battleTwice.RemainingBattleUses(twice) == 0, "限2跨回合改盘仍耗尽，不是每轮两次");

        var independent = new CombatBoardRoundState(layout, Resolve); independent.BeginRound(1);
        independent.TryReadNextCell(out var one); var firstSucceeded = independent.RecordSuccessfulCast(one.Placement!);
        independent.TryReadNextCell(out var two);
        Check(firstSucceeded && independent.RecordSuccessfulCast(two.Placement!), "不限总次时不同份各成功一次");
    }

    private void CheckCooldownAndExtra()
    {
        var layout = new CombatBoardLayout { placements = [Place("复命", 0, 0, "replay")] };
        var state = new CombatBoardRoundState(layout, Resolve); state.BeginRound(1); state.TryReadNextCell(out var first);
        Check(state.RecordSuccessfulCast(first.Placement!), "复命成功记录冷却");
        for (var round = 2; round <= 4; round++)
        {
            state.BeginRound(round); state.TryReadNextCell(out var next);
            Check(state.CanUsePlacement(next.Placement!) == (round == 4), "完整回合冷却 R+N+1");
        }
        var attackLayout = new CombatBoardLayout { placements = [Place("应敌", 0, 0, "attack")] };
        var extra = new CombatBoardRoundState(attackLayout, Resolve); extra.BeginRound(1); extra.TryReadNextCell(out var normal);
        Check(extra.RecordSuccessfulCast(normal.Placement!), "普通扫格机会");
        Check(extra.RecordSuccessfulCast(normal.Placement!, zhouTian: true), "周天有单独额外机会");
        Check(!extra.RecordSuccessfulCast(normal.Placement!, zhouTian: true), "周天额外机会每轮一次");
    }

    private void CheckGeometryAndMigration()
    {
        var keys = new[] { Skill("应敌").ResourcePath, Skill("应敌").ResourcePath, Skill("应敌").ResourcePath,
            Skill("应敌").ResourcePath, Skill("痛击").ResourcePath, Skill("架招").ResourcePath,
            Skill("复命").ResourcePath, "res://missing_board_skill.tres", "artifact:real_sword", "" };
        var bindings = new Dictionary<string, string> { ["gongfa:retained_source"] = Skill("架招").ResourcePath };
        var preview = CombatBoardMigration.CreatePreview(keys, bindings, Resolve, true);
        Check(preview.legacy_skill_keys.SequenceEqual(keys), "旧十槽包含重复与空位完整保留");
        Check(preview.legacy_rewrite_bindings["gongfa:retained_source"] == bindings["gongfa:retained_source"], "旧化法绑定备份");
        Check(preview.placements.Count(p => p.kind == CombatBoardPlacement.ObjectKind.Skill)
            + preview.pending_entries.Count(p => p.legacy_slot_index >= 0) == keys.Count(k => k.Length > 0), "每项旧来源均有摆放或待处理记录");
        Check(preview.placements.Count(p => p.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator && p.locked) == 1, "玄枢预留单格锁定指示");
        Check(CombatBoardGeometry.Validate(preview, Resolve, requiresZhouTianIndicator: true).Count == 0, "迁移预览不重叠不越界且遵守上限");
        Check(!preview.confirmed, "预览不冒充已确认布局");
        var wrong = new CombatBoardLayout { placements = [Place("应敌", 0, 0, "same"), Place("架招", 0, 0, "same")] };
        Check(CombatBoardGeometry.Validate(wrong, Resolve).Count >= 2, "拒绝重叠与重复摆放编号");

        var owner = new HumanData { combat_skill_slot_paths = new(keys), skill_rewrite_bindings = new() { ["retained"] = "original" } };
        var save = new PersistentGameSave { format_version = 12, player_data = owner };
        Check(save.migrate_to_current_format() && save.format_version == 13, "旧档12到13顺序升级");
        Check(owner.combat_skill_slot_paths.SequenceEqual(keys) && owner.skill_rewrite_bindings["retained"] == "original", "格式升级不覆盖旧配置");
        owner.combat_board_layout = preview;
        Check(ReferenceEquals(CombatBoardMigration.EnsurePreview(owner, true), preview), "重复访问不重新导入或复制对象");
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://tmp/combat_board_20261107"));
        const string output = "res://tmp/combat_board_20261107/isolated_save.tres";
        Check(ResourceSaver.Save(save, output) == Error.Ok, "隔离旧档保存");
        var loaded = ResourceLoader.Load<PersistentGameSave>(output, cacheMode: ResourceLoader.CacheMode.Ignore);
        Check(loaded?.player_data?.combat_board_layout?.placements.Count == preview.placements.Count, "新盘与旧配置存读档往返");
        Check(loaded!.player_data!.combat_skill_slot_paths.SequenceEqual(keys), "往返后旧十槽仍完整");
    }
}
