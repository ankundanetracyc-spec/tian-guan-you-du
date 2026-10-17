using Godot;
using GArray = Godot.Collections.Array;

/// <summary>战前与胜败结算共用固定窗口；人员和战利品独立浏览，操作始终可见。</summary>
public partial class BattleFlowView : CanvasLayer
{
    private Control _canvas=null!, _body=null!;
    private Panel _frame=null!;
    private TextureRect _titleArt=null!;
    private ColorRect _shade=null!;
    private Label _heading=null!, _hint=null!;
    private HBoxContainer _buttons=null!;
    private BattleEntityData? _selected;
    public override void _Ready()
    {
        Layer=110;ProcessMode=ProcessModeEnum.Always;
        var shade=new ColorRect{Color=new Color(0,0,0,1),MouseFilter=Control.MouseFilterEnum.Stop};_shade=shade;AddChild(shade);shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _canvas=new Control{MouseFilter=Control.MouseFilterEnum.Stop};AddChild(_canvas);BattleUiArt.Fit(_canvas);
        var backdrop=new TextureRect{Size=BattleUiArt.Canvas,Texture=BattleUiArt.Texture("res://textures/battle/ink_mountain_valley_background_v1.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,Modulate=new Color(.8f,.8f,.8f),MouseFilter=Control.MouseFilterEnum.Ignore};_canvas.AddChild(backdrop);
        _frame=Panel(_canvas,new(116,126),new(1440,700));
        _frame.AddThemeStyleboxOverride("panel",BattleUiArt.Style("res://textures/ui/combat/flow_ui_20261009/flow_panel_v1.png",new(1440,700),48,48));
        _body=new Control{Position=new(24,24),Size=new(1392,676)};_frame.AddChild(_body);
        _titleArt=BattleUiArt.Icon("timeline_panel",new(620,80));_titleArt.Position=new(526,8);_canvas.AddChild(_titleArt);
        _heading=Text(_canvas,"",new(116,22),new(1440,64),36);_heading.HorizontalAlignment=HorizontalAlignment.Center;
        _buttons=new HBoxContainer{Position=new(256,850),Size=new(1160,48),Alignment=BoxContainer.AlignmentMode.Center};_buttons.AddThemeConstantOverride("separation",18);_canvas.AddChild(_buttons);
        _hint=Text(_canvas,"",new(116,908),new(1440,26),15);_hint.HorizontalAlignment=HorizontalAlignment.Center;
    }
    public async System.Threading.Tasks.Task FadeOutForBattle()
    {
        foreach(var button in _buttons.GetChildren().OfType<Button>())button.Disabled=true;
        var tween=CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        tween.TweenProperty(_canvas,"modulate",new Color(1,1,1,0),.18);
        tween.TweenProperty(_shade,"modulate",new Color(1,1,1,0),.18);
        await ToSignal(tween,Tween.SignalName.Finished);
    }
    public async System.Threading.Tasks.Task PresentSettlement()
    {
        _shade.Modulate=new Color(1,1,1,0);
        var elements=_canvas.GetChildren().OfType<CanvasItem>().ToList();
        foreach(var element in elements)element.Modulate=new Color(1,1,1,0);
        var tween=CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        tween.TweenProperty(_shade,"modulate",Colors.White,.30);
        foreach(var element in elements)
        {
            var delay=(element==_frame || element==_buttons || element==_hint) ? .18 : 0;
            tween.TweenProperty(element,"modulate",Colors.White,.40).SetDelay(delay);
        }
        await ToSignal(tween,Tween.SignalName.Finished);
    }
    public override void _Process(double delta)=>BattleUiArt.Fit(_canvas);
    public bool try_return_to_previous_page()
    {
        for(var index=_buttons.GetChildCount()-1;index>=0;index--)
            if(_buttons.GetChild(index) is Button back && back.Visible && !back.Disabled && back.Text=="返回")
            {
                back.EmitSignal(BaseButton.SignalName.Pressed);
                return true;
            }
        return false;
    }
    private static Panel Panel(Node parent,Vector2 position,Vector2 size)
    {
        var panel=new Panel{Position=position,Size=size,MouseFilter=Control.MouseFilterEnum.Pass};
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("1b3031f5"),BorderColor=BattleUiArt.Accent.Darkened(.2f),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8});parent.AddChild(panel);return panel;
    }
    private static void Divider(Node parent,Vector2 position,Vector2 size)
    {
        parent.AddChild(new ColorRect{Position=position,Size=size,Color=BattleUiArt.Accent.Darkened(.45f),MouseFilter=Control.MouseFilterEnum.Ignore});
    }
    private static Label Text(Node parent,string text,Vector2 position,Vector2 size,int font=18)
    {
        var label=new Label{Text=text,Position=position,Size=size,ClipText=true,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};
        label.AddThemeFontSizeOverride("font_size",font);label.AddThemeColorOverride("font_color",BattleUiArt.Text);parent.AddChild(label);return label;
    }
    private static Button Button(Node parent,string text,Action action)
    {
        var button=new Button{Text=text,CustomMinimumSize=new(190,46)};BattleUiArt.Skin(button);button.Pressed+=action;parent.AddChild(button);return button;
    }
    private void Clear(string title)
    {
        BattleUiArt.ClearChildren(_body);BattleUiArt.ClearChildren(_buttons);_heading.Text=title;_hint.Text="";
        _frame.Position=new(116,126);_frame.Size=new(1440,700);_body.Position=new(24,24);
        _heading.Position=new(116,22);_heading.Size=new(1440,64);_heading.AddThemeFontSizeOverride("font_size",36);_titleArt.Show();
        _heading.AddThemeColorOverride("font_color",title.Contains("败")?new Color("d6a08e"):BattleUiArt.Text);
    }
    private static ScrollContainer Scroll(Node parent,Vector2 position,Vector2 size)
    {
        var scroll=new ScrollContainer{Position=position,Size=size,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,VerticalScrollMode=ScrollContainer.ScrollMode.Auto,ClipContents=true};parent.AddChild(scroll);return scroll;
    }
    private static void Portrait(Node parent,BattleEntityData unit,Vector2 position,float size)
    {
        var frame=Panel(parent,position,new(size,size));frame.ClipContents=true;
        var portrait=new BattlePortraitView{Position=new(3,3),Size=new(size-6,size-6)};frame.AddChild(portrait);portrait.Setup(unit);
    }
    private static void Health(Node parent,BattleEntityData unit,Vector2 position,Vector2 size)
    {
        // 结算静态细条不使用主题 ProgressBar 的最小高度，避免压到下一行文字。
        var bar=new Control{Position=position,Size=size,ClipContents=true,MouseFilter=Control.MouseFilterEnum.Ignore};parent.AddChild(bar);
        bar.AddChild(new ColorRect{Size=size,Color=BattleUiArt.Accent.Darkened(.35f),MouseFilter=Control.MouseFilterEnum.Ignore});
        bar.AddChild(new ColorRect{Position=Vector2.One,Size=size-Vector2.One*2,Color=new Color("102322"),MouseFilter=Control.MouseFilterEnum.Ignore});
        var fraction=Mathf.Clamp((float)unit.current_health/Math.Max(1,unit.max_health),0,1);
        bar.AddChild(new ColorRect{Position=Vector2.One*2,Size=new((size.X-4)*fraction,Math.Max(1,size.Y-4)),Color=unit.faction==1?new Color("63966c"):new Color("ab6758"),MouseFilter=Control.MouseFilterEnum.Ignore});
    }
    public void Briefing(string title,List<BattleEntityData> units,bool training,string retreatDescription,Action begin,Action? cancel=null,System.Collections.Generic.Dictionary<string,int>? capacities=null)
    {
        Clear(title+" · 战前确认");
        _frame.Position=new(116,78);_frame.Size=new(1440,776);_body.Position=new(24,62);
        _titleArt.Hide();_heading.Position=new(116,86);_heading.AddThemeFontSizeOverride("font_size",30);
        Divider(_body,new(696,8),new(1,550));
        _selected=units.FirstOrDefault(u=>u.uid==_selected?.uid)??units.FirstOrDefault(u=>u.faction==1)??units.FirstOrDefault();
        for(var side=0;side<2;side++)
        {
            var faction=side==0?1:2;var members=units.Where(u=>u.faction==faction).ToList();
            var column=new Control{Position=new(side*708,0),Size=new(684,560)};_body.AddChild(column);
            Text(column,$"{(side==0?"我方":"敌方")}  {members.Count}人",new(12,0),new(660,38),23);
            var scroll=Scroll(column,new(0,46),new(684,512));
            var list=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};list.AddThemeConstantOverride("separation",8);scroll.AddChild(list);
            foreach(var unit in members)
            {
                var row=Panel(list,Vector2.Zero,new(660,94));row.CustomMinimumSize=new(660,94);
                Portrait(row,unit,new(10,4),86);
                var name=Text(row,$"{unit.entity_name} · {unit.cultivation_realm}",new(112,5),new(522,28),20);name.TooltipText=unit.entity_name;
                Text(row,"生命",new(112,29),new(48,26),17);
                Health(row,unit,new(168,38),new(286,14));
                Text(row,$"{unit.current_health}/{unit.max_health}",new(466,29),new(168,28),17);
                var capacity=capacities?.GetValueOrDefault(unit.uid)??5;
                Text(row,$"速度 {unit.initiative_speed:0.#}    遁速 {(unit.dun_speed>0?unit.dun_speed.ToString("0.#"):"—")}    行动位 {capacity}",new(112,57),new(522,26),17);
                var hit=new Button{Flat=true,Position=Vector2.Zero,Size=new(660,94),TooltipText=unit.entity_name+" · 点击选择查看资料",FocusMode=Control.FocusModeEnum.None};
                foreach(var state in new[]{"normal","hover","pressed","focus"})hit.AddThemeStyleboxOverride(state,new StyleBoxFlat{BgColor=state=="hover"?new Color(1,1,1,.04f):Colors.Transparent});
                row.AddChild(hit);hit.Pressed+=()=>{_selected=unit;_hint.Text=$"当前查看：{unit.entity_name}";};
            }
        }
        var summary=Panel(_body,new(0,578),new(1392,76));
        Text(summary,"目标：击败敌方存活单位",new(16,8),new(430,28),19);
        Text(summary,retreatDescription,new(464,8),new(906,28),18);
        Text(summary,training?"演练 · 不改变正式角色与行囊":"各角色独立规划行动队列",new(16,40),new(1356,26),16);
        Button(_buttons,"开始战斗",begin).CustomMinimumSize=new(274,60);
        if(_selected is not null)Button(_buttons,"查看属性",()=>Attributes(_selected!,()=>Briefing(title,units,training,retreatDescription,begin,cancel,capacities)));
        if(cancel is not null)Button(_buttons,"返回",cancel);
    }
    private void Attributes(BattleEntityData unit,Action back)
    {
        Clear(unit.entity_name+" · 属性");Portrait(_body,unit,new(20,20),100);
        var scroll=Scroll(_body,new(156,12),new(1200,632));var list=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};list.AddThemeConstantOverride("separation",24);scroll.AddChild(list);
        void Paragraph(string value)
        {
            var label=new Label{Text=value,AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};label.AddThemeFontSizeOverride("font_size",21);label.AddThemeColorOverride("font_color",BattleUiArt.Text);list.AddChild(label);
        }
        Paragraph($"基础属性\n体魄 {unit.physique:0.#}　灵慧 {unit.intelligence:0.#}　敏捷 {unit.agility:0.#}　神魂 {unit.soul:0.#}");
        if(unit.entity_type==(int)WorldEntity.EntityType.CHARACTER)Paragraph($"福缘 {unit.fortune}　悟性 {unit.comprehension}　魅力 {unit.charisma:0.#}　交际 {unit.social:0.#}　治事 {unit.management:0.#}");
        Paragraph($"战斗属性\n生命 {unit.current_health}/{unit.max_health}　体力 {unit.current_stamina}/{unit.max_stamina}　速度 {unit.initiative_speed:0.#}\n物理防御 {unit.physical_defense}　物理减伤 {unit.physical_damage_reduction*100:0.#}%\n精准 {unit.precision}　身法 {unit.dodge}　会心 {unit.critical}　护心 {unit.crit_resist}　会心伤害 {unit.crit_damage_mult*100:0.#}%");
        if(unit.entity_type==(int)WorldEntity.EntityType.CHARACTER || unit.max_mana>0)Paragraph($"法力 {unit.current_mana}/{unit.max_mana}　灵识 {unit.used_spirit_perception}/{unit.max_spirit_perception}");
        Paragraph($"遁速 {(unit.dun_speed>0?unit.dun_speed.ToString("0.#"):"—")}\n速度影响行动积蓄；遁速影响行旅与撤退概率。");Button(_buttons,"返回",back);
    }
    public void Results(string title,List<BattleEntityData> before,List<BattleEntityData> after,GArray loot,bool training,Action confirm,string note="",Action? later=null)
    {
        Clear(title+" · 战后结算");
        Text(_body,"参战状况",new(8,0),new(460,38),23);
        Divider(_body,new(492,0),new(1,648));
        var tabs=new HBoxContainer{Position=new(0,46),Size=new(468,42)};tabs.AddThemeConstantOverride("separation",10);_body.AddChild(tabs);
        var roster=Scroll(_body,new(0,104),new(468,552));
        void ShowTeam(int faction)
        {
            BattleUiArt.ClearChildren(roster);roster.ScrollVertical=0;
            for(int index=0;index<tabs.GetChildCount();index++)tabs.GetChild<Button>(index).Modulate=index==faction-1?Colors.White:new Color(.6f,.65f,.63f);
            var list=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};list.AddThemeConstantOverride("separation",10);roster.AddChild(list);
            foreach(var unit in after.Where(u=>u.faction==faction))
            {
                var row=Panel(list,Vector2.Zero,new(450,102));row.CustomMinimumSize=new(450,102);Portrait(row,unit,new(8,8),86);
                var original=before.FirstOrDefault(u=>u.uid==unit.uid);
                var name=Text(row,unit.entity_name,new(106,4),new(330,26),19);row.TooltipText=unit.entity_name;
                Text(row,$"{original?.current_health??unit.current_health} → {unit.current_health}/{unit.max_health}",new(106,31),new(330,24),16);
                Health(row,unit,new(106,59),new(326,8));
                var status=unit.current_health<=0?"无法继续行动":$"外伤 {unit.battle_external_wounds} · 内伤 {unit.battle_internal_wounds}";
                var text=Text(row,status,new(106,73),new(330,22),15);if(unit.current_health<=0)text.AddThemeColorOverride("font_color",BattleUiArt.Warning);
            }
        }
        Button(tabs,$"我方 {after.Count(u=>u.faction==1)}",()=>ShowTeam(1));Button(tabs,$"敌方 {after.Count(u=>u.faction==2)}",()=>ShowTeam(2));ShowTeam(1);
        // 展示副本只合并同物品，不改动原始奖励账本或领取数量。
        var entries=loot.Select(v=>v.AsGodotDictionary()).Where(d=>d.GetValueOrDefault("count",0).AsInt32()>0)
            .GroupBy(d=>d["item_path"].AsString()).Select(group=>new LootDisplay(group.Key,group.Sum(d=>d["count"].AsInt32()),string.Join("\n",group.Select(d=>$"{d.GetValueOrDefault("source","").AsString()} ×{d["count"].AsInt32()}")))).ToList();
        Text(_body,entries.Count>0?"战利品":"战后结算",new(510,0),new(600,38),23);
        var count=Text(_body,entries.Count>0?$"共 {entries.Count} 种":"",new(1180,0),new(208,38),18);count.HorizontalAlignment=HorizontalAlignment.Right;
        if(entries.Count>0)
        {
            var scroll=Scroll(_body,new(510,50),new(882,574));
            var grid=new GridContainer{Columns=6,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};grid.AddThemeConstantOverride("h_separation",10);grid.AddThemeConstantOverride("v_separation",12);scroll.AddChild(grid);
            foreach(var entry in entries)
            {
                var tile=Panel(grid,Vector2.Zero,new(136,158));tile.CustomMinimumSize=new(136,158);tile.ClipContents=true;
                var item=ResourceLoader.Exists(entry.Path)?GD.Load<ItemData>(entry.Path):null;
                if(item is not null && !string.IsNullOrEmpty(item.icon_path) && ResourceLoader.Exists(item.icon_path))
                {
                    var icon=new TextureRect{Position=new(8,8),Size=new(120,110),Texture=GD.Load<Texture2D>(item.icon_path),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};tile.AddChild(icon);
                }
                var name=Text(tile,$"{item?.entity_name??"物品"} ×{entry.Count}",new(4,124),new(128,26),16);name.HorizontalAlignment=HorizontalAlignment.Center;
                var fullName=item?.entity_name??"物品";var shortened=fullName;var suffix=$" ×{entry.Count}";
                var font=name.GetThemeFont("font");
                while(shortened.Length>0 && font.GetStringSize(shortened+(shortened==fullName?"":"…")+suffix,HorizontalAlignment.Left,-1,16).X>128)shortened=shortened[..^1];
                name.Text=shortened+(shortened==fullName?"":"…")+suffix;
                tile.TooltipText=$"{item?.entity_name??"物品"} ×{entry.Count}\n{item?.get_grade_string()??""}\n来源：\n{entry.Sources}";
                var hit=new Button{Flat=true,Size=new(136,158),FocusMode=Control.FocusModeEnum.None,TooltipText=tile.TooltipText};
                foreach(var state in new[]{"normal","hover","pressed","focus"})hit.AddThemeStyleboxOverride(state,new StyleBoxEmpty());tile.AddChild(hit);
                hit.Pressed+=()=>ShowDetail(item,entry);
            }
            _hint.Text=training?"演练不发放正式战利品":"滚动查看全部战利品 · 行囊放不下的物品将保留待领";
        }
        else
        {
            Text(_body,training?"本次为演练，不发放正式战利品。":title.Contains("败")?"本场未获得胜利战利品。":"本次没有可领取的战利品。",new(534,74),new(834,44),22);
            if(title.Contains("败")&&!training)Text(_body,"返回后按本次遭遇结果处理恢复与损失。",new(534,132),new(834,40),19);
        }
        var details=Button(_buttons,"查看详情",()=>ShowNote(note));
        // 主操作固定在底部首位，滚动不会移动或遮挡按钮。
        var primary=Button(_buttons,entries.Count>0&&!training?"收取并返回":"返回",confirm);primary.CustomMinimumSize=new(274,60);_buttons.MoveChild(primary,0);
        if(later is not null)Button(_buttons,"稍后收取",later);
    }
    private sealed record LootDisplay(string Path,int Count,string Sources);
    private void ShowNote(string note)=>Detail("战斗详情",string.IsNullOrWhiteSpace(note)?"暂无额外记录。":note);
    private void ShowDetail(ItemData? item,LootDisplay entry)=>Detail(item?.entity_name??"物品",$"数量：{entry.Count}\n品阶：{item?.get_grade_string()??"—"}\n类别：{item?.item_category??"—"}\n来源：\n{entry.Sources}");
    private void Detail(string title,string content)
    {
        var cover=new ColorRect{Size=BattleUiArt.Canvas,Color=new Color(0,0,0,.72f),MouseFilter=Control.MouseFilterEnum.Stop};_canvas.AddChild(cover);
        var panel=Panel(cover,new(436,210),new(800,520));Text(panel,title,new(24,16),new(752,40),25);
        var scroll=Scroll(panel,new(24,72),new(752,350));
        var label=new Label{Text=content,AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};label.AddThemeFontSizeOverride("font_size",20);label.AddThemeColorOverride("font_color",BattleUiArt.Text);scroll.AddChild(label);
        var close=Button(panel,"关闭",()=>cover.QueueFree());close.Position=new(305,452);close.Size=new(190,46);
    }
}
