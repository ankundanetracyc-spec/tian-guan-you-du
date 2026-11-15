using Godot;

/// <summary>使用正式催血与百骸化法验证摆盘转牌快照，不启动战斗。</summary>
public partial class BoardDeckFactorySmoke : Node
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
            var books = GongfaAvailability.EnabledBooks().Where(book => new[]
            {
                "玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册", "舒卷天章"
            }.Contains(book.gongfa_name)).ToArray();
            var data = NpcCombatBuildPlanner.TestLearning(books);
            data.main_gongfa_path = "res://resources/gongfa/百骸衍枝化生宝册.tres";
            var actor = new Character();
            actor.load_from_data(data);
            var catalog = CombatBoardCatalog.FromCharacter(actor);
            actor.Free();

            const string skillPath = "res://resources/activeskills/催血.tres";
            const string effect = "baihai_c1_cuixue";
            var source = catalog.rewrites.Single(item => item.effect_ids.Contains(effect));
            var basis = catalog.FindSkill(skillPath) ?? throw new InvalidOperationException("缺少正式催血");
            Check(!basis.gongfa_rewrite_effect_ids.Contains(effect), "正式催血及化法来源可加载");
            var layout = new CombatBoardLayout
            {
                size = new(3, 2), confirmed = true,
                placements =
                [
                    new() { placement_id = "a", source_key = skillPath, cell = new(0, 0) },
                    new() { placement_id = "b", source_key = skillPath, cell = new(0, 1) },
                    new() { placement_id = "rewrite", source_key = source.source_key,
                        kind = CombatBoardPlacement.ObjectKind.Rewrite, cell = new(2, 0), rewrite_direction = 2 }
                ]
            };
            var issues = CombatBoardGeometry.Validate(layout, catalog.FindSkill,
                rewriteLimit: key => catalog.FindRewrite(key)?.max_placements ?? 0);
            Check(issues.Count == 0, "真实双份催血盘通过占格、份数及化法来源校验");

            var result = BoardDeckFactory.Build(1, layout, catalog);
            Check(result.Success && result.Cards.Count == 2 && result.OtherPlacements.Count == 1, "每份招式产生一张牌并保留化法对象");
            var first = result.Cards.Single(card => card.Key.PlacementId == "a");
            var second = result.Cards.Single(card => card.Key.PlacementId == "b");
            Check(first.Key != second.Key && first.SourceKey == skillPath && second.SourceKey == skillPath,
                "同来源两份保持独立身份和原来源");
            Check(first.Skill.gongfa_rewrite_effect_ids.Contains(effect) && !second.Skill.gongfa_rewrite_effect_ids.Contains(effect),
                "箭头只改写被指向的那一张牌");
            Check(!basis.gongfa_rewrite_effect_ids.Contains(effect) && !ReferenceEquals(first.Skill, basis)
                && !ReferenceEquals(first.Skill, second.Skill), "卡牌副本不污染原资源或另一张牌");
            Check(first.Skill.get_board_skill_key() == skillPath && first.Skill.energy_cost == basis.energy_cost
                && first.Skill.mana_cost == basis.mana_cost && first.Skill.target_type == basis.target_type,
                "化法副本保留技能身份、费用和目标");
            var otherOwner = BoardDeckFactory.Build(2, layout, catalog);
            Check(otherOwner.Success && first.Key != otherOwner.Cards.Single(card => card.Key.PlacementId == "a").Key,
                "不同角色的相同摆放编号不冲突");

            var otherLayout = layout.CopyConfiguration();
            otherLayout.placements.Add(new() { placement_id = "pill", kind = CombatBoardPlacement.ObjectKind.Pill,
                source_key = "pill-source", cell = new(2, 1), expended = true });
            otherLayout.placements.Add(new() { placement_id = "indicator", kind = CombatBoardPlacement.ObjectKind.ZhouTianIndicator,
                source_key = "indicator-source", cell = new(0, 2), locked = true });
            otherLayout.placements.Add(new() { placement_id = "talisman", kind = CombatBoardPlacement.ObjectKind.Talisman,
                source_key = "talisman-source", cell = new(1, 2) });
            var others = BoardDeckFactory.Build(1, otherLayout, catalog);
            Check(others.Success && others.Cards.Count == 2 && others.OtherPlacements.Count == 4,
                "化法、丹药、周天与符箓单列且不变成牌");
            Check(others.OtherPlacements.Single(item => item.placement_id == "pill").expended
                && others.OtherPlacements.Single(item => item.placement_id == "indicator").locked,
                "非招式保留消耗及锁定标记");
            otherLayout.placements.Single(item => item.placement_id == "pill").source_key = "changed";
            layout.placements[0].placement_id = "changed";
            Check(others.OtherPlacements.Single(item => item.placement_id == "pill").source_key == "pill-source"
                && first.Key.PlacementId == "a", "输出不随原盘修改而变化");

            var duplicate = otherLayout.CopyConfiguration();
            duplicate.placements[1].placement_id = duplicate.placements[0].placement_id;
            var failure = BoardDeckFactory.Build(1, duplicate, catalog);
            Check(!failure.Success && failure.Cards.Count == 0 && failure.OtherPlacements.Count == 0
                && failure.Errors.Any(error => error.Contains("重复")), "重复编号整体失败，不返回半副牌");
            var badSkill = otherLayout.CopyConfiguration(); badSkill.placements[0].source_key = "missing-skill";
            failure = BoardDeckFactory.Build(1, badSkill, catalog);
            Check(!failure.Success && failure.Cards.Count == 0 && failure.Errors.Any(error => error.Contains("missing-skill")),
                "缺失招式来源明确失败");
            var badRewrite = otherLayout.CopyConfiguration(); badRewrite.placements[2].source_key = "missing-rewrite";
            failure = BoardDeckFactory.Build(1, badRewrite, catalog);
            Check(!failure.Success && failure.Cards.Count == 0 && failure.Errors.Any(error => error.Contains("missing-rewrite")),
                "缺失化法来源明确失败");
            var empty = BoardDeckFactory.Build(1, new CombatBoardLayout(), catalog);
            Check(empty.Success && empty.Cards.Count == 0 && empty.OtherPlacements.Count == 0, "空盘不凭空生成牌");

            GD.Print($"BOARD_DECK_FACTORY_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("BOARD_DECK_FACTORY_SMOKE_FAILED " + error);
            GetTree().Quit(1);
        }
    }
}
