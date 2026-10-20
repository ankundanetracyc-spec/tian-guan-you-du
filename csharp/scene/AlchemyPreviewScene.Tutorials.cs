using Godot;
using System;
using System.Linq;

public partial class AlchemyPreviewScene
{
    private PanelContainer? _lessonPanel;
    private Label? _lessonTitle,_lessonText;
    private Button? _lessonNext;
    private int _lessonIndex,_lessonStep;
    private bool _lessonActive;
    private bool _advancedMode;
    private int _advancedIndex,_advancedRunBreaths;
    private string _advancedTitle="",_advancedIntro="";
    private Button? _lessonTakeoverButton;
    private TabContainer? _courseTabs;
    private static readonly string[] LessonTitles=["进阶一 · 运输与停机","进阶二 · 分流与增流","进阶三 · 高品质配伍","进阶四 · 灵识调度","进阶五 · 防损与修复"];
    private static readonly string[][] LessonNotes=
    [
        ["只看两味药：青髓芽提供主药，风络藤丝提供通。\n\n青髓芽→藤丝标着‘牵药’，是本息灵识操作；藤丝→汇合位是建好的通路。\n\n下一步：守炉一息，看通路基础导流。",
         "通路无需行也能缓流。线上‘本息流量／容量’显示真实运输，节点药量随之减少或增加。\n\n下一步：暂停通路，再守炉一息，看上游积料。",
         "运输暂停，灵识线位释放。基本牵药仍在向藤丝送料，因此藤丝积药，汇合位没有新到货。\n\n下一步：恢复通路，再守炉一息。",
         "通路恢复，药性继续流动。\n\n你已经看到‘投料来源→基本送料→通路运输→汇合位’。接手后可点牵药线停料、点通线暂停或撤线。"],
        ["五味药组成两条支路：藤丝直达中心，或先到溪纹藻丝再送入中心。\n\n此时暂停行与引，先看两条路线自身的缓流。\n\n下一步：守炉一息，记下本息流量。",
         "现在是未增流的状态。\n\n乌羽片的行可以作用于‘藤丝→藻丝’这条通路；雾铃花的引可以控制藤丝的两个出口。\n\n下一步：启用行、引，以3∶1权重分流，再守炉一息。",
         "行推动首段，实际增流会标在线上。下游藻丝的出口仍有自己的容量，入口快、出口慢就会积料。\n\n引的比例受各段容量限制，并非无条件强行三比一。\n\n下一步：把权重改成1∶3，再守炉一息。",
         "分支偏向已改变。点引线可以继续修改权重，点行线可以暂停供力。\n\n试着比较：只加速上游，与同时照顾下游出口，哪个更有效？你可保留当前炉局接手。"],
        ["八味完整产线：主药生机＋回泉珠的化＋凝霜叶的藏，沿主干分流运输；松脂在中心施固，回泉珠另留一部分聚药。\n\n下一步：连续守炉三息，观察多来源入流。",
         "只算接线没有用：药性要真正经过各段，进入汇合位。供力投入、药渣和结构绑定各自留账。\n\n下一步：继续运行到药性汇齐，完成混匀、分离排渣并开始凝丹。",
         "丹体已有真实来源，此时检查生、固、化、藏与混匀、夹渣、稳定的共同缺口。\n\n下一步：按同一模型继续加工，达标后出炉。",
         "这颗丹的品质来自实际配伍与加工。当前单味理想状态最高50分；缺化不能超过65，缺藏不能超过75。\n\n可重载炉局尝试去掉一味、停止某段运输，观察成丹缺口。"],
        ["八味产线同时送料、供力、调度、聚药与稳形。先看灵识：持续线位＋本息操作＝峰值。\n\n下一步：暂停行与引，再守炉。",
         "辅助线暂停，原有通路仍能缓流。灵识压力降低，运输变慢，等待仍耗法力。\n\n下一步：停止化与藏辅药的重复送料，保留主药送料。",
         "减少同时照管的操作，进一步降低灵识峰值。\n\n下一步：恢复行与引，比较分批送料后的负荷。",
         "按阶段切换安排：先送料，再处理风险，再分离稳形。\n\n接手后保留必要通路，把本息不用的辅助线暂停。"],
        ["本课操作者造诣40，可控3档。藤丝安全上限2档。下一步没有护时添焰到3档，观察实际热伤和转渣。",
         "损伤已进入来源账。\n\n下一步：敛焰回安全火候，同时让青髓芽以生修复藤丝；比较能力损伤与残渣。",
         "生修复尚存部分的能力，已转渣的质量保留在残渣中。\n\n下一步：重开相同初始教学炉况，先护再添焰。",
         "护在伤前、生在伤后，二者都要真实来源投入。\n\n当前保留护住本次热伤的炉况，可接手敛焰或继续观察。"]
    ];
    private void ShowTutorial()
    {
        var chooser=new AcceptDialog{Title="丹道入门与进阶",Size=new Vector2I(650,650)};AddChild(chooser);
        chooser.GetOkButton().Text="关闭";chooser.GetOkButton().AddThemeStyleboxOverride("normal",QuietRoomStyle.Paper(1,8));
        var paper=new PanelContainer{CustomMinimumSize=new Vector2(620,560)};paper.AddThemeStyleboxOverride("panel",QuietRoomStyle.Paper(1,16));chooser.AddChild(paper);
        var box=Box(12);paper.AddChild(box);box.AddChild(Text("先布置药材与功用线，再点一次开炉。功用课逐项练习；进阶课讲清灵识、法力、配伍与复杂炉网。",18,Ink));
        var tabs=new TabContainer{CustomMinimumSize=new Vector2(580,430),SizeFlagsVertical=SizeFlags.ExpandFill};box.AddChild(tabs);
        _courseTabs=tabs;
        tabs.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());var tabBar=tabs.GetTabBar();
        tabBar.AddThemeColorOverride("font_selected_color",Ink);tabBar.AddThemeColorOverride("font_unselected_color",Dim);
        tabBar.AddThemeStyleboxOverride("tab_selected",QuietRoomStyle.Edge(true));tabBar.AddThemeStyleboxOverride("tab_unselected",QuietRoomStyle.Edge());
        var first=Box(14);first.Name="第一炉";tabs.AddChild(first);
        first.AddChild(Text("不学技巧，不填线路参数。\n用真实材料、功用接线与基础炉务完成一炉。",20,Ink,true));
        first.AddChild(Button("第一炉 · 单味通脉丹",()=>{chooser.QueueFree();StartSimpleCourse();}));
        first.AddChild(Button("再试一炉 · 加入辅药稳形",()=>{chooser.QueueFree();StartSimpleCourse(true);}));
        first.AddChild(Button("看一炉演示 · 从空炉到出炉",()=>{chooser.QueueFree();StartSimpleCourse();StartSimpleDemo();}));
        var basic=Scroll();basic.Name="十二功用";tabs.AddChild(basic);var basics=Box(9);basics.SizeFlagsHorizontal=SizeFlags.ExpandFill;basic.AddChild(basics);
        basics.AddChild(Text("十二课分别练习一种功用。\n材料已备好，从点药材、选功用开始亲手接线；随后点一次开炉，看自动结算的对照结果。",18,Ink,true));
        basics.AddChild(Button("从第一课开始 · 亲手练习十二功用",()=>{chooser.QueueFree();StartUtilityLesson(0,true);}));
        for(int i=0;i<AlchemyUtilityExercise.Order.Length;i++){int index=i;string u=AlchemyUtilityExercise.Order[i];basics.AddChild(Button($"{i+1:D2} · {u} · {AlchemyUtilityExercise.LessonName(u)}"+(_completedUtilities.Contains(u)?"（已修）":""),()=>{chooser.QueueFree();StartUtilityLesson(index,true);}));}
        var advanced=Scroll();advanced.Name="进阶";tabs.AddChild(advanced);var lessons=Box(10);lessons.SizeFlagsHorizontal=SizeFlags.ExpandFill;advanced.AddChild(lessons);
        lessons.AddChild(Text("进阶课继续使用预设节点网与一次开炉。它们展示资源调度、配伍和当前系统边界，不增加每息操作或支路参数。",18,Ink,true));
        lessons.AddChild(Button("01 · 灵识：并行与轮候",()=>{chooser.QueueFree();StartAdvancedLesson(0);}));
        lessons.AddChild(Button("02 · 法力：火候与安全收炉",()=>{chooser.QueueFree();StartAdvancedLesson(1);}));
        lessons.AddChild(Button("03 · 多味配伍：辅药如何补足成品",()=>{chooser.QueueFree();StartSimpleCourse(true);}));
        lessons.AddChild(Button("04 · 五行与阴阳：看镇课的真实反应",()=>{chooser.QueueFree();StartUtilityLesson(Array.IndexOf(AlchemyUtilityExercise.Order,"镇"),true);}));
        lessons.AddChild(Button("05 · 简单与复杂炉网",()=>{chooser.QueueFree();ShowComplexNetworkLesson();}));
        chooser.Confirmed+=chooser.QueueFree;chooser.Canceled+=chooser.QueueFree;chooser.PopupCentered();
    }
    private void StartTutorial(int lesson)
    {
        Reset();_utilityMode=false;_advancedMode=false;_lessonIndex=lesson;_lessonStep=0;_lessonActive=true;
        _model=lesson==4?AlchemyUtilityExercise.Create("护").Model:AlchemyFactoryScenario.Create(Math.Min(lesson,2));
        if(lesson==1)
        {
            int feed=_model.InFurnace.Single(d=>d.Herb.Name=="风络藤丝").Id;
            var direct=_model.SignedLinks.Single(l=>l.UtilityId=="通"&&l.SourceDoseId==feed&&l.TargetDoseId==0);
            _model.SignFlowSupport(_model.InFurnace.Single(d=>d.Herb.Name=="乌羽片").Id,direct);
        }
        if(lesson==1)foreach(var l in _model.SignedLinks.Where(l=>l.UtilityId is "行" or "引")){l.Paused=true;_model.PlannedLinks.Remove(l);}
        EnsureLessonPanel();_lessonPanel!.Show();Refresh();_relationGraph.SetOpen(true);RefreshLesson();
    }
    private void EnsureLessonPanel()
    {
        if(_lessonPanel is null)
        {
            _lessonPanel=new PanelContainer{ZIndex=20};_lessonPanel.AddThemeStyleboxOverride("panel",QuietRoomStyle.Paper(.98f,16));AddChild(_lessonPanel);
            _lessonPanel.AnchorLeft=.025f;_lessonPanel.AnchorRight=.245f;_lessonPanel.AnchorTop=.19f;_lessonPanel.AnchorBottom=.76f;
            var box=Box(14);_lessonPanel.AddChild(box);_lessonTitle=Text("",22,Jade,true);box.AddChild(_lessonTitle);
            var content=Scroll();content.SizeFlagsVertical=SizeFlags.ExpandFill;content.CustomMinimumSize=new Vector2(0,200);box.AddChild(content);
            _lessonText=Text("",19,Ink,true);_lessonText.SizeFlagsHorizontal=SizeFlags.ExpandFill;content.AddChild(_lessonText);
            _lessonNext=Button("下一步",AdvanceTutorial);box.AddChild(_lessonNext);
            _utilityTryButton=Button("按指引亲自试",TryUtilityManually);box.AddChild(_utilityTryButton);_utilityTryButton.Hide();
            _utilityDemoButton=Button("看本课接线演示",DemonstrateUtility);box.AddChild(_utilityDemoButton);_utilityDemoButton.Hide();
            _utilityRetryButton=Button("重练本课 · 重新接线",()=>StartUtilityLesson(_utilityIndex,true));box.AddChild(_utilityRetryButton);_utilityRetryButton.Hide();
            _lessonTakeoverButton=Button("自己接手",TakeOverLesson);box.AddChild(_lessonTakeoverButton);
            box.AddChild(Button("选择其他教程",ShowTutorial));
        }
    }
    private void RefreshLesson(string feedback="")
    {
        if(_lessonPanel is null||!_lessonActive)return;
        if(_lessonTakeoverButton is not null)_lessonTakeoverButton.Disabled=_autoRunInProgress;
        if(_simpleCourse is not null){RefreshSimpleLesson(feedback);return;}
        if(_utilityMode){RefreshUtilityLesson(feedback);return;}
        if(_advancedMode){RefreshAdvancedLesson(feedback);return;}
        _utilityTryButton?.Hide();_utilityDemoButton?.Hide();_utilityRetryButton?.Hide();_lessonNext!.Disabled=false;
        _lessonTitle!.Text=LessonTitles[_lessonIndex]+$"\n{_lessonStep+1}／4";
        _lessonText!.Text=LessonNotes[_lessonIndex][_lessonStep]+(feedback.Length>0?"\n\n"+feedback:"");
        _lessonNext!.Text=_lessonStep==3?"本课完成 · 自己接手":"下一步";
    }
    private void AdvanceTutorial()
    {
        if(!_lessonActive)return;
        if(_simpleCourse is not null){AdvanceSimpleStep();return;}
        if(_utilityMode){AdvanceUtilityLesson();return;}
        if(_advancedMode){AdvanceAdvancedLesson();return;}
        if(_lessonStep==3){_lessonActive=false;_lessonPanel!.Hide();return;}
        int before=_model.Breath;
        if(_lessonIndex==0)
        {
            var route=_model.SignedLinks.Single(l=>l.UtilityId=="通");
            if(_lessonStep is 1 or 2)_model.PauseLink(route);
            _model.Action=AlchemyPreviewModel.Hand.Hold;string error=_model.Advance();if(error.Length>0){RefreshLesson(error);return;}
        }
        else if(_lessonIndex==1)
        {
            if(_lessonStep==1)foreach(var l in _model.SignedLinks.Where(l=>l.UtilityId is "行" or "引")){l.Paused=false;l.Repeat=true;}
            if(_lessonStep==2)
            {
                var guide=_model.SignedLinks.Single(l=>l.UtilityId=="引");
                foreach(var route in guide.BranchWeights.Keys.ToArray())guide.BranchWeights[route]=route.TargetDoseId==0?3:1;
            }
            _model.Action=AlchemyPreviewModel.Hand.Hold;string error=_model.Advance();if(error.Length>0){RefreshLesson(error);return;}
        }
        else if(_lessonIndex==2)
        {
            _demonstrating=true;
            if(_lessonStep==0)for(int i=0;i<3;i++)DemoNext();
            else if(_lessonStep==1)for(int i=0;i<40&&!_model.Done&&_model.Pills.Count==0;i++)DemoNext();
            else for(int i=0;i<25&&!_model.Done;i++)DemoNext();
            _demonstrating=false;_timer.Stop();
        }
        else if(_lessonIndex==3)
        {
            double load=_model.Preview().Spirit;
            if(_lessonStep is 0 or 2)foreach(var l in _model.SignedLinks.Where(l=>l.UtilityId is "行" or "引")){l.Paused=_lessonStep==0;if(l.Paused)_model.PlannedLinks.Remove(l);else l.Repeat=true;}
            if(_lessonStep==1){int main=_model.InFurnace.Single(d=>d.Kind=="主药").Id;_model.RepeatingOperations.RemoveWhere(o=>o.SourceId!=main);_model.PendingOperations.RemoveAll(o=>o.Kind==AlchemyPreviewModel.Hand.Draw&&o.SourceId!=main);}
            _model.Action=AlchemyPreviewModel.Hand.Hold;double nextLoad=_model.Preview().Spirit;string error=_model.Advance();if(error.Length>0){RefreshLesson(error);return;}
            _lessonStep++;Refresh();RefreshLesson($"灵识峰值预览 {load:F1}→{nextLoad:F1}／{_model.MainSpirit}；推进1息。");return;
        }
        else
        {
            var target=_model.InFurnace.Single(d=>d.Herb.Name=="风络藤丝");
            if(_lessonStep==0){_model.Action=AlchemyPreviewModel.Hand.Raise;_model.Advance();}
            else if(_lessonStep==1){_model.SignLink(_model.InFurnace.Single(d=>d.Herb.Name=="青髓芽").Id,target.Id,"生");_model.Action=AlchemyPreviewModel.Hand.Lower;_model.Advance();}
            else{var exercise=AlchemyUtilityExercise.Create("护");_model=exercise.Model;exercise.Apply();target=_model.InFurnace.Single(d=>d.Herb.Name=="风络藤丝");}
            _lessonStep++;Refresh();double damage=_model.Inventory.Where(p=>p.Location==target.Id).Sum(p=>p.Amount*p.Damage)/Math.Max(.00001,_model.NodeAvailable(target.Id));
            RefreshLesson($"当前损伤{damage:P2} · 残渣{_model.NodeSlag(target.Id):F3}。"+(_lessonStep==3?"\n本步重开相同炉况作护的对照。":""));return;
        }
        _lessonStep++;Refresh();
        string details=$"本步实际推进{_model.Breath-before}息 · 法力剩余{_model.Mana}。";
        if(_model.Done)details+="\n"+_model.Result+"\n"+string.Join("\n",_model.Pills.Select(p=>$"{p.Index}号 {p.Score}分 · {p.EffectText}"));
        RefreshLesson(details);
    }
    private void StartAdvancedLesson(int lesson)
    {
        Reset();_advancedMode=true;_advancedIndex=lesson;_lessonStep=0;_lessonActive=true;
        if(lesson==0)
        {
            var exercise=AlchemyUtilityExercise.Create("引");
            _model=exercise.Model;_model.MainSpirit=1;
            string signed=_model.SignLink(exercise.SourceId,exercise.TargetId,"引");
            if(!_model.SignedLinks.Any(l=>l.SourceDoseId==exercise.SourceId&&l.TargetDoseId==exercise.TargetId&&l.UtilityId=="引"))
                throw new InvalidOperationException("灵识课无法预设引线："+signed);
            _advancedTitle="进阶一 · 灵识并行与轮候";
            _advancedIntro="本课把灵识上限设为1，布置两条“通”和一条“引”。材料和连线都已准备好；点一次运行，观察线路状态怎样轮候。\n\n灵识是同时能照看的手数，不会每息扣空。超过上限的功用线排队轮换；线越多，需要的总息数通常越长。准备阶段放药、签线只占用手来布置，不额外扣角色灵识。";
            _advancedRunBreaths=6;
        }
        else if(lesson==1)
        {
            var exercise=AlchemyUtilityExercise.Create("护");
            _model=exercise.Model;_model.Mana=18;
            _advancedTitle="进阶二 · 法力、火候与安全收炉";
            _advancedIntro="本课把可用法力设为18，炉中留有适宜火候不同的材料。点一次运行，观察自动控火如何升档；支付不起下一道炉令时，系统停止并保留实际材料、药性与损伤账。\n\n添焰付新档维持费，守炉付当前档，敛焰付降火前档。十息为一时辰。灵识和法力各管一件事：灵识管并行线路，法力管实际火候。";
            _advancedRunBreaths=8;
        }
        else
        {
            _model=CreateAdvancedQualityModel();
            _advancedTitle="进阶三 · 多味配伍与品质缺口";
            _advancedIntro="本炉选九品通脉丹，溪纹藻丝作主药；凝露松脂补固、乌羽片给通路增流、霜叶枝护持经脉药性，回泉珠在汇合位聚药。每味材料都已摆成节点并预签真实功用线。\n\n点一次开炉，观察主药发挥、配伍协调、丹体稳定三项如何共同决定品质。少一味不一定炼不出丹，但对应的高品质缺口会留在结果里。";
            _advancedRunBreaths=0;
        }
        EnsureLessonPanel();_lessonPanel!.Show();Refresh();_relationGraph.SetOpen(true);RefreshLesson();
    }
    private static AlchemyPreviewModel CreateAdvancedQualityModel()
    {
        var model=new AlchemyPreviewModel{Wanted=1};
        model.SelectPill("tongmai");model.SelectFormula("stream_fiber");model.SelectMainHerb("溪纹藻丝");
        foreach(var (name,amount) in new[]{("溪纹藻丝",.8),("凝露松脂",.5),("乌羽片",.5),("霜叶枝",.5),("回泉珠",.5)})
        {
            string placed=model.PlaceHerb(name,amount);
            if(model.InFurnace.All(d=>d.Herb.Name!=name))throw new InvalidOperationException("进阶配伍炉局投料失败："+placed);
        }
        int Id(string name)=>model.InFurnace.Single(d=>d.Herb.Name==name).Id;
        string CheckLine(string source,string utility,int target)
        {
            string result=model.SignLink(Id(source),target,utility);
            if(!model.SignedLinks.Any(l=>l.SourceDoseId==Id(source)&&l.TargetDoseId==target&&l.UtilityId==utility))
                throw new InvalidOperationException("进阶配伍炉局接线失败："+result);
            return result;
        }
        CheckLine("溪纹藻丝","通",0);
        var mainRoute=model.SignedLinks.Single(l=>l.SourceDoseId==Id("溪纹藻丝")&&l.UtilityId=="通");
        string support=model.SignFlowSupport(Id("乌羽片"),mainRoute);
        if(!model.SignedLinks.Any(l=>l.SourceDoseId==Id("乌羽片")&&l.UtilityId=="行"&&l.RouteTarget==mainRoute))
            throw new InvalidOperationException("进阶配伍炉局未能预签行："+support);
        CheckLine("凝露松脂","固",0);
        CheckLine("霜叶枝","护",Id("溪纹藻丝"));
        CheckLine("回泉珠","聚",0);
        model.Explanation="多味配伍进阶课 · 主药与三项高品质补足能力均有真实来源。";
        return model;
    }
    private void RefreshAdvancedLesson(string feedback="")
    {
        if(!_advancedMode||_lessonTitle is null||_lessonText is null||_lessonNext is null)return;
        string phase=_autoRunInProgress?$"自动运行 · 第{_model.AutomaticBreathsElapsed}息":_lessonStep==0?"布网已就绪":"本课结算";
        _lessonTitle.Text=$"{_advancedTitle}\n{phase}";
        if(_lessonStep==0)
        {
            string setup=_advancedIndex switch
            {
                0=>$"\n\n当前布网：{_model.SignedLinks.Count}条功用线 · 灵识上限1",
                1=>$"\n\n当前火候：{_model.Heat}档 · 可用法力：{_model.Mana}",
                _=>$"\n\n当前布网：{_model.InFurnace.Count}味材料 · {_model.SignedLinks.Count}条功用线 · 目标丹药：{_model.CurrentPill.Name}"
            };
            _lessonText.Text=_advancedIntro+setup+(feedback.Length>0?"\n\n"+feedback:"");
            _lessonNext.Text=_autoRunInProgress?"自动运行中":"运行本课观察";
            _lessonNext.Disabled=_autoRunInProgress;
        }
        else
        {
            string lines=string.Join("\n",_model.SignedLinks.Select(l=>$"{_model.NodeName(l.SourceDoseId)} → {l.UtilityId} → {_model.NodeName(l.TargetDoseId)}：{l.Status}"));
            _lessonText.Text=$"{_model.Result}\n\n实际经过{_model.AutomaticBreathsElapsed}息，法力消耗{_model.AutomaticManaSpent}。\n\n线路末态：\n{lines}"+(feedback.Length>0?"\n\n"+feedback:"");
            _lessonNext.Text="返回教程目录";_lessonNext.Disabled=false;
        }
    }
    private void AdvanceAdvancedLesson()
    {
        if(_lessonStep==0)
        {
            if(_autoRunInProgress)return;
            if(_model.AutoRunStarted&&_model.Done){_lessonStep=1;Refresh();RefreshLesson();return;}
            string error=_advancedIndex==2?_model.BeginAutomaticRun():"";
            if(_advancedIndex!=2)_model.BeginAutomaticLesson(_advancedRunBreaths);
            if(error.Length>0){RefreshAdvancedLesson(error);return;}
            _demonstrating=true;_autoRunInProgress=true;_timer.Stop();_timer.WaitTime=.22;_timer.Start();
            Refresh();RefreshLesson();return;
        }
        _lessonActive=false;_advancedMode=false;_lessonPanel?.Hide();ShowTutorial();
    }
    private void ShowComplexNetworkLesson()
    {
        ShowMessage("简单与复杂炉网",
            "简单炼法：药材经加工与功用线路处理后，沿单向路线汇入丹炉口；可以分支和中继，不需要回路。\n\n"+
            "复杂炼法：必须有一份真实药性从节点流出后，沿通路返回已经处理过的材料节点，再经历不同且可观察的加工变化；丹方还要说明回流目的与停止条件。只把线画成圈、重复经过节点或多投几味材料，都不算复杂炼法。\n\n"+
            "中心丹胚只应在真实闭环工序成立后出现。当前测试场景先完成简单九品丹；复杂回流丹胚尚未接入，所以本课讲清判定边界，不会假装演示一个尚不存在的工序。\n\n"+
            "炼丹十二功用名单暂时保留。各项都对应不同工位；本轮把通（建路与缓流）、行（既有路线增流）、引（按下游需求分配）区分清楚，没有把它们合并成一个万能输送按钮。"
        );
    }
    public async void CaptureTutorials()
    {
        string directory=_captureDirectory;
        for(int lesson=0;lesson<3;lesson++)
        {
            _captureDirectory="";
            StartTutorial(lesson);AdvanceTutorial();if(lesson==1)AdvanceTutorial();if(lesson==2){AdvanceTutorial();AdvanceTutorial();}
            _captureDirectory=directory;
            await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
            await SaveRoomLayout("tutorial-"+(lesson+1));
        }
        GD.Print("ALCHEMY_TUTORIAL_CAPTURES_DONE");GetTree().Quit();
    }
    public async void CaptureUtilityTutorials()
    {
        string directory=_captureDirectory;
        ShowTutorial();await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("course-menu");
        _courseTabs!.CurrentTab=1;await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("utility-menu");
        foreach(var dialog in GetChildren().OfType<AcceptDialog>().Where(d=>d.Visible))dialog.Hide();
        for(int i=0;i<AlchemyUtilityExercise.Order.Length;i++)
        {
            _captureDirectory="";StartUtilityLesson(i,true);_captureDirectory=directory;
            await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("practice-"+(i+1).ToString("D2"));
            _captureDirectory="";DemonstrateUtility();_captureDirectory=directory;
            await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("utility-"+(i+1).ToString("D2"));
        }
        for(int i=3;i<5;i++)
        {
            _captureDirectory="";StartTutorial(i);AdvanceTutorial();AdvanceTutorial();AdvanceTutorial();_captureDirectory=directory;
            await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("advanced-"+(i+1));
        }
        _captureDirectory="";StartUtilityLesson(0,true);_captureDirectory=directory;
        await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("manual-practice");
        GD.Print("ALCHEMY_UTILITY_TUTORIAL_CAPTURES_DONE");GetTree().Quit();
    }
}
