using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public partial class AlchemySlotBrewPreviewScene
{
    private readonly LineEdit[] _paperDoseInputs = new LineEdit[8];
    private readonly Button[] _paperDosePlus = new Button[8], _paperDoseMinus = new Button[8];
    private readonly List<(PanelContainer Card, Control Body, int[] Slots)> _paperGroups = [];
    private Label _paperKinds = null!, _paperTotal = null!, _paperSpirit = null!, _paperEnergy = null!, _paperHours = null!, _paperAttainment = null!;

    private static Label PaperText(string text, int size = 22, bool muted = false) => CraftingPaperStyle.Text(text, size, muted);
    private static PanelContainer PaperPanel(int padding = 18, bool bordered = true, float brightness = CraftingPaperHeader.PaperBrightness)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(padding, bordered, brightness));
        return panel;
    }
    private void BuildPaperHeader()
    {
        var header = CraftingPaperHeader.Create(_canvas, "AlchemyPaperHeader");
        var help = MakeButton("炼丹说明", () => _tutorialOverlay.Show(), 22); help.Name = "AlchemyHelpButton";
        CraftingPaperHeader.Place(header, help, CraftingPaperHeader.Cell.Help);
        _alchemyStudyButton = MakeButton("丹诀入门", StudyAlchemyEntry, CraftingPaperHeader.FontSize);
        _alchemyStudyButton.Name = "AlchemyEntryStudyButton";
        CraftingPaperStyle.ApplyButton(_alchemyStudyButton, CraftingPaperStyle.ButtonKind.Regular,
            fontSize: CraftingPaperHeader.FontSize);
        _alchemyStudyButton.Visible = OwnerCharacter is not null;
        CraftingPaperHeader.Place(header, _alchemyStudyButton, CraftingPaperHeader.Cell.Context);
        _spiritHeaderValue = CraftingPaperHeader.Metric(header, CraftingPaperHeader.Cell.Spirit, "灵识",
            $"{AlchemySlotSimulation.AvailableSpirit(TotalSpirit, OtherSpiritOccupation)}/{TotalSpirit}");
        _energyHeaderValue = CraftingPaperHeader.Metric(header, CraftingPaperHeader.Cell.Energy, "精力", "—");
        _paperAttainment = CraftingPaperHeader.Metric(header, CraftingPaperHeader.Cell.Attainment, "丹道造诣", $"{Attainment:0.#}");
        _attainmentInput = new SpinBox { Name = "AttainmentInput", MinValue = 0, MaxValue = 1196, AllowGreater = true, Step = .1, Value = Attainment, Visible = OwnerCharacter is null, CustomMinimumSize = new Vector2(100, 46) };
        _attainmentInput.ValueChanged += SetTrialAttainment;
        var attainment = _paperAttainment.GetParent<Control>();
        attainment.AddChild(_attainmentInput);
        _paperAttainment.Visible = OwnerCharacter is not null;
        var back = MakeButton("返回", () => { if (OwnerCharacter is not null) ReturnToBaigong(); else GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"); }, 22);
        back.Name = "ReturnButton";
        _canvas.AddChild(back); CraftingPaperHeader.PlaceReturn(_canvas, back);
        AddPaperReturnArrow(back);
    }
    private void StudyAlchemyEntry()
    {
        if (OwnerCharacter is null) return;
        if (!OwnerCharacter.TryStudyAlchemyEntry(out float growth, out float energy, out double shichen, out string reason))
        {
            ShowMessage("暂不能研习", reason);
            return;
        }
        Attainment = OwnerCharacter.AlchemyAttainment;
        RefreshPage();
        ShowMessage("丹诀入门", $"你完成了入门研习，丹道造诣提升 {growth:0.#}，当前为 {OwnerCharacter.AlchemyAttainment:0.#}。\n\n本次消耗精力 {energy:0.#}，用时 {shichen:0.#} 时辰。造诣只影响之后的炼制。 ");
    }
    private void RefreshAlchemyStudyButton()
    {
        if (_alchemyStudyButton is null || OwnerCharacter is null || OwnerCharacter.entity_data is not HumanData data) return;
        double current = OwnerCharacter.AlchemyAttainment;
        double gap = Math.Max(0, Character.AlchemyEntryThreshold - current);
        double energy = gap * Character.ArtisanStudyEnergyPerPoint;
        double duration = gap * Character.ArtisanStudyShichenPerPoint;
        bool completed = OwnerCharacter.HasCompletedAlchemyEntry;
        _alchemyStudyButton.Text = completed ? "丹诀已研习" : gap <= 0 ? "已达九品门槛" :
            $"入门研习 · 精力{energy:0.#} · {duration:0.#}时辰";
        _alchemyStudyButton.TooltipText = completed
            ? "入门丹诀已研习；丹道造诣成长记录随人物存档。"
            : gap <= 0
                ? "当前丹道造诣已达到九品丹门槛。"
                : $"研习后丹道造诣达到{Character.AlchemyEntryThreshold:0.#}。每补1点消耗8精力、1时辰；当前精力{OwnerCharacter.current_world_stamina:0.#}。";
        _alchemyStudyButton.Disabled = completed || gap <= 0 || _brewing || _brewStage is not null ||
            !OwnerCharacter.can_spend_world_stamina((float)energy) || data.alchemy_proficiency_growth_sources is null;
    }
    private static void AddPaperReturnArrow(Button button)
    {
        var arrow = new PaperTriangle { Right = true, MouseFilter = MouseFilterEnum.Ignore };
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) arrow.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        button.AddChild(arrow);
        arrow.AnchorLeft = arrow.AnchorRight = 1;
        arrow.AnchorTop = arrow.AnchorBottom = .5f;
        arrow.OffsetLeft = -26; arrow.OffsetRight = -20;
        arrow.OffsetTop = -4; arrow.OffsetBottom = 4;
    }
    private void BuildPaperRecipeButton()
    {
        var button = MakeButton("", OpenRecipeBook, 22); button.Name = "FormulaBookButton";
        CraftingPaperStyle.ApplyButton(button, CraftingPaperStyle.ButtonKind.Regular, 22);
        button.SetMeta("paper_role", "object_entry.recipe_notebook");
        Place(_canvas, button, new Vector2(24, 794), new Vector2(188, 100));
        var icon = IconRect(75); icon.Name = "FormulaBookIcon"; icon.Texture = LoadUiTexture("formula_notebook_v2_20261109.png");
        button.AddChild(icon); icon.Position = new Vector2(7, 12); icon.Size = new Vector2(75, 75);
        var title = PaperText("丹方册", 25); button.AddChild(title); title.Position = new Vector2(87, 32);
        SetHover(button, "查看丹方，或记下当前配伍");
    }
    private void BuildPaperSingleSlot(int slot, Vector2 position, Vector2 size, string name)
    {
        var card = PaperPanel(); card.Name = slot < 0 ? "JunHerbCard" : "ChenHerbCard"; Place(_canvas, card, position, size);
        var body = new Control(); card.AddChild(body);
        var title = PaperText(name, 28); Place(body, title, Vector2.Zero, new Vector2(140, 36));
        var clear = SmallAction("×", () => { if (slot < 0) { _main = null; RefreshPage(); } else ClearAssistant(slot); });
        Place(body, clear, new Vector2(156, 0), new Vector2(34, 34));
        var pick = MakeButton("", () => ShowPicker(slot), 22); pick.Name = $"PickHerb_{slot + 1}"; ClearButtonBorder(pick);
        Place(body, pick, new Vector2(0, 42), new Vector2(190, 133));
        var icon = IconRect(89); pick.AddChild(icon); icon.Position = new Vector2(50, 3); icon.Size = new Vector2(89, 88);
        var label = PaperText("选药", 22); label.HorizontalAlignment = HorizontalAlignment.Center;
        pick.AddChild(label); label.Position = new Vector2(0, 99); label.Size = new Vector2(190, 31);
        var spin = DoseBox(1); spin.ValueChanged += value => SetDose(slot, value);
        if (slot < 0) { _mainPickButton = pick; _mainIcon = icon; _mainName = label; _mainClearButton = clear; _mainDoseBox = spin; }
        else { _assistantPickButtons[slot] = pick; _assistantIcons[slot] = icon; _assistantNames[slot] = label; _assistantClearButtons[slot] = clear; _assistantDoseBoxes[slot] = spin; _assistantRows[slot] = card; _assistantSeals[slot] = new Control(); _canvas.AddChild(_assistantSeals[slot]); }
        Place(body, DoseStepper(slot), new Vector2(28, 191), new Vector2(139, 43));
    }
    private void BuildPaperRoleGroup(string title, int[] slots, Vector2 position)
    {
        var card = PaperPanel(17); card.Name = title == "佐药" ? "ZuoHerbGroup" : "ShiHerbGroup";
        Place(_canvas, card, position, new Vector2(284, 304));
        var body = new Control(); card.AddChild(body); _paperGroups.Add((card, body, slots));
        Place(body, PaperText(title, 28), Vector2.Zero, new Vector2(220, 40));
        foreach (int index in slots)
        {
            var row = new Control { Name = $"HerbSlot_{index}" }; body.AddChild(row); _assistantRows[index] = row;
            var pick = MakeButton("", () => ShowPicker(index), 20); pick.Name = $"PickHerb_{index + 1}"; ClearButtonBorder(pick);
            Place(row, pick, Vector2.Zero, new Vector2(220, 39)); _assistantPickButtons[index] = pick;
            var icon = IconRect(33); pick.AddChild(icon); icon.Position = Vector2.Zero; icon.Size = new Vector2(33, 36); _assistantIcons[index] = icon;
            var label = PaperText("＋ 添药", 20); pick.AddChild(label); label.Position = new Vector2(42, 2); label.Size = new Vector2(175, 34); _assistantNames[index] = label;
            var spin = DoseBox(1); spin.ValueChanged += value => SetDose(index, value); _assistantDoseBoxes[index] = spin;
            Place(row, DoseStepper(index), new Vector2(41, 42), new Vector2(92, 31));
            var clear = SmallAction("×", () => ClearAssistant(index)); CraftingPaperStyle.ApplyButton(clear, CraftingPaperStyle.ButtonKind.Icon, 19);
            clear.CustomMinimumSize = new Vector2(27, 29); Place(row, clear, new Vector2(223, 4), new Vector2(27, 29)); _assistantClearButtons[index] = clear;
            int ordinal = Array.IndexOf(slots, index);
            var seal = MakeButton($"第{(ordinal == 1 ? "二" : "三")}味 · 造诣{UnlockAt(index)}", () => { }, 17);
            CraftingPaperStyle.ApplyButton(seal, CraftingPaperStyle.ButtonKind.Regular, 17); seal.Disabled = true;
            body.AddChild(seal); _assistantSeals[index] = seal;
        }
    }
    private Control BuildPaperDoseStepper(int slot)
    {
        bool compact = slot > 0; float width = compact ? 92 : 101, height = compact ? 31 : 43;
        var row = new Control { Name = $"DoseRow_{slot + 1}" };
        var well = new PanelContainer(); var style = CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Input, horizontalPadding: 0, verticalPadding: 0);
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 0;
        well.AddThemeStyleboxOverride("panel", style); Place(row, well, Vector2.Zero, new Vector2(width, height));
        var content = new Control(); well.AddChild(content);
        var input = new LineEdit { Name = $"DoseInput_{slot + 1}", Text = "1", Alignment = HorizontalAlignment.Center, ContextMenuEnabled = false, SelectAllOnFocus = true };
        input.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); input.AddThemeStyleboxOverride("focus", new StyleBoxEmpty()); input.AddThemeStyleboxOverride("read_only", new StyleBoxEmpty());
        input.AddThemeFontSizeOverride("font_size", compact ? 23 : 26); input.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
        Place(content, input, new Vector2(4, 0), new Vector2(width - 29, height)); _paperDoseInputs[slot + 1] = input;
        void Commit()
        {
            if (double.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)) SetDose(slot, value);
            else RefreshPaperFields();
        }
        input.TextSubmitted += _ => Commit(); input.FocusExited += Commit;
        var up = new PaperTriangle { Up = true, Name = $"DosePlus_{slot + 1}", MouseDefaultCursorShape = CursorShape.PointingHand };
        var down = new PaperTriangle { Name = $"DoseMinus_{slot + 1}", MouseDefaultCursorShape = CursorShape.PointingHand };
        up.Pressed += () => SetDose(slot, (slot < 0 ? _mainDose : _assistantDoses[slot]) + 1d);
        down.Pressed += () => SetDose(slot, (slot < 0 ? _mainDose : _assistantDoses[slot]) - 1d);
        _paperDosePlus[slot + 1] = up; _paperDoseMinus[slot + 1] = down;
        Place(content, up, new Vector2(width - 23, 1), new Vector2(22, height / 2 - 1));
        Place(content, down, new Vector2(width - 23, height / 2), new Vector2(22, height / 2 - 1));
        foreach (var arrow in new[] { up, down }) foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) arrow.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        var summary = PaperText("1份", 18); summary.Visible = false; row.AddChild(summary); _doseLabels[slot + 1] = summary;
        if (!compact) Place(row, PaperText("份", 21), new Vector2(112, 3), new Vector2(26, 37));
        return row;
    }
    private void BuildPaperCosts()
    {
        var panel = PaperPanel(22); panel.Name = "BatchRequirements";
        Place(_canvas, panel, new Vector2(1431, 132), new Vector2(221, 684));
        var body = new Control(); panel.AddChild(body);
        var heading = PaperText("本炉用度", 27); heading.HorizontalAlignment = HorizontalAlignment.Center;
        Place(body, heading, Vector2.Zero, new Vector2(177, 38));
        var divider = CraftingPaperStyle.Divider();
        Place(body, divider, new Vector2(0, 53), new Vector2(177, 9));
        Place(body, PaperText("药材种类", 20, true), new Vector2(0, 77), new Vector2(110, 28));
        Place(body, PaperText("投料总量", 20, true), new Vector2(0, 108), new Vector2(110, 28));
        _paperKinds = PaperText("—", 20); _paperKinds.HorizontalAlignment = HorizontalAlignment.Right; Place(body, _paperKinds, new Vector2(100, 77), new Vector2(77, 28));
        _paperTotal = PaperText("—", 20); _paperTotal.HorizontalAlignment = HorizontalAlignment.Right; Place(body, _paperTotal, new Vector2(100, 108), new Vector2(77, 28));
        _paperSpirit = CostMetric(body, "灵识占用", 171);
        _paperEnergy = CostMetric(body, "精力消耗", 266);
        _paperHours = CostMetric(body, "炼制耗时", 361);
        _resourceSummary = PaperText("", 19); _resourceSummary.Name = "BatchSummary"; _resourceSummary.Visible = false; body.AddChild(_resourceSummary);
        _brewCondition = PaperText("", 16); _brewCondition.Name = "BrewCondition"; _brewCondition.AutowrapMode = TextServer.AutowrapMode.WordSmart; _brewCondition.ClipText = true; _brewCondition.AddThemeColorOverride("font_color", CraftingPaperStyle.Danger);
        Place(body, _brewCondition, new Vector2(0, 490), new Vector2(177, 83));
        _brewButton = MakeButton("炼丹", ShowBrewSimulation, 26); _brewButton.Name = "BrewButton";
        CraftingPaperStyle.ApplyButton(_brewButton, CraftingPaperStyle.ButtonKind.Primary, 26); Place(body, _brewButton, new Vector2(0, 584), new Vector2(177, 56));
    }
    private static Label CostMetric(Control parent, string name, float y)
    {
        var label = PaperText(name, 18, true); parent.AddChild(label); label.Position = new Vector2(0, y); label.Size = new Vector2(177, 27);
        var value = PaperText("—", 33); parent.AddChild(value); value.Position = new Vector2(0, y + 32); value.Size = new Vector2(177, 45); return value;
    }
    private void RefreshPaperCosts(int kinds, int total, int spirit, int energy, double oldHours)
    {
        _paperKinds.Text = $"{kinds} 味"; _paperTotal.Text = $"{total} 份";
        _paperSpirit.Text = kinds == 0 ? "—" : spirit.ToString(); _paperEnergy.Text = _main is null ? "—" : energy.ToString();
        _paperHours.Text = kinds == 0 ? "— 小时" : $"{oldHours * 2:0.##} 小时";
        _paperSpirit.AddThemeColorOverride("font_color", spirit > AlchemySlotSimulation.AvailableSpirit(TotalSpirit, OtherSpiritOccupation) ? CraftingPaperStyle.Danger : CraftingPaperStyle.Ink);
        _paperEnergy.AddThemeColorOverride("font_color", energy > (OwnerCharacter?.current_world_stamina ?? 100) ? CraftingPaperStyle.Danger : CraftingPaperStyle.Ink);
        _paperSpirit.MouseFilter = MouseFilterEnum.Pass;
        SetHover(_paperSpirit, _resourceSummary.GetMeta("alchemy_hover_text", "").AsString());
        _paperAttainment.Text = $"{Attainment:0.#}";
        SetHover(_brewCondition, _brewCondition.Text);
    }
    private void RefreshPaperFields()
    {
        if (_paperDoseInputs[0] is null) return;
        for (int i = 0; i < _paperDoseInputs.Length; i++)
        {
            int dose = i == 0 ? _mainDose : _assistantDoses[i - 1];
            _paperDoseInputs[i].Text = dose.ToString(); _paperDosePlus[i].Disabled = dose == int.MaxValue; _paperDoseMinus[i].Disabled = dose <= 1;
            _paperDosePlus[i].QueueRedraw(); _paperDoseMinus[i].QueueRedraw();
        }
        _doseLabels[0].GetParent<Control>().Visible = _main is not null;
        foreach (var (card, body, slots) in _paperGroups)
        {
            card.Size = new Vector2(284, SlotUnlocked(slots[2]) ? 326 : 304);
            float y = 50;
            foreach (int i in slots)
            {
                bool unlocked = SlotUnlocked(i), filled = _assistants[i] is not null;
                _assistantRows[i].Position = new Vector2(0, y); _assistantRows[i].Size = new Vector2(250, 77); _assistantRows[i].CustomMinimumSize = Vector2.Zero;
                _assistantSeals[i].Position = new Vector2(0, y + 4); _assistantSeals[i].Size = new Vector2(250, 48);
                _assistantPickButtons[i].Size = new Vector2(220, filled ? 39 : 44);
                _assistantNames[i].Position = new Vector2(filled ? 42 : 14, 2);
                _assistantIcons[i].Visible = filled;
                y += unlocked ? 77 : 59;
            }
        }
    }
    private void LoadPaperPresentationPreference()
    {
        var settings = new ConfigFile(); settings.Load("user://alchemy_presentation.cfg"); _simpleAlchemyPresentation = settings.GetValue("view", "simple", false).AsBool();
    }
    private sealed partial class PaperTriangle : Button
    {
        public bool Up { get; set; }
        public bool Right { get; set; }
        public override Vector2 _GetMinimumSize() => Vector2.Zero;
        public override void _Draw()
        {
            Color color = CraftingPaperStyle.Accent; if (Disabled) color.A = .35f;
            Vector2 center = Size / 2; Vector2[] points = Right ? [new(0, 0), new(Size.X, Size.Y / 2), new(0, Size.Y)] : Up ? [center + new Vector2(-5, 3), center + new Vector2(5, 3), center + new Vector2(0, -3)] : [center + new Vector2(-5, -3), center + new Vector2(5, -3), center + new Vector2(0, 3)];
            DrawColoredPolygon(points, color);
        }
    }
}
