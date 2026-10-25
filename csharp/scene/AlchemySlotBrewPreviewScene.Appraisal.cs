using Godot;

public partial class AlchemySlotBrewPreviewScene
{
    private sealed record BrewSnapshot(int Sequence, double Attainment, AlchemySlotSimulation.Result Result,
        string Summary, string Report);
    private BrewSnapshot? _lastBrew;
    private int _brewSequence;
    private Button _appraisalButton = null!;
    private Button _appraisalBackButton = null!;
    private ScrollContainer _messageScroll = null!;

    private void ShowBrewSnapshot()
    {
        if (_lastBrew is null) return;
        ShowMessage("炼丹结果", _lastBrew.Summary);
        _appraisalButton.Show();
    }

    private void ShowAppraisalSnapshot()
    {
        if (_lastBrew is null) return;
        ShowMessage("炼后鉴定", _lastBrew.Report);
        _appraisalBackButton.Show();
    }
}
