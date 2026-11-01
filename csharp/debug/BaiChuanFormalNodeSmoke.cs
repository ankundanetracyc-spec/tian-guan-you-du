using Godot;
using Godot.Collections;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

public partial class BaiChuanFormalNodeSmoke : Node
{
    private static object? Invoke(object owner, string name, params object[] args) =>
        typeof(MainScene).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner,args);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 1) { for (var i=0; i<count; i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var deadline = Time.GetTicksMsec()+90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec()<deadline) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "真实旅程启动失败："+main.StartupError);
            Check(!ResourceLoader.HasCached("res://resources/world/yunqu_state_nodes.tres") &&
                !ResourceLoader.HasCached("res://textures/world_map/bai_chuan/bai_chuan_master_v9_hd.png"), "正式启动仍加载旧地图资源");
            var stage = typeof(MainScene).GetField("_qingxi_prologue",BindingFlags.Instance|BindingFlags.NonPublic)!;
            stage.SetValue(main,Enum.Parse(stage.FieldType,"Departed"));
            Invoke(main,"open_world");
            await Frames(5);
            var map = main.GetNodeOrNull<Control>("MapLayer/BaiChuanNodeWorld");
            Check(map is not null && map.Visible,"正式世界入口未显示节点地图");
            var world = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            Check(world.npcs.Any(n => n.origin_node_id == "bc_place_014"), "新地图城池未生成人口");
            Check(!world.npcs.Any(n => n.origin_node_id == "guanshan_prefecture"), "旧图废案仍生成人口");
            var catalogue = GD.Load<WorldNodeMapData>("res://resources/world/bai_chuan/bai_chuan_locations_20261101.tres");
            var residents = world.npcs.Where(n => n.npc_id.StartsWith("runtime_npc_",StringComparison.Ordinal)).ToArray();
            Check(residents.Length > 0 && residents.Select(n => n.origin_node_id).Distinct().Count() > 20 &&
                residents.All(n => catalogue.get_node(n.origin_node_id) is not null), "新生人口存在旧地点或分布过少");
            var old = main.GetNodeOrNull<Control>("MapLayer/TileWorldMapPresentation");
            Check(old is null,"正式游戏仍创建旧方格地图");
            Check(map!.Call("get_current_node").AsString()=="bc_place_021","新档起点错误，应为白石镇");
            var neighbours = map.Call("get_current_neighbours").AsGodotArray<string>();
            Check(neighbours.Count > 0, "新档节点没有相邻路线");
            var next = neighbours[0];
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var before = time.get_precise_game_hours();
            map.Call("_select_transit",next);
            var finished = ToSignal(map,"node_travel_finished");
            map.Call("_travel_selected");
            await finished;
            Check(map.Call("get_current_node").AsString()==next && time.get_precise_game_hours()>before,"正式节点行走或时间推进失败");
            var save = main.capture_battle_world_snapshot();
            Check(save is not null && save.node_world_state.GetValueOrDefault("current_node", "").AsString()==next,"正式快照没有保存节点位置");
            const string savePath = "user://bai_chuan_node_smoke.tres";
            Check(ResourceSaver.Save(save!,savePath)==Error.Ok,"节点新档写入失败");
            var loaded = ResourceLoader.Load<PersistentGameSave>(savePath,"",ResourceLoader.CacheMode.Ignore);
            Check(loaded is not null && loaded.node_world_state.GetValueOrDefault("current_node", "").AsString()==next,"节点新档读取失败");
            Check(map.Call("restore_node_state",loaded!.node_world_state).AsBool() && map.Call("get_current_node").AsString()==next,"节点新档恢复位置失败");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            var state = new Dictionary { ["current_node"]="bc_place_014", ["remaining_route"]=new Array<string>() };
            Check(map.Call("restore_node_state",state).AsBool(),"新城节点恢复失败");
            Invoke(main,"on_node_world_arrived","bc_place_014");
            map.Set("selected_node","bc_place_014");
            map.Call("_enter_current");
            await Frames(5);
            Check(main.FindChild("WorldLocationInteriorPanel",true,false) is Control { Visible: true },"新城没有进入通用地点界面");
            Invoke(main,"close_world_location");
            await Frames(5);
            map = main.GetNodeOrNull<Control>("MapLayer/BaiChuanNodeWorld") ?? throw new InvalidOperationException("从新城返回节点地图失败");
            Check(map.Visible,"从新城返回节点地图失败");
            state = new Dictionary { ["current_node"]="bc_place_070", ["remaining_route"]=new Array<string>() };
            Check(map.Call("restore_node_state",state).AsBool(),"节点新档恢复失败");
            Invoke(main,"on_node_world_arrived","bc_place_070");
            map.Set("selected_node","bc_place_070");
            map.Call("_enter_current");
            await Frames(5);
            var dungeon = main.GetNodeOrNull<Control>("SceneLayer/EastWoodsDungeonPanel");
            Check(dungeon is not null && dungeon.Visible,"节点地图未进入正式东林地");
            GD.Print("BAICHUAN_FORMAL_NODE_SMOKE_OK");
            main.QueueFree();
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("BAICHUAN_FORMAL_NODE_SMOKE_FAILED: "+ex); GetTree().Quit(1); }
    }
}
