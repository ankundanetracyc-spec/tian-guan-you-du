using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Full real-player fixture: inventory, map controls, saved hover and landing.</summary>
public partial class PlayerSpiritBoatFlowSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _checks;
    private void Check(bool ok, string message) { _checks++; if (!ok) throw new InvalidOperationException(message); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden)!.Invoke(owner, args);
    private async Task Frames(int n = 3) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("spirit_boat_player", StringComparison.OrdinalIgnoreCase), "需要隔离存档目录");
            await Frames();
            GetTree().CurrentScene = null;
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            saves.begin_new_game("驾舟验收");
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            GetTree().Root.AddChild(main); GetTree().CurrentScene = main;
            for (var i = 0; i < 300 && !main.StartupComplete; i++) await Frames(1);
            Check(main.StartupComplete && main.StartupError is null, "真实场景没有启动");
            typeof(MainScene).GetField("_story_free_start", Hidden)!.SetValue(main, true);
            typeof(MainScene).GetField("_baishiLecture", Hidden)!.SetValue(main, null);
            if (Field<BaishiLectureProgress?>(main, "_baishiLecture") is { } opening) opening.departed_qingxi = true;
            Field<StoryDialoguePanel>(main, "story_dialogue").Hide();
            var actor = Field<Character>(main, "player");
            var boat = ForgedArtifactInventory.Create(new ProcessForgingTrial.Artifact {
                InstanceId = "player-boat-fixture", Name = "灵舟夹具", Category = "舟", Tier = "法器", Shape = "灵舟",
                InventoryIconPath = "res://textures/items/inventory_density_20261029/spirit_boat_icon_20261106.png", InventoryWidth = 2, InventoryHeight = 6
            });
            boat.travel_data = GD.Load<ItemTravelData>("res://resources/world/bai_chuan/yuxu_escort_boat_travel_20261106.tres");
            Check(actor.add_artifact_to_inventory(boat) && actor.add_item_to_inventory("res://resources/entities/items/灵石.tres", 3), "隔离夹具入包失败");
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                var storage = Field<StoragePanel>(main, "storage");
                storage.show_panel(); await Frames(8);
                var entry = actor.get_spatial_inventory().Single(e => e.ArtifactId == boat.instance_id);
                var slots = Field<System.Collections.Generic.Dictionary<string, StorageItemSlot>>(storage, "_nativeSlots");
                if (slots.TryGetValue(entry.InstanceId, out var itemSlot))
                    HoverInfoManager.SetInfo(itemSlot, EquipmentItemPresentation.Hover(boat) with { Footprint = entry.Size }, true);
                await Frames(4); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://tmp/player_spirit_boat_inventory_20261106.png");
                storage.hide_panel();
            }
            Call(main, "open_world"); await Frames(8);
            var map = Field<Control>(main, "_nodeWorldMap");
            Check(map.Call("set_initial_runtime_id", "white_stone_town").AsBool(), "白石地图起点不可用");
            var button = map.Get("boat_button").AsGodotObject() as Button;
            Check(button is { Visible: true, Disabled: false }, "持有灵舟时没有驾舟入口");
            button!.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(boat.travel_in_use && map.Get("movement_mode").AsString() == "player_flight", "实际按钮没有登上玩家的器物");
            var visual = map.Get("flight_boat").AsGodotObject();
            Check(visual is not null && visual.Get("passenger_ids").AsStringArray().Length == 0, "单人驾舟时出现不存在的驾驶者");
            var npcs = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            var companion = new WorldNpcData { npc_id = "player_boat_companion_fixture", npc_name = "同行夹具", is_alive = true };
            Check(npcs.join_player_travel_party(companion), "实际同行者无法加入");
            Call(main, "sync_player_travel_party_map"); await Frames(2);
            Check(visual!.Get("passenger_ids").AsStringArray().Contains(companion.npc_id), "真实同行者没有出现在舟上");
            for (var i = 0; i < 4; i++)
                Check(npcs.join_player_travel_party(new WorldNpcData { npc_id = $"player_boat_capacity_{i}", npc_name = $"同行者{i}", is_alive = true }), "未满载时同行者无法登舟");
            var seventh = new WorldNpcData { npc_id = "player_boat_over_capacity", npc_name = "超载夹具", is_alive = true };
            Check(!npcs.join_player_travel_party(seventh) && npcs.get_npc(seventh.npc_id) is null, "超过六人时不能再加入同行");
            Call(main, "sync_player_travel_party_map");
            Check(visual.Get("passenger_ids").AsStringArray().Length == 5, "满载时舟上未显示全部五名同行者");
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            time.restore_calendar(1, 1, 1, 0, 0);
            time.is_paused = true;
            Check(map.Call("travel_to_runtime_location", "yuxu_xianzong_core").AsBool(), "无法选择玉虚落点");
            Check(actor.get_inventory_total_count("res://resources/entities/items/灵石.tres") == 2 && Math.Abs(boat.travel_charge_hours - 24) < .001,
                "航程预充没有真实扣除一枚灵石");
            var nav = map.Get("navigation").AsGodotObject();
            var reachedBlocked = false;
            for (var i = 0; i < 2000 && map.Call("is_traveling").AsBool(); i++)
            {
                map.Call("_advance_flight", .1f);
                var p = map.Call("get_world_position").AsVector2();
                if (!nav.Call("is_walkable", p).AsBool()) { reachedBlocked = true; break; }
            }
            Check(reachedBlocked && boat.travel_charge_hours < 24 && map.Get("flight_height").AsDouble() > 0, "未在山河上空按实际时间消耗续航");
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                map.Call("_update_flight_visual"); map.Call("_update_status"); map.Call("focus_player_node"); await Frames(4);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://tmp/player_spirit_boat_flight_20261106.png");
            }
            var point = map.Call("get_world_position").AsVector2();
            var charge = boat.travel_charge_hours;
            map.Call("stop_free_travel");
            Check(!map.Call("is_traveling").AsBool() && map.Get("movement_mode").AsString() == "player_flight" && Math.Abs(boat.travel_charge_hours - charge) < .001,
                "空中停航没有保留续航和悬停");
            Check(!map.Call("land_player_boat").AsBool() && boat.travel_in_use, "不可通行地面不能降落或移除灵舟");
            Check(!actor.remove_artifact_from_inventory(boat.instance_id), "空中不得丢弃使用中的舟");
            var airSave = main.capture_battle_world_snapshot()!;
            airSave.node_world_state["flight_speed"] = 12000.0; // older save of this exact shared profile
            Check(saves.save_snapshot(airSave) && saves.load_slot(saves.active_slot_id), "悬停存档无法写入或读取");
            main.prepare_return_to_title_from_menu(false); main.QueueFree(); await Frames(8);
            main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            GetTree().Root.AddChild(main); GetTree().CurrentScene = main;
            for (var i = 0; i < 300 && !main.StartupComplete; i++) await Frames(1);
            await Frames(10);
            Check(main.StartupComplete && main.StartupError is null, "悬停存档重建失败");
            actor = Field<Character>(main, "player");
            boat = actor.get_owned_spirit_boat("player-boat-fixture")!;
            map = Field<Control>(main, "_nodeWorldMap");
            Check(boat is { travel_in_use: true } && Math.Abs(boat.travel_charge_hours - charge) < .001 &&
                  map.Get("movement_mode").AsString() == "player_flight" && map.Call("get_world_position").AsVector2().DistanceTo(point) < .01,
                "重载没有恢复原舟、续航与悬停坐标");
            Check(Math.Abs(map.Get("flight_speed").AsDouble() - 4800.0) < .01, "引用相同舟配置的旧个人舟速没有迁移");
            Call(main, "sync_player_travel_party_map");
            visual = map.Get("flight_boat").AsGodotObject();
            Check(npcs.get_npc(companion.npc_id)?.is_player_travel_companion == true &&
                  visual!.Get("passenger_ids").AsStringArray().Contains(companion.npc_id), "重载没有恢复舟上同行者");
            Check(visual.Get("passenger_ids").AsStringArray().Length == 5 && !npcs.join_player_travel_party(seventh),
                "读档后满载人数或加入限制丢失");
            Check(actor.get_inventory_total_count("res://resources/entities/items/灵石.tres") == 2, "读档重复扣除灵石");
            Check(map.Call("travel_to_runtime_location", "yuxu_xianzong_core").AsBool(), "悬停后不能续航至合法目的地");
            Check(actor.get_inventory_total_count("res://resources/entities/items/灵石.tres") == 2, "已有续航足够时重复扣石");
            time.is_paused = false;
            for (var i = 0; i < 5000 && map.Call("is_traveling").AsBool(); i++) map.Call("_advance_flight", .1f);
            Check(!map.Call("is_traveling").AsBool() && map.Get("movement_mode").AsString() == "free" && !boat.travel_in_use,
                "到达可通行落点后没有收舟落地");
            Check(map.Call("get_current_location_runtime_id").AsString() == "yuxu_xianzong_core" && boat.travel_charge_hours > 0,
                "落地位置或剩余续航错误");
            time.is_paused = true;
            Call(main, "on_player_boat_requested", boat.instance_id);
            Check(boat.travel_in_use && map.Get("movement_mode").AsString() == "player_flight", "落地后无法再次驾舟");
            var origin = map.Call("get_world_position").AsVector2();
            nav = map.Get("navigation").AsGodotObject();
            Vector2? freePoint = null;
            foreach (var offset in new[] { new Vector2(80, 0), new Vector2(-80, 0), new Vector2(0, 80), new Vector2(0, -80) })
                if (nav.Call("is_walkable", origin + offset).AsBool()) { freePoint = origin + offset; break; }
            Check(freePoint.HasValue && map.Call("navigate_to_world", freePoint.Value, "").AsBool(), "无法选择非地点的可通行地面");
            for (var i = 0; i < 1000 && map.Call("is_traveling").AsBool(); i++) map.Call("_advance_flight", .1f);
            Check(map.Get("movement_mode").AsString() == "free" && map.Call("get_world_position").AsVector2().DistanceTo(freePoint!.Value) < .05 &&
                  actor.get_inventory_total_count("res://resources/entities/items/灵石.tres") == 2, "自由落点或剩余续航结算错误");
            Check(actor.remove_artifact_from_inventory(boat.instance_id), "落地后器物仍被错误锁定");
            main.prepare_return_to_title_from_menu(false); main.QueueFree(); await Frames(5);
            GD.Print($"PLAYER_SPIRIT_BOAT_FLOW_SMOKE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("PLAYER_SPIRIT_BOAT_FLOW_SMOKE_FAILED " + (error.InnerException ?? error)); GetTree().Quit(1); }
    }
}
