using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InfoPanel
{
    private const double SkillRewriteLongPressSeconds = .34;
    private sealed record SkillRewriteUiState(
        SkillRewriteChoice Choice,
        List<(int Slot, string Key, ActiveSkillData Skill)> Candidates,
        HashSet<int> CandidateSlots,
        string BoundKey,
        int BoundSlot,
        bool BoundActive);

    private string _selectedSkillRewriteId = "", _skillRewriteFeedback = "";
    private string _pendingSkillRewriteId = "", _activeSkillRewriteId = "";
    private Vector2 _skillRewritePressPosition;
    private Character? _skillRewriteInputHuman;
    private Godot.Timer? _skillRewriteHoldTimer;
    private VBoxContainer? _characterSkillRewritePanel;
    private ScrollContainer? _skillRewriteSourceScroll;
    private VBoxContainer? _skillRewriteSourceList;
    private readonly List<SkillRewriteChoice> _skillRewriteUiChoices = [];
    private readonly Dictionary<string, SkillRewriteUiState> _skillRewriteUiStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _skillRewriteSourceRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(string Label, bool Active, int Slot)>> _skillRewriteMarksBySkill = new(StringComparer.Ordinal);

    private void build_character_skill_rewrites(Control page, Character? human)
    {
        _characterSkillRewritePanel = CharacterBox(CharacterPanel(page, 1088, 92, 294, 703));
        _characterSkillRewritePanel.AddThemeConstantOverride("separation", 6);
        _skillRewriteHoldTimer ??= new Godot.Timer { Name = "SkillRewriteLongPress", OneShot = true, WaitTime = SkillRewriteLongPressSeconds };
        if (_skillRewriteHoldTimer.GetParent() is null)
        {
            AddChild(_skillRewriteHoldTimer);
            _skillRewriteHoldTimer.Timeout += begin_skill_rewrite_drag;
        }
    }

    private void prepare_skill_rewrite_ui_state(Character? human)
    {
        _skillRewriteUiChoices.Clear();
        _skillRewriteUiStates.Clear();
        _skillRewriteSourceRows.Clear();
        _skillRewriteMarksBySkill.Clear();
        _skillRewriteInputHuman = human;
        if (human is null) return;

        _skillRewriteUiChoices.AddRange(human.get_skill_rewrite_choices());
        var equippedKeys = human.get_combat_skill_slot_paths()
            .Select(human.get_base_combat_skill_path).ToList();
        foreach (var choice in _skillRewriteUiChoices)
        {
            var candidates = human.get_skill_rewrite_candidates(choice);
            var boundKey = human.get_skill_rewrite_binding(choice);
            var boundSlot = boundKey.Length == 0 ? -1 : equippedKeys.FindIndex(key => key == boundKey);
            var boundActive = choice.Available && candidates.Any(candidate => candidate.Key == boundKey);
            var state = new SkillRewriteUiState(choice, candidates,
                candidates.Select(candidate => candidate.Slot).ToHashSet(), boundKey, boundSlot, boundActive);
            _skillRewriteUiStates[choice.Id] = state;

            if (boundKey.Length == 0 || boundSlot < 0) continue;
            if (!_skillRewriteMarksBySkill.TryGetValue(boundKey, out var marks))
                _skillRewriteMarksBySkill[boundKey] = marks = [];
            marks.Add(($"{choice.Title} · {choice.Source}", boundActive, boundSlot));
        }

        if (!_skillRewriteUiStates.ContainsKey(_selectedSkillRewriteId))
        {
            _selectedSkillRewriteId = _skillRewriteUiChoices.FirstOrDefault()?.Id ?? "";
            _skillRewriteFeedback = "";
        }
    }

    private void refresh_character_skill_rewrites(Character? human)
    {
        if (_characterSkillRewritePanel is null || !GodotObject.IsInstanceValid(_characterSkillRewritePanel)) return;
        var detail = _characterSkillRewritePanel;
        CharacterClear(detail);
        detail.AddChild(CharacterText("化法来源", 27));
        if (human is null) { detail.AddChild(CharacterParagraph("暂无可配置的人物。", 18, true)); return; }
        if (_skillRewriteInputHuman != human || _skillRewriteUiStates.Count == 0 && _skillRewriteUiChoices.Count == 0)
            prepare_skill_rewrite_ui_state(human);
        var choices = _skillRewriteUiChoices;
        if (choices.Count == 0)
        {
            detail.AddChild(CharacterParagraph("尚无化法来源。研读秘籍卷章或获得带化法的器物后，会在这里显示。", 17, true));
            return;
        }

        var hint = CharacterParagraph("点击条目查看详情；长按后拖到亮起的已装配技能即可绑定。", 15, true);
        hint.Name = "RewriteDragHint";
        detail.AddChild(hint);

        _skillRewriteSourceScroll = new ScrollContainer
        {
            Name = "RewriteSourceListScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 150),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        detail.AddChild(_skillRewriteSourceScroll);
        _skillRewriteSourceList = new VBoxContainer { Name = "RewriteSourceList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _skillRewriteSourceList.AddThemeConstantOverride("separation", 4);
        _skillRewriteSourceScroll.AddChild(_skillRewriteSourceList);

        foreach (var item in choices)
        {
            if (!_skillRewriteUiStates.TryGetValue(item.Id, out var state)) continue;
            var row = CharacterButton("", 17, item.Id == _selectedSkillRewriteId, 4);
            row.Name = $"RewriteSource_{_skillRewriteSourceRows.Count}";
            row.CustomMinimumSize = new Vector2(0, 48);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.ClipContents = true;
            _skillRewriteSourceList.AddChild(row);
            _skillRewriteSourceRows[item.Id] = row;

            var check = CharacterText(state.BoundKey.Length > 0 ? "✓" : "", 18);
            check.Name = "BoundCheck";
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
            check.AddThemeColorOverride("font_color", state.BoundActive ? new Color("8a6725") : new Color("8a806e"));
            row.AddChild(check); CharacterRect(check, 3, 2, 22, 43);
            var title = CharacterClippedText(item.Title, 16);
            title.AddThemeColorOverride("font_color", item.Available ? WorldHudStyle.Ink : new Color("887f70"));
            row.AddChild(title); CharacterRect(title, 28, 2, 218, 22);
            var stateLabel = CharacterClippedText(skill_rewrite_state_text(human, state), 13, true);
            stateLabel.AddThemeColorOverride("font_color", state.BoundKey.Length > 0 && !state.BoundActive
                ? new Color("897d68")
                : item.Available ? new Color("74613c") : new Color("8a8070"));
            row.AddChild(stateLabel); CharacterRect(stateLabel, 28, 24, 218, 19);
            if (!item.Available) row.Modulate = new Color(.74f, .71f, .65f);
            row.MouseEntered += () =>
            {
                if (!item.Available) row.Modulate = new Color(.79f, .75f, .67f);
            };
            row.MouseExited += () => row.Modulate = item.Available ? Colors.White : new Color(.74f, .71f, .65f);
            row.Pressed += () =>
            {
                _selectedSkillRewriteId = item.Id;
                _skillRewriteFeedback = "";
                refresh_character_skill_rewrites(human);
            };
            row.GuiInput += input => on_skill_rewrite_source_input(item.Id, input);
        }

        if (!_skillRewriteUiStates.TryGetValue(_selectedSkillRewriteId, out var selected))
            selected = _skillRewriteUiStates.Values.First();
        var choice = selected.Choice;
        detail.AddChild(new HSeparator());
        var selectedTitle = CharacterClippedText(choice.Title, 18);
        selectedTitle.Name = "RewriteTitle";
        detail.AddChild(selectedTitle);
        var status = CharacterParagraph(skill_rewrite_state_text(human, selected), 15, true);
        status.Name = "RewriteStatus";
        detail.AddChild(status);
        var description = CharacterScroll(detail);
        description.Name = "RewriteDescriptionScroll";
        description.CustomMinimumSize = new Vector2(0, 112);
        description.SizeFlagsVertical = SizeFlags.ExpandFill;
        var addedEffect = choice.Description.Trim();
        if (addedEffect.StartsWith("化法追加：", StringComparison.Ordinal))
            addedEffect = addedEffect["化法追加：".Length..].Trim();
        var descriptionText = CharacterParagraph($"来源：{choice.Source}\n适用范围：{choice.Condition}\n\n化法追加：\n{addedEffect}", 15);
        descriptionText.Name = "RewriteDescription";
        descriptionText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        description.AddChild(descriptionText);

        detail.AddChild(new HSeparator());
        if (selected.BoundKey.Length > 0)
        {
            var clear = CharacterButton("解除绑定", 16);
            clear.Name = "ClearRewriteBinding";
            clear.CustomMinimumSize = new Vector2(0, 36);
            detail.AddChild(clear);
            clear.Pressed += () =>
            {
                human.set_skill_rewrite_binding(choice.Id, "", out _skillRewriteFeedback);
                _activeSkillRewriteId = _pendingSkillRewriteId = "";
                hide_tooltip();
                refresh_character_battle(human);
            };
        }
        if (_skillRewriteFeedback.Length > 0)
            detail.AddChild(CharacterParagraph(_skillRewriteFeedback, 14, true));
    }

    private static string skill_rewrite_state_text(Character human, SkillRewriteUiState state)
    {
        if (state.BoundKey.Length > 0)
        {
            var target = human.resolve_combat_skill(state.BoundKey) ??
                (ResourceLoader.Exists(state.BoundKey) ? ResourceLoader.Load<ActiveSkillData>(state.BoundKey) : null);
            var targetName = target?.skill_name ?? "目标技能";
            var slot = state.BoundSlot >= 0 ? $"术法槽{state.BoundSlot + 1} · " : "未装配 · ";
            return $"✓ {slot}{targetName} · {(state.BoundActive ? "已生效" : "暂停生效")}";
        }
        if (!state.Choice.Available) return state.Choice.UnavailableReason;
        return state.Candidates.Count > 0
            ? $"可绑定 · {state.Candidates.Count}个符合条件的术法槽"
            : "请先装配符合条件的技能";
    }

    private void on_skill_rewrite_source_input(string sourceId, InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton) return;
        if (mouseButton.Pressed)
        {
            if (_activeSkillRewriteId.Length > 0) return;
            _pendingSkillRewriteId = sourceId;
            _skillRewritePressPosition = GetGlobalMousePosition();
            _skillRewriteHoldTimer?.Stop();
            if (_skillRewriteUiStates.TryGetValue(sourceId, out var state) &&
                state.Choice.Available && state.CandidateSlots.Count > 0)
                _skillRewriteHoldTimer?.Start(SkillRewriteLongPressSeconds);
        }
        else
        {
            _skillRewriteHoldTimer?.Stop();
            if (_activeSkillRewriteId.Length == 0 && _pendingSkillRewriteId == sourceId)
                _pendingSkillRewriteId = "";
        }
    }

    private void handle_skill_rewrite_pointer_input(InputEvent input)
    {
        if (_pendingSkillRewriteId.Length > 0 && _activeSkillRewriteId.Length == 0 &&
            input is InputEventMouseMotion && GetGlobalMousePosition().DistanceTo(_skillRewritePressPosition) > 14f)
        {
            _skillRewriteHoldTimer?.Stop();
            _pendingSkillRewriteId = "";
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _skillRewriteHoldTimer?.Stop();
            if (_activeSkillRewriteId.Length > 0) CallDeferred(nameof(clear_skill_rewrite_drag_state));
            else _pendingSkillRewriteId = "";
        }
    }

    private void begin_skill_rewrite_drag()
    {
        var sourceId = _pendingSkillRewriteId;
        if (sourceId.Length == 0 || !Input.IsMouseButtonPressed(MouseButton.Left) ||
            !_skillRewriteUiStates.TryGetValue(sourceId, out var state) ||
            !state.Choice.Available || state.CandidateSlots.Count == 0 ||
            !_skillRewriteSourceRows.TryGetValue(sourceId, out var sourceRow) ||
            !GodotObject.IsInstanceValid(sourceRow) || _skillRewriteInputHuman is null)
            return;

        _selectedSkillRewriteId = sourceId;
        _activeSkillRewriteId = sourceId;
        hide_tooltip();
        update_skill_rewrite_drag_slot_visuals();
        var payload = new Godot.Collections.Dictionary
        {
            ["skill_rewrite_id"] = sourceId,
            ["owner"] = _skillRewriteInputHuman.GetInstanceId()
        };
        var preview = CharacterButton($"化法 · {state.Choice.Title}", 17, true, 5);
        preview.CustomMinimumSize = new Vector2(188, 40);
        sourceRow.ForceDrag(payload, preview);
    }

    private bool can_drop_skill_rewrite_on_slot(int targetIndex, Variant data, Character human)
    {
        if (targetIndex < 0 || data.VariantType != Variant.Type.Dictionary) return false;
        var payload = data.AsGodotDictionary();
        if (!payload.ContainsKey("skill_rewrite_id") ||
            payload.GetValueOrDefault("owner", 0).AsUInt64() != human.GetInstanceId()) return false;
        var sourceId = payload["skill_rewrite_id"].AsString();
        return _activeSkillRewriteId == sourceId &&
            _skillRewriteUiStates.TryGetValue(sourceId, out var state) &&
            state.CandidateSlots.Contains(targetIndex);
    }

    private void drop_skill_rewrite_on_slot(int targetIndex, Variant data, Character human)
    {
        if (!can_drop_skill_rewrite_on_slot(targetIndex, data, human)) return;
        var payload = data.AsGodotDictionary();
        var sourceId = payload["skill_rewrite_id"].AsString();
        var paths = human.get_combat_skill_slot_paths();
        if (targetIndex >= paths.Count) return;
        var skillKey = human.get_base_combat_skill_path(paths[targetIndex]);
        if (skillKey.Length == 0) return;

        _selectedSkillRewriteId = sourceId;
        human.set_skill_rewrite_binding(sourceId, skillKey, out _skillRewriteFeedback);
        _activeSkillRewriteId = _pendingSkillRewriteId = "";
        hide_tooltip();
        refresh_character_battle(human);
    }

    private void update_skill_rewrite_drag_slot_visuals()
    {
        if (_characterSkillSlots is null) return;
        var candidateSlots = _activeSkillRewriteId.Length > 0 &&
            _skillRewriteUiStates.TryGetValue(_activeSkillRewriteId, out var state)
                ? state.CandidateSlots
                : null;
        var slots = _characterSkillSlots.GetChildren().OfType<Button>().ToList();
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var dragging = candidateSlots is not null;
            var eligible = candidateSlots?.Contains(i) == true;
            var selected = i == _characterDropTarget;
            PaperButtonFeedback.Apply(slot, fontSize: slot.GetThemeFontSize("font_size"), selected: selected || eligible && dragging);
        }
        apply_skill_rewrite_drag_slot_tints();
    }

    private void apply_skill_rewrite_drag_slot_tints()
    {
        if (_characterSkillSlots is null) return;
        var candidateSlots = _activeSkillRewriteId.Length > 0 &&
            _skillRewriteUiStates.TryGetValue(_activeSkillRewriteId, out var state)
                ? state.CandidateSlots
                : null;
        var slots = _characterSkillSlots.GetChildren().OfType<Button>();
        var index = 0;
        foreach (var slot in slots)
        {
            var eligible = candidateSlots?.Contains(index) == true;
            slot.Modulate = candidateSlots is null ? Colors.White :
                eligible ? new Color(1f, .96f, .80f) : new Color(.48f, .45f, .39f, .58f);
            index++;
        }
    }

    private void clear_skill_rewrite_drag_state()
    {
        _skillRewriteHoldTimer?.Stop();
        _pendingSkillRewriteId = "";
        _activeSkillRewriteId = "";
        update_skill_rewrite_drag_slot_visuals();
    }

    private List<(string Label, bool Active, int Slot)> get_skill_rewrite_marks(Character? human, ActiveSkillData skill)
    {
        if (human is null) return [];
        var key = human.get_base_combat_skill_path(Character.combat_skill_key(skill));
        return _skillRewriteMarksBySkill.TryGetValue(key, out var marks) ? marks : [];
    }

    private string skill_rewrite_badge_text(Character? human, ActiveSkillData skill)
    {
        var marks = get_skill_rewrite_marks(human, skill);
        return marks.Count == 0 ? "" : marks.Count == 1 ? "化" : $"化×{marks.Count}";
    }

    private string skill_rewrite_assignment_detail(Character? human, ActiveSkillData skill)
    {
        var marks = get_skill_rewrite_marks(human, skill);
        if (marks.Count == 0) return "";
        var lines = marks.Select(mark =>
            $"· {mark.Label} · {(mark.Active ? "生效" : "暂停生效")}").ToList();
        return "\n\n已绑定化法\n" + string.Join("\n", lines);
    }

    private bool skill_rewrite_badge_active(Character? human, ActiveSkillData skill) =>
        get_skill_rewrite_marks(human, skill).Any(mark => mark.Active);

    private static PanelContainer create_skill_rewrite_badge(string text, bool active)
    {
        var badge = new PanelContainer { Name = "SkillRewriteBadge", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3 };
        var style = new StyleBoxFlat
        {
            BgColor = active ? new Color("7b3c32", .97f) : new Color("a39a88", .90f),
            BorderColor = active ? new Color("d2b775", .98f) : new Color("c4baa7", .82f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 1,
            ContentMarginRight = 1,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        badge.AddThemeStyleboxOverride("panel", style);
        var label = CharacterText(text, text == "化" ? 15 : 10);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true;
        label.AddThemeColorOverride("font_color", active ? new Color("f3e4c5") : new Color("e7e0d2"));
        badge.AddChild(label);
        return badge;
    }
}
