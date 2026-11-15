using Godot;

/// <summary>已选林间石地。只绘制场景，不参与战斗坐标、碰撞或资源结算。</summary>
public partial class BattleBoardStoneStage : Control
{
    public const string ProfileId="forest_stone_small_cultivators_20261109";
    public const string GroundPath="res://textures/battle/forest_stone_small_20261109/ground.png";
    private Texture2D? _ground,_pine,_rocks;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;ClipContents=true;
        _ground=BattleUiArt.Texture(GroundPath);
        _pine=BattleUiArt.Texture("res://textures/battle/forest_stone_small_20261109/pines.png");
        _rocks=BattleUiArt.Texture("res://textures/battle/forest_stone_small_20261109/rocks.png");
        SetMeta("combat_presentation_profile",ProfileId);SetMeta("ground_path",GroundPath);
        Resized+=QueueRedraw;QueueRedraw();
    }
    public override void _Draw()
    {
        if(_ground is null)return;
        DrawTextureRect(_ground,new Rect2(Vector2.Zero,Size),false,new Color(.93f,.96f,1f));
        var scale=Mathf.Min(Size.X/BattleUiArt.Canvas.X,Size.Y/BattleUiArt.Canvas.Y);
        void Prop(Texture2D? texture,Vector2 origin,Vector2 dimensions,bool flip=false)
        {
            if(texture is null)return;
            var box=new Rect2(origin*scale,dimensions*scale);
            if(flip)
            {
                DrawSetTransform(new(box.End.X,box.Position.Y),0,new(-1,1));
                DrawTextureRect(texture,new Rect2(Vector2.Zero,box.Size),false);
                DrawSetTransform(Vector2.Zero,0,Vector2.One);
            }
            else DrawTextureRect(texture,box,false);
        }
        // 林缘只留在画面四周；中央石坪、双方站位与底部资源栏保持清楚。
        Prop(_pine,new(-135,-176),new(620,470));
        Prop(_pine,new(BattleUiArt.Canvas.X-465,-184),new(620,470),true);
        Prop(_pine,new(-300,248),new(460,435));
        Prop(_pine,new(BattleUiArt.Canvas.X-150,250),new(460,435),true);
        Prop(_pine,new(-115,BattleUiArt.Canvas.Y-140),new(540,420));
        Prop(_pine,new(BattleUiArt.Canvas.X-425,BattleUiArt.Canvas.Y-130),new(540,420),true);
        Prop(_rocks,new(-62,BattleUiArt.Canvas.Y-280),new(285,175));
        Prop(_rocks,new(BattleUiArt.Canvas.X-225,BattleUiArt.Canvas.Y-285),new(285,175),true);
    }
    public override void _ExitTree(){_ground=_pine=_rocks=null;}
}
