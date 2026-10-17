using Godot;
using System;

/// <summary>丹炉外观与炉内实火视图。只读炉况，不推进游戏时间或改变炼制规则。</summary>
public partial class AlchemyFurnaceView : Control
{
    private Texture2D[] _shells=[];
    private ShaderMaterial _fire=null!;
    private ColorRect _window=null!;
    private TextureRect _rear=null!,_front=null!;
    private float _shownPower,_targetPower,_residual,_clock,_redraw;
    private int _appearance;
    public int Appearance { get=>_appearance; set{_appearance=Math.Clamp(value,0,1);Arrange();QueueRedraw();} }
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        _shells=[AlchemyPreviewScene.LoadRoomTexture("res://textures/ui/alchemy_room_20261017/furnace_front_shell_v8.png"),MakeShell(true)];
        _rear=new TextureRect{Texture=AlchemyPreviewScene.LoadRoomTexture("res://textures/ui/alchemy_room_20261017/furnace_inner_back_v8.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};AddChild(_rear);
        _fire=new ShaderMaterial{Shader=GD.Load<Shader>("res://shaders/alchemy_furnace_fire.gdshader")};
        _window=new ColorRect{Color=Colors.White,Material=_fire,MouseFilter=MouseFilterEnum.Ignore};AddChild(_window);
        _front=new TextureRect{Texture=_shells[0],ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};AddChild(_front);
        Resized+=()=>{Arrange();QueueRedraw();};Arrange();
    }
    public void SetState(double actualPower,double temperature)
    {
        _targetPower=(float)Math.Clamp(actualPower/4,0,1);_residual=(float)Math.Clamp((temperature-25)/85,0,1);
        SetProcess(true);
    }
    private float DrawingScale=>Mathf.Min(Size.X/600,Size.Y/540);
    private Vector2 Origin=>(Size-new Vector2(600,540)*DrawingScale)/2;
    private void Arrange()
    {
        if(_window is null)return;
        _rear.Visible=_front.Visible=_appearance==0;
        if(_appearance==0&&_shells.Length>0){var r=SpriteRect();_front.Position=r.Position;_front.Size=r.Size;_rear.Position=r.Position+r.Size*new Vector2(.315f,.595f);_rear.Size=r.Size*new Vector2(.37f,.175f);_window.Position=_rear.Position;_window.Size=_rear.Size;return;}
        _window.Position=Origin+new Vector2(220,247)*DrawingScale;_window.Size=new Vector2(160,160)*DrawingScale;
    }
    private Rect2 SpriteRect(){var native=_shells[0].GetSize();float s=Mathf.Min(Size.X/native.X,Size.Y/(native.Y*.989f));var fitted=native*s;return new Rect2(new Vector2((Size.X-fitted.X)/2,Size.Y-fitted.Y*.989f),fitted);}
    public override void _Process(double delta)
    {
        _clock+=(float)delta;_redraw+=(float)delta;
        _shownPower=Mathf.Lerp(_shownPower,_targetPower,1-Mathf.Exp(-(float)delta*6));
        _fire.SetShaderParameter("power",_shownPower);_fire.SetShaderParameter("residual",_residual);
        if(_redraw>.033){_redraw=0;QueueRedraw();}
        if(_targetPower<.001&&_shownPower<.001&&_residual<.001)SetProcess(false);
    }
    public override void _Draw()
    {
        if(_shells.Length==0||DrawingScale<=0)return;
        if(_appearance==0)return;
        var origin=Origin;float scale=DrawingScale;
        DrawSetTransform(origin,0,Vector2.One*scale);
        Vector2[] ground=new Vector2[49];for(int i=0;i<ground.Length;i++)ground[i]=new Vector2(300+Mathf.Cos(i*Mathf.Tau/48)*166,482+Mathf.Sin(i*Mathf.Tau/48)*14);
        DrawColoredPolygon(ground,new Color(.20f,.25f,.21f,.10f));
        DrawArc(new Vector2(300,282),232,Mathf.Pi*.11f,Mathf.Pi*.89f,90,new Color(.35f,.41f,.35f,.10f),1,true);
        DrawTexture(_shells[_appearance],Vector2.Zero);
        float smoke=Mathf.Max(_shownPower*.5f,_residual*.22f);
        if(smoke>.01)
        {
            for(int strand=0;strand<3;strand++)
            {
                var points=new Vector2[22];for(int i=0;i<points.Length;i++){float y=i/21f;points[i]=new Vector2(289+strand*12+Mathf.Sin(y*5+_clock*.6f+strand)*9*y,115-y*60);}
                DrawPolyline(points,new Color(.31f,.39f,.34f,smoke*.35f),1.7f,true);
            }
        }
        DrawSetTransform(Vector2.Zero);
    }
    private static Texture2D MakeShell(bool jade)
    {
        string body=jade?"#a6b9a4":"#53665a",edge=jade?"#566f62":"#35483d",metal=jade?"#839d89":"#a18e65",shade=jade?"#8ea68f":"#42574b";
        string form=jade
            ?"<path d='M192 194Q174 238 184 352L199 411Q300 450 401 411L416 352Q426 238 408 194Z'/>"
            :"<path d='M184 198C132 252 130 359 194 413Q300 457 406 413C470 359 468 252 416 198Z'/>";
        string handles=jade
            ?"<path d='M184 218H147V303L182 321M416 218H453V303L418 321'/>"
            :"<path d='M175 221C101 187 98 264 137 306L157 315M425 221C499 187 502 264 463 306L443 315'/>";
        string svg=$"<svg xmlns='http://www.w3.org/2000/svg' width='600' height='540' viewBox='0 0 600 540'><g fill='none' stroke='{edge}' stroke-width='7' stroke-linecap='round'>{handles}<path d='M205 407L185 477L216 480L241 423M395 407L415 477L384 480L359 423' fill='{shade}'/></g><g stroke='{edge}' stroke-width='3.5' fill='{body}'>{form}<path d='M176 182Q300 151 424 182L411 201Q300 226 189 201Z' fill='{shade}'/><path d='M207 163Q224 130 270 123H330Q376 130 393 163Q300 187 207 163Z'/><ellipse cx='300' cy='119' rx='31' ry='9' fill='{shade}'/><path d='M281 115Q276 91 300 89Q324 91 319 115' fill='{body}'/></g><g fill='none' stroke='{metal}' stroke-width='2.2'><path d='M192 231Q300 257 408 231M199 398Q300 427 401 398'/><path d='M207 253Q188 265 206 277Q223 289 209 301M393 253Q412 265 394 277Q377 289 391 301'/><path d='M236 185Q300 195 364 185' stroke-opacity='.5'/></g><ellipse cx='300' cy='327' rx='86' ry='86' fill='{shade}' stroke='{metal}' stroke-width='3'/><ellipse cx='300' cy='327' rx='79' ry='79' fill='#293b32' stroke='{edge}' stroke-width='4'/><g fill='none' stroke='{metal}' stroke-opacity='.28' stroke-width='1'><path d='M222 245Q232 252 225 268M379 245Q367 252 375 268M217 382Q236 399 252 401M383 382Q364 399 348 401'/></g></svg>";
        var image=new Image();image.LoadSvgFromString(svg);return ImageTexture.CreateFromImage(image);
    }
}
