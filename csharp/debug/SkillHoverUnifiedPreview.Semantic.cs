using Godot;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public partial class SkillHoverUnifiedPreview
{
    private async Task VerifyCurrentRewrites()
    {
        var books = GongfaAvailability.EnabledBooks().Where(book => new[]
        {
            "玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册", "舒卷天章",
        }.Contains(book.gongfa_name)).ToArray();
        Check(books.Length == 5, "rewrite audit uses the five current human manuals");
        foreach (var book in books)
        {
            var learning = NpcCombatBuildPlanner.TestLearning([book]);
            learning.entity_name = "真实已学化法显示样例";
            learning.unique_id = "isolated-hover-rewrite-" + book.gongfa_name;
            learning.main_gongfa_path = book.ResourcePath;
            var actor = new Character();
            actor.load_from_data(learning);
            try
            {
                // Exact effective values from the user's screenshot, kept on an isolated actor.
                actor.physique = 46.51f;
                actor.soul = 28.65f;
                var choices = actor.get_skill_rewrite_choices().Where(choice => choice.Available && choice.BoardDefinitionVersion == 1).ToArray();
                var expectedCount = book.gongfa_name switch
                {
                    "玄枢含章经" => 1,
                    "斫基损契玄经" => 1,
                    "百骸衍枝化生宝册" => 2,
                    "太和感物养元宝册" => 2,
                    "舒卷天章" => 1,
                    _ => throw new Exception("unreviewed manual: " + book.gongfa_name),
                };
                Check(choices.Length == expectedCount, book.gongfa_name + ": current retained rewrite sources only");
                foreach (var choice in choices)
                {
                    var target = _catalog.skills.FirstOrDefault(choice.Matches)
                        ?? throw new Exception(choice.Source + ": no actual learned base target");
                    var baseline = (target.energy_cost, target.mana_cost, target.board_size, target.description, target.gongfa_rewrite_effect_ids.Count);
                    var applied = choice.Apply(target);
                    var info = CombatBoardPresentation.SkillHover(applied, actor);
                    var summary = PlayerInfoText.Summary(info.Body);
                    var detailed = PlayerInfoText.Detailed(info.Body);
                    Check(PlayerInfoText.Summary(detailed) == summary, choice.Source + ": both modes retain the same costs targets and final effects");
                    Check(PlayerInfoText.Detailed(detailed) == detailed, choice.Source + ": current rewrite has idempotent calculation sources");
                    Check(!detailed.Contains("，计算：") && !Regex.IsMatch(detailed, @"取(?:较|最)小值|\b(?:MIN|round|floor|ceil)\s*[(（]|四舍五入|向[上下]取整"),
                        choice.Source + ": actual rewrite uses min and canonical sources without internal rounding jargon");
                    var laidOut = PlayerInfoText.LayoutLongParagraphs(detailed);
                    Check(PlayerInfoText.LayoutLongParagraphs(laidOut) == laidOut
                        && Regex.Replace(laidOut, @"\s+", "") == Regex.Replace(detailed, @"\s+", ""),
                        choice.Source + ": semantic layout preserves every formula and effect character");
                    Check(baseline == (target.energy_cost, target.mana_cost, target.board_size, target.description, target.gongfa_rewrite_effect_ids.Count),
                        choice.Source + ": formatting does not mutate the source skill or its gameplay values");
                    _rewriteOutputs.Add(new { source = choice.Source, skill = target.skill_name, summary, detailed,
                        effective_physique = actor.physique, effective_soul = actor.soul });
                    _rewritesAudited++;
                }
            }
            finally { actor.Free(); }
        }
        Check(_rewritesAudited == 7, "all seven current human rewrites audited from actual learned chapters");
    }
}
