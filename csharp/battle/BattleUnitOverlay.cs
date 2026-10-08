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
    private PanelContainer? statusInfoPanel;
    private Label? statusInfoLabel;
    private StatusInstance? hoveredStatus;
    private Button? pauseButton;
    public void Setup(BattleManager owner){manager=owner;}
    public override void _Ready()
    {
        Layer=5;
        targetFrames=new TargetFrameLayer{Size=GetViewport().GetVisibleRect().Size};AddChild(targetFrames);
        GetViewport().SizeChanged+=()=>{if(targetFrames is not null)targetFrames.Size=GetViewport().GetVisibleRect().Size;};
        var speed=new Button{Text="倍速 ×1",Position=new(24,90),Size=new(135,42)};
        speed.AddThemeFontSizeOverride("font_size",20);AddChild(speed);
        speed.Pressed+=()=>{manager.set_battle_speed(manager.get_battle_speed_scale()==1?2:manager.get_battle_speed_scale()==2?3:1);speed.Text=$"倍速 ×{manager.get_battle_speed_scale()}";};
        pauseButton=new Button{Text="暂停 · 空格",Position=new(170,90),Size=new(160,42)};
        pauseButton.AddThemeFontSizeOverride("font_size",18);AddChild(pauseButton);
        pauseButton.Pressed+=manager.toggle_battle_pause;
        refresh_pause_button();
        CallDeferred(nameof(PrepareInfo));
    }
    public void refresh_pause_button()
    {
        if(pauseButton is not null&&GodotObject.IsInstanceValid(pauseButton))
            pauseButton.Text=manager.is_battle_paused()?"继续 · 空格":"暂停 · 空格";
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
            var frame=new BattleCornerFrame{Size=new Vector2(204,330),Position=unit.GetGlobalTransformWithCanvas().Origin-new Vector2(102,292),ZIndex=220,MouseFilter=Control.MouseFilterEnum.Ignore,Modulate=color};
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

            var tween=frame.CreateTween();tween.TweenInterval(duration);tween.TweenProperty(frame,"modulate:a",0f,.18f);tween.TweenCallback(Callable.From(frame.QueueFree));
            if(detail is not null)
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
        foreach(var effect in skill.status_effects)
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
        foreach(var dead in views.Keys.Where(u=>!IsInstanceValid(u)).ToArray()){views[dead].QueueFree();views.Remove(dead);signatures.Remove(dead);queueSignatures.Remove(dead);}
        foreach(var unit in manager.all_units.OfType<Creature>().Where(IsInstanceValid)){
            if(!views.TryGetValue(unit,out var view)){
                // 视图为队列和血条预留同一块屏幕空间；队列本身只绘制紧凑图标。
                // 给我方“队首靠右、向左展开”和敌方相反的布局留出空间。
                view=new Control{Size=new(600,340),MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=90};AddChild(view);views[unit]=view;
                var queue=new Control{Name="Queue",Position=new(0,120),Size=new(600,160),MouseFilter=Control.MouseFilterEnum.Ignore,ClipContents=false};
                var cards=new Control{Name="Cards",Position=new(0,0),Size=new(600,160),MouseFilter=Control.MouseFilterEnum.Ignore,ClipContents=false};
                queue.AddChild(cards);view.AddChild(queue);
                var bar=new ProgressBar{Name="Health",Position=new(185,280),Size=new(230,26),ShowPercentage=false,MouseFilter=Control.MouseFilterEnum.Ignore};
                bar.AddThemeStyleboxOverride("background",new StyleBoxFlat{BgColor=new(.03f,.045f,.04f),CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6});
                bar.AddThemeStyleboxOverride("fill",new StyleBoxFlat{BgColor=new(.68f,.19f,.14f),CornerRadiusTopLeft=5,CornerRadiusTopRight=5,CornerRadiusBottomLeft=5,CornerRadiusBottomRight=5});view.AddChild(bar);
                var label=new Label{Name="Value",Position=new(185,280),Size=new(230,26),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",18);view.AddChild(label);
                var statuses=new HFlowContainer{Name="Statuses",Position=new(170,250),Size=new(260,84),MouseFilter=Control.MouseFilterEnum.Ignore};
                statuses.AddThemeConstantOverride("separation",4);view.AddChild(statuses);
            }
            view.Position=unit.GetGlobalTransformWithCanvas().Origin-new Vector2(300,550);
            UpdateQueue(unit,view);
            view.GetNode<ProgressBar>("Health").MaxValue=Math.Max(1,unit.max_health);view.GetNode<ProgressBar>("Health").Value=unit.current_health;
            view.GetNode<Label>("Value").Text=$"{unit.current_health} / {unit.max_health}";
            var statusRow=view.GetNode<HFlowContainer>("Statuses");
            statusRow.Position=new Vector2(170,272-statusRow.GetCombinedMinimumSize().Y);
            var entries=unit.status_snapshot().Select(s=>(id:s.effect!.effect_id,name:s.effect.name.Length>0?s.effect.name:s.effect.effect_id,count:s.stacks,tip:s.effect.description,bad:s.effect.is_debuff,instance:s)).ToList();
            if(unit.shield>0)entries.Add(("护持","护持",unit.shield,"优先抵扣伤害。",false,null!));
            if(unit.qi_blood>0)entries.Add(("气血","气血",unit.qi_blood,"当前体修气血资源。",false,null!));
            var sig=string.Join("|",entries.Select(e=>$"{e.id}:{e.name}:{e.count}:{e.bad}"));if(signatures.GetValueOrDefault(unit)==sig)continue;signatures[unit]=sig;
            var row=view.GetNode<HFlowContainer>("Statuses");foreach(var child in row.GetChildren()){row.RemoveChild(child);child.QueueFree();}
            foreach(var e in entries) row.AddChild(CreateStatusBadge(e.id,e.name,e.count,e.tip,e.bad,e.instance));
        }
    }
    private void UpdateQueue(Creature unit,Control view)
    {
        var snapshot=manager.get_queue_snapshot_for_unit(unit);
        var queue=view.GetNode<Control>("Queue");queue.Visible=snapshot.Count>0;
        var signature=string.Join("|",snapshot.Select(entry=>$"{entry.GetValueOrDefault("index",-1).AsInt32()}:{entry.GetValueOrDefault("text","").AsString()}:{entry.GetValueOrDefault("phase","").AsString()}:{entry.GetValueOrDefault("icon_path","").AsString()}:{entry.GetValueOrDefault("front",false).AsBool()}:{entry.GetValueOrDefault("visible",false).AsBool()}"));
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
        var badge=new PanelContainer{CustomMinimumSize=new(36,36),TooltipText=$"{name} ×{count}\n{tip}",MouseFilter=Control.MouseFilterEnum.Stop};
        badge.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=bad?new Color(.26f,.07f,.06f,.95f):new Color(.035f,.09f,.07f,.95f),BorderColor=bad?new Color(.94f,.35f,.28f):new Color(.52f,.80f,.58f),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=9,CornerRadiusTopRight=9,CornerRadiusBottomLeft=9,CornerRadiusBottomRight=9});
        var icon=new TextureRect{Texture=GetStatusIcon(id),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);badge.AddChild(icon);
        var stack=new Label{Text=count.ToString(),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Position=new(17,16),Size=new(18,18),MouseFilter=Control.MouseFilterEnum.Ignore};stack.AddThemeFontSizeOverride("font_size",15);stack.AddThemeColorOverride("font_outline_color",Colors.Black);stack.AddThemeConstantOverride("outline_size",4);badge.AddChild(stack);
        if(instance is not null)
        {
            badge.MouseEntered+=()=>ShowStatusInfo(badge,instance);
            badge.MouseExited+=HideStatusInfo;
        }
        return badge;
    }
    private void ShowStatusInfo(Control badge,StatusInstance instance)
    {
        hoveredStatus=instance;
        if(statusInfoPanel is null)
        {
            statusInfoPanel=new PanelContainer{ZIndex=260,MouseFilter=Control.MouseFilterEnum.Ignore,CustomMinimumSize=new Vector2(240,0)};
            statusInfoPanel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color(.025f,.04f,.035f,.96f),BorderColor=new Color(.72f,.78f,.62f,.95f),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=7,CornerRadiusTopRight=7,CornerRadiusBottomLeft=7,CornerRadiusBottomRight=7,ContentMarginLeft=10,ContentMarginRight=10,ContentMarginTop=7,ContentMarginBottom=7});
            statusInfoLabel=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};statusInfoLabel.AddThemeFontSizeOverride("font_size",15);statusInfoLabel.AddThemeColorOverride("font_color",new Color(.94f,.92f,.82f));statusInfoPanel.AddChild(statusInfoLabel);AddChild(statusInfoPanel);
        }
        statusInfoPanel.Show();RefreshHoveredStatusInfo();
        var rect=badge.GetGlobalRect();var size=statusInfoPanel.GetCombinedMinimumSize();var viewport=GetViewport().GetVisibleRect().Size;
        var x=rect.Position.X+rect.Size.X+8; if(x+size.X>viewport.X-8)x=rect.Position.X-size.X-8;
        var y=Mathf.Clamp(rect.Position.Y,8,Mathf.Max(8,viewport.Y-size.Y-8));statusInfoPanel.Position=new Vector2(Mathf.Clamp(x,8,Mathf.Max(8,viewport.X-size.X-8)),y);
    }
    private void HideStatusInfo()
    {
        hoveredStatus=null;statusInfoPanel?.Hide();
    }
    private void RefreshHoveredStatusInfo()
    {
        if(hoveredStatus is null||statusInfoLabel is null||!GodotObject.IsInstanceValid(hoveredStatus)||hoveredStatus.effect is null)
        {if(hoveredStatus is not null)HideStatusInfo();return;}
        var effect=hoveredStatus.effect;var name=string.IsNullOrWhiteSpace(effect.name)?effect.effect_id:effect.name;
        var duration=effect.duration_type switch
        {
            StatusEffect.DurationType.Frames when hoveredStatus.remaining_frames>0=>$"剩余 {Mathf.CeilToInt(hoveredStatus.remaining_frames)} 帧",
            StatusEffect.DurationType.Hits=>$"剩余受击次数：{hoveredStatus.remaining_hits}",
            StatusEffect.DurationType.Trigger=>hoveredStatus.source_id.StartsWith("power:")?"持续至本场战斗结束":"持续至触发条件满足或被移除",
            StatusEffect.DurationType.Turns when hoveredStatus.remaining_duration>0=>$"剩余 {hoveredStatus.remaining_duration} 回合",
            _=>"持续状态"
        };
        statusInfoLabel.Text=$"{name} ×{hoveredStatus.stacks}\n{effect.description}\n{duration}";
    }
    private static Texture2D? GetStatusIcon(string id)
    {
        var atlas=GD.Load<Texture2D>(StatusIconAtlas); if(atlas is null)return null;
        var slot=id switch {"护持" or "化解" or "灵护"=>0,"灼烧"=>1,"身法" or "逐猎"=>2,"剑痕" or "剑意"=>3,"魂隙" or "内伤"=>4,"引灵" or "气血" or "灵涌"=>5,"疲倦"=>6,"折损" or "定影" or "照痕" or "外伤"=>7,_ when id.StartsWith("心法:")=>5,_=>6};
        var width=atlas.GetWidth()/4;var height=atlas.GetHeight()/2;
        return new AtlasTexture{Atlas=atlas,Region=new Rect2(slot%4*width,slot/4*height,width,height)};
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(input is not InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Left} mouse)return;
        var unit=manager.all_units.OfType<WorldEntity>().Where(IsInstanceValid).FirstOrDefault(u=>new Rect2(u.GetGlobalTransformWithCanvas().Origin-new Vector2(95,300),new Vector2(190,310)).HasPoint(mouse.Position));
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
