using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Exercises the formal interaction entry points without loading or writing a player save.</summary>
public partial class NpcInteractionDirectionSmoke : Node
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Set(MainScene main, string name, object value) => typeof(MainScene).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, value);
    private static object? Call(MainScene main, string name, params object[] args) => typeof(MainScene).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, args);
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static Button Choice(StoryDialoguePanel panel, string prefix) => panel.FindChildren("*", "Button", true, false).OfType<Button>().First(button => button.IsVisibleInTree() && button.Text.StartsWith(prefix));
    private async Task Frames() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void CloseResult(StoryDialoguePanel panel) { panel.close(); panel.close(); }

    public override async void _Ready()
    {
        try
        {
            var main = new EastWoodsActionHarness();
            AddChild(main);
            var human = new HumanData { entity_name = "交谈核验", current_world_stamina = 100, max_world_stamina = 100 };
            var player = new Character { entity_name = human.entity_name, entity_data = human };
            main.AddChild(player);
            var dialogue = GD.Load<PackedScene>("res://tscn/panel/StoryDialoguePanel.tscn").Instantiate<StoryDialoguePanel>();
            main.AddChild(dialogue);
            var menu = new NpcInteractionPanel();
            main.AddChild(menu);
            var world = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            var npc = new WorldNpcData { npc_id = "interaction_direction_smoke", npc_name = "同修", current_node_id = "home_village", is_alive = true };
            npc.social_personality["risk"] = "冒进";
            world.npcs.Add(npc);
            Set(main, "player", player);
            Set(main, "story_dialogue", dialogue);
            Set(main, "npc_interactions", menu);
            Set(main, "_npc_world", world);
            Set(main, "_player_world_node_id", npc.current_node_id);
            main.open_world_npc_actions(npc);
            await Frames();
            var options = menu.FindChildren("*", "Button", true, false).OfType<Button>().Select(button => button.Text).ToArray();
            Check(options.Contains("交谈") && options.Contains("交易") && options.All(text => !text.StartsWith("测试：")), "正式菜单未撤下测试入口或误删正式交互");

            var talkButton = menu.FindChildren("*", "Button", true, false).OfType<Button>().First(button => button.IsVisibleInTree() && button.Text == "交谈");
            talkButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Check(!menu.Visible && dialogue.Visible, "从角色菜单点击交谈后没有打开对话，交互按钮链路未生效");
            var firstChoice = Choice(dialogue, "上前搭话");
            Check(Choice(dialogue, "作罢") is not null, "主动搭话缺少作罢选项");
            firstChoice.EmitSignal(BaseButton.SignalName.Pressed);
            firstChoice.EmitSignal(BaseButton.SignalName.Pressed);
            Check(NpcSocialRules.KnowsName(npc) && player.current_world_stamina == 99, "主动交谈未登记姓名或重复扣费");
            Check(Read<System.Collections.Generic.IReadOnlyList<NpcSocialPresentationText.Line>>(dialogue, "_socialLines").Any(line => line.Rule == "player_initiated"), "主动交谈被展示为接受来访");
            CloseResult(dialogue);

            main.open_world_npc_talk(npc);
            Check(Choice(dialogue, "聊聊近况") is not null, "熟人交谈仍用接受来访文案");
            var before = player.current_world_stamina;
            Choice(dialogue, "作罢").EmitSignal(BaseButton.SignalName.Pressed);
            Check(player.current_world_stamina == before && npc.action_history.Last().action_name == "取消交谈" &&
                !npc.action_history.Last().outcome.Contains("婉拒了这次来访"), "主动作罢错误收费或记成婉拒来访");
            CloseResult(dialogue);

            Call(main, "begin_npc_social_talk", npc, true, false);
            Check(Choice(dialogue, "接受") is not null, "NPC 来访缺少接受选项");
            Choice(dialogue, "婉拒").EmitSignal(BaseButton.SignalName.Pressed);
            Check(player.current_world_stamina == before && npc.action_history.Last().outcome.Contains("婉拒了这次来访"), "来访婉拒方向错误或收费");
            CloseResult(dialogue);
            Call(main, "begin_npc_social_talk", npc, true, false);
            Choice(dialogue, "接受").EmitSignal(BaseButton.SignalName.Pressed);
            Check(player.current_world_stamina == before - 1 && Read<System.Collections.Generic.IReadOnlyList<NpcSocialPresentationText.Line>>(dialogue, "_socialLines").Any(line => line.Rule == "player_accepted"), "接受来访未按原费用结算或方向反馈错误");
            CloseResult(dialogue);

            var entryRank = typeof(MainScene).GetMethod("get_yuxu_entry_rank", BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (var (roots, rank) in new[] { (0, ""), (1, "预备真传"), (2, "内门弟子"), (3, "外门弟子"), (4, "杂役"), (5, "杂役") })
            {
                human.root_weights.Clear();
                for (var index = 0; index < 5; index++) human.root_weights.Add(index < roots ? 1 : 0);
                human.spirit_root_name = roots == 0 ? "无灵根" : roots == 1 ? "天灵根" : $"{roots}灵根";
                var savedRoots = (int)Call(main, "get_player_spirit_root_count")!;
                Check(savedRoots == roots && (string)entryRank.Invoke(null, [savedRoots])! == rank, "入门身份未沿用已有灵根记录");
            }
            world.npcs.Remove(npc);
            GD.Print("NPC_INTERACTION_DIRECTION PASS: formal menu without test entries, player approach/familiar chat/cancellation, NPC accept/decline, one-time cost, entry rank from saved roots");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
