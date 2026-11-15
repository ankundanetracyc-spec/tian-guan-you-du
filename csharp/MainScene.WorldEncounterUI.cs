using Godot;
using Godot.Collections;

public partial class MainScene
{
    private CanvasLayer? _worldEncounterLayer;
    private ForestWolfEventView? _worldEncounterView;

    // Uses the same illustrated event window as the East Woods encounters.
    private void ensure_world_encounter_view()
    {
        if (_worldEncounterLayer is not null && GodotObject.IsInstanceValid(_worldEncounterLayer)) return;
        _worldEncounterLayer = new CanvasLayer { Name = "WorldEncounterLayer", Layer = 50 };
        AddChild(_worldEncounterLayer);
        _worldEncounterView = new ForestWolfEventView { Name = "WorldEncounterEvent", Visible = false };
        _worldEncounterLayer.AddChild(_worldEncounterView);
        _worldEncounterView.ChoiceSelected += on_world_encounter_choice;
        _worldEncounterView.CloseRequested += () => _worldEncounterView?.SetNotice("请先选择如何处理这次遭遇。");
    }

    private void show_world_encounter_event()
    {
        var pending = _worldEncounterState.GetValueOrDefault("pending", new Dictionary()).AsGodotDictionary();
        var id = pending.GetValueOrDefault("id", "").AsString();
        var kind = pending.GetValueOrDefault("kind", "").AsString();
        if (string.IsNullOrEmpty(id) || kind is not ("wildlife" or "bandit")) return;
        ensure_world_encounter_view();
        var bandit = kind == "bandit";
        var page = new ForestWolfEvent.Page(
            bandit ? "路上遇劫" : "路遇兽踪",
            bandit
                ? "前路有人拦住去路，神色不善，手已按在兵刃上。你提前察觉了动静，尚有退开的余地。"
                : "路旁草丛一阵轻响，一头凶狼伏在暗处，正盯着你。你停下脚步，发现旁边还有一条可以退开的路。",
            [new ForestWolfEvent.Choice("battle", "迎战"),
             new ForestWolfEvent.Choice("leave", "退开，继续赶路", "避开这次遭遇，不获得战利品。")],
            Quiet: bandit,
            Note: "行旅已暂停，处理遭遇后可继续前进。");
        _worldEncounterView!.Display(page);
    }

    private void on_world_encounter_choice(string choice)
    {
        var pending = _worldEncounterState.GetValueOrDefault("pending", new Dictionary()).AsGodotDictionary();
        if (pending.Count == 0) return;
        if (choice == "leave")
        {
            hide_world_encounter_event();
            resolve_world_encounter_and_continue();
            return;
        }
        if (choice != "battle") return;
        var kind = pending.GetValueOrDefault("kind", "").AsString() == "bandit" ? "bandit" : "beast";
        if (start_world_encounter_battle(kind, pending.GetValueOrDefault("id", "").AsString()))
            hide_world_encounter_event();
        else
            _worldEncounterView?.SetNotice("暂时无法开始战斗，请稍后再试。");
    }

    private void hide_world_encounter_event()
    {
        _worldEncounterView?.Hide();
    }
}
