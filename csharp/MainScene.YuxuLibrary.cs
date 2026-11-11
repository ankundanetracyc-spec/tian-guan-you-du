using Godot;

public partial class MainScene
{
    private YuxuManualLibraryPanel? _yuxuManualLibrary;
    private void open_yuxu_manual_library()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId || has_active_item_claim()) return;
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (_player_current_yuxu_peak_id != "xuanlan" || overview?.Visible != true || overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true) return;
        if (_yuxuManualLibrary is null || !GodotObject.IsInstanceValid(_yuxuManualLibrary))
        {
            _yuxuManualLibrary = new YuxuManualLibraryPanel { Name = "YuxuManualLibrary" };
            GetNode<CanvasLayer>("CanvasLayer").AddChild(_yuxuManualLibrary);
            _yuxuManualLibrary.BackRequested += () => _yuxuManualLibrary?.Hide();
            _yuxuManualLibrary.CopyRequested += request_yuxu_manual_copy;
        }
        HoverInfoManager.HideAll(); _yuxuManualLibrary.Open(player);
    }
    private void request_yuxu_manual_copy(string bookPath)
    {
        if (_yuxuManualLibrary?.IsVisibleInTree() != true || _player_current_yuxu_peak_id != "xuanlan" || has_active_item_claim()) return;
        if (!YuxuManualCopy.TryPrepare(player, bookPath, get_yuxu_contribution_timestamp(), out var request, out var error))
        { _yuxuManualLibrary.ShowStatus(error); return; }
        _yuxuManualLibrary.SetPending(true);
        void RefreshReceipt(Godot.Collections.Array claimed)
        {
            _yuxuManualLibrary.SetPending(false); _yuxuManualLibrary.Refresh();
            _yuxuManualLibrary.ShowStatus("");
            if (claimed.Count > 0) on_yuxu_exam_first_copy_committed();
            mark_save_dirty(); refresh_yuxu_life_context(); RefreshJournalTracker();
        }
        if (!present_item_claim("玄览峰拓印", request!.Rewards(), RefreshReceipt, null, request.BeforeCommit))
        { _yuxuManualLibrary.SetPending(false); _yuxuManualLibrary.ShowStatus("领取页尚未打开，请稍后再试。"); }
    }
}
