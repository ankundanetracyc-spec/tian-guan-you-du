using Godot;
using System.Reflection;

/// <summary>正式善功堂列表的离线审阅；不接购买结算、不写人物与存档。</summary>
public partial class ExchangeItemListPreview : Control
{
    private const string Review = "res://docs/美术规范/交易列表与物品占格悬停_20261029/";

    public override async void _Ready()
    {
        try
        {
            var exchange = new YuxuSectExchangePanel();
            AddChild(exchange);
            exchange.open_panel(1_000_000);
            exchange.back_requested += () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
            if (!OS.GetCmdlineUserArgs().Contains("--capture-exchange-list")) return;
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Review));
            await Settle();
            CheckList(exchange);
            await Capture("善功堂_全部列表.png");

            var filters = Field<Dictionary<string, Button>>(exchange, "_categoryButtons");
            filters["材料"].EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            CheckList(exchange);
            await Capture("善功堂_材料列表.png");

            filters["器物"].EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            CheckList(exchange);
            await Capture("善功堂_器物列表.png");
            var grid = Field<GridContainer>(exchange, "_itemGrid");
            var first = grid.GetChildren().OfType<Button>().First();
            var expectedName = first.GetNode<Label>("ExchangeItemRow/ItemIdentity/ItemName").Text;
            first.EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            var dialog = Field<PanelContainer>(exchange, "_purchaseDialog");
            var selectedName = Field<Label>(exchange, "_purchaseName");
            if (!dialog.Visible || selectedName.Text != expectedName)
                throw new InvalidOperationException("列表所选物品与原兑换确认不一致");
            await Capture("善功堂_列表兑换确认.png");
            Field<Button>(exchange, "_purchaseCancel").EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            if (dialog.Visible || !exchange.Visible)
                throw new InvalidOperationException("取消兑换未返回原列表");
            GD.Print("EXCHANGE_ITEM_LIST_OK: single-column list; icon/name/price/action bounds; category filters; selection and cancellation preserve confirmation flow; no purchases committed");
            Input.SetCustomMouseCursor(null);
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task Settle()
    {
        for (var frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Capture(string file)
    {
        HoverInfoManager.HideAll();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(Review + file) != Error.Ok)
            throw new InvalidOperationException("善功堂截图失败: " + file);
    }

    private static void CheckList(YuxuSectExchangePanel exchange)
    {
        var grid = Field<GridContainer>(exchange, "_itemGrid");
        if (grid.Columns != 1 || grid.GetChildCount() == 0)
            throw new InvalidOperationException("善功堂未显示单列物品");
        foreach (var card in grid.GetChildren().OfType<Button>())
        {
            var row = card.GetNode<HBoxContainer>("ExchangeItemRow");
            var icon = row.GetNode<TextureRect>("ItemIcon");
            var name = row.GetNode<Label>("ItemIdentity/ItemName");
            var price = row.GetNode<Label>("ContributionPrice");
            var action = row.GetNode<PanelContainer>("ExchangeAction");
            var bounds = card.GetGlobalRect().Grow(.5f);
            if (!bounds.Encloses(icon.GetGlobalRect()) || !bounds.Encloses(action.GetGlobalRect())
                || name.GetGlobalRect().End.X > price.GetGlobalRect().Position.X + .5f)
                throw new InvalidOperationException("善功堂列表图文或价格发生越界: " + name.Text);
            if (icon.StretchMode != TextureRect.StretchModeEnum.KeepAspectCentered)
                throw new InvalidOperationException("善功堂图标未保持比例: " + name.Text);
        }
    }

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
