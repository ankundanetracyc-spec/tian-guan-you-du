using Godot;
using Godot.Collections;
using System;
using System.Reflection;
using System.Threading.Tasks;

public partial class BaiChuanMapInteractionSmoke : Node
{
    private const string CaptureFolder = "res://docs/美术规范/主界面水墨美术迭代_20261104";
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Arrive(MainScene main, string id)
    {
        typeof(MainScene).GetMethod("on_node_world_arrived", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [id]);
        typeof(MainScene).GetMethod("refresh_node_world_character_list", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [true]);
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        MainScene? main = null;
        try
        {
            Check(OS.GetUserDataDir().Contains("isolated_appdata", StringComparison.OrdinalIgnoreCase), "UI smoke requires isolated user data");
            main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var limit = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < limit) await Frames();
            Check(main.StartupComplete && main.StartupError is null, "start: " + main.StartupError);
            typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            typeof(MainScene).GetField("_story_free_start", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
            typeof(MainScene).GetField("_baishiLecture", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, null);
            (typeof(MainScene).GetField("story_dialogue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main) as Control)?.Hide();
            typeof(MainScene).GetMethod("RefreshJournalTracker", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, []);
            typeof(MainScene).GetMethod("open_world", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, []);
            await Frames(8);
            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            Check(map.GetNodeOrNull("WorldMapDetails") is null, "Obsolete top-right toolbar remains");
            var entry = map.GetNode<Button>("EnterCurrentLocation");
            var hint = map.GetNode<Control>("WorldMapDestinationHint");
            var hud = main.GetNode<MainHUD>("HUDLayer/MainHUD");
            var roster = (CellCharacterList)GetTree().GetFirstNodeInGroup("cell_character_list");
            Check(map.Call("restore_node_state", new Dictionary { ["current_node"] = "bc_place_014", ["remaining_route"] = new Godot.Collections.Array() }).AsBool(), "restore city");
            Arrive(main, "bc_place_014");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(CaptureFolder));
            var hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(1280, 720) })
            {
                GetWindow().ContentScaleSize = size;
                DisplayServer.WindowSetSize(size);
                await Frames(10);
                map.Call("focus_player_node");
                await Frames(16);
                var viewport = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
                Check(entry.IsVisibleInTree() && !entry.Disabled && entry.Text == "进入", "Current place has no local entry");
                Check(viewport.Encloses(entry.GetGlobalRect()), "Entry out of viewport");
                Check(entry.GetGlobalRect().GetCenter().DistanceTo(viewport.GetCenter()) < 180, "Entry is detached from centered place");
                Check(!hint.Visible, "Current place redundantly displays destination hint");
                Check(roster.GetNode<Control>("ListPanel/RosterHeadingInk").GetGlobalRect().End.Y <= roster.GetNode<Control>("ListPanel/CharacterScroll").GlobalPosition.Y,
                    "Ink heading covers the first character row");
                var status = hud.GetNode<Control>("PlayerStatus");
                var nav = hud.GetNode<HBoxContainer>("Hbox");
                var realm = nav.GetNode<Control>("V_Realm");
                var oldRealmVisibility = realm.Visible;
                realm.Show();
                await Frames(4);
                foreach (var item in nav.GetChildren())
                {
                    if (item is not VBoxContainer { Visible: true } row) continue;
                    Check(viewport.Encloses(row.GetGlobalRect()) && !row.GetGlobalRect().Intersects(status.GetGlobalRect()), "Eight-entry navigation overlaps status or viewport");
                    var label = row.GetNode<Label>("Label");
                    Check(label.GetThemeColor("font_color") == WorldHudStyle.Ink, "Unreadable navigation label");
                    Check(viewport.Encloses(row.GetChild<Button>(0).GetNode<Control>("NavigationInkRing").GetGlobalRect()), "Decorative ring extends beyond viewport");
                }
                realm.Visible = oldRealmVisibility;
                await Frames(4);
                var statusTop = status.OffsetTop;
                status.OffsetTop -= 64; // Reserve the same space as two real status-summary rows.
                await Frames(4);
                Check(roster.get_ui_rect().End.Y + 12 <= status.GlobalPosition.Y, "Roster overlaps expanded status summaries");
                status.OffsetTop = statusTop;
                await Frames(4);
                var tracker = hud.GetNode<Control>("TaskTracker");
                var trackerToggle = hud.GetNode<Control>("TaskTrackerToggle");
                Check(Math.Abs(tracker.GlobalPosition.Y - trackerToggle.GlobalPosition.Y) < 1 && Math.Abs(tracker.Size.Y - trackerToggle.Size.Y) < 1,
                    $"Task toggle detached after resizing: panel={tracker.GetGlobalRect()} toggle={trackerToggle.GetGlobalRect()}");
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = GetViewport().GetTexture().GetImage();
                    Check(image.SavePng($"{CaptureFolder}/主界面_{size.X}x{size.Y}.png") == Error.Ok, "capture");
                }
                var prior = entry.Position;
                var camera = map.Get("camera_pos").AsVector2();
                var delta = new Vector2(32, 20);
                map.Set("camera_pos", camera + delta);
                map.Call("_update_camera");
                await Frames(3);
                Check((entry.Position - prior).DistanceTo(-delta * map.Get("world_zoom").AsSingle()) < 2, "Entry does not follow map pan");
                map.Call("focus_player_node");
                var neighbour = map.Call("get_current_neighbours").AsGodotArray<string>()[0];
                map.Call("_select_transit", neighbour);
                await Frames(5);
                Check(hint.Visible && hint.MouseFilter == Control.MouseFilterEnum.Ignore, "Selection hint missing or blocks map");
                Check(entry.Visible && !entry.Disabled, "Selecting another node removed the current-place entry");
                map.Call("set_encounter_blocked", true);
                Check(!entry.Visible && !hint.Visible, "Encounter lock leaves active overlays");
                map.Call("set_encounter_blocked", false);
                Check(map.Call("restore_node_state", new Dictionary { ["current_node"] = "bc_place_014", ["remaining_route"] = new Godot.Collections.Array() }).AsBool(), "reset city");
                await Frames(4);
                var toggle = hud.GetNode<Button>("ActionBarToggle");
                toggle.EmitSignal(Button.SignalName.Pressed);
                Check(!nav.Visible && toggle.Visible, "Navigation collapse");
                toggle.EmitSignal(Button.SignalName.Pressed);
                Check(nav.Visible, "Navigation expand");
                var rosterToggle = roster.GetNode<Button>("ToggleButton");
                rosterToggle.EmitSignal(Button.SignalName.Pressed);
                await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
                Check(roster.Size.X <= 32, "Collapsed roster still intercepts map");
                rosterToggle.EmitSignal(Button.SignalName.Pressed);
                await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
                Check(roster.get_ui_rect().Position.X >= 22, "Roster left alignment");
                GD.Print($"MAIN_UI_REFINEMENT viewport={size} entry_local=true pan_follow=true hints_pass_input=true eight_nav_fit=true collapse=true");
            }
            Check(hours == GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours(), "Viewing UI advanced time");
            // Activate the local entry through real viewport input, including mouse hit testing.
            map.Call("focus_player_node");
            await Frames(4);
            var point = entry.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            await Frames(2);
            Check(GetViewport().GuiGetHoveredControl() == entry, $"Local entry is occluded: point={point} rect={entry.GetGlobalRect()} hover={GetViewport().GuiGetHoveredControl()?.GetPath()} viewport={GetViewport().GetVisibleRect()} transform={GetViewport().GetFinalTransform()}");
            foreach (var pressed in new[] { true, false })
            {
                GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed,
                    ButtonMask = pressed ? MouseButtonMask.Left : 0 }, true);
                await Frames(2);
            }
            await Frames(6);
            Check(main.GetNodeOrNull<Control>("SceneLayer/WorldLocationInteriorPanel")?.IsVisibleInTree() == true, "Local entry does not open location");
            GD.Print("BAICHUAN_MAP_INTERACTION_SMOKE_OK local_entry_click=true clock_unchanged=true captures=" + ProjectSettings.GlobalizePath(CaptureFolder));
            main.QueueFree();
            await Frames(3);
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError("BAICHUAN_MAP_INTERACTION_SMOKE_FAILED " + e);
            if (main is not null && GodotObject.IsInstanceValid(main))
            {
                typeof(MainScene).GetField("_skipExitSave", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
                main.QueueFree();
            }
            GetTree().Quit(1);
        }
    }
}
