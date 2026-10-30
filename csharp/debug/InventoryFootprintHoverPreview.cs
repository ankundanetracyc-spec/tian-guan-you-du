using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Read-only metadata coverage and real shared-hover edge-layout capture.</summary>
public partial class InventoryFootprintHoverPreview : Control
{
    private const string Review = "res://docs/美术规范/物品列表与占格悬停_20261029/";
    private int _checks;
    private Vector2 _pointer;

    public override void _Ready() => CallDeferred(nameof(Run));
    private void Check(bool result, string reason)
    {
        _checks++;
        if (!result) throw new InvalidOperationException(reason);
    }
    private void Move(Vector2 point)
    {
        _pointer = point;
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
    }
    private async Task Settle()
    {
        for (var frame = 0; frame < 12; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Move(_pointer);
        }
    }

    private async void Run()
    {
        var originalMode = PlayerInfoText.Mode;
        try
        {
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            Theme = QuietRoomStyle.MakeTheme();
            var background = new ColorRect { Color = new Color("eae2cc"), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var rows = Json.ParseString(Godot.FileAccess.GetFileAsString(InventoryIconDensityPack.Manifest)).AsGodotArray();
            foreach (var value in rows)
            {
                var row = value.AsGodotDictionary();
                var item = GD.Load<ItemData>("res://" + row["path"].AsString());
                var expected = new Vector2I(row["width"].AsInt32(), row["height"].AsInt32());
                Check(StoragePanel.ItemHover(item).Footprint == expected, "Shared hover footprint differs from catalog: " + item.entity_name);
            }
            // Deliberately pair a 1x1 sprite with a 2x5 definition: metadata wins.
            var synthetic = new ItemData { entity_name = "占地读取验证", item_category = "材料", inventory_width = 2, inventory_height = 5,
                icon_path = InventoryIconDensityPack.Root + "spirit_stone_1x1.png" };
            Check(StoragePanel.ItemHover(synthetic).Footprint == new Vector2I(2, 5), "Footprint must never be inferred from the icon's dimensions");
            var anchor = new Button { Text = "物品", Position = new Vector2(40, 40), Size = new Vector2(156, 54) };
            AddChild(anchor);
            var manager = GetNode<HoverInfoManager>("/root/HoverInfoManager");
            var card = manager.GetNode<Control>("SharedHoverInfoCard");
            var grid = card.FindChild("InventoryFootprint", true, false) as InventoryFootprintView
                ?? throw new InvalidOperationException("Shared hover has no footprint view");
            var examples = new[] { "灵石", "秘籍", "丹药", "玄青云竹袍", "承露剑", "玄铁" };
            for (var index = 0; index < examples.Length; index++)
            {
                var key = examples[index];
                var row = rows.Select(v => v.AsGodotDictionary()).First(r => r["name"].AsString().Contains(key) || r["category"].AsString() == key);
                var item = GD.Load<ItemData>("res://" + row["path"].AsString());
                var info = StoragePanel.ItemHover(item);
                var viewport = GetViewport().GetVisibleRect().Size;
                anchor.Position = index % 2 == 0 ? new Vector2(20, viewport.Y - 80) : new Vector2(viewport.X - 180, viewport.Y - 80);
                anchor.Text = info.Title;
                Move(anchor.GetGlobalTransformWithCanvas() * (anchor.Size / 2));
                HoverInfoManager.SetInfo(anchor, info, true); await Settle();
                Check(grid.Visible && grid.Footprint == info.Footprint && grid.MouseFilter == MouseFilterEnum.Ignore, "Item footprint must be visible and mouse-transparent: " + key);
                Check(grid.OccupiedCellCount == row["area"].AsInt32(), "Occupied cell count differs from item catalog: " + key);
                var bounds = card.GetGlobalRect();
                Check(bounds.Position.X >= 0 && bounds.Position.Y >= 0 && bounds.End.X <= viewport.X + 1 && bounds.End.Y <= viewport.Y + 1,
                    "Footprint header pushes the full hover outside the viewport: " + key);
                Check(card.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().All(s => s.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled),
                    "Item hover must remain fully expanded");
                if (DisplayServer.GetName() != "headless")
                {
                    DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Review));
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = GetViewport().GetTexture().GetImage();
                    using var crop = image.GetRegion(new Rect2I((Vector2I)bounds.Position, (Vector2I)bounds.Size));
                    Check(crop.SavePng(Review + $"占格悬停_{index + 1:00}_{key}.png") == Error.Ok, "Hover capture failed");
                }
            }
            HoverInfoManager.SetInfo(anchor, new HoverInfoManager.HoverInfoContent("状态", "效果：测试非物品信息隔离。", ShowRules: false), true);
            await Settle();
            Check(!grid.Visible && grid.Footprint is null, "Non-item hover must clear the previous footprint");
            GD.Print($"INVENTORY_FOOTPRINT_HOVER_OK: {_checks} checks; 96 item definitions; six edge-layout samples; non-item isolation");
            HoverInfoManager.HideAll(); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(originalMode, persist: false); }
    }
}
