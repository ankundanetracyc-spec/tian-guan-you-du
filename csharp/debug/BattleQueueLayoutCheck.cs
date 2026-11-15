using Godot;
using System.Reflection;

/// <summary>BattleSandbox 的可选队列布局验收：--queue-layout-check；F5～F10 切换容量。</summary>
public partial class BattleQueueLayoutCheck : Node
{
    private BattleManager _manager=null!;
    private BattlePlanHud _hud=null!;
    private GodotObject _player=null!;
    private bool _ready;
    private readonly ActiveSkillData[] _skills =
    [
        GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres"),
        GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres"),
        GD.Load<ActiveSkillData>("res://resources/activeskills/痛击.tres")
    ];

    public override async void _Ready()
    {
        try
        {
            for(var frame=0;frame<12;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _manager=GetParent().GetNode<BattleManager>("BattleMgr");
            _hud=(BattlePlanHud)GetTree().Root.FindChild("BattlePlanHud",true,false);
            _player=_manager.get_main_player_unit()!;
            Require(_manager.can_player_plan(),"未进入规划阶段");
            // 不手动刷新：同时检查容量或内容变化是否会触发 HUD 自动重建。
            foreach(var fill in new[]{0,3,-1})
            {
                for(var cap=5;cap<=10;cap++)
                {
                    var count=fill<0?cap:fill;
                    SetQueue(cap,count);
                    for(var frame=0;frame<3;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    CheckLayout(cap,count);
                    GD.Print($"QUEUE_LAYOUT_PASS capacity={cap} filled={count} planning/execution/targets/drop");
                    if(fill<0)
                    {
                        var queue=_manager.get_battle_unit(_player).skill_queue;
                        var first=queue[0];
                        _manager.reorder_player_action(0,cap-1);
                        Require(ReferenceEquals(queue[cap-1],first),$"{cap} 跨排移至队尾失败");
                        _manager.cancel_player_action(cap-1);
                        for(var frame=0;frame<3;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                        CheckLayout(cap,cap-1);
                    }
                }
            }
            SetQueue(10,10);
            _ready=true;
            GD.Print("QUEUE_LAYOUT_ALL_PASS 5..10; F5..F10 切换满队列");
            if(OS.GetCmdlineUserArgs().Contains("--queue-layout-check-exit"))GetTree().Quit();
        }
        catch(Exception error)
        {
            GD.PushError($"QUEUE_LAYOUT_FAIL {error}");
            if(OS.GetCmdlineUserArgs().Contains("--queue-layout-check-exit"))GetTree().Quit(1);
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if(!_ready||@event is not InputEventKey{Pressed:true,Echo:false} key)return;
        var cap=(int)key.Keycode-(int)Key.F5+5;
        if(cap<5||cap>10)return;
        SetQueue(cap,cap);GetViewport().SetInputAsHandled();
    }

    private void SetQueue(int cap,int count)
    {
        _player.SetMeta("battle_action_slots",cap);
        var queue=_manager.get_battle_unit(_player).skill_queue;queue.Clear();
        _manager.get_battle_unit(_player).segment_action_index=0;
        for(var i=0;i<count;i++)queue.Add((i%5) switch
        {
            3=>BattleQueuedSkill.Rest(),4=>BattleQueuedSkill.Empty(),
            _=>new BattleQueuedSkill(_skills[i%5],null)
        });
        while(queue.Count<cap)queue.Add(BattleQueuedSkill.Empty());
    }

    private void CheckLayout(int cap,int count)
    {
        var panel=_hud.GetNode<Control>("PlanningPanel");
        var slots=panel.GetNode<Control>("PlanningSlots");
        var execution=_hud.GetNode<Control>("ActionPanel/ActionSlots");
        var target=_hud.GetNode<Control>("TargetPanel");
        var rows=target.GetNode<Control>("TargetRows");
        var controls=_hud.GetNode<Control>("PlanningControls");
        var toggle=_hud.GetNode<Control>("PlanningToggle");
        var canvas=new Rect2(Vector2.Zero,BattleUiArt.Canvas);
        Require(Contains(canvas,panel.GetRect()),$"{cap}/{count} 面板越界");
        foreach(var control in new[]{panel,controls,toggle,target})CheckScreen(control);
        Require(Contains(canvas,controls.GetRect())&&Contains(canvas,toggle.GetRect()),$"{cap}/{count} 按钮越界");
        Require(controls.Position.Y>=panel.Position.Y+panel.Size.Y+11,$"{cap} 按钮未置于框外");
        Require(controls.Position.Y+controls.Size.Y<743.4f,$"{cap} 按钮覆盖技能栏");
        Require(Contains(canvas,target.GetRect()),$"{cap} 目标清单越界");
        var label=panel.GetNode("TitleTab").GetChild<Label>(0);
        var filled=_manager.get_battle_unit(_player).skill_queue.Count(action=>action.kind!=BattleQueuedSkill.ActionKind.Empty);
        Require(label.Text==$"行动队列  {filled}/{cap}",$"{cap}/{count} 容量标题未按非空行动更新");
        Require(label.GetThemeFont("font").GetStringSize(label.Text,fontSize:label.GetThemeFontSize("font_size")).X<=label.Size.X,$"{cap} 标题文字溢出");
        CheckCards(slots,cap);CheckCards(execution,cap);
        Require(rows.GetChildCount()==cap,$"{cap} 目标清单数量");
        foreach(Control row in rows.GetChildren())Require(Contains(new Rect2(Vector2.Zero,target.Size),new Rect2(rows.Position+row.Position,row.Size)),"目标清单溢出");
        Require(slots.GetChild<Control>(cap-1).Position.Y>0== (cap>7),$"{cap} 换行规则错误");
        var drop=typeof(BattlePlanHud).GetMethod("QueueDropIndex",BindingFlags.Instance|BindingFlags.NonPublic)!;
        for(var i=0;i<cap;i++)
        {
            var card=slots.GetChild<Control>(i);
            var point=card.GetGlobalTransformWithCanvas()*(card.Size*.5f);
            var actual=(int)drop.Invoke(_hud,[point])!;
            Require(actual==i,$"{cap}/{count} 第{i+1}槽拖拽落位错误");
        }
        Require((int)drop.Invoke(_hud,[slots.GetGlobalTransformWithCanvas()*new Vector2(-8,-8)])! == -1,"框外拖拽未取消");
    }

    private static void CheckCards(Control area,int cap)
    {
        Require(area.GetChildCount()==cap,$"{area.Name} 槽位数不等于{cap}");
        var rectangles=new List<Rect2>();
        foreach(Control card in area.GetChildren())
        {
            Require(Mathf.Abs(card.Size.Y/card.Size.X-1.5f)<.001f,$"{cap} 卡槽宽高比错误");
            Require(Contains(new Rect2(Vector2.Zero,area.Size),card.GetRect()),$"{cap} 卡槽越界");
            foreach(var previous in rectangles)Require(!previous.Intersects(card.GetRect()),$"{cap} 卡槽重叠");
            rectangles.Add(card.GetRect());
            CheckScreen(card);
            // 悬停时 Godot 会附加 PopupPanel（Window），不属于卡牌内部布局。
            foreach(var child in card.GetChildren().OfType<Control>())Require(Contains(new Rect2(Vector2.Zero,card.Size),child.GetRect()),$"{cap} 卡内元素越界");
        }
    }
    private static void CheckScreen(Control control)
    {
        var transform=control.GetGlobalTransformWithCanvas();
        var rect=new Rect2(transform*Vector2.Zero,transform*control.Size-transform*Vector2.Zero);
        Require(Contains(control.GetViewport().GetVisibleRect(),rect),$"{control.Name} 屏幕边界溢出");
    }
    private static bool Contains(Rect2 outer,Rect2 inner)=>outer.Grow(.1f).Encloses(inner);
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
