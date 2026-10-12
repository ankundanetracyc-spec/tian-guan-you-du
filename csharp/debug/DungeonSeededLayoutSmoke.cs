using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Seed, graph, art and real Resource save/load checks; never opens a player save slot.</summary>
public partial class DungeonSeededLayoutSmoke : Node
{
    private static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
    private async Task Frames() { for(var i=0;i<4;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        string? tempPath=null;
        try
        {
            var snapshots = new System.Collections.Generic.HashSet<string>();
            for(var i=0;i<100;i++)
            {
                var seed="layout-check-"+i;
                EastWoodsDungeonRules.generate_layout(seed);
                var snapshot=EastWoodsDungeonRules.get_layout_state();
                var json=Json.Stringify(snapshot);
                // The same validation is used by loading, including required old content nodes.
                EastWoodsDungeonRules.restore_layout(snapshot);
                Check(Json.Stringify(EastWoodsDungeonRules.get_layout_state())==json,"地图恢复改变布局");
                EastWoodsDungeonRules.generate_layout(seed);
                Check(Json.Stringify(EastWoodsDungeonRules.get_layout_state())==json,"相同种子未复现");
                Check(EastWoodsDungeonRules.get_nodes().Any(n=>n.Terrain==6),"竹林缺失");
                Check(EastWoodsDungeonRules.get_nodes().Any(n=>n.Terrain==7),"阴岩地面缺失");
                Check(EastWoodsDungeonRules.get_nodes().Count(n=>n.Feature=="spring")==1,"泉眼特征缺失或重复");
                snapshots.Add(string.Join(";",EastWoodsDungeonRules.get_nodes().Select(n=>n.Id)));
            }
            Check(snapshots.Count==100,"不同种子的布局发生重复");
            EastWoodsDungeonRules.generate_layout("visual-check-20261011");
            var layout=EastWoodsDungeonRules.get_layout_state();
            EastWoodsDungeonRules.validate_layout(layout);
            EastWoodsDungeonRules.validate_layout(new GDictionary());
            Check(Json.Stringify(EastWoodsDungeonRules.get_layout_state())==Json.Stringify(layout),"只读校验修改了当前地图");
            var corrupted=layout.Duplicate(true);
            corrupted["edges"]=new Godot.Collections.Array();
            var rejected=false;
            try { EastWoodsDungeonRules.restore_layout(corrupted); } catch(InvalidOperationException) { rejected=true; }
            Check(rejected && Json.Stringify(EastWoodsDungeonRules.get_layout_state())==Json.Stringify(layout),"损坏地图静默重抽或污染有效地图");

            var main=new EastWoodsActionHarness(); AddChild(main);
            var prepare=typeof(MainScene).GetMethod("prepare_save_session",BindingFlags.Instance|BindingFlags.NonPublic)!;
            prepare.Invoke(main,null);
            Check(EastWoodsDungeonRules.LayoutSeed!="legacy","新旅程未创建种子");
            EastWoodsDungeonRules.generate_layout("visual-check-20261011");
            var panel=new EastWoodsDungeonPanel(); AddChild(panel); panel.show_dungeon(); await Frames();
            var state=panel.get_route_state(); state["current"]="outer_09_07";
            state["actions"]=new GDictionary { ["smoke-marker"]=new GDictionary { ["remaining"]=1,["completed"]=true } };
            panel.restore_route_state(state); panel.show_dungeon(); await Frames();
            var resource=new PersistentGameSave { scene_progress=new GDictionary { ["east_woods_dungeon_state"]=panel.get_route_state() } };
            tempPath="user://dungeon_seed_smoke_"+Guid.NewGuid().ToString("N")+".tres";
            Check(ResourceSaver.Save(resource,tempPath)==Error.Ok,"正式 Resource 格式存档失败");
            EastWoodsDungeonRules.use_legacy_layout();
            var loaded=ResourceLoader.Load<PersistentGameSave>(tempPath,cacheMode:ResourceLoader.CacheMode.Ignore);
            var restore=typeof(MainScene).GetMethod("restore_scene_progress",BindingFlags.Instance|BindingFlags.NonPublic)!;
            restore.Invoke(main,new object[]{loaded.scene_progress});
            var restored=loaded.scene_progress["east_woods_dungeon_state"].AsGodotDictionary();
            panel.restore_route_state(restored); panel.show_dungeon(); await Frames();
            Check(EastWoodsDungeonRules.LayoutSeed=="visual-check-20261011","正式存档未恢复种子");
            Check(panel.get_current_route_node_id()=="outer_09_07","当前位置丢失");
            Check(panel.get_route_state()["actions"].AsGodotDictionary().ContainsKey("smoke-marker"),"资源事件进度丢失");
            Check(Json.Stringify(EastWoodsDungeonRules.get_layout_state())==Json.Stringify(layout),"正式存档地图改变");
            var map=panel.GetNode<Control>("MapViewport/OrthogonalNodeMap");
            Check(map.GetChildren().OfType<Button>().Count()==128,"界面未重建128格");
            foreach(var terrain in Enum.GetValues<DungeonTerrainVisuals.Terrain>())
            {
                var count=DungeonTerrainVisuals.CountFor(terrain);
                var variants=Enumerable.Range(0,count).Select(v=>DungeonTerrainVisuals.GetVariant(terrain,v)).ToArray();
                Check(variants.All(t=>t.Atlas is not null && t.Region.Size.X>0),"地形素材加载失败");
                Check(variants.Select(t=>(t.Atlas.ResourcePath,t.Region)).Distinct().Count()==count,"地形变体重复");
            }
            foreach(var n in EastWoodsDungeonRules.get_nodes().Where(n=>n.Terrain is 5 or 6 or 7))
            {
                var texture=(AtlasTexture)map.GetNode<TextureRect>("Node_"+n.Id+"/Terrain").Texture;
                Check(texture.Atlas.ResourcePath.Contains("expansion_v1"),"新地形没有实际显示新素材");
                if(n.Feature=="spring") Check(map.GetNodeOrNull("Node_"+n.Id+"/SpringFeature") is TextureRect,"泉眼叠层缺失");
            }
            if(OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                var captureNode=EastWoodsDungeonRules.get_nodes().First(n=>n.Feature=="spring").Id;
                var viewState=panel.get_route_state(); viewState["current"]=captureNode;
                panel.restore_route_state(viewState); panel.show_dungeon(); await Frames();
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                var folder="res://docs/美术规范/副本扩展地形_20261011/实机验收";
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
                GetViewport().GetTexture().GetImage().SavePng(folder+"/种子布局与泉眼.png");
            }
            restore.Invoke(main,new object[]{new GDictionary { ["east_woods_dungeon_state"]=new GDictionary { ["current"]="quiet_grove",["inside"]=true,["actions"]=state["actions"] } }});
            Check(EastWoodsDungeonRules.LayoutSeed=="legacy","旧档被重抽");
            Check(EastWoodsDungeonRules.get_node("outer_15_09") is null,"旧档固定缺口丢失");
            var legacy=new GDictionary { ["current"]="quiet_grove",["actions"]=state["actions"] };
            panel.restore_route_state(legacy); panel.show_dungeon(); await Frames();
            Check(panel.get_current_route_node_id()=="quiet_grove" && panel.get_route_state()["actions"].AsGodotDictionary().ContainsKey("smoke-marker"),"旧档进度丢失");
            GD.Print("DUNGEON_SEEDED_LAYOUT_SMOKE_PASS: 100 seeds, graph, variants, new/legacy journeys, Resource save/load, UI and progress");
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { if(tempPath is not null) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tempPath)); }
    }
}
