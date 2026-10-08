using Godot;

/// <summary>四角描边，留空角色与牌面的主体。</summary>
public partial class BattleCornerFrame : Panel
{
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
        Resized+=QueueRedraw;
        // 目标框通常在设置完尺寸后才被加入覆盖层；必须主动绘制一次。
        QueueRedraw();
    }
    public override void _Draw()=>Paint(this,new Rect2(Vector2.One*2,Size-Vector2.One*4),new Color(1,.81f,.4f),22);
    public static void Paint(CanvasItem canvas,Rect2 rect,Color color,float length=22)
    {
        foreach(var corner in new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)}){
            var p=rect.Position+rect.Size*corner;var x=new Vector2(corner.X==0?length:-length,0);var y=new Vector2(0,corner.Y==0?length:-length);
            canvas.DrawPolyline(new[]{p+x,p,p+y},new Color(.03f,.04f,.035f,.9f),7,true);
            canvas.DrawPolyline(new[]{p+x,p,p+y},color,3,true);
        }
    }
}
