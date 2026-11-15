using Godot;

/// <summary>炼器演出独立样片。读取真实样方与成器快照，不执行人物开炼、发放或保存。</summary>
public partial class ForgingAnimationPreview : Control
{
    private const float Duration = 6.8f;
    private static readonly Vector2 WorkPoint = new(865, 480);
    private Control _canvas = null!;
    private TextureRect _sword = null!;
    private ShaderMaterial _formation = null!;
    private Label _result = null!;
    private Button _replay = null!;
    private Button _skip = null!;
    private readonly List<(AlchemyIngredientFlow View, float Delay, float Duration)> _ingredients = [];
    private ProcessForgingTrial.Artifact _artifact = null!;
    private float _clock;
    private string _capture = "";
    private int _frame;
    private float _nextCapture;
    private float _captureInterval = .08f;
    private bool _checking;
    private bool _reported;

    public override void _Ready()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            Theme = QuietRoomStyle.MakeTheme();
            var model = new ProcessForgingTrial();
            var draft = model.ExampleDraft(1);
            var outcome = model.Evaluate(draft);
            _artifact = outcome.Success ? outcome.Product! : throw new InvalidOperationException("样方未成器");
            var background = new TextureRect { Texture = GD.Load<Texture2D>(ForgingInkStyle.Assets + "ink_workshop.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore };
            AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var shade = new ColorRect { Color = new Color(.13f, .15f, .12f, .18f), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _canvas = QuietRoomStyle.Canvas(this);
            var ink = new ForgingProcessInk { Preview = this, Size = new(1672, 941), MouseFilter = MouseFilterEnum.Ignore };
            _canvas.AddChild(ink);
            var materials = draft.Inputs.Select(i => i.Material).Distinct().ToArray();
            for (int i = 0; i < materials.Length; i++)
            {
                var item = model.Materials[materials[i]];
                float offset = (i - (materials.Length - 1) * .5f) * 88;
                var flow = new AlchemyIngredientFlow { Name = $"ForgingMaterial{i + 1}",
                    Texture = GD.Load<Texture2D>(item.icon_path), Size = new(62, 62), Seed = i,
                    From = WorkPoint + new Vector2(offset, 13 + Math.Abs(offset) * .12f),
                    Mouth = WorkPoint + new Vector2((i - (materials.Length - 1) * .5f) * 16, -2),
                    LiftHeight = 34, EntryReach = 23, CurlStrength = .12f, FinalScale = .65f, TrailOpacity = .85f };
                _canvas.AddChild(flow);
                _ingredients.Add((flow, .4f + i * .075f, 1.55f + i % 3 * .035f));
            }
            var source = ForgedArtifactPresentation.Icon(_artifact)!;
            using var image = source.GetImage();
            var crop = new AtlasTexture { Atlas = source, Region = image.GetUsedRect(), FilterClip = true };
            _formation = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/forging_materialize.gdshader") };
            _sword = new TextureRect { Texture = crop, Material = _formation,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = new(130, 460), PivotOffset = new(65, 230), MouseFilter = MouseFilterEnum.Ignore };
            _canvas.AddChild(_sword);
            _result = QuietRoomStyle.Text("", 28); _result.HorizontalAlignment = HorizontalAlignment.Center;
            _canvas.AddChild(_result); _result.Position = new(470, 670); _result.Size = new(800, 50);
            _result.AddThemeColorOverride("font_color", new Color("303b34"));
            _skip = Button("跳过演出", () => _clock = Duration, new Vector2(1400, 48), new Vector2(200, 48));
            _replay = Button("重新观看", () => { _clock = 0; _reported = false; }, new Vector2(750, 775), new Vector2(230, 56));
            Button("〈 返回", () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"), new Vector2(35, 35), new Vector2(140, 48));
            var args = OS.GetCmdlineUserArgs();
            _checking = args.Contains("--forging-animation-check");
            var interval = args.FirstOrDefault(a => a.StartsWith("--forging-animation-capture-interval="))?.Split('=', 2)[1];
            if (uint.TryParse(interval, out var milliseconds)) _captureInterval = Math.Max(20u, milliseconds) / 1000f;
            _capture = args.FirstOrDefault(a => a.StartsWith("--forging-animation-capture="))?.Split('=', 2)[1] ?? "";
            if (_capture.Length > 0)
            {
                DirAccess.MakeDirRecursiveAbsolute(_capture);
                GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = new(1280, 720); GetWindow().Unfocusable = true;
            }
            GD.Print($"FORGING_ANIMATION_SAMPLE shape={_artifact.Shape} tier={_artifact.Tier} inputs={materials.Length} read_only=true");
            UpdatePresentation();
            if (_checking) _ = CheckPlayback();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private Button Button(string title, Action pressed, Vector2 position, Vector2 size)
    {
        var button = new Button { Text = title, Position = position, Size = size };
        ForgingInkStyle.Button(button); button.Pressed += pressed; _canvas.AddChild(button); return button;
    }

    private void UpdatePresentation()
    {
        foreach (var (view, delay, duration) in _ingredients)
        {
            float progress = Mathf.Clamp((_clock - delay) / duration, 0, 1);
            view.SetProgress(progress); view.Visible = progress < 1;
        }
        float form = Mathf.SmoothStep(2.6f, 4.65f, _clock);
        float cool = Mathf.SmoothStep(4.65f, 5.8f, _clock);
        float lift = Mathf.SmoothStep(5.7f, Duration, _clock);
        _formation.SetShaderParameter("reveal", form);
        _formation.SetShaderParameter("heat", 1 - cool);
        _sword.Scale = new Vector2(Mathf.Lerp(.92f, 1, form), Mathf.Lerp(.82f, 1, form));
        _sword.Position = WorkPoint - _sword.PivotOffset + new Vector2(0, -lift * 94 + Mathf.Sin(_clock * 2) * 1.5f * form * (1 - lift));
        _sword.Rotation = -Mathf.Pi * .5f + (1 - lift) * .08f;
        _sword.Modulate = new Color(1, 1, 1, Mathf.SmoothStep(2.6f, 3.2f, _clock));
        _result.Text = _clock >= Duration ? _artifact.Name + " · " + _artifact.Tier : "";
        _replay.Visible = _clock >= Duration;
        _skip.Visible = _clock < Duration;
    }

    public override void _Process(double delta)
    {
        if (_sword is null) return;
        _clock += (float)delta; UpdatePresentation();
        foreach (var child in _canvas.GetChildren().OfType<ForgingProcessInk>()) child.QueueRedraw();
        if (_clock >= Duration && !_reported) { _reported = true; GD.Print("FORGING_ANIMATION_PREVIEW_PASS completed=true"); }
        if (_capture.Length > 0 && DisplayServer.GetName() != "headless" && _clock >= _nextCapture)
        {
            _nextCapture = _clock + _captureInterval;
            using var frame = GetViewport().GetTexture().GetImage(); frame.SaveJpg($"{_capture}/frame{_frame++:D4}.jpg", .92f);
        }
        if (_capture.Length > 0 && _clock > Duration + 1) GetTree().Quit();
    }

    private async Task CheckPlayback()
    {
        try
        {
            int checks = 0;
            string identity = _artifact.InstanceId;
            void Verify(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
            async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            async Task Reach(float time) { while (_clock < time) await Frames(1); }
            foreach (float skipAt in new[] { 1.45f, 3.4f })
            {
                await Reach(skipAt); _skip.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                Verify(_ingredients.All(x => !x.View.Visible) && _result.Text == _artifact.Name + " · " + _artifact.Tier,
                    "跳过后材料残留或成器展示错误");
                Verify(_artifact.InstanceId == identity, "跳过重建了样片器物");
                _replay.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                Verify(_ingredients.All(x => x.View.Visible) && _result.Text.Length == 0 && !_replay.Visible,
                    "重播未恢复材料或提前显示结果");
            }
            await Reach(Duration + .1f);
            Verify(_artifact.InstanceId == identity && _ingredients.All(x => !x.View.Visible) && _replay.Visible,
                "自然完成或重播改变样片结果");
            GD.Print($"FORGING_ANIMATION_PLAYBACK_PASS checks={checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    public partial class ForgingProcessInk : Control
    {
        public ForgingAnimationPreview Preview = null!;
        private readonly Vector2[] _line = new Vector2[36];
        private readonly Vector2[] _billet = new Vector2[48];
        private void Oval(Vector2 center, Vector2 radius, Color color)
        {
            for (int i = 0; i < _line.Length; i++) { float a = i * Mathf.Tau / _line.Length; _line[i] = center + new Vector2(Mathf.Cos(a) * radius.X, Mathf.Sin(a) * radius.Y); }
            DrawColoredPolygon(_line, color);
        }
        public override void _Draw()
        {
            float time = Preview._clock;
            float active = Mathf.SmoothStep(.5f, 1.5f, time) * (1 - Mathf.SmoothStep(4.8f, 6.2f, time));
            Oval(WorkPoint + new Vector2(0, 25), new Vector2(220, 14), new Color(.16f, .19f, .16f, .10f + active * .13f));
            if (active <= .001f) return;
            // A restrained wash holds the material together; it never encodes tier, success or extra attributes.
            for (int strand = 0; strand < 5; strand++)
            {
                for (int j = 0; j < _line.Length; j++)
                {
                    float t = j / (_line.Length - 1f);
                    float x = (t - .5f) * Mathf.Lerp(165, 420, Mathf.SmoothStep(2.5f, 4.7f, time));
                    float y = Mathf.Sin(t * 12 - time * 3 + strand) * (12 + strand * 2) * Mathf.Sin(t * Mathf.Pi);
                    _line[j] = WorkPoint + new Vector2(x, y - strand * 3);
                }
                DrawPolyline(_line, new Color(.38f, .32f, .23f, active * .08f), 8 - strand, true);
            }
            float billet = Mathf.SmoothStep(1.15f, 2.15f, time) * (1 - Mathf.SmoothStep(3.65f, 4.6f, time));
            if (billet > 0)
            {
                float stretch = Mathf.SmoothStep(2.2f, 4.45f, time);
                float halfLength = Mathf.Lerp(74, 210, stretch);
                for (int layer = 0; layer < 3; layer++)
                {
                    float radius = 1 - layer * .13f;
                    for (int j = 0; j < _billet.Length; j++)
                    {
                        float a = j * Mathf.Tau / _billet.Length;
                        float ragged = 1 + Mathf.Sin(a * 7 + layer) * .035f + Mathf.Sin(a * 13) * .02f
                            + Mathf.Sin(a * 3 + time * 1.1f) * .015f;
                        _billet[j] = new Vector2(Mathf.Cos(a) * halfLength * radius * ragged,
                            Mathf.Sin(a) * Mathf.Lerp(15, 9, stretch) * radius * ragged);
                    }
                    DrawSetTransform(WorkPoint, (1 - stretch) * -.035f, Vector2.One);
                    var tint = layer == 0 ? new Color(.23f,.26f,.23f, billet * .20f)
                        : layer == 1 ? new Color(.40f,.30f,.21f, billet * .48f) : new Color(.60f,.47f,.30f, billet * .13f);
                    DrawColoredPolygon(_billet, tint);
                }
                DrawSetTransform(Vector2.Zero);
            }
            for (int i = 0; i < 16; i++)
            {
                float age = (time * .75f + i * .173f) % 1;
                float x = (i * 53 % 229 - 114) * (1 - age * .2f);
                var at = WorkPoint + new Vector2(x, -age * (27 + i % 5 * 8));
                DrawCircle(at, .7f + i % 3 * .35f, new Color(.58f, .37f, .16f, active * .45f * (1 - age)));
            }
        }
    }
}
