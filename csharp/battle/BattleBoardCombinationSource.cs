using Godot;

/// <summary>组合只读来源；局内拖入与点击落点共用整组校验。</summary>
public partial class BattleBoardCombinationSource : Control
{
    public BattleBoardView? Board;
    public CombatBoardLayout Layout=new();
    public Action? Picked;
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Left }){Picked?.Invoke();AcceptEvent();}
    }
    public override Variant _GetDragData(Vector2 atPosition)
    {
        HoverInfoManager.HideFor(this);return Board?.BeginCombinationDrag(this,Layout)??default;
    }
    public override void _Draw()=>DrawLine(new(0,Size.Y-1),new(Size.X,Size.Y-1),new Color("9a8a7155"));
}
