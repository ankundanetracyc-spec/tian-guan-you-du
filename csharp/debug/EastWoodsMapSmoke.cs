using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>独立东林地地图验收，不修改正式角色或旅程存档。</summary>
public partial class EastWoodsMapSmoke : Node
{
    private EastWoodsDungeonPanel _panel = null!;
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 4)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        await Frames();
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        var folder = "res://docs/美术规范/东林地节点地图_20261011/实机验收/外围128格";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(folder + "/" + name + ".png");
    }
    public override async void _Ready()
    {
        try
        {
            var compact = OS.GetCmdlineUserArgs().Contains("--compact");
            GetWindow().ContentScaleSize = compact ? new Vector2I(1280, 720) : new Vector2I(1920, 1080);
            var npcManager = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            EastWoodsDungeonRules.use_legacy_layout();
            var slowPoint = EastWoodsActions.ResourcePoints().Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth);
            var mineralPoint = EastWoodsActions.ResourcePoints().Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral);
            for (var i = 0; i < 12; i++)
                npcManager.npcs.Add(new WorldNpcData
                {
                    npc_id = "map_smoke_" + i, npc_name = "测试同修" + i,
                    dungeon_id = EastWoodsDungeonRules.DungeonId,
                    dungeon_node_id = i < 7 ? "forest_edge" : "deep_meet", is_alive = true,
                });
            _panel = new EastWoodsDungeonPanel(); AddChild(_panel); _panel.show_dungeon();
            await Frames();
            var map = _panel.GetNode<Control>("MapViewport/OrthogonalNodeMap");
            Check(_panel.find_action(slowPoint.Node, slowPoint.Action.Id) is { Recovery: EastWoodsActions.ResourceRecoveryGroup.SlowGrowth } &&
                  EastWoodsDungeonRules.get_node(slowPoint.Node) is { Terrain: 0 or 1 } &&
                  EastWoodsDungeonRules.get_neighbors(slowPoint.Node).Select(EastWoodsDungeonRules.get_node).Any(neighbor => neighbor?.Terrain == 5),
                "30日慢生资源没有接入实际湿润林缘地格与采集入口。");
            Check(_panel.find_action(mineralPoint.Node, mineralPoint.Action.Id) is { Recovery: EastWoodsActions.ResourceRecoveryGroup.Mineral } &&
                  EastWoodsDungeonRules.get_node(mineralPoint.Node) is { Terrain: 2 or 7, ResourceCategory: EastWoodsResourceCategory.Mineral, ResourceNodeId: "spirit_stone_vein" } &&
                  EastWoodsDungeonRules.get_node(mineralPoint.Node)?.Feature != "spring",
                "90日矿产采集没有接入显式专用矿脉节点。");
            Check(_panel.find_action(slowPoint.Node, mineralPoint.Action.Id) is null &&
                  _panel.find_action(mineralPoint.Node, slowPoint.Action.Id) is null,
                "错误资源类别仍可打开不匹配的采集行动。");
            foreach (var point in EastWoodsActions.ResourcePoints())
                Check(map.GetNodeOrNull<PanelContainer>($"Node_{point.Node}/ResourceNodeMarker") is not null,
                    $"资源地格 {point.Node} 缺少可见类别标识。");
            var slowMarker = map.GetNode<PanelContainer>($"Node_{slowPoint.Node}/ResourceNodeMarker");
            var mineralMarker = map.GetNode<PanelContainer>($"Node_{mineralPoint.Node}/ResourceNodeMarker");
            var slowCategory = slowMarker.FindChild("Category", true, false) as Label;
            var mineralCategory = mineralMarker.FindChild("Category", true, false) as Label;
            Check(slowCategory?.Text == "植" && slowMarker.TooltipText.Contains("植物") &&
                  mineralCategory?.Text == "矿" && mineralMarker.TooltipText.Contains("矿产"),
                "地图资源节点标识没有显示匹配类别。");
            foreach (var node in EastWoodsDungeonRules.get_nodes().Where(node =>
                         (node.Terrain is 2 or 7) && node.ResourceCategory != EastWoodsResourceCategory.Mineral))
            {
                var marker = map.GetNodeOrNull<PanelContainer>($"Node_{node.Id}/ResourceNodeMarker");
                if (marker is not null)
                {
                    var markerCategory = marker.FindChild("Category", true, false) as Label;
                    Check(markerCategory?.Text != "矿" && !marker.TooltipText.Contains("矿产"),
                        "普通岩格显示了矿产资源点标识。");
                }
            }
            Check(_panel.is_node_visible("deep_meet") && !_panel.is_node_visible("mid_split"), "四格视野边界错误");
            Check(!map.GetNode<Button>("Node_quiet_grove").Visible, "未知地块泄漏");
            var fogState = _panel.get_route_state();
            fogState["current"] = "quiet_grove"; _panel.restore_route_state(fogState);
            Check(_panel.is_node_discovered("forest_edge") && !_panel.is_node_visible("forest_edge"), "旧地形认知未保留");
            Check(map.GetNode<Control>("Node_forest_edge/Pawns").GetChildCount() == 0, "视野外NPC泄漏");
            var remembered = Json.ParseString(Json.Stringify(_panel.get_route_state())).AsGodotDictionary();
            remembered["current"] = "forest_edge"; _panel.restore_route_state(remembered);
            Check(_panel.is_node_discovered("quiet_grove") && !_panel.is_node_visible("quiet_grove"), "迷雾存读档失败");
            Check(map.GetNode<Button>("Node_quiet_grove").Modulate.R < .6f, "已知非可见地形未变暗");
            GD.Print("DUNGEON_FOG PASS: four-cell boundary, hidden unknown, remembered dim terrain, hidden NPCs, save restore");
            var viewport = _panel.GetNode<Control>("MapViewport");
            void Camera(InputEvent e) => typeof(EastWoodsDungeonPanel).GetMethod("handle_map_camera_input", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_panel, new object[]{e});
            Vector2 Centre() => map.GetGlobalTransform().AffineInverse() * viewport.GetGlobalRect().GetCenter();
            var focus = Centre();
            for(var i=0;i<30;i++) Camera(new InputEventMouseButton {ButtonIndex=MouseButton.WheelUp,Pressed=true});
            Check(Mathf.IsEqualApprox(map.Scale.X,1.8f) && Centre().DistanceTo(focus)<.01f,"放大上限或中心锚点错误");
            for(var i=0;i<60;i++) Camera(new InputEventMouseButton {ButtonIndex=MouseButton.WheelDown,Pressed=true});
            Check(Mathf.IsEqualApprox(map.Scale.X,.35f) && Centre().DistanceTo(focus)<.01f,"缩小下限或中心锚点错误");
            await Capture("00_全图_"+(compact?"1280x720":"1920x1080"));
            Camera(new InputEventMouseButton {ButtonIndex=MouseButton.Right,Pressed=true});
            _panel._Input(new InputEventMouseMotion {Position=_panel.GetGlobalMousePosition()+new Vector2(-120,-90)});
            _panel._Input(new InputEventMouseButton {ButtonIndex=MouseButton.Right,Pressed=false});
            Check(Centre().DistanceTo(focus)>20,"右键拖动未移动镜头");
            Camera(new InputEventMouseButton {ButtonIndex=MouseButton.Right,Pressed=true});
            _panel._Input(new InputEventMouseButton {ButtonIndex=MouseButton.Right,Pressed=false});
            Check(Centre().DistanceTo(focus)<.01f,"右键单击没有回中");
            _panel.show_dungeon(); await Frames();
            GetViewport().PushInput(new InputEventMouseButton {ButtonIndex=MouseButton.WheelUp,Pressed=true,Position=viewport.GetGlobalRect().GetCenter()},true);
            Check(Mathf.IsEqualApprox(map.Scale.X,1.12f),"真实地格上的滚轮未传入地图");
            _panel.return_one_level();
            Camera(new InputEventMouseButton {ButtonIndex=MouseButton.WheelDown,Pressed=true});
            Check(Mathf.IsEqualApprox(map.Scale.X,1.12f),"详情打开时仍缩放地图");
            _panel.return_one_level();
            _panel.show_dungeon(); await Frames();
            foreach (var kind in Enum.GetValues<DungeonTerrainVisuals.Terrain>())
            {
                var count = DungeonTerrainVisuals.CountFor(kind);
                var regions = Enumerable.Range(0, count).Select(index => DungeonTerrainVisuals.GetVariant(kind, index))
                    .Select(texture => (texture.Atlas.ResourcePath, texture.Region)).Distinct().Count();
                Check(regions == count, "同地形款式数量不符");
            }
            Check(Enumerable.Range(0, 32).Select(i => DungeonTerrainVisuals.VariantFor("test", "node" + i)).Distinct().Count() == 4, "选款未覆盖四款");
            var originalRegions = EastWoodsDungeonRules.get_nodes().ToDictionary(node => node.Id, node => ((AtlasTexture)map.GetNode<TextureRect>("Node_" + node.Id + "/Terrain").Texture).Region);
            Check(map.GetChildren().OfType<Button>().Count() == 128, "不是128格地图");
            var reached=new HashSet<string>{"forest_edge"}; var queue=new Queue<string>(); queue.Enqueue("forest_edge");
            while(queue.TryDequeue(out var id)) foreach(var neighbor in EastWoodsDungeonRules.get_neighbors(id)) if(reached.Add(neighbor)) queue.Enqueue(neighbor);
            Check(reached.Count==128,"扩图存在不可达地格");
            Check(viewport.ClipContents && viewport.Position.Length()<1 && viewport.Size.DistanceTo(_panel.Size)<1,"地图未铺满全屏");
            foreach (var node in EastWoodsDungeonRules.get_nodes())
            {
                var button = map.GetNode<Button>("Node_" + node.Id);
                Check(button.GetNode<TextureRect>("Terrain").Texture is AtlasTexture, "地块图集未接入");
                Check(button.GetNode<TextureRect>("Terrain").Material is ShaderMaterial, "没有圆角裁切");
                Check(((StyleBoxFlat)button.GetThemeStylebox("normal")).CornerRadiusTopLeft > 0, "地块框仍为尖角");
                Check(button.Size.X >= 90, "扩图把地格压得过小");
                Check(Mathf.IsEqualApprox(button.Size.X, button.Size.Y), "地块不是正方形");
                foreach (var neighbor in EastWoodsDungeonRules.get_neighbors(node.Id))
                {
                    var offset = EastWoodsDungeonRules.get_node(neighbor)!.GridPosition - node.GridPosition;
                    Check(Math.Abs(offset.X) + Math.Abs(offset.Y) == 1, "出现斜向路线");
                }
            }
            Check(!_panel.GetNode<PanelContainer>("RoutePreview").Visible, "空闲显示了路线大面板");
            var occupants = map.GetNode<Control>("Node_forest_edge/Pawns");
            Check(occupants.GetChildCount() == 8, "同格NPC未以棋子显示");
            foreach (var pawn in occupants.GetChildren().OfType<Control>())
                Check(pawn.Position.X >= 0 && pawn.Position.Y >= 0 && pawn.GetRect().End.X <= occupants.Size.X + 1 && pawn.GetRect().End.Y <= occupants.Size.Y + 1, "同格棋子超出地块");
            var standing = occupants.GetChildren().OfType<Control>().ToArray();
            for (var i = 0; i < standing.Length; i++)
                for (var j = 0; j < i; j++) Check(!standing[i].GetRect().Intersects(standing[j].GetRect()), "同格棋子重叠");
            foreach (var strip in map.GetNode("Connections").GetChildren().OfType<TextureRect>())
                Check(strip.Size.X <= 45 && strip.Size.Y <= 45, "地形过渡超出格间连接宽度");
            var suffix = compact ? "1280x720" : "1920x1080";
            await Capture("01_节点地图_" + suffix);
            var target = map.GetNode<Button>("Node_quiet_grove");
            target.EmitSignal(BaseButton.SignalName.Pressed);
            Check(_panel.get_current_route_node_id() == "forest_edge", "首次点击直接移动");
            Check(_panel.GetNode<PanelContainer>("RoutePreview").Visible, "没有路径预览");
            await Capture("02_路径预览_" + suffix);
            target.EmitSignal(Control.SignalName.MouseEntered);
            await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);
            Check(!_panel.GetNode<PanelContainer>("TerrainHoverInfo").Visible, "视野外悬停泄漏现场详情");
            var near = map.GetNode<Button>("Node_deep_meet");
            near.EmitSignal(Control.SignalName.MouseEntered);
            await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);
            Check(_panel.GetNode<PanelContainer>("TerrainHoverInfo").Visible, "悬停没有显示详情");
            await Capture("03_悬停详情_" + suffix);
            near.EmitSignal(Control.SignalName.MouseExited);
            Check(!_panel.GetNode<PanelContainer>("TerrainHoverInfo").Visible, "离开后详情没有隐藏");
            var moves = 0;
            _panel.RouteStepped += (_, _) => moves++;
            target.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(6), SceneTreeTimer.SignalName.Timeout);
            Check(_panel.get_current_route_node_id() == "quiet_grove" && moves >= 7, "二次点击没有逐格到达");
            var state = _panel.get_route_state();
            _panel.close(); _panel.restore_route_state(state); _panel.show_dungeon(); await Frames();
            foreach (var pair in originalRegions)
                Check(((AtlasTexture)map.GetNode<TextureRect>("Node_" + pair.Key + "/Terrain").Texture).Region == pair.Value, "重进或恢复改变地格款式");
            Check(_panel.get_current_route_node_id() == "quiet_grove", "路线存档恢复错误");
            _panel._UnhandledKeyInput(new InputEventKey { Keycode = Key.W, Pressed = true });
            Check(_panel.get_current_route_node_id() == "upper_approach", "W键未到正交相邻格");
            Check(map.GetNodeOrNull<Control>("TravelPawn") is not null, "移动没有生成过渡棋子");
            var sawRock=false;
            for(var i=0;i<20;i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if(map.GetNodeOrNull<Control>("TravelPawn") is {} pawn && Math.Abs(pawn.Rotation)>.001) {sawRock=true;break;}
            }
            Check(sawRock, "移动没有摇摆");
            await Capture("04_摇摆行进_" + suffix);
            _panel.return_one_level();
            await Frames();
            Check(map.GetNodeOrNull<Control>("TravelPawn") is null && _panel.get_current_route_node_id()=="upper_approach", "中断残留棋子或退回重复结算");
            _panel.return_one_level();
            var closed = 0; _panel.Closed += () => closed++;
            _panel.GetNode<Button>("ReturnToLocation").EmitSignal(BaseButton.SignalName.Pressed);
            Check(_panel.Visible && closed == 0 && _panel.GetNode<PanelContainer>("RoutePreview").Visible, "查看出口误退出或没有路线预览");
            _panel.return_one_level();
            Check(_panel.Visible && _panel.GetNode<Control>("NodeActionDetails").Visible, "Esc没有打开暂停");
            _panel.return_one_level();
            Check(_panel.Visible && !_panel.GetNode<Control>("NodeActionDetails").Visible, "Esc没有只关闭暂停");
            var exitState = _panel.get_route_state(); exitState["current"] = "forest_edge";
            _panel.restore_route_state(exitState);
            _panel.close();
            Check(!_panel.Visible && closed == 1 && !_panel.get_route_state()["inside"].AsBool(), "出口未正确离场");
            _panel.QueueFree();
            npcManager.npcs.Clear();
            await Frames();
            // Drain managed Godot wrappers while the native engine is still alive.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GD.Print("EAST_WOODS_MAP_SMOKE PASS: terrain, orthogonal graph, HUD bounds, hover, preview/travel, WASD, save restore, return");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
