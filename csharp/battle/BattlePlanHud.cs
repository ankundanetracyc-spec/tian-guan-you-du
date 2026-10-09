using Godot;
using Godot.Collections;

/// <summary>规划、逐项选目标、行动三个阶段共用的战斗界面。</summary>
public partial class BattlePlanHud : Control
{
    private BattleManager _manager=null!;
    private Panel _timelinePanel=null!, _planningPanel=null!, _actionPanel=null!, _targetPanel=null!;
    private HBoxContainer _timeline=null!;
    private Control _planningSlots=null!, _actionSlots=null!, _targetRows=null!;
    private Label _heading=null!;
    private Button _toggle=null!;
    private HBoxContainer _planningControls=null!;
    private Button _planningToggle=null!;
    private BattleActionProgress? _progress;
    private string _signature="",_names="";
    private bool _queueVisible=true;
    private bool _planningQueueVisible=true, _lastPlanning;
    private Tween? _planningTween;
    private bool _planningAnimating;
    private int _dragIndex=-1;
    private Vector2 _dragOrigin;
    private bool _dragging;
    private Control? _dragSource, _dragPreview;
    private ColorRect? _dropMarker;

    public void Setup(BattleManager manager)
    {
        _manager=manager;Name="BattlePlanHud";MouseFilter=MouseFilterEnum.Ignore;ZIndex=50;
        var title=Panel("battle_title",new(24,18),new(200,40));
        title.AddChild(Label("山谷遭遇 · 战局",new(5,0),new(190,40),17));AddChild(title);
        _timelinePanel=Panel("timeline_panel",new(556,0),new(560,58));AddChild(_timelinePanel);
        _timeline=new HBoxContainer{Position=new(54,4),Size=new(452,46),Alignment=BoxContainer.AlignmentMode.Center,MouseFilter=MouseFilterEnum.Ignore};_timeline.AddThemeConstantOverride("separation",8);_timelinePanel.AddChild(_timeline);
        _progress=GD.Load<PackedScene>("res://tscn/panel/BattleActionProgress.tscn")?.Instantiate<BattleActionProgress>();
        if(_progress is not null){_progress.Position=new(566,60);_progress.CustomMinimumSize=new(540,60);_progress.Size=new(540,60);AddChild(_progress);}
        _planningPanel=new Panel{Position=new(522,180),Size=new(628,184),MouseFilter=MouseFilterEnum.Pass};
        _planningPanel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("1b3031f2"),BorderColor=BattleUiArt.Accent.Darkened(.15f),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=14,CornerRadiusTopRight=14,CornerRadiusBottomLeft=14,CornerRadiusBottomRight=14,ShadowColor=new Color(0,0,0,.22f),ShadowSize=4});AddChild(_planningPanel);
        var tab=BattleUiArt.Icon("planning_title_tab",new(220,48));tab.Name="TitleTab";tab.Position=new(240,-24);_planningPanel.AddChild(tab);
        _heading=Label("行动队列",new(0,0),new(150,26),17);tab.AddChild(_heading);
        _planningSlots=new Control{Position=new(20,32),Size=new(588,136),MouseFilter=MouseFilterEnum.Ignore};_planningPanel.AddChild(_planningSlots);
        var controls=new HBoxContainer{Size=new(444,40),Alignment=BoxContainer.AlignmentMode.Center,MouseFilter=MouseFilterEnum.Ignore};controls.AddThemeConstantOverride("separation",12);_planningControls=controls;
        var rest=Button("休整 +2 精力",140);rest.Pressed+=()=>_manager.queue_system_action(0);controls.AddChild(rest);
        var breathe=Button("吐纳 +1 法力",140);breathe.Pressed+=()=>_manager.queue_system_action(1);controls.AddChild(breathe);
        var confirm=Button("确认队列",140);confirm.Pressed+=_manager.confirm_player_queue;controls.AddChild(confirm);
        _planningToggle=Button("隐藏队列",140);_planningToggle.Pressed+=TogglePlanningQueue;AddChild(_planningToggle);AddChild(controls);controls.Name="PlanningControls";
        _targetPanel=Panel("queue_panel",new(24,168),new(156,394));AddChild(_targetPanel);
        _targetPanel.AddChild(Label("选择目标",new(8,8),new(140,32),19));
        _targetRows=new Control{Position=new(12,46),MouseFilter=MouseFilterEnum.Ignore};_targetPanel.AddChild(_targetRows);
        _actionPanel=Panel("res://textures/ui/combat/combat_skill_bar_panel_user_v3.png",new(250.8f,743.4f),new(1170.4f,197.6f));AddChild(_actionPanel);
        _actionSlots=new Control{Size=new(1300,160),MouseFilter=MouseFilterEnum.Ignore};_actionPanel.AddChild(_actionSlots);
        _toggle=Button("隐藏队列",108);_toggle.Position=new(1392,691);_toggle.Pressed+=()=>{_queueVisible=!_queueVisible;UpdateVisibility();};AddChild(_toggle);
        BattleUiArt.Fit(this);RefreshAll();
    }
    public override void _Process(double delta)
    {
        BattleUiArt.Fit(this);
        if(!GodotObject.IsInstanceValid(_manager))return;
        var names=_manager.get_timeline_names();var joined=string.Join("|",names);
        if(_names!=joined){_names=joined;RebuildTimeline(names);}
        var signature=Signature();if(signature!=_signature){_signature=signature;RebuildQueues();}
        UpdateVisibility();
    }
    public void RefreshAll(){_signature="";RebuildQueues();RebuildTimeline(_manager.get_timeline_names());UpdateVisibility();}
    public void RefreshPlayerQueue()=>RefreshAll();
    private List<BattleQueuedSkill> Queue()
    {
        var player=_manager.get_main_player_unit();return GodotObject.IsInstanceValid(player)&&_manager.battle_units.ContainsKey(player!)?_manager.get_battle_unit(player!).skill_queue:[];
    }
    private string Signature()
    {
        var player=_manager.get_main_player_unit();
        return $"{_manager.get_segment_phase()}:{_manager.is_selecting_queue_targets()}:{_manager.get_target_queue_index()}:{(GodotObject.IsInstanceValid(player)?player!.Get("current_stamina").AsInt32():0)}:{(GodotObject.IsInstanceValid(player)?player!.Get("current_mana").AsInt32():0)}:"+string.Join("|",Queue().Select(a=>$"{a.get_display_name()}:{a.target?.GetInstanceId()}"));
    }
    private void RebuildQueues()
    {
        CancelQueueDrag();
        var queue=Queue();var cap=_manager.get_action_slot_capacity(_manager.get_main_player_unit());
        // 按槽位总宽加左右各 20、上 32、下 16 的内边距计算，按钮完全位于框外。
        var width=cap*120+28;_planningPanel.Position=new((1672-width)/2,180);_planningPanel.Size=new(width,184);
        _planningPanel.GetNode<Control>("TitleTab").Position=new((width-220)/2,-24);_heading.Position=new(40,14);_heading.Size=new(140,26);_heading.Text=$"行动队列  {queue.Count}/{cap}";
        _planningControls.Position=new((1672-596)/2,_planningPanel.Position.Y+_planningPanel.Size.Y+12);
        if(!_planningAnimating)_planningToggle.Position=PlanningTogglePosition();
        _planningSlots.Size=new(width-40,136);BattleUiArt.ClearChildren(_planningSlots);BattleUiArt.ClearChildren(_actionSlots);BattleUiArt.ClearChildren(_targetRows);
        var warnings=_manager.get_player_queue_resource_warnings();
        var rowHeight=cap>7?52:62;var gap=cap>7?4:6;var height=60+cap*rowHeight+(cap-1)*gap;
        _targetPanel.Size=new(156,height);_targetPanel.Position=new(24,Mathf.Clamp(608-height,90,168));
        for(var i=0;i<cap;i++)
        {
            var action=i<queue.Count?queue[i]:null;var index=i;var warning=i<warnings.Count?warnings[i]:"";
            var x=(_planningSlots.Size.X-(cap*120-12))*.5f+i*120;
            var slot=Card(action);slot.Position=new(x,0);_planningSlots.AddChild(slot);
            if(warning!="")
            {
                var border=new Panel{Size=new(108,136),MouseFilter=MouseFilterEnum.Ignore,ZIndex=3};
                border.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=Colors.Transparent,BorderColor=BattleUiArt.Warning,BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6});slot.AddChild(border);
                var strip=Panel("resource_chip",new(2,56),new(104,26));strip.ZIndex=4;slot.AddChild(strip);
                var warn=Label(warning,Vector2.Zero,new(104,26),15);warn.AddThemeColorOverride("font_color",BattleUiArt.Warning);strip.AddChild(warn);
            }
            if(action is not null)
            {
                slot.MouseFilter=MouseFilterEnum.Stop;slot.MouseDefaultCursorShape=CursorShape.PointingHand;
                slot.TooltipText="拖拽调整顺序 · 单击卸下";
                slot.GuiInput+=input=>
                {
                    if(input is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:true} && _manager.can_player_plan())
                    {
                        _dragIndex=index;_dragOrigin=GetLocalMousePosition();_dragSource=slot;slot.AcceptEvent();
                    }
                };
            }
            var cardScale=Mathf.Min(1f,(_actionPanel.Size.X-40)/(cap*120-12));
            var executed=Card(action);executed.Scale=Vector2.One*cardScale;executed.Position=new((_actionPanel.Size.X-(cap*120-12)*cardScale)*.5f+i*120*cardScale,24);if(i==0&&action is not null){executed.Scale*=1.05f;executed.Position-=new Vector2(3,5);}_actionSlots.AddChild(executed);
            var current=i==_manager.get_target_queue_index();var row=Panel(warning!=""?"queue_row_warning":current?"queue_row_active":"queue_row",new(0,i*(rowHeight+gap)),new(132,rowHeight));_targetRows.AddChild(row);
            if(current){var arrow=BattleUiArt.Icon("icon_queue_current",new(20,20));arrow.Position=new(2,12);row.AddChild(arrow);}
            row.AddChild(Label($"{i+1}  {action?.get_display_name()??"空位"}",new(22,4),new(106,28),16));
            var detail=warning!=""?warning:current?"请选择目标":action?.target?.Get("entity_name").AsString()??"";
            var secondary=Label(detail,new(6,31),new(120,20),12);if(warning!="")secondary.AddThemeColorOverride("font_color",BattleUiArt.Warning);row.AddChild(secondary);
        }
    }
    public override void _Input(InputEvent input)
    {
        if(_dragIndex<0)return;
        if(!_manager.can_player_plan()){CancelQueueDrag();return;}
        if(input is InputEventMouseMotion)
        {
            if(!_dragging && GetLocalMousePosition().DistanceTo(_dragOrigin)>=6)
            {
                _dragging=true;
                _dragPreview=Card(Queue()[_dragIndex]);_dragPreview.ZIndex=400;_dragPreview.Modulate=new Color(1,1,1,.85f);AddChild(_dragPreview);
                _dragSource!.Modulate=new Color(1,1,1,.35f);
                _dropMarker=new ColorRect{Size=new(3,136),Color=BattleUiArt.Accent,MouseFilter=MouseFilterEnum.Ignore,ZIndex=20};_planningSlots.AddChild(_dropMarker);
            }
            if(_dragging)
            {
                _dragPreview!.Position=GetLocalMousePosition()-new Vector2(54,68);
                var to=QueueDropIndex();_dropMarker!.Visible=to>=0;
                if(to>=0)_dropMarker.Position=new(PlanningSlotStart()+to*120-5,0);
            }
        }
        if(input is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:false})
        {
            var from=_dragIndex;var dragged=_dragging;var to=QueueDropIndex();CancelQueueDrag();
            if(dragged){if(to>=0&&to!=from)_manager.reorder_player_action(from,to);}
            else if(to==from)_manager.cancel_player_action(from);
            GetViewport().SetInputAsHandled();
        }
        if(input is InputEventKey{Keycode:Key.Escape,Pressed:true}){CancelQueueDrag();GetViewport().SetInputAsHandled();}
    }
    private float PlanningSlotStart()=> (_planningSlots.Size.X-(_manager.get_action_slot_capacity(_manager.get_main_player_unit())*120-12))*.5f;
    private int QueueDropIndex()
    {
        var point=_planningSlots.GetLocalMousePosition();var count=Queue().Count;
        var capacity=_manager.get_action_slot_capacity(_manager.get_main_player_unit());var start=PlanningSlotStart();
        if(count==0||point.Y<0||point.Y>136||point.X<start||point.X>start+capacity*120-12)return -1;
        return Mathf.Clamp(Mathf.FloorToInt((point.X-start)/120),0,count-1);
    }
    private void CancelQueueDrag()
    {
        if(GodotObject.IsInstanceValid(_dragSource))_dragSource!.Modulate=Colors.White;
        if(GodotObject.IsInstanceValid(_dragPreview))_dragPreview!.QueueFree();
        if(GodotObject.IsInstanceValid(_dropMarker))_dropMarker!.QueueFree();
        _dragIndex=-1;_dragging=false;_dragSource=null;_dragPreview=null;_dropMarker=null;
    }
    private static Control Card(BattleQueuedSkill? action)
    {
        var card=new Control{Size=new(108,136),MouseFilter=MouseFilterEnum.Ignore};
        if(action is not null&&action.kind==BattleQueuedSkill.ActionKind.Skill)card.AddChild(CombatSkillBar.CreateSkillIconVisual(action.skill,new(96,102),new(6,6)));
        var frame=BattleUiArt.Icon(action is null?"skill_slot_empty":"skill_card_frame",new(108,136));card.AddChild(frame);
        if(action is not null&&action.kind!=BattleQueuedSkill.ActionKind.Skill)card.AddChild(Label(action.kind==BattleQueuedSkill.ActionKind.Rest?"休":action.kind==BattleQueuedSkill.ActionKind.Retreat?"撤":"吐",new(6,10),new(96,90),40));
        card.AddChild(Label(action?.get_display_name()??"空位",new(6,109),new(96,23),16));return card;
    }
    private void RebuildTimeline(Array<string> names)
    {
        BattleUiArt.ClearChildren(_timeline);
        if(names.Count==0){_timeline.AddChild(Label(_manager.is_planning_phase()?"规划中":"行动结束",Vector2.Zero,new(452,46),17));return;}
        foreach(var name in names.Take(4)){var label=Label(name,Vector2.Zero,new(104,46),_timeline.GetChildCount()==0?18:16);label.CustomMinimumSize=new(104,46);label.AddThemeColorOverride("font_color",_timeline.GetChildCount()==0?BattleUiArt.Accent:BattleUiArt.Text);_timeline.AddChild(label);}
    }
    private void UpdateVisibility()
    {
        var planning=_manager.is_planning_phase();var selecting=_manager.is_selecting_queue_targets();
        if(planning&&!_lastPlanning)_planningQueueVisible=true;
        _lastPlanning=planning;
        _planningPanel.Visible=planning&&!selecting&&_planningQueueVisible&&!_planningAnimating;_targetPanel.Visible=selecting;
        _planningControls.Visible=planning&&!selecting&&_planningQueueVisible&&!_planningAnimating;
        _planningToggle.Visible=planning&&!selecting;
        if(!_planningAnimating)_planningToggle.Position=PlanningTogglePosition();
        _planningToggle.Text=_planningQueueVisible?"隐藏队列":"显示队列";
        _actionPanel.Visible=!planning&&_queueVisible;_toggle.Visible=!planning;_toggle.Text=_queueVisible?"隐藏队列":"显示队列";
        if(_progress is not null)_progress.Visible=!planning;
    }
    private Vector2 PlanningTogglePosition()=>_planningQueueVisible?_planningControls.Position+new Vector2(456,0):new Vector2((1672-140)/2,_planningPanel.Position.Y-18);
    private void TogglePlanningQueue()
    {
        if(_planningAnimating)return;
        CancelQueueDrag();_planningQueueVisible=!_planningQueueVisible;_planningAnimating=true;
        _planningToggle.Disabled=true;UpdateVisibility();
        _planningTween?.Kill();_planningTween=CreateTween();
        _planningTween.TweenProperty(_planningToggle,"position",PlanningTogglePosition(),.24).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _planningTween.TweenCallback(Callable.From(()=>{_planningAnimating=false;_planningToggle.Disabled=false;UpdateVisibility();}));
    }
    private static Panel Panel(string art,Vector2 position,Vector2 size)
    {
        var panel=new Panel{Position=position,Size=size,MouseFilter=MouseFilterEnum.Ignore};
        panel.AddThemeStyleboxOverride("panel",BattleUiArt.Style(art,art=="skillbar_panel"?new(1344,184):art=="timeline_panel"?new(720,80):size,art=="skillbar_panel"?80:art=="timeline_panel"?90:14,art=="skillbar_panel"?24:12));return panel;
    }
    private static Label Label(string text,Vector2 position,Vector2 size,int font)
    {
        var label=new Label{Text=text,Position=position,Size=size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,ClipText=true,MouseFilter=MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",font);label.AddThemeColorOverride("font_color",BattleUiArt.Text);return label;
    }
    private static Button Button(string text,float width){var button=new Button{Text=text,Size=new(width,40),CustomMinimumSize=new(width,28),MouseFilter=MouseFilterEnum.Stop};BattleUiArt.Skin(button);return button;}
}
