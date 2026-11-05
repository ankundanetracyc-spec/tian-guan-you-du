using Godot;

/// <summary>云息灵气的共用表现；参数均为画面参数，不写入人物或突破结算。</summary>
public partial class RealmQiVisual : ColorRect
{
    private ShaderMaterial? _qiMaterial;
    private double _time;
    private double _rotationPhase;
    private double _innerRotationPhase;
    private float _rotationSpeed = .055f;
    private float _innerRotationSpeed = -.075f;
    private float _radius = .09f;
    private float _density = .28f;
    private float _core;
    private float _heat;
    private float _pull;
    private float _rebound;
    private float _pathVisibility;
    private float _pathProgress;
    private Tween? _transition;

    public bool IsTransitioning => _transition?.IsRunning() == true;
    /// <summary>仅供境界演出提高主体辨识度；普通展示、牌局及养息页默认保持原样。</summary>
    public void SetPresentationFocus(float amount) => _qiMaterial?.SetShaderParameter("presentation_focus", Mathf.Clamp(amount, 0f, 1f));

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        // 着色器在fragment中自行输出颜色；载入失败时底板应透明，不能遮住纸面。
        Color = Colors.Transparent;
        _qiMaterial = new ShaderMaterial
        {
            Shader = ResourceLoader.Load<Shader>("res://shaders/realm_qi_cloud.gdshader")
        };
        Material = _qiMaterial;
        _qiMaterial.SetShaderParameter("swirl_texture", ResourceLoader.Load<Texture2D>("res://textures/particle_vfx/1024/alpha/effect_4.png"));
        Resized += ApplyParameters;
        ApplyParameters();
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _time += delta;
        // 积分转角保证加减速连续；不能用总时间乘以变化的转速，否则会瞬间跳转。
        var pull = _pull * (1f - _rebound * .65f);
        var response = 1f - Mathf.Exp(-(float)delta * 6f);
        _rotationSpeed = Mathf.Lerp(_rotationSpeed, .055f + pull * 1.85f, response);
        _innerRotationSpeed = Mathf.Lerp(_innerRotationSpeed, -.075f + pull * 1.55f, response);
        _rotationPhase += _rotationSpeed * delta;
        _innerRotationPhase += _innerRotationSpeed * delta;
        _qiMaterial?.SetShaderParameter("animation_time", (float)_time);
        _qiMaterial?.SetShaderParameter("rotation_phase", (float)_rotationPhase);
        _qiMaterial?.SetShaderParameter("inner_rotation_phase", (float)_innerRotationPhase);
    }

    public void ShowCloud(float accumulation)
    {
        StopTransition();
        SetCloud(accumulation);
    }

    private void SetCloud(float accumulation)
    {
        var amount = Mathf.Clamp(accumulation, 0f, 1f);
        _radius = .09f + .21f * Mathf.Pow(amount, .55f);
        _density = .28f + .46f * amount;
        _core = 0f;
        _heat = 0f;
        _pull = 0f;
        _rebound = 0f;
        _pathVisibility = 0f;
        ApplyParameters();
    }

    public void ShowCore()
    {
        StopTransition();
        SetCore();
    }

    /// <summary>牌局按凝核程度呈现逐步压聚；未定形前始终为云，不提前生成小核。</summary>
    public void ShowCompression(int score)
    {
        StopTransition();
        var amount = 1f - 1f / (1f + Mathf.Max(0, score) / 100f);
        _radius = Mathf.Lerp(.30f, .085f, amount);
        _density = Mathf.Lerp(.74f, 1f, amount);
        _core = 0f; _heat = 0f; _pull = .15f + amount * .55f;
        _rebound = 0f; _pathVisibility = 0f;
        ApplyParameters();
    }

    private void SetCore()
    {
        _radius = .055f;
        _density = 1f;
        _core = 1f;
        _heat = 0f;
        _pull = 0f;
        _rebound = 0f;
        _pathVisibility = 0f;
        ApplyParameters();
    }

    /// <summary>后续境界的纯视觉接口；不读取或写入突破结算，待分层规则确认后接入。</summary>
    public void ShowPath(float progress)
    {
        StopTransition();
        SetCore();
        _pathVisibility = 1f;
        _pathProgress = Mathf.Clamp(progress, 0f, 1f);
        ApplyParameters();
    }

    public void ShowBreaths(int count)
    {
        _qiMaterial?.SetShaderParameter("breath_count", Mathf.Clamp(count, 0, 9));
        _qiMaterial?.SetShaderParameter("breath_previous_count", Mathf.Clamp(count, 0, 9));
        _qiMaterial?.SetShaderParameter("breath_reveal", 1f);
    }

    public void PlayPathGrowth(float from, float to, Action completed)
    {
        ShowPath(from); ShowBreaths(0);
        _transition = CreateTween();
        _transition.TweenMethod(Callable.From<float>(p => { _pathProgress = Mathf.Lerp(from, to, p); ApplyParameters(); }), 0f, 1f, 2.8)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _transition.TweenCallback(Callable.From(completed));
    }

    /// <summary>只演出已经结算的息数变化，不推进时间或修改修行进度。</summary>
    public void PlayBreathGain(int from, int to, Action completed)
    {
        ShowPath(1f); ShowBreaths(to);
        _qiMaterial?.SetShaderParameter("breath_previous_count", Mathf.Clamp(from, 0, 9));
        _qiMaterial?.SetShaderParameter("breath_reveal", 0f);
        _transition = CreateTween();
        _transition.TweenMethod(Callable.From<float>(p => _qiMaterial?.SetShaderParameter("breath_reveal", p)), 0f, 1f, 3.2)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _transition.TweenCallback(Callable.From(() => { ShowBreaths(to); completed(); }));
    }

    public void PlayAccumulation(float from, float to, Action completed)
    {
        StopTransition();
        SetCloud(from);
        _transition = CreateTween();
        _transition.TweenMethod(Callable.From<float>(progress =>
        {
            SetCloud(Mathf.Lerp(from, to, progress));
            _rebound = Mathf.Sin(progress * Mathf.Pi) * .16f;
            ApplyParameters();
        }), 0f, 1f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _transition.TweenCallback(Callable.From(() => { SetCloud(to); completed(); }));
    }

    /// <summary>只演出已结算的六至七层变化，不能由动画决定成核或增减灵力。</summary>
    public void PlayCondensation(Action<string> phaseChanged, Action completed)
    {
        StopTransition();
        SetCloud(1f);
        phaseChanged("灵云向内收拢");
        _transition = CreateTween();
        _transition.TweenMethod(Callable.From<float>(progress =>
        {
            _radius = Mathf.Lerp(.30f, .185f, progress);
            _density = Mathf.Lerp(.74f, .87f, progress);
            _pull = progress;
            ApplyParameters();
        }), 0f, 1f, 2.2).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _transition.TweenCallback(Callable.From(() => phaseChanged("压聚的灵气向外回弹")));
        _transition.TweenMethod(Callable.From<float>(progress =>
        {
            _rebound = Mathf.Sin(progress * Mathf.Pi) * .62f;
            ApplyParameters();
        }), 0f, 1f, .8);
        _transition.TweenCallback(Callable.From(() => phaseChanged("散气重新引回，中心逐渐紧密")));
        _transition.TweenMethod(Callable.From<float>(progress =>
        {
            var eased = progress * progress * (3f - 2f * progress);
            _radius = Mathf.Lerp(.185f, .055f, eased);
            _density = Mathf.Lerp(.87f, 1f, eased);
            _core = eased;
            _rebound = 0f;
            ApplyParameters();
        }), 0f, 1f, 2.1);
        _transition.TweenCallback(Callable.From(() => phaseChanged("灵核凝定，气华骤明")));
        _transition.TweenMethod(Callable.From<float>(progress =>
        {
            _heat = progress;
            _pull = 1f - progress * .35f;
            ApplyParameters();
        }), 0f, 1f, .2).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _transition.TweenInterval(.3);
        _transition.TweenCallback(Callable.From(() => phaseChanged("气华渐敛，灵核自持")));
        _transition.TweenMethod(Callable.From<float>(progress =>
        {
            _heat = (1f-progress)*(1f-progress);
            _pull = .65f * (1f-progress);
            ApplyParameters();
        }), 0f, 1f, 2.8);
        _transition.TweenCallback(Callable.From(() => { SetCore(); completed(); }));
    }

    public void StopTransition()
    {
        _transition?.Kill();
        _transition = null;
    }

    private void ApplyParameters()
    {
        if (_qiMaterial is null) return;
        _qiMaterial.SetShaderParameter("cloud_radius", _radius);
        _qiMaterial.SetShaderParameter("cloud_density", _density);
        _qiMaterial.SetShaderParameter("core_amount", _core);
        _qiMaterial.SetShaderParameter("core_heat", _heat);
        _qiMaterial.SetShaderParameter("inward_pull", _pull);
        _qiMaterial.SetShaderParameter("rebound", _rebound);
        _qiMaterial.SetShaderParameter("path_visibility", _pathVisibility);
        _qiMaterial.SetShaderParameter("path_progress", _pathProgress);
        _qiMaterial.SetShaderParameter("aspect_ratio", Size.X / Mathf.Max(1f, Size.Y));
    }
}
