using Godot;

/// <summary>地图和战斗共用的纸娃娃棋子；男修使用分层素材，其余外观使用绘制回退。</summary>
public partial class CharacterToken : Node2D
{
    private const string Root = "res://mods/appearance/水墨半身男修标准包/";
    private static readonly string[] Parts = ["hair_back","face","clothes","eye","nose","mouth","beard","hair_front"];
    private static readonly Dictionary<string,string> PartDirs = new(){{"hair_back","hair_back"},{"face","face"},{"clothes","clothes"},{"eye","eye"},{"nose","nose"},{"mouth","mouth"},{"beard","beard"},{"hair_front","hair_front"}};
    private readonly Dictionary<string,Sprite2D> sprites=[];
    private Node2D? layers; private Polygon2D? marker; private bool facingRight=true; private float idle; private int face,hair,clothes,eyebrow,eye,nose,mouth,hairColor,gender;
    public override void _Ready(){ZAsRelative=false;ZIndex=25;Visible=true;ensure_layers();refresh_layers();QueueRedraw();}
    public void configure_appearance(int seed,int face_style,int hair_style,int clothes_style,int accessory_style,int p_gender=0,int eyebrow_style=-1,int eye_style=-1,int nose_style=-1,int mouth_style=-1,int hair_color=-1){var rng=new RandomNumberGenerator{Seed=(ulong)(seed==0?1:seed)};face=resolve(face_style,rng);hair=resolve(hair_style,rng);eyebrow=resolve(eyebrow_style,rng);eye=resolve(eye_style,rng);nose=resolve(nose_style,rng);mouth=resolve(mouth_style,rng);hairColor=hair_color>=0?hair_color:rng.RandiRange(0,3);clothes=clothes_style>=0?clothes_style:rng.RandiRange(0,5);gender=p_gender;refresh_layers();QueueRedraw();}
    public void set_facing_right(bool right){facingRight=right;if(layers is not null)layers.Scale=new(right?1:-1,1);}
    public void set_faction_marker(int faction){marker ??= new Polygon2D{Name="FactionMarker",Polygon=[new(-42,0),new(-31,-10),new(0,-15),new(31,-10),new(42,0),new(31,10),new(0,15),new(-31,10)],Position=new(0,4),ZIndex=-1};if(marker.GetParent() is null)AddChild(marker);marker.Color=faction==1?new Color("5e9b81"):faction==2?new Color("b35d57"):new Color("6f8293");}
    public override void _Process(double delta){idle+=(float)delta;QueueRedraw();}
    public override void _Draw(){if(gender==0)return;var offset=new Vector2(0,Mathf.Sin(idle*1.8f)*1.5f);DrawCircle(new Vector2(0,-51)+offset,15,new Color("f0d1b4"));DrawCircle(new Vector2(0,-59)+offset,17,new Color("332b29"));DrawCircle(new Vector2(0,-51)+offset,14.2f,new Color("f0d1b4"));}
    private static int resolve(int value,RandomNumberGenerator rng)=>value>=0?0:0;
    private void ensure_layers(){if(layers is not null)return;layers=new Node2D{Name="AppearanceLayers",ZIndex=2};AddChild(layers);foreach(var part in Parts){var sprite=new Sprite2D{Name=part,Centered=true,Scale=new(.065f,.065f),ZIndex=Array.IndexOf(Parts,part)};layers.AddChild(sprite);sprites[part]=sprite;}}
    private void refresh_layers(){if(!IsInsideTree())return;ensure_layers();if(layers is null)return;layers.Visible=gender==0;var styles=new Dictionary<string,int>{{"hair_back",hair},{"face",face},{"clothes",clothes},{"eye",eye},{"nose",nose},{"mouth",mouth},{"beard",face},{"hair_front",hair}};foreach(var pair in styles){var path=$"{Root}{PartDirs[pair.Key]}/{pair.Value}.png";if(!ResourceLoader.Exists(path))path=$"{Root}{PartDirs[pair.Key]}/0.png";sprites[pair.Key].Texture=ResourceLoader.Load<Texture2D>(path);sprites[pair.Key].Position=new(0,-50);}layers.Scale=new(facingRight?1:-1,1);}
}
