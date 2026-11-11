using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class WorldDungeonNavigationSmoke : Node
{
    private static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
    private static object? Call(object target,string method,params object[] args) => typeof(MainScene).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static void Set(object target,string field,object value) => typeof(MainScene).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static object? PanelCall(object target,string method,params object[] args) => typeof(EastWoodsDungeonPanel).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Read<T>(object target,string field) => (T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private async Task Frames(int n=4) { for(var i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize=new Vector2I(1920,1080);
            var main=new EastWoodsActionHarness(); AddChild(main);
            var layer=new CanvasLayer { Name="SceneLayer" }; main.AddChild(layer);
            main.AddChild(new CanvasLayer { Name="CanvasLayer" });
            var world=new Control { Name="TileWorldMap", Size=new Vector2(1920,1080),MouseFilter=Control.MouseFilterEnum.Ignore }; layer.AddChild(world);
            Set(main,"tile_world_map",world);
            var data=new WorldNodeMapData { use_catalog=false };
            data.nodes.Add(new WorldNodeData { node_id="east_woods",node_name="东林地",node_type=WorldNodeData.NodeType.Dungeon });
            data.nodes.Add(new WorldNodeData { node_id="home_village",node_name="青溪里",node_type=WorldNodeData.NodeType.Home });
            data.nodes.Add(new WorldNodeData { node_id="town_test",node_name="测试城",node_type=WorldNodeData.NodeType.City });
            Set(main,"_world_node_data",data);
            typeof(MainScene).GetField("_story_free_start", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main, true);
            typeof(MainScene).GetField("_baishiLecture", BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main, null); Set(main,"_cloud_state_initialized",true);
            Set(main,"_player_world_node_id","east_woods");
            var roster=GD.Load<PackedScene>("res://tscn/panel/CellCharacterList.tscn").Instantiate<CellCharacterList>();
            var hudLayer=new CanvasLayer { Layer=20 }; main.AddChild(hudLayer); hudLayer.AddChild(roster);
            roster.show_world_npcs([],"world:east_woods");
            roster.GetNode<Button>("ToggleButton").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(!Read<bool>(roster,"_expanded"),"名单未收起");
            for(var i=0;i<3;i++) { Check(!main.try_return_to_previous_page(),"根世界返回错误声明有上层页面"); Check(world.Visible,"世界Esc关闭了根地图"); }
            Call(main,"close_world_map"); Check(world.Visible,"旧返回入口关闭世界");
            Call(main,"open_location_map"); Check(world.Visible,"地点入口关闭了当前地图");

            var info=GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); hudLayer.AddChild(info); Set(main,"info",info);
            info.Show(); main.try_return_to_previous_page(); Check(!info.Visible && world.Visible,"档案Esc关闭了底下的地图");
            var storage=GD.Load<PackedScene>("res://tscn/panel/StoragePanel.tscn").Instantiate<StoragePanel>(); hudLayer.AddChild(storage); Set(main,"storage",storage);
            storage.Show(); main.try_return_to_previous_page(); Check(!storage.Visible && world.Visible,"行囊Esc关闭了底下的地图");

            Call(main,"on_tile_world_node_enter_requested","east_woods"); await Frames();
            var dungeon=main.GetNode<EastWoodsDungeonPanel>("SceneLayer/EastWoodsDungeonPanel");
            Check(dungeon.Visible && !world.Visible && main.GetNodeOrNull("SceneLayer/EastWoodsLocationPanel") is null,"进入副本仍经过中转页或没有隐藏世界");
            Check(!Read<bool>(roster,"_expanded"),"进入或移动场景强制展开名单");
            var viewport=dungeon.GetNode<Control>("MapViewport"); var map=viewport.GetNode<Control>("OrthogonalNodeMap");
            Check(viewport.Position.Length()<1 && viewport.Size.DistanceTo(dungeon.Size)<1,"地图不是全屏显示范围");
            var focus=Read<Vector2>(dungeon,"_cameraFocus");
            // Deliver actual viewport input, not a direct call to the zoom helper.
            var mouse=GetViewport().GetVisibleRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position=mouse,GlobalPosition=mouse },true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex=MouseButton.WheelUp,Pressed=true,Position=mouse,GlobalPosition=mouse },true);
            await Frames();
            if (!Mathf.IsEqualApprox(map.Scale.X,1.12f)) GD.Print($"WHEEL_DIAGNOSTIC: mouse={mouse}, rect={GetViewport().GetVisibleRect()}, hovered={GetViewport().GuiGetHoveredControl()?.GetPath()}, zoom={map.Scale}, viewport={viewport.GetGlobalRect()}, modal={dungeon.GetNode<Control>("NodeActionDetails").Visible}");
            Check(Mathf.IsEqualApprox(map.Scale.X,1.12f),"真实鼠标滚轮没有缩放");
            Check(Read<Vector2>(dungeon,"_cameraFocus").DistanceTo(focus)<.01f,"滚轮不是视野中心锚点");
            info.Show(); main.try_return_to_previous_page(); Check(!info.Visible && dungeon.Visible && !dungeon.GetNode<Control>("NodeActionDetails").Visible,"副本上层档案Esc误打开暂歇");

            dungeon._UnhandledKeyInput(new InputEventKey { Keycode=Key.D,Pressed=true });
            var last=Read<Vector2>(dungeon,"_cameraFocus"); var maxJump=0f;
            for(var i=0;i<24;i++)
            {
                await ToSignal(GetTree().CreateTimer(.02),SceneTreeTimer.SignalName.Timeout);
                var current=Read<Vector2>(dungeon,"_cameraFocus"); maxJump=Mathf.Max(maxJump,current.DistanceTo(last)); last=current;
            }
            Check(maxJump<75,"行走镜头仍有大幅跳变");
            Check(!Read<bool>(roster,"_expanded"),"移动刷新名单强制展开");
            dungeon._UnhandledKeyInput(new InputEventKey { Keycode=Key.A,Pressed=true }); await Frames();
            var pawn=dungeon.GetNode<Control>("MapViewport/OrthogonalNodeMap/TravelPawn");
            Check(pawn.GetChildren().OfType<TextureRect>().Single().FlipH,"向左行走没有翻转棋子");
            await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
            Check(map.GetNode<Control>("Node_forest_edge/Pawns/PlayerPawn").GetChildren().OfType<TextureRect>().Single().FlipH,"落格后朝向丢失");
            dungeon._UnhandledKeyInput(new InputEventKey { Keycode=Key.D,Pressed=true }); await Frames();
            Check(!dungeon.GetNode<Control>("MapViewport/OrthogonalNodeMap/TravelPawn").GetChildren().OfType<TextureRect>().Single().FlipH,"向右行走没有恢复朝向");
            await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
            var manager=GetNode<NpcWorldManager>("/root/NpcWorldManager");
            var npc=new WorldNpcData { npc_id="navigation-facing-test",npc_name="朝向测试",is_alive=true,
                dungeon_id=EastWoodsDungeonRules.DungeonId,dungeon_node_id="leaf_creek",dun_speed=100 };
            manager.npcs.Add(npc); PanelCall(dungeon,"refresh");
            PanelCall(dungeon,"queue_npc_step",npc,"leaf_creek","forest_edge"); npc.dungeon_node_id="forest_edge";
            PanelCall(dungeon,"play_npc_steps"); await Frames();
            Check(map.GetChildren().OfType<Control>().Single(n=>n.Name=="NpcTravelPawn").GetChildren().OfType<TextureRect>().Single().FlipH,"NPC向左没有翻转");
            await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
            var npcToken=map.GetNode<Control>("Node_forest_edge/Pawns").GetChildren().OfType<Control>().Single(n=>n.HasMeta("npc_id") && n.GetMeta("npc_id").AsString()==npc.npc_id);
            Check(npcToken.GetChildren().OfType<TextureRect>().Single().FlipH,"NPC落格后朝向丢失");
            manager.npcs.Remove(npc); PanelCall(dungeon,"refresh");
            main.try_return_to_previous_page(); Check(dungeon.Visible && !world.Visible && dungeon.GetNode<Control>("NodeActionDetails").Visible,"副本Esc绕过实体出口");
            main.try_return_to_previous_page();
            dungeon._UnhandledKeyInput(new InputEventKey { Keycode=Key.A,Pressed=true }); await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
            dungeon.close(); Check(!dungeon.Visible && world.Visible,"实体出口未直接回到新版世界");
            Check(!Read<bool>(roster,"_expanded"),"离场刷新强制展开名单");
            main.try_return_to_previous_page(); Check(world.Visible,"副本离场后Esc误退旧场景");
            Call(main,"on_tile_world_node_enter_requested","east_woods"); await Frames();
            Check(dungeon.Visible && dungeon.get_current_route_node_id()=="forest_edge","重进副本错误"); dungeon.close();

            var interior=new WorldLocationInteriorPanel(); layer.AddChild(interior); Set(main,"location_interior",interior);
            Set(main,"_player_world_node_id","town_test"); Call(main,"on_tile_world_node_enter_requested","town_test");
            Check(interior.Visible && !world.Visible,"普通地点进入错误");
            main.try_return_to_previous_page(); Check(!interior.Visible && world.Visible,"普通地点Esc未回世界");
            Set(main,"_player_world_node_id","home_village"); Call(main,"on_tile_world_node_enter_requested","home_village");
            Check(world.Visible && !interior.Visible,"拒绝进入青溪里后把地图隐藏了");
            GD.Print($"WORLD_DUNGEON_NAVIGATION_PASS: root Esc, overlays, direct entry/exit, rejected entry, fullscreen wheel, collapsed roster, facing and smooth camera (max step {maxJump:0.0})");
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
