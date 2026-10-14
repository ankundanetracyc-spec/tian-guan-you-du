using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>山门迎真因果事实判读与硬门规保守匹配的轻量回归检查。</summary>
public partial class YuxuAdmissionSmoke : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T Read<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
    private static void Call(object value, string name, params object[] arguments) => value.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(value, arguments);
    private async Task Frames() { for (var index = 0; index < 3; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
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
            var actor = new Character { entity_name = "试演小友", entity_data = new HumanData { entity_name = "试演小友", appearance_seed = 684201 } };
            dialogue.SetProcess(false);
            foreach (var hasSect in new[] { true, false })
            {
                var answerCount = 0; var completedCount = 0;
                string[] facts = ["长老静静看了你片刻，收回神识，神色如常。", "长老察看灵机，颔首确认天灵根资质。"];
                dialogue.show_yuxu_admission_review(elder, facts, () =>
                    dialogue.show_yuxu_admission_question(elder, answer => {
                        Check(answer == hasSect, "宗门问答分支反转"); answerCount++;
                        dialogue.show_yuxu_admission_result(elder, [answer ? "既有所属，小友请便。" : "小友随接引弟子去办入门之礼。"],
                            () => completedCount++, player: actor);
                    }, player: actor), player: actor, lastLineIsAptitude: true);
                await Frames();
                var stage = dialogue.GetNode<Control>("YuxuAdmissionInteraction");
                Check(stage.FindChildren("YuxuAdmissionPaper", "*", true, false).Count == 0 &&
                    stage.FindChildren("YuxuAdmissionHeadshot", "*", true, false).Count == 0 &&
                    stage.FindChildren("*", "ProgressBar", true, false).Count == 0,
                    "迎真仍有旧纸面、顶部头像或进度线");
                Check(stage.FindChildren("PlayerSocialPortrait", "Control", true, false).Count == 1 &&
                    stage.FindChildren("NpcSocialPortrait", "Control", true, false).Count == 1 &&
                    Read<Character?>(dialogue, "_socialPlayer") == actor, "迎真没有使用玩家和长老各自立绘");
                var rows = Read<List<Label>>(dialogue, "_socialFactors");
                var words = Read<IReadOnlyList<NpcSocialPresentationText.Line>>(dialogue, "_socialLines");
                Check(words.Select(line => line.Result).SequenceEqual(facts) && words.Last().Rule == "yuxu_aptitude", "迎真证据被改写、重排或与资质混同");
                Call(dialogue, "process_social_reveal", .25);
                Check(rows[0].VisibleCharacters > 0 && rows[0].VisibleCharacters < words[0].Pending.Length && rows[1].Modulate.A == 0,
                    "迎真未逐字展示，或后续行提前出现");
                var skip = Read<Button>(dialogue, "_socialDone");
                skip.EmitSignal(BaseButton.SignalName.Pressed);
                Check(skip.Text == "继续" && answerCount == 0 && rows.Select(row => row.Text).SequenceEqual(facts), "略过提前问答或未还原真实证据");
                skip.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                var choices = dialogue.FindChildren("*", "Button", true, false).OfType<Button>().Where(button => button.IsVisibleInTree()).ToArray();
                Check(choices.Length == 2 && choices[0].Text == "有" && choices[1].Text == "无" &&
                    choices[0].GlobalPosition.Y < choices[1].GlobalPosition.Y && choices.All(button => button.Size.X <= 300),
                    "唯一宗门问答没有单列竖排或按钮过宽");
                choices[hasSect ? 0 : 1].EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                Check(answerCount == 1 && completedCount == 0, "回答重复执行或未确认就完成分支");
                Call(dialogue, "process_social_reveal", 100.0);
                var done = Read<Button>(dialogue, "_socialDone");
                Check(done.Text == "继续", "自然播放结束后未恢复继续按钮");
                done.EmitSignal(BaseButton.SignalName.Pressed);
                Check(completedCount == 1 && !dialogue.Visible, "迎真分支结果未单次完成并退出");
                dialogue.close(); Check(completedCount == 1, "退出迎真后重复触发回调");
                await Frames();
            }
            dialogue.show_narration("普通剧情回归检查");
            Check(Read<PanelContainer>(dialogue, "_dialogueFrame").Visible && Read<Control?>(dialogue, "_socialContent") is null,
                "迎真结束后普通剧情底栏未恢复");
            dialogue.close(); actor.Free();
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
