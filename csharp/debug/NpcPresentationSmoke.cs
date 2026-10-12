using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class NpcPresentationSmoke : Node
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static int Portraits(Node node) => (node is PortraitRenderer ? 1 : 0) + node.GetChildren().Sum(Portraits);
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1920,1080);
            foreach (int count in new[] { 1, 2, 7, 15, 60 })
            for (ulong seed = 0; seed < 100; seed++)
            {
                var bounds = new Vector2(135,110);
                var packed = EastWoodsDungeonPanel.pack_pawns(bounds,count,seed);
                var again = EastWoodsDungeonPanel.pack_pawns(bounds,count,seed);
                Check(packed.SequenceEqual(again),"站位刷新不稳定");
                for (int i=0;i<count;i++)
                {
                    Check(new Rect2(Vector2.Zero,bounds).Encloses(packed[i]),"棋子越界");
                    for (int j=0;j<i;j++) Check(!packed[i].Intersects(packed[j]),"棋子重叠");
                }
            }
            GD.Print("PAWN_PACKING PASS: 500 layouts, bounds, no overlap, deterministic");
            var manager = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            manager.npcs.Clear();
            for (int i=0;i<20;i++) manager.npcs.Add(new WorldNpcData {
                npc_id="spawn_smoke_"+i,current_node_id="east_woods",is_alive=true,
                dungeon_id=i<10?EastWoodsDungeonRules.DungeonId:"",dungeon_node_id=i<10?"forest_edge":"" });
            var walker = manager.npcs[0]; walker.dungeon_step_index=2; walker.dungeon_travel_progress=.25f;
            manager.enter_east_woods_route();
            Check(walker.dungeon_node_id=="forest_edge" && walker.dungeon_step_index==2 && walker.dungeon_travel_progress==.25f,"迁移覆盖真实进度");
            Check(manager.npcs.Select(n=>n.dungeon_node_id).Distinct().Count()>8,"入场仍挤入口");
            var locations=manager.npcs.Select(n=>n.dungeon_node_id).ToArray();
            manager.enter_east_woods_route();
            Check(locations.SequenceEqual(manager.npcs.Select(n=>n.dungeon_node_id)),"重进重置位置");
            GD.Print("DUNGEON_SPAWN PASS: new/legacy scattered, active journey preserved, reentry stable");
            var list=GD.Load<PackedScene>("res://tscn/panel/CellCharacterList.tscn").Instantiate<CellCharacterList>();
            AddChild(list);
            var roster=new Godot.Collections.Array<WorldNpcData>();
            for(int i=0;i<60;i++) roster.Add(new WorldNpcData{npc_id="roster_smoke_"+i,npc_name="同修"+i,appearance_seed=i+1});
            var watch=System.Diagnostics.Stopwatch.StartNew();
            list.show_world_npcs(roster,"world:smoke");
            watch.Stop();
            Check(Portraits(list)==0,"入格同步创建全部头像");
            var box=list.GetNode<Container>("ListPanel/CharacterScroll/CharacterVBox");
            var first=box.GetChild(0).GetInstanceId();
            list.show_world_npcs(roster,"world:smoke");
            Check(first==box.GetChild(0).GetInstanceId(),"重复刷新重建名单");
            await Frames(30);
            var visibleLoaded=Portraits(list);
            Check(visibleLoaded>0 && visibleLoaded<=9,"屏幕外头像被加载");
            first=box.GetChild(0).GetInstanceId();
            list.show_world_npcs(roster,"world:smoke");
            Check(first==box.GetChild(0).GetInstanceId(),"头像加载后重复刷新重建名单");
            var scroll=list.GetNode<ScrollContainer>("ListPanel/CharacterScroll");
            scroll.ScrollVertical=4000;
            await Frames(30);
            Check(Portraits(list)>visibleLoaded && Portraits(list)<22,"滚动未按需加载");
            GD.Print($"ROSTER_LAZY PASS: 60 people, initial rows {watch.Elapsed.TotalMilliseconds:F2}ms, first view {visibleLoaded} portraits, total after scroll {Portraits(list)}, unchanged rows retained");
            list.GetNode<Button>("ToggleButton").EmitSignal(BaseButton.SignalName.Pressed);
            list.show_world_npcs(roster,"world:second");
            await Frames(20);
            Check(Portraits(list)==0 && !box.Visible,"收起后强制展开或后台加载头像");
            GD.Print("ROSTER_COLLAPSED PASS: movement preserves collapse, no hidden portrait work");
            GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
