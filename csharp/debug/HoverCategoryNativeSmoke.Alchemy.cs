using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class HoverCategoryNativeSmoke
{
    private async Task AlchemyEntries()
    {
        _anchor.Hide();
        var actor = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData
        {
            entity_name = "炼丹独立卡隔离人物", cultivation_realm = "云息三层",
            alchemy_proficiency = 508, base_alchemy_proficiency = 508, max_health = 100, current_health = 100,
        } }; AddChild(actor); actor.Hide(); actor.max_spirit_perception = 10000;
        actor.max_world_stamina = actor.current_world_stamina = 100;
        var materials = TalismanCraftingCatalog.DiscoverMaterials().Where(material => material.Item.item_category == "材料" && !material.Name.Contains("骨"))
            .OrderBy(material => material.Item.get_inventory_cell_count()).Take(8).ToArray();
        foreach (var material in materials) if (!actor.add_item_to_inventory(material.Path, 1)) throw new Exception("cannot prepare isolated owned material " + material.Name);
        var inventory = string.Join("|", actor.get_spatial_inventory().Select(entry => entry.InstanceId + ":" + entry.ItemPath));
        var energy = actor.current_world_stamina;
        var room = GD.Load<PackedScene>("res://tscn/scene/AlchemyPreviewScene.tscn").Instantiate<AlchemySlotBrewPreviewScene>();
        room.OwnerCharacter = actor; AddChild(room);
        try
        {
            var route = new AlchemyRecipeCatalog.SampleRoute("八味悬停布局隔离样本", "", materials.Select((material, index) =>
                new AlchemyRecipeCatalog.SampleIngredient(material.Name, 1, index - 1)).ToArray());
            Invoke(room, "ApplySampleRoute", route, true); await Frames(4);
            var main = Field<Button>(room, "_mainPickButton");
            var spirit = Field<Label>(room, "_paperSpirit");
            var card = Field<PanelContainer>(room, "_hoverCard"); var text = Field<Label>(room, "_hoverText");
            Check(card.Name == "AlchemyHerbHoverCard", "formal alchemy retains its actual independent hover card");
            foreach (var (id, anchor) in new[] { ("君药", (Control)main), ("八味本炉灵识", (Control)spirit) })
            {
                Check(anchor.IsVisibleInTree() && anchor.HasMeta("alchemy_hover_text"), "formal alchemy uses a real registered source " + id, anchor.GetPath());
                Invoke(room, "HideHover"); HoverInfoManager.HideAll(); await NativeSource(anchor, "actual alchemy " + id);
                anchor.EmitSignal(Control.SignalName.MouseEntered);
                await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout); await Frames(4);
                var source = anchor.GetMeta("alchemy_hover_text").AsString();
                var detailedSize = Vector2.Zero; var simpleSize = Vector2.Zero;
                foreach (var mode in new[] { PlayerInfoText.InformationMode.Detailed, PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
                {
                    PlayerInfoText.SetMode(mode, persist: false); await Frames(4);
                    _geometry.Add(new { type = "alchemy", id, mode = mode.ToString(), card_size = card.Size.ToString(), card_minimum = card.GetMinimumSize().ToString(),
                        card_combined_minimum = card.GetCombinedMinimumSize().ToString(), actual_bounds = Bounds(card).ToString(),
                        label_size = text.Size.ToString(), label_custom_minimum = text.CustomMinimumSize.ToString(), label_minimum = text.GetMinimumSize().ToString(),
                        lines = text.GetLineCount(), visible_lines = text.GetVisibleLineCount(), font_size = text.GetThemeFontSize("font_size"),
                        line_spacing = text.GetThemeConstant("line_spacing"), canvas_transform = Field<Control>(room, "_canvas").GetGlobalTransformWithCanvas().ToString(),
                        viewport = GetViewport().GetVisibleRect().ToString() });
                    var expected = PlayerInfoText.LayoutLongParagraphs(PlayerInfoText.Display(source));
                    Check(card.IsVisibleInTree() && text.Text == expected, "open alchemy independent card refreshes the mode without reentering " + id + mode, text.Text);
                    Check(Bounds(card).Grow(1).Encloses(Bounds(text)), "alchemy text region stays within its actual card " + id + mode);
                    Check(text.GetVisibleLineCount() == text.GetLineCount(), "alchemy displays every naturally wrapped line " + id + mode,
                        text.GetVisibleLineCount() + "/" + text.GetLineCount());
                    Check(!GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard").IsVisibleInTree(),
                        "alchemy source opens only its existing independent card " + id + mode);
                    if (mode == PlayerInfoText.InformationMode.Detailed) detailedSize = Bounds(card).Size; else simpleSize = Bounds(card).Size;
                    var key = "炼丹_" + id + "_" + mode;
                    if (!_captures.Any(capture => capture.ToString()?.Contains(key) == true))
                        await Capture(card, key, "正式炼丹独立卡", "正式页真实来源控件", "AlchemySlotBrewPreviewScene.SetHover → RefreshHoverCard",
                            anchor.GetPath(), text.Text);
                }
                Check(detailedSize.Y > simpleSize.Y, "alchemy card shrinks after switching to simple mode " + id, detailedSize + " → " + simpleSize);
                if (id == "八味本炉灵识")
                {
                    Check(PlayerInfoText.CalculationSources(source).Count == 9, "eight real materials expose total plus eight separate occupation sources", source);
                    Check(detailedSize.Y > 420 || detailedSize.X > simpleSize.X, "long actual eight-material source uses measured height or greater width rather than a 420-pixel clipping cap", detailedSize);
                }
            }
            Check(inventory == string.Join("|", actor.get_spatial_inventory().Select(entry => entry.InstanceId + ":" + entry.ItemPath))
                && energy == actor.current_world_stamina, "viewing alchemy sources does not consume ingredients or world stamina");
        }
        finally { room.Free(); actor.Free(); _anchor.Show(); }
    }
}
