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
    private readonly System.Collections.Generic.List<Tween> _presentationTweens = [];
    private Tween PresentationTween() { var tween = CreateTween(); _presentationTweens.Add(tween); return tween; }


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
            return (Texture: MaterialIcon(choice), Position: _canvas.GetGlobalTransform().AffineInverse() * slot.GetGlobalRect().GetCenter());
        }).ToArray();
        _skipPresentation = _simpleAlchemyPresentation; _presentationTweens.Clear();
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
            _mainIcon.Modulate = Colors.Transparent;
            foreach (var icon in _assistantIcons) icon.Modulate = Colors.Transparent;
            _brewStage = new Control { Name = "AlchemyBrewPresentation", ZIndex = 180, MouseFilter = MouseFilterEnum.Stop };
            _canvas.AddChild(_brewStage); _brewStage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var shade = new ColorRect { Color = new Color(.055f, .065f, .06f, 0), MouseFilter = MouseFilterEnum.Stop };
            _brewStage.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var mouth = _canvas.GetGlobalTransform().AffineInverse() * _furnace.MouthGlobalPosition;
            var currents = new AlchemyInkVortex { Position = mouth, MouseFilter = MouseFilterEnum.Ignore };
            _brewStage.AddChild(currents);
            var caption = QuietRoomStyle.Text("", 29);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.AddThemeColorOverride("font_color", new Color("eee5ce"));
            Place(_brewStage, caption, new Vector2(410, 120), new Vector2(850, 55));
            var skip = MakeButton("跳过演出 · 查看获得", () => _skipPresentation = true, 18);
            Place(_brewStage, skip, new Vector2(1310, 70), new Vector2(300, 48));
            var dim = PresentationTween(); dim.TweenProperty(shade, "color:a", .38f, .55);
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                var icon = IconRect(82); icon.Name = "FlyingIngredient"; icon.Texture = source.Texture;
                icon.SetMeta("alchemy_flying_ingredient", true);
                Place(_brewStage, icon, source.Position - new Vector2(41, 41), new Vector2(82, 82));
                icon.PivotOffset = new Vector2(41, 41);
                var flight = PresentationTween(); flight.TweenInterval(i * .12);
                flight.TweenMethod(Callable.From<float>(t =>
                {
                    var offset = source.Position - mouth;
                    var pos = mouth + offset.Rotated(t * Mathf.Tau * 1.15f) * Mathf.Pow(1 - t, 1.65f);
                    icon.Position = pos - new Vector2(41, 41);
                    icon.Scale = Vector2.One * (1 - .86f * t);
                    icon.Rotation = t * 2.6f;
                    icon.Modulate = new Color(1, 1, 1, 1 - t * t);
                }), 0f, 1f, 1.25);
                flight.TweenCallback(Callable.From(icon.QueueFree));
            }
            _furnace.SetState(2.4, 70);
            await PausePresentation(1.3 + sources.Length * .12);
            if (!IsInsideTree()) return;
            currents.Dissipating = true;

            var fireMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/alchemy_furnace_fire.gdshader") };
            fireMaterial.SetShaderParameter("power", .78f);
            fireMaterial.SetShaderParameter("use_engine_time", true);
            var fire = new ColorRect { Visible = false, Material = fireMaterial, MouseFilter = MouseFilterEnum.Ignore };
            Place(_brewStage, fire, mouth - new Vector2(115, 190), new Vector2(230, 190));
            // Shader TIME animates independently of any gameplay operation; no additive blending.
            await PausePresentation(1.15);
            if (!IsInsideTree()) return;

            var fadeFire = PresentationTween(); fadeFire.TweenProperty(fire, "modulate:a", 0f, .65);
            _furnace.SetState(.7, 45);
            await PausePresentation(.7);
            if (!IsInsideTree()) return;
            var revealShade = PresentationTween(); revealShade.TweenProperty(shade, "color:a", .76f, .6);
            var pills = result.Pills.Where(p => p.Quality >= 20).ToArray();
            await InspectBottleAndReceive(result, pills, mouth, skip, caption, shade, currents, fire);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            if (IsInsideTree())
            {
                if (committedResult is not null)
                {
                    // 演出中断也必须保留本炉成品；事务只扣料，领取才入包。
                    foreach (var tween in _presentationTweens) if (tween.IsValid()) tween.Kill();
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
        double until = Time.GetTicksMsec() + seconds * 1000;
        while (!_skipPresentation && Time.GetTicksMsec() < until && IsInsideTree())
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void CloseBrewPresentation()
    {
        HoverInfoManager.HideAll();
        foreach (var tween in _presentationTweens) if (tween.IsValid()) tween.Kill();
        _presentationTweens.Clear();
        _brewStage?.QueueFree(); _brewStage = null;
        _mainIcon.Modulate = Colors.White;
        foreach (var icon in _assistantIcons) icon.Modulate = Colors.White;
        _furnace.SetState(.45, 30);
        RefreshSummary();
    }
}
