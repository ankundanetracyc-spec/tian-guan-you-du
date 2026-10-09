using Godot;
using GArray=Godot.Collections.Array;
using GDictionary=Godot.Collections.Dictionary;

public partial class BattleTransitionManager
{
    private BattleFlowView? _flowView;
    private bool _flowPreviousPause, _awaitingSettlement;
    private List<BattleEntityData> _beforeBattle=[];
    private List<string> _attackerOrder=[], _defenderOrder=[];
    private string _battleId="";
    private GDictionary _rewardLedger=[];
    public GDictionary export_reward_ledger()=>_rewardLedger.Duplicate(true);
    public void restore_reward_ledger(GDictionary ledger)=>_rewardLedger=ledger.Duplicate(true);
    public void clear_reward_ledger(){CloseFlow();_rewardLedger.Clear();_lootReminder?.QueueFree();_lootReminder=null;}
    private string _outcomeNote="";
    public void set_battle_outcome_note(string note)=>_outcomeNote=note;
    private bool Training=>return_context.GetValueOrDefault("is_training",false).AsBool() || return_context.GetValueOrDefault("kind","").AsString()=="yuxu_tianwen_practice";
    private static List<BattleEntityData> Data(GArray values)=>values.Select(v=>v.AsGodotObject()).OfType<BattleEntityData>().ToList();
    private BattleFlowView OpenFlow()
    {
        if(GodotObject.IsInstanceValid(_flowView))return _flowView!;
        _flowPreviousPause=GetTree().Paused;GetTree().Paused=true;
        _flowView=new BattleFlowView();AddChild(_flowView);return _flowView;
    }
    private void CloseFlow()
    {
        if(GodotObject.IsInstanceValid(_flowView)){_flowView!.QueueFree();GetTree().Paused=_flowPreviousPause;}
        _flowView=null;
    }
    private void ShowBattleBriefing()
    {
        if(_awaitingSettlement || GodotObject.IsInstanceValid(_flowView))return;
        if(!return_context.ContainsKey("retreat_self_coefficient"))return_context["retreat_self_coefficient"]=1f;
        if(!return_context.ContainsKey("retreat_enemy_coefficient"))return_context["retreat_enemy_coefficient"]=1.5f;
        _beforeBattle=Data(pending_attacker_data).Concat(Data(pending_defender_data)).Select(d=>(BattleEntityData)d.Duplicate(true)).ToList();
        _attackerOrder=Data(pending_attacker_data).Select(d=>d.uid).ToList();_defenderOrder=Data(pending_defender_data).Select(d=>d.uid).ToList();
        _battleId=Guid.NewGuid().ToString("N");_outcomeNote="";
        Action? cancel=return_context.GetValueOrDefault("allow_prebattle_cancel",false).AsBool()?()=>{CloseFlow();pending_attackers=[];pending_defenders=[];pending_attacker_data=[];pending_defender_data=[];return_context.Clear();}:null;
        var capacities=new System.Collections.Generic.Dictionary<string,int>();
        var preview=GD.Load<PackedScene>("res://tscn/battle/BattleScene.tscn").Instantiate();
        var config=preview.GetNode<BattleManager>("BattleMgr");
        foreach(var unit in _beforeBattle)capacities[unit.uid]=config.get_action_slot_capacity_for_realm(unit.cultivation_realm);
        preview.Free();
        OpenFlow().Briefing(return_context.GetValueOrDefault("title","遭遇战").AsString(),_beforeBattle,Training,RetreatDescription(),BeginConfirmedBattle,cancel,capacities);
    }
    private void BeginConfirmedBattle()
    {
        CloseFlow();saved_entity_data.Clear();
        var world=GetTree().CurrentScene;
        if(world is not null){save_entity_data_recursive(world);saved_world=new PackedScene();if(saved_world.Pack(world)!=Error.Ok)saved_world=null;}
        if(GetTree().ChangeSceneToFile("res://tscn/battle/BattleScene.tscn")!=Error.Ok)GD.PushError("BattleTransitionMgr: 无法加载战斗场景");
    }
    public string RetreatDescription()=>_beforeBattle.Any(d=>d.current_health>0&&d.faction==(int)WorldEntity.Faction.ENEMY&&d.dun_speed<=0)?"敌方遁速尚未配置，本场暂不可撤退":!return_context.GetValueOrDefault("allow_retreat",true).AsBool()?"本场禁止撤退":retreat_coefficients_configured()?"允许撤退：占一个行动位，按遁速概率判定":"撤退公式系数待配置，暂不可尝试";
    public bool retreat_coefficients_configured()=>return_context.GetValueOrDefault("retreat_self_coefficient",0f).AsSingle()>0 && return_context.GetValueOrDefault("retreat_enemy_coefficient",0f).AsSingle()>0;
    public bool try_get_retreat_chance(float own,float enemy,out float chance)
    {
        chance=0;
        if(!return_context.GetValueOrDefault("allow_retreat",true).AsBool()||!retreat_coefficients_configured()||own<=0||enemy<=0)return false;
        var a=return_context["retreat_self_coefficient"].AsSingle();var b=return_context["retreat_enemy_coefficient"].AsSingle();
        chance=Mathf.Clamp(a*own/(a*own+b*enemy),.1f,.9f);return true;
    }
    private void PrepareSettlement(GArray updatedAttackers,GArray updatedDefenders,bool escaped)
    {
        if(_awaitingSettlement)return;_awaitingSettlement=true;
        var all=Data(updatedAttackers).Concat(Data(updatedDefenders)).ToList();
        // 战斗管理器按阵营回传；世界遭遇记录按原发起方回传，二者不能混为一谈。
        result_attackers=new GArray(_attackerOrder.Select(id=>all.FirstOrDefault(d=>d.uid==id)).Where(d=>d is not null).Select(d=>Variant.From(d!)));
        result_defenders=new GArray(_defenderOrder.Select(id=>all.FirstOrDefault(d=>d.uid==id)).Where(d=>d is not null).Select(d=>Variant.From(d!)));
        if(_beforeBattle.Count==0){result_attackers=updatedAttackers;result_defenders=updatedDefenders;}
        var victory=!escaped&&all.Any(d=>d.faction==(int)WorldEntity.Faction.PLAYER&&d.current_health>0)&&all.Where(d=>d.faction==(int)WorldEntity.Faction.ENEMY).All(d=>d.current_health<=0);
        last_summary=new GDictionary{{"attackers_alive",alive_count(result_attackers)},{"defenders_alive",alive_count(result_defenders)},{"player_victory",victory},{"escaped",escaped},{"is_draw",all.All(d=>d.current_health<=0)},{"battle_id",_battleId}};
        GArray rewards=[];
        var spectator=return_context.GetValueOrDefault("kind","").AsString()=="east_woods_npc_encounter"&&!return_context.GetValueOrDefault("target_player",false).AsBool();
        if(victory&&!spectator)
        {
            foreach(var defeated in all.Where(d=>d.faction==(int)WorldEntity.Faction.ENEMY&&d.current_health<=0))RollLoot(defeated.battle_loot.Select(d=>Variant.From(d)),defeated.entity_name,rewards);
            if(return_context.TryGetValue("loot_rewards",out var configured))RollLoot(configured.AsGodotArray(),"遭遇奖励",rewards);
            // 东林地已有明确的击杀掠夺规则，沿用该遭遇，而非全局默认搬空背包。
            if(return_context.GetValueOrDefault("kind","").AsString()=="east_woods_npc_encounter"&&return_context.GetValueOrDefault("target_player",false).AsBool()&&!Training)
            {
                var loser=all.FirstOrDefault(d=>d.uid.StartsWith("worldnpc:")&&d.current_health<=0);
                if(loser is not null)
                {
                    for(var i=0;i<Math.Min(loser.pack_item_paths.Count,loser.pack_item_counts.Count);i++)AddLoot(loser.pack_item_paths[i],loser.pack_item_counts[i],loser.entity_name,rewards,loser.uid[9..]);
                    return_context["loot_handled_by_settlement"]=true;
                }
            }
        }
        var rewardPlayer=all.FirstOrDefault(d=>d.faction==(int)WorldEntity.Faction.PLAYER&&!d.uid.StartsWith("worldnpc:"));
        if(!Training)_rewardLedger[_battleId]=new GDictionary{{"entries",rewards},{"player_uid",rewardPlayer?.uid??""},{"claimed",rewards.Count==0}};
        var title=Training?(escaped?"演练结束 · 已撤离":victory?"演练结束 · 胜利":"演练结束 · 败北"):escaped?"成功撤离":victory?"战斗胜利":spectator?"战斗结束":"战斗败北";
        OpenFlow().Results(title,_beforeBattle,all,rewards,Training,ReturnAfterSettlement,(escaped?"已脱离战斗，不发放胜利战利品。":"生命按战斗实际结果回传；以下为战斗结束时的伤势，临时状态按现行规则清理。")+"\n"+_outcomeNote);
    }
    public void end_team_battle_escaped(GArray attackers,GArray defenders)=>PrepareSettlement(attackers,defenders,true);
    private void ReturnAfterSettlement()
    {
        CloseFlow();_awaitingSettlement=false;has_results=!Training;
        if(Training){result_attackers=[];result_defenders=[];last_summary.Clear();return_context.Clear();}
        var error=saved_world is not null?GetTree().ChangeSceneToPacked(saved_world):Error.Failed;
        if(error!=Error.Ok&&!string.IsNullOrEmpty(world_scene_path))GetTree().ChangeSceneToFile(world_scene_path);
    }
    private static void RollLoot(IEnumerable<Variant> definitions,string source,GArray rewards)
    {
        foreach(var variant in definitions)
        {
            if(variant.VariantType!=Variant.Type.Dictionary)continue;
            var row=variant.AsGodotDictionary();var chance=row.GetValueOrDefault("chance",1f).AsSingle();
            var min=row.GetValueOrDefault("min_count",row.GetValueOrDefault("count",0)).AsInt32();var max=row.GetValueOrDefault("max_count",min).AsInt32();
            if(chance<0||chance>1||min<=0||max<min){GD.PushWarning("战斗掉落配置不合法，未生成奖励。");continue;}
            if(GD.Randf()<chance)AddLoot(row.GetValueOrDefault("item_path","").AsString(),GD.RandRange(min,max),source,rewards);
        }
    }
    private static void AddLoot(string path,int count,string source,GArray rewards,string npcId="")
    {
        if(count<=0||string.IsNullOrEmpty(path)||!ResourceLoader.Exists(path)||GD.Load<ItemData>(path) is null)return;
        rewards.Add(new GDictionary{{"item_path",path},{"count",count},{"source",source},{"npc_id",npcId}});
    }
    private CanvasLayer? _lootReminder;
    private void ShowPendingLootReminder()
    {
        if(GodotObject.IsInstanceValid(_lootReminder))return;
        _lootReminder=new CanvasLayer{Layer=105};AddChild(_lootReminder);
        var root=new Control();_lootReminder.AddChild(root);root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);root.MouseFilter=Control.MouseFilterEnum.Ignore;
        var button=new Button{Text="待领战利品",CustomMinimumSize=new(156,42)};BattleUiArt.Skin(button);root.AddChild(button);
        button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);button.Position=new(-180,80);
        button.Pressed+=resume_pending_loot;
    }
    public void resume_pending_loot()
    {
        _lootReminder?.QueueFree();_lootReminder=null;
        if(GetTree().CurrentScene is not MainScene main || main.get_player_character() is not { } player)return;
        foreach(var key in _rewardLedger.Keys.ToArray())
        {
            var record=_rewardLedger[key].AsGodotDictionary();if(record.GetValueOrDefault("claimed",false).AsBool())continue;
            if(record.GetValueOrDefault("player_uid","").AsString()!=player.unique_id)continue;
            main.notify_battle_rewards_changed();
            var entries=record["entries"].AsGodotArray();
            foreach(var value in entries)
            {
                var entry=value.AsGodotDictionary();var count=entry["count"].AsInt32();if(count<=0)continue;
                if(!player.add_item_to_inventory(entry["item_path"].AsString(),count))continue;
                var npc=GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager")?.get_npc(entry.GetValueOrDefault("npc_id","").AsString());npc?.change_carried_item(entry["item_path"].AsString(),-count);
                entry["count"]=0;
                main.notify_battle_rewards_changed();
            }
            var remaining=new GArray(entries.Where(v=>v.AsGodotDictionary()["count"].AsInt32()>0));record["claimed"]=remaining.Count==0;
            if(remaining.Count>0)
            {
                OpenFlow().Results("战利品待领取",[],[player.get_snapshot_data()],remaining,false,()=>{CloseFlow();resume_pending_loot();},"行囊空间不足，未收取物品保留，不会重抽或重复发放。请先整理行囊。",()=>{CloseFlow();ShowPendingLootReminder();});
                return;
            }
        }
    }
}
