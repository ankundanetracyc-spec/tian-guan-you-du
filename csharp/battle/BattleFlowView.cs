using Godot;
using GArray = Godot.Collections.Array;

/// <summary>战前与战后共用的界面；挂在切换服务上，不被打包进世界场景。</summary>
public partial class BattleFlowView : CanvasLayer
{
    private Control _canvas=null!;
    private VBoxContainer _body=null!;
    private HBoxContainer _buttons=null!;
    public override void _Ready()
    {
        Layer=110;ProcessMode=ProcessModeEnum.Always;
        _canvas=new Control{MouseFilter=Control.MouseFilterEnum.Stop};AddChild(_canvas);
        var shade=new ColorRect{Size=BattleUiArt.Canvas,Color=new Color(.025f,.045f,.04f,.92f),MouseFilter=Control.MouseFilterEnum.Stop};_canvas.AddChild(shade);
        var frame=new PanelContainer{Position=new(286,80),Size=new(1100,780),MouseFilter=Control.MouseFilterEnum.Stop};
        frame.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("1b3031"),BorderColor=BattleUiArt.Accent,BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=16,CornerRadiusTopRight=16,CornerRadiusBottomLeft=16,CornerRadiusBottomRight=16,ContentMarginLeft=32,ContentMarginRight=32,ContentMarginTop=24,ContentMarginBottom=24});_canvas.AddChild(frame);
        var layout=new VBoxContainer();layout.AddThemeConstantOverride("separation",16);frame.AddChild(layout);
        var scroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};layout.AddChild(scroll);
        _body=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};_body.AddThemeConstantOverride("separation",14);scroll.AddChild(_body);
        _buttons=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center};_buttons.AddThemeConstantOverride("separation",16);layout.AddChild(_buttons);
        BattleUiArt.Fit(_canvas);
    }
    public override void _Process(double delta)=>BattleUiArt.Fit(_canvas);
    private Label Text(string text,int size=19)
    {
        var label=new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",BattleUiArt.Text);return label;
    }
    private void Clear(string title)
    {
        BattleUiArt.ClearChildren(_body);BattleUiArt.ClearChildren(_buttons);
        var heading=Text(title,30);heading.HorizontalAlignment=HorizontalAlignment.Center;_body.AddChild(heading);
    }
    private void Button(string title,Action action)
    {
        var button=new Button{Text=title,CustomMinimumSize=new(190,44)};BattleUiArt.Skin(button);button.Pressed+=action;_buttons.AddChild(button);
    }
    public void Briefing(string title,List<BattleEntityData> units,bool training,string retreatDescription,Action begin,Action? cancel=null,System.Collections.Generic.Dictionary<string,int>? capacities=null)
    {
        Clear(title+" · 战前确认");
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",24);_body.AddChild(row);
        foreach(var faction in new[]{(int)WorldEntity.Faction.PLAYER,(int)WorldEntity.Faction.ENEMY})
        {
            var column=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(480,0)};column.AddThemeConstantOverride("separation",10);row.AddChild(column);
            column.AddChild(Text(faction==(int)WorldEntity.Faction.PLAYER?"我方":"敌方",23));
            foreach(var unit in units.Where(u=>u.faction==faction))
            {
                var path=string.IsNullOrEmpty(unit.battle_portrait_path)?BattlePaperDoll.DefaultPortraitPath:unit.battle_portrait_path;
                if(ResourceLoader.Exists(path))column.AddChild(new TextureRect{ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Texture=GD.Load<Texture2D>(path),CustomMinimumSize=new(160,160)});
                column.AddChild(Text($"{unit.entity_name} · {unit.cultivation_realm}\n生命 {unit.current_health}/{unit.max_health}　速度 {unit.initiative_speed:0.#}\n精力 {unit.current_stamina}/{unit.max_stamina}　法力 {unit.current_mana}/{unit.max_mana}\n每轮行动位 {capacities?.GetValueOrDefault(unit.uid)??5}"));
            }
        }
        _body.AddChild(Text("目标：击败敌方存活单位\n"+retreatDescription));
        if(training)_body.AddChild(Text("演练：不改变正式角色与背包。"));
        Button("开始战斗",begin);
        var player=units.FirstOrDefault(u=>u.faction==(int)WorldEntity.Faction.PLAYER);
        if(player is not null)Button("查看属性",()=>Attributes(player,()=>Briefing(title,units,training,retreatDescription,begin,cancel,capacities)));
        if(cancel is not null)Button("返回",cancel);
    }
    private void Attributes(BattleEntityData unit,Action back)
    {
        Clear(unit.entity_name+" · 属性");
        _body.AddChild(Text($"基础属性\n体魄 {unit.physique:0.#}　灵慧 {unit.intelligence:0.#}　敏捷 {unit.agility:0.#}\n神魂 {unit.soul:0.#}　福缘 {unit.fortune}　悟性 {unit.comprehension}\n魅力 {unit.charisma:0.#}　交际 {unit.social:0.#}　治事 {unit.management:0.#}"));
        _body.AddChild(Text($"战斗属性\n生命 {unit.current_health}/{unit.max_health}　精力 {unit.current_stamina}/{unit.max_stamina}　法力 {unit.current_mana}/{unit.max_mana}\n灵识 {unit.used_spirit_perception}/{unit.max_spirit_perception}　速度 {unit.initiative_speed:0.#}\n物理防御 {unit.physical_defense}　物理减伤 {unit.physical_damage_reduction*100:0.#}%\n精准 {unit.precision}　身法 {unit.dodge}　会心 {unit.critical}　护心 {unit.crit_resist}\n会心伤害 {unit.crit_damage_mult*100:0.#}%"));
        _body.AddChild(Text($"行旅属性\n遁速 {(unit.dun_speed>0?unit.dun_speed.ToString("0.#"):"未配置")}\n速度影响行动积蓄；遁速影响行旅与撤退概率。"));
        Button("返回",back);
    }
    public void Results(string title,List<BattleEntityData> before,List<BattleEntityData> after,GArray loot,bool training,Action confirm,string note="",Action? later=null)
    {
        Clear(title+" · 战后结算");
        foreach(var unit in after.Where(u=>u.faction==(int)WorldEntity.Faction.PLAYER))
        {
            var original=before.FirstOrDefault(u=>u.uid==unit.uid);
            _body.AddChild(Text($"{unit.entity_name}　生命 {original?.current_health??unit.current_health} → {unit.current_health}/{unit.max_health}　{(unit.current_health>0?"存活":"无法继续行动")}"));
        }
        _body.AddChild(Text("战利品",23));
        if(loot.Count==0)_body.AddChild(Text(training?"本次为演练，不发放正式战利品。":"本次没有可领取的配置奖励。"));
        foreach(var value in loot)
        {
            var entry=value.AsGodotDictionary();var path=entry["item_path"].AsString();var item=GD.Load<ItemData>(path);
            var row=new HBoxContainer();row.AddThemeConstantOverride("separation",14);_body.AddChild(row);
            if(item is not null&&!string.IsNullOrEmpty(item.icon_path)&&ResourceLoader.Exists(item.icon_path))row.AddChild(new TextureRect{ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Texture=GD.Load<Texture2D>(item.icon_path),CustomMinimumSize=new(48,48)});
            row.AddChild(Text($"{item?.entity_name??"物品"} ×{entry["count"].AsInt32()}　来源：{entry.GetValueOrDefault("source","").AsString()}"));
        }
        if(note!="")_body.AddChild(Text(note));
        Button(loot.Count>0&&!training?"收取并返回":"返回",confirm);
        if(later is not null)Button("稍后收取",later);
    }
}
