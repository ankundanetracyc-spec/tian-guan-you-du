using Godot;
using Godot.Collections;

public partial class PortraitRenderer : Control
{
    private static readonly string[] LayerOrder=["hair_back","face","ear","clothes","eye","brow","nose","mouth","beard","makeup","hair_front"];
    private static readonly string[] FacialLayers=["face","ear","eye","brow","nose","mouth","beard","makeup"];
    private static readonly Color[] SkinTones=[new("fff0df"),new("f4ddc8"),new("e5c4a7"),new("c99b79"),new("a77758")];
    private static readonly Color[] HairTones=[new("ffffff"),new("c8a77b"),new("8a6550"),new("89919a"),new("9b4b38")];
    private static readonly System.Collections.Generic.Dictionary<string,Dictionary> SelectionCache=[];
    private readonly System.Collections.Generic.Dictionary<string,TextureRect> _layers=[];
    private string _current_key="";
    private bool _headshotMode;
    private const float HeadshotZoom = 2.8f;
    private const float HeadshotFocusY = .268f;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;ensure_layers();}
    /// <summary>名单头像使用现有立绘的头部近景裁切；应在配置人物外观前调用。</summary>
    public void set_headshot_mode(bool enabled){_headshotMode=enabled;ClipContents=enabled;}
    public void show_character(GodotObject character)
    {
        var data=character.Get("entity_data").AsGodotObject() as EntityData;
        if(data is null){clear_portrait();return;}
        var catalog=GetNode<PortraitCatalog>("/root/PortraitCatalog");
        var gender=data is HumanData humanData&&humanData.gender==1?"female":"male";
        var context=catalog.get_context(data,gender,character.Get("current_age").AsInt32(),data.faction_name,character.Get("cultivation_realm").AsString());
        configure(data,context,catalog.get_requested_appearance(data));
    }
    public void show_world_npc(WorldNpcData npc)
    {
        if(npc is null){clear_portrait();return;}
        npc.ensure_appearance_identity();
        var data=new HumanData{gender=npc.gender,appearance_seed=npc.appearance_seed,faction_name=npc.faction_id};
        var gender=data.gender==1?"female":"male";
        var catalog=GetNode<PortraitCatalog>("/root/PortraitCatalog");
        var context=catalog.get_context(data,gender,npc.current_age,data.faction_name,npc.cultivation_realm);
        configure(data,context,catalog.get_requested_appearance(data));
    }
    public void configure(EntityData data,Dictionary context,Dictionary? requested=null)
    {
        ensure_layers();requested??=[];
        var catalog=GetNode<PortraitCatalog>("/root/PortraitCatalog");
        var key=catalog.make_cache_key(data,context,requested);
        if(!SelectionCache.TryGetValue(key,out var selected)){selected=catalog.select_portrait(context,requested);SelectionCache[key]=selected;}
        _current_key=key;
        foreach(var part in LayerOrder)
        {
            var layer=_layers[part];var entry=selected.GetValueOrDefault(part,new Dictionary()).AsGodotDictionary();
            var path=entry.GetValueOrDefault("resource_path","").AsString();
            layer.Texture=path.Length>0&&ResourceLoader.Exists(path)?ResourceLoader.Load<Texture2D>(path):null;
            layer.Visible=layer.Texture is not null;
            layer.Position=new Vector2(entry.GetValueOrDefault("offset_x",0).AsSingle(),entry.GetValueOrDefault("offset_y",0).AsSingle());
            layer.Scale=Vector2.One;layer.Modulate=Colors.White;
        }
        apply_appearance(data,selected);
        if(_headshotMode) apply_headshot_crop();
    }
    public void clear_portrait(){foreach(var layer in _layers.Values){layer.Texture=null;layer.Visible=false;}_current_key="";}
    public string get_cache_key()=>_current_key;
    public bool has_required_layers()=>_layers.TryGetValue("face",out var face)&&face.Texture is not null&&_layers.TryGetValue("clothes",out var clothes)&&clothes.Texture is not null;
    public static void clear_selection_cache()=>SelectionCache.Clear();
    private void ensure_layers(){if(_layers.Count>0)return;for(var i=0;i<LayerOrder.Length;i++){var l=new TextureRect{Name=LayerOrder[i].Capitalize(),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore,ZIndex=i};l.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(l);_layers[LayerOrder[i]]=l;}}
    private void apply_headshot_crop()
    {
        ClipContents = true;
        var cropSize = Size.X > 0f && Size.Y > 0f ? Size : CustomMinimumSize;
        var focus = new Vector2(cropSize.X * .5f, cropSize.Y * HeadshotFocusY);
        var centeringOffset = cropSize * .5f - focus;
        foreach (var layer in _layers.Values)
        {
            if (layer.Texture is null) continue;
            layer.PivotOffset = focus;
            layer.Scale *= new Vector2(HeadshotZoom, HeadshotZoom);
            layer.Position += centeringOffset;
        }
    }
    private void apply_appearance(EntityData data,Dictionary selected)
    {
        var faceScale=new Vector2(data.appearance_face_width,data.appearance_face_height);
        var faceEntry=selected.GetValueOrDefault("face",new Dictionary()).AsGodotDictionary();
        var rigPivots=faceEntry.GetValueOrDefault("scale_pivots_normalized",new Dictionary()).AsGodotDictionary();
        var facePivot=read_pivot(rigPivots,"face_and_feature_scale",new Vector2(.5f,.5f));
        var eyePivot=read_pivot(rigPivots,"eye_variant_scale",facePivot);
        foreach(var part in FacialLayers)
        {
            var layer=_layers[part];
            if(layer.Texture is null)continue;
            layer.PivotOffset=new Vector2(Size.X*facePivot.X,Size.Y*facePivot.Y);
            layer.Scale=faceScale;
        }
        var eye=_layers["eye"];
        if(eye.Texture is not null)
        {
            eye.PivotOffset=new Vector2(Size.X*eyePivot.X,Size.Y*eyePivot.Y);
            eye.Scale*=data.appearance_eye_scale;
            eye.Position+=Vector2.Down*data.appearance_eye_offset_y;
        }
        if(faceEntry.GetValueOrDefault("allow_skin_tone_tint",true).AsBool()&&data.appearance_skin_tone>=0&&data.appearance_skin_tone<SkinTones.Length&&_layers["face"].Texture is not null)
        {
            _layers["face"].Modulate=SkinTones[data.appearance_skin_tone];
            if(_layers["ear"].Texture is not null) _layers["ear"].Modulate=SkinTones[data.appearance_skin_tone];
        }
        var hairEntry=selected.GetValueOrDefault("hair_front",new Dictionary()).AsGodotDictionary();
        if(hairEntry.GetValueOrDefault("allow_hair_color_tint",true).AsBool()&&data.appearance_hair_color>=0&&data.appearance_hair_color<HairTones.Length)
        {
            _layers["hair_back"].Modulate=HairTones[data.appearance_hair_color];
            _layers["hair_front"].Modulate=HairTones[data.appearance_hair_color];
        }
    }
    private static Vector2 read_pivot(Dictionary pivots,string key,Vector2 fallback)
    {
        var value=pivots.GetValueOrDefault(key,new Dictionary()).AsGodotDictionary();
        if(value.Count==0)return fallback;
        return new Vector2(value.GetValueOrDefault("x",fallback.X).AsSingle(),value.GetValueOrDefault("y",fallback.Y).AsSingle());
    }
    private static int stable_seed(string value)
    {
        unchecked
        {
            uint hash=2166136261;
            foreach(char character in value){hash^=character;hash*=16777619;}
            var seed=(int)(hash&0x7fffffff);
            return seed==0?1:seed;
        }
    }
}
