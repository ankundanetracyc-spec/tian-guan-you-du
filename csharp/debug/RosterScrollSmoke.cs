using Godot;
using System;
using System.Threading.Tasks;

public partial class RosterScrollSmoke : Node
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private async Task Frames(int n=4) { for(int i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private void Wheel(Vector2 point, MouseButton direction)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position=point,GlobalPosition=point },true);
        GetViewport().PushInput(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=direction,Pressed=true },true);
        GetViewport().PushInput(new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=direction,Pressed=false },true);
    }
    private static Godot.Collections.Array<WorldNpcData> Roster(int count)
    {
        var result=new Godot.Collections.Array<WorldNpcData>();
        for(int i=0;i<count;i++) result.Add(new WorldNpcData{npc_id="scroll_"+i,npc_name="测试同修"+i});
        return result;
    }
    public override async void _Ready()
    {
        try
        {
            GetWindow().ContentScaleSize=new Vector2I(1920,1080);
            var world=GD.Load<PackedScene>("res://tscn/world/TileWorldMapPresentation.tscn").Instantiate<Control>();
            AddChild(world);
            world.Show();
            var hud=new CanvasLayer{Layer=20}; AddChild(hud);
            var list=GD.Load<PackedScene>("res://tscn/panel/CellCharacterList.tscn").Instantiate<CellCharacterList>();
            hud.AddChild(list);
            var scroll=list.GetNode<ScrollContainer>("ListPanel/CharacterScroll");
            foreach(int count in new[]{0,1,7})
            {
                list.show_world_npcs(Roster(count),"world:scroll"); await Frames(20);
                Check(!scroll.GetVScrollBar().Visible,$"{count}人仍显示多余滑条");
                var zoom=world.Get("_user_zoom").AsSingle();
                Wheel(scroll.GetGlobalRect().GetCenter(),MouseButton.WheelDown); await Frames();
                Check(Mathf.IsEqualApprox(zoom,world.Get("_user_zoom").AsSingle()),"短名单滚轮泄漏地图缩放");
            }
            list.show_world_npcs(Roster(25),"world:scroll"); await Frames(30);
            Check(scroll.GetVScrollBar().Visible,"长名单无滑条");
            var initialZoom=world.Get("_user_zoom").AsSingle();
            Wheel(scroll.GetGlobalRect().Position+new Vector2(100,35),MouseButton.WheelDown); await Frames();
            Check(scroll.ScrollVertical>0,"头像/人物行上的滚轮未滚名单");
            Check(Mathf.IsEqualApprox(initialZoom,world.Get("_user_zoom").AsSingle()),"名单内滚轮缩放世界");
            scroll.ScrollVertical=0; await Frames();
            Wheel(list.get_ui_rect().Position+new Vector2(100,35),MouseButton.WheelDown); await Frames();
            Check(scroll.ScrollVertical>0,"标题区域滚轮未滚名单");
            scroll.ScrollVertical=100000; await Frames();
            Wheel(scroll.GetGlobalRect().GetCenter(),MouseButton.WheelDown); await Frames();
            Check(Mathf.IsEqualApprox(initialZoom,world.Get("_user_zoom").AsSingle()),"名单底部滚轮泄漏缩放");
            Wheel(new Vector2(1000,500),MouseButton.WheelDown); await Frames();
            Check(!Mathf.IsEqualApprox(initialZoom,world.Get("_user_zoom").AsSingle()),"地图区域无法正常缩放");
            world.Hide();
            var dungeon=new EastWoodsDungeonPanel(); AddChild(dungeon); dungeon.show_dungeon();
            list.show_world_npcs(Roster(25),"interior:scroll"); await Frames(30);
            scroll.ScrollVertical=0; await Frames();
            var map=dungeon.GetNode<Control>("MapViewport/OrthogonalNodeMap");
            var scale=map.Scale;
            Wheel(scroll.GetGlobalRect().Position+new Vector2(100,35),MouseButton.WheelDown); await Frames();
            Check(scroll.ScrollVertical>0 && map.Scale==scale,"副本内名单滚轮未隔离");
            list.show_world_npcs(Roster(1),"interior:scroll"); await Frames(20);
            Check(!scroll.GetVScrollBar().Visible && scroll.ScrollVertical==0,"人数减少未隐藏滑条/复位滚动");
            GD.Print("ROSTER_SCROLL PASS: 0/1/7 auto-hide, 25 scrolling, row/header/end wheel capture, world/dungeon isolation, map wheel intact, shrink reset");
            GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
