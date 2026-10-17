using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>新版分息炼丹试验：同一模型供手动拖拽、按钮、教学演示与自动检查使用。</summary>
public partial class AlchemyPreviewScene : Control
{
    private AlchemyPreviewModel _model=null!;
    private readonly Dictionary<string,AlchemyDragTile> _shelf=[];
    private readonly Dictionary<AlchemyPreviewModel.Hand,Button> _hands=[];
    private AlchemyDragTile _furnace=null!;
    private AlchemyFurnaceView _furnaceView=null!;
    private AlchemyPolarityGauge _yin=null!;
    private Label _yinLabel=null!;
    private OptionButton _appearance=null!;
    private AcceptDialog _settings=null!;
    private VBoxContainer _pending=null!,_inside=null!,_changes=null!;
    private Label _title=null!,_guide=null!,_temperature=null!,_condition=null!,_forecast=null!,_resources=null!,_result=null!,_task=null!,_functions=null!;
    private ProgressBar _spirit=null!;
    private OptionButton _mainChoice=null!,_operator=null!;
    private SpinBox _wanted=null!,_attainment=null!;
    private CheckButton _recover=null!,_potion=null!;
    private Button _advance=null!,_demoButton=null!,_next=null!,_learn=null!;
    private Godot.Timer _timer=null!;
    private Control _ghost=null!;
    private bool _updating,_demonstrating,_animating,_auto;
    private int _demoPhase,_generation,_captureIndex;
    private string _captureDirectory="";
    private static readonly Color Ink=new("303d38"),Dim=new("626b5b"),Gold=new("8c7049"),Jade=new("466d5c"),Red=new("a05240");
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _model=new();Theme=QuietRoomStyle.MakeTheme();
        Theme.SetColor("font_color","CheckButton",Ink);Theme.SetColor("font_disabled_color","CheckButton",Dim);
        BuildRoom();Refresh();
        var args=OS.GetCmdlineUserArgs();int at=Array.IndexOf(args,"--alchemy-capture");
        if(at>=0&&at+1<args.Length) _captureDirectory=args[at+1];
        CaptureFrame("ready");
        if(args.Contains("--alchemy-check")) { RunChecks();GetTree().Quit();return; }
        if(args.Contains("--alchemy-fast")) _timer.WaitTime=1.6;
        if(args.Contains("--alchemy-jade")){_appearance.Select(1);_furnaceView.Appearance=1;}
        if(args.Contains("--alchemy-demo")) CallDeferred(nameof(StartAutoDemo));
        if(args.Contains("--alchemy-layout")) CallDeferred(nameof(CaptureRoomLayout));
        if(args.Contains("--alchemy-fire-preview")) CallDeferred(nameof(ShowFirePreview));
    }
    private void QueueIntoFurnace(string name) { _model.Explanation=_model.QueueHerb(name);Refresh(); }
    private void SelectHand(AlchemyPreviewModel.Hand hand)
    {
        if(_demonstrating||_model.Done)return;
        _techniques.Hide();
        _model.Action=hand;_model.Explanation=$"已点击【{AlchemyPreviewModel.HandNames[(int)hand]}】。"+HandHelp(hand)+"\n悬停【推进一息】查看预计消耗，再点击执行。";Refresh();
    }
    private static string HandHelp(AlchemyPreviewModel.Hand h)=>h switch
    {
        AlchemyPreviewModel.Hand.Raise=>"提高一级供火；实际炉温会逐渐上升。",AlchemyPreviewModel.Hand.Lower=>"降低一级供火；炉内仍有余热。",
        AlchemyPreviewModel.Hand.Circulate=>"推动可用药性交流，不能代替未完成的炼化。",AlchemyPreviewModel.Hand.Separate=>"辨认并分开药性与残渣，减少排渣夹带。",
        AlchemyPreviewModel.Hand.Purge=>"排出90%现有残渣；分离不足会夹带可用药性。",AlchemyPreviewModel.Hand.Gather=>"按计划颗数汇集分配，逐渐保持成形。",
        AlchemyPreviewModel.Hand.Output=>"结束本炉，逐颗检查组成、剂量和成形。",AlchemyPreviewModel.Hand.Fine=>"专注精细照管，更快取得高品能力；不会自动加火。",_=>"维持供火，各批材料继续受热和变化。"
    };
    private void PlanChanged()
    {
        if(_updating)return;_model.Helper=_operator.Selected==1;_model.Recover=_recover.ButtonPressed;_model.TakePotion=_potion.ButtonPressed;Refresh();
    }
    private void Advance()
    {
        if(_demonstrating)return;string action=AlchemyPreviewModel.HandNames[(int)_model.Action];string error=_model.Advance();
        _model.Explanation=error.Length>0?error:$"已点击【推进一息】，执行【{action}】。"+(_model.Done?"点击【丹方判定】查看各颗结果。":"双击丹炉可查看变化；可以继续投药或选择另一种手段。");
        Refresh();CaptureFrame("manual");
    }
    private void Refresh()
    {
        _updating=true;_title.Text=$"丹 室   ·   第 {_model.Breath} 息";_guide.Text=_model.Explanation;
        _mainChoice.Disabled=_demonstrating||_model.InFurnace.Count+_model.Pending.Count>0;_mainChoice.Select(0);
        _attainment.Value=_model.Attainment;_attainment.Editable=!_demonstrating;_wanted.Value=_model.Wanted;_wanted.Editable=!_demonstrating&&!_model.Done;
        _learn.Text=_model.FineLearned?"已学会精细控火":"学习精细控火（造诣24）";_learn.Disabled=_demonstrating||_model.FineLearned;
        _temperature.Text=$"火势 {(_model.ActualFire<.01?"无火":_model.ActualFire<1.25?"微火":_model.ActualFire<2.25?"文火":_model.ActualFire<3.25?"旺火":"烈火")}    炉内{(_model.Temperature>70?"灼热":_model.Temperature>50?"热":_model.Temperature>30?"温热":"微温")}";
        _temperature.TooltipText=$"实际供火 {_model.ActualFire:F2} 档，供火指令 {_model.Heat:F0} 档；法力不足时实际供火会降低。\n炉温 {_model.Temperature:F1}（只读）；动画不独立推进游戏时间。";
        double perPill=_model.MainAmount/Math.Max(1,_model.Wanted);
        double mainDone=_model.InFurnace.Where(d=>d.Kind=="主药").Select(d=>d.Released).DefaultIfEmpty(0).Average();
        _condition.Text=$"主药已炼化 {mainDone:P0}  ·  药性混匀 {_model.Fusion:P0}  ·  丹药成形 {_model.Shape:P0}";
        _task.Text=$"准备 {_model.Wanted} 颗 · 每颗平均主药药性 {perPill:F1} 份 · 药渣 {_model.Slag:F1} 份";
        _task.TooltipText=$"每颗至少8份；剂量加分到24份封顶，超过部分不再加分。\n现在排渣预计带走 {_model.CarryRisk:F2} 份药性。\n以上为旧体验参数，正式成品标准尚待接入。";
        _yinLabel.Text=_model.Available<.001?"阴阳 · 暂无已炼出的药性":$"阴阳 {_model.Polarity:+0.00;-0.00;0.00} · {(Math.Abs(_model.Polarity)<.1?"较平衡":_model.Polarity>0?"偏阳":"偏阴")}  |  本方允许 −0.75～+0.75";
        _yin.Value=_model.Polarity;_yin.HasMedicine=_model.Available>.001;_yin.QueueRedraw();
        _furnaceView.SetState(_model.ActualFire,_model.Temperature);
        _attainment.TooltipText=$"当前控炉者造诣{_model.OperatorAttainment}：本息最多投{Math.Max(1,_model.OperatorAttainment/8)}份药材；同时凝丹最多{_model.OperatorAttainment/8*3}颗。\n精细控火：学习与施展均需造诣24，达到资格后仍需主动学习。";
        _resources.Text=$"本人法力 {_model.Mana}/160 · 造诣 {_model.Attainment}\n副手法力 {_model.HelperMana}/110 · 回灵散 {_model.Potions}份";
        _operator.Select(_model.Helper?1:0);_recover.SetPressedNoSignal(_model.Recover);_potion.SetPressedNoSignal(_model.TakePotion);
        var f=_model.Preview();_spirit.MaxValue=100;_spirit.Value=Math.Min(100,f.Ratio*100);_spirit.ShowPercentage=false;
        _spirit.TooltipText=$"下一息灵识占用 {f.Spirit:F1}/{f.Capacity}；超过80%不支，超过100%通常炸炉。";
        _forecast.Text=$"下一息：法力−{f.Mana} · 灵识 {f.Ratio:P0}\n"+(f.Ratio>1?"超载：本次推进会炸炉。":f.Ratio>.8?"不支：精细程度下降。":"可稳定控制。")+"\n当前手段："+AlchemyPreviewModel.HandNames[(int)_model.Action];
        if(_model.Done) _forecast.Text="本炉已结束。\n点击丹方判定查看每颗的计算与原因。";
        _forecast.AddThemeColorOverride("font_color",f.Ratio>.8?Red:Jade);
        foreach(var h in _model.Herbs){_shelf[h.Name].TooltipText=HerbInfo(h,null);_shelf[h.Name].Modulate=Colors.White;_shelf[h.Name].AddThemeStyleboxOverride("panel",Style(h.Name==_model.MainName?new Color(.80f,.85f,.74f,.70f):new Color(.96f,.94f,.86f,.45f),h.Name==_model.MainName?Jade:new Color(.56f,.53f,.43f,.35f),5));}
        Clear(_pending);var pg=new HBoxContainer();_pending.AddChild(pg);
        foreach(var d in _model.Pending){var b=HerbButton(d.Herb,"点击撤回本份投料");b.Disabled=_demonstrating;b.Pressed+=()=>{_model.Pending.Remove(d);Refresh();};pg.AddChild(b);}
        if(_model.Pending.Count==0)_pending.AddChild(Text("还没有安排投料",17,Dim));
        Clear(_inside);var ig=new GridContainer{Columns=6};_inside.AddChild(ig);
        foreach(var d in _model.InFurnace){string info=HerbInfo(d.Herb,d);var b=HerbButton(d.Herb,info);b.Text=$"{d.Batch}息";b.Pressed+=()=>ShowMessage(d.Herb.Name,info);ig.AddChild(b);}
        if(_model.InFurnace.Count==0)_inside.AddChild(Text("炉内尚无药材",18,Dim));
        var active=_model.Contributions.Keys.OrderByDescending(k=>k.StartsWith(_model.MainName+"·")).Take(3).ToArray();
        _functions.Text=active.Length==0?"本息没有功用产生新作用；点击本息功用查看原因。":
            "本息发挥："+string.Join("；",active.Select(k=>k+"（"+AlchemyPreviewModel.Purposes[k.Split('·').Last()]+"）"));
        _functions.TooltipText="点击炉内详情中的查看每项功用，了解实际作用及未生效原因。";
        Clear(_changes);
        foreach(string line in _model.Changes.Distinct().TakeLast(6))_changes.AddChild(Text(line,18,Ink,true));
        if(_model.Changes.Count==0)_changes.AddChild(Text("每息后显示实际变化；详细炉记可看所有功用来源。",18,Dim,true));
        _result.Text=_model.Result+(_model.Pills.Count==0?"":"\n"+string.Join("\n",_model.Pills.Select(p=>$"{p.Index}号：{p.Score} · {AlchemyPreviewModel.Tier(p.Score)}")));
        foreach(var pair in _hands){pair.Value.Disabled=_demonstrating||_model.Done||(pair.Key==AlchemyPreviewModel.Hand.Fine&&!_model.FineLearned);pair.Value.Modulate=Colors.White;pair.Value.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=Colors.Transparent,BorderWidthBottom=pair.Key==_model.Action?2:0,BorderColor=Jade});pair.Value.AddThemeColorOverride("font_disabled_color",pair.Key==_model.Action?Gold:Dim);pair.Value.AddThemeStyleboxOverride("disabled",new StyleBoxEmpty());}
        _advance.Disabled=_model.Done||_demonstrating;_operator.Disabled=_demonstrating||_model.Done;_recover.Disabled=_demonstrating||_model.Done;_potion.Disabled=_demonstrating||_model.Done||_model.Potions==0;
        _demoButton.Text=_demonstrating?"暂停演示，自己接手":"自动讲解演示";_next.Text=_demonstrating?"下一步演示 →":"逐步看演示 →";_next.Disabled=_animating;
        UpdateRoomState();_updating=false;
    }
    private string HerbInfo(AlchemyPreviewModel.Herb h,AlchemyPreviewModel.Dose? d)
    {
        var p=AlchemyPreviewModel.Profile(h.Craft.texture);
        string s=$"{h.Name} · {(h.Name==_model.MainName?"主药":"辅药")}\n五行：{(h.Craft.element.Length==0?"无":h.Craft.element)} · {h.Craft.texture}质 · {(h.Craft.polarity>0?"阳":h.Craft.polarity<0?"阴":"和")}\n质地处理参考：开始炼化{p.OpenHeat:F0}，有效受热约{p.Ideal:F0}，超过{p.Safe:F0}有热伤。\n炼出后主要呈{p.State}。";
        if(d is not null)s+=$"\n\n第{d.Batch}息投料；已处理{d.Released:P0}\n未处理{d.Raw:F1} / 可用{d.Available:F1} / 残渣{d.Slag:F1}\n过程投入{d.Spent:F2} / 烧毁{d.Destroyed:F2} / 散失{d.Escaped:F2} / 排出{d.Expelled:F2}\n可修复损伤{d.Disorder:P0}；分离{d.Separation:P0}\n总量核对{d.Balance:F2}/100";
        foreach(var u in h.Craft.utilities)
        {
            s+=$"\n\n{u.utility_id}{MaterialCraftData.utility_grade_name(u.grade)}：{AlchemyPreviewModel.Purposes[u.utility_id]}";
            if(d is not null){s+=$"\n目前能发挥约{d.Readiness(u.utility_id):P0}的潜在能力";s+="\n"+_model.FunctionStatus(h,u.utility_id);}
        }
        if(d is null&&_model.Temperature>p.Safe)s+="\n\n当前炉内过热，立即投入可能损伤。";
        return s;
    }
    private Button HerbButton(AlchemyPreviewModel.Herb h,string tooltip)
    {
        var b=new Button{Icon=GD.Load<Texture2D>(h.Item.icon_path),ExpandIcon=true,CustomMinimumSize=new Vector2(76,70),TooltipText=tooltip,IconAlignment=HorizontalAlignment.Center};b.AddThemeConstantOverride("icon_max_width",42);b.AddThemeStyleboxOverride("normal",Style(new Color(.92f,.92f,.83f,.6f),new Color(.56f,.53f,.43f,.35f),4));return b;
    }
    private void Reset()
    {
        _generation++;_timer.Stop();_demonstrating=_animating=_auto=false;_ghost.Hide();_bag.Hide();_techniques.Hide();_doseDialog.Hide();_demoPhase=0;_model=new();Refresh();
    }
    public void StartAutoDemo()
    {
        if(_demonstrating){_generation++;_timer.Stop();_demonstrating=_auto=_animating=false;_ghost.Hide();_model.Explanation="已暂停，当前炉况保留。你可以自己拖药、选择手段，再推进一息。";Refresh();return;}
        BeginDemo();_auto=true;_timer.Start();DemoNext();
    }
    private void NextDemo(){if(!_demonstrating)BeginDemo();_auto=false;_timer.Stop();DemoNext();}
    private void BeginDemo(){Reset();_demonstrating=true;_model.Explanation="演示开始：每次先提示拖药或点击的按钮，执行后显示本息变化。";Refresh();}
    private void DemoNext()
    {
        if(!_demonstrating||_animating)return;
        if(_model.Done){_timer.Stop();_demonstrating=_auto=false;Refresh();CaptureFrame("finished");return;}
        switch(_demoPhase)
        {
            case 0: AnimateDose("青木芯","第一批：拖【青木芯】到丹炉。它是回春丹主药，通帮助混匀，生保留作修复基础。",1);return;
            case 1: DemoHand(AlchemyPreviewModel.Hand.Raise,"点【加火】→【推进一息】：升到文火，草木药材不需要烈火猛烧。",2);return;
            case 2:
                if(_model.InFurnace.First(d=>d.Kind=="主药").Released<.25){DemoHand(AlchemyPreviewModel.Hand.Hold,"点【维持】→【推进一息】：让青木芯逐渐炼出药性，主药的通也开始发挥。",2);return;}
                AnimateDose("灵泉露","第二批：拖【灵泉露】入炉。化帮助尚未炼完的材料继续炼化，聚会在凝丹时帮助汇集。",4);return;
            case 4: DemoHand(AlchemyPreviewModel.Hand.Hold,"点【维持】→【推进一息】：灵泉露开始炼化，之后可以帮助主药；不是入炉就满额发挥。",5);return;
            case 5: AnimateDose("凝露松脂","第三批：拖【凝露松脂】入炉。固会在凝丹时帮助保持形状；它现在仍需炼化。",6);return;
            case 6: DemoHand(AlchemyPreviewModel.Hand.Circulate,"点【搅动药性】→【推进一息】：已炼出的部分交流混匀，其余材料同时继续炼化。",7);return;
            case 7: AnimateDose("苦苔根","第四批：拖【苦苔根】入炉。藏帮助减少药性流失，阴性组成与松脂的阳性相配。",8);return;
            case 8: DemoHand(AlchemyPreviewModel.Hand.Hold,"点【维持】→【推进一息】：让苦苔根逐步炼化；双击丹炉，查看每项功用是否发挥。",9);return;
            case 9:
                _model.Explanation="演示在此学习【精细控火】。自己操作时，到【设置】中学习；资格需要造诣24。";_model.LearnFine();_demoPhase=10;Refresh();CaptureFrame("learn");return;
            case 10:
                if(_model.InFurnace.Any(d=>d.Raw>2)||_model.InFurnace.First(d=>d.Herb.Name=="青木芯").Readiness("生")<.95){DemoHand(AlchemyPreviewModel.Hand.Fine,"点【精细控火】→【推进一息】：以更多灵识照管高品能力，不靠继续加火。",10);return;}
                _demoPhase=11;goto case 11;
            case 11: DemoHand(AlchemyPreviewModel.Hand.Separate,"点【分离】→【推进一息】：把可用部分与残渣分开，观察预计夹带下降。",12);return;
            case 12: DemoHand(AlchemyPreviewModel.Hand.Separate,"再点【分离】→【推进一息】：更充分分离后再排，保留更多可用部分。",13);return;
            case 13: DemoHand(AlchemyPreviewModel.Hand.Purge,"点【排渣】→【推进一息】：只排现有残渣；未炼开的材料仍留在炉内。",14);return;
            case 14:
                if(_model.Fusion<.96){DemoHand(AlchemyPreviewModel.Hand.Circulate,"点【搅动药性】→【推进一息】：提高混合与分配均匀程度，减少同炉各丹差异。",14);return;}
                _demoPhase=15;goto case 15;
            case 15:
                if(_model.Shape<.97){DemoHand(AlchemyPreviewModel.Hand.Gather,"点【凝丹】→【推进一息】：按3颗分配；松脂的固帮助保持形状，灵泉露的聚帮助汇集。",15);return;}
                _demoPhase=16;goto case 16;
            case 16: DemoHand(AlchemyPreviewModel.Hand.Output,"点【出炉】→【推进一息】：逐颗判定，不以留存率代替丹效；点击丹方判定看试算明细。",17);return;
        }
    }
    private async void DemoHand(AlchemyPreviewModel.Hand hand,string explanation,int next)
    {
        _animating=true;int gen=_generation;_model.Action=hand;_model.Helper=_model.Recover=false;
        _bag.Hide();_techniques.Visible=hand is not (AlchemyPreviewModel.Hand.Hold or AlchemyPreviewModel.Hand.Raise or AlchemyPreviewModel.Hand.Lower);
        if(_model.Mana<28&&_model.HelperMana>14){_model.Helper=_model.Recover=true;explanation="切换【副手控炉】并勾选【本人回气】，副手维持供火。\n"+explanation;}
        if(!_model.Helper&&_model.Mana<16&&_model.Potions>0){_model.TakePotion=true;explanation="勾选【本息服回灵散】，当场补充法力。\n"+explanation;}
        _model.Explanation=explanation;Refresh();_hands[hand].Modulate=new Color(1.6f,1.3f,.65f);CaptureFrame("before");
        await ToSignal(GetTree().CreateTimer(.9),SceneTreeTimer.SignalName.Timeout);
        if(gen!=_generation)return;
        _techniques.Hide();
        _advance.AddThemeColorOverride("font_disabled_color",Gold);
        _advance.Modulate=new Color(1.6f,1.3f,.65f);CaptureFrame("advance");
        await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
        if(gen!=_generation)return;
        string error=_model.Advance();_advance.Modulate=Colors.White;
        _advance.RemoveThemeColorOverride("font_disabled_color");
        if(error.Length>0){_model.Explanation=error;_timer.Stop();_demonstrating=false;}else{_model.Explanation=explanation+"\n已执行；双击丹炉可查看本息结果。";_demoPhase=next;}
        _animating=false;Refresh();CaptureFrame("after");
        if(_model.Breath>90){_timer.Stop();_demonstrating=false;_model.Explanation="演示保护停止：请查看炉记。";Refresh();}
    }
    private void AnimateDose(string name,string explanation,int next)
    {
        _bag.Show();
        _animating=true;int gen=_generation;_model.Explanation=explanation;Refresh();
        _ghost.Position=_shelf[name].GlobalPosition;((TextureRect)_ghost.GetChild(0)).Texture=_shelf[name].Icon;_ghost.Show();CaptureFrame("drag");
        var tween=CreateTween();tween.TweenProperty(_ghost,"position",_furnace.GlobalPosition+_furnace.Size*.5f-new Vector2(35,35),1.1).SetTrans(Tween.TransitionType.Sine);
        tween.TweenCallback(Callable.From(()=>{if(gen!=_generation)return;_ghost.Hide();string message=_model.QueueHerb(name);_model.Explanation=explanation+"\n"+message;_demoPhase=next;_animating=false;Refresh();CaptureFrame("queued");}));
    }
    private void ShowTutorial()=>ShowMessage("怎样开始", "1. 点击左下行囊，拖药到中央丹炉，确认份数。待投材料可在行囊中点击撤回；丹方卷轴可调整计划产量。\n2. 点击维持、加火、减火，或展开技法选择手段；悬停推进按钮查看消耗，双击丹炉查看详细炉况。\n3. 点击推进一息，执行所选手段并让所有批次共同变化。\n4. 根据余热、未处理部分和能力情况继续投药，不需要切换阶段。\n5. 分离后排渣，搅动药性改善混合，凝丹时按计划颗数分配。可减少计划颗数保效，单颗有上限。\n6. 出炉结束，查看丹方判定的逐颗原因。\n\n自动讲解会显示每次拖药和点击；逐步演示每次只执行一个动作。随时点暂停接手。\n\n法力提供供火；灵识是占用，>80%不支、>100%本试验通常炸炉。副手顶炉时可回气，也可服药。\n\n高品能力要求更充分的取得与精细照管。造诣控制操作规模及学习资格，不直接给丹效加分。参数均为本次试验。" );
    private void ShowActiveUtilities()=>ShowMessage("这一息，各味药发挥了什么",string.Join("\n\n",_model.InFurnace.Select(d=>d.Herb).Distinct().SelectMany(h=>h.Craft.utilities.Select(u=>$"{h.Name} · {u.utility_id}{MaterialCraftData.utility_grade_name(u.grade)}\n{_model.FunctionStatus(h,u.utility_id)}"))));
    private void ShowUtilities()=>ShowMessage("十二功用 · 按材料原表", "主辅使用同一规则，功用是共用药性的能力。没有对象不产生作用消耗；已形成结构不每息重复扣。\n\n"+string.Join("\n\n",AlchemyPreviewModel.Purposes.Select(p=>p.Key+"："+p.Value))+"\n\n各项实际来源与投入见详细炉记、炉内药材详情。" );
    private void ShowChecks()=>ShowMessage("回春丹试方 · 成丹要求与结果",_model.Checks()+"\n\n"+string.Join("\n\n",_model.Pills.Select(p=>$"{p.Index}号：主药{p.MainAmount:F2}，{p.Score}·{AlchemyPreviewModel.Tier(p.Score)}\n{p.Reason}")));
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
        AlchemyPreviewChecks.Run();
        void Verify(bool ok,string name){if(!ok)throw new InvalidOperationException(name);GD.Print("ALCHEMY_UI_CHECK_OK "+name);}
        var data=new Godot.Collections.Dictionary{["alchemy_herb"]="青木芯"};
        Verify(_furnace._CanDropData(Vector2.Zero,data),"丹炉接受材料拖拽数据");
        _furnace._DropData(Vector2.Zero,data);
        if(_doseDialog.Visible)_doseDialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Verify(_model.Pending.Count==1&&_model.Pending[0].Kind=="主药","拖拽回调进入待投区并识别主药");
        _hands[AlchemyPreviewModel.Hand.Raise].EmitSignal(BaseButton.SignalName.Pressed);
        Verify(_model.Action==AlchemyPreviewModel.Hand.Raise,"加火按钮回调选定手段");
        _advance.EmitSignal(BaseButton.SignalName.Pressed);
        Verify(_model.Breath==1&&_model.InFurnace.Count==1&&_model.Heat==2,"推进按钮执行投药与加火");
        _model.Done=true;
        Verify(!_furnace._CanDropData(Vector2.Zero,data),"结束后不再接受拖拽");
        Reset();Verify(_model.Breath==0&&_model.InFurnace.Count==0,"重新开炉清空试验状态");
        GD.Print("ALCHEMY_UI_CHECKS_PASSED 6");
    }
    private static VBoxContainer Box(int gap = 14) { var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", gap); return v; }
    private static Label Text(string text, int size, Color color, bool wrap = false) { var l = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off }; l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", color); return l; }
    private static TextureRect Image(Texture2D? texture, int size) => new() { Texture = texture, CustomMinimumSize = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
    private static StyleBoxFlat Style(Color bg, Color border, int padding = 12) => new() { BgColor = bg, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2, ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding };
    private static ScrollContainer Scroll() => new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
    private static Button Button(string title, Action action) { var b = new Button { Text = title, CustomMinimumSize = new Vector2(0, 42) }; b.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge()); b.AddThemeStyleboxOverride("hover", QuietRoomStyle.Edge(true)); b.AddThemeColorOverride("font_color", Ink); b.Pressed += action; return b; }
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
}


