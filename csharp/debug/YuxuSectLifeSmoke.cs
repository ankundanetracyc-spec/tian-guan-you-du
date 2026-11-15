using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Checks Heguang peer Q&A and taskless Yaopu cultivation using the real action callbacks.</summary>
public partial class YuxuSectLifeSmoke : Node
{
    private const string FlowerPath = "res://resources/entities/items/雾铃花.tres";
    private const string CultivationDutyId = "yaopu_cultivate_mist_bell";

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    private static object? Call(MainScene main, string method, params object[] args)
        => typeof(MainScene).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, args);

    private static void Set(MainScene main, string field, object value)
        => typeof(MainScene).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, value);

    private async Task Frames()
    {
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void ChooseSocialChoice(StoryDialoguePanel dialogue, int choiceIndex = 0)
    {
        var choices = dialogue.FindChild("SocialChoices", true, false) as VBoxContainer;
        Check(choices is not null && choices.GetChildCount() > choiceIndex, "没有出现同门答疑选项");
        (choices!.GetChild(choiceIndex) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static Button? GetActionButton(Control peakInterior, string actionId)
    {
        var buttons = peakInterior.Get("_action_buttons").AsGodotDictionary();
        return buttons.GetValueOrDefault(actionId).AsGodotObject() as Button;
    }

    private static PersistentGameSave Snapshot(HumanData human, WorldNpcData peer, YuxuSectTreasuryLedger treasury, GameTimeMgr time)
        => new()
        {
            player_data = human,
            world_npcs = new Godot.Collections.Array<WorldNpcData> { peer },
            yuxu_sect_treasury = treasury,
            year = time.year,
            month = time.month,
            day = time.day,
            hour = time.hour,
        };

    public override async void _Ready()
    {
        var savePath = "user://yuxu_sect_life_smoke_" + Guid.NewGuid().ToString("N") + ".tres";
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1280, 720);
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            time.restore_calendar(1, 1, 1, 0, 0f);

            var main = new EastWoodsActionHarness();
            AddChild(main);
            var layer = new CanvasLayer { Name = "SceneLayer" };
            main.AddChild(layer);
            var overview = GD.Load<PackedScene>("res://tscn/world/bai_chuan/YuxuXianzongOverview.tscn").Instantiate<Control>();
            overview.Name = "YuxuXianzongOverview";
            layer.AddChild(overview);
            var peakInterior = overview.GetNode<Control>("PeakInterior");

            var human = new HumanData
            {
                entity_name = "峰内活动验收",
                sect_affiliation_id = "yuxu_xianzong",
                sect_current_rank = "外门弟子",
                current_world_stamina = 100,
                max_world_stamina = 100,
            };
            var player = new Character { entity_data = human };
            main.AddChild(player);
            Set(main, "player", player);
            Set(main, "_player_world_node_id", "yuxu_xianzong_core");

            var world = new NpcWorldManager();
            main.AddChild(world);
            var peer = new WorldNpcData
            {
                npc_id = "yuxu_life_smoke_peer",
                npc_name = "同门验收",
                faction_id = "yuxu_xianzong",
                formal_identity = "外门弟子",
                current_node_id = "yuxu_xianzong_core",
                current_subscene_id = "heguang",
                player_relationship = 0,
            };
            world.npcs.Add(peer);
            Set(main, "_npc_world", world);

            var dialogue = GD.Load<PackedScene>("res://tscn/panel/StoryDialoguePanel.tscn").Instantiate<StoryDialoguePanel>();
            main.AddChild(dialogue);
            Set(main, "story_dialogue", dialogue);

            var treasury = new YuxuSectTreasuryLedger();
            Set(main, "_yuxu_sect_treasury_ledger", treasury);

            void EnterPeak(string peakId)
            {
                overview.Visible = true;
                overview.Call("_on_peak_pressed", peakId);
                Set(main, "_player_current_yuxu_peak_id", peakId);
                Call(main, "refresh_yuxu_life_context");
            }

            EnterPeak("heguang");
            var qaButton = GetActionButton(peakInterior, "heguang_peer_qa");
            Check(qaButton is not null && qaButton.Visible, "和光峰没有显示同门答疑入口");
            var beforeHours = time.get_total_game_hours();
            var beforeStamina = player.current_world_stamina;
            var beforeFlowers = player.get_inventory_total_count(FlowerPath);
            var beforeInspiration = human.cultivation_inspiration_records.Count;
            var beforeMemoryCount = peer.player_social_memory.Count;
            Call(main, "on_yuxu_life_action_requested", "heguang_peer_qa", "heguang");
            ChooseSocialChoice(dialogue, 1);
            Check(player.current_world_stamina == beforeStamina && peer.action_history.Count == 0,
                "取消答疑仍消耗精力或留下完成记录");
            await Frames();
            Call(main, "on_yuxu_life_action_requested", "heguang_peer_qa", "heguang");
            ChooseSocialChoice(dialogue);
            await Frames();
            Call(main, "on_yuxu_life_action_requested", "heguang_peer_qa", "heguang");
            ChooseSocialChoice(dialogue);
            Check(time.get_total_game_hours() == beforeHours, "同门答疑推进了游戏时间");
            Check(player.current_world_stamina == beforeStamina - WorldStaminaRules.ConversationCost * 2, "同门答疑精力费用错误或没有允许同日重复");
            Check(peer.action_history.Count(record => record.action_id == "heguang_peer_qa") == 2, "同日两次答疑没有各自留下活动记录");
            Check(peer.player_relationship == 0 && peer.player_social_memory.Count == beforeMemoryCount, "答疑改变了同门关系或交往记忆");
            Check(player.get_inventory_total_count(FlowerPath) == beforeFlowers && human.get_yuxu_contribution_total() == 0 &&
                human.cultivation_inspiration_records.Count == beforeInspiration && treasury.records.Count == 0,
                "答疑意外结算物品、贡献、灵感或公库流水");

            EnterPeak("yaopu");
            var flowerButton = GetActionButton(peakInterior, "yaopu_cultivate");
            Check(flowerButton is not null && flowerButton.Visible, "瑶圃峰没有显示培护入口");
            var startHour = time.get_total_game_hours();
            var staminaBeforeCare = player.current_world_stamina;
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            var careHours = YuxuSectPrototypeBalance.YaopuCultivationStartHours;
            var careCost = YuxuSectPrototypeBalance.YaopuCultivationStartStaminaCost;
            var waitHours = YuxuSectPrototypeBalance.YaopuCultivationHours;
            var yieldCount = YuxuSectPrototypeBalance.YaopuCultivationYieldCount;
            Check(time.get_total_game_hours() == startHour + careHours && player.current_world_stamina == staminaBeforeCare - careCost,
                "无任务培护没有按现有公事原型值推进时间与扣除精力");
            Check(human.sect_yaopu_free_cultivation_stage == 1 && human.sect_yaopu_free_cultivation_started_hour == time.get_total_game_hours() &&
                player.get_inventory_total_count(FlowerPath) == beforeFlowers, "照料阶段没有独立保存，或提前获得花株");

            var snapshot = Snapshot(human, peer, treasury, time);
            Check(ResourceSaver.Save(snapshot, savePath) == Error.Ok, "峰内活动状态无法写入存档快照");
            var loaded = ResourceLoader.Load<PersistentGameSave>(savePath, "", ResourceLoader.CacheMode.Ignore);
            Check(loaded?.player_data is not null && loaded.world_npcs.Count == 1, "峰内活动存档快照无法重载");
            var restoredHuman = loaded!.player_data!;
            var restoredPlayer = new Character { entity_data = restoredHuman };
            Check(restoredHuman.sect_yaopu_free_cultivation_stage == 1 &&
                restoredHuman.sect_yaopu_free_cultivation_started_hour == time.get_total_game_hours() &&
                restoredHuman.get_yuxu_contribution_total() == 0 &&
                restoredPlayer.get_inventory_total_count(FlowerPath) == beforeFlowers,
                "无任务培育阶段、物品或贡献状态没有持久化");
            restoredPlayer.Free();
            var restoredPeer = loaded.world_npcs[0];
            Check(restoredPeer.action_history.Count(record => record.action_id == "heguang_peer_qa") == 2 &&
                restoredPeer.player_relationship == 0 && restoredPeer.player_social_memory.Count == beforeMemoryCount,
                "同门答疑记录或无关系副作用状态没有持久化");
            player.entity_data = restoredHuman;
            human = restoredHuman;

            time.add_hours(waitHours - 1);
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            Check(player.get_inventory_total_count(FlowerPath) == beforeFlowers && human.sect_yaopu_free_cultivation_stage == 1,
                "未满一个游戏昼夜就采收了雾铃花");
            time.add_hours(1);
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            Check(player.get_inventory_total_count(FlowerPath) == beforeFlowers + yieldCount && human.sect_yaopu_free_cultivation_stage == 2,
                "成熟后没有将四株雾铃花交给玩家并保存采收状态");
            Check(human.get_yuxu_contribution_total() == 0 && treasury.records.Count == 0 &&
                human.cultivation_inspiration_records.Count == beforeInspiration,
                "无任务采收错误地结算了公事奖励或公库流水");

            Check(ResourceSaver.Save(Snapshot(human, peer, treasury, time), savePath) == Error.Ok,
                "雾铃花采收后的状态无法保存");
            var harvestedSave = ResourceLoader.Load<PersistentGameSave>(savePath, "", ResourceLoader.CacheMode.Ignore);
            Check(harvestedSave?.player_data is not null && harvestedSave.player_data.sect_yaopu_free_cultivation_stage == 2 &&
                harvestedSave.player_data.get_yuxu_contribution_total() == 0,
                "采收后的无任务状态没有持久化");
            player.entity_data = harvestedSave!.player_data;
            human = harvestedSave.player_data!;

            time.add_hours(waitHours);
            Check(player.get_inventory_total_count(FlowerPath) == beforeFlowers + yieldCount,
                "没有再次主动培护却自动补种或发放了雾铃花");
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            Check(human.sect_yaopu_free_cultivation_stage == 1 &&
                player.get_inventory_total_count(FlowerPath) == beforeFlowers + yieldCount,
                "再次培护没有经过玩家主动开始并独立等待");

            EnterPeak("heguang");
            Call(main, "handle_yuxu_heguang_action", "heguang_accept_duty:" + CultivationDutyId, human);
            Check(human.sect_active_yuxu_duty_id == "" && human.sect_yaopu_free_cultivation_stage == 1,
                "无任务幼苗仍在生长时允许接取公事，可能重复结算同一株");
            time.add_hours(waitHours);
            EnterPeak("yaopu");
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            Check(player.get_inventory_total_count(FlowerPath) == beforeFlowers + yieldCount * 2 &&
                human.sect_yaopu_free_cultivation_stage == 2, "第二次主动培护与采收没有形成独立一轮");

            EnterPeak("heguang");
            Call(main, "handle_yuxu_heguang_action", "heguang_accept_duty:" + CultivationDutyId, human);
            Check(human.sect_active_yuxu_duty_id == CultivationDutyId, "自养幼苗采收后无法正常接取独立培育公事");
            EnterPeak("yaopu");
            var officialStartHour = time.get_total_game_hours();
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            Check(human.sect_active_yuxu_duty_id == CultivationDutyId && human.sect_active_yuxu_duty_stage == 1 &&
                time.get_total_game_hours() == officialStartHour + careHours && human.get_yuxu_contribution_total() == 0,
                "正式公事没有启动自己的独立培护阶段");
            var officialFlowerCount = player.get_inventory_total_count(FlowerPath);
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            Check(player.get_inventory_total_count(FlowerPath) == officialFlowerCount && human.get_yuxu_contribution_total() == 0,
                "正式公事借用了旧的自养花株，或尚未等待就结算");
            time.add_hours(waitHours);
            Call(main, "on_yuxu_life_action_requested", "yaopu_cultivate", "yaopu");
            Check(human.sect_active_yuxu_duty_id == "" && human.get_yuxu_contribution_total() == YuxuSectPrototypeBalance.OfficialYaopuCultivationReward &&
                treasury.get_item_balance(FlowerPath) == YuxuSectPrototypeBalance.YaopuCultivationSectDeliveryCount,
                "正式培育公事没有沿原结算路径完成贡献与公库入账");

            main.QueueFree();
            await Frames();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GD.Print("YUXU_SECT_LIFE PASS: same-day Heguang Q&A fee/log/no-reward; taskless Yaopu care, wait, harvest, save/load, no auto-restock; official-duty isolation and unchanged settlement");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            if (Godot.FileAccess.FileExists(savePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
        }
    }
}
