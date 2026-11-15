using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Real viewport input regression for overlapping hover cards, scaled UI and map navigation.</summary>
public partial class HoverWheelNavigationSmoke : Node
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 6) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Set(MainScene main, string field, object value)
    {
        var target = typeof(MainScene).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingFieldException(typeof(MainScene).FullName, field);
        if (!target.FieldType.IsInstanceOfType(value))
            throw new InvalidOperationException($"验收装配字段{field}需要{target.FieldType.Name}，实际为{value.GetType().Name}");
        target.SetValue(main, value);
    }
    private void Move(Vector2 point) => GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
    private void Click(Vector2 point, MouseButton button)
    {
        Move(point);
        foreach (var pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = pressed }, true);
    }
    private static Vector2 Center(Control control) => control.GetGlobalTransformWithCanvas() * (control.Size * .5f);

    public override async void _Ready()
    {
        try
        {
            AddToGroup("gameplay_scene");
            GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
            var world = GD.Load<PackedScene>("res://tscn/world/TileWorldMapPresentation.tscn").Instantiate<Control>();
            AddChild(world);
            world.Show();
            var hud = new CanvasLayer { Layer = 20, Transform = new Transform2D(new Vector2(.8f, 0), new Vector2(0, .8f), new Vector2(110, 55)) };
            AddChild(hud);
            var panel = new Control { Position = new Vector2(160, 80), Size = new Vector2(420, 280), Scale = Vector2.One * .9f, MouseFilter = Control.MouseFilterEnum.Ignore };
            hud.AddChild(panel);
            var list = new ScrollContainer { Size = panel.Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, MouseForcePassScrollEvents = false };
            panel.AddChild(list);
            var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            list.AddChild(rows);
            for (var i = 0; i < 30; i++) rows.AddChild(new Button { Text = "滚动项目 " + i, CustomMinimumSize = new Vector2(0, 44) });
            await Frames(16);
            var zoom = world.Get("_user_zoom").AsSingle();
            Click(Center(list), MouseButton.WheelDown);
            await Frames();
            Check(list.ScrollVertical > 0, "缩放及偏移画布中的 UI 没有收到滚轮");
            Check(Mathf.IsEqualApprox(zoom, world.Get("_user_zoom").AsSingle()), "UI 滚轮被地图抢走");
            Click(new Vector2(1450, 800), MouseButton.WheelDown);
            await Frames();
            Check(!Mathf.IsEqualApprox(zoom, world.Get("_user_zoom").AsSingle()), "空白地图区域无法缩放");

            list.ScrollVertical = 0;
            await Frames();
            var first = rows.GetChild<Button>(0);
            var second = rows.GetChild<Button>(1);
            Move(Center(first));
            HoverInfoManager.SetInfo(first, new HoverInfoManager.HoverInfoContent("长说明", string.Join("\n", Enumerable.Range(0, 16).Select(i => "第 " + i + " 行：只读悬停详情"))), true);
            await Frames(16);
            var manager = GetNode<HoverInfoManager>("/root/HoverInfoManager");
            var card = manager.GetNode<PanelContainer>("SharedHoverInfoCard");
            var reader = card.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Single();
            Check(card.Visible && reader.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled, "变换后的目标没有显示完整只读详情");
            card.Position = Center(second) - new Vector2(60, 30);
            var beforeReadonlyWheel = list.ScrollVertical;
            Click(Center(second), MouseButton.WheelDown);
            await Frames();
            Check(list.ScrollVertical > beforeReadonlyWheel && reader.ScrollVertical == 0, "只读悬停仍拦截底下列表的滚轮");

            list.ScrollVertical = 0;
            await Frames();
            Move(Center(first));
            HoverInfoManager.SetInfo(first, new HoverInfoManager.HoverInfoContent("短说明", "简短内容"), true);
            await Frames(12);
            card.Position = Center(second) - new Vector2(60, 30);
            var before = list.ScrollVertical;
            Click(Center(second), MouseButton.WheelDown);
            await Frames();
            Check(list.ScrollVertical > before, "短悬停提示仍然吞掉底下列表的滚轮");
            list.ScrollVertical = 0;
            await Frames();
            Move(new Vector2(1600, 950));
            HoverInfoManager.SetInfo(second, new HoverInfoManager.HoverInfoContent("第二项", "被卡片覆盖的目标"));
            card.Position = Center(second) - new Vector2(60, 30);
            Move(Center(second));
            await Frames();
            var active = typeof(HoverInfoManager).GetField("_activeAnchor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager);
            Check(ReferenceEquals(active, second), "悬停卡覆盖目标后没有穿透切换详情");
            HoverInfoManager.HideAll();
            var clipped = rows.GetChild<Button>(29);
            Move(Center(clipped));
            HoverInfoManager.SetInfo(clipped, new HoverInfoManager.HoverInfoContent("视口外", "不应显示"), true);
            await Frames();
            Check(!card.Visible, "被滚动视口裁掉的目标错误命中");
            panel.Hide();

            var main = new EastWoodsActionHarness();
            AddChild(main);
            main.AddToGroup("main_scene");
            var roster = GD.Load<PackedScene>("res://tscn/panel/CellCharacterList.tscn").Instantiate<CellCharacterList>();
            hud.AddChild(roster);
            roster.show_world_npcs(new Godot.Collections.Array<WorldNpcData> { new() { npc_id = "wheel-navigation", npc_name = "同修" } }, "world:test");
            Set(main, "_nodeWorldMap", world);
            Set(main, "_interactionRoster", roster);
            var actions = new NpcInteractionPanel();
            hud.AddChild(actions);
            Set(main, "npc_interactions", actions);
            actions.show_menu("同修", new[] { ("交谈", (Action)(() => { })) });
            Click(new Vector2(1450, 800), MouseButton.Right);
            await Frames();
            Check(!actions.Visible && roster.Visible, "右键未关闭互动菜单或误隐藏名单");
            Click(new Vector2(1450, 800), MouseButton.Right);
            await Frames();
            Check(roster.GetNode<Container>("ListPanel/CharacterScroll/CharacterVBox").Visible && world.Visible, "根地图右键收起了角色列表");

            var depth = (Node2D)world.Get("_world_depth").AsGodotObject();
            var player = (Sprite2D)world.Get("_player_marker").AsGodotObject();
            Check(depth.YSortEnabled && player.GetParent() == depth && player.ZIndex == 0, "角色未进入地物共用的前后排序");
            Check(depth.ZIndex > world.GetNode<TileMapLayer>("MapCanvas/BaiChuanDualGridTerrainV1/LogicalGridLines").ZIndex, "格线会穿过角色与立体地物");
            var scenery = depth.FindChildren("*", "Sprite2D", true, false).OfType<Sprite2D>().First(sprite => sprite.HasMeta("world_map_authored_position"));
            var baseY = world.Call("_texture_base_y", scenery.Texture, scenery.Centered).AsDouble();
            Check(Math.Abs(baseY + scenery.Offset.Y) < .01, "地物排序点仍在图片中心");
            var originalForest = world.Get("_forest_cells").AsGodotDictionary().Keys.Select(key => key.AsVector2I()).ToHashSet();
            var originalMountain = world.Get("_mountain_cells").AsGodotDictionary().Keys.Select(key => key.AsVector2I()).ToHashSet();
            world.Call("_collect_slow_terrain_cells");
            Check(originalForest.SetEquals(world.Get("_forest_cells").AsGodotDictionary().Keys.Select(key => key.AsVector2I())) &&
                originalMountain.SetEquals(world.Get("_mountain_cells").AsGodotDictionary().Keys.Select(key => key.AsVector2I())), "调整排序锚点改变了地形行旅判定");
            world.Call("prepare_world_markers");
            await Frames();
            Check(depth.GetChildren().OfType<Sprite2D>().Any(sprite => sprite.Name.ToString().EndsWith("_DepthIllustration")), "建筑插画未参与排序");
            var fogMaterial = (ShaderMaterial)world.Get("_known_illustration_fog_material").AsGodotObject();
            Check(fogMaterial.GetShaderParameter("world_fog").AsGodotObject() == world.Get("_fog_texture").AsGodotObject(), "已知地点的迷雾图示未绑定实际迷雾");
            GD.Print("HOVER_WHEEL_NAVIGATION PASS: scaled UI wheel, map isolation, readonly-card wheel passthrough, short-hint passthrough, hover switch, clipping, right-back roster, shared ground sorting, terrain semantics, known-place fog");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
