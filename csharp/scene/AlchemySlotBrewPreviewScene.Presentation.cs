using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class AlchemySlotBrewPreviewScene
{
    private const string AnimationPillTexturePath = InventoryIconDensityPack.AnimationPillPath;
    private Control? _brewStage;
    private bool _skipPresentation;
    private readonly List<Tween> _presentationTweens = [];
    private readonly List<CanvasItem> _presentationHiddenUi = [];
    private Tween PresentationTween() { var tween = CreateTween(); _presentationTweens.Add(tween); return tween; }

    private void StopPresentationTweens()
    {
        foreach (var tween in _presentationTweens) if (tween.IsValid()) tween.Kill();
        _presentationTweens.Clear();
    }

    private void HidePresentationUi()
    {
        foreach (var item in _canvas.GetChildren().OfType<CanvasItem>())
            if (item != _furnace && item != _brewStage && item.Visible)
            {
                _presentationHiddenUi.Add(item);
                item.Hide();
            }
    }

    private void RestorePresentationUi()
    {
        foreach (var item in _presentationHiddenUi)
            if (IsInstanceValid(item)) item.Show();
        _presentationHiddenUi.Clear();
    }

    private void SkipBrewPresentation()
    {
        _skipPresentation = true;
        StopPresentationTweens();
        _furnace.SetState(.45, 30);
    }

    private void TweenFurnace(float from, float to, double seconds)
    {
        PresentationTween().TweenMethod(Callable.From<float>(power =>
            _furnace.SetState(power, 30 + power * 15)), from, to, seconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private async Task BrewWithPresentation()
    {
        if (_brewing || _brewStage is not null) return;
        RefreshSummary();
        if (_brewButton.Disabled) { ShowMessage("暂不能开炉", _brewCondition.Text); return; }
        var inputs = GetSimulationInputs();
        var sources = inputs.Select(input =>
        {
            var choice = _materials.First(m => m.Path == input.Path);
            var slot = input.IsMain ? _mainIcon : _assistantIcons[Array.FindIndex(_assistants, m => m?.Path == input.Path)];
            return (Texture: MaterialIcon(choice), Size: slot.Size,
                Position: _canvas.GetGlobalTransform().AffineInverse() * slot.GetGlobalRect().GetCenter());
        }).ToArray();
        _skipPresentation = false; StopPresentationTweens();
        _brewing = true;
        RefreshSummary();
        HideHover(); HoverInfoManager.HideAll();
        AlchemySlotSimulation.Result? committedResult = null;
        try
        {
            AlchemySlotSimulation.Result? result;
            if (OwnerCharacter is not null)
            {
                if (!OwnerCharacter.TryBrewAlchemy(inputs, out result, out var reason))
                { ShowMessage("暂不能开炉", reason); return; }
            }
            else result = AlchemySlotSimulation.Simulate(inputs, Attainment, TotalSpirit, OtherSpiritOccupation);
            if (result is null || !result.CanSimulate)
            { ShowMessage("暂不能开炉", result?.BlockingReason ?? "未开始炼制。"); return; }
            committedResult = result;
            // The transaction commits exactly once. Animation only reveals the committed result.
            HidePresentationUi();
            _mainIcon.Modulate = Colors.Transparent;
            foreach (var icon in _assistantIcons) icon.Modulate = Colors.Transparent;
            _brewStage = new Control { Name = "AlchemyBrewPresentation", ZIndex = 180, MouseFilter = MouseFilterEnum.Stop };
            _canvas.AddChild(_brewStage); _brewStage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var shade = new ColorRect { Color = new Color(.055f, .065f, .06f, 0), MouseFilter = MouseFilterEnum.Stop };
            _brewStage.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var mouth = _canvas.GetGlobalTransform().AffineInverse() * _furnace.MouthGlobalPosition;
            var caption = QuietRoomStyle.Text("", 29);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.AddThemeColorOverride("font_color", new Color("eee5ce"));
            Place(_brewStage, caption, new Vector2(410, 120), new Vector2(850, 55));
            var skip = MakeButton("跳过演出 · 查看获得", SkipBrewPresentation, 18);
            skip.AddThemeColorOverride("font_color", new Color("eee5ce"));
            skip.AddThemeColorOverride("font_hover_color", Colors.White);
            Place(_brewStage, skip, new Vector2(1310, 70), new Vector2(300, 48));
            var pills = result.Pills.Where(p => p.Quality >= 20).ToArray();
            if (!_simpleAlchemyPresentation)
            {
                var currents = new AlchemyInkVortex { Name = "IngredientInkCurrent", Position = mouth,
                    MouseFilter = MouseFilterEnum.Ignore };
                _brewStage.AddChild(currents);
                PresentationTween().TweenProperty(shade, "color:a", .30f, .45)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                TweenFurnace(.45f, 1.8f, .85);
                double intakeDuration = 0;
                for (int i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    var icon = new AlchemyIngredientFlow { Name = "FlyingIngredient", Texture = source.Texture,
                        From = source.Position, Mouth = mouth, Seed = i, Size = source.Size };
                    icon.SetMeta("alchemy_flying_ingredient", true);
                    _brewStage.AddChild(icon);
                    double delay = i * .14, duration = 1.55 + i % 3 * .07;
                    intakeDuration = Math.Max(intakeDuration, delay + duration);
                    var flight = PresentationTween(); flight.TweenInterval(delay);
                    flight.TweenMethod(Callable.From<float>(icon.SetProgress), 0f, 1f, duration);
                    flight.TweenCallback(Callable.From(icon.QueueFree));
                }
                await PausePresentation(intakeDuration + .06);
                if (!IsInsideTree()) return;
                currents.Dissipating = true;
                if (!_skipPresentation)
                {
                    // Animate the approved fire behind the furnace shell, never above the lid.
                    TweenFurnace(1.8f, 3.5f, .4);
                    await PausePresentation(.45);
                    if (!IsInsideTree()) return;
                    if (!_skipPresentation)
                    {
                        PresentationTween().TweenMethod(Callable.From<float>(t =>
                            _furnace.SetState(3.35f + Mathf.Sin(t * Mathf.Tau) * .15f, 80)), 0f, 1f, .7);
                        await PausePresentation(.75);
                        if (!IsInsideTree()) return;
                    }
                    if (!_skipPresentation)
                    {
                        TweenFurnace(3.35f, .35f, .65);
                        await PausePresentation(.7);
                        if (!IsInsideTree()) return;
                    }
                }
                currents.Hide(); currents.QueueFree();
            }
            await InspectBottleAndReceive(result, pills, mouth, skip, caption, shade);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            if (IsInsideTree())
            {
                if (committedResult is not null)
                {
                    // 演出中断也必须保留本炉成品；事务只扣料，领取才入包。
                    StopPresentationTweens();
                    if (_brewStage is null)
                    {
                        _brewStage = new Control { Name = "AlchemyBrewRecovery", ZIndex = 180, MouseFilter = MouseFilterEnum.Stop };
                        _canvas.AddChild(_brewStage); _brewStage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                    }
                    OpenAlchemyClaim(committedResult, committedResult.Pills.Where(p => p.Quality >= 20).ToArray());
                }
                else { CloseBrewPresentation(); ShowMessage("呈现中断", "未能展示本次炼丹，请重新查看配伍。" ); }
            }
        }
        finally { _brewing = false; if (IsInsideTree()) RefreshSummary(); }
    }

    private async Task PausePresentation(double seconds)
    {
        // Match Tween's frame clock; wall-clock deadlines can cut off motion after a slow frame.
        double elapsed = 0;
        while (!_skipPresentation && elapsed < seconds && IsInsideTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInsideTree()) return;
            elapsed += GetProcessDeltaTime();
        }
    }

    private void CloseBrewPresentation()
    {
        HoverInfoManager.HideAll();
        StopPresentationTweens();
        _brewStage?.QueueFree(); _brewStage = null;
        RestorePresentationUi();
        _mainIcon.Modulate = Colors.White;
        foreach (var icon in _assistantIcons) icon.Modulate = Colors.White;
        _furnace.SetState(.45, 30);
        RefreshSummary();
    }
}
