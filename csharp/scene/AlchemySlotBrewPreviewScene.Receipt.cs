using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class AlchemySlotBrewPreviewScene
{
    private bool _simpleAlchemyPresentation;
    private void BuildAlchemyPresentationMode()
    {
        var settings = new ConfigFile(); settings.Load("user://alchemy_presentation.cfg");
        _simpleAlchemyPresentation = settings.GetValue("view", "simple", false).AsBool();
        var toggle = new CheckButton { Text = "简洁展示", ButtonPressed = _simpleAlchemyPresentation };
        Place(_canvas, toggle, new Vector2(35, 100), new Vector2(190, 42));
        toggle.Toggled += value => { _simpleAlchemyPresentation = value; settings.SetValue("view", "simple", value); settings.Save("user://alchemy_presentation.cfg"); };
    }

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
            var close = MakeButton("返回", CloseBrewPresentation, 23); ApplyInkBrushButtonStyle(close);
            Place(_brewStage!, close, new Vector2(710, 825), new Vector2(280, 56));
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
        if (!claim.Present(recipient, rewards, OwnerCharacter is null ? "炼丹所得 · 独立试演" : "炼丹所得"))
        {
            _alchemyClaim = null; claim.QueueFree(); previewRecipient?.Free();
            var retry = MakeButton("重新打开领取", () => { OpenAlchemyClaim(result, pills); }, 23);
            Place(_brewStage!, retry, new Vector2(710, 825), new Vector2(280, 56));
        }
        else _brewStage?.Hide();
    }

    // Calibrated landmarks inside the existing PNGs, including transparent padding.
    private static readonly Vector2 PorcelainMouthUv = new(.49f, .168f);
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
        ColorRect shade, AlchemyInkVortex currents, ColorRect fire)
    {
        // Every path reveals the already committed transaction; receipt never grants items again.
        bool simple = _simpleAlchemyPresentation;
        if (simple) _skipPresentation = false;
        var ordered = simple ? pills.OrderByDescending(p => p.Quality).ToArray() : pills;
        var cards = new List<(Control Card, AlchemySlotSimulation.PillResult Pill)>();
        foreach (var tween in _presentationTweens) if (tween.IsValid()) tween.Kill();
        foreach (var child in _brewStage!.GetChildren().OfType<Control>())
            if (child.HasMeta("alchemy_flying_ingredient")) { child.Hide(); child.QueueFree(); }
        currents.Hide(); fire.Hide(); shade.Color = new Color(.055f, .065f, .06f, .76f);
        Vector2[] scatter = [new(-145, 22), new(35, -44), new(165, 53), new(-12, 110)];
        for (int i = 0; i < ordered.Length; i++)
        {
            var pill = ordered[i];
            var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, AlchemyQualityRules.Tier(pill.Quality)));
            var card = new Control { Name = $"RevealedPill{i + 1}", MouseFilter = MouseFilterEnum.Stop };
            Vector2 end = simple ? new Vector2(625, 245 + i * 103) : new Vector2(805, 490) + scatter[i % scatter.Length];
            Place(_brewStage, card, mouth - new Vector2(40, 40), new Vector2(simple ? 400 : 80, 80));
            var icon = IconRect(68); icon.Texture = GD.Load<Texture2D>(AnimationPillTexturePath);
            Place(card, icon, new Vector2(6, 6), new Vector2(68, 68));
            if (item.efficacy_tier >= 3)
            {
                var aura = new AlchemyPillAura { Full = item.efficacy_tier == 4, MouseFilter = MouseFilterEnum.Ignore };
                Place(card, aura, new Vector2(-10, -10), new Vector2(100, 100)); card.MoveChild(aura, 0);
            }
            var hover = AlchemyPillVisual.Hover(item, pill.Quality);
            HoverInfoManager.SetInfo(card, hover with { Icon = icon.Texture });
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
                var start = mouth - new Vector2(40, 40);
                var flight = PresentationTween();
                flight.TweenMethod(Callable.From<float>(t => {
                    card.Position = start.Lerp(end, t) - new Vector2(0, Mathf.Sin(t * Mathf.Pi) * 85);
                    card.Scale = Vector2.One * Mathf.Lerp(.2f, 1, Math.Min(1, t * 4));
                }), 0f, 1f, .85);
                await PausePresentation(.95);
                if (!IsInsideTree()) return;
            }
            card.Position = end; card.Scale = Vector2.One;
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
                var bottle = IconRect(100); bottle.Texture = GD.Load<Texture2D>(container.open_bottle_icon_path);
                var position = new Vector2(835 + (i - (groups.Length - 1) * .5f) * 180 - 50, 690);
                Place(_brewStage, bottle, position, new Vector2(100, 100)); bottles.Add(bottle);
                foreach (var pair in cards.Where(c => AlchemyQualityRules.Tier(c.Pill.Quality) == groups[i].Key))
                {
                    pair.Card.PivotOffset = new Vector2(40, 40);
                    var from = pair.Card.Position + pair.Card.PivotOffset;
                    var target = TextureLandmark(bottle, PorcelainMouthUv) + new Vector2(0, 2);
                    var fly = PresentationTween();
                    fly.TweenMethod(Callable.From<float>(t => {
                        pair.Card.Position = from.Lerp(target, t) - new Vector2(0, Mathf.Sin(t * Mathf.Pi) * 45) - pair.Card.PivotOffset;
                        pair.Card.Scale = Vector2.One * Mathf.Lerp(1, .12f, Mathf.SmoothStep(0, 1, t));
                        pair.Card.Modulate = new Color(1, 1, 1, 1 - Mathf.SmoothStep(.82f, 1, t));
                    }), 0f, 1f, .7);
                }
            }
            // All groups fill concurrently, then every stopper falls concurrently.
            await PausePresentation(.75);
            for (int i = 0; i < bottles.Count && !_skipPresentation; i++)
            {
                var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, groups[i].Key));
                var container = PillContainerCatalog.LoadContainerDefinition(item.container_material_id)!;
                var stopperSize = new Vector2(48, 48);
                var stopper = IconRect(48); stopper.Texture = GD.Load<Texture2D>(container.stopper_icon_path);
                var mouthPoint = TextureLandmark(bottles[i], PorcelainMouthUv);
                Place(_brewStage, stopper, Vector2.Zero, stopperSize);
                var seatOffset = TextureLandmark(stopper, PorcelainStopperSeatUv);
                var sealedPosition = mouthPoint - seatOffset;
                stopper.Position = sealedPosition - new Vector2(0, 45);
                stopper.SetMeta("bottle_stopper", true);
                PresentationTween().TweenProperty(stopper, "position", sealedPosition, .3);
            }
            await PausePresentation(.35);
            foreach (var node in _brewStage.GetChildren().OfType<Control>().Where(c => c.HasMeta("bottle_stopper"))) node.Hide();
            for (int i = 0; i < bottles.Count; i++)
            {
                var item = GD.Load<PillItemData>(AlchemyProductCatalog.Path(result.ProductId, groups[i].Key));
                bottles[i].Texture = GD.Load<Texture2D>(item.icon_path);
                AlchemyPillVisual.Apply(bottles[i], groups[i].Key, item.pill_id);
            }
            await PausePresentation(.2);
        }
        foreach (var tween in _presentationTweens) if (tween.IsValid()) tween.Kill();
        foreach (var pair in cards) { pair.Card.Hide(); pair.Card.QueueFree(); }
        foreach (var bottle in bottles) { bottle.Hide(); bottle.QueueFree(); }
        foreach (var child in _brewStage.GetChildren().OfType<Control>().Where(c => c.HasMeta("bottle_stopper"))) child.QueueFree();
        skip.Hide(); caption.Text = pills.Length == 0 ? (result.Exploded ? "炸炉 · 未获丹药" : "未成丹") : "领取丹药"; caption.Show();
        OpenAlchemyClaim(result, pills);
        _brewing = false;
    }
}

