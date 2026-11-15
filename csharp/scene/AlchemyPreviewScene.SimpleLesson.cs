using Godot;
using System;
using System.Linq;

public partial class AlchemyPreviewScene
{
    private AlchemySimpleLesson? _simpleCourse;
    private bool _simpleAuto;
    private void StartSimpleCourse(bool assistant = false)
    {
        Reset();
        _simpleCourse = new AlchemySimpleLesson(assistant); _model = _simpleCourse.Model;
        _lessonActive = true;
        EnsureLessonPanel(); _lessonPanel!.Show();
        Refresh(); _relationGraph.SetOpen(true);
    }
    private void StartSimpleDemo()
    {
        if (_simpleAuto)
        {
            _simpleAuto = _demonstrating = false;_autoRunInProgress=false;_timer.Stop();_timer.WaitTime=3.2;Refresh();return;
        }
        if (_simpleCourse is null || _model.Done) StartSimpleCourse();
        _lessonActive=true;EnsureLessonPanel();_lessonPanel!.Show();_relationGraph.SetOpen(true);
        string error=_model.BeginAutomaticRun();
        if(error.Length>0){_model.Explanation=error;Refresh();ShowMessage("教程炉局不能开炉",error);return;}
        _simpleAuto = _demonstrating = _autoRunInProgress = true;
        _timer.Stop();_timer.WaitTime=.22;_timer.Start();RefreshSimpleLesson();Refresh();
    }
    private void AdvanceSimpleStep()
    {
        if (_simpleCourse is null) { StartSimpleCourse(); return; }
        if(_autoRunInProgress){AdvanceAutomaticAlchemy();if(!_autoRunInProgress){_simpleAuto=_demonstrating=false;Refresh();}return;}
        if (_simpleCourse.Stage == 8) { RefreshSimpleLesson(); return; }
        try
        {
            _simpleCourse.Step();
            if(_simpleCourse.Model.AutoRunActive)
            {
                _autoRunInProgress=true;_demonstrating=true;_timer.WaitTime=.22;_timer.Start();
            }
            Refresh(); CaptureFrame("simple-step");
        }
        catch (Exception error)
        {
            _simpleAuto = _demonstrating = _autoRunInProgress = false;_timer.Stop();_timer.WaitTime=3.2;
            RefreshSimpleLesson(error.Message); Refresh();
        }
    }
    private void RefreshSimpleLesson(string error = "")
    {
        if (_simpleCourse is null || _lessonTitle is null || !_lessonActive) return;
        _simpleCourse.Observe();
        _lessonTitle.Text = _simpleCourse.Title + (_model.AutoRunStarted?$"\n自动运行 · 第{_model.AutomaticBreathsElapsed}息":"\n布网已就绪 · 第0息");
        _lessonText!.Text = _simpleCourse.Instruction + (error.Length > 0 ? "\n\n" + error : "");
        _lessonNext!.Text = _simpleAuto ? "演示中 · 可点上方演示暂停" : _simpleCourse.ActionLabel;
        _lessonNext.Disabled = _simpleAuto||_autoRunInProgress||_simpleCourse.Stage==8;
        _utilityTryButton?.Hide();
        _utilityDemoButton?.Hide();_utilityRetryButton?.Hide();
    }
    private void TakeOverLesson()
    {
        if(_simpleCourse is not null&&_model.Done){Reset();_relationGraph.SetOpen(true);return;}
        _simpleAuto=false;_autoRunInProgress=false;_lessonActive=false;_lessonPanel?.Hide();_demonstrating=false;_timer.Stop();_timer.WaitTime=3.2;_relationGraph.GuideWiring();Refresh();
    }
    private string BasicFurnaceAction(AlchemyPreviewModel.Hand hand)
    {
        if (_demonstrating) return "先暂停演示，再自己操作。";
        if (hand is not (AlchemyPreviewModel.Hand.Purge or AlchemyPreviewModel.Hand.Gather or AlchemyPreviewModel.Hand.Output)) return "当前仅开放基础炉务。";
        string result = _model.QueueOperation(hand);
        _model.Explanation = result;
        Refresh();
        return hand==AlchemyPreviewModel.Hand.Output&&_model.Done?"本炉已结束，点击丹团查看结果。":result;
    }

    public async void CaptureSimpleLessons()
    {
        string directory = _captureDirectory;
        await ToSignal(GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
        await SaveRoomLayout("empty-start");
        _captureDirectory = ""; StartSimpleCourse();
        while (_simpleCourse!.Stage < 2) AdvanceSimpleStep();
        _captureDirectory = directory;
        await ToSignal(GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
        var source=_model.InFurnace.First(d=>d.Kind=="主药");var sourcePoint=_relationGraph.NodeLocalPosition(source.Id);
        _relationGraph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=sourcePoint});_relationGraph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=sourcePoint});
        await SaveRoomLayout("simple-wiring");
        _captureDirectory = "";
        for(int i=0;i<60&&_simpleCourse!.Stage<4;i++)AdvanceSimpleStep();
        var corePoint=_relationGraph.NodeLocalPosition(0);
        _relationGraph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=corePoint});_relationGraph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=corePoint});
        _captureDirectory=directory;await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("simple-furnace");
        _captureDirectory="";
        for (int i = 0; i < 80 && !_model.Done; i++) AdvanceSimpleStep();
        _captureDirectory = directory;
        await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
        await SaveRoomLayout("simple-result");
        _captureDirectory = ""; StartSimpleCourse(true);
        for (int i = 0; i < 90 && !_model.Done; i++) AdvanceSimpleStep();
        _captureDirectory = directory;
        await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
        await SaveRoomLayout("simple-assisted-result");
        GD.Print("ALCHEMY_SIMPLE_CAPTURES_DONE"); GetTree().Quit();
    }
}
