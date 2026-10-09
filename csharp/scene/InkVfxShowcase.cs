using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 运行时水墨特效实验室。首版采用程序化墨形 + 单 CanvasItem Shader，
/// 用低成本方式比较扩散、笔锋、爆散和干湿边缘的观感。
/// </summary>
public partial class InkVfxShowcase : Control
{
    private const string BackdropPath = "res://textures/start_menu/ink_mountain_dawn_v2.png";
    private const string ShaderPath = "res://shaders/ink_bleed_showcase.gdshader";
    private const int MaskWidth = 512;
    private const int MaskHeight = 288;

    private enum PreviewType { Shader, Shield, Death, Punch, SwordCleave, SwordImpact, Impact, CriticalImpact, SwordExchange, RealSkill }

    private readonly record struct EffectDefinition(string Id, string Name, string Summary, Color Color, float Spread, float Distortion, float EdgeDarkness, float Duration, float Mode, PreviewType Preview, string SkillPath = "");

    private static readonly EffectDefinition[] BaseEffects =
    [
        new("bleed", "墨晕扩散", "中心墨团向纸面缓慢渗开，保留湿边和中心沉积。", new Color("18211f"), 1.0f, 0.040f, 0.95f, 2.4f, 0f, PreviewType.Shader),
        new("drop", "墨滴回渗", "墨滴落纸后形成不规则外圈，边缘有轻微回流。", new Color("26352f"), 1.18f, 0.052f, 0.86f, 2.6f, 1f, PreviewType.Shader),
        new("brush", "笔锋扫掠", "一笔横扫留下带方向性的湿墨拖尾。", new Color("1c2925"), 0.82f, 0.032f, 1.08f, 1.75f, 2f, PreviewType.Shader),
        new("burst", "碎墨爆散", "墨团向外炸开，边缘破碎并带有颗粒感。", new Color("222b28"), 1.28f, 0.070f, 0.70f, 1.55f, 3f, PreviewType.Shader),
        new("dry_edge", "干湿边缘", "主体淡化，只观察外圈、回渗和干燥边界。", new Color("29302c"), 1.04f, 0.045f, 1.20f, 2.2f, 4f, PreviewType.Shader),
        new("battle_shield", "战斗·常驻护盾", "青灰湿墨形成护持边界；来袭一侧积墨受力，淡墨向外洇开。", new Color("316ca0"), 0f, 0f, 0f, 2.4f, 0f, PreviewType.Shield),
        new("battle_death", "战斗·死亡墨散", "深墨短暂沉积，再收束为不规则枯墨，湿边逐渐散淡。", new Color("18211f"), 0f, 0f, 0f, 0.9f, 0f, PreviewType.Death),
        new("battle_punch", "战斗·发劲", "暖灰劲痕加速推出，接触顿笔后向受力方向散开，干湿笔触逐层收淡。", new Color("a85c4d"), 0f, 0f, 0f, 0.65f, 0f, PreviewType.Punch),
        new("battle_sword_cleave", "练手·剑劈斩（出手）", "浓墨笔锋疾扫，淡墨随笔势流动，拖尾逐渐破开；只留几滴抛落的余墨。", new Color("3d7187"), 0f, 0f, 0f, 0.70f, 0f, PreviewType.SwordCleave),
        new("battle_sword_impact", "练手·剑斩受击", "短促落锋形成浓墨切痕，湿边略微洇开，再由枯笔断裂消散。", new Color("7da8b7"), 0f, 0f, 0f, 0.65f, 0f, PreviewType.SwordImpact),
        new("battle_ink_hit", "战斗·泼墨命中", "接触处浓墨压缩后爆开，不规则墨花与断裂冲击纹向外散去，墨滴抛落后收淡。", new Color("18211f"), 0f, 0f, 0f, 0.5f, 0f, PreviewType.Impact),
        new("battle_ink_critical", "战斗·重击墨爆", "重击扩展墨花范围，以克制的赭红积墨强调接触瞬间，余劲沿受力方向断笔收淡。", new Color("783d2d"), 0f, 0f, 0f, 0.5f, 0f, PreviewType.CriticalImpact),
        new("battle_ink_exchange", "练手·水墨剑斩", "观察出手、落剑、受击与余墨消散的完整节奏。", new Color("18211f"), 0f, 0f, 0f, 0.9f, 0f, PreviewType.SwordExchange),
    ];

    private readonly List<Button> _effectButtons = [];
    private EffectDefinition[] _effects = [];
    private int _previewDirection;
    private readonly List<ImageTexture> _masks = [];
    private TextureRect? _inkLayer;
    private Control? _stage;
    private Node2D? _battleLayer;
    private Node2D? _battleAttacker;
    private Node2D? _battleTarget;
    private BattleShieldAura? _shieldPreview;
    private Label? _effectTitle;
    private Label? _effectSummary;
    private Label? _status;
    private ShaderMaterial? _material;
    private EffectDefinition _current;
    private int _currentIndex;
    private float _elapsed;
    private bool _playing;
    private bool _shieldPreviewImpactPlayed;
    private int _previewGeneration;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var definitions = new List<EffectDefinition>(BaseEffects);
        foreach (var path in SkillFiles("res://resources/activeskills"))
        {
            // Weapon resources remain legacy practice material; active skills are previewed from their real data.
            if (path.Contains("/武器技能/")) continue;
            var skill = GD.Load<ActiveSkillData>(path);
            if (skill is null || skill.skill_type == ActiveSkillData.SkillType.POWER || skill.skill_type == ActiveSkillData.SkillType.CURSE) continue;
            var suffix = string.IsNullOrEmpty(skill.source_gongfa_name) ? "" : " · " + skill.source_gongfa_name;
            definitions.Add(new EffectDefinition(path, skill.skill_name + suffix, BattleInkCombatVfx.Describe(skill), new Color("18211f"),
                0f, 0f, 0f, 0.85f, 0f, PreviewType.RealSkill, path));
        }
        _effects = definitions.ToArray();
        _masks.Add(CreateMask(0));
        _masks.Add(CreateMask(1));
        _masks.Add(CreateMask(2));
        _masks.Add(CreateMask(3));
        _masks.Add(CreateMask(4));

        BuildBackdrop();
        BuildLayout();
        var initial = System.Array.FindIndex(_effects, effect => effect.SkillPath.EndsWith("/应敌.tres"));
        SelectEffect(initial >= 0 ? initial : 0);
    }

    private static IEnumerable<string> SkillFiles(string directory)
    {
        foreach (var file in DirAccess.GetFilesAt(directory).OrderBy(value => value))
            if (file.EndsWith(".tres")) yield return directory + "/" + file;
        foreach (var folder in DirAccess.GetDirectoriesAt(directory).OrderBy(value => value))
            foreach (var path in SkillFiles(directory + "/" + folder)) yield return path;
    }

    public void PreviewSkill(string path, int direction = 0)
    {
        _previewDirection = Mathf.PosMod(direction, 4);
        var index = System.Array.FindIndex(_effects, effect => effect.SkillPath == path);
        if (index >= 0) SelectEffect(index);
    }

    public override void _Process(double delta)
    {
        if (!_playing) return;
        _elapsed += (float)delta;

        if (_current.Preview != PreviewType.Shader)
        {
            if (_status is not null)
            {
                var label = _current.Preview == PreviewType.Shield ? "常驻显示" : "播放中";
                _status.Text = $"{label}  ·  {_elapsed:0.00}s";
            }
            if (_current.Preview == PreviewType.Shield)
            {
                if (!_shieldPreviewImpactPlayed && _elapsed >= 0.62f && _shieldPreview is not null)
                {
                    _shieldPreview.PlayImpact(Vector2.Left);
                    _shieldPreviewImpactPlayed = true;
                }
                return;
            }
            if (_elapsed >= _current.Duration)
            {
                _playing = false;
                if (_status is not null) _status.Text = "播放完成  ·  点击“重播”或按空格再次观看";
            }
            return;
        }

        if (_material is null) return;
        var normalized = Mathf.Clamp(_elapsed / _current.Duration, 0f, 1f);
        _material.SetShaderParameter("progress", normalized);
        if (_status is not null)
            _status.Text = $"播放中  ·  {normalized * _current.Duration:0.00}s / {_current.Duration:0.00}s";

        if (normalized >= 1f)
        {
            _playing = false;
            if (_status is not null) _status.Text = "播放完成  ·  点击“重播”或按空格再次观看";
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
            return;
        }

        if (@event.IsActionPressed("ui_accept"))
        {
            Replay();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildBackdrop()
    {
        var backdrop = new TextureRect
        {
            Texture = GD.Load<Texture2D>(BackdropPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(0.58f, 0.62f, 0.58f, 1f)
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var veil = new ColorRect { Color = new Color(0.055f, 0.075f, 0.070f, 0.62f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
    }

    private void BuildLayout()
    {
        var margin = new MarginContainer { Name = "Margin" };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 72);
        margin.AddThemeConstantOverride("margin_top", 48);
        margin.AddThemeConstantOverride("margin_right", 72);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(margin);

        var root = new VBoxContainer { Name = "Root", SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 70) };
        root.AddChild(header);
        var heading = MakeLabel("水墨特效实验室", 34, new Color("eee2c5"));
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(heading);
        var back = MakeButton("返回启动菜单", 150, 42);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
        header.AddChild(back);

        var divider = new ColorRect { Color = new Color("ae8e55") };
        divider.CustomMinimumSize = new Vector2(0, 1);
        divider.MouseFilter = MouseFilterEnum.Ignore;
        root.AddChild(divider);

        var columns = new HBoxContainer { Name = "Columns", SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 18);
        root.AddChild(columns);

        var leftPanel = new PanelContainer { CustomMinimumSize = new Vector2(310, 0) };
        leftPanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.88f, 0.83f, 0.70f, 0.93f), new Color("514735")));
        columns.AddChild(leftPanel);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddChild(scroll);
        var left = new VBoxContainer { Name = "EffectList" };
        left.AddThemeConstantOverride("separation", 8);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(left);
        left.AddChild(MakeLabel("效果类型", 23, new Color("30291f")));
        left.AddChild(MakeLabel("选择一种表现，右侧会自动播放。\n空格：重播　Esc：返回", 15, new Color("615645")));

        for (var i = 0; i < _effects.Length; i++)
        {
            var index = i;
            var button = MakeChoiceButton(_effects[i].Name);
            button.Pressed += () => SelectEffect(index);
            _effectButtons.Add(button);
            left.AddChild(button);
        }

        var note = MakeLabel("观察交手\n下方技能来自当前技能资源。短打、灵息、神魂、符箓与妖兽招式分别呈现；切换方向可检查出手、落点与受力。", 13, new Color("6b604e"));
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.SizeFlagsVertical = SizeFlags.Expand;
        left.AddChild(note);

        var stagePanel = new PanelContainer { Name = "StagePanel", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        stagePanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.88f, 0.84f, 0.74f, 0.96f), new Color("6a5d46")));
        columns.AddChild(stagePanel);

        var stage = new Control { Name = "Stage", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        stagePanel.AddChild(stage);
        _stage = stage;

        var paper = new ColorRect { Color = new Color("e9dfc8"), MouseFilter = MouseFilterEnum.Ignore };
        paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage.AddChild(paper);

        var paperWash = new ColorRect { Color = new Color(0.32f, 0.28f, 0.20f, 0.07f), MouseFilter = MouseFilterEnum.Ignore };
        paperWash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage.AddChild(paperWash);

        _battleLayer = new Node2D { Name = "BattleVfxLayer", ZIndex = 30, Visible = false };
        stage.AddChild(_battleLayer);
        _battleAttacker = CreateBattleMarker("出手", new Color(0.12f, 0.28f, 0.24f, 0.92f));
        _battleTarget = CreateBattleMarker("目标", new Color(0.34f, 0.20f, 0.15f, 0.92f));
        _battleLayer.AddChild(_battleAttacker);
        _battleLayer.AddChild(_battleTarget);
        _shieldPreview = BattleShieldAura.CreatePreview(8);
        _battleLayer.AddChild(_shieldPreview);

        _inkLayer = new TextureRect
        {
            Name = "InkLayer",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _inkLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage.AddChild(_inkLayer);

        var topLabel = MakeLabel("运行时预览", 15, new Color("786b58"));
        topLabel.Position = new Vector2(22, 18);
        topLabel.MouseFilter = MouseFilterEnum.Ignore;
        stage.AddChild(topLabel);

        var replay = MakeButton("重播", 100, 38);
        replay.Position = new Vector2(22, 58);
        replay.Pressed += Replay;
        var facing = MakeButton("切换交手方向", 160, 36);
        facing.Position = new Vector2(154f, 76f);
        facing.Pressed += () => { _previewDirection = (_previewDirection + 1) % 4; Replay(); };
        stage.AddChild(facing);
        stage.AddChild(replay);

        var info = new VBoxContainer { Name = "Info", Position = new Vector2(22, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        info.SetAnchorsPreset(LayoutPreset.BottomWide);
        info.OffsetTop = -112;
        info.OffsetRight = -22;
        info.OffsetBottom = -20;
        stage.AddChild(info);
        _effectTitle = MakeLabel("", 27, new Color("30291f"));
        info.AddChild(_effectTitle);
        _effectSummary = MakeLabel("", 15, new Color("615645"));
        _effectSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(_effectSummary);
        _status = MakeLabel("", 14, new Color("8b5f35"));
        info.AddChild(_status);
    }

    private void SelectEffect(int index)
    {
        if (index < 0 || index >= _effects.Length || _inkLayer is null) return;
        _currentIndex = index;
        _current = _effects[index];
        var isBattlePreview = _current.Preview != PreviewType.Shader;
        _inkLayer.Visible = !isBattlePreview;
        if (_battleLayer is not null) _battleLayer.Visible = isBattlePreview;

        if (!isBattlePreview)
        {
            _inkLayer.Texture = _masks[index];
            _material ??= new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
            _inkLayer.Material = _material;
            _material.SetShaderParameter("ink_color", _current.Color);
            _material.SetShaderParameter("mask_texture", _masks[index]);
            _material.SetShaderParameter("mask_texel", new Vector2(1f / MaskWidth, 1f / MaskHeight));
            _material.SetShaderParameter("effect_mode", _current.Mode);
            _material.SetShaderParameter("spread", _current.Spread);
            _material.SetShaderParameter("distortion", _current.Distortion);
            _material.SetShaderParameter("edge_darkness", _current.EdgeDarkness);
            _material.SetShaderParameter("progress", 0f);
        }

        if (_effectTitle is not null) _effectTitle.Text = _current.Name;
        if (_effectSummary is not null) _effectSummary.Text = _current.Summary;
        for (var i = 0; i < _effectButtons.Count; i++)
            StyleChoiceButton(_effectButtons[i], i == _currentIndex);
        Replay();
    }

    private void Replay()
    {
        _previewGeneration++;
        _elapsed = 0f;
        _playing = true;
        _shieldPreviewImpactPlayed = false;

        if (_current.Preview != PreviewType.Shader)
        {
            PositionBattlePreview();
            TriggerBattlePreview();
            if (_status is not null) _status.Text = _current.Preview == PreviewType.Shield ? "常驻显示" : "准备播放";
            return;
        }

        if (_material is null) return;
        _material.SetShaderParameter("progress", 0f);
        if (_status is not null) _status.Text = "准备播放";
    }

    private void PositionBattlePreview()
    {
        if (_stage is null || _battleAttacker is null || _battleTarget is null || _shieldPreview is null) return;
        var size = _stage.Size;
        if (size.X < 300f || size.Y < 200f) size = new Vector2(700f, 430f);
        var center = new Vector2(size.X * 0.5f, size.Y * 0.50f);
        _battleAttacker.Position = center + new Vector2(-165f, 112f);
        _battleTarget.Position = center + new Vector2(165f, 112f);
        if (_previewDirection == 1)
        {
            _battleAttacker.Position = center + new Vector2(165f, 112f);
            _battleTarget.Position = center + new Vector2(-165f, 112f);
        }
        else if (_previewDirection == 2) _battleTarget.Position += Vector2.Up * 100f;
        else if (_previewDirection == 3) _battleTarget.Position += Vector2.Down * 100f;
        var direction = (_battleTarget.Position - _battleAttacker.Position).Normalized();
        _battleAttacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.face_direction(direction);
        _battleTarget.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.face_direction(-direction);
        _shieldPreview.Position = _battleLayer!.ToLocal(BattleInkCombatVfx.Anchor(_battleTarget));
    }

    private void TriggerBattlePreview()
    {
        if (_battleAttacker is null || _battleTarget is null || _shieldPreview is null) return;
        _shieldPreview.Visible = _current.Preview == PreviewType.Shield;
        if (_current.Preview == PreviewType.Shield) return;

        if (_current.Preview == PreviewType.Death)
        {
            BattleInkSlashEffect.PlayDeath(_battleTarget);
            return;
        }

        if (_current.Preview == PreviewType.RealSkill)
        {
            var skill = GD.Load<ActiveSkillData>(_current.SkillPath);
            if (skill is null) return;
            var attacker = _battleAttacker;
            var target = _battleTarget;
            var direction = (target.GlobalPosition - attacker.GlobalPosition).Normalized();
            if (!BattleInkCombatVfx.DealsDamage(skill)) { BattleInkCombatVfx.PlayUtility(attacker, target, skill); return; }
            attacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_attack(direction);
            BattleInkCombatVfx.PlayAttack(attacker, target, skill);
            var generation = _previewGeneration;
            GetTree().CreateTimer(BattleInkCombatVfx.ContactSeconds).Timeout += () =>
            {
                if (generation != _previewGeneration || !IsInstanceValid(target)) return;
                BattleInkCombatVfx.PlayImpact(target, skill, attacker);
                target.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_hit(direction, BattleInkCombatVfx.HitWeight(skill));
            };
            return;
        }

        if (_current.Preview == PreviewType.Punch)
        {
            var skill = new ActiveSkillData { skill_name = "发劲", damage_type = "physical" };
            BattleInkSlashEffect.PlayAttack(_battleAttacker, _battleTarget, skill);
            var target = _battleTarget;
            var generation = _previewGeneration;
            GetTree().CreateTimer(0.16f).Timeout += () =>
            {
                if (generation == _previewGeneration && GodotObject.IsInstanceValid(target)) BattleInkSlashEffect.PlayImpactForSkill(target, skill, false);
            };
            return;
        }

        if (_current.Preview == PreviewType.SwordCleave)
        {
            var skill = new ActiveSkillData { skill_name = "剑劈斩", required_weapon_type = (int)Equipment.WeaponType.SWORD, damage_type = "physical" };
            BattleSwordVfx.PlayAttack(_battleAttacker, _battleTarget, skill);
            return;
        }

        if (_current.Preview == PreviewType.SwordImpact)
            BattleInkSlashEffect.PlaySwordImpact(_battleTarget, _battleAttacker);

        if (_current.Preview is PreviewType.Impact or PreviewType.CriticalImpact)
            BattleInkSlashEffect.PlayImpact(_battleTarget, _current.Preview == PreviewType.CriticalImpact);

        if (_current.Preview == PreviewType.SwordExchange)
        {
            var attacker = _battleAttacker;
            var target = _battleTarget;
            var generation = _previewGeneration;
            attacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_attack(Vector2.Right);
            BattleInkSlashEffect.PlaySwordCleave(attacker, target);
            GetTree().CreateTimer(0.15f).Timeout += () =>
            {
                if (generation != _previewGeneration || !IsInstanceValid(target)) return;
                BattleInkSlashEffect.PlaySwordImpact(target, attacker);
                target.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_hit(Vector2.Right);
            };
        }
    }

    private static ImageTexture CreateMask(int type)
    {
        var image = Image.CreateEmpty(MaskWidth, MaskHeight, false, Image.Format.Rgba8);
        for (var y = 0; y < MaskHeight; y++)
        {
            for (var x = 0; x < MaskWidth; x++)
            {
                var uv = new Vector2((x + 0.5f) / MaskWidth, (y + 0.5f) / MaskHeight);
                var alpha = type switch
                {
                    0 => Blob(uv, new Vector2(0.49f, 0.50f), new Vector2(0.20f, 0.27f), 1.0f)
                         + Blob(uv, new Vector2(0.35f, 0.43f), new Vector2(0.15f, 0.12f), 0.56f)
                         + Blob(uv, new Vector2(0.65f, 0.56f), new Vector2(0.16f, 0.14f), 0.52f),
                    1 => Blob(uv, new Vector2(0.50f, 0.48f), new Vector2(0.12f, 0.15f), 1.0f)
                         + Blob(uv, new Vector2(0.39f, 0.60f), new Vector2(0.12f, 0.10f), 0.68f)
                         + Blob(uv, new Vector2(0.64f, 0.37f), new Vector2(0.10f, 0.09f), 0.58f)
                         + Blob(uv, new Vector2(0.70f, 0.63f), new Vector2(0.07f, 0.06f), 0.42f),
                    2 => Blob(uv, new Vector2(0.26f, 0.56f), new Vector2(0.26f, 0.085f), 0.95f)
                         + Blob(uv, new Vector2(0.54f, 0.49f), new Vector2(0.25f, 0.070f), 0.72f)
                         + Blob(uv, new Vector2(0.78f, 0.42f), new Vector2(0.18f, 0.055f), 0.45f),
                    3 => BurstMask(uv),
                    _ => Blob(uv, new Vector2(0.50f, 0.50f), new Vector2(0.22f, 0.23f), 0.72f)
                         + Blob(uv, new Vector2(0.33f, 0.46f), new Vector2(0.13f, 0.10f), 0.48f)
                         + Blob(uv, new Vector2(0.68f, 0.57f), new Vector2(0.14f, 0.11f), 0.45f)
                };
                var noise = 0.86f + Hash(x * 17 + y * 31) * 0.22f;
                alpha = Mathf.Clamp(alpha * noise, 0f, 1f);
                var feathered = Mathf.SmoothStep(0.03f, 0.62f, alpha);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, feathered));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static float Blob(Vector2 uv, Vector2 center, Vector2 radius, float strength)
    {
        var p = (uv - center) / radius;
        return Mathf.Exp(-p.Dot(p) * 1.85f) * strength;
    }

    private static float BurstMask(Vector2 uv)
    {
        var total = Blob(uv, new Vector2(0.50f, 0.50f), new Vector2(0.11f, 0.12f), 0.92f);
        var center = new Vector2(0.50f, 0.50f);
        for (var i = 0; i < 12; i++)
        {
            var angle = i * Mathf.Tau / 12f + 0.18f;
            var distance = 0.17f + (i % 3) * 0.045f;
            var dot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            var radius = new Vector2(0.035f + (i % 2) * 0.018f, 0.040f + ((i + 1) % 3) * 0.012f);
            total += Blob(uv, dot, radius, 0.38f + (i % 4) * 0.10f);
        }
        return total;
    }

    private static float Hash(int value)
    {
        unchecked
        {
            var n = value;
            n = (n << 13) ^ n;
            return 0.5f + 0.5f * (1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f);
        }
    }

    private static Node2D CreateBattleMarker(string text, Color color)
    {
        var marker = new Node2D { Name = text };
        marker.AddChild(new Polygon2D
        {
            Polygon = [new Vector2(-42f, 4f), new Vector2(-26f, -3f), new Vector2(26f, -3f), new Vector2(42f, 4f), new Vector2(25f, 11f), new Vector2(-25f, 11f)],
            Color = new Color(0.06f, 0.08f, 0.07f, 0.22f),
            Position = new Vector2(0f, -20f)
        });
        var doll = new BattlePaperDoll { Name = "BattlePaperDoll" };
        marker.AddChild(doll);
        doll.Configure(new BattleEntityData { entity_name = text, gender = 0,
            faction = text == "目标" ? (int)WorldEntity.Faction.ENEMY : 1 });
        var label = MakeLabel(text, 13, new Color(0.19f, 0.16f, 0.11f, 0.90f));
        label.Position = new Vector2(-28f, 18f);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        marker.AddChild(label);
        return marker;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button MakeButton(string text, int width, int height)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, height), FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", new Color("352c20"));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.77f, 0.68f, 0.49f, 0.68f), new Color("5b4a31")));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.92f, 0.84f, 0.66f, 0.92f), new Color("896c3f")));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.62f, 0.70f, 0.62f, 0.94f), new Color("3d574a")));
        return button;
    }

    private static Button MakeChoiceButton(string text)
    {
        var button = MakeButton(text, 0, 48);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Alignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("outline_size", 0);
        return button;
    }

    private static void StyleChoiceButton(Button button, bool selected)
    {
        var fill = selected ? new Color(0.62f, 0.70f, 0.62f, 0.96f) : new Color(0.80f, 0.75f, 0.63f, 0.72f);
        var border = selected ? new Color("2e5145") : new Color("7b6c53");
        button.AddThemeStyleboxOverride("normal", ButtonStyle(fill, border));
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 14,
            ContentMarginRight = 14
        };
    }

    private static StyleBoxFlat PanelStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 18,
            ContentMarginBottom = 18
        };
    }
}

