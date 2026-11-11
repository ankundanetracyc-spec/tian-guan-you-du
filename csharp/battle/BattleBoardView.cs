using Godot;

/// <summary>真实占形、原生拖放与只读遍历反馈；不在视图中支付费用或执行招式。</summary>
public partial class BattleBoardView : Control
{
    public CombatBoardLayout? Layout;
    public CombatBoardCatalog? Catalog;
    public CombatBoardRoundState? Runtime;
    public GodotObject? CombatOwner;
    public BattleManager? Manager;
    public float CellSize=76;
    public string Selected="";
    public HashSet<string>? SelectedPlacements;
    public bool Editable;
    public bool OccupiedCellsOnly;
    public bool RowSelectionEnabled { get; private set; }
    public int HoveredRow { get; private set; } = -1;
    public Action<Vector2I>? CellPressed;
    public Action<int>? RowPressed;
    public Action<string,Vector2I>? DropRequested;
    public Action<string>? SelectedChanged;
    public Func<CombatBoardPlacement,bool>? PlacementAllowed;
    public Func<CombatBoardPlacement,bool>? PlacementCommitted;
    public Action<string>? PlacementRemoved;
    public Action? DragEnded;
    public Func<IReadOnlyList<CombatBoardPlacement>,bool>? CombinationAllowed;
    public Func<IReadOnlyList<CombatBoardPlacement>,bool>? CombinationCommitted;

    private static readonly Dictionary<string,Texture2D> Icons=[];
    private static readonly HashSet<string> OwnedIcons=[];
    private static int _activeViews;
    private Control? _hoverAnchor;
    private string _hoverKey="";
    private BattleBoardDragSession? _drag;
    private CombatBoardRoundState? _feedbackRuntime;
    private int _feedbackRound;
    private long _lastFeedbackSerial;
    private readonly Queue<(Vector2I Cell,string PlacementId)> _feedbackQueue=[];
    private float _feedbackAge=1;
    private Vector2I _feedbackCell;
    private string _feedbackPlacementId="";
    private const float FeedbackDuration=.12f;

    public bool DragActive=>_drag is not null;
    public string DraggingPlacementId=>_drag?.Candidate.placement_id??"";
    public string HiddenPlacementId=>_drag?.FromBoard==true?_drag.Candidate.placement_id:"";
    public int DragTurns=>_drag is null?0:_drag.Combination is not null?_drag.CombinationTurns:_drag.Candidate.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator
        ?_drag.Candidate.rewrite_direction:_drag.Candidate.quarter_turns;
    public Vector2I? PreviewCell=>_drag?.InsideBoard==true?_drag.PreviewCell:null;
    public bool PreviewValid=>_drag?.InsideBoard==true&&_drag.PreviewValid;
    public string FeedbackPlacementId=>_feedbackAge<FeedbackDuration?_feedbackPlacementId:"";
    public Vector2I? FeedbackCell=>_feedbackAge<FeedbackDuration&&_feedbackPlacementId.Length==0?_feedbackCell:null;
    public float FeedbackScale
    {
        get
        {
            if(FeedbackPlacementId.Length==0)return 1;
            var t=Mathf.Clamp(_feedbackAge/FeedbackDuration,0,1);
            if(t<.3f)return 1+.07f*Mathf.Sin(t/.3f*Mathf.Pi/2);
            var tail=(t-.3f)/.7f;
            return 1+.07f*Mathf.Exp(-5*tail)*Mathf.Cos(tail*Mathf.Pi*2.8f);
        }
    }
    public float EmptyFeedbackOpacity
    {
        get
        {
            if(!FeedbackCell.HasValue)return 0;
            var t=Mathf.Clamp(_feedbackAge/FeedbackDuration,0,1);
            if(t<.26f)return .98f;
            return .98f*Mathf.Pow(1-Mathf.Clamp((t-.26f)/.74f,0,1),1.2f);
        }
    }
    public float EmptyFeedbackScale
    {
        get
        {
            if(!FeedbackCell.HasValue)return 1;
            var t=Mathf.Clamp(_feedbackAge/FeedbackDuration,0,1);
            if(t<.26f)return Mathf.Lerp(1.04f,1.14f,Mathf.Sin(t/.26f*Mathf.Pi/2));
            return Mathf.Lerp(1.14f,1,(t-.26f)/.74f);
        }
    }

    public override void _EnterTree()=>_activeViews++;
    public override void _Ready()=>MouseExited+=OnMouseExited;
    public override void _Notification(int what)
    {
        if(what==NotificationDragBegin)ClearBoardHover();
        else if(what==NotificationDragEnd)FinishDrag();
    }
    public override void _ExitTree()
    {
        ClearBoardHover();
        if(_drag is not null){HoverInfoManager.SetDragInProgress(this,false);_drag=null;}
        if(--_activeViews>0)return;
        foreach(var key in OwnedIcons)if(Icons.TryGetValue(key,out var texture))texture.Dispose();
        Icons.Clear();OwnedIcons.Clear();
    }
    public override void _Process(double delta)
    {
        if(_drag is not null)QueueRedraw();
        if(_feedbackRuntime!=Runtime)
        {
            _feedbackRuntime=Runtime;_feedbackRound=Runtime?.Round??0;_lastFeedbackSerial=0;
            _feedbackQueue.Clear();_feedbackAge=1;_feedbackPlacementId="";
        }
        if(Runtime is null)return;
        if(_feedbackRound!=Runtime.Round)
        {
            _feedbackRound=Runtime.Round;_feedbackQueue.Clear();_feedbackAge=1;_feedbackPlacementId="";
        }
        foreach(var visit in Runtime.VisitFeedbackEvents.Where(visit=>visit.Serial>_lastFeedbackSerial))
        {
            _feedbackQueue.Enqueue((visit.Cell,visit.PlacementId));_lastFeedbackSerial=visit.Serial;
        }
        if(Manager?.is_battle_paused()==true)return;
        if(_feedbackAge>=FeedbackDuration&&_feedbackQueue.TryDequeue(out var next))
        {
            _feedbackCell=next.Cell;_feedbackPlacementId=next.PlacementId;_feedbackAge=0;
        }
        if(_feedbackAge<FeedbackDuration)
        {
            _feedbackAge+=(float)delta;QueueRedraw();
        }
    }

    private void ClearBoardHover()
    {
        if(_hoverAnchor is not null)HoverInfoManager.HideFor(_hoverAnchor);
        _hoverKey="";
    }
    private void OnMouseExited()
    {
        ClearBoardHover();
        if(HoveredRow<0)return;
        HoveredRow=-1;QueueRedraw();
    }
    private void UpdateBoardHover(Vector2 position)
    {
        if(Layout is null||Catalog is null||DragActive||HoverInfoManager.IsDragInProgress()){ClearBoardHover();return;}
        var cell=CellAt(position);
        if(!InsideBoard(position)){ClearBoardHover();return;}
        var item=CombatBoardGeometry.At(Layout,cell,Catalog.FindSkill);
        var key=item is null?"empty:"+cell:item.GetInstanceId().ToString();
        if(_hoverAnchor is null)
        {
            _hoverAnchor=new Control { Name="BoardHoverAnchor",MouseFilter=MouseFilterEnum.Ignore };AddChild(_hoverAnchor);
        }
        var start=item?.cell??cell;var shape=item is null?Vector2I.One:CombatBoardGeometry.Footprint(item,Catalog.FindSkill);
        _hoverAnchor.Position=(Vector2)start*CellSize;_hoverAnchor.Size=(Vector2)shape*CellSize;
        if(_hoverKey==key)return;
        _hoverKey=key;
        HoverInfoManager.SetProvider(_hoverAnchor,()=>CombatBoardPresentation.PlacementHover(Layout,Catalog,item,CombatOwner,Runtime,Manager));
    }
    public static Texture2D? Icon(string path)
    {
        if(path.Length==0)return null;
        if(Icons.TryGetValue(path,out var cached))return cached;
        if(ResourceLoader.Exists(path)&&ResourceLoader.Load<Texture2D>(path)is {} imported)return Icons[path]=imported;
        var disk=ProjectSettings.GlobalizePath(path);if(!System.IO.File.Exists(disk))return null;
        using var image=Image.LoadFromFile(disk);OwnedIcons.Add(path);return Icons[path]=ImageTexture.CreateFromImage(image);
    }

    public override void _Draw()
    {
        if(Layout is null||Catalog is null)return;
        Size=(Vector2)Layout.size*CellSize;
        var groups=new Dictionary<Vector2I,string>();
        foreach(var item in Layout.placements.Where(item=>item.placement_id!=HiddenPlacementId))
            foreach(var cell in CombatBoardGeometry.Cells(item,CombatBoardGeometry.Footprint(item,Catalog.FindSkill)))groups[cell]=item.placement_id;
        IEnumerable<Vector2I> cells=OccupiedCellsOnly?groups.Keys:Enumerable.Range(0,Layout.size.Y).SelectMany(y=>Enumerable.Range(0,Layout.size.X).Select(x=>new Vector2I(x,y)));
        BattleBoardSkin.DrawCells(this,cells,CellSize,groups);
        if(Layout.unlocked_cells.Count>0)
            foreach(var cell in cells.Where(cell=>!Layout.unlocked_cells.Contains(cell)))
            {
                var rect=new Rect2((Vector2)cell*CellSize,Vector2.One*CellSize).Grow(-2);
                DrawRect(rect,new Color(.07f,.085f,.073f,.72f));
                DrawRect(rect,new Color(.50f,.48f,.39f,.65f),false,Mathf.Max(1,CellSize*.025f));
                DrawLine(rect.Position+new Vector2(8,8),rect.End-new Vector2(8,8),new Color(.70f,.66f,.52f,.45f),Mathf.Max(1,CellSize*.02f));
                DrawLine(new(rect.End.X-8,rect.Position.Y+8),new(rect.Position.X+8,rect.End.Y-8),new Color(.70f,.66f,.52f,.45f),Mathf.Max(1,CellSize*.02f));
            }
        if(Runtime is { SelectedRow: >=0 } runtime && !OccupiedCellsOnly)
        {
            var selectedRow=runtime.SelectedRow;
            var rowRect=new Rect2(0,selectedRow*CellSize,Layout.size.X*CellSize,CellSize).Grow(-1);
            DrawRect(rowRect,new Color(.80f,.66f,.31f,.16f));
            DrawRect(rowRect,new Color(.95f,.78f,.41f,.72f),false,Mathf.Max(1.5f,CellSize*.035f));
        }
        if(RowSelectionEnabled && HoveredRow>=0 && HoveredRow<Layout.size.Y)
        {
            var rowRect=new Rect2(0,HoveredRow*CellSize,Layout.size.X*CellSize,CellSize).Grow(-2);
            DrawRect(rowRect,new Color(.33f,.78f,.61f,.24f));
            DrawRect(rowRect,new Color(.56f,1f,.78f,.96f),false,Mathf.Max(2,CellSize*.045f));
        }
        if(FeedbackCell is {} empty)
        {
            var rect=new Rect2((Vector2)empty*CellSize,Vector2.One*CellSize).Grow(-1);
            DrawSetTransform(rect.GetCenter(),0,Vector2.One*EmptyFeedbackScale);
            var local=new Rect2(-rect.Size/2,rect.Size);var opacity=EmptyFeedbackOpacity;
            DrawRect(local.Grow(-2),new Color(.76f,.65f,.39f,opacity*.42f));
            BattleBoardSkin.DrawItemFrame(this,local,Mathf.Min(CellSize*.15f,9),new Color(1,.84f,.47f,opacity));
            DrawRect(local.Grow(-8),new Color(.98f,.89f,.63f,opacity*.9f),false,Mathf.Max(1.5f,CellSize*.028f));
            DrawSetTransform(Vector2.Zero);
        }
        // 当前占格的整个对象一起缩放；不叠加该格/整行高亮。
        foreach(var item in Layout.placements.Where(item=>item.placement_id!=HiddenPlacementId&&item.placement_id!=FeedbackPlacementId))
            DrawPlaced(item,1);
        if(FeedbackPlacementId.Length>0&&Layout.placements.FirstOrDefault(item=>item.placement_id==FeedbackPlacementId)is {} current)
            DrawPlaced(current,FeedbackScale);
        if(_drag?.InsideBoard==true)
        {
            var border=_drag.PreviewValid?new Color("648b70"):new Color("ac5747");
            foreach(var candidate in _drag.Candidates())
            {
                var shape=CombatBoardGeometry.Footprint(candidate,Catalog.FindSkill);
                foreach(var cell in CombatBoardGeometry.Cells(candidate,shape))
                    DrawRect(new Rect2((Vector2)cell*CellSize,Vector2.One*CellSize).Grow(-1),new Color(border.R,border.G,border.B,.17f));
                BattleBoardPlacementArt.Draw(this,candidate,Catalog,new Rect2((Vector2)candidate.cell*CellSize,(Vector2)shape*CellSize),
                    CellSize,CombatOwner,Manager,candidate.expended,false,false,1,new Color(1,1,1,.88f),border);
            }
        }
    }
    private void DrawPlaced(CombatBoardPlacement item,float scale)
    {
        if(Catalog is null)return;
        var shape=CombatBoardGeometry.Footprint(item,Catalog.FindSkill);
        if(shape.X<=0||shape.Y<=0)return;
        var dim=item.expended||(item.kind is CombatBoardPlacement.ObjectKind.Skill or CombatBoardPlacement.ObjectKind.Talisman)
            &&Runtime?.HasBeenChecked(item.placement_id)==true&&item.placement_id!=FeedbackPlacementId;
        BattleBoardPlacementArt.Draw(this,item,Catalog,new Rect2((Vector2)item.cell*CellSize,(Vector2)shape*CellSize),CellSize,
            CombatOwner,Manager,dim,item.placement_id==Selected||SelectedPlacements?.Contains(item.placement_id)==true,Runtime?.IsArmed(item.placement_id)==true,scale);
    }

    private Vector2I CellAt(Vector2 point)=>new(Mathf.FloorToInt(point.X/CellSize),Mathf.FloorToInt(point.Y/CellSize));
    private bool InsideBoard(Vector2 point)=>Layout is not null&&new Rect2(Vector2.Zero,(Vector2)Layout.size*CellSize).HasPoint(point);
    public void SetRowSelectionEnabled(bool enabled)
    {
        if(RowSelectionEnabled==enabled)return;
        RowSelectionEnabled=enabled;
        HoveredRow=-1;
        MouseDefaultCursorShape=enabled?CursorShape.PointingHand:CursorShape.Arrow;
        if(enabled)ClearBoardHover();
        QueueRedraw();
    }
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseMotion motion)
        {
            if(RowSelectionEnabled)
            {
                var row=InsideBoard(motion.Position)?CellAt(motion.Position).Y:-1;
                if(row!=HoveredRow){HoveredRow=row;QueueRedraw();}
                ClearBoardHover();
            }
            else UpdateBoardHover(motion.Position);
        }
        if(RowSelectionEnabled && Layout is not null && input is InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Left } rowClick
            && InsideBoard(rowClick.Position))
        {
            var row=CellAt(rowClick.Position).Y;
            if(row>=0 && Manager is not null && row<Manager.get_board_row_choice_count())RowPressed?.Invoke(row);
            AcceptEvent();
            return;
        }
        if(Layout is null||Catalog is null||!Editable||DragActive)return;
        if(input is InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Left } click&&InsideBoard(click.Position))
        {
            var cell=CellAt(click.Position);Selected=CombatBoardGeometry.At(Layout,cell,Catalog.FindSkill)?.placement_id??"";
            SelectedChanged?.Invoke(Selected);CellPressed?.Invoke(cell);QueueRedraw();AcceptEvent();
        }
    }
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if(!Editable||Layout is null||Catalog is null)return default;
        var item=CombatBoardGeometry.At(Layout,CellAt(atPosition),Catalog.FindSkill);if(item is null)return default;
        Selected=item.placement_id;SelectedChanged?.Invoke(Selected);
        return StartDrag(this,item.CopyConfiguration(),true,atPosition/CellSize-(Vector2)item.cell);
    }
    public Variant BeginSourceDrag(Control source,string key,Vector2? anchor=null)
    {
        if(!Editable||Catalog is null)return default;
        CombatBoardPlacement item;
        if(Catalog.FindSkill(key)is not null)item=new() { placement_id=Guid.NewGuid().ToString("N"),source_key=key };
        else if(Catalog.FindRewrite(key)is not null)item=new() { placement_id=Guid.NewGuid().ToString("N"),source_key=key,kind=CombatBoardPlacement.ObjectKind.Rewrite };
        else if(BattleSupplyRules.Load(key)is not null)item=new() { placement_id=Guid.NewGuid().ToString("N"),source_key=key,kind=CombatBoardPlacement.ObjectKind.Pill };
        else if(TalismanCombatCatalog.Load(key)is not null)item=new() { placement_id=Guid.NewGuid().ToString("N"),source_key=key,kind=CombatBoardPlacement.ObjectKind.Talisman };
        else if(key=="heart:xuan_shu")item=new() { placement_id="heart:xuan_shu:indicator",source_key=key,kind=CombatBoardPlacement.ObjectKind.ZhouTianIndicator,locked=true };
        else return default;
        var shape=CombatBoardGeometry.Footprint(item,Catalog.FindSkill);
        return StartDrag(source,item,false,anchor??(Vector2)shape/2);
    }
    public Variant BeginCombinationDrag(Control source,CombatBoardLayout saved)
    {
        if(!Editable||Catalog is null||saved.placements.Count==0||saved.layout_version!=CombatBoardLayout.CurrentLayoutVersion)return default;
        var layout=CombatBoardCombinations.Normalize(saved,Catalog);
        if(layout.placements.Any(item=>{var shape=CombatBoardGeometry.Footprint(item,Catalog.FindSkill);return shape.X<=0||shape.Y<=0;}))return default;
        foreach(var item in layout.placements)item.placement_id=Guid.NewGuid().ToString("N");
        return StartDrag(source,layout.placements[0].CopyConfiguration(),false,new(.5f,.5f),layout);
    }
    internal static CombatBoardLayout RotateCombination(CombatBoardLayout saved,CombatBoardCatalog catalog)=>CombatBoardCombinations.Rotate(saved,catalog);
    private Variant StartDrag(Control source,CombatBoardPlacement item,bool fromBoard,Vector2 anchor,CombatBoardLayout? combination=null)
    {
        ClearBoardHover();HoverInfoManager.HideFor(source);
        var shape=combination?.size??CombatBoardGeometry.Footprint(item,Catalog!.FindSkill);
        anchor=new(Mathf.Clamp(anchor.X,.001f,shape.X-.001f),Mathf.Clamp(anchor.Y,.001f,shape.Y-.001f));
        // 仅初始化时允许采样；后续坐标由同一条鼠标事件流驱动。
        _drag=new(item,fromBoard,anchor) { Pointer=GetLocalMousePosition(),Combination=combination };HoverInfoManager.SetDragInProgress(this,true);
        var preview=new BattleBoardDragPreview { Name="BoardDragPreview",Board=this,Session=_drag,MouseFilter=MouseFilterEnum.Ignore,ProcessMode=ProcessModeEnum.Always };
        _drag.Preview=preview;source.SetDragPreview(preview);RefreshDragPointer();QueueRedraw();
        return new Godot.Collections.Dictionary { ["board_drag_token"]=_drag.Token,["placement_id"]=item.placement_id,["source_key"]=item.source_key };
    }
    internal bool OwnsDrag(BattleBoardDragSession session)=>ReferenceEquals(_drag,session);
    private bool OwnsPayload(Variant data)=>_drag is not null&&data.VariantType==Variant.Type.Dictionary
        &&data.AsGodotDictionary().TryGetValue("board_drag_token",out var token)&&token.AsString()==_drag.Token;
    internal void UpdateDragEventPointer(Vector2 viewportPosition,bool released=false)
    {
        if(_drag is null||_drag.ReleaseReceived)return;
        _drag.Pointer=GetGlobalTransformWithCanvas().AffineInverse()*viewportPosition;
        _drag.HasEventPointer=true;_drag.ReleaseReceived=released;RefreshDragPointer();
    }
    private void UseNativePointerFallback(Vector2 point)
    {
        if(_drag is null)return;
        // 原生回调可能读取另一套OS采样；有事件坐标后不得覆盖，尤其是松手坐标。
        if(!_drag.HasEventPointer)_drag.Pointer=point;
        RefreshDragPointer();
    }
    internal void RefreshDragPointer()
    {
        if(_drag is not null)RefreshDragPointer(_drag.Pointer);
    }
    private void RefreshDragPointer(Vector2 point)
    {
        if(_drag is null||Catalog is null||Layout is null)return;
        _drag.InsideBoard=InsideBoard(point);
        var grab=new Vector2I(Mathf.FloorToInt(_drag.Anchor.X),Mathf.FloorToInt(_drag.Anchor.Y));
        var origin=CellAt(point)-grab;_drag.Candidate.cell=origin;_drag.CombinationOrigin=origin;_drag.PreviewCell=origin;
        _drag.PreviewValid=_drag.InsideBoard&&(_drag.Combination is null?Allowed(_drag.Candidate):CombinationAllowed?.Invoke(_drag.Candidates())==true);QueueRedraw();
    }
    private bool Allowed(CombatBoardPlacement item)
    {
        if(PlacementAllowed is not null)return PlacementAllowed(item);
        if(Layout is null||Catalog is null)return false;
        var occupied=Layout.placements.Where(existing=>existing.placement_id!=item.placement_id)
            .SelectMany(existing=>CombatBoardGeometry.Cells(existing,CombatBoardGeometry.Footprint(existing,Catalog.FindSkill))).ToHashSet();
        return CombatBoardGeometry.Fits(Layout.size,item.cell,CombatBoardGeometry.Footprint(item,Catalog.FindSkill),occupied);
    }
    internal void RotateDrag()
    {
        if(_drag is null||_drag.ReleaseReceived||Catalog is null)return;
        if(_drag.Combination is { } combination)
        {
            var next=CombatBoardCombinations.Rotate(combination,Catalog);
            if(combination.placements.Zip(next.placements).All(pair=>pair.First.cell==pair.Second.cell&&pair.First.quarter_turns==pair.Second.quarter_turns&&pair.First.rewrite_direction==pair.Second.rewrite_direction))return;
            _drag.Anchor=new(combination.size.Y-_drag.Anchor.Y,_drag.Anchor.X);_drag.Combination=next;_drag.CombinationTurns=(_drag.CombinationTurns+1)%4;
            RefreshDragPointer();_drag.Preview?.QueueRedraw();return;
        }
        var item=_drag.Candidate;
        if(item.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator)
            item.rewrite_direction=(item.rewrite_direction+1)%4;
        else
        {
            var canRotate=item.kind==CombatBoardPlacement.ObjectKind.Skill?Catalog.FindSkill(item.source_key)?.board_rotation_allowed==true
                :item.kind==CombatBoardPlacement.ObjectKind.Pill?BattleSupplyRules.Load(item.source_key)?.inventory_can_rotate==true
                :item.kind==CombatBoardPlacement.ObjectKind.Talisman&&TalismanCombatCatalog.Load(item.source_key)?.inventory_can_rotate==true;
            if(!canRotate)return;
            var old=CombatBoardGeometry.Footprint(item,Catalog.FindSkill);var wasRotated=item.quarter_turns%2!=0;
            item.quarter_turns=wasRotated?0:1;
            _drag.Anchor=wasRotated?new(_drag.Anchor.Y,old.X-_drag.Anchor.X):new(old.Y-_drag.Anchor.Y,_drag.Anchor.X);
        }
        RefreshDragPointer();_drag.Preview?.QueueRedraw();
    }
    public override bool _CanDropData(Vector2 atPosition,Variant data)
    {
        if(!Editable)return false;
        if(OwnsPayload(data)){UseNativePointerFallback(atPosition);return _drag!.PreviewValid;}
        return data.VariantType==Variant.Type.String||data.VariantType==Variant.Type.Dictionary&&data.AsGodotDictionary().ContainsKey("placement_id");
    }
    public override void _DropData(Vector2 atPosition,Variant data)
    {
        if(!Editable)return;
        if(OwnsPayload(data))
        {
            UseNativePointerFallback(atPosition);if(_drag?.PreviewValid!=true||_drag.Cancelled)return;
            if(_drag.Combination is not null){_drag.Committed=CombinationCommitted?.Invoke(_drag.Candidates())==true;return;}
            var candidate=_drag.Candidate.CopyConfiguration();
            if(PlacementCommitted is not null)_drag.Committed=PlacementCommitted(candidate);
            else {DropRequested?.Invoke(_drag.FromBoard?candidate.placement_id:"source:"+candidate.source_key,candidate.cell);_drag.Committed=true;}
            return;
        }
        // 兼容旧调用方的数据形状，真实交互统一使用上面的完整候选事务。
        var cell=CellAt(atPosition);
        if(data.VariantType==Variant.Type.String){DropRequested?.Invoke(data.AsString(),cell);return;}
        if(data.VariantType!=Variant.Type.Dictionary||Layout is null||Catalog is null)return;
        var entry=data.AsGodotDictionary();if(!entry.ContainsKey("placement_id"))return;
        var id=entry["placement_id"].AsString();
        var offset=entry.TryGetValue("grab_cell",out var value)?value.AsVector2I():Vector2I.Zero;
        var item=Layout.placements.FirstOrDefault(item=>item.placement_id==id);if(item is null)return;
        var shape=CombatBoardGeometry.Footprint(item,Catalog.FindSkill);
        offset=new(Math.Clamp(offset.X,0,shape.X-1),Math.Clamp(offset.Y,0,shape.Y-1));DropRequested?.Invoke(id,cell-offset);
    }
    private void FinishDrag()
    {
        if(_drag is null)return;
        var drag=_drag;RefreshDragPointer();
        var remove=Editable&&drag.FromBoard&&!drag.Committed&&!drag.Cancelled&&!drag.InsideBoard&&!drag.Candidate.locked
            &&drag.ReleaseReceived;
        _drag=null;HoverInfoManager.SetDragInProgress(this,false);ClearBoardHover();
        if(remove)PlacementRemoved?.Invoke(drag.Candidate.placement_id);
        QueueRedraw();DragEnded?.Invoke();
    }
    public override string _GetTooltip(Vector2 atPosition)=>"";
}

public partial class BattleBoardSource : Control
{
    public string SourceKey="";
    public BattleBoardView? Board;
    public Action? Picked;
    public override string _GetTooltip(Vector2 atPosition)=>"";
    public override void _Ready()=>MouseDefaultCursorShape=CursorShape.Drag;
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseButton { Pressed:true,ButtonIndex:MouseButton.Left }){Picked?.Invoke();AcceptEvent();}
    }
    public override Variant _GetDragData(Vector2 atPosition)
    {
        HoverInfoManager.HideFor(this);if(Board is null)return default;
        Vector2? anchor=null;
        var sample=GetChildren().OfType<BattleBoardView>().FirstOrDefault();
        if(sample?.Layout is not null)
        {
            var local=atPosition-sample.Position;
            if(new Rect2(Vector2.Zero,(Vector2)sample.Layout.size*sample.CellSize).HasPoint(local))anchor=local/sample.CellSize;
        }
        return Board.BeginSourceDrag(this,SourceKey,anchor);
    }
}
