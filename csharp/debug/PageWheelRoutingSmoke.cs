using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>Viewport input against production pages inside a complete MainScene; no save slot is opened.</summary>
public partial class PageWheelRoutingSmoke : Node
{
    private static object? Read(object obj, string field) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj);
    private static object? Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(obj, args);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private async Task Frames(int count = 4) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private Control _world = null!;
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new(1920, 1080);
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saves.has_active_slot, "滚轮测试不得使用存档槽");
            var fixture = new PersistentGameSave { year = 5, player_data = new HumanData { entity_name = "页面滚轮验收", cultivation_realm = "云息三层" },
                current_node_id = "yuxu_xianzong_core", world_cell = new(34, 166), npc_population_version = 1,
                scene_progress = new() { ["qingxi_prologue"] = 3, ["cloud_state_initialized"] = true, ["xuanjian_awakened"] = true } };
            for (var i = 0; i < 25; i++) fixture.world_npcs.Add(new WorldNpcData { npc_id = "page_wheel_" + i, npc_name = "同修" + i, current_node_id = "yuxu_xianzong_core" });
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "主场景加载失败");
            await Frames(20);
            var main = (MainScene)GetTree().CurrentScene;
            Call(main, "open_world"); await Frames(10);
            _world = (Control)Read(main, "tile_world_map")!;
            var player = (Character)Read(main, "player")!;
            var layer = main.GetNode<CanvasLayer>("CanvasLayer");
            var info = main.GetNode<InfoPanel>("CanvasLayer/HumanInfoPanel");
            info.show_entity(player); await TestPage("人物总览", info); info.Hide();
            var storage = (StoragePanel)Read(main, "storage")!;
            player.add_item_to_inventory("res://resources/entities/items/兽骨.tres", 1);
            storage.bind_player(player); storage.show_panel(); storage.MoveToFront();
            await TestPage("行囊", storage); storage.Hide();
            var letters = (LetterPanel)Read(main, "_letters")!;
            var mailbox = (LetterMailbox)Read(main, "_letterMailbox")!;
            for (var i = 0; i < 15; i++) mailbox.letters.Add(new LetterData { letter_id = "wheel_letter_" + i, source_event_id = "wheel_letter_" + i,
                subject = "来信" + i, body = string.Join("\n", Enumerable.Repeat("这是滚动正文的真实文字行。", 60)), sender_name = "行客" });
            mailbox.EmitChanged(); letters.Open("wheel_letter_0");
            await TestPage("信件", letters); letters.Hide();
            var journal = (JournalPanel)Read(main, "journal")!;
            journal.set_entries(JournalPanel.Section.Rumors, Enumerable.Range(0, 20).Select(i => new JournalPanel.Entry("见闻" + i,
                string.Join("\n", Enumerable.Repeat("山中见闻正文。", 60)), "耳闻", "百川", "待查")));
            journal.show_panel(); await TestPage("见闻", journal); journal.Hide();
            var cultivation = (CultivationUI)Read(main, "cultivation_ui")!;
            cultivation.show_entity(player); await TestPage("修炼", cultivation); cultivation.Hide();
            var encyclopedia = new EncyclopediaPanel { Name = "PageWheelEncyclopedia" }; layer.AddChild(encyclopedia);
            encyclopedia.show_panel(); await TestPage("百科", encyclopedia); encyclopedia.Hide();
            var settings = new GameSettingsPanel { Name = "PageWheelSettings" }; layer.AddChild(settings);
            await TestPage("设置", settings); settings.Hide();
            var trade = (TradePanel)Read(main, "trade_panel")!;
            trade.show_for(player, fixture.world_npcs[0]); await TestPage("交易", trade); trade.Hide();
            Check(!saves.has_active_slot, "测试意外开启存档槽");
            GD.Print($"PAGE_WHEEL_ROUTING PASS: 8 production pages, {_checks} real wheel targets; overflow forced only where needed, clipped/occluded targets skipped, world zoom unchanged");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task TestPage(string name, Control page)
    {
        HoverInfoManager.HideAll(); await Frames(8);
        var tested = 0;
        foreach (var scroll in page.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Where(s => s.IsVisibleInTree()).ToArray())
        {
            if (scroll.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled || scroll.GetChildOrNull<Control>(0) is not { } content) continue;
            var oldMinimum = content.CustomMinimumSize;
            if (scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page < 100)
                content.CustomMinimumSize = new(oldMinimum.X, Math.Max(oldMinimum.Y, scroll.Size.Y + 700));
            await Frames();
            var candidates = new List<(Vector2 Point, string Kind)>();
            foreach (var control in content.FindChildren("*", "Control", true, false).OfType<Control>())
            {
                if (!control.IsVisibleInTree() || control is not (Button or Label or RichTextLabel)) continue;
                var point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
                if (Accessible(control, point) && In(scroll, point)) candidates.Add((point, control is Button ? "button" : "text"));
            }
            foreach (var ratio in new[] { new Vector2(.5f, .55f), new Vector2(.92f, .8f), new Vector2(.1f, .9f) })
                candidates.Add((scroll.GetGlobalTransformWithCanvas() * (scroll.Size * ratio), "area"));
            var kinds = new HashSet<string>();
            foreach (var candidate in candidates)
            {
                if (kinds.Contains(candidate.Kind)) continue;
                scroll.ScrollVertical = 0; HoverInfoManager.HideAll(); await Frames(2);
                GetViewport().PushInput(new InputEventMouseMotion { Position = candidate.Point, GlobalPosition = candidate.Point }, true);
                await Frames(1);
                var hit = GetViewport().GuiGetHoveredControl();
                if (hit is null || hit is ScrollBar || NearestScroll(hit) != scroll || !Accessible(hit, candidate.Point)) continue;
                var before = scroll.ScrollVertical;
                var zoom = _world.Get("_user_zoom").AsSingle();
                foreach (var pressed in new[] { true, false })
                    GetViewport().PushInput(new InputEventMouseButton { Position = candidate.Point, GlobalPosition = candidate.Point, ButtonIndex = MouseButton.WheelDown, Pressed = pressed }, true);
                await Frames(2);
                Check(scroll.ScrollVertical > before, $"{name} 滚轮未滚动: {scroll.GetPath()}, hit={hit.GetPath()}, kind={candidate.Kind}, range={scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page}");
                Check(Mathf.IsEqualApprox(zoom, _world.Get("_user_zoom").AsSingle()), $"{name} 滚轮穿透缩放地图");
                kinds.Add(candidate.Kind); _checks++;
            }
            content.CustomMinimumSize = oldMinimum;
            if (kinds.Count > 0) tested++;
            else GD.Print($"PAGE_WHEEL_SKIP {name}: {scroll.GetPath()} has no unobscured scroll target");
        }
        Check(tested > 0, $"{name} 没有实际验证到滚动区域");
        GD.Print($"PAGE_WHEEL {name}: {tested} visible scroll containers passed");
    }

    private static ScrollContainer? NearestScroll(Node control)
    {
        for (Node? node = control; node is not null; node = node.GetParent()) if (node is ScrollContainer scroll) return scroll;
        return null;
    }
    private static bool In(Control control, Vector2 point) => new Rect2(Vector2.Zero, control.Size).HasPoint(control.GetGlobalTransformWithCanvas().AffineInverse() * point);
    private bool Accessible(Control control, Vector2 point)
    {
        if (!GetViewport().GetVisibleRect().HasPoint(point)) return false;
        for (Node? node = control; node is not null; node = node.GetParent())
            if (node is Control parent && (!parent.IsVisibleInTree() || parent.ClipContents && !In(parent, point))) return false;
        return true;
    }
}
