using Godot;

/// <summary>
/// 独立交锋试玩的人物展示。气血和炁只读取调用方传入的实际值；
/// 定力机制尚未接入，空槽不伪造数值或结算。
/// </summary>
public partial class TrialCombatantView : Control
{
    private const string QiIconPath = "res://textures/ui/combat/status_icons/炁_v1.png";
    private static readonly Color Frame = new("b59a69"), Ink = new("1a1c19"),
        Blood = new("a94539"), Resolve = new("c6a15c"), Text = new("e8d9b7");

    private QueueSmallAvatarBridge? _doll;
    private Texture2D? _qiIcon;
    private bool _isLeft = true;
    private int _health, _maxHealth, _qi;

    public int CurrentHealth => _health;
    public int MaximumHealth => _maxHealth;
    public int Qi => _qi;
    public bool IsLeft => _isLeft;
    public bool ResolveAvailable => false;

    /// <summary>左侧人物朝右、右侧人物朝左；两条竖槽始终位于人物外侧。</summary>
    public void Configure(bool isLeft)
    {
        _isLeft = isLeft;
        if (IsNodeReady()) ConfigureDoll();
        QueueRedraw();
    }

    /// <summary>由调用方传入该角色当前气血、气血上限与真实炁值。</summary>
    public void Refresh(int hp, int maxHp, int qi)
    {
        _maxHealth = Math.Max(0, maxHp);
        _health = Math.Clamp(hp, 0, _maxHealth);
        _qi = Math.Max(0, qi);
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _qiIcon = GD.Load<Texture2D>(QiIconPath);
        _doll = new QueueSmallAvatarBridge { Name = "CombatantPortrait" };
        AddChild(_doll);
        Resized += PositionDoll;
        ConfigureDoll();
    }

    private void ConfigureDoll()
    {
        if (_doll is null) return;
        // 与现有战斗人物相同的 Q 版骨骼，直接复用其展示适配器；无需构造战斗快照。
        if (!_doll.Configure(BattlePaperDoll.SideInkReferenceHeight,
                "res://textures/characters/turn_based_pair_20261106", "turn_basic_male_20261106", true))
            GD.PushWarning("试玩人物素材未能加载。");
        // 同一套现有立绘仅在运行时轻微着色，便于区分对手；不改动源图。
        _doll.Modulate = _isLeft ? Colors.White : new Color(.94f, .86f, .83f);
        _doll.SetFacing(!_isLeft);
        PositionDoll();
    }

    private void PositionDoll()
    {
        if (_doll is null) return;
        // 向内偏移，给外侧血条与定力槽留出独立空间。
        _doll.Position = new Vector2(Size.X * (_isLeft ? .59f : .41f), Size.Y * .78f);
        _doll.Scale = Vector2.One * (Mathf.Clamp(Size.Y * .64f, 100f, 190f) / BattlePaperDoll.SideInkReferenceHeight);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var height = Mathf.Max(30, Size.Y * .61f);
        const float barWidth = 13f, gap = 7f, groupWidth = 33f;
        var inset = Mathf.Clamp(Size.X * .28f, 11f, Mathf.Max(11f, (Size.X - groupWidth) * .5f));
        var left = _isLeft ? inset : Mathf.Max(0f, Size.X - groupWidth - inset);
        var top = Mathf.Max(28, Size.Y * .10f);
        var bloodX = _isLeft ? left : left + barWidth + gap;
        var resolveX = _isLeft ? left + barWidth + gap : left;
        var blood = new Rect2(bloodX, top, barWidth, height);
        var resolve = new Rect2(resolveX, top, barWidth, height);

        DrawGauge(blood, _maxHealth > 0 ? (float)_health / _maxHealth : 0f, Blood);
        DrawGauge(resolve, 0f, Resolve);
        DrawString(font, new Vector2(bloodX - 5, top - 5), "血", HorizontalAlignment.Center, 23, 14, Text);
        DrawString(font, new Vector2(resolveX - 5, top - 5), "定", HorizontalAlignment.Center, 23, 14, Text);
        var captionX = left + (groupWidth - 86f) * .5f;
        var bloodText = _maxHealth > 0 ? $"{_health}/{_maxHealth}" : "—";
        DrawString(font, new Vector2(captionX, top + height + 17), bloodText,
            HorizontalAlignment.Center, 86, 13, Text);
        DrawString(font, new Vector2(captionX, top + height + 33), "定力 —",
            HorizontalAlignment.Center, 86, 12, new Color("b6a989"));

        var statusY = Mathf.Min(Size.Y - 24, Size.Y * .81f);
        var center = Size.X * (_isLeft ? .59f : .41f);
        if (_qi <= 0)
        {
            DrawString(font, new Vector2(center - 45, statusY + 16), "无状态",
                HorizontalAlignment.Center, 90, 12, new Color("a69c87", .68f));
            return;
        }
        var status = new Rect2(center - 25, statusY, 22, 22);
        if (_qiIcon is not null) DrawTextureRect(_qiIcon, status, false);
        else DrawRect(status, new Color("4a7567"));
        DrawString(font, new Vector2(status.End.X + 4, status.Position.Y + 16), $"炁 {_qi}",
            HorizontalAlignment.Left, -1, 13, Text);
    }

    private void DrawGauge(Rect2 rect, float ratio, Color color)
    {
        DrawRect(rect.Grow(2), Ink);
        DrawRect(rect, new Color("292b25"));
        var fill = rect.Size.Y * Mathf.Clamp(ratio, 0f, 1f);
        if (fill > 0) DrawRect(new Rect2(rect.Position.X, rect.End.Y - fill, rect.Size.X, fill), color);
        DrawRect(rect.Grow(1), Frame, false, 1f);
    }
}
