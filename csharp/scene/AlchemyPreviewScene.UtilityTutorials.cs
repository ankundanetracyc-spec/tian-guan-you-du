using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class AlchemyPreviewScene
{
    private bool _utilityMode;
    private int _utilityIndex;
    private AlchemyUtilityExercise? _utilityExercise;
    private readonly HashSet<string> _completedUtilities=[];
    private string _utilityBaseline="",_utilityEffect="";
    private double _utilityBaselineMetric,_utilityEffectMetric;
    private Button? _utilityTryButton;
    private Button? _utilityDemoButton,_utilityRetryButton;
    private bool _utilityHandsOn;
    private bool _utilityManual;
    private int _utilityManualBreath;
    private void StartUtilityLesson(int index,bool handsOn=false)
    {
        Reset();_utilityMode=true;_utilityManual=false;_utilityIndex=index;_lessonStep=0;_lessonActive=true;
        _utilityHandsOn=handsOn;_utilityBaseline=_utilityEffect="";
        _utilityExercise=AlchemyUtilityExercise.Create(AlchemyUtilityExercise.Order[index]);_model=_utilityExercise.Model;
        EnsureLessonPanel();_lessonPanel!.Show();Refresh();_relationGraph.SetOpen(true);
        if(handsOn)
        {
            var baseline=AlchemyUtilityExercise.Create(_utilityExercise.Utility);baseline.Model.RunAutomaticLesson(_utilityExercise.LessonBreaths);
            _utilityBaseline=baseline.Readout();_utilityBaselineMetric=baseline.Metric();_lessonStep=1;_utilityManual=true;_utilityManualBreath=_model.Breath;
            _relationGraph.GuideWiring(_utilityExercise.SourceId,_utilityExercise.TargetId,_utilityExercise.Utility);
        }
        else _relationGraph.FocusTeachingSource(_utilityExercise.SourceId,_utilityExercise.Utility);
        RefreshUtilityLesson();
    }
    private void RefreshUtilityLesson(string feedback="")
    {
        if(_utilityExercise is null||_lessonTitle is null)return;
        _lessonNext!.Disabled=false;
        var e=_utilityExercise;
        _lessonTitle.Text=$"第{_utilityIndex+1}课 · {e.Utility} · {AlchemyUtilityExercise.LessonName(e.Utility)}\n"+(_utilityManual?"亲手练习":_lessonStep>=2?"效果与用法":"观察对照");
        string body=_lessonStep switch
        {
            0=>e.Instruction+"\n\n先看同一初始炉局运行"+e.LessonBreaths+"息后的结果。",
            1=>"无功用对照：\n"+_utilityBaseline+"\n\n下一步重开相同初始炉局，亲手签本项功用线；开炉后自动运行相同息数。",
            2=>$"{e.MetricLabel}：{_utilityBaselineMetric:F3}→{_utilityEffectMetric:F3}\n\n"+_utilityEffect+"\n\n"+FollowUpInstruction(e.Utility),
            _=>e.Boundary+"\n\n当前炉况：\n"+e.Readout()
        };
        if(_utilityManual)body=e.WiringSteps+"\n\n要看什么："+e.MetricLabel+"。\n\n"+e.Instruction+"\n\n本课已备好真实材料与节点；签线不推进时间。开炉后自动运行"+e.LessonBreaths+"息，与上方无功用结果同初态、同观察时长。";
        if(_lessonStep==2)body=$"{e.MetricLabel}（{e.MetricUnit}）\n未使用：{_utilityBaselineMetric:F3}\n已使用：{_utilityEffectMetric:F3}\n\n"+e.Readout()+"\n\n"+FollowUpInstruction(e.Utility);
        _lessonText!.Text=body+(feedback.Length>0?"\n\n"+feedback:"");
        _lessonNext!.Text=_utilityManual?(_model.AutoRunStarted?"自动运行中":"运行本课 · 自动结算"):_lessonStep==3?(_utilityIndex<11?"完成本课 · 下一功用":"十二课完成 · 返回选课"):_lessonStep==2?"读完边界":"下一步";
        _lessonNext!.Disabled=_utilityManual&&_model.AutoRunStarted;
        if(_utilityTryButton is not null)_utilityTryButton.Visible=_lessonStep==1&&!_utilityManual;
        if(_utilityDemoButton is not null)_utilityDemoButton.Visible=_utilityManual;
        if(_utilityRetryButton is not null)_utilityRetryButton.Visible=true;
    }
    private void TryUtilityManually()
    {
        if(!_utilityMode||_lessonStep!=1)return;
        _utilityExercise=AlchemyUtilityExercise.Create(AlchemyUtilityExercise.Order[_utilityIndex]);_model=_utilityExercise.Model;
        _utilityManual=true;_utilityManualBreath=_model.Breath;
        Refresh();_relationGraph.SetOpen(true);_relationGraph.GuideWiring(_utilityExercise.SourceId,_utilityExercise.TargetId,_utilityExercise.Utility);RefreshUtilityLesson();
    }
    private void DemonstrateUtility()
    {
        if(!_utilityMode||!_utilityManual)return;
        _utilityManual=false;_lessonStep=1;AdvanceUtilityLesson();
    }
    private void ObserveUtilityAttempt(string error="")
    {
        if(!_lessonActive||!_utilityMode||!_utilityManual||_utilityExercise is null)return;
        var e=_utilityExercise;
        bool target=_model.SignedLinks.Any(l=>l.SourceDoseId==e.SourceId&&l.TargetDoseId==e.TargetId&&l.UtilityId==e.Utility);
        bool worked=_model.Contributions.ContainsKey(e.SourceName+"·"+e.Utility);
        if(e.Utility=="行")target&=_model.SignedLinks.Any(l=>l.SourceDoseId==e.SourceId&&l.UtilityId=="行"&&l.RouteTarget==e.Route);
        if(e.Utility=="通")worked=_model.SignedLinks.Any(l=>l.SourceDoseId==e.SourceId&&l.TargetDoseId==e.TargetId&&l.UtilityId=="通"&&l.LastFlow>1e-9);
        if(e.Utility=="引")target&=_model.SignedLinks.Any(l=>l.SourceDoseId==e.SourceId&&l.UtilityId=="引");
        if(error.Length==0&&_model.Breath>_utilityManualBreath&&target&&worked)
        {
            _utilityEffect=e.Readout();_utilityEffectMetric=e.Metric();_utilityManual=false;_lessonStep=2;
            _relationGraph.GuideWiring();
            RefreshUtilityLesson("操作成功：本功用已在指定对象上实际结算，来源投入已入账。");
        }
        else RefreshUtilityLesson(error.Length>0?error:e.Utility=="通"&&target?"通路已经接好；建成后再守炉一息，看到实际到货才完成本课。":"还未检测到本项实际作用。请确认目标、可用药性和炉令；签线本身不推进时间。");
    }
    private static string FollowUpInstruction(string utility)=>utility switch
    {
        "通"=>"通负责建立联系和基础导流；线路建成后才开始真正送药。",
        "行"=>"行沿已建通路增流；路线本身仍有容量上限。",
        "引"=>"引按支路需求重新分配药流，不增加总药量。",
        "化"=>"化可以处理未炼原料或黏胶滞留，但不能替代破开结构。",
        "护"=>"护在损伤发生前抵挡威胁；没有实际威胁就不会空耗。",
        "生"=>"生修复仍存在药性上的可恢复损伤，不能复原已转渣或散失的质量。",
        "明"=>"明提高诊断显露与分离度，后续排渣会更少夹带药性。",
        "藏"=>"藏降低以后逸散；已经逸散的药性无法追回。",
        _=>"线路只在有实际对象和缺口时消耗药性；有效线路受灵识并行上限调度。"
    };
    private void AdvanceUtilityLesson()
    {
        if(_utilityExercise is null)return;
        if(_utilityManual)
        {
            if(_model.Done){RefreshUtilityLesson("本次演练已经结束；可点“重练本课”重新布网。");return;}
            if(!_model.AutoRunStarted)StartAutomaticAlchemy();
            else ObserveUtilityAttempt();
            return;
        }
        string utility=AlchemyUtilityExercise.Order[_utilityIndex];
        if(_lessonStep==3)
        {
            _completedUtilities.Add(utility);
            if(_utilityIndex<11)StartUtilityLesson(_utilityIndex+1,_utilityHandsOn);else ShowTutorial();return;
        }
        var e=_utilityExercise;
        try
        {
        if(_lessonStep==0){e=AlchemyUtilityExercise.Create(utility);_utilityExercise=e;_model=e.Model;e.Model.RunAutomaticLesson(e.LessonBreaths);_utilityBaseline=e.Readout();_utilityBaselineMetric=e.Metric();}
            else if(_lessonStep==1)
            {
                e=AlchemyUtilityExercise.Create(utility);_utilityExercise=e;_model=e.Model;e.Apply();
                _utilityEffect=e.Readout();_utilityEffectMetric=e.Metric();
            }
            else
            {
                // 每课只跑一次真实对照；后续可以在自由炉局里继续试线路。
                _completedUtilities.Add(utility);
            }
            _lessonStep++;Refresh();_relationGraph.SetOpen(true);_relationGraph.GuideWiring();RefreshUtilityLesson();
        }
        catch(Exception error){RefreshUtilityLesson("本步未完成："+error.Message);GD.PushError(error.ToString());}
    }
}
