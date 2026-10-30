using Godot;
using System;
using System.Linq;
using GDictionary = Godot.Collections.Dictionary;

public partial class EastWoodsDungeonPanel
{
    public event Action<string, string, string>? ForestWolfChoiceRequested;
    public event Action? ForestWolfStateChanged;
    private ForestWolfEventView? _forestView;
    private string _forestRecordKey = "";
    public bool has_open_forest_event => _forestView?.Visible == true && _actionModal?.Visible == true;
    public GDictionary forest_record(string key) => ForestWolfEvent.Get(_actionProgress, key);
    public void store_forest_record(GDictionary record) => ForestWolfEvent.Store(_actionProgress, record);
    public void mark_forest_wolf_driven_off(GDictionary record, int hour)
    {
        EastWoodsEcology.MarkEncounterDrivenOff(_actionProgress, record["node"].AsString(),
            record["action"].AsString(), record.GetValueOrDefault("encounter_id", "").AsString(), hour);
        refresh();
    }
    public GDictionary open_forest_record()
        => _actionProgress.GetValueOrDefault(ForestWolfEvent.StateKey, new GDictionary()).AsGodotDictionary().Values
            .Where(v => v.VariantType == Variant.Type.Dictionary).Select(v => v.AsGodotDictionary())
            .FirstOrDefault(r => r.GetValueOrDefault("window_open", false).AsBool() && r.GetValueOrDefault("node", "").AsString() == _current) ?? new();
    private bool is_authored_forest_encounter(EastWoodsActions.ActionDef action)
        => !_dynamicMode && ForestWolfEvent.Supports(_current) && action.Battle && ForestWolfChoiceRequested is not null;
    public string previous_forest_contact(string node) => ForestWolfEvent.PreviousContact(_actionProgress, node);
    private void request_forest_choice(string choice)
    {
        if (_actionBusy || _npcEncounterBusy || _isTraveling || _travelPawn is not null || GetTree().Paused) return;
        if (choice != "open" && !has_open_forest_event) return;
        _actionBusy = true;
        try { ForestWolfChoiceRequested?.Invoke(_current, choice == "open" ? "" : _forestRecordKey, choice); }
        finally { _actionBusy = false; if (IsInsideTree()) refresh_action_bar(); }
    }
    public void show_forest_event(GDictionary record, BattleEntityData playerData)
    {
        clear_hover();
        if (_forestView is null)
        {
            _forestView = new ForestWolfEventView { Name = "ForestWolfEvent" };
            _actionModal!.AddChild(_forestView);
            _forestView.ChoiceSelected += request_forest_choice;
            _forestView.CloseRequested += () => close_action_details();
        }
        _forestRecordKey = record["key"].AsString();
        record["window_open"] = true; store_forest_record(record);
        _offeredAction = "";
        ((Control)_actionCard!.GetParent()).Hide();
        _actionModal!.Show(); _actionModal.MoveToFront();
        _forestView.Display(ForestWolfEvent.Describe(record, playerData));
    }
    private void hide_forest_event()
    {
        if (_forestView?.Visible != true) return;
        _forestView.Hide();
        var record = forest_record(_forestRecordKey);
        if (record.Count > 0) { record["window_open"] = false; store_forest_record(record); }
        ForestWolfStateChanged?.Invoke();
    }
    public void forest_event_notice(string text) => _forestView?.SetNotice(text);
}
