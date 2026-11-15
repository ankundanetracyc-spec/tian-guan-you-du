using Godot;
using Godot.Collections;

/// <summary>稳定扇形手牌、目标拖拽箭头，以及牌堆与详情浏览。</summary>
public partial class SkillHandPanel : Control
{
    [Signal] public delegate void card_selectedEventHandler(ActiveSkillData skill_data,int index);
    [Signal] public delegate void card_release_attemptedEventHandler(SkillCard card,Vector2 drop_viewport_pos);
    private Control? hand; private Label? energy,mana,stamina,spirit,turn,deckInfo; private Button? drawButton,discardButton,exhaustButton;
    private Panel? browser,detail; private RichTextLabel? browserText,glossary; private bool layoutDirty,shown,observing; private SkillCard? hovered,dragging;
    public WorldEntity? current_unit; public Array<SkillCard> current_cards=[]; public int selected_index=-1; private int previousEnergy=-1,previousMana=-1,previousSpirit=-1,previousMaxStamina=-1,previousMaxMana=-1,previousMaxSpirit=-1; private Creature? previousResourceUnit;
    // 牌面缩放后宽约 211px；保留徽章向外延伸的安全间距，避免相邻牌盖住费用。
    private const float CardWidth=236, CardScale=.88f, ObserveCardScale=.52f;
    public override void _Ready(){AddToGroup("skill_hand_panel");MouseFilter=MouseFilterEnum.Ignore;hand=GetNodeOrNull<Control>("%HandArea");energy=GetNodeOrNull<Label>("%EnergyLabel");mana=GetNodeOrNull<Label>("%ManaLabel");stamina=GetNodeOrNull<Label>("%StaminaLabel");spirit=GetNodeOrNull<Label>("%SpiritLabel");turn=GetNodeOrNull<Label>("%TurnInfoLabel");deckInfo=GetNodeOrNull<Label>("%DeckInfoLabel");create_pile_buttons();create_browser();create_detail();Hide();}
    private static StyleBoxFlat style()=>new(){BgColor=new Color(.035f,.045f,.04f,.96f),BorderColor=new Color(.8f,.65f,.33f,.95f),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=10,CornerRadiusTopRight=10,CornerRadiusBottomLeft=10,CornerRadiusBottomRight=10};
    private void create_pile_buttons(){drawButton=pile_button("抽牌堆\n0",new(Size.X-318,34));discardButton=pile_button("弃牌堆\n0",new(Size.X-110,34));exhaustButton=pile_button("消耗\n0",new(Size.X-214,34));drawButton.Pressed+=()=>open_pile("抽牌堆",0);discardButton.Pressed+=()=>open_pile("弃牌堆",1);exhaustButton.Pressed+=()=>open_pile("消耗牌堆",2);Resized+=()=>{if(drawButton is not null)drawButton.Position=new(Size.X-318,34);if(discardButton is not null)discardButton.Position=new(Size.X-110,34);if(exhaustButton is not null)exhaustButton.Position=new(Size.X-214,34);};}
    private Button pile_button(string text,Vector2 pos){var b=new Button{Text=text,Position=pos,Size=new(92,68),MouseFilter=MouseFilterEnum.Stop};b.AddThemeFontSizeOverride("font_size",16);b.AddThemeStyleboxOverride("normal",style());b.AddThemeStyleboxOverride("hover",new StyleBoxFlat{BgColor=new(.19f,.16f,.09f,.98f),BorderColor=new(1,.78f,.36f,1),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=10,CornerRadiusTopRight=10,CornerRadiusBottomLeft=10,CornerRadiusBottomRight=10});AddChild(b);return b;}
    private CanvasLayer? modalLayer;
    private ScrollContainer? pileScroll;
    private readonly System.Collections.Generic.Dictionary<SkillCard,Tween> motions = new();
    // 和成熟卡牌手牌的做法一致：布局只更新目标值，位置/角度/缩放由每帧的阻尼追踪完成。
    private readonly System.Collections.Generic.Dictionary<SkillCard,(Vector2 position,Vector2 scale,float rotation)> handTargets = new();
    private void create_browser()
    {
        modalLayer=new CanvasLayer{Name="BattleModals",Layer=100}; AddChild(modalLayer);
        browser=new Panel{Name="PileBrowser",Visible=false,MouseFilter=MouseFilterEnum.Stop};
        modalLayer.AddChild(browser); browser.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        browser.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color(.015f,.02f,.018f,1)});
        add_ink_texture(browser);
        var title=new Label{Name="Title",Position=new(32,20),Size=new(600,46)}; title.AddThemeFontSizeOverride("font_size",30);browser.AddChild(title);
        var close=new Button{Name="Close",Text="返回",Size=new(120,48)};close.Pressed+=close_browser;browser.AddChild(close);
        pileScroll=new ScrollContainer{Name="Scroll",HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};
        browser.AddChild(pileScroll);
        pileScroll.AddChild(new Control{Name="PileGrid",MouseFilter=MouseFilterEnum.Ignore});
        GetViewport().SizeChanged+=resize_modals;
    }
    private void resize_modals()
    {
        var screen=GetViewportRect().Size;
        if(browser is not null){browser.Position=Vector2.Zero;browser.Size=screen;browser.GetNode<Button>("Close").Position=new(screen.X-145,18);}
        if(pileScroll is not null){pileScroll.Position=new(40,86);pileScroll.Size=screen-new Vector2(80,110);}
        if(detail is not null){detail.Scale=Vector2.One*Mathf.Min(1,Mathf.Min(screen.X/940,screen.Y/610));detail.Position=(screen-detail.Size*detail.Scale)*.5f;}
    }
    public override void _Input(InputEvent input)
    {
        if(input is InputEventKey {Pressed:true,Keycode:Key.Escape} && (browser?.Visible==true||detail?.Visible==true)){
            if(detail?.Visible==true)detail.Hide();else browser?.Hide();GetViewport().SetInputAsHandled();
        }
    }
    private void create_detail(){detail=new Panel{Visible=false,Size=new(900,570),ZIndex=200,MouseFilter=MouseFilterEnum.Stop};detail.AddThemeStyleboxOverride("panel",style());modalLayer!.AddChild(detail);add_ink_texture(detail);var close=new Button{Text="返回",Position=new(750,18),Size=new(120,42)};close.Pressed+=()=>hide_detail();detail.AddChild(close);glossary=new RichTextLabel{Position=new(490,80),Size=new(375,445),BbcodeEnabled=true};glossary.AddThemeFontSizeOverride("normal_font_size",19);detail.AddChild(glossary);}
    private static void add_ink_texture(Control parent){var texture=ResourceLoader.Load<Texture2D>("res://textures/ui/battle_pile_panel_v1.png");if(texture is null)return;var view=new TextureRect{Texture=texture,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,Modulate=new Color(1,1,1,.28f),MouseFilter=MouseFilterEnum.Ignore};view.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);parent.AddChild(view);}
    public void refresh(WorldEntity unit){current_unit=unit;clear_hand();if(unit is Creature creature){var skills=new Array<ActiveSkillData>();foreach(var resource in creature.active_skills)if(resource is ActiveSkillData skill)skills.Add(skill);append_cards(skills);}_=turn is null?null:turn.Text=unit.entity_name;update_resources();}
    public void set_battle_unit(GodotObject unit){current_unit=unit as WorldEntity;update_resources();}
    /// <summary>同步完整手牌但复用仍在手中的卡牌节点，避免每次抽牌时整排销毁重建。</summary>
    public void populate_hand(Array<ActiveSkillData> skills)=>sync_hand(skills);
    public void draw_to_hand(Array<ActiveSkillData> skills)=>append_cards(skills);
    private SkillCard? create_card(ActiveSkillData skill,int index)
    {
        if(hand is null)return null;
        var scene=ResourceLoader.Load<PackedScene>("res://tscn/panel/BattleCard.tscn");if(scene is null)return null;
        var card=scene.Instantiate<SkillCard>();hand.AddChild(card);
        card.setup(skill,current_unit is not null?current_unit:this,index);
        card.card_selected+=on_card_selected;card.card_drag_played+=on_card_drag_played;card.detail_requested+=show_detail;
        card.MouseEntered+=()=>{if(!observing&&hovered!=card){hovered=card;request_layout();}};
        card.MouseExited+=()=>{if(hovered==card){hovered=null;request_layout();}};
        // 新抽入的牌从牌栏中心平滑进入目标位置，不改变旧牌的当前坐标。
        var width=hand.Size.X>0?hand.Size.X:1500f;card.Position=new Vector2(width*.5f,34);card.Scale=Vector2.One*(observing?ObserveCardScale:CardScale*.82f);
        return card;
    }
    private void append_cards(Array<ActiveSkillData> skills)
    {
        foreach(var skill in skills)
        {
            var card=create_card(skill,current_cards.Count);if(card is not null)current_cards.Add(card);
        }
        update_resources(true);apply_observing_state();request_layout();
    }
    private void sync_hand(Array<ActiveSkillData> skills)
    {
        if(hand is null)return;
        var desired=skills.Where(skill=>skill is not null).ToList();
        var available=current_cards.Where(IsInstanceValid).ToList();
        var ordered=new List<SkillCard>();
        foreach(var skill in desired)
        {
            var card=available.FirstOrDefault(candidate=>candidate.skill_data==skill);
            if(card is not null){available.Remove(card);ordered.Add(card);}
            else
            {
                var created=create_card(skill,ordered.Count);if(created is not null)ordered.Add(created);
            }
        }
        // 不再属于手牌的节点只做淡出，不让它们参与下一帧布局。
        foreach(var stale in available)animate_discard(stale);
        var selected=ordered.FirstOrDefault(card=>card.is_selected);
        current_cards.Clear();foreach(var card in ordered)current_cards.Add(card);
        for(var i=0;i<current_cards.Count;i++)current_cards[i].card_index=i;
        selected_index=selected is null?-1:selected.card_index;
        update_resources(true);apply_observing_state();request_layout();
    }
    public override void _Process(double delta)
    {
        if(!Visible)return;
        update_resources();
        var active=get_dragging_card();
        if(active!=dragging){dragging=active;request_layout();}
        if(dragging is not null && motions.Remove(dragging,out var motion))motion.Kill();
        if(dragging is not null&&!dragging.is_aiming()&&is_point_in_hand_area(GetGlobalMousePosition()))preview_reorder();
        if(layoutDirty)layout_cards();
        animate_hand_targets((float)delta);
        if(dragging is not null)QueueRedraw();
    }
    private void preview_reorder()
    {
        if(hand is null||dragging is null)return;
        var count=current_cards.Count;var step=Mathf.Min(CardWidth,hand.Size.X*.72f/Mathf.Max(1,count-1));
        var start=hand.Size.X*.5f-step*Mathf.Max(0,count-1)*.5f;
        var old=current_cards.IndexOf(dragging);var x=GetGlobalMousePosition().X-hand.GlobalPosition.X;
        var next=Mathf.Clamp(Mathf.RoundToInt((x-start)/step),0,count-1);
        if(next==old||Mathf.Abs(x-(start+old*step))<step*.62f)return;
        current_cards.Remove(dragging);current_cards.Insert(next,dragging);sync_hand_order();request_layout();
    }
    private void sync_hand_order()
    {
        for(var i=0;i<current_cards.Count;i++)current_cards[i].card_index=i;
        if(current_unit is not null&&GetTree().GetFirstNodeInGroup("battle_mgr") is BattleManager manager)
            manager.reorder_hand(current_unit,new Array<ActiveSkillData>(current_cards.Where(c=>c.skill_data is not null).Select(c=>c.skill_data!)));
    }
    private void request_layout()=>layoutDirty=true;
    private readonly List<SkillCard> queued_cards = new();
    public void confirm_card_played(ActiveSkillData skill)
    {
        if(queued_cards.Any(c=>c.skill_data==skill)){release_queued_card(skill);return;}
        var card=current_cards.FirstOrDefault(c=>c.skill_data==skill);
        if(card is null)return;
        if(motions.Remove(card,out var motion))motion.Kill();
        card.cancel_drag();card.MouseFilter=MouseFilterEnum.Ignore;card.SetProcess(false);
        card.Position-=new Vector2(0,90);
        animate_discard(card);
        var message=new Label{Text=$"已打出 · {skill.skill_name}",Position=new Vector2(Size.X*.5f-180,-130),Size=new Vector2(360,45),HorizontalAlignment=HorizontalAlignment.Center,ZIndex=2000,MouseFilter=MouseFilterEnum.Ignore};
        message.AddThemeFontSizeOverride("font_size",26);AddChild(message);
        var tween=message.CreateTween();tween.TweenInterval(.65);tween.TweenProperty(message,"modulate:a",0f,.3);tween.TweenCallback(Callable.From(message.QueueFree));
    }
    /// <summary>观察态中的自动应对只抽走对应卡牌，绝不重建整手牌，避免整排卡片抖动。</summary>
    public void confirm_auto_response(ActiveSkillData skill)
    {
        var card=current_cards.FirstOrDefault(c=>c.skill_data==skill);if(card is null)return;
        if(motions.Remove(card,out var motion))motion.Kill();
        card.cancel_drag();card.MouseFilter=MouseFilterEnum.Ignore;card.SetProcess(false);
        animate_discard(card);
        apply_observing_state();request_layout();
    }
    public void queue_card_for_play(ActiveSkillData skill,int order){var card=current_cards.FirstOrDefault(c=>c.skill_data==skill);if(card is null||queued_cards.Contains(card)||hand is null)return;current_cards.Remove(card);queued_cards.Add(card);card.MouseFilter=MouseFilterEnum.Ignore;card.SetProcess(false);card.ZIndex=1800;var center=GetViewportRect().Size*.5f-new Vector2(120,180)+new Vector2(order*14,-order*10);var target=hand.GetGlobalTransformWithCanvas().AffineInverse()*center;card.Rotation=0;var tween=card.CreateTween();tween.SetParallel();tween.TweenProperty(card,"position",target,.24).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);tween.TweenProperty(card,"scale",Vector2.One*.78f,.24).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);}
    private void release_queued_card(ActiveSkillData skill){var card=queued_cards.FirstOrDefault(c=>c.skill_data==skill);if(card is null)return;queued_cards.Remove(card);var tween=card.CreateTween();tween.SetParallel();tween.TweenProperty(card,"modulate:a",0f,.18);tween.TweenProperty(card,"scale",Vector2.One*.25f,.22);tween.Chain().TweenCallback(Callable.From(card.QueueFree));}
    public void cancel_queued_card(ActiveSkillData skill){var card=queued_cards.FirstOrDefault(c=>c.skill_data==skill);if(card is null)return;queued_cards.Remove(card);if(!current_cards.Contains(card))current_cards.Add(card);card.SetProcess(true);card.MouseFilter=MouseFilterEnum.Stop;card.Modulate=Colors.White;card.Scale=Vector2.One*CardScale;request_layout();}
    private void layout_cards(){layoutDirty=false;if(hand is null)return;var cards=current_cards.Where(IsInstanceValid).ToList();if(cards.Count==0)return;var width=hand.Size.X<=0?1500:hand.Size.X;var cardWidth=observing?154f:CardWidth;var step=Mathf.Min(cardWidth,width*.72f/Mathf.Max(1,cards.Count-1));var span=step*Mathf.Max(0,cards.Count-1);var start=width*.5f-span*.5f-CardWidth*.5f;var live=new HashSet<SkillCard>();for(var i=0;i<cards.Count;i++){var c=cards[i];live.Add(c);if(c==dragging&&c.is_dragging())continue;var middle=i-(cards.Count-1)*.5f;var pos=new Vector2(start+i*step,(observing?8:-20)+Mathf.Abs(middle)*(observing?3:6));var scale=Vector2.One*(observing?ObserveCardScale:CardScale);if(!observing&&c==hovered){pos.Y-=54;scale*=1.08f;c.ZIndex=200;}else c.ZIndex=i;handTargets[c]=(pos,scale,middle*(observing ? .7f : 1.15f));}foreach(var card in handTargets.Keys.Where(card=>!live.Contains(card)).ToArray())handTargets.Remove(card);}
    private void animate_hand_targets(float delta){var blendPosition=1f-Mathf.Exp(-delta*14f);var blendScale=1f-Mathf.Exp(-delta*16f);var blendRotation=1f-Mathf.Exp(-delta*18f);foreach(var entry in handTargets){var c=entry.Key;if(!IsInstanceValid(c)||c==dragging&&c.is_dragging())continue;var target=entry.Value;c.Position=c.Position.Lerp(target.position,blendPosition);c.Scale=c.Scale.Lerp(target.scale,blendScale);c.RotationDegrees=Mathf.Lerp(c.RotationDegrees,target.rotation,blendRotation);if(c.Position.DistanceTo(target.position)<1f)c.Position=target.position;if(c.Scale.DistanceTo(target.scale)<.002f)c.Scale=target.scale;if(Mathf.Abs(c.RotationDegrees-target.rotation)<.1f)c.RotationDegrees=target.rotation;}}
    public override void _Draw()
    {
        if(dragging?.is_target_previewing()!=true)return;
        var a=dragging.GetGlobalRect().GetCenter()-GlobalPosition;var b=GetLocalMousePosition();
        var manager=GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        var target=manager?.pick_card_target(dragging.skill_data!,GetViewport().GetMousePosition()) as Node2D;
        var color=target is null?new Color(1,.76f,.27f):new Color(.4f,1,.55f);
        var needsManualTarget=dragging.skill_data!.target_type is 2 or 3||dragging.skill_data.aoe_range>0||dragging.skill_data.can_target_empty;
        if(needsManualTarget){var control=new Vector2(a.X,Mathf.Min(a.Y,b.Y)-90);var previous=a;
        for(var i=1;i<=24;i++){var t=i/24f;var point=(1-t)*(1-t)*a+2*(1-t)*t*control+t*t*b;DrawLine(previous,point,color,4,true);previous=point;}
        var d=(b-control).Normalized();var side=new Vector2(-d.Y,d.X);DrawColoredPolygon(new Vector2[]{b,b-d*24+side*12,b-d*24-side*12},color);}
        // 拖拽期间实时画框，松手结算后这层自然停止绘制。
        var previewTargets=manager?.get_card_drag_preview_targets(dragging.skill_data!,GetViewport().GetMousePosition())??[];
        foreach(var value in previewTargets)
        {
            if(value is not Node2D unit)continue;
            var selected=unit==target;
            var frameColor=selected?new Color(.4f,1,.55f):new Color(1,.62f,.25f,.88f);
            var center=unit.GetGlobalTransformWithCanvas().Origin-GlobalPosition-new Vector2(0,130);
            BattleCornerFrame.Paint(this,new Rect2(center-new Vector2(78,150),new Vector2(156,300)),frameColor);
        }
        if(manager is not null)DrawDragCombatPreview(manager,dragging.skill_data!,previewTargets);
    }
    private void DrawDragCombatPreview(BattleManager manager,ActiveSkillData skill,Array<GodotObject> targets)
    {
        var dealsDamage=!Mathf.IsZeroApprox(skill.base_damage)||skill.damage_bonuses.Count>0;
        var debuffs=skill.status_effects.Select(effect=>
        {
            var id=effect.GetValueOrDefault("status_id","").AsString();
            var status=string.IsNullOrEmpty(id)?null:GD.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres");
            return (id,name:status?.name??id,chance:effect.GetValueOrDefault("hit_formula","1.0").AsString(),isDebuff:status?.is_debuff??false);
        }).Where(effect=>effect.isDebuff).ToArray();
        foreach(var target in targets)
        {
            var name=target.Get("entity_name").AsString();
            var lines=new List<(string text,Color color)> { (name,new Color(1f,.82f,.42f)) };
            if(dealsDamage)
            {
                var hit=skill.damage_type is "soul" or "spiritual"?"必中":$"命中 {BattleActionResolver.preview_hit_chance(manager.active_unit!,target)*100:0}%";
                var crit=skill.damage_type is "soul" or "spiritual"?"":$"  暴击 {BattleActionResolver.preview_crit_chance(manager.active_unit!,target)*100:0}%";
                lines.Add(($"{hit}{crit}",new Color(.93f,.92f,.84f)));
            }
            else lines.Add(("作用生效",new Color(.70f,.92f,.72f)));
            foreach(var effect in debuffs) lines.Add(($"  {effect.name} {BattleActionResolver.evaluate_formula(effect.chance,manager.active_unit!,target)*100:0}%",new Color(1f,.62f,.48f)));
            if(target is not Node2D unit)continue;
            var anchor=unit.GetGlobalTransformWithCanvas().Origin-GlobalPosition-new Vector2(0,130);
            var width=Mathf.Max(172,lines.Max(line=>ThemeDB.FallbackFont.GetStringSize(line.text,HorizontalAlignment.Left,-1,16).X+26));
            var height=12+lines.Count*22;
            // 朝屏幕外侧摆放，保留角色本体与角框的可视性。
            var onRight=anchor.X<GetViewportRect().Size.X*.5f;
            var position=new Vector2(onRight?anchor.X+88:anchor.X-width-88,anchor.Y-height*.5f);
            position.X=Mathf.Clamp(position.X,10,GetViewportRect().Size.X-width-10);
            position.Y=Mathf.Clamp(position.Y,10,GetViewportRect().Size.Y-height-10);
            var rect=new Rect2(position,new Vector2(width,height));
            DrawRect(rect,new Color(.025f,.035f,.03f,.91f),true);DrawRect(rect,new Color(.72f,.60f,.32f,.90f),false,1.5f,true);
            var font=ThemeDB.FallbackFont;for(var i=0;i<lines.Count;i++)DrawString(font,rect.Position+new Vector2(13,19+i*22),lines[i].text,HorizontalAlignment.Left,-1,16,lines[i].color);
        }
    }
    public bool is_point_in_hand_area(Vector2 p){if(hand is null)return false;var r=hand.GetGlobalRect();r.Position-=new Vector2(40,70);r.Size+=new Vector2(80,120);return r.HasPoint(p);} public SkillCard? get_dragging_card()=>current_cards.FirstOrDefault(c=>IsInstanceValid(c)&&c.is_dragging());
    private void on_card_drag_played(SkillCard card,Vector2 pos){dragging=card;QueueRedraw();if(is_point_in_hand_area(pos)){commit_dragging_card_reorder(pos,card);return;}EmitSignal(SignalName.card_release_attempted,card,pos);if(IsInstanceValid(card)&&current_cards.Contains(card))card.cancel_drag();dragging=null;request_layout();}
    private void on_card_selected(SkillCard card){hide_detail();reset_selection();card.set_selected(true);selected_index=card.card_index;if(card.skill_data is not null)EmitSignal(SignalName.card_selected,card.skill_data,card.card_index);} public void highlight_card_by_index(int i){reset_selection();var c=current_cards.FirstOrDefault(c=>c.card_index==i);if(c is not null){c.set_selected(true);selected_index=i;}} public void set_card_selected(ActiveSkillData skill){reset_selection();var c=current_cards.FirstOrDefault(c=>c.skill_data==skill);if(c is not null){c.set_selected(true);selected_index=c.card_index;}} private void reset_selection(){selected_index=-1;foreach(var c in current_cards)if(IsInstanceValid(c))c.set_selected(false);}
    private void show_detail(SkillCard source){if(detail is null||glossary is null||source.skill_data is null)return;resize_modals();foreach(var n in detail.GetChildren().Where(n=>n.Name=="Preview").ToArray())n.QueueFree();var scene=ResourceLoader.Load<PackedScene>("res://tscn/panel/BattleCard.tscn");if(scene?.Instantiate<SkillCard>() is not SkillCard card)return;card.Name="Preview";card.Position=new(92,58);card.Scale=new(1.35f,1.35f);card.MouseFilter=MouseFilterEnum.Ignore;detail.AddChild(card);card.setup(source.skill_data,source.owner_unit??this,source.card_index);card.MouseFilter=MouseFilterEnum.Ignore;card.SetProcess(false);card.Modulate=Colors.White;PlayerInfoText.Bind(glossary,CardPresentation.Details(source.skill_data,source.owner_unit));detail.Show();}

    private void hide_detail(SkillCard? _=null)=>detail?.Hide();
    private void close_browser(){if(browser is null||!browser.Visible)return;var tw=browser.CreateTween();tw.TweenProperty(browser,"modulate:a",0f,.16f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);tw.TweenCallback(Callable.From(()=>{if(browser is not null){browser.Hide();browser.Modulate=Colors.White;}}));}
    private void open_pile(string title,int pileType)
    {
        if(browser is null||pileScroll is null)return;
        hide_detail();cancel_dragging_card();resize_modals();
        var manager=GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        var pile=current_unit is null?null:manager?.get_deck_manager(current_unit);
        var cards=pile is null?[]:pileType switch{1=>pile.discard_pile.ToArray(),2=>pile.exhausted_pile.ToArray(),_=>pile.draw_pile.ToArray()};
        browser.GetNode<Label>("Title").Text=$"{title} · {cards.Length} 张";
        var grid=pileScroll.GetNode<Control>("PileGrid");
        foreach(var n in grid.GetChildren()){grid.RemoveChild(n);n.QueueFree();}
        // STS2 的牌堆查看使用较宽的固定卡槽；在 1920 宽度下约六列，避免缩成一排小卡。
        const float cellWidth=288f, cellHeight=380f, cardScale=0.96f;
        var columns=Mathf.Max(1,Mathf.FloorToInt((pileScroll.Size.X-48)/cellWidth));
        grid.CustomMinimumSize=new Vector2(columns*cellWidth,Mathf.Max(420,Mathf.CeilToInt(cards.Length/(float)columns)*cellHeight));
        var scene=GD.Load<PackedScene>("res://tscn/panel/BattleCard.tscn");
        for(var i=0;i<cards.Length;i++){
            var c=scene.Instantiate<SkillCard>();grid.AddChild(c);
            c.setup(cards[i],(GodotObject?)current_unit??this,i);
            var basePosition=new Vector2(30+i%columns*cellWidth,18+i/columns*cellHeight);c.Position=basePosition;c.Scale=Vector2.One*cardScale;
            c.MouseFilter=MouseFilterEnum.Stop;c.SetProcess(false);c.Modulate=Colors.White;c.card_selected+=show_detail;c.detail_requested+=show_detail;
            c.MouseEntered+=()=>{var tw=c.CreateTween();tw.SetParallel();tw.TweenProperty(c,"position",basePosition-new Vector2(0,18),.16).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);tw.TweenProperty(c,"scale",Vector2.One*(cardScale+0.08f),.16).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);c.ZIndex=100;};
            c.MouseExited+=()=>{var tw=c.CreateTween();tw.SetParallel();tw.TweenProperty(c,"position",basePosition,.18).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);tw.TweenProperty(c,"scale",Vector2.One*cardScale,.18).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);c.ZIndex=0;};
        }
        if(cards.Length==0)grid.AddChild(new Label{Text="暂无卡牌",Position=new(48,48)});
        pileScroll.ScrollVertical=0;browser.Modulate=new Color(1,1,1,0);browser.Show();var reveal=browser.CreateTween();reveal.TweenProperty(browser,"modulate:a",1f,.22f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
    private void update_resources(bool force=false){if(current_unit is not Creature u){if(previousResourceUnit is not null||force)set_resource_text(0,0,0,0,0,0);previousResourceUnit=null;previousEnergy=previousMana=previousSpirit=previousMaxStamina=previousMaxMana=previousMaxSpirit=-1;return;}var changed=force||previousResourceUnit!=u||previousEnergy!=u.current_stamina||previousMana!=u.current_mana||previousSpirit!=u.used_spirit_perception||previousMaxStamina!=u.max_stamina||previousMaxMana!=u.max_mana||previousMaxSpirit!=u.max_spirit_perception;if(!changed)return;set_resource_text(u.current_stamina,u.max_stamina,u.current_mana,u.max_mana,u.used_spirit_perception,u.max_spirit_perception);if(previousEnergy>=0&&u.current_stamina<previousEnergy&&energy is not null)energy.Modulate=new(.5f,1,.8f);previousResourceUnit=u;previousEnergy=u.current_stamina;previousMana=u.current_mana;previousSpirit=u.used_spirit_perception;previousMaxStamina=u.max_stamina;previousMaxMana=u.max_mana;previousMaxSpirit=u.max_spirit_perception;foreach(var c in current_cards)if(IsInstanceValid(c))c.update_resource_state();}
    private void set_resource_text(int e,int me,int m,int mm,int us,int ms){if(energy is not null)energy.Text=$"体力 {e}/{me}";if(mana is not null)mana.Text=$"法力 {m}/{mm}";if(stamina is not null)stamina.Hide();if(spirit is not null)spirit.Text=$"灵识 {us}/{ms}";}
    public void update_move_restrictions(bool r){foreach(var c in current_cards)if(c.is_move_card&&c.skill_data?.keywords.Contains("灵活")!=true)c.set_move_restricted(r);} public void update_momentum_states(int n){foreach(var c in current_cards)if(c.skill_data?.keywords.Contains("乘势")==true)c.set_momentum_active(MomentumConditionChecker.check(c.skill_data.momentum_condition_type,new Dictionary{{"last_skill_type",n}}));}
    public void update_deck_info(int d,int x,int e=0)=>set_deck_info(d,x,e); public void set_deck_info(int d,int x,int e=0){if(deckInfo is not null)deckInfo.Text=$"抽牌堆 {d} | 弃牌堆 {x} | 消耗 {e}";if(drawButton is not null)drawButton.Text=$"抽牌堆\n{d}";if(discardButton is not null)discardButton.Text=$"弃牌堆\n{x}";if(exhaustButton is not null)exhaustButton.Text=$"消耗\n{e}";}
    public void show_hand(){shown=true;observing=false;Show();apply_observing_state();update_resources();request_layout();}
    public void show_observing_hand(){shown=true;observing=true;Show();hide_detail();browser?.Hide();reset_selection();apply_observing_state();request_layout();}
    private void apply_observing_state(){foreach(var card in current_cards.Where(IsInstanceValid)){if(observing||card!=dragging)card.cancel_drag();card.SetProcess(!observing);card.MouseFilter=observing?MouseFilterEnum.Ignore:MouseFilterEnum.Stop;}if(drawButton is not null)drawButton.Visible=!observing;if(discardButton is not null)discardButton.Visible=!observing;if(exhaustButton is not null)exhaustButton.Visible=!observing;}
    public void hide_hand(){shown=false;observing=false;Hide();hide_detail();browser?.Hide();reset_selection();} public bool is_hand_visible()=>shown;
    public void remove_cards_by_skills(Array<ActiveSkillData> skills){var paths=new HashSet<string>(skills.Select(s=>s.ResourcePath));foreach(var c in current_cards.Where(c=>c.skill_data is not null&&paths.Contains(c.skill_data.ResourcePath)).ToArray())animate_discard(c);} public void animate_discard(SkillCard c){if(!IsInstanceValid(c))return;current_cards.Remove(c);if(c==dragging)dragging=null;if(c==hovered)hovered=null;var tw=c.CreateTween();tw.SetParallel();tw.TweenProperty(c,"modulate:a",0f,.2);tw.TweenProperty(c,"scale",new Vector2(.3f,.3f),.25);tw.Chain().TweenCallback(Callable.From(c.QueueFree));request_layout();} public void remove_cards_by_weapon_source(string path){foreach(var c in current_cards.Where(c=>c.skill_data?.GetMeta("weapon_source").AsString()==path).ToArray()){current_cards.Remove(c);c.QueueFree();}request_layout();}
    private void clear_hand(){foreach(var tween in motions.Values)tween.Kill();motions.Clear();handTargets.Clear();foreach(var card in queued_cards)if(IsInstanceValid(card))card.QueueFree();queued_cards.Clear();hide_detail();if(hand is not null)foreach(var n in hand.GetChildren())n.QueueFree();current_cards.Clear();hovered=null;dragging=null;selected_index=-1;previousResourceUnit=null;previousEnergy=previousMana=previousSpirit=previousMaxStamina=previousMaxMana=previousMaxSpirit=-1;} public void cancel_dragging_card(){dragging?.cancel_drag();dragging=null;request_layout();} public void commit_dragging_card_reorder(Vector2 p,SkillCard? explicit_card=null){var c=explicit_card??dragging;if(c is null)return;c.finish_drag_in_hand();dragging=null;sync_hand_order();request_layout();}
}
