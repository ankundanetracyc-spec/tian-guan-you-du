using Godot;

/// <summary>妖兽实体的随机初始属性与境界倍率。</summary>
public partial class Beast : Creature
{
    [Export] public BeastTemplate? beast_template;
    [Export] public string species_id = "";
    [Export] public BeastRacialTalent? racial_talent;
    [Export] public BeastModifier? beast_modifier;
    [Export] public bool is_elite;
    /// <summary>妖兽使用独立兽种牌组。</summary>
    [Export] public BasicDeckResource? beast_deck;
    [Export(PropertyHint.Range,"1,8,1")] public int beast_initial_hand_size = 5;
    [Export(PropertyHint.Range,"1,8,1")] public int beast_cards_per_turn = 3;
    public override void _Ready(){entity_data ??= new LivingData();base._Ready();entity_type=EntityType.BEAST;apply_template();if(beast_modifier?.deck_override is not null)beast_deck=beast_modifier.deck_override;if(beast_modifier?.racial_talent_override is not null)racial_talent=beast_modifier.racial_talent_override;if(data?.data_initialized==true){update_ui();return;}generate_attributes();apply_modifiers();recalculate_beast();if(data is not null)data.data_initialized=true;update_ui();}
    private void apply_template(){if(beast_template is null)return;species_id=beast_template.species_id;cultivation_realm=beast_template.cultivation_realm;if(entity_data is not null){if(!string.IsNullOrWhiteSpace(beast_template.battle_portrait_path))entity_data.battle_portrait_path=beast_template.battle_portrait_path;}racial_talent=beast_template.racial_talent;beast_deck=beast_template.deck;beast_initial_hand_size=beast_template.initial_hand_size;beast_cards_per_turn=beast_template.cards_per_turn;if(entity_name=="未命名")entity_name=beast_template.display_name;}
    public CombatBoardLayout? get_fixed_combat_board_layout()
    {
        if (beast_modifier?.combat_board_layout_override is { } modifierLayout) return modifierLayout;
        if (beast_template?.combat_board_layout is { } templateLayout) return templateLayout;
        var path = species_id switch
        {
            "forest_wolf" => "res://resources/combat_boards/生灵/林地凶狼固定盘.tres",
            "wild_boar" => "res://resources/combat_boards/生灵/灰背野猪固定盘.tres",
            "forest_bei" => "res://resources/combat_boards/生灵/林地妖狈固定盘.tres",
            _ => ""
        };
        return string.IsNullOrEmpty(path) ? null : ResourceLoader.Load<CombatBoardLayout>(path);
    }
    private static float round_one(float value)=>Mathf.Round(value*10f)/10f;
    private static float ceil_one(float value)=>Mathf.Ceil(value*10f)/10f;
    private static float normal(float mean,float deviation,float minimum,float maximum)
    {
        var u1=Mathf.Max(.0001f,(float)GD.Randf());var u2=(float)GD.Randf();
        return round_one(Mathf.Clamp(mean+deviation*Mathf.Sqrt(-2f*Mathf.Log(u1))*Mathf.Cos(Mathf.Tau*u2),minimum,maximum));
    }
    private void generate_attributes(){var template=beast_template;base_physique=physique=normal(template?.physique_mean??11,(template?.physique_max-template?.physique_min??2)/4f,template?.physique_min??10,template?.physique_max??12);base_intelligence=intelligence=normal(template?.intelligence_mean??2,(template?.intelligence_max-template?.intelligence_min??2)/4f,template?.intelligence_min??1,template?.intelligence_max??3);base_soul=soul=normal(template?.soul_mean??2,(template?.soul_max-template?.soul_min??2)/4f,template?.soul_min??1,template?.soul_max??3);fortune=round_one(GD.RandRange(1,10));comprehension=round_one(GD.RandRange(1,10));initial_hand_size=beast_initial_hand_size;cards_per_turn=beast_cards_per_turn;}
    /// <summary>固定顺序：正态随机基础四维 → 变种百分比 → 精英 +25%（均向上取至一位小数）。</summary>
    private void apply_modifiers()
    {
        if(beast_modifier is not null){base_physique=physique=round_one(base_physique*beast_modifier.physique_multiplier);base_intelligence=intelligence=round_one(base_intelligence*beast_modifier.intelligence_multiplier);base_soul=soul=round_one(base_soul*beast_modifier.soul_multiplier);}
        if(!is_elite)return;
        base_physique=physique=ceil_one(base_physique*1.25f);base_intelligence=intelligence=ceil_one(base_intelligence*1.25f);base_soul=soul=ceil_one(base_soul*1.25f);
    }
    /// <summary>妖兽体魄没有“基础/最终”两层：两者均取当前体魄。兽种差异只由种族天赋在此基础上修正。</summary>
    public void recalculate_beast(){max_health=Mathf.RoundToInt(60+physique*11+soul*3+intelligence*1.5f);max_mind=Mathf.RoundToInt(30+soul*4+intelligence*1.5f+physique*.5f);physical_defense=0;precision=Mathf.RoundToInt(intelligence*7+soul+physique*.5f);dodge=Mathf.RoundToInt(coordination*4+soul*2+intelligence+physique*.5f);critical=Mathf.RoundToInt(physique*.5f+soul*2f+intelligence*.3f);crit_resist=Mathf.RoundToInt(physique*3+intelligence*2+coordination*2+soul*1.5f);crit_damage_mult=1.5f;initiative_speed=get_base_initiative_speed();spirit_strands=(int)(soul*18+comprehension*8)*spirit_multiplier();max_mana=cultivation_realm=="凡兽"?0:(int)(spirit_strands*.06f+intelligence*.8f+soul*.5f);racial_talent?.apply_derived_attributes(this);current_health=max_health;current_mind=max_mind;current_mana=max_mana;}
    private int spirit_multiplier()=>cultivation_realm switch{"通智"=>1,"妖府"=>2,"内丹"=>3,"蜕形"=>5,"天妖"=>8,_=>0};
    public float get_species_physical_damage_multiplier()=>species_id=="forest_wolf"?get_status_stacks("逐猎")*.10f:0f;
    public float get_species_action_speed_multiplier()=>species_id=="forest_wolf"?1f+get_status_stacks("逐猎")*.05f:1f;
    public override void apply_battle_data(BattleEntityData snapshot){base.apply_battle_data(snapshot);physical_defense=snapshot.physical_defense;precision=snapshot.precision;dodge=snapshot.dodge;critical=snapshot.critical;crit_resist=snapshot.crit_resist;crit_damage_mult=snapshot.crit_damage_mult;initiative_speed=snapshot.initiative_speed;cards_per_turn=snapshot.cards_per_turn;initial_hand_size=snapshot.initial_hand_size;}
    public override BattleEntityData get_snapshot_data(){var snapshot=base.get_snapshot_data();snapshot.dun_speed=beast_template?.dun_speed??0; if(beast_template is not null)snapshot.battle_loot=beast_template.battle_loot.Duplicate(true);snapshot.combat_board_layout=get_fixed_combat_board_layout()?.CopyConfiguration();snapshot.active_skill_paths=[];if(beast_deck is not null)foreach(var path in beast_deck.get_deck_skills().Distinct(StringComparer.Ordinal))snapshot.active_skill_paths.Add(path);snapshot.physical_defense=physical_defense;snapshot.precision=precision;snapshot.dodge=dodge;snapshot.critical=critical;snapshot.crit_resist=crit_resist;snapshot.crit_damage_mult=crit_damage_mult;snapshot.initiative_speed=initiative_speed;snapshot.cards_per_turn=cards_per_turn;snapshot.initial_hand_size=initial_hand_size;return snapshot;}
    public string get_spirit_root_name()=>"无灵根（妖兽）"; public override string get_detail_text()=>base.get_detail_text()+"类型：妖兽\n"+(racial_talent is null?"":$"种族天赋：{racial_talent.talent_name}\n{racial_talent.description}\n");
}
