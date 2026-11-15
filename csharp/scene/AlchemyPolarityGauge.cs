using Godot;
/// <summary>阴阳偏向只读刻度；允许区间与当前值分别呈现。</summary>
public partial class AlchemyPolarityGauge : Control
{
    public double Value, Minimum=-.75, Maximum=.75;
    public bool HasMedicine;
    public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;Resized+=QueueRedraw;}
    public override void _Draw()
    {
        float y=Size.Y/2,left=16,right=Size.X-16,width=right-left;
        float X(double v)=>left+(float)((v+1)/2)*width;
        DrawLine(new Vector2(left,y),new Vector2(right,y),new Color("b9b4a2"),3,true);
        DrawRect(new Rect2(X(Minimum),y-6,X(Maximum)-X(Minimum),12),new Color(.36f,.49f,.40f,.15f));
        for(int i=0;i<=4;i++)DrawLine(new Vector2(left+width*i/4,y-5),new Vector2(left+width*i/4,y+5),new Color("9a9381"),1,true);
        DrawLine(new Vector2(X(Minimum),y-9),new Vector2(X(Minimum),y+9),new Color("657c68"),1,true);
        DrawLine(new Vector2(X(Maximum),y-9),new Vector2(X(Maximum),y+9),new Color("657c68"),1,true);
        if(HasMedicine){float x=X(Mathf.Clamp((float)Value,-1,1));DrawCircle(new Vector2(x,y),6,new Color("384f42"));DrawCircle(new Vector2(x,y),2,new Color("ede6d2"));}
    }
}
