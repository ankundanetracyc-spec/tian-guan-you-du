using Godot;

public partial class AlchemySlotBrewPreviewScene
{
    private Button? _compactBrewButton;
    private void AddSidebarToggle(Control panel, bool left)
    {
        bool collapsed = false;
        var tab = MakeButton("", () => { }, 16);
        tab.Name = left ? "ToggleIngredientLedger" : "ToggleBatchRequirements";
        tab.AddThemeStyleboxOverride("normal", QuietRoomStyle.Paper(.98f, 4));
        tab.AddThemeStyleboxOverride("hover", QuietRoomStyle.Paper(1f, 4));
        tab.AddThemeStyleboxOverride("pressed", QuietRoomStyle.Paper(.94f, 4));
        Place(_canvas, tab, new Vector2(left ? 238 : 1418, 420), new Vector2(30, 118));
        if (!left)
        {
            _compactBrewButton = MakeButton("炼丹", ShowBrewSimulation, 24);
            _compactBrewButton.Name = "CollapsedBrewButton";
            ApplyInkBrushButtonStyle(_compactBrewButton);
            Place(_canvas, _compactBrewButton, new Vector2(1490, 840), new Vector2(150, 56));
            _compactBrewButton.Hide();
        }
        void RefreshTab()
        {
            panel.Visible = !collapsed;
            tab.Position = new Vector2(left ? (collapsed ? 0 : 238) : (collapsed ? 1642 : 1418), 420);
            tab.Text = string.Join(((char)10).ToString(), left ? new[] { "药", "材", collapsed ? "〉" : "〈" } : new[] { "用", "度", collapsed ? "〈" : "〉" });
            SetHover(tab, collapsed ? "展开信息栏" : "收起到侧边");
            if (!left) _compactBrewButton!.Visible = collapsed;
        }
        tab.Pressed += () => { collapsed = !collapsed; RefreshTab(); };
        RefreshTab();
    }
}
