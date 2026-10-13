using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>山门迎真因果事实判读与硬门规保守匹配的轻量回归检查。</summary>
public partial class YuxuAdmissionSmoke : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public override void _Ready()
    {
        try
        {
            var record = new PlayerCausalEventRecord
            {
                event_id = "smoke.event.one",
                occurred_at_total_game_hours = 24,
                event_type_id = "defense",
                event_type_name = "平息兽患",
                location_id = "home_village",
                location_name = "青溪里",
                context = "山魈率兽群冲下山来。",
                outcome = "村中暂时平安。"
            };
            var player = new HumanData { karma_value = 1000 };
            Check(player.record_player_causal_event(record), "首次经历应写入");
            Check(!player.record_player_causal_event(record), "同一稳定事件 ID 不应重复写入");
            var savedCopy = player.Duplicate(true) as HumanData;
            Check(savedCopy?.player_causal_event_records.Count == 1 &&
                  savedCopy.player_causal_event_records[0].event_id == "smoke.event.one",
                "经历事实应随人物存档资源复制");

            var assessment = player.assess_player_causal_history();
            Check(assessment.history_conclusion == PlayerCausalHistoryConclusion.CitableFacts, "真实事件应成为可引用经历");
            Check(assessment.citable_facts.Count == 1, "事件应只出现一次");
            var lines = player.get_yuxu_gate_judgment_lines();
            Check(lines.Count == 1 && lines[0].Contains("平息兽患"), "交互文案应来自事件记录");
            Check(!lines.Any(line => line.Contains("业力") || line.Contains("声望") || line.Contains("分类缺失")), "评语不得用业力、声望或调试信息代替跟脚");

            var hardRuleRecord = new PlayerCausalEventRecord
            {
                event_id = "smoke.event.rule",
                occurred_at_total_game_hours = 48,
                event_type_id = "rule_evidence",
                event_type_name = "与门规相关的经历",
                outcome = "相关事实已记录。",
                supports_yuxu_existing_hard_rule = true,
                yuxu_hard_rule_id = "yuxu.existing.rule.smoke"
            };
            player.record_player_causal_event(hardRuleRecord);
            var unverified = player.assess_player_causal_history();
            Check(unverified.yuxu_hard_rule_conclusion != YuxuHardRuleConclusion.ConfirmedHardRuleFact,
                "没有可信规则目录时不得确认硬门规事实");
            var verified = player.assess_player_causal_history(new HashSet<string> { "yuxu.existing.rule.smoke" });
            Check(verified.yuxu_hard_rule_conclusion == YuxuHardRuleConclusion.ConfirmedHardRuleFact,
                "只有事件与可信现行规则 ID 匹配才确认硬门规");

            var dialogueScene = ResourceLoader.Load<PackedScene>("res://tscn/panel/StoryDialoguePanel.tscn");
            Check(dialogueScene is not null, "迎真展示面板场景应可加载");
            var dialogue = dialogueScene!.Instantiate<StoryDialoguePanel>();
            AddChild(dialogue);
            var elder = new WorldNpcData { npc_id = "smoke_elder", npc_name = "沈长老", faction_id = "yuxu_xianzong" };

            var reviewCancelled = false;
            dialogue.show_yuxu_admission_review(elder, ["长老察看一段经历。"], () => { }, () => reviewCancelled = true);
            dialogue.close();
            Check(reviewCancelled && !dialogue.Visible, "迎真评察按关闭/ESC 时应回调退出而非留在遮罩页");

            var answered = false;
            var questionCancelled = false;
            dialogue.show_yuxu_admission_question(elder, _ => answered = true, () => questionCancelled = true);
            dialogue.close();
            Check(questionCancelled && !answered, "宗门问答取消不应误触发回答分支");

            var resultCompleted = false;
            dialogue.show_yuxu_admission_result(elder, ["迎真礼毕。"], () => resultCompleted = true);
            dialogue.close();
            Check(resultCompleted && !dialogue.Visible, "迎真结果按关闭/ESC 应完成并收起展示层");
            dialogue.QueueFree();

            GD.Print("YuxuAdmissionSmoke: PASS");
        }
        catch (Exception exception)
        {
            GD.PushError($"YuxuAdmissionSmoke: FAIL — {exception}");
            GetTree().Quit(1);
            return;
        }

        GetTree().Quit();
    }
}
