using Godot;

/// <summary>屏幕空间血条、状态与头顶应对牌；位置跟随角色。</summary>
public partial class BattleUnitOverlay : CanvasLayer
{
    private const string StatusIconAtlas = "res://textures/ui/battle_status_icons_v1.png";
    private BattleManager manager=null!;
    private readonly System.Collections.Generic.Dictionary<Creature,Control> views=[];
    private readonly System.Collections.Generic.Dictionary<Creature,string> signatures=[];
    private readonly System.Collections.Generic.Dictionary<Creature,string> queueSignatures=[];
    private readonly List<Control> activeTargetPreviews=[];
    private InfoPanel? info;
    private TargetFrameLayer? targetFrames;
    private StatusInstance? hoveredStatus;
    private Control? hoveredStatusBadge;
    private string hoveredStatusId="",hoveredStatusName="",hoveredStatusTip="",lastStatusHoverText="";
    private int hoveredStatusCount;
    private Button? pauseButton;
    private Button? speedButton;
    private Button? retreatButton;
    private Label? retreatNotice;
    public void Setup(BattleManager owner){manager=owner;}
    public override void _Ready()
    {
        Layer=5;
        targetFrames=new TargetFrameLayer();AddChild(targetFrames);
        var speed=new Button{Text="倍速 ×1",Size=new(140,40)};
        speedButton=speed;BattleUiArt.Skin(speed);speed.Icon=BattleUiArt.Texture("icon_speed");speed.ExpandIcon=true;speed.AddThemeConstantOverride("icon_max_width",22);
        speed.AddThemeFontSizeOverride("font_size",20);AddChild(speed);
        speed.Pressed+=()=>{manager.set_battle_speed(manager.get_battle_speed_scale()==1?2:manager.get_battle_speed_scale()==2?3:1);speed.Text=$"倍速 ×{manager.get_battle_speed_scale()}";};
        pauseButton=new Button{Text="暂停",Size=new(156,40)};
        BattleUiArt.Skin(pauseButton);pauseButton.Icon=BattleUiArt.Texture("icon_pause");pauseButton.ExpandIcon=true;pauseButton.AddThemeConstantOverride("icon_max_width",20);
        pauseButton.AddThemeFontSizeOverride("font_size",18);AddChild(pauseButton);
        pauseButton.Pressed+=manager.toggle_battle_pause;
        retreatButton=new Button{Text="撤退",Size=new(140,40)};BattleUiArt.Skin(retreatButton);AddChild(retreatButton);
        retreatButton.Pressed+=manager.queue_player_retreat;
        retreatNotice=new Label{HorizontalAlignment=HorizontalAlignment.Center,Size=new(460,32),MouseFilter=Control.MouseFilterEnum.Ignore};
        retreatNotice.AddThemeFontSizeOverride("font_size",21);retreatNotice.AddThemeColorOverride("font_color",new Color("d6a28b"));AddChild(retreatNotice);
        refresh_pause_button();
        CallDeferred(nameof(PrepareInfo));
    }
    public void refresh_pause_button()
    {
        if(pauseButton is not null&&GodotObject.IsInstanceValid(pauseButton))
            pauseButton.Text=manager.is_battle_paused()?"继续":"暂停";
    }
    public void ShowTargetFrames(IEnumerable<GodotObject> targets,Color color,float duration=.55f)
        => ShowTargetFrames(targets,color,duration,null,null);

    /// <summary>立刻移除当前的目标框和每个目标旁的命中信息。</summary>
    public void ClearTargetFrames()
    {
        foreach(var preview in activeTargetPreviews)
            if(GodotObject.IsInstanceValid(preview)) preview.QueueFree();
        activeTargetPreviews.Clear();
        targetFrames?.Hide();
    }

    /// <summary>
    /// 显示可选目标框，并在每个目标旁边单独放置命中/会心/状态成功率。
    /// 信息牌不压住角色主体：左侧角色放右边，右侧角色放左边，并在视口内自动夹紧。
    /// </summary>
    public void ShowTargetFrames(IEnumerable<GodotObject> targets,Color color,float duration,ActiveSkillData? skill,GodotObject? caster)
    {
        ClearTargetFrames();
        // 不再依赖一张全屏画布的坐标换算：每个框直接挂在覆盖层，和血条使用完全相同的屏幕坐标系。
        foreach(var unit in targets.OfType<Node2D>().Where(GodotObject.IsInstanceValid).Distinct())
        {
            var rect=manager.get_unit_screen_rect(unit).Grow(8);
            var frame=new Control{Size=rect.Size,Position=rect.Position,ZIndex=220,MouseFilter=Control.MouseFilterEnum.Ignore};
            var edge=28f*BattleUiArt.Scale(GetViewport());
            foreach(var (id,pos) in new[]{("tl",Vector2.Zero),("tr",new Vector2(rect.Size.X-edge,0)),("bl",new Vector2(0,rect.Size.Y-edge)),("br",rect.Size-new Vector2(edge,edge))}){var corner=BattleUiArt.Icon($"target_corner_{id}",new(edge,edge));corner.Position=pos;frame.AddChild(corner);}
            AddChild(frame);
            activeTargetPreviews.Add(frame);
            frame.QueueRedraw();

            Control? detail=null;
            if(skill is not null && GodotObject.IsInstanceValid(caster))
            {
                detail=CreateTargetInfoBadge(skill,caster!,unit,color);
                if(detail is not null)
                {
                    AddChild(detail);
                    activeTargetPreviews.Add(detail);
                    detail.Position=PlaceTargetInfo(unit,detail.Size);
                }
            }

            if(float.IsFinite(duration)){var tween=frame.CreateTween();tween.TweenInterval(duration);tween.TweenProperty(frame,"modulate:a",0f,.18f);tween.TweenCallback(Callable.From(frame.QueueFree));}
            if(detail is not null && float.IsFinite(duration))
            {
                var detailTween=detail.CreateTween();detailTween.TweenInterval(duration);detailTween.TweenProperty(detail,"modulate:a",0f,.18f);detailTween.TweenCallback(Callable.From(detail.QueueFree));
            }
        }
    }

    private static Control? CreateTargetInfoBadge(ActiveSkillData skill,GodotObject caster,GodotObject target,Color tint)
    {
        if(!GodotObject.IsInstanceValid(caster)||!GodotObject.IsInstanceValid(target))return null;
        var lines=new List<string>();
        var hasDamage=!Mathf.IsZeroApprox(skill.base_damage)||skill.damage_bonuses.Count>0||!string.IsNullOrWhiteSpace(skill.damage_formula);
        if(hasDamage)
        {
            if(skill.damage_type is "soul" or "spiritual") lines.Add("命中  必中");
            else
            {
                var hit=BattleActionResolver.preview_hit_chance(caster,target);
                var crit=BattleActionResolver.preview_crit_chance(caster,target);
                lines.Add($"命中  {hit*100f:0}%");
                lines.Add($"暴击  {crit*100f:0}%");
            }
        }
        foreach(var effect in skill.status_effects.Where(_ => target is not ArtifactBody))
        {
            var id=effect.GetValueOrDefault("status_id","").AsString();
            if(string.IsNullOrWhiteSpace(id))continue;
            var formula=effect.GetValueOrDefault("hit_formula","1.0").AsString();
            var chance=BattleActionResolver.evaluate_formula(formula,caster,target);
            var status=GD.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres");
            var name=status is not null&&!string.IsNullOrWhiteSpace(status.name)?status.name:id;
            lines.Add($"{name}  {chance*100f:0}%");
        }
        if(lines.Count==0)return null;
        var targetName=target.Get("entity_name").AsString();
        if(string.IsNullOrWhiteSpace(targetName))targetName="目标";
        lines.Insert(0,targetName);
        var panel=new Panel{Size=new Vector2(178,Mathf.Clamp(16+lines.Count*19,54,122)),MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=225};
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat
        {
            BgColor=new Color(.025f,.04f,.035f,.92f),
            BorderColor=new Color(tint.R,tint.G,tint.B,.9f),
            BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,
            CornerRadiusTopLeft=7,CornerRadiusTopRight=7,CornerRadiusBottomLeft=7,CornerRadiusBottomRight=7,
            ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=5,ContentMarginBottom=5
        });
        var label=new Label{Text=string.Join("\n",lines),Position=new Vector2(8,5),Size=new Vector2(panel.Size.X-16,panel.Size.Y-10),MouseFilter=Control.MouseFilterEnum.Ignore,ClipText=true};
        label.AddThemeFontSizeOverride("font_size",13);
        label.AddThemeColorOverride("font_color",new Color(.94f,.92f,.82f));
        label.AddThemeColorOverride("font_outline_color",new Color(0,0,0,.85f));label.AddThemeConstantOverride("outline_size",3);
        panel.AddChild(label);
        return panel;
    }

    private Vector2 PlaceTargetInfo(Node2D target,Vector2 badgeSize)
    {
        var viewportSize=GetViewport().GetVisibleRect().Size;
        var center=target.GetGlobalTransformWithCanvas().Origin;
        var placeLeft=center.X>viewportSize.X*.58f;
        var x=placeLeft?center.X-badgeSize.X-116f:center.X+116f;
        var y=center.Y-218f;
        return new Vector2(Mathf.Clamp(x,8f,Mathf.Max(8f,viewportSize.X-badgeSize.X-8f)),Mathf.Clamp(y,8f,Mathf.Max(8f,viewportSize.Y-badgeSize.Y-8f)));
    }
    private void PrepareInfo()
    {
        info=GetTree().CurrentScene?.GetNodeOrNull<InfoPanel>("UILayer/InfoPanel");
        if(info is null)return;
        var layer=new CanvasLayer{Layer=90,Name="BattleCharacterPanel"};AddChild(layer);info.Reparent(layer);
    }
    public override void _Process(double delta)
    {
        RefreshHoveredStatusInfo();
        var scale=BattleUiArt.Scale(GetViewport());var origin=BattleUiArt.Origin(GetViewport());
        if(retreatButton is not null){retreatButton.Scale=Vector2.One*scale;retreatButton.Position=origin+new Vector2(1506,18)*scale;retreatButton.Disabled=!manager.can_queue_retreat();}
        if(retreatNotice is not null){retreatNotice.Scale=Vector2.One*scale;retreatNotice.Position=origin+new Vector2(606,110)*scale;retreatNotice.Text=manager.get_retreat_notice();}
        if(speedButton is not null){speedButton.Scale=Vector2.One*scale;speedButton.Position=origin+new Vector2(1190,18)*scale;}
        if(pauseButton is not null){pauseButton.Scale=Vector2.One*scale;pauseButton.Position=origin+new Vector2(1340,18)*scale;}

        foreach(var dead in views.Keys.Where(u=>!IsInstanceValid(u)).ToArray()){views[dead].QueueFree();views.Remove(dead);signatures.Remove(dead);queueSignatures.Remove(dead);}
        foreach(var unit in manager.all_units.OfType<Creature>().Where(IsInstanceValid)){
            if(!views.TryGetValue(unit,out var view)){
                // 视图为队列和血条预留同一块屏幕空间；队列本身只绘制紧凑图标。
                // 给我方“队首靠右、向左展开”和敌方相反的布局留出空间。
                view=new Control{Size=new(232,110),MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=90};AddChild(view);views[unit]=view;
                var queue=new Control{Name="Queue",Visible=false,MouseFilter=Control.MouseFilterEnum.Ignore};queue.AddChild(new Control{Name="Cards"});view.AddChild(queue);
                var bar=new BattleHealthBar{Name="Health",Size=new(232,54),IsAlly=unit.faction==(int)WorldEntity.Faction.PLAYER};view.AddChild(bar);
                var name=new Label{Name="Name",Position=new(24,9),Size=new(92,24),ClipText=true,MouseFilter=Control.MouseFilterEnum.Ignore};name.AddThemeFontSizeOverride("font_size",16);name.AddThemeColorOverride("font_color",BattleUiArt.Text);view.AddChild(name);
                var label=new Label{Name="Value",Position=new(122,9),Size=new(86,24),ClipText=true,HorizontalAlignment=HorizontalAlignment.Right,MouseFilter=Control.MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",15);label.AddThemeColorOverride("font_color",BattleUiArt.Text);view.AddChild(label);
                var intent=new Label{Name="Intent",Position=new(0,57),Size=new(232,20),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};intent.AddThemeFontSizeOverride("font_size",11);view.AddChild(intent);
                var statuses=new HFlowContainer{Name="Statuses",Position=new(0,-38),Size=new(232,36),MouseFilter=Control.MouseFilterEnum.Ignore};
                statuses.AddThemeConstantOverride("separation",4);view.AddChild(statuses);
            }
            view.Scale=Vector2.One*scale;
            var team=unit.faction==(int)WorldEntity.Faction.PLAYER?manager.player_units:manager.enemy_units;
            var stagger=team.Count==5 && team.IndexOf(unit)==1?30f:0f;
            view.Position=unit.GetGlobalTransformWithCanvas().Origin-new Vector2(116*scale,manager.get_unit_display_height()+(64+stagger)*scale);
            view.GetNode<BattleHealthBar>("Health").MaxValue=Math.Max(1,unit.max_health);view.GetNode<BattleHealthBar>("Health").Value=unit.current_health;view.GetNode<BattleHealthBar>("Health").QueueRedraw();
            view.GetNode<Label>("Name").Text=unit.entity_name;
            view.GetNode<Label>("Value").Text=$"{unit.current_health}/{unit.max_health}";
            var snapshot=manager.get_queue_snapshot_for_unit(unit);
            view.GetNode<Label>("Intent").Text=manager.get_segment_phase()==BattleManager.SegmentPhase.Executing && snapshot.Count>0 && snapshot[0].GetValueOrDefault("visible",false).AsBool()?snapshot[0].GetValueOrDefault("text","").AsString():"";
            var statusRow=view.GetNode<HFlowContainer>("Statuses");
            statusRow.Position=new Vector2(0,-statusRow.GetCombinedMinimumSize().Y-4);
            var entries=unit.status_snapshot()
                .Where(s=>s.effect is not null&&!IsInternalStatusBadge(s.effect.effect_id))
                .Select(s=>(id:s.effect!.effect_id,name:s.effect.name.Length>0?s.effect.name:s.effect.effect_id,count:s.stacks,tip:s.effect.description,bad:s.effect.is_debuff,instance:s)).ToList();
            if(unit.shield>0)entries.Add(("护盾","护盾",unit.shield,$"当前护盾：{unit.shield}。受击时按吸收的伤害扣除；此资源没有固定层数上限。",false,null!));
            if(unit.qi_blood>0)entries.Add(("气血","气血",unit.qi_blood,$"气血层数：{unit.qi_blood}/{unit.max_qi_blood}（上限 {unit.max_qi_blood}）。每下降1层，按当前回落回复率恢复生命；基础回复率为最大生命的5%，可由明确效果调整。战斗结束清空。",false,null!));
            var battleQi=manager.get_battle_qi(unit);
            if(battleQi>0)
            {
                var manaCapacity=manager.get_effective_mana_capacity(unit);
                var threshold=manaCapacity>0?$"周天触发阈值：{2*manaCapacity} 炁（当前法力上限 {manaCapacity}）。达到阈值立即完成周天并清空炁，超额不保留；这是触发阈值，不是可储存层数上限。":"当前法力上限为 0，不触发周天；炁没有固定层数上限。";
                entries.Add(("炁","炁",battleQi,$"当前炁：{battleQi}。炁是战斗资源，不是法力费用；战斗结束时清空。{threshold}",false,null!));
            }
            if(unit.blood_furnace_value>0)
            {
                var bonus=unit.blood_furnace_value/2;
                entries.Add(("血炉","血炉",unit.blood_furnace_value,$"血炉层数：{unit.blood_furnace_value}/{unit.blood_furnace_value_cap}（上限 {unit.blood_furnace_value_cap}）。每2点提高1%物理伤害，当前提供物理伤害 +{bonus}%；可被相关招式消耗；战斗结束时清空。",false,null!));
            }
            var sig=string.Join("|",entries.Select(e=>$"{e.id}:{e.name}:{e.count}:{e.bad}"));if(signatures.GetValueOrDefault(unit)==sig)continue;signatures[unit]=sig;
            var row=view.GetNode<HFlowContainer>("Statuses");foreach(var child in row.GetChildren()){row.RemoveChild(child);child.QueueFree();}
            foreach(var e in entries) row.AddChild(CreateStatusBadge(e.id,e.name,e.count,e.tip,e.bad,e.instance));
        }
    }
    private void UpdateQueue(Creature unit,Control view)
    {
        var snapshot=manager.get_queue_snapshot_for_unit(unit);
        var queue=view.GetNode<Control>("Queue");queue.Visible=snapshot.Count>0;
        var signature=string.Join("|",snapshot.Select(entry=>$"{entry.GetValueOrDefault("index",-1).AsInt32()}:{entry.GetValueOrDefault("text","").AsString()}:{entry.GetValueOrDefault("phase","").AsString()}:{entry.GetValueOrDefault("icon_path","").AsString()}:{entry.GetValueOrDefault("front",false).AsBool()}:{entry.GetValueOrDefault("visible",false).AsBool()}:{entry.GetValueOrDefault("nian",0).AsInt32()}"));
        var cards=view.GetNode<Control>("Queue/Cards");
        if(queueSignatures.GetValueOrDefault(unit)!=signature)
        {
            queueSignatures[unit]=signature;
            foreach(var child in cards.GetChildren()){cards.RemoveChild(child);child.QueueFree();}
            var player=unit.Get("faction").AsInt32()==(int)WorldEntity.Faction.PLAYER;
            foreach(var entry in snapshot)
            {
                var queueIndex=entry.GetValueOrDefault("index",-1).AsInt32();
                var front=entry.GetValueOrDefault("front",false).AsBool();
                var visible=entry.GetValueOrDefault("visible",false).AsBool();
                // 队列是角色头顶的轻量提示；敌方只会进入一个首招条目。
                // 未看破时单独绘制问号和看破进度，不伪装成真实技能牌。
                var slotSize=front?new Vector2(132,140):new Vector2(82,90);
                var item=new Control{Name=$"QueueSkill{queueIndex}",Size=slotSize,MouseFilter=Control.MouseFilterEnum.Ignore};
                item.SetMeta("queue_index",queueIndex);
                var iconPath=entry.GetValueOrDefault("icon_path","").AsString();
                if(!visible && front)
                {
                    var unknown=new UnknownIntentVisual
                    {
                        Name="UnknownIntent",
                        Position=new Vector2(10,0),
                        Size=new Vector2(112,112),
                        RevealProgress=entry.GetValueOrDefault("intent_progress",0f).AsSingle(),
                        MouseFilter=Control.MouseFilterEnum.Ignore,
                        ZIndex=2
                    };
                    item.AddChild(unknown);
                }
                else if(visible && !string.IsNullOrWhiteSpace(iconPath))
                {
                    var icon=new QueueSkillVisual
                    {
                        Name="Icon",
                        Texture=ResourceLoader.Load<Texture2D>(iconPath),
                        Phase=entry.GetValueOrDefault("phase","准备").AsString(),
                        Progress=entry.GetValueOrDefault("progress",0f).AsSingle(),
                        Position=front?new Vector2(10,0):new Vector2(7,0),
                        Size=front?new Vector2(112,112):new Vector2(68,68),
                        Cover=false,
                        ClipContents=true,
                        MouseFilter=Control.MouseFilterEnum.Ignore,
                        ZIndex=2
                    };
                    item.AddChild(icon);
                    var textLabel=new Label{Name="SkillName",Text=entry.GetValueOrDefault("text","技能").AsString(),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore,Position=front?new Vector2(4,115):new Vector2(2,70),Size=front?new Vector2(slotSize.X-8,18):new Vector2(slotSize.X-4,16)};
                    textLabel.AddThemeFontSizeOverride("font_size",front?15:12);textLabel.AddThemeColorOverride("font_color",new Color(.95f,.88f,.64f));item.AddChild(textLabel);
                    var nian=entry.GetValueOrDefault("nian",0).AsInt32();
                    if(nian>0)
                    {
                        var badge=new Panel{Position=front?new Vector2(72,5):new Vector2(43,3),Size=front?new Vector2(48,24):new Vector2(38,19),MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=4};
                        badge.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color(.08f,.12f,.10f,.92f),BorderColor=new Color("bda36d",.8f),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6});
                        var count=new Label{Text=$"念 {nian}",Size=badge.Size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};
                        count.AddThemeFontSizeOverride("font_size",front?12:10);count.AddThemeColorOverride("font_color",new Color("efe3bf"));badge.AddChild(count);item.AddChild(badge);
                    }
                }
                const float step=86f, frontWidth=132f;
                // 队首中心固定在角色正上方；我方从队首向左展开，敌方从队首向右展开。
                // 旧实现把队列贴到 600px 视图的边缘，导致双方图标都被推到屏幕中间。
                var frontX=300f-frontWidth*.5f;
                var x=player ? frontX-queueIndex*step : frontX+queueIndex*step;
                item.Position=new Vector2(x,0);
                item.ZIndex=front?20:10-queueIndex;
                cards.AddChild(item);
            }
        }
        foreach(var item in cards.GetChildren().OfType<Control>())
        {
            var index=item.HasMeta("queue_index")?item.GetMeta("queue_index").AsInt32():item.GetIndex();if(index<0||index>=snapshot.Count)continue;
            var current=snapshot[index];
            var progressValue=current.GetValueOrDefault("progress",0f).AsSingle();
            var visual=item.GetNodeOrNull<QueueSkillVisual>("Icon");
            if(visual is not null)
            {
                visual.Phase=current.GetValueOrDefault("phase","准备").AsString();
                visual.Progress=progressValue;
                visual.QueueRedraw();
            }
            var unknown=item.GetNodeOrNull<UnknownIntentVisual>("UnknownIntent");
            if(unknown is not null)
            {
                unknown.RevealProgress=current.GetValueOrDefault("intent_progress",0f).AsSingle();
                unknown.QueueRedraw();
            }
        }
    }
    private ActiveSkillData snapshotSkill(Creature unit,int index)
    {
        var queue=manager.get_battle_unit(unit).skill_queue;
        return index>=0&&index<queue.Count?queue[index].skill:new ActiveSkillData{skill_name=""};
    }
    private Control CreateStatusBadge(string id,string name,int count,string tip,bool bad,StatusInstance? instance)
    {
        var badge=new PanelContainer{CustomMinimumSize=new(36,36),MouseFilter=Control.MouseFilterEnum.Stop};
        badge.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=bad?new Color(.26f,.07f,.06f,.95f):new Color(.035f,.09f,.07f,.95f),BorderColor=bad?new Color(.94f,.35f,.28f):new Color(.52f,.80f,.58f),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=9,CornerRadiusTopRight=9,CornerRadiusBottomLeft=9,CornerRadiusBottomRight=9});
        var icon=new TextureRect{Texture=GetStatusIcon(id,instance),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);badge.AddChild(icon);
        if(!id.StartsWith("心法:",StringComparison.Ordinal))
        {
            var stack=new Label{Text=count.ToString(),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Position=new(17,16),Size=new(18,18),MouseFilter=Control.MouseFilterEnum.Ignore};stack.AddThemeFontSizeOverride("font_size",15);stack.AddThemeColorOverride("font_outline_color",Colors.Black);stack.AddThemeConstantOverride("outline_size",4);badge.AddChild(stack);
        }
        badge.MouseEntered+=()=>ShowStatusInfo(badge,id,instance,name,count,tip);
        badge.MouseExited+=HideStatusInfo;
        return badge;
    }
    private void ShowStatusInfo(Control badge,string id,StatusInstance? instance,string name,int count,string tip)
    {
        hoveredStatusBadge=badge;
        hoveredStatus=instance;
        hoveredStatusId=id;
        hoveredStatusName=name;
        hoveredStatusTip=tip;
        hoveredStatusCount=count;
        lastStatusHoverText="";
        RefreshHoveredStatusInfo();
    }
    private void HideStatusInfo()
    {
        if(hoveredStatusBadge is not null)HoverInfoManager.HideFor(hoveredStatusBadge);
        hoveredStatus=null;hoveredStatusBadge=null;hoveredStatusId=hoveredStatusName=hoveredStatusTip=lastStatusHoverText="";hoveredStatusCount=0;
    }
    private void RefreshHoveredStatusInfo()
    {
        if(hoveredStatusBadge is null||!GodotObject.IsInstanceValid(hoveredStatusBadge)||!hoveredStatusBadge.IsVisibleInTree())return;
        string title,body,subtitle;
        Texture2D? icon;
        if(hoveredStatus is { } status)
        {
            if(!GodotObject.IsInstanceValid(status)||status.effect is null){HideStatusInfo();return;}
            var effect=status.effect;
            title=string.IsNullOrWhiteSpace(effect.name)?effect.effect_id:effect.name;
            body=$"{effect.description}\n\n{status_duration_text(status,effect)}";
            subtitle=hoveredStatusId.StartsWith("心法:",StringComparison.Ordinal)
                ?"心法效果"
                :$"战斗状态 · 层数 {status.stacks}/{Math.Max(1,effect.max_stacks)}（上限 {Math.Max(1,effect.max_stacks)}）";
            icon=GetStatusIcon(hoveredStatusId,status);
        }
        else
        {
            title=$"{hoveredStatusName} ×{hoveredStatusCount}";
            body=hoveredStatusTip;
            subtitle="战斗状态";
            icon=GetStatusIcon(hoveredStatusId,null);
        }
        title=CardPresentation.Underline(title);
        body=CardPresentation.Underline(body);
        var key=$"{title}\n{subtitle}\n{body}";
        if(key==lastStatusHoverText)return;
        lastStatusHoverText=key;
        HoverInfoManager.SetInfo(hoveredStatusBadge,new HoverInfoManager.HoverInfoContent(title,body,icon,subtitle));
    }
    private static string status_duration_text(StatusInstance instance,StatusEffect effect)=>effect.duration_type switch
    {
        StatusEffect.DurationType.Frames when instance.remaining_frames>0=>$"剩余 {Mathf.CeilToInt(instance.remaining_frames)} 帧",
        StatusEffect.DurationType.Hits=>$"剩余受击次数：{instance.remaining_hits}",
        StatusEffect.DurationType.Trigger=>instance.source_id.StartsWith("power:")?"持续至本场战斗结束":"持续至触发条件满足或被移除",
        StatusEffect.DurationType.Turns when instance.remaining_duration>0=>$"剩余 {instance.remaining_duration} 回合",
        _=>"持续状态"
    };
    private static bool IsInternalStatusBadge(string id)=>id is "化解" or "身法" or "借力";
    internal static Texture2D? GetStatusIcon(string id,StatusInstance? instance)
    {
        if(id.StartsWith("心法:"))
        {
            var heartIcon=instance?.source_name switch
            {
                "乘物游骸真经"=>"心法_乘物游骸",
                "内景损益篇"=>"心法_内景损益",
                "灵枢经筋篇"=>"心法_灵枢经筋",
                "补虚行血诀"=>"心法_补虚行血",
                "赤阳参同经"=>"心法_赤阳参同",
                "阴符应物经"=>"心法_阴符应物",
                "玄枢含章经"=>"心法_玄枢含章",
                "斫基损契玄经"=>"心法_斫基损契",
                "胎息经"=>"心法_黄庭养形",
                _=>null
            };
            return heartIcon is null?GetLegacyStatusIcon(5):LoadStatusIcon(heartIcon);
        }
        var iconName=id switch
        {
            "护盾" or "护持" or "护盾生成" or "法力护罩"=>"法力护罩",
            "内伤"=>"内伤",
            "气血"=>"气血",
            "炁"=>"炁",
            "周天"=>"周天",
            "血炉"=>"血炉",
            "外伤"=>"外伤",
            "身法"=>"身法",
            "化解"=>"化解",
            "借力"=>"借力",
            "游骸"=>"游骸",
            "逐猎"=>"逐猎",
            _=>null
        };
        if(iconName is not null)
        {
            var icon=LoadStatusIcon(iconName);
            if(icon is not null)return icon;
        }
        return id switch
        {
            "血炉"=>GetLegacyStatusIcon(1),
            "疲倦" or "exhausted"=>GetLegacyStatusIcon(6),
            _=>null
        };
    }
    private static Texture2D? LoadStatusIcon(string name)
    {
        var pngPath = $"res://textures/ui/combat/status_icons/{name}_v1.png";
        if (ResourceLoader.Exists(pngPath)) return GD.Load<Texture2D>(pngPath);
        var svgPath = $"res://textures/ui/combat/status_icons/{name}_v1.svg";
        return ResourceLoader.Exists(svgPath) ? GD.Load<Texture2D>(svgPath) : null;
    }
    private static Texture2D? GetLegacyStatusIcon(int slot)
    {
        var atlas=GD.Load<Texture2D>(StatusIconAtlas); if(atlas is null)return null;
        var width=atlas.GetWidth()/4f;var height=atlas.GetHeight()/2f;
        return new AtlasTexture{Atlas=atlas,Region=new Rect2(slot%4*width,slot/4*height,width,height)};
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(input is not InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Left} mouse)return;
        var unit=manager.all_units.OfType<WorldEntity>().Where(IsInstanceValid).FirstOrDefault(u=>manager.get_unit_screen_rect(u).HasPoint(mouse.Position));
        if(unit is null)return;
        if(mouse.DoubleClick){if(info is null)return;info.show_entity(unit);GetViewport().SetInputAsHandled();return;}
        if(!manager.is_waiting_for_skill_target() && unit.Get("faction").AsInt32()==(int)WorldEntity.Faction.PLAYER){manager.select_active_unit(unit);GetViewport().SetInputAsHandled();}
    }
    public void ShowResponse(GodotObject unit,ActiveSkillData skill)
    {
        if(unit is not Node2D node)return;
        ShowSkillToast(node,skill,"应对");
    }
    /// <summary>敌方的每一张实际出牌都在头顶完整亮相，便于辨识连续行动。</summary>
    public void ShowPlayedCard(GodotObject unit,ActiveSkillData skill)
    {
        if(unit is not Node2D node)return;
        ShowSkillToast(node,skill,"施放");
    }
    private void ShowSkillToast(Node2D node,ActiveSkillData skill,string prefix)
    {
        var panel=new PanelContainer{Size=new(170,48),ZIndex=80,MouseFilter=Control.MouseFilterEnum.Ignore};
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color(.05f,.07f,.06f,.94f),BorderColor=new Color(.72f,.78f,.70f,.94f),BorderWidthLeft=2,BorderWidthTop=2,BorderWidthRight=2,BorderWidthBottom=2,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8});
        var label=new Label{Text=$"{prefix} · {skill.skill_name}",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};label.AddThemeFontSizeOverride("font_size",16);label.AddThemeColorOverride("font_color",new Color(.88f,.91f,.82f));panel.AddChild(label);AddChild(panel);
        panel.Position=node.GetGlobalTransformWithCanvas().Origin-new Vector2(85,480);
        var tween=panel.CreateTween();tween.TweenProperty(panel,"position:y",panel.Position.Y-22,.18).SetTrans(Tween.TransitionType.Sine);tween.TweenInterval(.82);tween.TweenProperty(panel,"modulate:a",0f,.28);tween.TweenCallback(Callable.From(panel.QueueFree));
    }
}

/// <summary>敌方未看破时的唯一提示：大问号 + 不泄露技能时长的看破进度。</summary>
public sealed partial class UnknownIntentVisual : Control
{
    public float RevealProgress { get; set; }

    public override void _Draw()
    {
        var bounds=new Rect2(2,2,Mathf.Max(1,Size.X-4),Mathf.Max(1,Size.Y-4));
        var panel=new StyleBoxFlat
        {
            BgColor=new Color(.018f,.028f,.03f,.96f),
            BorderColor=new Color(.44f,.57f,.58f,.94f),
            BorderWidthLeft=2,BorderWidthTop=2,BorderWidthRight=2,BorderWidthBottom=2,
            CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12
        };
        DrawStyleBox(panel,bounds);
        var center=bounds.GetCenter();
        var radius=Mathf.Min(bounds.Size.X,bounds.Size.Y)*.39f;
        DrawArc(center,radius,-Mathf.Pi*.5f,-Mathf.Pi*.5f+Mathf.Pi*2f,48,new Color(.08f,.14f,.15f,.96f),4f,true);
        var progress=Mathf.Clamp(RevealProgress,0f,1f);
        if(progress>0.001f)
            DrawArc(center,radius,-Mathf.Pi*.5f,-Mathf.Pi*.5f+Mathf.Pi*2f*progress,48,new Color(.64f,.86f,.86f,.98f),5f,true);
        var font=ThemeDB.FallbackFont;
        var text="?";
        var textSize=font.GetStringSize(text,HorizontalAlignment.Left,-1,58);
        DrawString(font,center-new Vector2(textSize.X*.5f,-textSize.Y*.28f),text,HorizontalAlignment.Left,-1,58,new Color(.91f,.95f,.89f));
    }
}

/// <summary>
/// 队列技能图标的阶段可视化：准备阶段顺时针亮起，收招阶段顺时针退光。
/// 图标本身保持在队列牌面内，不依赖额外的进度条素材。
/// </summary>
public sealed partial class QueueSkillVisual : Control
{
    public Texture2D? Texture { get; set; }
    public string Phase { get; set; } = "准备";
    public float Progress { get; set; }
    /// <summary>队首牌面使用裁切填充，让图标成为牌面主体；后续队列仍保持完整图标。</summary>
    public bool Cover { get; set; }

    public override void _Draw()
    {
        var progress=Mathf.Clamp(Progress,0f,1f);
        var rect=new Rect2(new Vector2(2,2),new Vector2(Mathf.Max(1,Size.X-4),Mathf.Max(1,Size.Y-4)));
        if(Texture is not null)
        {
            var source=Texture.GetSize();
            if(source.X>0 && source.Y>0)
            {
                var scale=Cover
                    ? Mathf.Max(rect.Size.X/source.X,rect.Size.Y/source.Y)
                    : Mathf.Min(rect.Size.X/source.X,rect.Size.Y/source.Y);
                var drawSize=source*scale;
                var drawRect=new Rect2(rect.Position+(rect.Size-drawSize)*.5f,drawSize);
                var brightness=Phase=="准备" ? .34f+.66f*progress : Phase=="收招" ? 1f-.62f*progress : 1f;
                DrawTextureRect(Texture,drawRect,false,new Color(brightness,brightness,brightness,1f));
                if(Phase=="准备" || Phase=="收招")
                {
                    var dim=Phase=="准备" ? (1f-progress)*.58f : progress*.58f;
                    DrawRect(drawRect,new Color(0,0,0,dim),true);
                }
            }
        }
        var center=Size*.5f;
        var radius=Mathf.Max(4f,Mathf.Min(Size.X,Size.Y)*.5f-2f);
        var track=new Color(.08f,.10f,.09f,.78f);
        DrawArc(center,radius,0f,Mathf.Pi*2f,36,track,2f,true);
        if(Phase=="准备")
        {
            if(progress>0.001f)DrawArc(center,radius,-Mathf.Pi*.5f,-Mathf.Pi*.5f+Mathf.Pi*2f*progress,36,new Color(1f,.76f,.28f,.98f),3f,true);
        }
        else if(Phase=="收招")
        {
            if(progress<.999f)DrawArc(center,radius,-Mathf.Pi*.5f,-Mathf.Pi*.5f+Mathf.Pi*2f*(1f-progress),36,new Color(.48f,.78f,.96f,.95f),3f,true);
        }
    }
}

/// <summary>手牌选中后的受影响目标提示，独立于手牌本身，始终绘制在角色之上。</summary>
public partial class TargetFrameLayer : Control
{
    private GodotObject[] targets=[]; private Color color; private double until;
    public TargetFrameLayer(){MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);ZIndex=70;}
    public void ShowTargets(IEnumerable<GodotObject> values,Color tint,float duration)
    {
        targets=values.Where(GodotObject.IsInstanceValid).Distinct().ToArray();color=tint;until=Time.GetTicksMsec()/1000.0+duration;Show();QueueRedraw();
    }
    public override void _Process(double _)
    {
        if(Time.GetTicksMsec()/1000.0>until){Hide();return;} QueueRedraw();
    }
    public override void _Draw()
    {
        var pulse=.75f+.25f*Mathf.Sin((float)Time.GetTicksMsec()/1000f*9f);
        foreach(var unit in targets.OfType<Node2D>().Where(GodotObject.IsInstanceValid))
        {
            var center=unit.GetGlobalTransformWithCanvas().Origin;
            BattleCornerFrame.Paint(this,new Rect2(center-new Vector2(94,285),new Vector2(188,315)),new Color(color.R,color.G,color.B,pulse));
        }
    }
}
