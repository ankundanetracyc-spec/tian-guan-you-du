using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GDictionary=Godot.Collections.Dictionary;
using GArray=Godot.Collections.Array;

/// <summary>Actual MainScene -> BattleScene -> MainScene handoff, without opening a save slot.</summary>
public partial class TianwenReturnSmoke : Node
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
    private static T Read<T>(object value,string name)=>(T)Field(value.GetType(),name).GetValue(value)!;
    private static void Invoke(object value,string name)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,null);
    private async Task Until(Func<bool> done,string message)
    {
        for(int i=0;i<1800;i++){if(done())return;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        throw new Exception(message);
    }
    public override async void _Ready()
    {
        try
        {
            var saveManager=GetNode<GameSaveManager>("/root/GameSaveManager");
            Check(!saveManager.has_active_slot,"冒烟不得写正式存档");
            var fixture=new PersistentGameSave {
                player_data=new HumanData {entity_name="演法验收", cultivation_realm="云息三层",current_world_stamina=73,has_spirit_core=true},
                current_node_id="yuxu_xianzong_core",world_cell=new Vector2I(34,166),year=2,month=4,day=8,hour=9,
                npc_population_version=1,npc_simulation_enabled=false,
                scene_progress=new GDictionary{{"qingxi_prologue",3},{"cloud_state_initialized",true},{"xuanjian_awakened",true}},
            };
            fixture.world_npcs.Add(new WorldNpcData{npc_id="return_smoke_npc",npc_name="留守同修",current_node_id="yuxu_xianzong_core"});
            Field(typeof(GameSaveManager),"_pendingLoad").SetValue(saveManager,fixture);
            GetTree().CurrentScene=null; // Keep this test runner alive through actual scene changes.
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn")==Error.Ok,"初始主场景载入失败");
            await Until(()=>GetTree().CurrentScene is MainScene,"主场景未就绪");
            for(int i=0;i<5;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var original=(MainScene)GetTree().CurrentScene;
            var player=Read<Character>(original,"player");
            var uid=player.unique_id; var health=player.current_health;
            var clock=GetNode<GameTimeMgr>("/root/GameTimeManager"); var hours=clock.get_total_game_hours();
            var transition=GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            Invoke(original,"start_yuxu_tianwen_practice");
            Invoke(transition,"BeginConfirmedBattle");
            await Until(()=>GetTree().CurrentScene?.SceneFilePath=="res://tscn/battle/BattleScene.tscn"&&!transition.is_flow_transition_active(),"演法未进入实际战斗场景");
            var before=Read<System.Collections.Generic.List<BattleEntityData>>(transition,"_beforeBattle");
            var attackers=new GArray();var defenders=new GArray();
            foreach(var entry in before)
            {
                var copy=(BattleEntityData)entry.Duplicate(true);
                if(copy.faction==(int)WorldEntity.Faction.PLAYER){copy.current_health=1;attackers.Add(copy);}
                else{copy.current_health=0;defenders.Add(copy);}
            }
            transition.end_team_battle(attackers,defenders);
            await Until(()=>Read<bool>(transition,"_awaitingSettlement")&&!transition.is_flow_transition_active(),"结算未就绪");
            Invoke(transition,"ReturnAfterSettlement");
            await Until(()=>GetTree().CurrentScene is MainScene,"演法未返回主场景");
            for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var restored=(MainScene)GetTree().CurrentScene;
            var restoredPlayer=Read<Character>(restored,"player");
            Check(Read<object>(restored,"_qingxi_prologue").ToString()=="Departed","返回重置青溪里剧情");
            Check(Read<string>(restored,"_player_world_node_id")=="yuxu_xianzong_core","返回丢失所在势力");
            Check(restoredPlayer.unique_id==uid && restoredPlayer.current_health==health && restoredPlayer.current_world_stamina==73,$"演练覆盖主角或伤势/精力：UID {uid}->{restoredPlayer.unique_id}, 生命 {health}->{restoredPlayer.current_health}, 精力 {restoredPlayer.current_world_stamina}");
            Check(clock.get_total_game_hours()==hours,"演练返回改变世界时间");
            Check(GetNode<NpcWorldManager>("/root/NpcWorldManager").get_npc("return_smoke_npc") is not null,"返回丢失NPC");
            Check(Read<string>(restored,"_player_current_yuxu_peak_id")=="tianwen" && restored.GetNode<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior").IsVisibleInTree(),"未回到天问峰");
            Check(!transition.has_pending_battle_return_data() && transition.consume_world_progress_snapshot() is null,"回传未一次消费");
            GD.Print("TIANWEN_RETURN PASS: actual scene roundtrip, departed story, peak, player identity/health/stamina, calendar, NPC, one-shot handoff, no save-slot writes");
            GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
