using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class AlchemySlotBrewPreviewScene
{
    private bool _simpleAlchemyPresentation;
    private void LoadAlchemyPresentationPreference() => LoadPaperPresentationPreference();

    private ItemClaimPanel? _alchemyClaim;
    private void OpenAlchemyClaim(AlchemySlotSimulation.Result result, AlchemySlotSimulation.PillResult[] pills)
    {
        if (_alchemyClaim is not null) return;
        var rewards = new Godot.Collections.Array();
        foreach (var group in pills.GroupBy(p => AlchemyQualityRules.Tier(p.Quality)).OrderByDescending(g => g.Key))
            rewards.Add(new Godot.Collections.Dictionary {
                ["item_path"] = AlchemyProductCatalog.Path(result.ProductId, group.Key),
                ["count"] = group.Count(), ["source"] = "炼丹"
            });
        if (rewards.Count == 0)
        {
            var close = MakeButton("返回", CloseBrewPresentation, 22); AddPaperReturnArrow(close);
            Place(_brewStage!, close, new Vector2(1534, 18), new Vector2(114, 46));
            return;
        }
        // Preview uses an isolated recipient. The real panel and its add-to-inventory logic
        // are shared, while the player's inventory is never touched by an independent demo.
        Character? previewRecipient = OwnerCharacter is null
            ? new Character { entity_data = new HumanData { entity_name = "炼丹试演" } } : null;
        var recipient = OwnerCharacter ?? previewRecipient!;
        var claim = new ItemClaimPanel { Name = "AlchemyItemClaimPanel" };
        _alchemyClaim = claim;
        GetTree().Root.AddChild(claim);
        claim.Closed += () => {
            _alchemyClaim = null;
            previewRecipient?.Free();
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) CloseBrewPresentation();
        };
        var title = OwnerCharacter is null ? "炼丹所得 · 独立试演" : "炼丹所得";
        if (OwnerCharacter is not null && result.ProficiencyGrowth > 0)
            title += $" · 丹道造诣+{result.ProficiencyGrowth:0.#}";
        if (!claim.Present(recipient, rewards, title))
        {
            _alchemyClaim = null; claim.QueueFree(); previewRecipient?.Free();
            var retry = MakeButton("重新打开领取", () => { OpenAlchemyClaim(result, pills); }, 23);
            Place(_brewStage!, retry, new Vector2(710, 825), new Vector2(280, 56));
        }
        else _brewStage?.Hide();
    }

    // Calibrated landmarks inside the existing PNGs, including transparent padding.
    private static readonly Vector2 PorcelainStopperSeatUv = new(.49f, .67f);
    private static Vector2 TextureLandmark(TextureRect image, Vector2 uv)
    {
        var native = image.Texture.GetSize();
        float fit = Mathf.Min(image.Size.X / native.X, image.Size.Y / native.Y);
        var fitted = native * fit;
        return image.Position + (image.Size - fitted) * .5f + fitted * uv;
    }

    private async Task InspectBottleAndReceive(AlchemySlotSimulation.Result result,
        AlchemySlotSimulation.PillResult[] pills, Vector2 mouth, Button skip, Label caption,
        ColorRect shade)
    {
        // Every path reveals the already committed transaction; receipt never grants items again.
        bool simple = _simpleAlchemyPresentation;
        StopPresentationTweens();
        foreach (var child in _brewStage!.GetChildren().OfType<Control>())
            if (child.HasMeta("alchemy_flying_ingredient")) { child.Hide(); child.QueueFree(); }
        _furnace.SetState(.35, 35);
        if (_skipPresentation)
        {
            skip.Hide(); shade.Color = new Color(.055f, .065f, .06f, .76f);
            caption.Text = pills.Length == 0 ? (result.Exploded ? "炸炉 · 未获丹药" : "未成丹") : "领取丹药";
            OpenAlchemyClaim(result, pills); return;
        }
        if (simple) shade.Color = new Color(.055f, .065f, .06f, .76f);
        else PresentationTween().TweenProperty(shade, "color:a", .76f, .5)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        var ordered = simple ? pills.OrderByDescending(p => p.Quality).ToArray() : pills;
        var cards = new List<(Control Card, AlchemySlotSimulation.PillResult Pill)>();
        Vector2[] scatter = [new(-145, 22), new(35, -44), new(165, 53), new(-12, 110)];
        for (int i = 0; i < ordered.Length; i++)
        {
            var pill = ordered[i];
            var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, AlchemyQualityRules.Tier(pill.Quality)));
            var card = new Control { Name = $"RevealedPill{i + 1}", MouseFilter = MouseFilterEnum.Stop };
            Vector2 end = simple ? new Vector2(625, 245 + i * 103) : new Vector2(805, 490) + scatter[i % scatter.Length];
            Place(_brewStage, card, mouth - new Vector2(40, 40), new Vector2(simple ? 400 : 80, 80));
            card.PivotOffset = new Vector2(40, 40);
            var icon = IconRect(68); icon.Texture = GD.Load<Texture2D>(AnimationPillTexturePath);
            Place(card, icon, new Vector2(6, 6), new Vector2(68, 68));
            if (item.efficacy_tier >= 3)
            {
                var aura = new AlchemyPillAura { Full = item.efficacy_tier == 4, MouseFilter = MouseFilterEnum.Ignore };
                Place(card, aura, new Vector2(-10, -10), new Vector2(100, 100)); card.MoveChild(aura, 0);
            }
            HoverInfoManager.SetInfo(card, AlchemyPillVisual.UnbottledHover(item, pill.Quality, icon.Texture));
            card.SetMeta("pill_item_path", item.ResourcePath); card.SetMeta("pill_score", pill.Quality); card.SetMeta("settled_position", end);
            if (simple)
            {
                var line = QuietRoomStyle.Text($"{pill.QualityName} · 丹效 {pill.Quality}/100", 23);
                line.AddThemeColorOverride("font_color", new Color("eee5ce"));
                Place(card, line, new Vector2(95, 22), new Vector2(300, 40));
            }
            cards.Add((card, pill));
            if (!simple && !_skipPresentation)
            {
                card.Scale = Vector2.One * .18f; card.Modulate = Colors.Transparent;
                var target = end + card.PivotOffset;
                float lean = (i % 2 == 0 ? -1 : 1) * .12f;
                var flight = PresentationTween();
                flight.TweenMethod(Callable.From<float>(t => {
                    float travel = Mathf.SmoothStep(0, 1, t);
                    card.Position = mouth.BezierInterpolate(mouth + new Vector2(0, -115),
                        target + new Vector2(0, -65), target, travel) - card.PivotOffset;
                    card.Scale = Vector2.One * Mathf.Lerp(.18f, 1, Mathf.SmoothStep(0, .46f, t));
                    card.Rotation = Mathf.Sin(t * Mathf.Pi) * lean;
                    card.Modulate = new Color(1, 1, 1, Mathf.SmoothStep(0, .16f, t));
                }), 0f, 1f, .95);
                await PausePresentation(1.05);
                if (!IsInsideTree()) return;
            }
            if (_skipPresentation) break;
            card.Position = end; card.Scale = Vector2.One; card.Rotation = 0; card.Modulate = Colors.White;
        }
        bool confirmed = false;
        var confirm = MakeButton("确定 · 按丹效装瓶", () => confirmed = true, 23);
        Place(_brewStage, confirm, new Vector2(700, 825), new Vector2(300, 56)); ApplyInkBrushButtonStyle(confirm);
        _brewing = false;
        if (pills.Length == 0) confirmed = true;
        while (!confirmed && !_skipPresentation && IsInsideTree()) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree()) return;
        _brewing = true; confirm.Hide(); HoverInfoManager.HideAll();
        var groups = pills.GroupBy(p => AlchemyQualityRules.Tier(p.Quality)).OrderByDescending(g => g.Key).ToArray();
        var bottles = new List<TextureRect>();
        if (!_skipPresentation && !simple)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, groups[i].Key));
                var container = PillContainerCatalog.LoadContainerDefinition(item.container_material_id)!;
                var bottle = IconRect(1);
                bottle.Texture = GD.Load<Texture2D>(container.animation_from_closed_icon ? item.icon_path : container.open_bottle_icon_path);
                if (container.animation_from_closed_icon)
                {
                    AlchemyPillVisual.Apply(bottle, groups[i].Key, item.pill_id);
                    var skin = (ShaderMaterial)bottle.Material;
                    skin.SetShaderParameter("open_bottle", true);
                    skin.SetShaderParameter("body_cutoff_uv", container.animation_body_cutoff_uv);
                    skin.SetShaderParameter("mouth_uv", container.animation_mouth_uv);
                    bottle.SetMeta("animation_bottle_source", item.icon_path);
                }
                var position = new Vector2(835 + (i - (groups.Length - 1) * .5f) * 180 - 50, 650);
                Place(_brewStage, bottle, position + new Vector2(0, 16), new Vector2(100, 160)); bottles.Add(bottle);
                bottle.Modulate = Colors.Transparent;
                var entrance = PresentationTween().SetParallel();
                entrance.TweenProperty(bottle, "position", position, .22)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                entrance.TweenProperty(bottle, "modulate:a", 1f, .22);
            }
            await PausePresentation(.25);
            if (!IsInsideTree()) return;
            for (int i = 0; i < groups.Length && !_skipPresentation; i++)
            {
                var bottle = bottles[i];
                var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, groups[i].Key));
                var container = PillContainerCatalog.LoadContainerDefinition(item.container_material_id)!;
                foreach (var pair in cards.Where(c => AlchemyQualityRules.Tier(c.Pill.Quality) == groups[i].Key))
                {
                    pair.Card.PivotOffset = new Vector2(40, 40);
                    var from = pair.Card.Position + pair.Card.PivotOffset;
                    var target = TextureLandmark(bottle, container.animation_mouth_uv) + new Vector2(0, 2);
                    var fly = PresentationTween();
                    fly.TweenMethod(Callable.From<float>(t => {
                        pair.Card.MouseFilter = MouseFilterEnum.Ignore;
                        float travel = Mathf.SmoothStep(0, 1, t);
                        pair.Card.Position = from.BezierInterpolate(from + new Vector2(0, -42),
                            target + new Vector2(0, -80), target, travel) - pair.Card.PivotOffset;
                        pair.Card.Scale = Vector2.One * Mathf.Lerp(1, .12f, Mathf.SmoothStep(.25f, 1, t));
                        pair.Card.Modulate = new Color(1, 1, 1, 1 - Mathf.SmoothStep(.82f, 1, t));
                    }), 0f, 1f, .78);
                }
            }
            // All groups fill concurrently, then every stopper falls concurrently.
            await PausePresentation(.83);
            if (!IsInsideTree()) return;
            for (int i = 0; i < bottles.Count && !_skipPresentation; i++)
            {
                var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, groups[i].Key));
                var container = PillContainerCatalog.LoadContainerDefinition(item.container_material_id)!;
                var stopperSize = new Vector2(48, 48);
                var stopper = IconRect(1);
                Vector2 sealedPosition;
                if (container.animation_from_closed_icon)
                {
                    var bottle = bottles[i]; var uv = container.animation_stopper_region_uv;
                    stopper.Texture = new AtlasTexture { Atlas = bottle.Texture,
                        Region = new Rect2(uv.Position * bottle.Texture.GetSize(), uv.Size * bottle.Texture.GetSize()), FilterClip = true };
                    stopperSize = TextureLandmark(bottle, uv.End) - TextureLandmark(bottle, uv.Position);
                    sealedPosition = TextureLandmark(bottle, uv.Position);
                }
                else
                {
                    stopper.Texture = GD.Load<Texture2D>(container.stopper_icon_path);
                    var mouthPoint = TextureLandmark(bottles[i], container.animation_mouth_uv);
                    sealedPosition = mouthPoint;
                }
                Place(_brewStage, stopper, Vector2.Zero, stopperSize);
                if (!container.animation_from_closed_icon) sealedPosition -= TextureLandmark(stopper, PorcelainStopperSeatUv);
                stopper.Position = sealedPosition - new Vector2(0, 45);
                stopper.SetMeta("bottle_stopper", true);
                stopper.PivotOffset = stopperSize * .5f; stopper.Rotation = -.10f;
                var seat = PresentationTween().SetParallel();
                seat.TweenProperty(stopper, "position", sealedPosition, .32)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                seat.TweenProperty(stopper, "rotation", 0f, .32)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            }
            await PausePresentation(.36);
            if (!IsInsideTree()) return;
            foreach (var node in _brewStage.GetChildren().OfType<Control>().Where(c => c.HasMeta("bottle_stopper"))) node.Hide();
            for (int i = 0; i < bottles.Count; i++)
            {
                var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, groups[i].Key));
                bottles[i].Texture = GD.Load<Texture2D>(item.icon_path);
                AlchemyPillVisual.Apply(bottles[i], groups[i].Key, item.pill_id);
            }
            await PausePresentation(.2);
            if (!IsInsideTree()) return;
        }
        StopPresentationTweens();
        foreach (var pair in cards) { pair.Card.Hide(); pair.Card.QueueFree(); }
        foreach (var bottle in bottles) { bottle.Hide(); bottle.QueueFree(); }
        foreach (var child in _brewStage.GetChildren().OfType<Control>().Where(c => c.HasMeta("bottle_stopper"))) child.QueueFree();
        skip.Hide(); caption.Text = pills.Length == 0 ? (result.Exploded ? "炸炉 · 未获丹药" : "未成丹") : "领取丹药"; caption.Show();
        OpenAlchemyClaim(result, pills);
        _brewing = false;
    }
}

