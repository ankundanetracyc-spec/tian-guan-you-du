using Godot;

/// <summary>原生拖拽的无鼠标命中预览；只改临时候选，合法松手才提交。</summary>
public partial class BattleBoardDragPreview : Control
{
    internal BattleBoardView Board = null!;
    internal BattleBoardDragSession Session = null!;

    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;ProcessMode=ProcessModeEnum.Always;
        SetProcessInput(true);UpdateAppearance();
    }
    public override void _Process(double delta)
    {
        if(!GodotObject.IsInstanceValid(Board)||!Board.OwnsDrag(Session))return;
        UpdateAppearance();QueueRedraw();
    }
    private void UpdateAppearance()
    {
        if(Board.Catalog is null)return;
        var shape=Session.Combination?.size??CombatBoardGeometry.Footprint(Session.Candidate,Board.Catalog.FindSkill);
        Size=(Vector2)shape*Board.CellSize;
        Scale=Board.GetGlobalTransformWithCanvas().Scale;
        // 原生Viewport逐次把此控件定位到鼠标；抓取偏移放在绘制坐标，不能覆盖控件Position。
    }
    public override void _Input(InputEvent input)
    {
        if(!GodotObject.IsInstanceValid(Board)||!Board.OwnsDrag(Session))return;
        if(input is InputEventMouseMotion motion)
        {
            Board.UpdateDragEventPointer(motion.Position);QueueRedraw();return;
        }
        if(input is InputEventMouseButton { Pressed:false,ButtonIndex:MouseButton.Left } release)
        {
            Board.UpdateDragEventPointer(release.Position,true);return;
        }
        if(input is InputEventKey { Pressed:true,Echo:false } key&&(key.Keycode==Key.R||key.PhysicalKeycode==Key.R)
            ||input is InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Right })
        {
            Board.RotateDrag();UpdateAppearance();GetViewport().SetInputAsHandled();QueueRedraw();
        }
        else if(input.IsActionPressed("ui_cancel")) Session.Cancelled=true;
    }
    public override void _Draw()
    {
        if(!GodotObject.IsInstanceValid(Board)||Board.Catalog is null||!Board.OwnsDrag(Session)||Session.InsideBoard)return;
        var origin=-Session.Anchor*Board.CellSize;
        var placements=Session.Combination is { } combination?combination.placements.ToArray():new[]{Session.Candidate};
        foreach(var item in placements)
        {
            var shape=CombatBoardGeometry.Footprint(item,Board.Catalog.FindSkill);
            var local=Session.Combination is null?Vector2I.Zero:item.cell;
            for(var y=0;y<shape.Y;y++)for(var x=0;x<shape.X;x++)DrawTextureRect(BattleBoardSkin.Part("cell_face"),new Rect2(origin+(Vector2)(local+new Vector2I(x,y))*Board.CellSize,Vector2.One*Board.CellSize),false);
            BattleBoardPlacementArt.Draw(this,item,Board.Catalog,new Rect2(origin+(Vector2)local*Board.CellSize,(Vector2)shape*Board.CellSize),
                Board.CellSize,Board.CombatOwner,Board.Manager,item.expended,false,false,1,Colors.White,new Color("8a7653"));
        }
    }
}

internal sealed class BattleBoardDragSession(CombatBoardPlacement candidate,bool fromBoard,Vector2 anchor)
{
    internal readonly string Token=Guid.NewGuid().ToString("N");
    internal readonly CombatBoardPlacement Candidate=candidate;
    internal readonly bool FromBoard=fromBoard;
    internal Vector2 Anchor=anchor;
    internal Vector2 Pointer;
    internal bool HasEventPointer,ReleaseReceived;
    internal bool InsideBoard,PreviewValid,Committed,Cancelled;
    internal Vector2I? PreviewCell;
    internal BattleBoardDragPreview? Preview;
    internal CombatBoardLayout? Combination;
    internal Vector2I CombinationOrigin;
    internal int CombinationTurns;
    private CombatBoardPlacement[]? _candidates;
    private CombatBoardLayout? _candidateTemplate;
    private Vector2I _candidateOrigin;
    internal CombatBoardPlacement[] Candidates()
    {
        if(Combination is null)return [Candidate];
        if(_candidates is not null&&ReferenceEquals(_candidateTemplate,Combination)&&_candidateOrigin==CombinationOrigin)return _candidates;
        _candidateTemplate=Combination;_candidateOrigin=CombinationOrigin;
        return _candidates=Combination.placements.Select(item=>{var copy=item.CopyConfiguration();copy.cell+=CombinationOrigin;return copy;}).ToArray();
    }
}
