using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>节点网炼丹预览：布网交互、单击开炉自动演算与教程共用炉次模型。</summary>
public partial class AlchemyPreviewScene : Control
{
    private AlchemyPreviewModel _model=null!;
    private readonly Dictionary<string,AlchemyDragTile> _shelf=[];
    private readonly Dictionary<AlchemyPreviewModel.Hand,Button> _hands=[];
    private AlchemyDragTile _furnace=null!;
    private AlchemyFurnaceView _furnaceView=null!;
    private AlchemyFurnaceRelationGraph _relationGraph=null!;
    private AlchemyPolarityGauge _yin=null!;
    private Label _yinLabel=null!;
    private OptionButton _appearance=null!;
    private AcceptDialog _settings=null!;
    private VBoxContainer _pending=null!,_inside=null!,_changes=null!;
    private Label _title=null!,_guide=null!,_condition=null!,_forecast=null!,_resources=null!,_result=null!,_task=null!;
    private OptionButton _operator=null!;
    private SpinBox _wanted=null!,_attainment=null!;
    private CheckButton _recover=null!,_potion=null!;
    private Button _demoButton=null!,_next=null!,_learn=null!;
    private Button _runButton=null!;
    private Button _recipeDetailsButton=null!;
    private Godot.Timer _timer=null!;
    private Control _ghost=null!;
    private bool _updating,_demonstrating,_animating,_auto,_recipeExpanded;
    private bool _autoRunInProgress;
    private int _demoPhase,_generation,_captureIndex;
    private string _captureDirectory="";
    private static readonly Color Ink=new("303d38"),Dim=new("626b5b"),Gold=new("8c7049"),Jade=new("466d5c"),Red=new("a05240");
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _model=new(){Wanted=1};_model.SelectPill("tongmai");_model.SelectFormula("stream_fiber");_model.SelectMainHerb("溪纹藻丝");Theme=QuietRoomStyle.MakeTheme();
        Theme.SetColor("font_color","CheckButton",Ink);Theme.SetColor("font_disabled_color","CheckButton",Dim);
        BuildRoom();
        // Godot的GUI命中按控件树顺序判断，单独提高ZIndex不能避开透明投药区。
        var furnaceOrders=GetNode<HBoxContainer>("Actions");
        furnaceOrders.ZIndex=40;MoveChild(furnaceOrders,GetChildCount()-1);
        Refresh();_relationGraph.SetOpen(true);
        var args=OS.GetCmdlineUserArgs();int at=Array.IndexOf(args,"--alchemy-capture");
        if(at>=0&&at+1<args.Length) _captureDirectory=args[at+1];
        CaptureFrame("ready");
        if(args.Contains("--alchemy-check")) { try{RunChecks();GetTree().Quit();}catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}return; }
        if(args.Contains("--alchemy-pointer-check")) CallDeferred(nameof(CheckFurnacePointer));
        if(args.Contains("--alchemy-fast")) _timer.WaitTime=1.6;
        if(args.Contains("--alchemy-jade")){_appearance.Select(1);_furnaceView.Appearance=1;}
        if(args.Contains("--alchemy-demo")) CallDeferred(nameof(StartAutoDemo));
        if(args.Contains("--alchemy-simple-tutorials")) CallDeferred(nameof(CaptureSimpleLessons));
        if(args.Contains("--alchemy-layout")) CallDeferred(nameof(CaptureRoomLayout));
        if(args.Contains("--alchemy-tutorials")) CallDeferred(nameof(CaptureTutorials));
        if(args.Contains("--alchemy-utility-tutorials")) CallDeferred(nameof(CaptureUtilityTutorials));
        if(args.Contains("--alchemy-fire-preview")) CallDeferred(nameof(ShowFirePreview));
        if(args.Contains("--alchemy-halves-preview")) CallDeferred(nameof(CaptureDepthLayers));
    }
    private void QueueIntoFurnace(string name) { _model.Explanation=_model.PlaceHerb(name);Refresh(); }
    private void SelectHand(AlchemyPreviewModel.Hand hand)
    {
        if(_demonstrating||_model.Done)return;
        _techniques.Hide();
        if(hand is AlchemyPreviewModel.Hand.Hold or AlchemyPreviewModel.Hand.Raise or AlchemyPreviewModel.Hand.Lower){_model.Action=hand;Advance();return;}
        if(hand is AlchemyPreviewModel.Hand.Output or AlchemyPreviewModel.Hand.Quench or AlchemyPreviewModel.Hand.SlowFeed or AlchemyPreviewModel.Hand.Gather or AlchemyPreviewModel.Hand.Divide){_model.Explanation=_model.QueueOperation(hand);Refresh();return;}
        ChooseOperationTarget(hand);
    }
    private void ChooseOperationTarget(AlchemyPreviewModel.Hand hand)
    {
        var dialog=new ConfirmationDialog{Title="安排"+AlchemyPreviewModel.HandNames[(int)hand],Size=new Vector2I(520,260)};AddChild(dialog);
        var box=Box(10);dialog.AddChild(box);box.AddChild(Text("选择实际作用目标；确认不推进时间。",18,Ink));
        var target=new OptionButton();target.AddItem(_model.NodeName(0),0);foreach(var d in _model.InFurnace)target.AddItem(d.Herb.Name+" #"+d.Id,d.Id);box.AddChild(target);
        var source=new OptionButton();if(hand==AlchemyPreviewModel.Hand.Draw){box.AddChild(Text("从哪个节点牵药",18,Ink));foreach(var d in _model.InFurnace)source.AddItem(d.Herb.Name+" #"+d.Id,d.Id);box.AddChild(source);}
        dialog.Confirmed+=()=>{_model.Explanation=_model.QueueOperation(hand,target.GetSelectedId(),hand==AlchemyPreviewModel.Hand.Draw?source.GetSelectedId():0);Refresh();dialog.QueueFree();};
        dialog.Canceled+=dialog.QueueFree;dialog.PopupCentered();
    }
    private void PlanChanged()
    {
        if(_updating)return;_model.Helper=_operator.Selected==1;_model.Recover=_recover.ButtonPressed;_model.TakePotion=_potion.ButtonPressed;Refresh();
    }
    private void Advance()
    {
        if(_demonstrating||_model.AutoRunStarted)return;string error=_model.Advance();
        _model.Explanation=error.Length>0?error:_model.LastFeedback+(_model.Done?"\n点击【丹方判定】查看各颗结果。":"");
        Refresh();if(error.Length>0)ShowMessage("本息不能执行",error);CaptureFrame("manual");
        ObserveUtilityAttempt(error);
    }
    private void StartAutomaticAlchemy()
    {
        if(_model.Done){Reset();_relationGraph.SetOpen(true);return;}
        if(_autoRunInProgress)return;
        string error="";
        if(_utilityMode&&_utilityManual)_model.BeginAutomaticLesson(_utilityExercise?.LessonBreaths??1);
        else error=_model.BeginAutomaticRun();
        if(error.Length>0){_model.Explanation=error;Refresh();ShowMessage("还不能开炉",error);return;}
        _autoRunInProgress=true;_relationGraph.SetOpen(true);
        _timer.Stop();_timer.WaitTime=.22;_timer.Start();Refresh();CaptureFrame("automatic-start");
    }
    private void AdvanceAutomaticAlchemy()
    {
        bool active=_model.AdvanceAutomaticRun();
        ObserveUtilityAttempt();
        if(!active)
        {
            _autoRunInProgress=false;_timer.Stop();_timer.WaitTime=3.2;
            if(_simpleCourse is not null)_simpleAuto=_demonstrating=false;
            if(_advancedMode){_demonstrating=false;_lessonStep=1;}
            _model.Explanation=$"炼制结算完成：{_model.AutomaticBreathsElapsed}息，法力实耗{_model.AutomaticManaSpent}。";
        }
        Refresh();CaptureFrame(active?"automatic-breath":"automatic-result");
    }
    private void Refresh()
    {
        _simpleCourse?.Observe();
        _relationGraph.ReservedLeft=_lessonActive?440:0;
        _updating=true;_title.Text=$"丹 室   ·   第 {_model.Breath} 息";_guide.Text=_model.Explanation;
        _attainment.Value=_model.Attainment;_attainment.Editable=!_demonstrating;_wanted.Value=_model.Wanted;_wanted.Editable=!_demonstrating&&!_model.Done;
        var learnHand=(AlchemyPreviewModel.Hand)_learnChoice.GetSelectedId();var learnRule=AlchemyProcessConfig.Tools[AlchemyPreviewModel.HandNames[(int)learnHand]];_learn.Text=_model.Learned.Contains(learnHand)?"已学会所选技术":$"研读《{learnRule.Book}》1时辰（造诣{learnRule.Learn}）";_learn.Disabled=_demonstrating||_model.Learned.Contains(learnHand)||_model.Attainment<learnRule.Learn||!_model.CanChangeRecipe;
        var mainDose=_model.InFurnace.FirstOrDefault(d=>d.Kind=="主药");
        string mainState="尚未投主药";
        if(mainDose is not null)
        {
            var profile=mainDose.Herb.Processing;
            mainState=mainDose.Raw<=.01?"已炼完":_model.Temperature<profile.Open?$"未到开炼火候（需{profile.Open:F0}）":_model.Temperature>profile.Safe?$"超过耐热线（{profile.Safe:F0}）":$"炼化中 {mainDose.Released:P0}";
        }
        string mixing=_model.Fusion<.02?"尚未混合":_model.Fusion<.98?"正在混合":"已经混匀";
        string forming=_model.Shape<.02?"尚未凝丹":_model.Shape<.98?"正在成形":"已经成形";
        _condition.Text=$"主药 {mainState}  ·  药性 {mixing}  ·  丹体 {forming}";
        _task.Text=$"计划 {_model.Wanted} 颗  ·  已凝 {_model.Pills.Count} 颗  ·  炉渣 {_model.Slag:F1}";
        _task.Text+="\n高品质配伍："+string.Join("、",AlchemyRecipeCatalog.SupportingRequirements(_model.SelectedPillId).Select(r=>$"{r.Utility}（>{r.StartsAbove}分起）"));
        if(_model.Pills.Count>0)_task.Text+="\n"+string.Join("\n",_model.Pills.Select(p=>$"{p.Index}号预估{_model.ProjectedQuality(p)}分；90分缺口：{_model.QualityGaps(p)}"));
        _task.TooltipText=$"凝丹把实际可用药性分配到各颗；出炉逐颗判定，缺项不互相补分。\n排渣会按分离程度带走药性，预计夹带 {_model.CarryRisk:F2} 份。改变颗数后需要再次凝丹。";
        _yinLabel.Text=_model.Available<.001?"阴阳 · 暂无已炼出的药性":$"阴阳 {_model.Polarity:+0.00;-0.00;0.00} · {(Math.Abs(_model.Polarity)<.1?"较平衡":_model.Polarity>0?"偏阳":"偏阴")}";
        var pillRule=_model.CurrentPill;
        _yinLabel.TooltipText=$"{pillRule.Name}阴阳目标{pillRule.PolarityTarget:+0.00;-0.00;0.00}；丹效20时容许偏差±{pillRule.PolarityToleranceAt20:F2}，丹效100时收紧至±{pillRule.PolarityToleranceAt100:F2}。刻度显示当前已炼药性偏向。";
        _yin.Value=_model.Polarity;_yin.HasMedicine=_model.Available>.001;_yin.QueueRedraw();
        _furnaceView.SetState(_model.ActualFire,_model.Temperature);
        _relationGraph.SetModel(_model);
        _attainment.TooltipText=$"当前操作者造诣{_model.OperatorAttainment}：可控火候0～{_model.FireLimit}档，本息投料上限{_model.FeedLimit}，凝丹上限{_model.MaxPills}，实际精度{_model.Preview().Precision:F1}。\n每20点造诣增加1档火候，仍受丹炉与火种上限限制。";
        _operator.Select(_model.Helper?1:0);_recover.SetPressedNoSignal(_model.Recover);_potion.SetPressedNoSignal(_model.TakePotion);
        var f=_model.Preview();
        int currentMana=_model.Helper?_model.HelperMana:_model.Mana;
        int manaCap=_model.Helper?AlchemyProcessConfig.HelperMana:AlchemyProcessConfig.MainMana;
        int spiritCap=_model.Helper?AlchemyProcessConfig.HelperSpirit:AlchemyProcessConfig.MainSpirit;
        _resources.Text=_model.AutoRunStarted
            ?$"灵识并行 {_model.ActiveAutomaticLines}/{spiritCap} · 轮候 {_model.QueuedAutomaticLines}线  ·  法力 {currentMana}/{manaCap}（已耗 {_model.AutomaticManaSpent}）"
            :$"灵识并行上限 {spiritCap} · 已布功用线 {_model.SignedLinks.Count}  ·  法力 {currentMana}/{manaCap}";
        _resources.TooltipText="灵识是同时照看线路的手数：每条功用线需要1点才能并行运行；超过上限的线自动轮候，不会扣空灵识或炸炉。放药、拖节点和签线在准备时使用修士的手，但不推进时间、不扣灵识。法力只供火候：添焰付下一档、守炉付当前档、敛焰付原档，自动炉务按实际档位结算。";
        _resources.AddThemeColorOverride("font_color",Ink);
        int targetHeat=_model.InFurnace.FirstOrDefault(d=>d.Kind=="主药")?.Herb.Processing.OptimalLow??0;
        _forecast.Text=_model.AutoRunStarted
            ?$"自动火候 {_model.Heat}档 · 累计 {_model.ElapsedText}（{_model.ElapsedHours:0.0}小时）\n{_model.AutomaticStopReason}"
            :$"自动火候以主药适宜档{Math.Min(targetHeat,_model.FireLimit)}档为先 · 当前最高可控{_model.FireLimit}档\n法力按升温、守火、降火实际档位扣除；10息为一时辰。";
        if(_model.Done) _forecast.Text="本炉已结束。\n已显示实际耗时、法力、功用瓶颈与逐颗成品结果。";
        _forecast.AddThemeColorOverride("font_color",Jade);
        foreach(var h in _model.Herbs){var tile=_shelf[h.Name];tile.TooltipText=HerbInfo(h,null);tile.Modulate=Colors.White;tile.AddThemeStyleboxOverride("panel",h.Name==_model.MainName?MainHerbClothMark():new StyleBoxEmpty());}
        Clear(_pending);var pg=new HBoxContainer();_pending.AddChild(pg);
        foreach(var d in _model.Pending){var b=HerbButton(d.Herb);b.Disabled=_demonstrating;b.Pressed+=()=>{_model.Pending.Remove(d);Refresh();};pg.AddChild(b);}
        if(_model.Pending.Count==0)_pending.AddChild(Text("还没有安排投料",17,Dim));
        foreach(var op in _model.PendingOperations.ToArray()){var planned=Button("撤回 · "+AlchemyPreviewModel.HandNames[(int)op.Kind]+" → "+_model.NodeName(op.TargetId),()=>{_model.PendingOperations.Remove(op);_model.RepeatingOperations.Remove(op);Refresh();});_pending.AddChild(planned);}
        Clear(_inside);
        foreach(var d in _model.InFurnace)_inside.AddChild(DoseButton(d));
        if(_model.InFurnace.Count==0)_inside.AddChild(Text("炉内尚无药材",18,Dim));
        Clear(_changes);
        foreach(string line in _model.Changes.Distinct().TakeLast(6))_changes.AddChild(Text(line,18,Ink,true));
        if(_model.Changes.Count==0)_changes.AddChild(Text("每息后显示实际变化；详细炉记可看所有功用来源。",18,Dim,true));
        _result.Text=_model.Result+(!_model.Done?"":"\n"+string.Join("\n",_model.Pills.Select(p=>$"{p.Index}号：{p.Score} · {AlchemyPreviewModel.Tier(p.Score)} · 试验效果：{p.EffectText}\n{p.NextGap}")));
        foreach(var pair in _hands){pair.Value.Disabled=_demonstrating||_model.Done||!_model.CanUse(pair.Key);pair.Value.Modulate=Colors.White;pair.Value.AddThemeColorOverride("font_disabled_color",pair.Key==_model.Action?Gold:Dim);}
        _operator.Disabled=_demonstrating||!_model.CanPrepare;_recover.Disabled=_demonstrating||!_model.CanPrepare;_potion.Disabled=_demonstrating||!_model.CanPrepare||_model.Potions==0;
        _runButton.Text=_model.Done?"重新开炉":_model.AutoRunActive?$"自动运行中 · 第{_model.AutomaticBreathsElapsed}息":"开炉炼丹";
        _runButton.Disabled=_demonstrating||_autoRunInProgress;
        _demoButton.Text=_demonstrating?"暂停演示，自己接手":"自动讲解演示";_next.Text=_demonstrating?"下一步演示 →":"逐步看演示 →";_next.Disabled=_animating;
        UpdateRoomState();_updating=false;
        RefreshSimpleLesson();
    }
    private string HerbInfo(AlchemyPreviewModel.Herb h,AlchemyPreviewModel.Dose? d)
    {
        var p=h.Processing;
        string s=$"{h.Name} · {(h.Name==_model.MainName?"主药":"辅药")}\n五行：{(h.Craft.element.Length==0?"无":h.Craft.element)} · 加工：{h.Craft.texture} · {(h.Craft.polarity>0?"阳":h.Craft.polarity<0?"阴":"和")}\n火候：{p.Open}档开炼，{p.OptimalLow}—{p.OptimalHigh}档适宜，最高耐受{p.Safe}档。\n适宜区间基础炼化{p.Rate:F0}质量／息／原始份，可用药性得率{p.Yield:P0}。";
        if(d is not null)
        {
            string state=d.Raw<=.01?"已炼完":_model.Temperature<p.Open?"尚未到开炼火候":_model.Temperature>p.Safe?"超过耐热线，正在受损":"正在炼化";
            s+=$"\n\n第{d.Batch}息投入 · {state}\n已炼化{d.Released:P0} · 可用药性{d.Available:F1} · 未炼部分{d.Raw:F1} · 炉渣{d.Slag:F1}";
        }
        foreach(var u in h.Craft.utilities)
        {
            s+=$"\n\n{u.utility_id}{MaterialCraftData.utility_grade_name(u.grade)}：{AlchemyPreviewModel.Purposes[u.utility_id]}";
            if(d is not null)s+="\n"+_model.FunctionStatus(h,u.utility_id);
        }
        if(d is null&&_model.Temperature>p.Safe)s+="\n\n当前火候已超过该旧原型档的耐热线，立即投料会受损。";
        return s;
    }
    private Button HerbButton(AlchemyPreviewModel.Herb h)
    {
        var b=new Button{Icon=GD.Load<Texture2D>(h.Item.icon_path),ExpandIcon=true,CustomMinimumSize=new Vector2(76,70),IconAlignment=HorizontalAlignment.Center};b.AddThemeConstantOverride("icon_max_width",42);b.AddThemeStyleboxOverride("normal",Style(new Color(.92f,.92f,.83f,.6f),new Color(.56f,.53f,.43f,.35f),4));return b;
    }
    private Button DoseButton(AlchemyPreviewModel.Dose dose)
    {
        var profile=dose.Herb.Processing;
        string state=dose.Raw<=.01?"炼化完成":_model.Temperature<profile.Open?"待升温":_model.Temperature>profile.Safe?"热损伤风险":$"炼化{dose.Released:P0}";
        var button=new Button
        {
            Icon=GD.Load<Texture2D>(dose.Herb.Item.icon_path),
            Text=$"{dose.Herb.Name} · 第{dose.Batch}息\n{state} · 可用药性 {dose.Available:F1}",
            CustomMinimumSize=new Vector2(0,72),
            TooltipText=HerbInfo(dose.Herb,dose),
            IconAlignment=HorizontalAlignment.Left,
            Alignment=HorizontalAlignment.Left,
            ExpandIcon=true
        };
        button.AddThemeConstantOverride("icon_max_width",46);
        button.AddThemeConstantOverride("h_separation",12);
        button.AddThemeFontSizeOverride("font_size",17);
        button.AddThemeStyleboxOverride("normal",Style(new Color(.92f,.92f,.83f,.46f),new Color(.56f,.53f,.43f,.32f),8));
        button.Pressed+=()=>ShowMessage(dose.Herb.Name,HerbInfo(dose.Herb,dose));
        return button;
    }
    private void Reset()
    {
        _simpleCourse=null;_simpleAuto=false;_autoRunInProgress=false;
        _lessonActive=false;_advancedMode=false;_lessonPanel?.Hide();
        _utilityMode=false;_utilityExercise=null;
        _utilityManual=false;
        _generation++;_timer.Stop();_timer.WaitTime=3.2;_demonstrating=_animating=_auto=false;_ghost.Hide();_herbCloth.SetOpen(false);_techniques.Hide();_doseDialog.Hide();_demoPhase=0;_model=new(){Wanted=1};_model.SelectPill("tongmai");_model.SelectFormula("stream_fiber");_model.SelectMainHerb("溪纹藻丝");Refresh();_relationGraph.SetOpen(true);
    }
    private void LoadFactoryScenario()
    {
        Reset();_model=AlchemyFactoryScenario.Create();Refresh();_relationGraph.SetOpen(true);
    }
    public void StartAutoDemo()=>StartSimpleDemo();
    private void NextDemo(){if(_simpleCourse is null)StartSimpleCourse();else AdvanceSimpleStep();}
    private void BeginDemo(){LoadFactoryScenario();_demonstrating=true;_model.Explanation="基建演示：观察八味药材的分支、中继、增流与配伍；暂停后可拆改产线。";Refresh();}
    private void DemoNext()
    {
        if(_autoRunInProgress){AdvanceAutomaticAlchemy();return;}
        if(!_demonstrating||_animating)return;
        if(_model.Done){_timer.Stop();_demonstrating=_auto=false;Refresh();CaptureFrame("finished");return;}
        _model.Action=AlchemyPreviewModel.Hand.Hold;
        int mainId=_model.InFurnace.First(d=>d.Kind=="主药").Id;
        if(_demoPhase>=2){_model.RepeatingOperations.RemoveWhere(o=>o.Kind==AlchemyPreviewModel.Hand.Draw&&o.SourceId!=mainId);_model.PendingOperations.RemoveAll(o=>o.Kind==AlchemyPreviewModel.Hand.Draw&&o.SourceId!=mainId);}
        double arrivedMain=_model.Inventory.Where(p=>p.Location==0&&p.Source.Kind=="主药").Sum(p=>p.Amount);
        if(arrivedMain>=24||_model.Pills.Count>0)
        {
            _model.RepeatingOperations.Clear();_model.PendingOperations.Clear();
            foreach(var route in _model.SignedLinks.Where(l=>l.UtilityId=="通"))route.FlowOpen=false;
            foreach(var l in _model.SignedLinks.Where(l=>l.UtilityId is "行" or "引")){l.Repeat=false;l.Paused=true;_model.PlannedLinks.Remove(l);}
            _model.QueueOperation(AlchemyPreviewModel.Hand.Circulate);
            _model.QueueOperation(AlchemyPreviewModel.Hand.Separate);
            _model.QueueOperation(AlchemyPreviewModel.Hand.Purge);
            if(_model.Pills.Count>0||(_model.NodeMixed(0)>=.97&&_model.NodeSlag(0)/Math.Max(1,_model.NodeAvailable(0))<.001))_model.QueueOperation(AlchemyPreviewModel.Hand.Gather);
        }
        if(_model.Pills.Count>0&&_model.Pills.All(p=>_model.ProjectedQuality(p)>=90)){_model.QueueOperation(AlchemyPreviewModel.Hand.Output);Refresh();return;}
        string error=_model.Advance();_demoPhase++;
        _model.Explanation=error.Length>0?error:"基建产线第"+_demoPhase+"步：药性经过分支、中继与中心，暂停后可接手拆改。\n"+_model.LastFeedback;
        if(error.Length>0||_demoPhase>60){_timer.Stop();_demonstrating=_auto=false;}
        Refresh();CaptureFrame("factory");
    }
    private async void DemoHand(AlchemyPreviewModel.Hand hand,string explanation,int next)
    {
        _animating=true;int gen=_generation;_model.Action=hand;_model.Helper=_model.Recover=false;
        _herbCloth.SetOpen(false);_techniques.Visible=hand is not (AlchemyPreviewModel.Hand.Hold or AlchemyPreviewModel.Hand.Raise or AlchemyPreviewModel.Hand.Lower);
        if(_model.Mana<_model.Preview().Mana+12&&_model.Potions>0){_model.TakePotion=true;explanation="安排【本息服回灵散】，执行时支付一份恢复药。\n"+explanation;}
        else if(_model.Mana<_model.Preview().Mana+8&&_model.HelperMana>14){_model.Helper=_model.Recover=true;_model.Action=hand=AlchemyPreviewModel.Hand.Hold;next=_demoPhase;explanation="本步先由副手【维持】，本人【回气】；仍推进一息，炉内继续变化。恢复后再执行原手段。";}
        _model.Explanation=explanation;Refresh();_hands[hand].Modulate=new Color(1.6f,1.3f,.65f);CaptureFrame("before");
        await ToSignal(GetTree().CreateTimer(.9),SceneTreeTimer.SignalName.Timeout);
        if(gen!=_generation)return;
        _techniques.Hide();
        string error=_model.Advance();
        if(error.Length>0){_model.Explanation=error;_timer.Stop();_demonstrating=false;}else{_model.Explanation=explanation+"\n"+_model.LastFeedback;_demoPhase=next;}
        _animating=false;Refresh();CaptureFrame("after");
        if(_model.Breath>90){_timer.Stop();_demonstrating=false;_model.Explanation="演示保护停止：请查看炉记。";Refresh();}
    }
    private void AnimateDose(string name,string explanation,int next)
    {
        _herbCloth.SetOpen(true);
        _animating=true;int gen=_generation;_model.Explanation=explanation;Refresh();
        _ghost.Position=_shelf[name].GlobalPosition;((TextureRect)_ghost.GetChild(0)).Texture=_shelf[name].Icon;_ghost.Show();CaptureFrame("drag");
        var tween=CreateTween();tween.TweenProperty(_ghost,"position",_furnace.GlobalPosition+_furnace.Size*.5f-new Vector2(35,35),1.1).SetTrans(Tween.TransitionType.Sine);
        tween.TweenCallback(Callable.From(()=>{if(gen!=_generation)return;_ghost.Hide();string message=_model.QueueHerb(name);_model.Action=AlchemyPreviewModel.Hand.Hold;string error=_model.Advance();_model.Explanation=explanation+"\n"+message+(error.Length>0?error:"\n"+_model.LastFeedback);_demoPhase=next;_animating=false;Refresh();CaptureFrame("queued");}));
    }
    private void ShowTutorialReference()=>ShowMessage("看懂炼丹", 
        "先放入主药与辅药，再在节点网上预设药材功用线；布置完成后点一次【开炉炼丹】。自动炉务逐息安排整数火候、处理药性、输送与凝丹，结束时给出实际成品、余料和停车缘由。\n\n"+
        "灵识代表同时照看的手数：每条正在执行的功用线占1点；超过上限的线路轮候，不会扣空灵识或触发旧式超载。放药和签线在布置时完成，不逐息收费。法力只供火候：添焰支付新档，守炉支付当前档，敛焰支付降火前档。10息为1时辰。\n\n"+
        "左键展开丹炉节点网，右键收起。药材可拖动，点药材再选功用并点目标完成接线；线上显示功用名。节点只显图标与名称，点击显示详情，悬停不弹栏。\n\n"+
        "成品：主药发挥、配伍协调、丹体稳定共同决定评级，实际药效按丹种规则计算。五行关系只沿真实药性路线结算；阴阳躁动在实际接触后交给镇等功用处理。复杂回流丹胚尚未接入当前测试炉。");
    private void ShowActiveUtilities()=>ShowMessage("这一息，各味药发挥了什么",string.Join("\n\n",_model.InFurnace.Select(d=>d.Herb).Distinct().SelectMany(h=>h.Craft.utilities.Select(u=>$"{h.Name} · {u.utility_id}{MaterialCraftData.utility_grade_name(u.grade)}\n{_model.FunctionStatus(h,u.utility_id)}"))));
    private void ShowUtilities()=>ShowMessage("十二功用 · 按材料原表", "主辅使用同一规则，功用是共用药性的能力。没有对象不产生作用消耗；已形成结构不每息重复扣。\n\n"+string.Join("\n\n",AlchemyPreviewModel.Purposes.Select(p=>p.Key+"："+p.Value))+"\n\n各项实际来源与投入见详细炉记、炉内药材详情。" );
    private void ShowChecks()=>ShowMessage($"{_model.CurrentPill.Name} · 成丹要求与结果",_model.Checks()+"\n\n"+string.Join("\n\n",_model.Pills.Select(p=>$"{p.Index}号：主药{p.MainAmount:F2}，{p.Score}·{AlchemyPreviewModel.Tier(p.Score)}\n{p.Reason}")));
    private void ShowDetails()=>ShowMessage("详细炉记",string.Join("\n",_model.Log.TakeLast(120)));
    private void ShowMessage(string title,string content)
    {
        var dialog=new AcceptDialog{Title=title,Size=new Vector2I(950,750)};AddChild(dialog);var rich=new RichTextLabel{Text=content,CustomMinimumSize=new Vector2(890,640)};rich.AddThemeFontSizeOverride("normal_font_size",22);dialog.AddChild(rich);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;dialog.PopupCentered();
    }
    private async void CaptureFrame(string name)
    {
        if(_captureDirectory.Length==0)return;int index=_captureIndex++;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(_captureDirectory);GetViewport().GetTexture().GetImage().SavePng(_captureDirectory.PathJoin($"{index:D3}-{name}.png"));
        if(_model.Done){System.IO.File.WriteAllText(_captureDirectory.PathJoin("炉记.txt"),string.Join("\n",_model.Log)+"\n\n"+_model.Checks()+"\n"+string.Join("\n",_model.Pills.Select(p=>p.Reason)));GD.Print("ALCHEMY_DEMO_FINISHED "+_model.Result);}
    }
    private void RunChecks()
    {
        AlchemyProcessChecks.Run();
        void Verify(bool ok,string name){if(!ok)throw new InvalidOperationException(name);GD.Print("ALCHEMY_UI_CHECK_OK "+name);}
        Verify(_model.Breath==0&&_model.InFurnace.Count==0&&_relationGraph.Visible&&!_techniqueButton.Visible&&!_shortcutRow.Visible&&!_learn.Visible&&!_learnChoice.Visible,"测试入口是第0息空炉，节点网无遮挡且教程入口可用");
        var data=new Godot.Collections.Dictionary{["alchemy_herb"]="青木芯"};
        Verify(_furnace._CanDropData(Vector2.Zero,data),"丹炉接受材料拖拽数据");
        _furnace._DropData(Vector2.Zero,data);
        if(_doseDialog.Visible)_doseDialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Verify(_model.Pending.Count==1&&_model.InFurnace.Count==0&&_model.Breath==0,"确认投药只安排，不推进时间");
        _hands[AlchemyPreviewModel.Hand.Raise].EmitSignal(BaseButton.SignalName.Pressed);
        Verify(_model.Action==AlchemyPreviewModel.Hand.Raise,"加火按钮回调选定手段");
        Verify(_model.Breath==1&&_model.InFurnace.Count==1&&_model.Heat==1,"加火按钮直接执行下一息");
        _model.Done=true;
        Verify(!_furnace._CanDropData(Vector2.Zero,data),"结束后不再接受拖拽");
        Reset();Verify(_model.Breath==0&&_model.InFurnace.Count==0,"重新开炉清空试验状态");
        BeginDemo();for(int i=0;i<90&&!_model.Done;i++)DemoNext();
        Verify(_model.Done&&_model.Pills.Count==1&&_model.Pills[0].Score>=90&&_model.InFurnace.Count==8,"八味真实药材经过分支中继配伍到高品质成丹");
        GD.Print($"ALCHEMY_DEMO_RESULT breaths={_model.Breath} score={_model.Pills[0].Score} mana={_model.Mana}");
        StartTutorial(0);AdvanceTutorial();var tutorialRoute=_model.SignedLinks.Single(l=>l.UtilityId=="通");
        Verify(tutorialRoute.LastFlow>0,"教程一推进真实通路运输");AdvanceTutorial();
        Verify(tutorialRoute.Paused&&tutorialRoute.LastFlow==0,"教程一暂停后真实停流");AdvanceTutorial();Verify(!tutorialRoute.Paused&&tutorialRoute.LastFlow>0,"教程一恢复真实运输");
        StartTutorial(1);AdvanceTutorial();var trunk=_model.SignedLinks.First(l=>l.UtilityId=="通"&&l.TargetDoseId!=0);double baseline=trunk.LastFlow;
        AdvanceTutorial();Verify(trunk.LastFlow>baseline,"教程二启用行后实际增流");AdvanceTutorial();Verify(_lessonStep==3,"教程二保存反向分支权重并推进");
        StartTutorial(2);AdvanceTutorial();AdvanceTutorial();AdvanceTutorial();Verify(_model.Done&&_model.Pills.Single().Score>=90,"教程三按步骤实际炼出高品质丹");
        for(int i=0;i<AlchemyUtilityExercise.Order.Length;i++)
        {
            StartUtilityLesson(i);string utility=AlchemyUtilityExercise.Order[i];AdvanceTutorial();AdvanceTutorial();
            double baselineMetric=_utilityBaselineMetric,effectMetric=_utilityEffectMetric;AdvanceTutorial();
            bool effect=utility switch
            {
                "化" or "镇" or "护" or "生"=>effectMetric<baselineMetric-1e-8,
                "通"=>_utilityExercise!.Metric()>baselineMetric+1e-8,
                _=>effectMetric>baselineMetric+1e-8
            };
            Verify(_lessonStep==3&&effect,"十二功用教程有真实对照及后续操作："+utility);
        }
        StartTutorial(3);double busy=_model.Preview().Spirit;AdvanceTutorial();double paused=_model.Preview().Spirit;AdvanceTutorial();double staged=_model.Preview().Spirit;AdvanceTutorial();
        Verify(paused<busy&&staged<paused&&_lessonStep==3,"进阶灵识课实际降低并行负担并恢复供力");
        StartTutorial(4);AdvanceTutorial();var injured=_model.InFurnace.Single(d=>d.Herb.Name=="风络藤丝");double waste=_model.NodeSlag(injured.Id);
        AdvanceTutorial();double repaired=_model.Inventory.Where(p=>p.Location==injured.Id).Sum(p=>p.Amount*p.Damage);bool retainedWaste=_model.NodeSlag(injured.Id)>=waste-1e-8;AdvanceTutorial();
        Verify(repaired<1e-8&&retainedWaste&&_lessonStep==3,"进阶防损课：生修复能力不返渣，护在伤前生效");
        StartUtilityLesson(0);AdvanceTutorial();TryUtilityManually();var manualExercise=_utilityExercise!;
        _model.SignLink(manualExercise.SourceId,manualExercise.TargetId,"破");_hands[AlchemyPreviewModel.Hand.Hold].EmitSignal(BaseButton.SignalName.Pressed);
        Verify(!_utilityManual&&_lessonStep==2&&_utilityEffectMetric>_utilityBaselineMetric,"手动签线并点击炉令后，教程检测实际作用并推进");
        StartUtilityLesson(Array.IndexOf(AlchemyUtilityExercise.Order,"引"));AdvanceTutorial();TryUtilityManually();manualExercise=_utilityExercise!;
        _model.SignLink(manualExercise.SourceId,manualExercise.TargetId,"引");var manualGuide=_model.SignedLinks.Last();
        foreach(var route in _model.SignedLinks.Where(l=>l.UtilityId=="通"))manualGuide.BranchWeights[route]=route.TargetDoseId==0?3:1;
        _hands[AlchemyPreviewModel.Hand.Hold].EmitSignal(BaseButton.SignalName.Pressed);
        Verify(!_utilityManual&&_lessonStep==2,"手动引课核对分支设置和实际输送后推进");
        StartUtilityLesson(0);_relationGraph._Process(1);
        var dragExercise=_utilityExercise!;var graph=_relationGraph;
        var dragId=dragExercise.TargetId;var original=graph.NodePosition(dragId);var local=graph.NodeLocalPosition(dragId);
        var breathBefore=_model.Breath;var linksBefore=_model.SignedLinks.Count;var spiritBefore=_model.Preview().Spirit;
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=local});
        graph._GuiInput(new InputEventMouseMotion{Position=local+new Vector2(-100,70),ButtonMask=MouseButtonMask.Left});
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=local+new Vector2(-100,70)});
        var moved=graph.NodePosition(dragId);
        Verify(moved.DistanceTo(original)>50&&_model.Breath==breathBefore&&_model.SignedLinks.Count==linksBefore&&_model.Preview().Spirit==spiritBefore,"拖动改变位置，不误签线、不耗息数或灵识");
        Refresh();graph.SetOpen(false);graph._Process(1);graph.SetOpen(true);graph._Process(1);
        Verify(graph.NodePosition(dragId)==moved,"刷新及关闭重开保留节点布局");
        local=graph.NodeLocalPosition(dragId);
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=local});
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=local});
        Verify(_model.SignedLinks.Count==linksBefore+1,"拖动后仍能短按目标签线");
        _hands[AlchemyPreviewModel.Hand.Hold].EmitSignal(BaseButton.SignalName.Pressed);
        Verify(graph.NodePosition(dragId)==moved,"炉令结算后保留节点位置");
        var core=graph.NodePosition(0);local=graph.NodeLocalPosition(0);
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=local});
        graph._GuiInput(new InputEventMouseMotion{Position=local+new Vector2(60,40),ButtonMask=MouseButtonMask.Left});
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=local+new Vector2(60,40)});
        Verify(graph.NodePosition(0)==core&&graph.NodeLocalPosition(0).DistanceTo(_furnaceView.MouthGlobalPosition-graph.GlobalPosition)<1,"汇合节点固定对准丹炉口");
        graph.ResetLayout();Verify(graph.NodePosition(dragId)==original&&graph.NodePosition(0)==core,"整理布局恢复默认疏排");
        Verify(graph._CanDropData(Vector2.Zero,data),"展开节点网仍可接受投药拖拽");
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Right,Pressed=true,Position=Vector2.Zero});graph._Process(1);
        Verify(!graph.Visible,"右键关闭节点网");
        StartUtilityLesson(0);graph._Process(1);dragId=_utilityExercise!.SourceId;local=graph.NodeLocalPosition(dragId);
        graph._GuiInput(new InputEventMouseMotion{Position=local});graph._Process(.11);
        Verify(!graph.NodeHoverCardVisible&&graph.DisplayedInfoNodeId is null&&graph.NodeInfoLines(dragId)[0].Contains("%")&&graph.NodeInfoLines(dragId)[2].Contains("%"),"悬停不显示信息，详情的药性与进度仍用百分号");
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=local});
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=local});
        graph._GuiInput(new InputEventMouseMotion{Position=Vector2.Zero});graph._Process(.11);
        Verify(graph.NodeHoverCardVisible&&graph.DisplayedInfoNodeId==dragId,"点击显示节点信息卡，移开鼠标仍显示");
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=local});
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=local});
        graph._GuiInput(new InputEventMouseMotion{Position=Vector2.Zero});graph._Process(.11);
        Verify(!graph.NodeHoverCardVisible&&graph.DisplayedInfoNodeId is null,"再次点击同一节点取消详情常驻");
        StartUtilityLesson(Array.IndexOf(AlchemyUtilityExercise.Order,"明"));AdvanceTutorial();AdvanceTutorial();
        var diagnosed=_model.InFurnace.First(d=>d.Id==_utilityExercise!.TargetId);
        Verify(diagnosed.Diagnosis>0&&graph.NodeInfoLines(diagnosed.Id)[3].Contains($"显露 {diagnosed.Diagnosis:F0}%"),"显露进度按0至100正确显示百分号，不重复乘100");
        Verify(Math.Abs(graph.NodeDrugRatio(diagnosed.Id)-_model.NodeAvailable(diagnosed.Id)/_model.NodeMass(diagnosed.Id))<1e-9&&graph.NodeDrugRatio(0)>=0&&graph.NodeDrugRatio(0)<=1,"药性占比以节点现存物料为分母，空工位与汇合位均不越界");
        StartUtilityLesson(0);graph._Process(1);dragId=_utilityExercise!.SourceId;local=graph.NodeLocalPosition(dragId);
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=local});graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=local});graph._Process(.1);
        var utilityButton=graph.FindChild("NodeUtility_破",true,false) as Button;
        Verify(utilityButton is not null&&graph.Size.X>GetViewportRect().Size.X*.9f&&_furnaceView.Modulate.A==1,"全幅节点网保留丹炉，功用入口放在点击信息卡内");
        utilityButton!.EmitSignal(BaseButton.SignalName.Pressed);local=graph.NodeLocalPosition(_utilityExercise.TargetId);
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=local});graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=local});
        Verify(_model.SignedLinks.Any(l=>l.SourceDoseId==dragId&&l.TargetDoseId==_utilityExercise.TargetId&&l.UtilityId=="破"&&l.Repeat),"信息卡内选择功用后可点击目标签线并自动按需工作");
        var depletedModel=new AlchemyPreviewModel{Attainment=1000,MainSpirit=1000,Mana=10000};depletedModel.QueueHerb("青髓芽");depletedModel.Action=AlchemyPreviewModel.Hand.Raise;depletedModel.Advance();
        _model=depletedModel;Refresh();int spentId=_model.InFurnace[0].Id;
        Verify(graph.HasVisibleNode(spentId),"未炼完材料的节点仍显示");
        while(_model.Heat<_model.InFurnace[0].Herb.Processing.OptimalLow){_model.Action=AlchemyPreviewModel.Hand.Raise;var error=_model.Advance();if(error.Length>0)throw new InvalidOperationException(error);}
        for(int i=0;i<60&&_model.InFurnace[0].Raw>1e-6;i++){_model.Action=AlchemyPreviewModel.Hand.Hold;_model.Advance();}
        _model.RepeatingOperations.Add(new(AlchemyPreviewModel.Hand.Draw,0,spentId));
        for(int i=0;i<60&&_model.NodeAvailable(spentId)>1e-6;i++){_model.Action=AlchemyPreviewModel.Hand.Hold;_model.Advance();}
        _model.AssertConservation();Refresh();
        Verify(!graph.HasVisibleNode(spentId)&&graph.HasVisibleNode(0),"彻底耗尽的药材节点消失，炉口汇合位保留");
        LoadFactoryScenario();
        Verify(_model.PendingOperations.Any(o=>o.Kind==AlchemyPreviewModel.Hand.Draw)&&graph.VisibleUtilityIds.Length==_model.SignedLinks.Count&&graph.VisibleUtilityIds.All(AlchemyPreviewModel.Purposes.ContainsKey),"节点网只显示真实十二功用线，基本搬运保留在本息操作中");
        StartSimpleCourse();
        _lessonNext!.EmitSignal(BaseButton.SignalName.Pressed);
        while(_model.Heat<_model.Herbs.Single(h=>h.Name==AlchemySimpleLesson.MainName).Processing.OptimalLow)_hands[AlchemyPreviewModel.Hand.Raise].EmitSignal(BaseButton.SignalName.Pressed);
        for(int i=0;i<25&&_simpleCourse!.Stage<2;i++)_hands[AlchemyPreviewModel.Hand.Hold].EmitSignal(BaseButton.SignalName.Pressed);
        int simpleMain=_model.InFurnace.Single().Id;graph._Process(1);
        void ClickNode(int id)
        {
            var position=graph.NodeLocalPosition(id);
            graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=position});
            graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=position});graph._Process(.2);
        }
        ClickNode(simpleMain);((Button)graph.FindChild("NodeUtility_通",true,false)!).EmitSignal(BaseButton.SignalName.Pressed);ClickNode(0);
        var midpoint=(graph.NodeLocalPosition(simpleMain)+graph.NodeLocalPosition(0))*.5f;
        graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=midpoint});graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=midpoint});
        Verify(graph.GetChildren().OfType<AcceptDialog>().Any(d=>d.Visible)&&graph.FindChildren("*","SpinBox",true,false).Count==0,"接线不要求数值配置，点线只显示简明控制");
        foreach(var dialog in graph.GetChildren().OfType<AcceptDialog>()){dialog.Hide();dialog.QueueFree();}
        for(int i=0;i<80&&!_model.Done;i++)
        {
            int stage=_simpleCourse!.Stage;
            if(stage==4||stage==6||stage==7)
            {
                if(graph.DisplayedInfoNodeId!=0)ClickNode(0);else graph._Process(.2);
                string action=stage==4?"Purge":stage==6?"Gather":"Output";
                ((Button)graph.FindChild("Furnace_"+action,true,false)!).EmitSignal(BaseButton.SignalName.Pressed);
                if(stage==7)continue;
            }
            _hands[stage==5?AlchemyPreviewModel.Hand.Lower:AlchemyPreviewModel.Hand.Hold].EmitSignal(BaseButton.SignalName.Pressed);
        }
        int plainScore=_model.Pills.FirstOrDefault()?.Score??0;
        Verify(_model.Done&&plainScore>=20&&_model.Learned.Count==0&&_model.RepeatingOperations.Count==0,"亲手接线及炉口基础炉务可以完成第一炉，不依赖隐藏搬运或技巧");
        StartSimpleCourse(true);StartSimpleDemo();for(int i=0;i<80&&!_model.Done;i++)AdvanceSimpleStep();
        Verify(_model.Done&&_model.Pills.Single().Score>plainScore&&_model.Learned.Count==0,"辅药稳形演示使用同一基础流程，真实提升品质");
        AdvanceSimpleStep();Verify(_model.Breath==0&&!_model.Done&&_simpleCourse is null,"教程完成后可以直接开始新一炉，不留在结束锁定状态");
        StartSimpleCourse();StartSimpleDemo();AdvanceSimpleStep();var sameModel=_model;int sameBreath=_model.Breath;StartSimpleDemo();
        Verify(!_simpleAuto&&!_demonstrating&&ReferenceEquals(_model,sameModel)&&_model.Breath==sameBreath,"暂停第一炉演示保留当前炉况，不重置或暗推息数");
        for(int i=0;i<AlchemyUtilityExercise.Order.Length;i++)
        {
            StartUtilityLesson(i,true);graph._Process(1);
            var exercise=_utilityExercise!;string utility=exercise.Utility;
            Verify(_utilityManual&&graph.SelectedSourceId is null&&_lessonText!.Text.Contains("① 点击"),"功用手动课从玩家点选起点开始："+utility);
            ClickNode(exercise.SourceId);
            ((Button)graph.FindChild("NodeUtility_"+utility,true,false)!).EmitSignal(BaseButton.SignalName.Pressed);
            if(utility=="行")
            {
                var position=graph.LinkLocalPosition(exercise.Route!);
                graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=position});graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=position});
            }
            else ClickNode(exercise.TargetId);
            if(utility=="引")
            {
                var guide=_model.SignedLinks.Last(l=>l.UtilityId=="引");
                var position=graph.LinkLocalPosition(guide);
                graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=position});graph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=position});
                var dialog=graph.GetChildren().OfType<AcceptDialog>().First(d=>d.Visible);
                var prefer=dialog.FindChildren("*","Button",true,false).OfType<Button>().First(b=>b.Text.StartsWith("优先送往"));prefer.EmitSignal(BaseButton.SignalName.Pressed);dialog.Hide();
            }
            _hands[exercise.Command].EmitSignal(BaseButton.SignalName.Pressed);
            if(utility=="通")for(int guard=0;guard<8&&_utilityManual;guard++)_hands[AlchemyPreviewModel.Hand.Hold].EmitSignal(BaseButton.SignalName.Pressed);
            Verify(!_utilityManual&&_lessonStep==2,"亲手点药材、功用、目标及炉令完成课程："+utility);
        }
        Reset();GD.Print("ALCHEMY_UI_CHECKS_PASSED 77");
    }
    private static VBoxContainer Box(int gap = 14) { var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", gap); return v; }
    private static Label Text(string text, int size, Color color, bool wrap = false) { var l = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off }; l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", color); return l; }
    private static TextureRect Image(Texture2D? texture, int size) => new() { Texture = texture, CustomMinimumSize = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
    private static StyleBoxFlat Style(Color bg, Color border, int padding = 12) => new() { BgColor = bg, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2, ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding };
    private static ScrollContainer Scroll() => new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
    private static Button Button(string title, Action action) { var b = new Button { Text = title, CustomMinimumSize = new Vector2(0, 42) }; b.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge()); b.AddThemeStyleboxOverride("hover", QuietRoomStyle.Edge(true)); b.AddThemeColorOverride("font_color", Ink); b.Pressed += action; return b; }
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
}


