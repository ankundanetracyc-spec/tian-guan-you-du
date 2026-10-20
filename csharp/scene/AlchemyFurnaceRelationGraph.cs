using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>药材批次作为节点，药性功用作为有向连线；线的维持占用灵识。</summary>
public partial class AlchemyFurnaceRelationGraph : Control
{
    private const float BaseWidth = 1800f;
    private const float BaseHeight = 780f;
    public Func<Vector2>? FurnaceMouthGlobal;
    public float ReservedLeft;
    private Vector2 GraphCenter => FurnaceMouthGlobal is null?new Vector2(900,330):ToDesignPoint(FurnaceMouthGlobal()-GlobalPosition);
    private static readonly (string Id, string Brief)[] UtilityLabels =
    [
        ("破", "开"), ("化", "炼"), ("通", "连"), ("行", "推"),
        ("聚", "汇"), ("引", "导"), ("固", "定"), ("镇", "抑"),
        ("护", "护"), ("生", "修"), ("明", "辨"), ("藏", "封")
    ];

    private readonly Dictionary<string,ImageTexture> _art=[];
    private Texture2D Art(string name)
    {
        if(_art.TryGetValue(name,out var cached))return cached;
        using var image=new Image();image.LoadSvgFromString(Godot.FileAccess.GetFileAsString("res://textures/ui/alchemy_nodes_20261018/"+name+".svg"));
        var texture=ImageTexture.CreateFromImage(image);_art[name]=texture;return texture;
    }
    public override void _ExitTree(){foreach(var texture in _art.Values)texture.Dispose();_art.Clear();}
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphEdge> _edges = [];
    private readonly List<(string Id, int Grade, Rect2 Rect)> _abilityHits = [];
    private HashSet<int> _closedLoopDoseIds = [];
    private AlchemyPreviewModel? _model;
    private int? _selectedSourceId;
    private int? _pinnedInfoId;
    public int? DisplayedInfoNodeId => _pinnedInfoId;
    private PanelContainer _infoCard=null!;
    private TextureRect _infoIcon=null!;
    private Label _infoTitle=null!,_infoMode=null!,_infoBody=null!,_infoHint=null!;
    public Func<AlchemyPreviewModel.Hand,string>? BasicAction;
    private Vector2 _wirePointer;
    private bool _havePointer;
    private string _infoFeedback="";
    private int _feedbackBreath;
    private HBoxContainer _infoUtilities=null!;
    private string _infoUtilityKey="";
    public bool NodeHoverCardVisible => _infoCard?.Visible==true;
    private string _selectedUtility = "";
    private int? _guideSource,_guideTarget;
    private string _guideUtility="";
    public int? SelectedSourceId=>_selectedSourceId;
    public string SelectedUtility=>_selectedUtility;
    public void GuideWiring(int? source=null,int? target=null,string utility="")
    { _guideSource=source;_guideTarget=target;_guideUtility=utility;QueueRedraw(); }
    private string _selectedLinkKey = "";
    private int _hoverEdge = -1;
    private int? _hoverDoseId;
    private string _hoverUtility = "";
    private string _interactionHint = "左键选药材与功用预设线路；原料节点无需先炼出药性。右键收起节点网。";
    private float _opacity;
    private float _progress;
    private float _transitionFrom;
    private float _opacityFrom;
    private float _transitionTime;
    private float _transitionDuration;
    private float _motionTime;
    private float _releaseTime;
    private float _drawScale = 1f;
    private bool _wantOpen;
    private readonly Dictionary<int, Vector2> _positions = [];
    private int? _pressedNode;
    private Vector2 _pressPoint, _dragOffset;
    private bool _dragging;
    public event Action<bool>? OpenChanged;
    public string[] VisibleUtilityIds=>_edges.Select(e=>e.Link.UtilityId).ToArray();
    public bool HasVisibleNode(int id)=>_nodes.Any(n=>n.DoseId==id);
    public Vector2 NodePosition(int id) => _nodes.First(n => n.DoseId == id).Point;
    public Vector2 NodeLocalPosition(int id)
    {
        var scale = Mathf.Min(Size.X / BaseWidth, Size.Y / BaseHeight);
        return (Size-new Vector2(BaseWidth,BaseHeight)*scale)*.5f + NodePosition(id)*scale;
    }
    public Vector2 LinkLocalPosition(AlchemyPreviewModel.SignedLink link)
    {
        var edge=_edges.First(e=>ReferenceEquals(e.Link,link));
        float scale=Mathf.Min(Size.X/BaseWidth,Size.Y/BaseHeight);
        return (Size-new Vector2(BaseWidth,BaseHeight)*scale)*.5f+EdgePath(edge).Points[12]*scale;
    }
    public void ResetLayout() { _positions.Clear(); Rebuild(); QueueRedraw(); }

    private float InkVisibility => _opacity * Mathf.SmoothStep(0.08f, 0.38f, Mathf.Clamp(_progress, 0f, 1f));

    public event Action? GraphChanged;
    public Func<Vector2,Variant,bool>? CanDropMaterial;
    public Action<Vector2,Variant>? DropMaterial;
    public override bool _CanDropData(Vector2 position,Variant data)=>CanDropMaterial?.Invoke(position,data)??false;
    public override void _DropData(Vector2 position,Variant data)=>DropMaterial?.Invoke(position,data);

    private sealed class GraphNode(string id, string title, string caption, string functions, Vector2 point, int doseId, double available, bool ready, Texture2D? icon = null, string legacyTexture = "", double open = 0, bool isPillCore = false)
    {
        public string Id = id;
        public string Title = title;
        public string Caption = caption;
        public string Functions = functions;
        public Vector2 Point = point;
        public int DoseId = doseId;
        public double Available = available;
        public bool Ready = ready;
        public Texture2D? Icon = icon;
        public string LegacyTexture = legacyTexture;
        public double Open = open;
        public bool IsPillCore = isPillCore;
    }

    private sealed class GraphEdge(GraphNode source, GraphNode target, AlchemyPreviewModel.SignedLink link)
    {
        public GraphNode Source = source;
        public GraphNode Target = target;
        public AlchemyPreviewModel.SignedLink Link = link;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        BuildInfoCard();
        MouseExited+=()=>{_hoverDoseId=null;};
        SetProcess(false);
        Resized += ()=>{Rebuild();QueueRedraw();};
    }

    private void BuildInfoCard()
    {
        _infoCard=new PanelContainer{Visible=false,ZIndex=30,MouseFilter=MouseFilterEnum.Ignore,CustomMinimumSize=new Vector2(365,0)};
        _infoCard.AddThemeStyleboxOverride("panel",QuietRoomStyle.Paper(.98f,14));AddChild(_infoCard);
        var frame=new InventoryPaperFrame{Dark=false,MouseFilter=MouseFilterEnum.Ignore};_infoCard.AddChild(frame);frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var inset=new MarginContainer{MouseFilter=MouseFilterEnum.Ignore};foreach(var side in new[]{"left","right","top","bottom"})inset.AddThemeConstantOverride("margin_"+side,16);_infoCard.AddChild(inset);
        var column=new VBoxContainer{MouseFilter=MouseFilterEnum.Ignore};column.AddThemeConstantOverride("separation",8);inset.AddChild(column);
        var header=new HBoxContainer{MouseFilter=MouseFilterEnum.Ignore};header.AddThemeConstantOverride("separation",12);column.AddChild(header);
        _infoIcon=new TextureRect{CustomMinimumSize=new Vector2(48,48),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore};header.AddChild(_infoIcon);
        Label Text(string value,int size,string color){var label=new Label{Text=value,MouseFilter=MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",new Color(color));return label;}
        _infoTitle=Text("",22,"304c43");header.AddChild(_infoTitle);
        _infoMode=Text("",14,"52665c");column.AddChild(_infoMode);
        column.AddChild(new ColorRect{Color=new Color("657b65"),CustomMinimumSize=new Vector2(0,1),MouseFilter=MouseFilterEnum.Ignore});
        _infoBody=Text("",17,"354d42");column.AddChild(_infoBody);
        _infoHint=Text("",14,"855346");_infoHint.AutowrapMode=TextServer.AutowrapMode.WordSmart;_infoHint.CustomMinimumSize=new Vector2(330,0);column.AddChild(_infoHint);
        _infoUtilities=new HBoxContainer{MouseFilter=MouseFilterEnum.Ignore};_infoUtilities.AddThemeConstantOverride("separation",8);column.AddChild(_infoUtilities);
        var note=Text("药性占比＝可用药性÷当前节点物料总量。\n原料、残渣和工艺绑定也计入总量。",13,"637365");column.AddChild(note);
    }

    private void UpdateInfoCard()
    {
        if(_infoCard is null)return;
        var info=DisplayedInfoNodeId is {} id?_nodes.FirstOrDefault(n=>n.DoseId==id):null;
        _infoCard.Visible=_wantOpen&&info is not null&&!_dragging;
        if(!_infoCard.Visible||info is null)return;
        _infoTitle.Text=info.Title;_infoIcon.Texture=info.Icon;_infoIcon.Visible=info.Icon is not null;
        _infoMode.Text=_model!.AutoRunStarted?"自动跑炉中 · 节点网已锁定":info.DoseId==0?"丹炉口 · 点击查看汇合药性":"原料可预先牵出功用线 · 再点收起详情";
        if(_model!.Breath!=_feedbackBreath)_infoFeedback="";
        _infoHint.Text=_infoFeedback.Length>0?_infoFeedback:_selectedUtility.Length>0?(_selectedUtility=="行"?"已选行 · 点击一条已布置的通路线，开炉后增流":"已选"+_selectedUtility+" · 点击目标节点接线"):"";
        var dose=_model!.InFurnace.FirstOrDefault(d=>d.Id==info.DoseId);
        var key=$"{info.DoseId}:{_selectedSourceId}:{_selectedUtility}:{_model.NodeAvailable(info.DoseId)>.01}:{_model.NodeSlag(0)>1e-6}:{_model.PendingOperations.Count}:{_model.Pills.Count}:{_model.Done}:{_model.Heat<=1}";
        if(key!=_infoUtilityKey)
        {
            _infoUtilityKey=key;
            foreach(var child in _infoUtilities.GetChildren()){_infoUtilities.RemoveChild(child);child.QueueFree();}
            if(dose is not null)foreach(var utility in GetUtilities(dose))
            {
                bool hasMaterial=dose.Raw>.01||_model.NodeAvailable(dose.Id)>.01;
                var button=new Button{Name="NodeUtility_"+utility.Id,Text=utility.Id+" · "+MaterialCraftData.utility_grade_name(utility.Grade),CustomMinimumSize=new Vector2(88,34),Disabled=!_model.CanPrepare||!hasMaterial};
                button.AddThemeFontSizeOverride("font_size",16);_infoUtilities.AddChild(button);
                bool selected=_selectedSourceId==dose.Id&&_selectedUtility==utility.Id;
                button.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=new Color(selected?"52725c":"d5dfc9"),ContentMarginLeft=10,ContentMarginRight=10});
                button.AddThemeColorOverride("font_color",new Color(selected?"f0ead9":"304a38"));
                button.Pressed+=()=>{_infoFeedback="";_selectedSourceId=dose.Id;_selectedUtility=selected?"":utility.Id;_interactionHint=_selectedUtility=="行"?"选择一条已布置的通路线，开炉后自动供力。":"已选“"+_selectedUtility+"”，点击目标节点。";UpdateInfoCard();QueueRedraw();};
                button.GuiInput+=e=>{if(e is InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Right})SetOpen(false);};
            }
            if(info.DoseId==0)
            {
                void Basic(string title,AlchemyPreviewModel.Hand kind,bool unavailable)
                {
                    bool planned=_model.PendingOperations.Any(o=>o.Kind==kind&&o.TargetId==0);
                    var button=new Button{Name="Furnace_"+kind,Text=planned?"已安排"+title:title,CustomMinimumSize=new Vector2(91,36),Disabled=_model.Done||unavailable};
                    button.AddThemeFontSizeOverride("font_size",16);
                    button.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=new Color("d5dfc9"),ContentMarginLeft=10,ContentMarginRight=10});
                    button.AddThemeStyleboxOverride("hover",new StyleBoxFlat{BgColor=new Color("eee8d5")});
                    button.AddThemeStyleboxOverride("disabled",new StyleBoxFlat{BgColor=new Color("d9d5c6"),ContentMarginLeft=10,ContentMarginRight=10});
                    button.AddThemeColorOverride("font_color",new Color("304a38"));button.AddThemeColorOverride("font_hover_color",new Color("304a38"));button.AddThemeColorOverride("font_disabled_color",new Color("879680"));
                    _infoUtilities.AddChild(button);
                    button.Pressed+=()=>{_interactionHint=BasicAction?.Invoke(kind)??"";_infoFeedback=_interactionHint;_feedbackBreath=_model.Breath;UpdateInfoCard();};
                }
                Basic("排渣",AlchemyPreviewModel.Hand.Purge,_model.NodeSlag(0)<=1e-6);
                Basic("凝丹",AlchemyPreviewModel.Hand.Gather,_model.Pills.Count==0&&_model.NodeAvailable(0)<1e-6);
                Basic("出炉",AlchemyPreviewModel.Hand.Output,_model.Heat>1||_model.Pills.Count==0);
            }
        }
        _infoBody.Text=string.Join("\n",NodeInfoLines(info.DoseId));
        _infoCard.ResetSize();
        var scale=Mathf.Min(Size.X/BaseWidth,Size.Y/BaseHeight);
        var origin=(Size-new Vector2(BaseWidth,BaseHeight)*scale)*.5f;
        var point=origin+AnimatedPoint(info)*scale;
        var cardSize=_infoCard.GetCombinedMinimumSize();
        float x=point.X+(info.IsPillCore?105:65)*scale;
        if(x+cardSize.X>origin.X+BaseWidth*scale)x=point.X-65*scale-cardSize.X;
        _infoCard.Position=new Vector2(Mathf.Clamp(x,4,Mathf.Max(4,Size.X-cardSize.X-4)),Mathf.Clamp(point.Y-40*scale,4,Mathf.Max(4,Size.Y-cardSize.Y-4)));
    }

    public void SetModel(AlchemyPreviewModel model)
    {
        if (!ReferenceEquals(_model, model)) { _positions.Clear(); _infoFeedback=""; _infoUtilityKey=""; _pinnedInfoId=null; _hoverDoseId=null; _pressedNode=null; _dragging=false; _selectedSourceId=null; _selectedUtility=""; GuideWiring(); }
        _model = model;
        Rebuild();
        QueueRedraw();
    }
    public void FocusTeachingSource(int id,string utility)
    {
        _selectedSourceId=id;_selectedUtility=utility;
        _interactionHint="本课起点与功用已选中；左侧按步骤观察，或自己接手选目标。";
        QueueRedraw();
    }

    public void SetOpen(bool open)
    {
        if (_wantOpen == open) return;
        _wantOpen = open;
        _pressedNode=null; _dragging=false;
        if(!open)_infoCard.Hide();
        OpenChanged?.Invoke(open);
        if (open)
        {
            Visible = true;
            _releaseTime = 0;
        }
        _transitionFrom = _progress;
        _opacityFrom = _opacity;
        _transitionTime = 0;
        _transitionDuration = open ? 0.58f : 0.28f;
        SetProcess(true);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventMouseMotion motion)
        {
            var point=ToDesignPoint(motion.Position);_wirePointer=point;_havePointer=true;
            if (_pressedNode is {} id)
            {
                if (!_dragging && motion.Position.DistanceTo(_pressPoint)>6) _dragging=true;
                if (_dragging)
                {
                    var node=_nodes.FirstOrDefault(n=>n.DoseId==id);
                    if(node is not null)
                    {
                        node.Point=new Vector2(Mathf.Clamp(point.X+_dragOffset.X,Mathf.Max(65,ReservedLeft+65),BaseWidth-65),Mathf.Clamp(point.Y+_dragOffset.Y,65,BaseHeight-65));
                        _positions[id]=node.Point;
                        UpdateRouteTargets();
                        _interactionHint="位置已保留；拖动不推进息数，也不占用灵识。";
                    }
                    QueueRedraw(); AcceptEvent(); return;
                }
            }
            UpdatePointer(motion.Position); return;
        }
        if (@event is not InputEventMouseButton button) return;
        if (button.ButtonIndex==MouseButton.Right && button.Pressed)
        { SetOpen(false); AcceptEvent(); return; }
        if(button.ButtonIndex!=MouseButton.Left)return;
        var design=ToDesignPoint(button.Position);
        if(button.Pressed)
        {
            _pressPoint=button.Position;_pressedNode=FindDoseAt(design);if(_pressedNode==0)_pressedNode=null;_dragging=false;
            if(_pressedNode is {} id)_dragOffset=NodePosition(id)-design;
        }
        else
        {
            if(!_dragging)HandleClick(design);
            _pressedNode=null;_dragging=false;
        }
        AcceptEvent();
    }

    private void UpdateRouteTargets()
    {
        foreach(var edge in _edges.Where(e=>e.Link.RouteTarget is not null))
        {
            var route=edge.Link.RouteTarget!;
            var a=_nodes.FirstOrDefault(n=>n.DoseId==route.SourceDoseId);
            var b=_nodes.FirstOrDefault(n=>n.DoseId==route.TargetDoseId);
            if(a is not null && b is not null)edge.Target.Point=(a.Point+b.Point)/2;
        }
    }

    private Vector2 ToDesignPoint(Vector2 localPosition)
    {
        var scale = Mathf.Min(Size.X / BaseWidth, Size.Y / BaseHeight);
        if (scale <= 0.001f) return Vector2.Zero;
        var origin = (Size - new Vector2(BaseWidth, BaseHeight) * scale) * 0.5f;
        return (localPosition - origin) / scale;
    }

    private void UpdatePointer(Vector2 localPosition)
    {
        var point = ToDesignPoint(localPosition);
        UpdateAbilityHitBoxes();
        var edge = FindEdgeAt(point);
        var node = FindDoseAt(point);
        var utility = FindUtilityAt(point);
        if (edge == _hoverEdge && node == _hoverDoseId && utility == _hoverUtility) return;
        MouseDefaultCursorShape=node is not null?CursorShape.Drag:CursorShape.PointingHand;
        _hoverEdge = edge;
        _hoverDoseId = node;
        _hoverUtility = utility;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _transitionTime += (float)delta;
        _motionTime += (float)delta;
        if (_wantOpen) _releaseTime += (float)delta;
        var t = Mathf.Clamp(_transitionTime / Mathf.Max(_transitionDuration, 0.001f), 0f, 1f);
        if (_wantOpen)
        {
            _progress = Mathf.Lerp(_transitionFrom, 1f, SpringOut(t));
            _opacity = Mathf.Lerp(_opacityFrom, 1f, EaseOut(t));
        }
        else
        {
            var close = EaseInOut(t);
            _progress = Mathf.Lerp(_transitionFrom, 0f, close);
            _opacity = Mathf.Lerp(_opacityFrom, 0f, close);
        }

        if (_transitionTime >= _transitionDuration)
        {
            _progress = _wantOpen ? 1f : 0f;
            _opacity = _wantOpen ? 1f : 0f;
            if (!_wantOpen)
            {
                Visible = false;
                SetProcess(false);
            }
        }
        UpdateInfoCard();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_model is null || _opacity <= 0.001f) return;
        var scale = Mathf.Min(Size.X / BaseWidth, Size.Y / BaseHeight);
        if (scale <= 0.001f) return;
        _drawScale = scale;
        var origin = (Size - new Vector2(BaseWidth, BaseHeight) * scale) * 0.5f;
        DrawSetTransform(origin, 0, Vector2.One * scale);
        var font = GetThemeFont("font", "Label");


        foreach (var edge in _edges)
            DrawEdge(font, edge, LinkKey(edge.Link) == _selectedLinkKey || _edges.IndexOf(edge) == _hoverEdge);
        if(_havePointer&&_selectedSourceId is {} selectedId&&_selectedUtility.Length>0&&_nodes.FirstOrDefault(n=>n.DoseId==selectedId) is {} selectedNode)
        {
            var from=AnimatedPoint(selectedNode);var direction=(_wirePointer-from).Normalized();
            DrawDashedLine(from+direction*55,_wirePointer,Tint("496d56",.8f),2,10,true);
        }
        foreach (var node in _nodes) DrawMaterialNode(font, node);
        if(_guideSource is {} guideSource)
        {
            bool choosingTarget=_selectedSourceId==guideSource&&_selectedUtility==_guideUtility;
            var guideNode=_nodes.FirstOrDefault(n=>n.DoseId==(choosingTarget?_guideTarget:guideSource));
            if(guideNode is not null)
            {
                var point=AnimatedPoint(guideNode);
                if(choosingTarget&&_guideUtility=="行")
                {
                    var route=_edges.FirstOrDefault(e=>e.Link.UtilityId=="通"&&e.Target.DoseId==_guideTarget);
                    if(route is not null){DrawEdge(font,route,true);point=EdgePath(route).Points[12];}
                }
                else DrawArc(point,guideNode.IsPillCore?79:65,0,Mathf.Tau,64,Tint("ac6851",.65f+.2f*Mathf.Sin(_motionTime*3)),2,true);
                DrawText(font,point+new Vector2(0,-78),choosingTarget?(_guideUtility=="行"?"③ 点已布置的通路线":"③ 点这里接线"):"① 点药材，选择功用",15,Tint("85513e",1));
            }
        }
        DrawReleaseRings();
        DrawSetTransform(Vector2.Zero);
    }

    private void Rebuild()
    {
        _nodes.Clear();
        _edges.Clear();
        if (_model is null) return;

        var live=_model.LiveNodeIds();
        var doses = _model.InFurnace.Where(d=>live.Contains(d.Id)).ToArray();
        if(_pinnedInfoId is {} pin&&pin!=0&&!live.Contains(pin)){_pinnedInfoId=null;_infoUtilityKey="";_infoCard.Hide();}
        _closedLoopDoseIds = _model.ClosedLoopDoseIds();
        var positions = MaterialPositions(doses.Length);
        for (var i = 0; i < doses.Length; i++)
        {
            var dose = doses[i];
            var functions = dose.Herb.Craft.utilities
                .Where(u => u is not null && !string.IsNullOrWhiteSpace(u.utility_id))
                .Select(u => (u.utility_id, u.grade))
                .DistinctBy(u => u.utility_id)
                .ToArray();
            var functionText = string.Join(" · ", functions.Select(u => $"{u.utility_id}{MaterialCraftData.utility_grade_name(u.grade)[0]}"));
            var materialRole = dose.Kind == "主药" ? "主" : "辅";
            bool hasStoneShell = dose.Herb.Processing.Dense;
            var caption = hasStoneShell
                ? dose.Open >= .98 ? $"{materialRole} · 结构已开" : $"{materialRole} · 开结构{dose.Open:P0}"
                : $"{materialRole} · {dose.Herb.Craft.texture}";
            var icon = GD.Load<Texture2D>(dose.Herb.Item.icon_path);
            if(!_positions.ContainsKey(dose.Id))_positions[dose.Id]=positions[i];
            _nodes.Add(new GraphNode($"dose:{dose.Id}", dose.Herb.Name, caption, functionText, _positions.GetValueOrDefault(dose.Id,positions[i]), dose.Id,
                _model.NodeAvailable(dose.Id), _model.NodeAvailable(dose.Id) > 0.01, icon, dose.Herb.Processing.Dense?"致密":dose.Herb.Craft.texture, dose.Open));
        }

        _nodes.Add(new GraphNode("pill-core", _model.Pills.Count>0?_model.CurrentPill.Name:_model.NodeName(0), $"药性{_model.NodeAvailable(0):F1} · 渣{_model.NodeSlag(0):F1}", "中心汇", GraphCenter, 0, _model.NodeAvailable(0), true, null, "丹胚", 1, true));

        foreach (var link in _model.SignedLinks)
        {
            if(!AlchemyPreviewModel.Purposes.ContainsKey(link.UtilityId))continue;
            var source = _nodes.FirstOrDefault(n => n.DoseId == link.SourceDoseId);
            var target = _nodes.FirstOrDefault(n => n.DoseId == link.TargetDoseId);
            if(link.UtilityId=="行"&&link.RouteTarget is {} route)
            {
                var a=_nodes.FirstOrDefault(n=>n.DoseId==route.SourceDoseId);var b=_nodes.FirstOrDefault(n=>n.DoseId==route.TargetDoseId);
                if(a is not null&&b is not null)target=new GraphNode("route", "通路", "", "", (a.Point+b.Point)/2, -1, 0, true);
            }
            if (source is null || target is null) continue;
            _edges.Add(new GraphEdge(source, target, link));
        }

        if (_selectedSourceId is not null && doses.All(d => d.Id != _selectedSourceId)) _selectedSourceId = null;
        if (_selectedLinkKey.Length > 0 && _model.SignedLinks.All(link => LinkKey(link) != _selectedLinkKey)) _selectedLinkKey = "";
        _hoverEdge = -1;
        UpdateAbilityHitBoxes();
    }

    private Vector2[] MaterialPositions(int count)
    {
        var positions=new Vector2[count];
        var center=GraphCenter+new Vector2(ReservedLeft>0?150:0,65);
        float radiusX=ReservedLeft>0?550:690;
        float radiusY=count<=2?180:270;
        for(int i=0;i<count;i++)
        {
            float angle=count==2?-Mathf.Pi*.78f+i*Mathf.Pi*.56f:-Mathf.Pi*.5f+Mathf.Tau*i/Math.Max(1,count);
            var point=center+new Vector2(Mathf.Cos(angle)*radiusX,Mathf.Sin(angle)*radiusY);
            positions[i]=new Vector2(Mathf.Clamp(point.X,Mathf.Max(75,ReservedLeft+70),BaseWidth-75),Mathf.Clamp(point.Y,85,BaseHeight-80));
        }
        return positions;
    }

    public double NodeDrugRatio(int id)
    {
        if(_model is null||_model.NodeMass(id)<=1e-9)return 0;
        return Math.Clamp(_model.NodeAvailable(id)/_model.NodeMass(id),0,1);
    }
    public string[] NodeInfoLines(int id)
    {
        if(_model is null)return [];
        if(id==0&&_model.Pills.Count>0)return [
            $"丹团 {_model.Pills.Count}颗 · 稳形 {_model.Shape:P0}",
            $"当前丹效 {_model.Pills.Average(p=>_model.Done?p.Score:_model.ProjectedQuality(p)):F0}分",
            _model.Done?string.Join("、",_model.Pills.Select(p=>p.EffectText)):"尚未出炉，可继续稳形。"];
        var dose=_model.InFurnace.FirstOrDefault(d=>d.Id==id);
        var packets=_model.Inventory.Where(p=>p.Location==id&&p.Amount>0).ToArray();
        double available=packets.Sum(p=>p.Amount);
        double bias=available<=1e-9?0:packets.Sum(p=>p.Amount*Math.Sign(p.Source.Herb.Craft.polarity))/available;
        string polarity=Math.Abs(bias)<.005?"平衡":(bias<0?"偏阴":"偏阳")+$"{Math.Abs(bias):P0}";
        double total=_model.NodeMass(id);
        double Portion(double amount)=>total<=1e-9?0:Math.Clamp(amount/total,0,1);
        if(dose is null)return [
            $"药性占比 {NodeDrugRatio(id):P1}",
            $"残渣占比 {Portion(_model.NodeSlag(id)):P1}",
            $"混匀 {_model.NodeMixed(id):P0} · 稳定 {_model.HubStability:P0}",
            $"局部躁动 {_model.NodeAgitation(id):F0}%",
            "药团阴阳："+polarity,
            available>1e-6?"药性已入炉，可排渣、收拢凝丹。":"药性尚未送入，先接通药路。"];
        var functions=_nodes.FirstOrDefault(n=>n.DoseId==id)?.Functions??"";
        return [
            $"药性占比 {NodeDrugRatio(id):P1}",
            $"原料 {Portion(dose.Raw):P1} · 残渣 {Portion(_model.NodeSlag(id)):P1}",
            $"{dose.Kind} · {dose.Herb.Craft.texture} · 开启{dose.Open:P0}",
            $"混匀 {_model.NodeMixed(id):P0} · 显露 {dose.Diagnosis:F0}%",
            "功用："+functions,
            $"原材五行：{(string.IsNullOrEmpty(dose.Herb.Craft.element)?"无行":dose.Herb.Craft.element)} · {(dose.Herb.Craft.polarity<0?"阴":dose.Herb.Craft.polarity>0?"阳":"和")}",
            $"药团{polarity} · 躁动{_model.NodeAgitation(id):F0}%"];
    }

    private static List<(string Id, int Grade)> GetUtilities(AlchemyPreviewModel.Dose source)
    {
        return source.Herb.Craft.utilities
            .Where(u => u is not null && !string.IsNullOrWhiteSpace(u.utility_id) && AlchemyPreviewModel.Purposes.ContainsKey(u.utility_id))
            .GroupBy(u => u.utility_id)
            .Select(group => (group.Key, group.Max(u => u.grade)))
            .OrderBy(item => Array.FindIndex(UtilityLabels, label => label.Id == item.Key))
            .ToList();
    }

    private void UpdateAbilityHitBoxes() => _abilityHits.Clear();

    private void DrawEdge(Font font, GraphEdge edge, bool selected)
    {
        var path = EdgePath(edge);
        bool closedRoute = _model?.IsClosedLoopLink(edge.Link) ?? false;
        var color = selected ? Tint("9e5747", 1f) : closedRoute ? Tint("365f4f", 1f) : Tint("466f5d", 1f);
        DrawPolyline(path.Points, Tint("f5efdc", .92f), selected ? 7f : 5.4f, true);
        DrawPolyline(path.Points, color, selected ? 2.8f : 1.8f, true);
        DrawArrowHead(path.Points[^2], path.Points[^1], color, selected ? 9f : 7f);
        if (_wantOpen && _progress > .65f && !edge.Link.Paused && edge.Link.LastFlow>0)
        {
            var phase = Mathf.PosMod(_motionTime * .20f + _edges.IndexOf(edge) * .31f, 1f);
            var point = path.Points[(int)(phase * (path.Points.Length - 1))];
            DrawCircle(point, selected ? 3.1f : 2.2f, Tint("d6e6dc", .94f));
        }
        var mid = path.Points[(int)((path.Points.Length-1)*(.35+.12*(_edges.IndexOf(edge)%3)))];
        var text = edge.Link.UtilityId+(edge.Link.Paused?"·停":edge.Link.Status.Contains("轮候",StringComparison.Ordinal)?"·候":"");
        var badgeWidth = Math.Max(43f, text.Length * 12f + 15f);
        DrawTextureRect(Art("line_label"),new Rect2(mid-new Vector2(badgeWidth*.5f,15),new Vector2(badgeWidth,30)),false,new Color(1,1,1,InkVisibility));

        DrawText(font, mid + new Vector2(0, 4), text, 11, color);
    }

    private (Vector2[] Points, Vector2 Start, Vector2 End) EdgePath(GraphEdge edge)
    {
        var fromCenter = AnimatedPoint(edge.Source);
        var toCenter = AnimatedPoint(edge.Target);
        var delta = toCenter - fromCenter;
        if (delta.LengthSquared() < .01f) delta = Vector2.Right;
        var direction = delta.Normalized();
        var start = fromCenter + direction * (edge.Source.IsPillCore ? 35f : 47f);
        float mouthRadius=1f/Mathf.Sqrt(direction.X*direction.X/(65*65)+direction.Y*direction.Y/(22*22));
        var end = toCenter - direction * (edge.Target.DoseId<0 ? 0f : edge.Target.IsPillCore ? mouthRadius : 47f);
        var normal = new Vector2(-direction.Y, direction.X);
        var parallels = _edges.Where(other => SamePair(other, edge)).ToList();
        var lane = (parallels.IndexOf(edge) - (parallels.Count - 1) * .5f) * 22f;
        var midpoint = (start + end) * .5f;
        var outward = (midpoint - GraphCenter).Normalized();
        var control = midpoint + outward * 22f + normal * lane;
        var points = new Vector2[25];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i / (float)(points.Length - 1);
            var inverse = 1f - t;
            points[i] = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }
        return (points, start, end);
    }

    private static bool SamePair(GraphEdge left, GraphEdge right)
    {
        return left.Source.Id == right.Source.Id && left.Target.Id == right.Target.Id
            || left.Source.Id == right.Target.Id && left.Target.Id == right.Source.Id;
    }

    private void DrawMaterialNode(Font font, GraphNode node)
    {
        if (node.IsPillCore)
        {
            DrawPillCoreNode(font, node);
            return;
        }
        var point = AnimatedPoint(node);
        var selected = node.DoseId == _selectedSourceId;
        var hovered = node.DoseId == _hoverDoseId;
        var targetable = _selectedSourceId is not null && _selectedUtility.Length > 0 && node.DoseId != _selectedSourceId
            && _selectedUtility != "行";
        bool stoneShell = node.LegacyTexture=="致密";
        DrawTextureRect(Art(selected?"selected_node":"material_node"),new Rect2(point-new Vector2(58,58),new Vector2(116,116)),false,new Color(1,1,1,InkVisibility));
        if(stoneShell)DrawStoneShell(point,node.Open);
        if(hovered||targetable)DrawArc(point,55,0,Mathf.Tau,64,Tint("6c947c",.7f),1.5f,true);
        if (node.Icon is not null)
        {
            var iconRect = new Rect2(point + new Vector2(-23, -35), new Vector2(46, 46));
            DrawTextureRect(node.Icon, iconRect, false, Tint(node.Ready ? "ffffff" : "9ba79f", node.Ready ? .98f : .58f));
        }
        else DrawCircle(point + new Vector2(0, -24), 7f, Tint("a4cbbb", .9f));
        var titleSize = node.Title.Length > 5 ? 14 : 16;
        DrawText(font, point + new Vector2(0, 22), node.Title, titleSize, Tint("2f493a", 1f));
        if (selected) DrawCircle(point + new Vector2(0, -48), 3f, Tint("e1ad99", 1f));
    }

    private void DrawStoneShell(Vector2 point, double open)
    {
        float intact = 1f - Mathf.Clamp((float)open, 0f, 1f);
        const int segments = 10;
        for (int i = 0; i < segments; i++)
        {
            if ((i + .5f) / segments > intact) continue;
            float start = Mathf.Tau * i / segments + .035f;
            float end = Mathf.Tau * (i + 1) / segments - .035f;
            DrawArc(point, 61f, start, end, 7, Tint("677465", .64f + intact * .24f), 2.5f, true);
        }
        float crackAngle = Mathf.Pi * (1.18f + (float)open * .42f);
        var radial = new Vector2(Mathf.Cos(crackAngle), Mathf.Sin(crackAngle));
        DrawLine(point + radial * 54f, point + radial * 62f + new Vector2(3, -2), Tint("596b5b", .63f * intact), 1.4f, true);
        DrawLine(point + radial * 59f + new Vector2(3, -2), point + radial * 65f + new Vector2(-1, -5), Tint("596b5b", .50f * intact), 1.2f, true);
    }

    private void DrawPillCoreNode(Font font, GraphNode node)
    {
        var point = AnimatedPoint(node);
        bool selected=_pinnedInfoId==0;
        bool targetable=_selectedSourceId is not null&&_selectedUtility is "通" or "聚" or "引" or "固" or "镇" or "护" or "生" or "明" or "藏";
        bool hasDrug=node.Available>1e-6;
        bool hasPills=_model!.Pills.Count>0;
        // 口沿上的扁平药雾，避免独立徽章遮住丹炉。
        DrawInkOval(point+new Vector2(0,5),74,22,Tint("e6ece1",hasDrug?.62f:.28f));
        DrawInkOval(point,62,17,Tint("547e76",hasDrug?.42f:.14f));
        var rim=new Vector2[49];
        for(int i=0;i<rim.Length;i++){float a=Mathf.Tau*i/(rim.Length-1);rim[i]=point+new Vector2(Mathf.Cos(a)*70,Mathf.Sin(a)*21);}
        DrawPolyline(rim,Tint(selected?"a36450":"678b7c",selected||targetable?.95f:.55f),selected||targetable?2:1.1f,true);
        if(hasDrug||hasPills)
        {
            for(int strand=0;strand<3;strand++)
            {
                var mist=new Vector2[19];
                for(int i=0;i<mist.Length;i++){float t=i/(float)(mist.Length-1);mist[i]=point+new Vector2((strand-1)*25+Mathf.Sin(t*5+_motionTime*.65f+strand)*9*t,-6-t*42);}
                DrawPolyline(mist,Tint("688d82",.22f),2,true);
            }
            if(hasPills)
            {
                int count=Math.Min(5,_model.Pills.Count);
                for(int i=0;i<count;i++)
                {
                    var pill=point+new Vector2((i-(count-1)*.5f)*19,-7+Mathf.Sin(_motionTime*.8f+i)*1.5f);
                    DrawCircle(pill,9,Tint("3f6b60",.95f));DrawCircle(pill+new Vector2(-1,-2),6,Tint("a2bfa3",1));DrawCircle(pill+new Vector2(-3,-4),2,Tint("e4edcf",.9f));
                }
            }
            else
            {
                DrawInkOval(point+new Vector2(0,-5),41,12,Tint("9cb9a0",.9f));
                DrawInkOval(point+new Vector2(-8,-8),24,5,Tint("d2dec0",.7f));
            }
        }
        var label=point+new Vector2(0,43);
        float width=Math.Max(74,node.Title.Length*16+20);
        DrawTextureRect(Art("line_label"),new Rect2(label-new Vector2(width/2,19),new Vector2(width,30)),false,new Color(1,1,1,InkVisibility*.92f));
        DrawText(font,label,node.Title,15,Tint("304c43",1));
    }

    private void DrawText(Font font, Vector2 center, string text, int size, Color color)
    {
        size = EffectiveTextSize(size);
        var width = text.Length * size;
        var start = center.X - width * .5f;
        var shadow = Tint("f3edde", .32f);
        DrawString(font, new Vector2(start - 1, center.Y + 1), text, HorizontalAlignment.Left, -1, size, shadow);
        DrawString(font, new Vector2(start + 1, center.Y + 1), text, HorizontalAlignment.Left, -1, size, shadow);
        DrawString(font, new Vector2(start, center.Y), text, HorizontalAlignment.Left, -1, size, color);
    }

    private int EffectiveTextSize(int size)
    {
        var minimumForScreen = Mathf.CeilToInt(12f / Mathf.Max(_drawScale, 0.25f));
        return Math.Max(size, minimumForScreen);
    }

    private void DrawInkOval(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int segments = 40;
        var points = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = Mathf.Tau * i / segments;
            points[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        DrawColoredPolygon(points, color);
    }

    private void DrawArrowHead(Vector2 from, Vector2 to, Color color, float size)
    {
        var direction = (to - from).Normalized();
        var basePoint = to - direction * size;
        var side = new Vector2(-direction.Y, direction.X) * size * .46f;
        DrawLine(basePoint - side, to, color, 1.6f, true);
        DrawLine(basePoint + side, to, color, 1.6f, true);
    }

    private void DrawReleaseRings()
    {
        if (!_wantOpen || _releaseTime > .72f) return;
        for (var i = 0; i < 2; i++)
        {
            var t = (_releaseTime - i * .12f) / .62f;
            if (t <= 0 || t >= 1) continue;
            var color = Tint("a4cbbb", (1f - t) * .20f);
            DrawArc(GraphCenter, 42f + t * 205f, 0, Mathf.Tau, 72, color, 1.1f, true);
        }
    }

    private int FindEdgeAt(Vector2 point)
    {
        var best = -1;
        var bestDistance = 13f;
        for (var i = 0; i < _edges.Count; i++)
        {
            var points = EdgePath(_edges[i]).Points;
            for (var p = 1; p < points.Length; p++)
            {
                var closest = Geometry2D.GetClosestPointToSegment(point, points[p - 1], points[p]);
                var distance = point.DistanceTo(closest);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private int? FindDoseAt(Vector2 point)
    {
        foreach (var node in _nodes)
            if (Mathf.Abs(point.X - AnimatedPoint(node).X) <= (node.IsPillCore ? 76f : 55f) && Mathf.Abs(point.Y - AnimatedPoint(node).Y) <= (node.IsPillCore ? 50f : 55f))
                return node.DoseId;
        return null;
    }

    private string FindUtilityAt(Vector2 point)
    {
        foreach (var option in _abilityHits)
            if (option.Rect.HasPoint(point)) return option.Id;
        return "";
    }

    private void ShowLineActions(AlchemyPreviewModel.SignedLink link)
    {
        if(_model is null)return;
        string target=link.RouteTarget is {} routeTarget?_model.NodeName(routeTarget.SourceDoseId)+"的通路":_model.NodeName(link.TargetDoseId);
        var dialog=new AcceptDialog{Title=link.UtilityId+" · "+_model.NodeName(link.SourceDoseId)+" → "+target,Size=new Vector2I(480,240)};AddChild(dialog);
        var box=new VBoxContainer();box.AddThemeConstantOverride("separation",12);dialog.AddChild(box);
        box.AddChild(new Label{Text=AlchemyPreviewModel.Purposes[link.UtilityId]+"。随炉令按需工作。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        box.AddChild(new Label{Text=link.Status,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Add(string title,Func<string> action){var button=new Button{Text=title};box.AddChild(button);button.Pressed+=()=>{_interactionHint=action();Rebuild();QueueRedraw();GraphChanged?.Invoke();dialog.QueueFree();};}
        if(_model.AutoRunStarted)
            box.AddChild(new Label{Text="线路已锁定；灵识不足的线路会自动轮候。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        else
            Add("拆除这条线",()=>_model.RemoveSignedLink(link));
        dialog.Confirmed+=dialog.QueueFree;dialog.Canceled+=dialog.QueueFree;dialog.PopupCentered();
    }
    private void CompleteWiring(int previousCount)
    {
        if(_model is null||_model.SignedLinks.Count<=previousCount)
        { _pinnedInfoId=_selectedSourceId;_infoFeedback=_interactionHint;_feedbackBreath=_model!.Breath;UpdateInfoCard();return; }
        _infoFeedback="";var link=_model.SignedLinks[^1];link.Repeat=false;
        _selectedLinkKey=LinkKey(link);_selectedUtility="";_selectedSourceId=null;_pinnedInfoId=null;_infoCard.Hide();
        Rebuild();GraphChanged?.Invoke();QueueRedraw();
    }
    private void HandleClick(Vector2 point)
    {
        if(_model is null)return;
        if(_model.AutoRunStarted&&_selectedUtility.Length>0)
        { _interactionHint="自动跑炉中，线路已锁定。";_selectedUtility="";_selectedSourceId=null;QueueRedraw();return; }
        var doseId=FindDoseAt(point);
        if(doseId is not null)
        {
            if(_selectedSourceId is {} source&&_selectedUtility.Length>0&&doseId!=source)
            {
                int count=_model.SignedLinks.Count;
                _interactionHint=_model.SignLink(source,doseId.Value,_selectedUtility);CompleteWiring(count);return;
            }
            _infoFeedback="";_pinnedInfoId=_pinnedInfoId==doseId?null:doseId;
            _selectedSourceId=doseId==0?null:(_pinnedInfoId is null?null:doseId);_selectedUtility="";
            UpdateInfoCard();QueueRedraw();return;
        }
        int edgeIndex=FindEdgeAt(point);
        if(edgeIndex>=0)
        {
            var edge=_edges[edgeIndex];
            if(_selectedUtility=="行"&&_selectedSourceId is {} provider&&edge.Link.UtilityId=="通")
            {int count=_model.SignedLinks.Count;_interactionHint=_model.SignFlowSupport(provider,edge.Link);CompleteWiring(count);}
            else ShowLineActions(edge.Link);
            return;
        }
        _pinnedInfoId=null;_selectedSourceId=null;_selectedUtility="";_infoCard.Hide();QueueRedraw();
    }

    private Vector2 AnimatedPoint(GraphNode node)
    {
        if (node.IsPillCore) return GraphCenter;
        var point = GraphCenter + (node.Point - GraphCenter) * _progress;
        var index = Math.Max(0, _nodes.IndexOf(node));
        var settled = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_progress, 0f, 1f));
        var phase = _motionTime * 1.10f + index * 1.73f;
        if(!_positions.ContainsKey(node.DoseId))point += new Vector2(Mathf.Sin(phase) * 1.05f, Mathf.Sin(phase * .8f + .7f) * .8f) * settled;
        return point;
    }

    private Color Tint(string html, float alpha)
    {
        var color = new Color(html);
        color.A = alpha * InkVisibility;
        return color;
    }

    private static string UtilityBrief(string id) => id switch
    {
        "破" => "开", "化" => "炼", "通" => "连", "行" => "推",
        "聚" => "汇", "引" => "导", "固" => "定", "镇" => "抑",
        "护" => "护", "生" => "修", "明" => "辨", "藏" => "封",
        _ => id
    };

    private static string LinkKey(AlchemyPreviewModel.SignedLink link) => $"{link.SourceDoseId}:{link.TargetDoseId}:{link.UtilityId}";
    private static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInOut(float t) => t < .5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    private static float SpringOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        const float damping = 6f;
        const float frequency = 9f;
        var value = 1f - Mathf.Exp(-damping * t) * (Mathf.Cos(frequency * t) + damping / frequency * Mathf.Sin(frequency * t));
        return Mathf.Clamp(value, 0f, 1.06f);
    }
}

