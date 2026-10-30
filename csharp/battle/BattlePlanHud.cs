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
    private Label _actionIndicator=null!;
    private Button _toggle=null!;
    private HBoxContainer _planningControls=null!;
    private Button _planningToggle=null!;
    private BattleActionProgress? _progress;
    private string _signature="",_names="";
    private bool _queueVisible=true;
    private bool _planningQueueVisible=true, _lastPlanning;
    private Tween? _planningTween;
    private bool _planningAnimating;
    private string ActionName(BattleQueuedSkill? action) => FumingPresentation.QueueName(_manager.get_main_player_unit(), action, _manager);
    private int _dragIndex=-1;
    private Vector2 _dragOrigin;
    private bool _dragging;
    private Control? _dragSource, _dragPreview;
    private ColorRect? _dropMarker;
    private Vector2 _planningCardSize=new(208,312);
    private int _planningColumns=5;
    private const float PlanningGap=16;
    private float PlanningPitch=>_planningCardSize.X+PlanningGap;
    private Tween? _cardBeat;

    public void Setup(BattleManager manager)
    {
        _manager=manager;Name="BattlePlanHud";MouseFilter=MouseFilterEnum.Ignore;ZIndex=50;
        var title=Panel("battle_title",new(24,18),new(200,40));
        title.AddChild(Label("山谷遭遇 · 战局",new(5,0),new(190,40),17));AddChild(title);
        _timelinePanel=Panel("timeline_panel",new(556,0),new(560,58));AddChild(_timelinePanel);
        _timeline=new HBoxContainer{Position=new(54,4),Size=new(452,46),Alignment=BoxContainer.AlignmentMode.Center,MouseFilter=MouseFilterEnum.Ignore};_timeline.AddThemeConstantOverride("separation",8);_timelinePanel.AddChild(_timeline);
        _progress=GD.Load<PackedScene>("res://tscn/panel/BattleActionProgress.tscn")?.Instantiate<BattleActionProgress>();
        if(_progress is not null){_progress.Position=new(566,60);_progress.CustomMinimumSize=new(540,60);_progress.Size=new(540,60);AddChild(_progress);}
        _planningPanel=new Panel{Name="PlanningPanel",Position=new(522,180),Size=new(628,184),MouseFilter=MouseFilterEnum.Pass};
        _planningPanel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("1b3031f2"),BorderColor=BattleUiArt.Accent.Darkened(.15f),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=14,CornerRadiusTopRight=14,CornerRadiusBottomLeft=14,CornerRadiusBottomRight=14,ShadowColor=new Color(0,0,0,.22f),ShadowSize=4});AddChild(_planningPanel);
        var tab=BattleUiArt.Icon("planning_title_tab",new(300,66));tab.Name="TitleTab";_planningPanel.AddChild(tab);
        _heading=Label("行动队列",new(0,0),new(150,26),17);tab.AddChild(_heading);
        _planningSlots=new Control{Name="PlanningSlots",Position=new(20,32),Size=new(588,136),MouseFilter=MouseFilterEnum.Ignore};_planningPanel.AddChild(_planningSlots);
        var controls=new HBoxContainer{Size=new(292,40),Alignment=BoxContainer.AlignmentMode.Center,MouseFilter=MouseFilterEnum.Ignore};controls.AddThemeConstantOverride("separation",12);_planningControls=controls;
        var rest=Button("休整 +2 体力",140);rest.Pressed+=()=>_manager.queue_rest_action();controls.AddChild(rest);
        var confirm=Button("确认队列",140);confirm.Pressed+=_manager.confirm_player_queue;controls.AddChild(confirm);
        _planningToggle=Button("隐藏队列",140);_planningToggle.Name="PlanningToggle";_planningToggle.Pressed+=TogglePlanningQueue;AddChild(_planningToggle);AddChild(controls);controls.Name="PlanningControls";
        _targetPanel=Panel("queue_panel",new(24,168),new(156,394));AddChild(_targetPanel);
        _targetPanel.AddChild(Label("选择目标",new(8,8),new(140,32),19));
        _targetPanel.Name="TargetPanel";
        _targetRows=new Control{Name="TargetRows",Position=new(12,46),MouseFilter=MouseFilterEnum.Ignore};_targetPanel.AddChild(_targetRows);
        _actionPanel=new Panel{Position=new(124,689),Size=new(1424,252),MouseFilter=MouseFilterEnum.Ignore};
        _actionPanel.AddThemeStyleboxOverride("panel",BattleUiArt.PaperBarStyle());AddChild(_actionPanel);
        _actionPanel.Name="ActionPanel";
        _actionSlots=new Control{Name="ActionSlots",Position=new(48,18),Size=new(1328,216),MouseFilter=MouseFilterEnum.Ignore};_actionPanel.AddChild(_actionSlots);
        _actionIndicator=Label("▼",Vector2.Zero,new(30,24),22);_actionIndicator.Name="CurrentActionIndicator";_actionIndicator.ZIndex=8;_actionIndicator.AddThemeColorOverride("font_color",BattleUiArt.Text);_actionPanel.AddChild(_actionIndicator);
        _toggle=Button("隐藏队列",108);_toggle.Position=new(1440,641);_toggle.Pressed+=()=>{_queueVisible=!_queueVisible;UpdateVisibility();};AddChild(_toggle);
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
    public void RefreshAll(){RebuildQueues();_signature=Signature();RebuildTimeline(_manager.get_timeline_names());UpdateVisibility();}
    public void RefreshPlayerQueue()=>RefreshAll();
    private List<BattleQueuedSkill> Queue()
    {
        var player=_manager.get_main_player_unit();return GodotObject.IsInstanceValid(player)&&_manager.battle_units.ContainsKey(player!)?_manager.get_battle_unit(player!).skill_queue:[];
    }
    private string Signature()
    {
        var player=_manager.get_main_player_unit();
        return $"{_manager.get_action_slot_capacity(player)}:{_manager.get_segment_phase()}:{_manager.is_selecting_queue_targets()}:{_manager.get_target_queue_index()}:{_manager.get_current_action_slot(player)}:{(GodotObject.IsInstanceValid(player)?player!.Get("current_stamina").AsInt32():0)}:{(GodotObject.IsInstanceValid(player)?player!.Get("current_mana").AsInt32():0)}:"+string.Join("|",Queue().Select(a=>$"{ActionName(a)}:{(a.kind == BattleQueuedSkill.ActionKind.Skill ? FumingPresentation.Resolve(player, a.skill, _manager).icon_path : "")}:{a.target?.GetInstanceId()}:{a.completed}:{a.skipped}:{PlayerNian(player,a)}"));
    }
    private int PlayerNian(GodotObject? player, BattleQueuedSkill? action)
    {
        if(!GodotObject.IsInstanceValid(player)||action is null||action.kind!=BattleQueuedSkill.ActionKind.Skill||action.artifact_instance_id.Length>0)return 0;
        return _manager.get_skill_nian(player!,action.execution_skill);
    }
    private void AddNianBadge(Control card,BattleQueuedSkill? action,Vector2 size,GodotObject? player)
    {
        var stacks=PlayerNian(player,action);if(stacks<=0)return;
        var badge=Panel("resource_chip",new(size.X-74,31),new(56,28));badge.ZIndex=5;card.AddChild(badge);
        badge.AddChild(Label($"念 {stacks}",Vector2.Zero,new(56,28),size.X>=168?15:12));
    }
    private void RebuildQueues()
    {
        CancelQueueDrag();
        var queue=Queue();var cap=_manager.get_action_slot_capacity(_manager.get_main_player_unit());
        // 长队列换行保留大卡面；卡框九宫格只延展中段，不压扁边角。
        var rows=cap>7?2:1;
        _planningColumns=Mathf.CeilToInt((float)cap/rows);
        _planningCardSize=CardSize(rows==2?168:Mathf.Min(208,(1512-64-(_planningColumns-1)*PlanningGap)/_planningColumns));
        var width=_planningColumns*_planningCardSize.X+(_planningColumns-1)*PlanningGap+64;
        var slotsHeight=rows*_planningCardSize.Y+(rows-1)*PlanningGap;
        _planningPanel.Position=new((1672-width)/2,rows==2?108:150);_planningPanel.Size=new(width,32+slotsHeight+24);
        _planningPanel.GetNode<Control>("TitleTab").Position=new((width-300)/2,-33);_heading.Position=new(80,26);_heading.Size=new(140,26);_heading.Text=$"行动队列  {_manager.get_filled_action_slot_count(_manager.get_main_player_unit())}/{cap}";
        _planningControls.Position=new((1672-444)/2,_planningPanel.Position.Y+_planningPanel.Size.Y+12);
        if(!_planningAnimating)_planningToggle.Position=PlanningTogglePosition();
        _planningSlots.Position=new(32,32);_planningSlots.Size=new(width-64,slotsHeight);
        _cardBeat?.Kill();BattleUiArt.ClearChildren(_planningSlots);BattleUiArt.ClearChildren(_actionSlots);BattleUiArt.ClearChildren(_targetRows);
        var warnings=_manager.get_player_queue_resource_warnings();
        var rowHeight=cap>7?52:62;var gap=cap>7?4:6;var height=60+cap*rowHeight+(cap-1)*gap;
        _targetPanel.Size=new(156,height);_targetPanel.Position=new(24,Mathf.Clamp(608-height,90,168));
        for(var i=0;i<cap;i++)
        {
            var action=i<queue.Count?queue[i]:null;var index=i;var warning=i<warnings.Count?warnings[i]:"";
            var slot=Card(action,_planningCardSize);slot.Position=PlanningSlotPosition(i);AddNianBadge(slot,action,_planningCardSize,_manager.get_main_player_unit());_planningSlots.AddChild(slot);
            if(warning!="")
            {
                var border=new Panel{Position=new(4,4),Size=_planningCardSize-new Vector2(8,8),MouseFilter=MouseFilterEnum.Ignore,ZIndex=3};
                border.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=Colors.Transparent,BorderColor=BattleUiArt.Warning,BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6});slot.AddChild(border);
                var stripSize=new Vector2(_planningCardSize.X-10,46);
                var strip=Panel("resource_chip",new(5,_planningCardSize.Y*.48f),stripSize);strip.ZIndex=4;slot.AddChild(strip);
                var responseWarning=action?.kind==BattleQueuedSkill.ActionKind.Skill && CardRules.IsResponse(action.skill);
                var warn=Label($"{warning}\n{(responseWarning?"无法应对":"无法施展")}",Vector2.Zero,stripSize,16);warn.AddThemeColorOverride("font_color",BattleUiArt.Warning);strip.AddChild(warn);
            }
            if(action is not null && action.kind != BattleQueuedSkill.ActionKind.Empty)
            {
                slot.MouseFilter=MouseFilterEnum.Stop;slot.MouseDefaultCursorShape=CursorShape.PointingHand;
                slot.GuiInput+=input=>
                {
                    if(input is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:true} press && _manager.can_player_plan())
                    {
                        _dragIndex=index;
                        _dragOrigin=GetGlobalTransformWithCanvas().AffineInverse()*(slot.GetGlobalTransformWithCanvas()*press.Position);
                        _dragSource=slot;slot.AcceptEvent();
                    }
                };
                HoverInfoManager.SetInfo(slot, BuildQueueActionHover(action, i, _manager.get_main_player_unit(), warning, true));
            }
            var executionSize=CardSize(Mathf.Min(144,(_actionSlots.Size.X-(cap-1)*12)/cap));
            var executionWidth=cap*executionSize.X+(cap-1)*12;
            var executed=Card(action,executionSize);AddNianBadge(executed,action,executionSize,_manager.get_main_player_unit());
            executed.Position=new((_actionSlots.Size.X-executionWidth)*.5f+i*(executionSize.X+12),(_actionSlots.Size.Y-executionSize.Y)*.5f);
            executed.Name=$"ActionSlot{i+1}";
            if(action?.completed==true)executed.Modulate=new Color(.42f,.44f,.43f,.72f);
            executed.MouseFilter=MouseFilterEnum.Pass;
            if(action is not null && action.kind != BattleQueuedSkill.ActionKind.Empty)
                HoverInfoManager.SetInfo(executed, BuildQueueActionHover(action, i, _manager.get_main_player_unit(), warning, false));
            _actionSlots.AddChild(executed);
            if(i==_manager.get_current_action_slot(_manager.get_main_player_unit()))
                _actionIndicator.Position=_actionSlots.Position+executed.Position+new Vector2((executionSize.X-30)*.5f,-22);
            var current=i==_manager.get_target_queue_index();var row=Panel(warning!=""?"queue_row_warning":current?"queue_row_active":"queue_row",new(0,i*(rowHeight+gap)),new(132,rowHeight));_targetRows.AddChild(row);
            if(current){var arrow=BattleUiArt.Icon("icon_queue_current",new(20,20));arrow.Position=new(2,12);row.AddChild(arrow);}
            row.AddChild(Label($"{i+1}  {ActionName(action)}",new(22,6),new(106,24),16));
            var detail=warning!=""?warning:current?"请选择目标":action?.kind==BattleQueuedSkill.ActionKind.Empty?"等待一次行动":action?.target?.Get("entity_name").AsString()??"";
            var secondary=Label(detail,new(8,rowHeight>52?34:29),new(116,rowHeight>52?18:15),12);if(warning!="")secondary.AddThemeColorOverride("font_color",BattleUiArt.Warning);row.AddChild(secondary);
        }
    }
    public override void _Input(InputEvent input)
    {
        if(_dragIndex<0)return;
        if(!_manager.can_player_plan()){CancelQueueDrag();return;}
        if(input is InputEventMouseMotion motion)
        {
            var pointer=GetGlobalTransformWithCanvas().AffineInverse()*motion.Position;
            if(!_dragging && pointer.DistanceTo(_dragOrigin)>=6)
            {
                _dragging=true;
                _dragPreview=Card(Queue()[_dragIndex],_planningCardSize);_dragPreview.ZIndex=400;_dragPreview.Modulate=new Color(1,1,1,.85f);AddChild(_dragPreview);
                _dragSource!.Modulate=new Color(1,1,1,.35f);
                _dropMarker=new ColorRect{Size=new(3,_planningCardSize.Y),Color=BattleUiArt.Accent,MouseFilter=MouseFilterEnum.Ignore,ZIndex=20};_planningSlots.AddChild(_dropMarker);
            }
            if(_dragging)
            {
                _dragPreview!.Position=pointer-_planningCardSize*.5f;
                var to=QueueDropIndex(motion.Position);_dropMarker!.Visible=to>=0;
                if(to>=0)_dropMarker.Position=PlanningSlotPosition(to)-new Vector2(5,0);
            }
        }
        if(input is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:false} release)
        {
            var from=_dragIndex;var dragged=_dragging;var to=QueueDropIndex(release.Position);CancelQueueDrag();
            if(dragged){if(to>=0&&to!=from)_manager.reorder_player_action(from,to);}
            else if(to==from)_manager.cancel_player_action(from);
            GetViewport().SetInputAsHandled();
        }
    }
    private Vector2 PlanningSlotPosition(int index)=>new(index%_planningColumns*PlanningPitch,index/_planningColumns*(_planningCardSize.Y+PlanningGap));
    private int QueueDropIndex(Vector2 viewportPosition)
    {
        var point=_planningSlots.GetGlobalTransformWithCanvas().AffineInverse()*viewportPosition;
        var capacity=_manager.get_action_slot_capacity(_manager.get_main_player_unit());
        if(point.Y<0||point.Y>_planningSlots.Size.Y||point.X<0||point.X>_planningSlots.Size.X)return -1;
        var row=Mathf.FloorToInt(point.Y/(_planningCardSize.Y+PlanningGap));
        var column=Mathf.Clamp(Mathf.FloorToInt(point.X/PlanningPitch),0,_planningColumns-1);
        var index=row*_planningColumns+column;
        if(index>=capacity)return -1;
        return index;
    }
    private void CancelQueueDrag()
    {
        if(GodotObject.IsInstanceValid(_dragSource))_dragSource!.Modulate=Colors.White;
        if(GodotObject.IsInstanceValid(_dragPreview))_dragPreview!.QueueFree();
        if(GodotObject.IsInstanceValid(_dropMarker))_dropMarker!.QueueFree();
        _dragIndex=-1;_dragging=false;_dragSource=null;_dragPreview=null;_dropMarker=null;
    }
    private static Vector2 CardSize(float width)=>new(width,width*1.5f);
    private Control Card(BattleQueuedSkill? action,Vector2 size)
    {
        if(action?.kind==BattleQueuedSkill.ActionKind.Empty)action=null;
        var card=new Control{Size=size,MouseFilter=MouseFilterEnum.Ignore};
        const int edge=24,foot=64;
        // 插图、空槽与名称使用同一内边界，边框仅拉伸透明中芯。
        var opening=new Control{Position=new(24,26),Size=new(size.X-48,size.Y-95),ClipContents=true,MouseFilter=MouseFilterEnum.Ignore};card.AddChild(opening);
        if(action is not null&&action.kind==BattleQueuedSkill.ActionKind.Skill)
        {
            opening.AddChild(new ColorRect{Size=opening.Size,Color=new Color("ded5c3"),MouseFilter=MouseFilterEnum.Ignore});
            opening.AddChild(CombatSkillBar.CreateSkillIconVisual(FumingPresentation.Resolve(_manager.get_main_player_unit(), action.skill, _manager),opening.Size));
        }
        if (action?.kind == BattleQueuedSkill.ActionKind.Pill && BattleSupplyRules.Load(action.supply_path) is { } pill)
        {
            var icon = new TextureRect { Size = opening.Size, Texture = GD.Load<Texture2D>(pill.icon_path),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore };
            opening.AddChild(icon); AlchemyPillVisual.Apply(icon, pill.efficacy_tier, pill.pill_id);
            if (_manager.get_main_player_unit() is Character owner && action.supply_slot >= 0 && action.supply_slot < owner.battle_supplies.Count)
            {
                var count = Label($"×{owner.battle_supplies[action.supply_slot].count}", new(opening.Size.X - 44, 24), new(44, 24), 16);
                count.ZIndex = 3; opening.AddChild(count);
            }
        }
        var frame=new NinePatchRect{Texture=BattleUiArt.Texture(action is null?"skill_slot_empty":"skill_card_frame"),Size=size,PatchMarginLeft=edge,PatchMarginRight=edge,PatchMarginTop=26,PatchMarginBottom=foot,MouseFilter=MouseFilterEnum.Ignore,ZIndex=2};card.AddChild(frame);
        if(action is not null&&action.kind is BattleQueuedSkill.ActionKind.Rest or BattleQueuedSkill.ActionKind.Retreat)card.AddChild(Label(action.kind==BattleQueuedSkill.ActionKind.Rest?"休":"撤",opening.Position,opening.Size,Mathf.RoundToInt(size.X*.38f)));
        var name=Label(ActionName(action),new(24,size.Y-62),new(size.X-48,48),size.X>=168?22:18);name.ZIndex=3;card.AddChild(name);
        if (action?.kind == BattleQueuedSkill.ActionKind.Pill && BattleSupplyRules.Load(action.supply_path) is { } drug)
        { name.Text = $"服用·{BattleSupplyRules.Name(drug)}\n{BattleSupplyRules.QualityNames[drug.efficacy_tier]}"; name.AddThemeFontSizeOverride("font_size", size.X >= 168 ? 18 : 15); }
        return card;
    }
    private HoverInfoManager.HoverInfoContent BuildQueueActionHover(
        BattleQueuedSkill action, int slotIndex, GodotObject? player, string warning, bool planning)
    {
        var status = action.completed ? action.skipped ? "已跳过" : "已执行" : "待行动";
        var subtitle = $"第 {slotIndex + 1} 位 · {status}";
        var details = new List<string>();
        if (action.kind == BattleQueuedSkill.ActionKind.Pill && player is Character owner)
        {
            var info = _manager.SupplyHover(owner, action.supply_slot, action.supply_path);
            return new(action.get_display_name(), info.Body + (planning ? "\n\n拖拽调整顺序，单击卸下；卸下不扣药。" : ""), info.Icon, $"{info.Subtitle} · {subtitle}");
        }
        if (!string.IsNullOrWhiteSpace(warning))
        {
            var warningDetail = action.kind == BattleQueuedSkill.ActionKind.Skill && CardRules.IsResponse(action.skill)
                ? $"队列检查：{warning}；若匹配来袭时仍不足则跳过且不扣费。未触发应对时按普通分支结算，未配置则免费空放。"
                : $"队列检查：{warning}；实际执行时跳过且不扣费。";
            details.Add(warningDetail);
        }
        if (action.kind == BattleQueuedSkill.ActionKind.Skill && player is WorldEntity unit)
        {
            var skillInfo = CombatSkillBar.BuildSkillHoverInfo(unit, action.skill, _manager, action.execution_skill);
            var targetName = GodotObject.IsInstanceValid(action.target) ? action.target!.Get("entity_name").AsString() : "";
            if (!string.IsNullOrWhiteSpace(targetName)) details.Add($"指定目标：{targetName}");
            var nian = PlayerNian(player, action);
            if (nian > 0) details.Add($"技能实例念：{nian}");
            if (planning) details.Add("队列操作：拖至空槽移动，拖至招式互换，单击卸下。");
            var sections = (skillInfo.Sections ?? new[] { new HoverInfoManager.HoverInfoSection("", skillInfo.Body) }).ToList();
            if (details.Count > 0) sections.Add(new("队列信息", string.Join("\n", details)));
            return HoverInfoManager.Structured(ActionName(action), skillInfo.Icon, $"{skillInfo.Subtitle}　·　{subtitle}", sections.ToArray());
        }

        details.Insert(0, action.kind switch
        {
            BattleQueuedSkill.ActionKind.Rest => "效果：回复 2 点体力，不超过体力上限。",
            BattleQueuedSkill.ActionKind.Retreat => "效果：按本场撤退规则尝试离开战斗。",
            _ => "此行动槽尚未安排技能。"
        });
        if (planning) details.Add("队列操作：拖至空槽移动，拖至招式互换，单击卸下。");
        return new HoverInfoManager.HoverInfoContent(action.get_display_name(), string.Join("\n\n", details), null, subtitle);
    }
    public void PresentAction(GodotObject unit)
    {
        if(unit!=_manager.get_main_player_unit()||_actionSlots.GetChildCount()==0)return;
        RefreshAll();
        var index=_manager.get_current_action_slot(unit);
        if(index<0||index>=_actionSlots.GetChildCount())return;
        _cardBeat?.Kill();var card=_actionSlots.GetChild<Control>(index);var home=card.Position;
        _cardBeat=CreateTween().SetPauseMode(Tween.TweenPauseMode.Stop);
        _cardBeat.SetParallel();
        _cardBeat.TweenProperty(card,"position",home-new Vector2(0,10),.10).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _cardBeat.TweenProperty(card,"modulate",new Color(1.18f,1.18f,1.12f),.10);
        _cardBeat.Chain().TweenInterval(.35);
        _cardBeat.Chain().SetParallel();
        _cardBeat.TweenProperty(card,"position",home,.18).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _cardBeat.TweenProperty(card,"modulate",Colors.White,.18);
    }
    public bool is_action_animation_playing()=>_cardBeat?.IsValid()==true&&_cardBeat.IsRunning();
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
        var cursor=_manager.get_current_action_slot(_manager.get_main_player_unit());
        _actionIndicator.Visible=_manager.get_segment_phase()==BattleManager.SegmentPhase.Executing&&cursor>=0&&cursor<Queue().Count;
        if(_progress is not null)_progress.Visible=!planning;
    }
    private Vector2 PlanningTogglePosition()=>_planningQueueVisible?_planningControls.Position+new Vector2(304,0):new Vector2((1672-140)/2,_planningPanel.Position.Y-18);
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
