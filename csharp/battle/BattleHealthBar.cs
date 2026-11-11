using Godot;

/// <summary>同一个固定画布绘制外框与槽内填色，避免主题最小高度撑开生命条。</summary>
public partial class BattleHealthBar : Control
{
    public float MaxValue { get; set; } = 1;
    public float Value { get; set; }
    public bool IsAlly { get; set; }
    public bool Simple { get; set; }
    public override void _Ready() => MouseFilter=MouseFilterEnum.Ignore;
    public override void _Draw()
    {
        if (!Simple) DrawTextureRect(BattleUiArt.Texture("health_frame"),new Rect2(Vector2.Zero,Size),false);
        var ratio=Mathf.Clamp(Value/Mathf.Max(1,MaxValue),0,1);
        // 纹理中预留的窄槽在外框内部，不作为另一条控件挂在框下。
        var slot=new Rect2(Size.X*.08f,Size.Y*.70f,Size.X*.84f,Size.Y*.15f);
        DrawRect(slot,new Color("102322"));
        if(ratio<=0)return;
        if (Simple)
        { DrawRect(new Rect2(slot.Position, new Vector2(slot.Size.X * ratio, slot.Size.Y)), new Color("9f493a")); return; }
        var fill=BattleUiArt.Texture(IsAlly?"health_fill_ally":"health_fill_enemy");
        DrawTextureRectRegion(fill,new Rect2(slot.Position,new Vector2(slot.Size.X*ratio,slot.Size.Y)),new Rect2(Vector2.Zero,new Vector2(fill.GetWidth()*ratio,fill.GetHeight())));
    }
}
