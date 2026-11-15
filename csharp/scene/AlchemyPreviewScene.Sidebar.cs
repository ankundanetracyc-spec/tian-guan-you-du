using Godot;

public partial class AlchemyPreviewScene
{
    private void BuildFurnaceSidebar(VBoxContainer status)
    {
        _roomDetails=GD.Load<PackedScene>("res://tscn/ui/alchemy/FurnaceSidebar.tscn").Instantiate<PanelContainer>();
        AddChild(_roomDetails);_roomDetails.ZIndex=8;
        _roomDetails.GetNode<Button>("Body/Header/Close").Pressed+=ToggleFurnaceInfo;
        var tabs=_roomDetails.GetNode<TabContainer>("Body/Tabs");
        tabs.AddThemeFontSizeOverride("font_size",20);
        VBoxContainer Page(string name)=>_roomDetails.GetNode<VBoxContainer>("Body/Tabs/"+name+"/Content");
        Page("炉况").AddChild(status);
        _condition=Text("",19,Ink,true);Page("炉况").AddChild(_condition);
        _task=Text("",18,Dim,true);Page("炉况").AddChild(_task);
        _inside=Box(8);Page("药材").AddChild(_inside);
        Page("炉记").AddChild(_guide);
        _changes=Box(12);Page("炉记").AddChild(_changes);
        _result=Text("",19,Jade,true);Page("炉记").AddChild(_result);
        Page("炉记").AddChild(RoomButton("查看完整炉记",ShowDetails));
        Page("炉记").AddChild(RoomButton("本息功用明细",ShowActiveUtilities));
        Page("炉记").AddChild(RoomButton("十二功用基础表",ShowUtilities));
        var demo=new HBoxContainer();demo.AddChild(_demoButton);demo.AddChild(_next);Page("炉记").AddChild(demo);
        _infoToggle=RoomAction("‹  炉内信息",ToggleFurnaceInfo);
        AddChild(_infoToggle);_infoToggle.ZIndex=9;
        _infoToggle.AnchorLeft=_infoToggle.AnchorRight=1;
        _infoToggle.AnchorTop=_infoToggle.AnchorBottom=.115f;
        _infoToggle.OffsetLeft=-174;_infoToggle.OffsetRight=-22;_infoToggle.OffsetBottom=50;
    }
    private void ToggleFurnaceInfo()
    {
        _roomDetails.Visible=!_roomDetails.Visible;
        _infoToggle.Visible=!_roomDetails.Visible;
        if(_roomDetails.Visible)_techniques.Hide();
    }
}
