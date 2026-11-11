using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>在原炼器页面的中央加入演出层，不制作或替换两侧UI。</summary>
public partial class ForgingNativeFirePresentation : Control
{
    public const float Duration = 6.8f;
    private static readonly Vector2 HeatPoint = new(868, 408);
    private readonly List<(AlchemyIngredientFlow View, float Delay)> _materials = [];
    private TextureRect _fire = null!, _foreground = null!, _sword = null!;
    private ShaderMaterial _formation = null!;
    private Label _phase = null!, _result = null!;
    private Panel _resultPaper = null!;
    private Button _skip = null!, _back = null!;
    private float _clock = -1;
    private bool _playing;
    public Action? Returned;
    public float Clock => _clock;
    public bool Finished => _clock >= Duration;

    public override void _Ready()
    {
        Size = new(1672, 941); MouseFilter = MouseFilterEnum.Ignore;
        const string assets = "res://textures/ui/forging_fire_20261107/";
        Picture(GD.Load<Texture2D>(assets + "hearth_empty_v1.png"), new(578, 218), new(580, 386.6667f));
        _fire = Picture(GD.Load<Texture2D>(assets + "flame_amber_loop_2x2_v1.png"), new(643, 176), new(450, 300));
        _fire.Material = GD.Load<ShaderMaterial>(assets + "flame_amber_flow_v2.tres").Duplicate() as ShaderMaterial;
        _fire.Modulate = new(1, 1, 1, .65f);
        _formation = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/forging_materialize.gdshader") };
        _sword = Picture(null, Vector2.Zero, new(130, 460)); _sword.Material = _formation;
        _sword.PivotOffset = new(65, 230); _sword.Visible = false;
        _foreground = Picture(GD.Load<Texture2D>(assets + "hearth_foreground_v1.tres"), new(578, 421.90625f), new(580, 182.7604f));
        _phase = QuietRoomStyle.Text("", 22); _phase.Position = new(575, 130); _phase.Size = new(585, 34); _phase.Visible = false; AddChild(_phase);
        _resultPaper = new Panel { Position = new(552, 610), Size = new(632, 64), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _resultPaper.AddThemeStyleboxOverride("panel", ForgingSurfacePalette.PanelPaper(padding: 0, plain: true));
        AddChild(_resultPaper); QuietRoomStyle.Frame(_resultPaper);
        _result = QuietRoomStyle.Text("", 27); _result.HorizontalAlignment = HorizontalAlignment.Center;
        _result.Position = new(13, 7); _result.Size = new(606, 50); _resultPaper.AddChild(_result);
        _skip = ActionButton("跳过演出", new(1400, 40), new(200, 48), () => { _clock = Duration; _playing = false; Present(); });
        _back = ActionButton("返回投料", new(760, 778), new(210, 52), ReturnToInputs);
        _skip.Visible = _back.Visible = false;
    }

    private TextureRect Picture(Texture2D? texture, Vector2 position, Vector2 size)
    {
        var node = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, Texture = texture, Position = position,
            Size = size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(node); return node;
    }

    private Button ActionButton(string text, Vector2 position, Vector2 size, Action action)
    {
        var button = new Button { Text = text, Position = position, Size = size };
        ForgingSurfacePalette.Button(button); button.Pressed += action; AddChild(button); return button;
    }

    public void Play(ProcessForgingTrial model, ProcessForgingTrial.Draft draft, ProcessForgingTrial.Artifact artifact)
    {
        foreach (var old in _materials) { RemoveChild(old.View); old.View.QueueFree(); } _materials.Clear();
        var names = draft.Inputs.Select(x => x.Material).Distinct().ToArray();
        for (int i = 0; i < names.Length; i++)
        {
            float offset = (i - (names.Length - 1) * .5f) * 68;
            var flow = new AlchemyIngredientFlow { Texture = GD.Load<Texture2D>(model.Materials[names[i]].icon_path),
                Size = new(62, 62), Seed = i, From = HeatPoint + new Vector2(offset, -70 + Math.Abs(offset) * .10f),
                Mouth = HeatPoint + new Vector2((i - (names.Length - 1) * .5f) * 12, 0),
                LiftHeight = 34, EntryReach = 23, CurlStrength = .12f, FinalScale = .65f, TrailOpacity = .85f };
            AddChild(flow); _materials.Add((flow, .4f + i * .075f));
        }
        MoveChild(_foreground, GetChildCount() - 1);
        var source = ForgedArtifactPresentation.Icon(artifact)!;
        using var image = source.GetImage();
        _sword.Texture = new AtlasTexture { Atlas = source, Region = image.GetUsedRect(), FilterClip = true };
        _result.Text = artifact.Name + " · " + artifact.Tier;
        _clock = 0; _playing = true; _sword.Visible = true; _phase.Visible = true; Present();
    }

    public override void _Process(double delta)
    {
        if (!_playing) return;
        _clock = Math.Min(Duration, _clock + (float)delta);
        if (_clock >= Duration) _playing = false;
        Present();
    }

    private void Present()
    {
        foreach (var (view, delay) in _materials)
        {
            float progress = Mathf.Clamp((_clock - delay) / 1.55f, 0, 1);
            view.SetProgress(progress); view.Visible = progress < 1;
        }
        float form = Mathf.SmoothStep(2.6f, 4.65f, _clock), cool = Mathf.SmoothStep(4.55f, 5.8f, _clock), lift = Mathf.SmoothStep(5.7f, Duration, _clock);
        _formation.SetShaderParameter("reveal", form); _formation.SetShaderParameter("heat", 1 - cool);
        _sword.Scale = new(Mathf.Lerp(.92f, 1, form), Mathf.Lerp(.82f, 1, form));
        _sword.Position = HeatPoint - _sword.PivotOffset + new Vector2(0, -lift * 94);
        _sword.Rotation = -Mathf.Pi * .5f + (1 - lift) * .08f;
        _sword.Modulate = new(1, 1, 1, Mathf.SmoothStep(2.6f, 3.2f, _clock));
        _fire.Modulate = new(1, 1, 1, 1 - cool);
        _phase.Text = _clock < 1.1f ? "投料" : _clock < 2.6f ? "炼合" : _clock < 4.65f ? "凝形" : _clock < 5.8f ? "收火" : "定器";
        _skip.Visible = !Finished; _back.Visible = _resultPaper.Visible = Finished;
    }

    public void Skip() => _skip.EmitSignal(BaseButton.SignalName.Pressed);
    public void ReturnToInputs()
    {
        _clock = -1; _playing = false;
        foreach (var material in _materials) material.View.Visible = false;
        _sword.Visible = _phase.Visible = _resultPaper.Visible = _skip.Visible = _back.Visible = false;
        _fire.Modulate = new(1, 1, 1, .65f);
        Returned?.Invoke();
    }
}
