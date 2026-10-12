using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Runs real duty/action callbacks without touching formal progress slots.</summary>
public partial class YuxuEastWoodsDutySmoke : Node
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static object? Call(MainScene main, string method, params object[] args) => typeof(MainScene).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main,args);
    private static void Set(MainScene main,string field,object value) => typeof(MainScene).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,value);
    private async Task Frames() { for(var i=0;i<4;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if(!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await Frames();
        var folder="res://docs/美术规范/东林地节点地图_20261011/实机验收/委托与札记";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(folder+"/"+name+".png");
    }
    public override async void _Ready()
    {
        var savePath="user://east_woods_duty_smoke_"+Guid.NewGuid().ToString("N")+".tres";
        try
        {
            GetWindow().ContentScaleSize=new Vector2I(1280,720);
            var main=new EastWoodsActionHarness(); AddChild(main); main.AddChild(new CanvasLayer{Name="SceneLayer"});
            var human=new HumanData{entity_name="任务验收",sect_affiliation_id="yuxu_xianzong",sect_current_rank="外门弟子", current_world_stamina=100,max_world_stamina=100};
            var player=new Character{entity_data=human}; main.AddChild(player); Set(main,"player",player); Set(main,"_player_current_yuxu_peak_id","heguang");
            void Duty(string action) => Call(main,"handle_yuxu_heguang_action",action,human);
            var boardLayer=new CanvasLayer{Layer=20}; AddChild(boardLayer);
            var board=new Control{Size=new Vector2(1280,720)}; board.SetScript(GD.Load<Script>("res://scripts/world/bai_chuan/YuxuSectDutyBoard.gd")); boardLayer.AddChild(board); board.Hide();
            var panel=(EastWoodsDungeonPanel)Call(main,"get_or_create_east_woods_dungeon")!;
            var map=panel.GetNode<Control>("MapViewport/OrthogonalNodeMap");
            Duty("heguang_accept_duty:east_woods_patrol");
            var instance=human.sect_active_yuxu_duty_instance_id;
            Duty("heguang_submit_duty:east_woods_patrol"); Check(human.get_yuxu_contribution_total()==0,"未巡查可领奖");
            panel.show_dungeon(); await Frames();
            var target=map.GetNode<Button>("Node_quiet_grove"); target.EmitSignal(BaseButton.SignalName.Pressed); target.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(6),SceneTreeTimer.SignalName.Timeout);
            Check(human.sect_active_yuxu_duty_ready && human.sect_active_yuxu_duty_stage>=4 && human.get_yuxu_contribution_total()==0,"现场未形成待回报或提前发奖");
            Check(human.sect_active_yuxu_duty_instance_id==instance,"巡查实例改变");
            var snapshot=new PersistentGameSave{player_data=human,scene_progress=new Godot.Collections.Dictionary{["east_woods_dungeon_state"]=panel.get_route_state()}};
            Check(ResourceSaver.Save(snapshot,savePath)==Error.Ok,"任务状态无法写入测试资源");
            var loaded=ResourceLoader.Load<PersistentGameSave>(savePath,"",ResourceLoader.CacheMode.Ignore)!;
            var restored=loaded.player_data!;
            Check(restored.sect_active_yuxu_duty_ready && restored.sect_active_yuxu_duty_instance_id==instance && restored.sect_active_yuxu_duty_stage==human.sect_active_yuxu_duty_stage,"待回报状态存读档丢失");
            Check(loaded.scene_progress["east_woods_dungeon_state"].AsGodotDictionary()["current"].AsString()=="quiet_grove","正式快照容器未保留副本位置");
            await Capture("01_巡查复核");
            Duty("heguang_submit_duty:east_woods_patrol"); Check(human.get_yuxu_contribution_total()==0,"副本内远程领奖");
            board.Call("open_board","east_woods_patrol"); board.Call("set_active_duty","east_woods_patrol",true,human.sect_active_yuxu_duty_stage,false);
            board.Call("_on_duty_pressed","east_woods_patrol");
            var accept=board.Get("_accept_button").AsGodotObject() as Button;
            Check(accept?.Text=="回报巡查" && !accept.Disabled,"任务板没有回报按钮"); board.Hide();
            panel.Hide(); board.Show(); await Capture("02_回报巡查"); board.Hide(); panel.Show();
            panel.close_action_details(); panel.interrupt_travel();
            var exit=map.GetNode<Button>("Node_forest_edge"); exit.EmitSignal(BaseButton.SignalName.Pressed); exit.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(6),SceneTreeTimer.SignalName.Timeout); panel.close();
            Check(!panel.Visible,"巡查后无法正常走出口");
            Set(main,"_player_current_yuxu_peak_id","guanfu"); Duty("heguang_submit_duty:east_woods_patrol"); Check(human.get_yuxu_contribution_total()==0,"非和光峰领奖");
            Set(main,"_player_current_yuxu_peak_id","heguang"); Duty("heguang_submit_duty:east_woods_patrol"); Duty("heguang_submit_duty:east_woods_patrol");
            Check(human.get_yuxu_contribution_total()==5 && human.sect_active_yuxu_duty_id=="" && human.sect_contribution_records.Count==1,"回报重复领奖或不清任务");
            Duty("heguang_accept_duty:beast_bone_submission"); Duty("heguang_submit_duty:beast_bone_submission"); Check(human.get_yuxu_contribution_total()==5,"缺物仍领奖");
            player.add_item_to_inventory("res://resources/entities/items/兽骨.tres",2);
            Duty("heguang_submit_duty:beast_bone_submission"); Duty("heguang_submit_duty:beast_bone_submission");
            Check(human.get_yuxu_contribution_total()==10 && player.get_inventory_total_count("res://resources/entities/items/兽骨.tres")==1,"物品交付数量或重复结算错误");
            // Replay an already settled instance: rollback must return the removed item.
            human.sect_active_yuxu_duty_id="beast_bone_submission";
            human.sect_active_yuxu_duty_instance_id=human.sect_contribution_records.Last().source_transaction_id.Split(':').Last();
            Duty("heguang_submit_duty:beast_bone_submission");
            Check(human.get_yuxu_contribution_total()==10 && player.get_inventory_total_count("res://resources/entities/items/兽骨.tres")==1,"重复流水扣物或发奖");
            Duty("heguang_cancel_duty:beast_bone_submission"); Check(!human.sect_active_yuxu_duty_ready && human.sect_active_yuxu_duty_id=="","取消未清状态");
            var entries=(System.Collections.Generic.List<JournalPanel.Entry>)Call(main,"get_quest_entries")!;
            Check(entries.Count(e=>e.Id.StartsWith("yuxu.settled."))==2,"贡献流水未进入任务完成记录");
            panel.show_dungeon(); panel.GetNode<Button>("ExplorationNotes").EmitSignal(BaseButton.SignalName.Pressed);
            var notes=(string)typeof(EastWoodsDungeonPanel).GetMethod("exploration_notes",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(panel,null)!;
            Check(!notes.Contains("苦苔根") && !notes.Contains("凶狼"),"札记泄露未探明内容");
            panel.complete_action("fern_lowland","explore"); panel.complete_action("fern_lowland","gather_root");
            notes=(string)typeof(EastWoodsDungeonPanel).GetMethod("exploration_notes",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(panel,null)!;
            Check(notes.Contains("苦苔根，余 2 份"),"札记没有读取实际探明和余量");
            panel.close_action_details(); panel.GetNode<Button>("ExplorationNotes").EmitSignal(BaseButton.SignalName.Pressed); await Capture("03_林间札记");
            panel.close_action_details(); panel.Hide();
            const string rootPath = "res://resources/entities/items/苦苔根.tres";
            const string rootDuty = "bitter_moss_root_submission";
            Duty("heguang_accept_duty:"+rootDuty);
            var rootInstance=human.sect_active_yuxu_duty_instance_id;
            Duty("heguang_submit_duty:"+rootDuty);
            Check(human.get_yuxu_contribution_total()==10,"零份苦苔根可领奖");
            player.add_item_to_inventory(rootPath);
            Duty("heguang_submit_duty:"+rootDuty);
            Check(human.get_yuxu_contribution_total()==10 && player.get_inventory_total_count(rootPath)==1,"缺一份仍扣物或领奖");
            board.Call("open_board",rootDuty); board.Call("set_active_duty",rootDuty,false,0,false,1);
            board.Call("_on_duty_pressed",rootDuty);
            Check(accept!.Disabled && accept.Text=="提交苦苔根","缺物按钮可提交");
            var harvestState=panel.get_route_state(); harvestState["current"]="fern_lowland";
            panel.restore_route_state(harvestState); panel.show_dungeon();
            Call(main,"on_east_woods_action","fern_lowland","gather_root");
            Check(player.get_inventory_total_count(rootPath)==3 && human.get_yuxu_contribution_total()==10,"实际采集未入行囊或提前发贡献");
            Check(panel.action_remaining("fern_lowland",EastWoodsActions.Content("fern_lowland")!)==0,"实际采集未耗库存");
            panel.close_action_details(); panel.Hide();
            board.Call("set_active_duty",rootDuty,false,0,false,3);
            Check(!accept.Disabled,"备齐后按钮不可提交");
            var rootTask=(JournalPanel.Entry)Call(main,"get_active_yuxu_journal_task")!;
            Check(rootTask.Body.Contains("3/2"),"任务持有量未更新");
            snapshot=new PersistentGameSave{player_data=human,scene_progress=new Godot.Collections.Dictionary{["east_woods_dungeon_state"]=panel.get_route_state()}};
            Check(ResourceSaver.Save(snapshot,savePath)==Error.Ok,"苦苔根事务无法存档");
            loaded=ResourceLoader.Load<PersistentGameSave>(savePath,"",ResourceLoader.CacheMode.Ignore)!;
            Check(loaded.player_data!.sect_active_yuxu_duty_id==rootDuty && loaded.player_data.sect_active_yuxu_duty_instance_id==rootInstance,"苦苔根接取实例存读丢失");
            var loadedPlayer=new Character{entity_data=loaded.player_data};
            Check(loadedPlayer.get_inventory_total_count(rootPath)==3,"交付物存读丢失"); loadedPlayer.Free();
            board.Show(); await Capture("04_苦苔根交付"); board.Hide();
            Set(main,"_player_current_yuxu_peak_id","guanfu"); Duty("heguang_submit_duty:"+rootDuty);
            Check(player.get_inventory_total_count(rootPath)==3,"异地可扣交付物");
            Set(main,"_player_current_yuxu_peak_id","heguang"); panel.Show(); Duty("heguang_submit_duty:"+rootDuty);
            Check(player.get_inventory_total_count(rootPath)==3,"副本内可远程提交"); panel.Hide();
            Duty("heguang_submit_duty:"+rootDuty); Duty("heguang_submit_duty:"+rootDuty);
            Check(human.get_yuxu_contribution_total()==15 && player.get_inventory_total_count(rootPath)==1 && human.sect_active_yuxu_duty_id=="","苦苔根扣物数量或防重错误");
            player.add_item_to_inventory(rootPath);
            human.sect_active_yuxu_duty_id=rootDuty; human.sect_active_yuxu_duty_instance_id=rootInstance;
            Duty("heguang_submit_duty:"+rootDuty);
            Check(human.get_yuxu_contribution_total()==15 && player.get_inventory_total_count(rootPath)==2,"苦苔根重复流水未回退两份");
            Duty("heguang_cancel_duty:"+rootDuty);
            Check(human.sect_active_yuxu_duty_id=="" && player.get_inventory_total_count(rootPath)==2,"取消苦苔根任务扣物或未清理");
            entries=(System.Collections.Generic.List<JournalPanel.Entry>)Call(main,"get_quest_entries")!;
            Check(entries.Count(e=>e.Id.StartsWith("yuxu.settled."))==3,"苦苔根流水未进入完成记录");
            boardLayer.QueueFree(); main.QueueFree(); await Frames(); GC.Collect(); GC.WaitForPendingFinalizers();
            GD.Print("YUXU_EAST_WOODS_DUTY PASS: real route, pending report, resource save/load, board status, exit, place guard, item handoff, idempotency/rollback, journal, knowledge-only notes");
            GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        finally { if(Godot.FileAccess.FileExists(savePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath)); }
    }
}
