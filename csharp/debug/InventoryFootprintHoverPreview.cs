using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Read-only metadata coverage and real shared-hover edge-layout capture.</summary>
public partial class InventoryFootprintHoverPreview : Control
{
    private const string Review = "res://tmp/item_hover_capacity_preview/";
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
            // Deliberately pair a 1x1 sprite and a 2x5 display shape with an independent volume of 7.
            var synthetic = new ItemData { entity_name = "体积读取验证", item_category = "材料", storage_volume = 7, inventory_width = 2, inventory_height = 5,
                icon_path = InventoryIconDensityPack.Root + "spirit_stone_1x1.png" };
            var syntheticHover = StoragePanel.ItemHover(synthetic);
            var syntheticFields = syntheticHover.Sections is { Count: > 0 } ? syntheticHover.Sections[0].Body : syntheticHover.Body;
            Check(syntheticHover.Footprint is null && syntheticFields.StartsWith("单件体积：7 容量", StringComparison.Ordinal),
                "Volume must be a text field and never inferred from icon dimensions");
            var anchor = new Button { Text = "物品", Position = new Vector2(40, 40), Size = new Vector2(156, 54) };
            AddChild(anchor);
            var manager = GetNode<HoverInfoManager>("/root/HoverInfoManager");
            var card = manager.GetNode<Control>("SharedHoverInfoCard");
            var grid = card.FindChild("HeaderInventoryFootprint", true, false) as InventoryFootprintView
                ?? throw new InvalidOperationException("Shared hover has no footprint view");
            var examples = new (string Name, string Path, int? Count)[]
            {
                ("灵石", "res://resources/entities/items/灵石.tres", null),
                ("丹药", "res://resources/entities/items/alchemy/huichun_1.tres", 5),
                ("秘籍", "res://resources/entities/items/灵枢经筋篇秘籍.tres", null),
                ("衣袍", "res://resources/entities/items/clothing/男_玄青云竹袍.tres", null),
                ("铁剑", "res://resources/entities/equipment/铁剑.tres", null),
                ("玄铁", "res://resources/entities/items/玄铁.tres", null),
            };
            for (var index = 0; index < examples.Length; index++)
            {
                var (key, path, count) = examples[index];
                var item = GD.Load<ItemData>(path) ?? throw new InvalidOperationException("物品资源加载失败：" + path);
                var info = StoragePanel.ItemHover(item, count);
                var viewport = GetViewport().GetVisibleRect().Size;
                anchor.Position = index % 2 == 0 ? new Vector2(20, viewport.Y - 80) : new Vector2(viewport.X - 180, viewport.Y - 80);
                anchor.Text = info.Title;
                Move(anchor.GetGlobalTransformWithCanvas() * (anchor.Size / 2));
                HoverInfoManager.SetInfo(anchor, info, true); await Settle();
                var footerInventory = card.FindChild("InventoryFootprint", true, false) as InventoryFootprintView;
                var firstFields = info.Sections is { Count: > 0 } ? info.Sections[0].Body : info.Body;
                Check(!grid.Visible && footerInventory?.Visible != true && info.Footprint is null,
                    "Capacity mini-grid must be hidden from item hover: " + key);
                string expectedFields = $"单件体积：{item.get_storage_volume()} 容量"
                    + (count is > 1 ? $"\n当前堆叠容量：{(long)item.get_storage_volume() * count.Value} 容量" : "");
                Check(firstFields.StartsWith(expectedFields, StringComparison.Ordinal),
                    "Item volume must be the first field: " + key);
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
                    var scale = new Vector2(image.GetWidth() / viewport.X, image.GetHeight() / viewport.Y);
                    var cropRect = new Rect2I(
                        Mathf.RoundToInt(bounds.Position.X * scale.X), Mathf.RoundToInt(bounds.Position.Y * scale.Y),
                        Mathf.RoundToInt(bounds.Size.X * scale.X), Mathf.RoundToInt(bounds.Size.Y * scale.Y));
                    using var crop = image.GetRegion(cropRect);
                    Check(crop.SavePng(Review + $"物品体积悬停_{index + 1:00}_{key}.png") == Error.Ok, "Hover capture failed");
                }
            }
            HoverInfoManager.SetInfo(anchor, new HoverInfoManager.HoverInfoContent("状态", "效果：测试非物品信息隔离。", ShowRules: false), true);
            await Settle();
            Check(!grid.Visible && grid.Footprint is null, "Non-item hover must clear the previous footprint");
            GD.Print($"CAPACITY_VOLUME_HOVER_OK: {_checks} checks; item-volume hover; six edge-layout samples; non-item isolation");
            HoverInfoManager.HideAll(); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(originalMode, persist: false); }
    }
}
