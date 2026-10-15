using Godot;

public partial class MainHUD
{
    private PanelContainer? _letterBadge;
    private Label? _letterBadgeText;
    public void set_letter_counts(int total, int unread)
    {
        if (FindButton("BtnLetters") is not { } button) return;
        if (_letterBadge is null)
        {
            _letterBadge = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(24, 24) };
            _letterBadge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color("985344"), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
                ContentMarginLeft = 5, ContentMarginRight = 5, ContentMarginTop = 2, ContentMarginBottom = 2
            });
            button.AddChild(_letterBadge); _letterBadge.Position = new Vector2(74, 6);
            _letterBadgeText = WorldHudStyle.Text("", 15); _letterBadgeText.HorizontalAlignment = HorizontalAlignment.Center;
            _letterBadgeText.AddThemeColorOverride("font_color", new Color("f4eedc")); _letterBadge.AddChild(_letterBadgeText);
        }
        _letterBadge.Visible = unread > 0; _letterBadgeText!.Text = unread > 99 ? "99+" : unread.ToString();
        // 沿用首次收信后可用的现行入口规则；禁用态也保留原素材。
        button.AddThemeStyleboxOverride("disabled", button.GetThemeStylebox("normal"));
        button.Disabled = total == 0;
        button.TooltipText = unread > 0 ? $"{unread} 封未读来信 · 查看信件与邀约" : total > 0 ? $"收存 {total} 封信件" : "查看信匣 · 暂无来信";
    }
}
