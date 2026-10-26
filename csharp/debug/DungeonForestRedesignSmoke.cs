using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GDictionary = Godot.Collections.Dictionary;

public partial class DungeonForestRedesignSmoke : Control
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private async Task Frames(int count = 4) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Settle(EastWoodsDungeonPanel panel)
    {
        for (var i = 0; i < 200 && (panel.is_route_traveling() || Read<Control?>(panel, "_travelPawn") is not null); i++)
            await ToSignal(GetTree().CreateTimer(.01), SceneTreeTimer.SignalName.Timeout);
        Check(!panel.is_route_traveling() && Read<Control?>(panel, "_travelPawn") is null, "Arrival did not finish");
    }
    private async Task Capture(string file)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = "res://docs/美术规范/东林地疏路分区_20261026";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        GetViewport().GetTexture().GetImage().SavePng(folder + "/" + file);
    }

    public override async void _Ready()
    {
        try
        {
            GetNode<NpcWorldManager>("/root/NpcWorldManager").set_simulation_enabled(false);
            for (var i = 0; i < 100; i++)
            {
                EastWoodsDungeonRules.generate_layout("forest-sparse-" + i);
                var layout = EastWoodsDungeonRules.get_layout_state();
                Check(layout["version"].AsInt32() == 3 && layout["nodes"].AsGodotArray().Count == 64, "Wrong sparse layout size/version");
                EastWoodsDungeonRules.validate_layout(layout);
                var nodes = EastWoodsDungeonRules.get_nodes();
                var crossings = nodes.Sum(n => EastWoodsDungeonRules.get_neighbors(n.Id).Count(id =>
                    (n.GridPosition.X >= 8) != (EastWoodsDungeonRules.get_node(id)!.GridPosition.X >= 8)));
                Check(crossings == 2, "Shallow/deep forest has multiple gates");
                Check(layout["edges"].AsGodotArray().Count == 66, "Peripheral connections formed extra loops");
                Check(EastWoodsActions.OrdinaryBeastPoints().Count == 3 && EastWoodsActions.ResourcePoints().Count == 8, "Content quantities changed");
                Check(EastWoodsDungeonRules.forest_region("forest_edge") == "浅林" && EastWoodsDungeonRules.forest_region("outer_13_06") == "深林", "Region classification incorrect");
            }
            EastWoodsDungeonRules.generate_layout("forest-sparse-visual");
            var panel = new EastWoodsDungeonPanel(); AddChild(panel); panel.show_dungeon(); await Frames();
            var map = panel.GetNode<Control>("MapViewport/OrthogonalNodeMap");
            Check(map.GetChildren().OfType<Button>().Count() == 64, "UI did not rebuild sparse map");
            foreach (var node in EastWoodsDungeonRules.get_nodes())
            {
                var button = map.GetNode<Control>("Node_" + node.Id);
                Check(button.GetNodeOrNull("ResourceNodeMarker") is null && button.GetNodeOrNull("BeastEncounterMarker") is null, "Map reveals contents");
                Check((string)Call(panel, "get_action_badge", node.Id)! == "", "Map badge reveals content");
            }
            Check(map.GetNode<TextureRect>("Node_outer_08_02/Terrain").SelfModulate != Colors.White, "Deep forest lacks visual distinction");
            var hover = (HoverInfoManager.HoverInfoContent)Call(panel, "build_node_hover_content", EastWoodsDungeonRules.get_node("fern_lowland")!)!;
            Check(!hover.Body.Contains("苦苔根"), "Remote hover reveals resource");
            var notes = (string)Call(panel, "exploration_notes")!;
            Check(!notes.Contains("凶狼尚在") && !notes.Contains("余 "), "Notes reveal remote contents");
            await Capture("01_浅林_1080.png");

            var requested = 0;
            string lastNode = "", lastAction = "";
            panel.ActionRequested += (node, action) => { requested++; lastNode = node; lastAction = action; };
            var state = panel.get_route_state(); state["current"] = "root_path";
            panel.restore_route_state(state); await Frames();
            Check(!Read<Control>(panel, "_actionModal").Visible, "Restoring save opened arrival narration");
            Check(panel.begin_assigned_route("deep_meet"), "Cannot walk across wolf point"); await Settle(panel);
            Check(panel.get_current_route_node_id() == "root_hollow", "Wolf narration did not stop route");
            Check(requested == 0, "Arrival started battle without explicit choice");
            Check(Read<Control>(panel, "_actionModal").Visible && Read<Label>(panel, "_actionBody").Text.Contains("凶狼"), "Wolf narration missing");
            Check(Read<Button>(panel, "_actionConfirm").Text == "查看战前情报", "Wolf choice is not explicit");
            Check(!panel.begin_assigned_route("deep_meet"), "Assigned route bypassed narration");
            await Capture("02_抵达遇狼_1080.png");
            GetWindow().Size = new Vector2I(1280, 720); await Frames(8);
            await Capture("02b_抵达遇狼_720.png");
            GetWindow().Size = new Vector2I(1920, 1080); await Frames(8);
            panel.close_action_details(); Call(panel, "refresh"); await Frames(8);
            Check(!Read<Control>(panel, "_actionModal").Visible && requested == 0, "Cancelled narration reopened on refresh");
            state = panel.get_route_state(); panel.restore_route_state(state); await Frames();
            Check(!Read<Control>(panel, "_actionModal").Visible, "Save restore replayed narration");
            Call(panel, "offer_arrival_narration", panel.available_actions("root_hollow").First(a => a.Battle));
            Read<Button>(panel, "_actionConfirm").EmitSignal(Button.SignalName.Pressed);
            Check(requested == 1 && lastNode == "root_hollow" && lastAction == "wolf", "Explicit choice did not request correct briefing");

            state = panel.get_route_state(); state["current"] = "south_turn";
            panel.restore_route_state(state); await Frames();
            var actionsBefore = Json.Stringify(panel.get_route_state()["actions"]);
            Check(panel.begin_assigned_route("fallen_canopy"), "Cannot walk across resource point"); await Settle(panel);
            Check(panel.get_current_route_node_id() == "fern_lowland" && Read<Control>(panel, "_actionModal").Visible, "Resource arrival did not stop and narrate");
            Check(Read<Label>(panel, "_actionBody").Text.Contains("苦苔根") && requested == 1, "Resource missing or auto-collected");
            Check(Json.Stringify(panel.get_route_state()["actions"]) == actionsBefore, "Narration consumed resources");
            await Capture("03_抵达采集_1080.png");
            Read<Button>(panel, "_actionConfirm").EmitSignal(Button.SignalName.Pressed);
            Check(requested == 2 && lastAction == "gather_root", "Explicit harvest not dispatched");
            panel.complete_action("fern_lowland", "gather_root"); panel.complete_action("fern_lowland", "gather_root");
            Check(panel.available_actions("fern_lowland").Count == 0, "Resource exhaustion not respected");
            state = panel.get_route_state(); state["current"] = "south_turn";
            panel.restore_route_state(state); await Frames();
            Check(panel.begin_assigned_route("fern_lowland"), "Cannot revisit empty resource"); await Settle(panel);
            Check(!Read<Control>(panel, "_actionModal").Visible, "Exhausted resource opened old narration");

            state = panel.get_route_state(); state["current"] = "outer_08_02";
            panel.restore_route_state(state); await Frames();
            Check(Read<VBoxContainer>(panel, "_actionRows").GetNode<Label>("ForestAmbience").Text.Contains("林口"), "Deep gate ambience missing");
            Check(Read<PanelContainer>(panel, "_actionBar").Size.Y < panel.Size.Y * .4f, "Action paper retains excessive empty height");
            await Capture("04_深林口_1080.png");
            GetWindow().Size = new Vector2I(1280, 720); await Frames(8);
            await Capture("05_深林口_720.png");
            GetWindow().Size = new Vector2I(1920, 1080); await Frames(8);
            state = panel.get_route_state();
            state["discovered"] = new Godot.Collections.Array<string>(EastWoodsDungeonRules.get_nodes().Select(n => n.Id));
            panel.restore_route_state(state); await Frames();
            typeof(EastWoodsDungeonPanel).GetField("_followPlayer", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(panel, false);
            typeof(EastWoodsDungeonPanel).GetField("_mapZoom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(panel, .42f);
            typeof(EastWoodsDungeonPanel).GetField("_cameraFocus", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(panel, Read<Control>(panel, "_mapRoot").Size * .5f);
            await Frames();
            await Capture("06_全图通路检查_1080.png");
            panel.QueueFree(); await Frames();
            GD.Print("DUNGEON_FOREST_REDESIGN_PASS: 100 seeds, gate, sparse graph, hidden contents, real arrival, explicit action, cancel/restore, exhaustion, regions");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
