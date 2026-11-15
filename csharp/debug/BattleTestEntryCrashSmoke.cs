using Godot;
using System.Reflection;

public partial class BattleTestEntryCrashSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var scene=GD.Load<PackedScene>("res://tscn/scene/StartMenu.tscn");
            var menu=scene.Instantiate<StartMenu>();AddChild(menu);
            menu.Reparent(GetTree().Root);GetTree().CurrentScene=menu;
            GD.Print("BATTLE_ENTRY_CHECK: opening setup");
            typeof(StartMenu).GetMethod("OpenBattleTestSetup",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(menu,null);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(Find(menu,"载入") is { } load) {GD.Print("BATTLE_ENTRY_CHECK: loading saved preset");load.EmitSignal(Button.SignalName.Pressed);}
            if(OS.GetCmdlineUserArgs().Contains("--auto-build"))
                foreach(var button in AllButtons(menu).Where(button=>button.Text=="自动搭配技能与心法").ToArray())
                {GD.Print("BATTLE_ENTRY_CHECK: automatic build");button.EmitSignal(Button.SignalName.Pressed);}
            GD.Print("BATTLE_ENTRY_CHECK: setup ready; collecting");
            GC.Collect();GC.WaitForPendingFinalizers();
            var start=Find(menu,"开始战斗测试")!;
            GD.Print("BATTLE_ENTRY_CHECK: pressing start");
            start.EmitSignal(Button.SignalName.Pressed);
            GD.Print("BATTLE_ENTRY_CHECK: start returned");
            for(var i=0;i<60;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GC.Collect();GC.WaitForPendingFinalizers();
            GD.Print("BATTLE_ENTRY_CHECK: briefing alive");
            var begin=Find(GetTree().Root,"开始战斗")??throw new Exception("战前确认缺少开始按钮");
            begin.EmitSignal(Button.SignalName.Pressed);
            for(var i=0;i<180;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GC.Collect();GC.WaitForPendingFinalizers();
            if(GetTree().GetFirstNodeInGroup("battle_mgr") is not BattleManager manager)throw new Exception("没有进入战斗场景");
            GD.Print("BATTLE_ENTRY_CHECK: combat planning alive; units="+manager.all_units.Count);
            manager.confirm_player_queue();
            for(var i=0;i<300;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print("BATTLE_ENTRY_CHECK: combat execution alive");
            GetTree().Quit();
        }
        catch(Exception error){GD.PushError("BATTLE_ENTRY_CHECK: "+error);GetTree().Quit(1);}
    }
    private static Button? Find(Node node,string text)
    {
        if(node is Button button&&button.Text==text)return button;
        foreach(var child in node.GetChildren())if(Find(child,text) is { } found)return found;
        return null;
    }
    private static IEnumerable<Button> AllButtons(Node node)
    {
        if(node is Button button)yield return button;
        foreach(var child in node.GetChildren())foreach(var item in AllButtons(child))yield return item;
    }
}
