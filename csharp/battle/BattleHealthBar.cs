using Godot;

/// <summary>同一个固定画布绘制外框与槽内填色，避免主题最小高度撑开生命条。</summary>
public partial class BattleHealthBar : Control
{
    public float MaxValue { get; set; } = 1;
    public float Value { get; set; }
    public float MaxResolve { get; set; } = 1;
    public float Resolve { get; set; }
    public bool IsAlly { get; set; }
    public bool Simple { get; set; }
    public bool ShowResolve { get; set; }
    public override void _Ready() => MouseFilter=MouseFilterEnum.Ignore;
    public override void _Draw()
    {
        if (!Simple) DrawTextureRect(BattleUiArt.Texture("health_frame"),new Rect2(Vector2.Zero,Size),false);
        var ratio=Mathf.Clamp(Value/Mathf.Max(1,MaxValue),0,1);
        var slot=Simple?new Rect2(18,38,196,7):new Rect2(Size.X*.08f,Size.Y*.70f,Size.X*.84f,Size.Y*.15f);
        DrawRect(slot,new Color("101b1b"));
        if (ratio > 0)
        {
            var filled=new Rect2(slot.Position,new Vector2(slot.Size.X*ratio,slot.Size.Y));
            if (Simple) DrawRect(filled,new Color("c45b48"));
            else
            {
                var fill=BattleUiArt.Texture(IsAlly?"health_fill_ally":"health_fill_enemy");
                DrawTextureRectRegion(fill,filled,new Rect2(Vector2.Zero,new Vector2(fill.GetWidth()*ratio,fill.GetHeight())));
            }
        }
        if (!ShowResolve) return;
        var resolveRatio=Mathf.Clamp(Resolve/Mathf.Max(1,MaxResolve),0,1);
        var resolveSlot=new Rect2(18,48,196,8);
        DrawRect(resolveSlot,new Color("101b1b"));
        DrawRect(new Rect2(resolveSlot.Position,new Vector2(resolveSlot.Size.X*resolveRatio,resolveSlot.Size.Y)),new Color("d6b568"));
        DrawRect(new Rect2(resolveSlot.Position,resolveSlot.Size),new Color("78847a"),false,1);
    }
}
