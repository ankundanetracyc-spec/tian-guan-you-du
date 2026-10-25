using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>槽位式炼丹配伍界面。</summary>
public partial class AlchemySlotBrewPreviewScene : Control
{
    [Export] public double Attainment { get; set; } = AlchemySlotSimulation.TrialAttainment;
    [Export] public int TotalSpirit { get; set; } = AlchemySlotSimulation.SpiritCapacity;
    [Export] public int OtherSpiritOccupation { get; set; }
    public Character? OwnerCharacter { get; set; }
    public void ReturnToBaigong()
    {
        // 已扣料的炉次必须先完成演出及领取，不能由全局返回跳过。
        if (_brewing || _brewStage is not null) return;
        HoverInfoManager.HideAll();
        QueueFree();
    }
    private AlchemyFurnaceView _furnace = null!;
    private Label _energyHeaderValue = null!;
    private bool _brewing;
    private const string ItemDirectory = "res://resources/entities/items";
    private const string BackgroundPath = "res://textures/ui/alchemy_room_20261017/wall_only_front_v8.png";

    private sealed record MaterialChoice(string Path, ItemData Item, MaterialCraftData Craft)
    {
        public string Name => Item.entity_name;
        public int Grade(string utility) => Craft.utilities
            .Where(entry => entry is not null && entry.utility_id == utility)
            .Select(entry => Math.Clamp(entry.grade, 1, 9)).DefaultIfEmpty(0).Max();
        public string UtilityText => string.Join("、", Craft.utilities
            .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.utility_id))
            .GroupBy(entry => entry.utility_id)
            .Select(group => $"{group.Key}{MaterialCraftData.utility_grade_name(group.Max(entry => entry.grade))}"));
    }

    private readonly List<MaterialChoice> _materials = [];
    private readonly MaterialChoice?[] _assistants = new MaterialChoice?[7];
    private readonly int[] _assistantDoses = [1, 1, 1, 1, 1, 1, 1];
    private readonly Label[] _assistantNames = new Label[7];
    private readonly TextureRect[] _assistantIcons = new TextureRect[7];
    private readonly SpinBox[] _assistantDoseBoxes = new SpinBox[7];
    private readonly Button[] _assistantPickButtons = new Button[7];
    private readonly Button[] _assistantClearButtons = new Button[7];

    private readonly Control[] _assistantRows = new Control[7];
    private readonly Control[] _assistantSeals = new Control[7];
    private readonly Label[] _doseLabels = new Label[8];
    private Label _spiritHeaderValue = null!;
    private Label _brewCondition = null!;
    private PanelContainer _hoverCard = null!;
    private Label _hoverText = null!;
    private Control? _hoverTarget;
    private double _hoverDelay;
    private Control _canvas = null!;
    private MaterialChoice? _main;
    private MaterialChoice? _inspectedMaterial;
    private int _inspectedSlot = -1;
    private int _mainDose = 1;
    private Label _mainName = null!;
    private TextureRect _mainIcon = null!;
    private SpinBox _mainDoseBox = null!;
    private Button _mainPickButton = null!;
    private Button _mainClearButton = null!;
    private Label _resourceSummary = null!;
    private Button _brewButton = null!;
    private Control _recipeOverlay = null!;
    private Control _pickerOverlay = null!;
    private LineEdit _pickerSearch = null!;
    private GridContainer _pickerGrid = null!;
    private int _pickerTarget = -1;
    private SpinBox _attainmentInput = null!;
    private OptionButton _elementFilter = null!;
    private OptionButton _polarityFilter = null!;
    private CheckBox _effectiveOnly = null!;
    private readonly HashSet<string> _utilityFilters = new(StringComparer.Ordinal);
    private readonly List<Button> _utilityFilterButtons = [];
    private static readonly string[] FilterUtilities = ["破", "化", "通", "行", "引", "聚", "固", "镇", "护", "生", "明", "藏"];
    private Label _pickerCount = null!;
    private Label _pickerHint = null!;
    private Control _messageOverlay = null!;
    private Label _messageTitle = null!;
    private Label _messageBody = null!;
    private Control _tutorialOverlay = null!;

    public override void _Ready()
    {
        if (OwnerCharacter is not null)
        {
            Attainment = OwnerCharacter.AlchemyAttainment;
            TotalSpirit = OwnerCharacter.max_spirit_perception;
            OtherSpiritOccupation = OwnerCharacter.used_spirit_perception;
        }
        Theme = QuietRoomStyle.MakeTheme();
        Theme.SetStylebox("panel", "TooltipPanel", QuietRoomStyle.Paper(1, 16));
        Theme.SetColor("font_color", "TooltipLabel", QuietRoomStyle.Ink);
        Theme.SetFontSize("font_size", "TooltipLabel", 19);
        MouseFilter = MouseFilterEnum.Stop;
        LoadMaterials();
        BuildPage();
        RefreshPage();
        _tutorialOverlay.Show();
        if (OwnerCharacter is not null)
        {
            OwnerCharacter.InventoryChanged += OnAlchemyOwnerInventoryChanged;
            OwnerCharacter.WorldStaminaChanged += OnAlchemyOwnerInventoryChanged;
        }
        if (OS.GetCmdlineUserArgs().Contains("--alchemy-notebook-restart-write") ||
            OS.GetCmdlineUserArgs().Contains("--alchemy-notebook-restart-read"))
            RunNotebookRestartCheck();
        if (OS.GetCmdlineUserArgs().Contains("--alchemy-notebook-check"))
            _ = RunNotebookChecks();
        if (OS.GetCmdlineUserArgs().Contains("--alchemy-slot-ui-check"))
            _ = RunSlotUiChecks();
    }

    private void OnAlchemyOwnerInventoryChanged(Character _) => RefreshPage();
    public override void _ExitTree()
    {
        if (OwnerCharacter is not null)
        {
            OwnerCharacter.InventoryChanged -= OnAlchemyOwnerInventoryChanged;
            OwnerCharacter.WorldStaminaChanged -= OnAlchemyOwnerInventoryChanged;
        }
    }

    private void LoadMaterials()
    {
        using var directory = DirAccess.Open(ItemDirectory);
        if (directory is null) return;
        foreach (var file in directory.GetFiles().Where(name => name.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            // 骨材暂不进入这组三种九品丹的试演材料架。
            if (file.Contains("骨", StringComparison.Ordinal)) continue;
            var path = $"{ItemDirectory}/{file}";
            var item = GD.Load<ItemData>(path);
            if (item is null || item.item_category != "材料" || item.craft_data is not MaterialCraftData craft) continue;
            _materials.Add(new MaterialChoice(path, item, craft));
        }
    }

    private void BuildPage()
    {
        var background = new TextureRect
        {
            Name = "AlchemyRoomBackground",
            Texture = GD.Load<Texture2D>(BackgroundPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = Colors.White,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = QuietRoomStyle.Canvas(this);

        BuildFurnaceWatermark();
        BuildHeader();
        BuildAlchemyPresentationMode();
        BuildRecipeColumn();
        BuildSlotColumn();
        BuildPreviewColumn();
        BuildPicker();
        BuildRecipeOverlay();
        BuildTutorialOverlay();
        BuildMessageOverlay();
        BuildHoverCard();
    }

    private void BuildFurnaceWatermark()
    {
        // Use the same approved 2D front/rear renderer as the original alchemy room.
        _furnace = new AlchemyFurnaceView { Name = "ApprovedTwoLayerFurnace", MouseFilter = MouseFilterEnum.Ignore };
        Place(_canvas, _furnace, new Vector2(490, 72), new Vector2(608, 869));
        _furnace.SetState(.45, 30);
    }

    private void BuildHeader()
    {
        var back = MakeButton("〈 返回", () => { if (OwnerCharacter is not null) ReturnToBaigong(); else GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"); }, 19);
        ClearButtonBorder(back);
        back.AddThemeColorOverride("font_color", new Color("f0ead8"));
        back.AddThemeColorOverride("font_hover_color", Colors.White);
        Place(_canvas, back, new Vector2(26, 29), new Vector2(130, 48));

        var stats = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stats.AddThemeConstantOverride("separation", 24);
        Place(_canvas, stats, new Vector2(650, 24), new Vector2(720, 48));
        stats.AddChild(StatChip("灵识", $"{AlchemySlotSimulation.AvailableSpirit(TotalSpirit, OtherSpiritOccupation)}/{TotalSpirit}", value => _spiritHeaderValue = value));
        stats.AddChild(StatChip("精力", AlchemySlotSimulation.EnergyCapacity.ToString(), value => _energyHeaderValue = value));
        var attainmentLabel = QuietRoomStyle.Text(OwnerCharacter is null ? "试演造诣" : "丹道造诣", 18);
        attainmentLabel.AddThemeColorOverride("font_color", new Color("e4decd"));
        stats.AddChild(attainmentLabel);
        _attainmentInput = new SpinBox
        {
            Name = "AttainmentInput", MinValue = 0, MaxValue = 1196,
            AllowGreater = true, Step = .1, Value = Attainment,
            CustomMinimumSize = new Vector2(150, 44)
        };
        stats.AddChild(_attainmentInput);
        _attainmentInput.ValueChanged += SetTrialAttainment;
        if (OwnerCharacter is not null) { _attainmentInput.Editable = false; _attainmentInput.MouseFilter = MouseFilterEnum.Ignore; }
        var tutorial = MakeButton("教程", () => _tutorialOverlay.Show(), 17);
        ClearButtonBorder(tutorial);
        tutorial.AddThemeColorOverride("font_color", new Color("f0ead8"));
        tutorial.AddThemeColorOverride("font_hover_color", Colors.White);
        Place(_canvas, tutorial, new Vector2(1577, 26), new Vector2(76, 44));
    }

    private void SetTrialAttainment(double value)
    {
        if (OwnerCharacter is not null) { _attainmentInput.SetValueNoSignal(OwnerCharacter.AlchemyAttainment); return; }
        if (!double.IsFinite(value)) return;
        Attainment = Math.Max(0, value);
        HideHover();
        int cleared = 0;
        for (var i = 0; i < _assistants.Length; i++)
            if (!SlotUnlocked(i) && _assistants[i] is not null)
            {
                _assistants[i] = null;
                _assistantDoses[i] = 1;
                cleared++;
            }
        _inspectedMaterial = null;
        _inspectedSlot = -1;
        _messageOverlay.Hide();
        RefreshPage();
        if (cleared > 0)
            ShowMessage("试演造诣已调整", $"已清空{cleared}个尚未解锁药位的投料；其余药材保留。");
    }

    private Control StatChip(string name, string value, Action<Label>? onValueCreated = null)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        row.TooltipText = name == "灵识" ? "剩余可支配灵识 / 人物灵识总值\n剩余可支配为人物总值扣除其他事务占用；本炉需求见右侧。" : "";
        var label = QuietRoomStyle.Text(name, 15);
        label.AddThemeColorOverride("font_color", new Color("e4decd"));
        row.AddChild(label);
        var number = QuietRoomStyle.Text(value, 18);
        number.AddThemeColorOverride("font_color", new Color("fff9e8"));
        row.AddChild(number);
        onValueCreated?.Invoke(number);
        return row;
    }

    private void BuildRecipeColumn()
    {
        var recipe = MakeButton("", OpenRecipeBook, 20);
        recipe.Name = "FormulaBookButton";
        Place(_canvas, recipe, new Vector2(20, 794), new Vector2(186, 100));
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8; row.OffsetRight = -8;
        row.Alignment = BoxContainer.AlignmentMode.Center;
        recipe.AddChild(row);
        var icon = IconRect(70);
        icon.Texture = LoadUiTexture("formula_notebook_v1.png");
        row.AddChild(icon);
        var titles = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        titles.AddChild(QuietRoomStyle.Text("丹方册", 22));
        titles.AddChild(QuietRoomStyle.Text("样方 · 自拟", 13));
        row.AddChild(titles);
        SetHover(recipe, "查看样方，或把当前配伍保存为本地丹方");
    }

    private static AlchemySlotSimulation.SlotRole RoleFor(int slot) => slot switch
    {
        < 0 => AlchemySlotSimulation.SlotRole.Jun,
        0 => AlchemySlotSimulation.SlotRole.Chen,
        1 or 2 or 4 => AlchemySlotSimulation.SlotRole.Zuo,
        _ => AlchemySlotSimulation.SlotRole.Shi
    };

    private static int UnlockAt(int slot) => slot switch { 2 => 68, 4 => 508, 5 => 32, 6 => 203, _ => 8 };
    private bool SlotUnlocked(int slot) => Attainment >= UnlockAt(slot);

    private void BuildSlotColumn()
    {
        BuildMainSlot(new Vector2(274, 160), new Vector2(226, 270));
        BuildAssistantSlot(0, new Vector2(274, 494), new Vector2(226, 270));
        BuildRoleGroup("佐药", [1, 2, 4], new Vector2(1094, 154));
        BuildRoleGroup("使药", [3, 5, 6], new Vector2(1094, 496));
        _canvas.AddChild(_mainDoseBox); _mainDoseBox.Hide();
        foreach (var doseBox in _assistantDoseBoxes) { _canvas.AddChild(doseBox); doseBox.Hide(); }
    }

    private void BuildMainSlot(Vector2 position, Vector2 size)
    {
        var card = MakeInkSlip();
        card.Name = "JunHerbCard";
        Place(_canvas, card, position, size);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        card.AddChild(body);
        var header = new HBoxContainer(); body.AddChild(header);
        var title = QuietRoomStyle.Text("君药", 27); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
        _mainClearButton = SmallAction("×", () => { _main = null; RefreshPage(); }); header.AddChild(_mainClearButton);
        _mainPickButton = MakeButton("", () => ShowPicker(-1), 18);
        _mainPickButton.Name = "PickHerb_0";
        _mainPickButton.CustomMinimumSize = new Vector2(0, 126);
        _mainPickButton.SizeFlagsVertical = SizeFlags.ExpandFill;
        ClearButtonBorder(_mainPickButton); body.AddChild(_mainPickButton);
        var imageAndName = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _mainPickButton.AddChild(imageAndName); imageAndName.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _mainIcon = IconRect(84); _mainIcon.SizeFlagsVertical = SizeFlags.ExpandFill; imageAndName.AddChild(_mainIcon);
        _mainName = QuietRoomStyle.Text("点击选药", 20); _mainName.HorizontalAlignment = HorizontalAlignment.Center; imageAndName.AddChild(_mainName);
        _mainDoseBox = DoseBox(1); _mainDoseBox.ValueChanged += value => SetDose(-1, value);
        body.AddChild(DoseStepper(-1));
    }

    private void BuildAssistantSlot(int index, Vector2 position, Vector2 size)
    {
        var card = MakeInkSlip(); card.Name = "ChenHerbCard";
        Place(_canvas, card, position, size);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 8); card.AddChild(body);
        var header = new HBoxContainer(); body.AddChild(header);
        var title = QuietRoomStyle.Text("臣药", 27); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
        _assistantClearButtons[index] = SmallAction("×", () => ClearAssistant(index)); header.AddChild(_assistantClearButtons[index]);
        _assistantPickButtons[index] = MakeButton("", () => ShowPicker(index), 18);
        _assistantPickButtons[index].Name = $"PickHerb_{index + 1}";
        _assistantPickButtons[index].CustomMinimumSize = new Vector2(0, 126);
        _assistantPickButtons[index].SizeFlagsVertical = SizeFlags.ExpandFill;
        ClearButtonBorder(_assistantPickButtons[index]); body.AddChild(_assistantPickButtons[index]);
        var imageAndName = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _assistantPickButtons[index].AddChild(imageAndName); imageAndName.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _assistantIcons[index] = IconRect(84); _assistantIcons[index].SizeFlagsVertical = SizeFlags.ExpandFill; imageAndName.AddChild(_assistantIcons[index]);
        _assistantNames[index] = QuietRoomStyle.Text("点击选药", 20); _assistantNames[index].HorizontalAlignment = HorizontalAlignment.Center; imageAndName.AddChild(_assistantNames[index]);
        _assistantDoseBoxes[index] = DoseBox(1); _assistantDoseBoxes[index].ValueChanged += value => SetDose(index, value);
        body.AddChild(DoseStepper(index));
        _assistantRows[index] = card; _assistantSeals[index] = new Control();
        _canvas.AddChild(_assistantSeals[index]);
    }

    private void BuildRoleGroup(string title, int[] slots, Vector2 position)
    {
        var card = MakeInkSlip(); card.Name = title == "佐药" ? "ZuoHerbGroup" : "ShiHerbGroup";
        Place(_canvas, card, position, new Vector2(284, 304));
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 5); card.AddChild(body);
        body.AddChild(QuietRoomStyle.Text(title, 26));
        for (int ordinal = 0; ordinal < slots.Length; ordinal++)
        {
            int index = slots[ordinal];
            var row = new HBoxContainer { Name = $"HerbSlot_{index}", CustomMinimumSize = new Vector2(0, 70) };
            row.AddThemeConstantOverride("separation", 6); body.AddChild(row); _assistantRows[index] = row;
            var pick = MakeButton("", () => ShowPicker(index), 17); ClearButtonBorder(pick);
            pick.Name = $"PickHerb_{index + 1}";
            pick.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(pick); _assistantPickButtons[index] = pick;
            var contents = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; contents.AddThemeConstantOverride("separation", 6);
            pick.AddChild(contents); contents.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _assistantIcons[index] = IconRect(52); contents.AddChild(_assistantIcons[index]);
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            contents.AddChild(text);
            _assistantNames[index] = QuietRoomStyle.Text("＋ 添药", 18); text.AddChild(_assistantNames[index]);
            _assistantDoseBoxes[index] = DoseBox(1); _assistantDoseBoxes[index].ValueChanged += value => SetDose(index, value);
            text.AddChild(DoseStepper(index));
            _assistantClearButtons[index] = SmallAction("×", () => ClearAssistant(index)); row.AddChild(_assistantClearButtons[index]);
            var seal = MakeButton($"第{(ordinal == 1 ? "二" : "三")}味 · 造诣{UnlockAt(index)}", () => {}, 16);
            seal.Name = $"LockedSlot_{index}";
            seal.CustomMinimumSize = new Vector2(0, 42); seal.Disabled = true;
            seal.AddThemeStyleboxOverride("disabled", new StyleBoxFlat { BgColor = new Color("b3ae9622"), BorderColor = new Color("b3ae9666"), BorderWidthBottom = 1, ContentMarginLeft = 4, ContentMarginRight = 4 });
            SetHover(seal, $"有效炼丹造诣达到{UnlockAt(index)}后，可在{title}组添入这一味药材。");
            body.AddChild(seal); _assistantSeals[index] = seal;
        }
    }

    private void ClearAssistant(int slot) { _assistants[slot] = null; _assistantDoses[slot] = 1; RefreshPage(); }

    private Control DoseStepper(int slot)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 7);
        var less = SmallAction("−", () => SetDose(slot, (slot < 0 ? _mainDose : _assistantDoses[slot]) - 1));
        less.Name = $"DoseMinus_{slot + 1}"; row.AddChild(less);
        var label = QuietRoomStyle.Text("1份", 18); label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(40, 26); row.AddChild(label); _doseLabels[slot + 1] = label;
        var more = SmallAction("＋", () => SetDose(slot, (slot < 0 ? _mainDose : _assistantDoses[slot]) + 1));
        more.Name = $"DosePlus_{slot + 1}"; row.AddChild(more);
        return row;
    }

    private Button SmallAction(string text, Action action)
    {
        var button = MakeButton(text, action, 18); button.CustomMinimumSize = new Vector2(28, 28);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color("b9ad8755"), CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14
        };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("b9ad8799"), CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14 });
        if (text == "×") button.AddThemeColorOverride("font_color", new Color("975b47"));
        return button;
    }

    private static void ClearButtonBorder(Button button)
    {
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", QuietRoomStyle.Edge(true, 0));
        button.AddThemeStyleboxOverride("pressed", QuietRoomStyle.Edge(true, 0));
    }

    private void BuildPreviewColumn()
    {
        var panel = MakeInkSlip(vertical: true); panel.Name = "BatchRequirements";
        Place(_canvas, panel, new Vector2(1454, 132), new Vector2(202, 684));
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 18); panel.AddChild(body);
        var heading = QuietRoomStyle.Text("本炉用度", 25); heading.HorizontalAlignment = HorizontalAlignment.Center; body.AddChild(heading);
        body.AddChild(HSeparatorLine());
        body.AddChild(JournalLine("产量由药材配伍与用量决定，开炉后揭晓。"));
        _resourceSummary = QuietRoomStyle.Text("", 19); _resourceSummary.Name = "BatchSummary"; _resourceSummary.AddThemeConstantOverride("line_spacing", 12);
        _resourceSummary.SizeFlagsVertical = SizeFlags.ExpandFill; body.AddChild(_resourceSummary);
        _brewCondition = QuietRoomStyle.Text("先选择君药", 14); _brewCondition.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _brewCondition.AddThemeColorOverride("font_color", new Color("8d5645")); body.AddChild(_brewCondition);
        _brewButton = MakeButton("炼丹", ShowBrewSimulation, 28); _brewButton.Name = "BrewButton";
        _brewButton.CustomMinimumSize = new Vector2(0, 62); ApplyInkBrushButtonStyle(_brewButton); body.AddChild(_brewButton);
    }

    private static Label JournalLine(string text)
    {
        var label = QuietRoomStyle.Text(text, 15);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.AddThemeColorOverride("font_color", new Color("424d43"));
        return label;
    }

    private void BuildTutorialOverlay()
    {
        // Keep modal layers inside the same fixed design canvas as the furnace UI.
        // The root Control has not completed its layout pass yet during _Ready,
        // which can leave a centered panel anchored around the origin.
        _tutorialOverlay = new Control { Name = "AlchemyTutorialOverlay", Visible = false, MouseFilter = MouseFilterEnum.Stop, ZIndex = 100 };
        _canvas.AddChild(_tutorialOverlay);
        _tutorialOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(.08f, .10f, .09f, .48f), MouseFilter = MouseFilterEnum.Stop };
        _tutorialOverlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = MakePaperPanel(.98f, 20);
        PlaceCentered(_tutorialOverlay, panel, new Vector2(1136, 842));
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 10);
        panel.AddChild(layout);

        var header = new HBoxContainer();
        layout.AddChild(header);
        var title = QuietRoomStyle.Text("炼丹入门 · 君臣佐使与配伍", 25);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.AddThemeColorOverride("font_color", new Color("365b50"));
        header.AddChild(title);
        header.AddChild(MakeButton("关闭", () => _tutorialOverlay.Hide(), 15));

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(scroll);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(content);

        AddTutorialSection(content, "一、先配伍，开炉后才知成丹", "普通炼丹从投料开始，不预先选择目标丹药。你先安排君、臣、佐、使与整数剂量；炼前只核对已知药材信息和本次资源需求，不显示丹种、颗数、品质或成败预估。点击炼丹后，系统读取君药核心功用并结算整炉药性，结果此时才揭晓。\n\n君药、臣药各一种；佐药、使药各最多三种。造诣32／68／203／508依次解锁第二味使药、第二味佐药、第三味使药、第三味佐药。\n\n左下角丹方笺可载入一套起步配伍；载入后仍能换药、调量或移位，样方不限制其他解法。");
        AddTutorialSection(content, "二、君臣佐使：四类药位", "君位：提供成品识别核心，并决定颗数基础。\n臣位：可用「聚」争取更多丹；可用「固」扶持成形。\n佐位：可用「破、化」争取得率，也可用「固、镇、护、明、藏」处理稳形、冲突、危险、纯度与封存。\n使位：可用「通、行、引、藏」处理相合、送达、主效导向与留存。\n\n这些是角色能发挥的功用范围，不是必须照抄的格子。放错角色的功用不会触发对应效果，但该材料的五行、阴阳、原料和灵识负担仍会计入。换材料、换位置、换剂量都可能形成另一条有效路线。");
        AddTutorialSection(content, "三、以尝试积累经验", "不载入丹方时，先选一味核心功用合适的君药，臣、佐、使可以留空，再自由替换材料、剂量与角色位。炼前只会看到本炉材料、已知五行阴阳与资源需求；每一次尝试的结果要等点击炼丹后才会出现。\n\n成功不要求凑齐固定辅药，也不按材料味数封顶；成色来自每颗实际得到的核心药力、五行阴阳、通路、稳形和纯度。同一种丹药可以用不同材料路线炼成。");
        AddTutorialSection(content, "四、五行、阴阳与资源", "五行生克会改变各味药性发挥；阴阳偏性影响配伍相合，阴阳相激会损失混合效率，过烈时还会带来炸炉危险。「镇」缓和相激并改善混合；「护」只降低炸炉危险，不补回混合损耗。\n\n灵识占用取决于材料品阶、本位发挥的功用数量与品级、药位和剂量。君臣位责任较重；高阶、多功用材料通常更难驾驭。增加同味剂量会缓慢增加占用。本位不发挥的功用不收功用负担，但材料本身仍需灵识照看。\n\n顶部显示剩余可支配灵识／人物灵识总值。左边扣除其他事务占用，右边始终是人物总值，配伍不提前扣除左边余额。本炉灵识需求显示在右侧用度栏，悬停查看各味分摊；需求不得超过顶部左边的剩余额度。悬停药材查看该药位、剂量的占用明细。超限请换药、减量或调整药位。正式入口读取人物真实数据；独立试演总灵识24，其他占用0。\n\n精力按丹阶、产量、材料种数、功用组合和总投料计算。右侧只列本次用度，不提前揭晓炼制结果。");
        AddTutorialSection(content, "五、十二功用：按短板选路线", "破：佐位拆解致密材料残余，改善有效得率。\n化：佐位转化全炉残余；可与破互补，也可少用破、专注处理总残余。\n通：使位改善多味材料的相合程度。\n行：使位提高药力送达比例。\n引：使位从其他丹胚转移一部分君药药性给首颗；总药性守恒，一颗时没有可转移来源。\n聚：臣位影响本炉可形成的丹药数量；数量由配伍与剂量自动决定。\n固：臣或佐位支撑成形，是稳形路线之一。\n镇：佐位缓和阴阳相激，兼顾混合与危险。\n护：佐位降低炸炉危险，不补回混合损耗。\n生：可作为君药核心之一；当前九品试演中对应回春丹方向，其他已知丹种由各自核心功用识别。\n明：佐位降低残余夹带、改善纯度。\n藏：佐／使位减少已炼出药性在炉内的自然散失；不改变同档成品的效果或时长。\n\n功用品级会按造诣掌控率折算。同一目标有不同办法：例如用「化」减少残余，或用「明」降低残余夹带；调阴阳可以直接换材料，也可以用「镇」处理相激。不同路线各有成本与边界。");

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        layout.AddChild(footer);
        AddTutorialSection(content, "六、自动分丹、逐颗丹效与正式消耗", "本炉自动形成0—4颗候选丹，数量由君药实际可用药性、剂量及臣位聚性决定，不设置颗数选择。各颗再独立判断是否成丹；想改变数量，需要调整配伍或用量。引只在丹胚之间搬运君药药性，不增加总药性。\n\n每颗独立按关键短板判分，同炉可以出现相邻丹效档；少数丹被引性集中时，其余丹可能降档。同档成品药效一致并可堆叠：薄效20—50、良效51—75、优效76—90、极效91—99、满效100，低于20不成丹。高档需要明显更好的实际配伍，核心药力过量不能抵消阴阳或纯度缺陷。\n\n百工正式入口使用人物行囊和精力：预检失败零消耗，普通失败或炸炉也消耗投料与精力。成功丹药按档入包；空间不足不会吞料。独立试演免扣资源。正式炼制结束按显示耗时推进历法。丹方只保存药材、位置与剂量，不保存指定产量。结果逐颗展示具体丹效分，悬停查看物品信息；同档药效相同、入包后合并堆叠。");
        var example = MakeButton("载入回春丹样方甲", () => ApplySampleRoute(
            AlchemyRecipeCatalog.GetPill("huichun").SampleRoutes[2], true), 16);
        example.CustomMinimumSize = new Vector2(280, 46);
        footer.AddChild(example);
        var note = QuietRoomStyle.Text("左下角【丹方】可载入十五套样例，只自动填料，不指定成品；替换药材、角色位或整数份数后可重新炼丹。", 14);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.AddThemeColorOverride("font_color", new Color("727b70"));
        footer.AddChild(note);
    }

    private void BuildMessageOverlay()
    {
        _messageOverlay = new Control
        {
            Name = "AlchemyMessageOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 200
        };
        _canvas.AddChild(_messageOverlay);
        _messageOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(.08f, .10f, .09f, .70f), MouseFilter = MouseFilterEnum.Stop };
        _messageOverlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = MakePaperPanel(.98f, 20);
        PlaceCentered(_messageOverlay, panel, new Vector2(920, 680));
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        panel.AddChild(layout);

        var header = new HBoxContainer();
        layout.AddChild(header);
        _messageTitle = QuietRoomStyle.Text("炼丹配伍", 24);
        _messageTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _messageTitle.AddThemeColorOverride("font_color", new Color("304d43"));
        header.AddChild(_messageTitle);
        header.AddChild(MakeButton("关闭", () => _messageOverlay.Hide(), 15));

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(scroll);
        _messageBody = QuietRoomStyle.Text("", 17);
        _messageBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _messageBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _messageBody.AddThemeColorOverride("font_color", new Color("303933"));
        scroll.AddChild(_messageBody);

        var close = MakeButton("明白", () => _messageOverlay.Hide(), 16);
        close.CustomMinimumSize = new Vector2(120, 42);
        close.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        layout.AddChild(close);
    }

    private void ShowMessage(string title, string body)
    {
        _messageTitle.Text = title;
        _messageBody.Text = body;
        _messageOverlay.Show();
    }

    private void PlaceCentered(Control parent, Control control, Vector2 requestedSize)
    {
        parent.AddChild(control);
        // Modal parents are placed on _canvas, whose design size is assigned
        // synchronously by QuietRoomStyle.Canvas. Use that local size so sizing
        // and centering share the same coordinates as the page artwork.
        var available = parent.Size;
        if (available.X <= 0 || available.Y <= 0) available = GetViewportRect().Size;
        var size = new Vector2(
            Math.Min(requestedSize.X, Math.Max(320, available.X - 48)),
            Math.Min(requestedSize.Y, Math.Max(300, available.Y - 48)));
        control.AnchorLeft = control.AnchorRight = 0.5f;
        control.AnchorTop = control.AnchorBottom = 0.5f;
        control.OffsetLeft = -size.X / 2;
        control.OffsetRight = size.X / 2;
        control.OffsetTop = -size.Y / 2;
        control.OffsetBottom = size.Y / 2;
    }

    private static string AssistantRoleTitle(int index) => AssistantRoleShort(index);
    private static string AssistantRoleShort(int index) => RoleFor(index) switch
    {
        AlchemySlotSimulation.SlotRole.Chen => "臣药",
        AlchemySlotSimulation.SlotRole.Zuo => "佐药",
        _ => "使药"
    };
    private static string AssistantRoleHint(int index) => RoleFor(index) switch
    {
        AlchemySlotSimulation.SlotRole.Chen => "聚、固",
        AlchemySlotSimulation.SlotRole.Zuo => "破、化、固、镇、护、明、藏",
        _ => "通、行、引、藏"
    };

    private static void AddTutorialSection(VBoxContainer parent, string title, string text)
    {
        var section = MakePaperPanel(.90f, 14);
        section.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(section);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 5);
        section.AddChild(body);
        var heading = QuietRoomStyle.Text(title, 18);
        heading.AddThemeColorOverride("font_color", new Color("3d6255"));
        body.AddChild(heading);
        var paragraph = QuietRoomStyle.Text(text, 15);
        paragraph.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        paragraph.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        paragraph.AddThemeColorOverride("font_color", new Color("4e554b"));
        body.AddChild(paragraph);
    }

    private void ApplySampleRoute(AlchemyRecipeCatalog.SampleRoute route, bool closeTutorial = false)
    {
        MaterialChoice? Find(string name) => _materials.FirstOrDefault(material => material.Name == name);
        var resolved = route.Ingredients.Select(ingredient => (Ingredient: ingredient, Material: Find(ingredient.HerbName))).ToArray();
        var missing = resolved.Where(entry => entry.Material is null).Select(entry => entry.Ingredient.HerbName).Distinct().ToArray();
        if (missing.Length > 0)
        {
            ShowMessage("载入样方", $"材料册缺少：{string.Join("、", missing)}。可以手动选择其他材料继续配伍。");
            return;
        }

        var unavailable = route.Ingredients.FirstOrDefault(ingredient => ingredient.AssistantSlot >= 0 && !SlotUnlocked(ingredient.AssistantSlot));
        if (unavailable is not null)
        {
            ShowMessage("药位尚未解锁", $"此方需要{AssistantRoleShort(unavailable.AssistantSlot)}扩展药位，须造诣{UnlockAt(unavailable.AssistantSlot)}。当前投料保留，可另选入门四味方。");
            return;
        }

        _main = null;
        Array.Clear(_assistants);
        Array.Fill(_assistantDoses, 1);
        _mainDose = 1;
        foreach (var entry in resolved)
        {
            var ingredient = entry.Ingredient;
            var material = entry.Material!;
            if (ingredient.AssistantSlot < 0)
            {
                _main = material;
                _mainDose = ingredient.Dose;
            }
            else if (ingredient.AssistantSlot < _assistants.Length)
            {
                _assistants[ingredient.AssistantSlot] = material;
                _assistantDoses[ingredient.AssistantSlot] = ingredient.Dose;
            }
        }

        _inspectedSlot = -1;
        _inspectedMaterial = _main;
        _mainDoseBox.SetValueNoSignal(_mainDose);
        for (int i = 0; i < _assistantDoses.Length; i++)
            _assistantDoseBoxes[i].SetValueNoSignal(_assistantDoses[i]);
        if (closeTutorial) _tutorialOverlay.Hide();
        RefreshPage();
    }

    private void BuildPicker()
    {
        _pickerOverlay = new Control { Name = "MaterialPickerOverlay", Visible = false, ZIndex = 100 };
        _pickerOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas.AddChild(_pickerOverlay);
        var veil = new ColorRect { Color = new Color(.10f, .14f, .12f, .56f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pickerOverlay.AddChild(veil);
        var panel = MakePaperPanel(.99f, 20);
        panel.Position = new Vector2(230, 112);
        panel.Size = new Vector2(1212, 716);
        _pickerOverlay.AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 12);
        panel.AddChild(body);
        var header = new HBoxContainer();
        body.AddChild(header);
        _pickerSearch = new LineEdit { PlaceholderText = "搜索药材名称、五行或功用", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pickerSearch.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
        _pickerSearch.AddThemeColorOverride("font_placeholder_color", new Color("788276"));
        _pickerSearch.TextChanged += _ => RefreshPickerGrid();
        header.AddChild(_pickerSearch);
        header.AddChild(MakeButton("关闭", () => _pickerOverlay.Hide(), 16));
        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 14);
        body.AddChild(filters);
        filters.AddChild(QuietRoomStyle.Text("五行", 16));
        _elementFilter = new OptionButton { Name = "ElementFilter", CustomMinimumSize = new Vector2(136, 36) };
        foreach (var text in new[] { "全部五行", "金", "木", "水", "火", "土" }) _elementFilter.AddItem(text);
        filters.AddChild(_elementFilter);
        filters.AddChild(QuietRoomStyle.Text("阴阳", 16));
        _polarityFilter = new OptionButton { Name = "PolarityFilter", CustomMinimumSize = new Vector2(136, 36) };
        foreach (var text in new[] { "全部阴阳", "阴", "和", "阳" }) _polarityFilter.AddItem(text);
        filters.AddChild(_polarityFilter);
        _effectiveOnly = new CheckBox { Name = "EffectiveOnlyFilter", Text = "仅显示本位功用有效" };
        filters.AddChild(_effectiveOnly);
        var resetFilters = MakeButton("重置筛选", () =>
        {
            _elementFilter.Select(0);
            _polarityFilter.Select(0);
            _effectiveOnly.ButtonPressed = false;
            _utilityFilters.Clear();
            foreach (var button in _utilityFilterButtons)
            {
                button.SetPressedNoSignal(false);
                button.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(false, 8));
            }
            _pickerSearch.Clear();
            RefreshPickerGrid();
        }, 15);
        resetFilters.Name = "ResetPickerFilters";
        filters.AddChild(resetFilters);
        _elementFilter.ItemSelected += _ => RefreshPickerGrid();
        _polarityFilter.ItemSelected += _ => RefreshPickerGrid();
        _effectiveOnly.Toggled += _ => RefreshPickerGrid();
        var utilities = new HBoxContainer();
        utilities.AddThemeConstantOverride("separation", 8);
        body.AddChild(utilities);
        utilities.AddChild(QuietRoomStyle.Text("功用", 16));
        foreach (var utility in FilterUtilities)
        {
            var selectedUtility = utility;
            var button = MakeButton(utility, () => { }, 16);
            button.Name = $"UtilityFilter_{utility}";
            button.ToggleMode = true;
            button.CustomMinimumSize = new Vector2(44, 36);
            button.Toggled += pressed =>
            {
                if (pressed) _utilityFilters.Add(selectedUtility);
                else _utilityFilters.Remove(selectedUtility);
                button.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(pressed, 8));
                RefreshPickerGrid();
            };
            _utilityFilterButtons.Add(button);
            utilities.AddChild(button);
        }
        utilities.AddChild(QuietRoomStyle.Text("多选 · 任一匹配", 13));
        _pickerHint = QuietRoomStyle.Text("", 15);
        _pickerHint.AddThemeColorOverride("font_color", new Color("71796e"));
        body.AddChild(_pickerHint);
        _pickerCount = QuietRoomStyle.Text("", 15);
        body.AddChild(_pickerCount);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        _pickerGrid = new GridContainer { Name = "MaterialChoiceGrid", Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pickerGrid.AddThemeConstantOverride("h_separation", 10);
        _pickerGrid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(_pickerGrid);
    }

    private void ShowPicker(int target)
    {
        if (target >= 0 && !SlotUnlocked(target)) return;
        _pickerTarget = target;
        _pickerSearch.Clear();
        RefreshPickerGrid();
        _pickerOverlay.Show();
        _pickerSearch.GrabFocus();
    }

    private void RefreshPickerGrid()
    {
        foreach (var child in _pickerGrid.GetChildren())
        {
            _pickerGrid.RemoveChild(child);
            child.QueueFree();
        }
        HideHover();
        var query = _pickerSearch.Text.Trim();
        var element = _elementFilter.Selected == 0 ? "" : _elementFilter.GetItemText(_elementFilter.Selected);
        var polarity = _polarityFilter.Selected == 0 ? "" : _polarityFilter.GetItemText(_polarityFilter.Selected);
        var available = _materials.Where(material => IsAvailableInSlot(material, _pickerTarget)).ToArray();
        var choices = available
            .Where(material => query.Length == 0 || PickerText(material).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(material => element.Length == 0 || material.Craft.element == element)
            .Where(material => polarity.Length == 0 || PolarityName(material.Craft.polarity) == polarity)
            .Where(material => _utilityFilters.Count == 0 || material.Craft.utilities.Any(
                utility => utility is not null && _utilityFilters.Contains(utility.utility_id)))
            .Where(material => !_effectiveOnly.ButtonPressed || material.Craft.utilities.Any(
                utility => utility is not null && FunctionActsInSlot(_pickerTarget, utility.utility_id))).ToArray();
        _pickerCount.Text = choices.Length == 0 ? "没有符合筛选条件的材料，可放宽筛选或重置。" : $"显示 {choices.Length} / {available.Length} 种可选材料";
        _pickerHint.Text = $"当前药位：{(_pickerTarget < 0 ? "君" : AssistantRoleShort(_pickerTarget))}。本位有效指至少一项功用可在此位发挥，不代表保证成丹。";
        foreach (var material in choices)
        {
            var choice = material;
            var tile = MakeButton("", () => ChooseMaterial(choice), 15);
            tile.Name = $"Material_{choice.Name}";
            tile.CustomMinimumSize = new Vector2(0, 104);
            tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            ApplyPaperButtonStyle(tile);
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 12; row.OffsetRight = -12; row.OffsetTop = 10; row.OffsetBottom = -10;
            tile.AddChild(row);
            var icon = IconRect(56);
            icon.Texture = MaterialIcon(choice);
            row.AddChild(icon);
            var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            labels.AddThemeConstantOverride("separation", 3);
            row.AddChild(labels);
            var name = QuietRoomStyle.Text(choice.Name, 16);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            labels.AddChild(name);
            SetHover(tile, MaterialTooltip(choice, _pickerTarget));
            _pickerGrid.AddChild(tile);
        }
    }

    private bool IsAvailableInSlot(MaterialChoice material, int target)
    {
        if (OwnerCharacter is not null && !OwnerCharacter.has_inventory_item(material.Path)) return false;
        if (target == -1 && _main?.Path == material.Path) return true;
        if (target >= 0 && _assistants[target]?.Path == material.Path) return true;
        if (_main?.Path == material.Path && target != -1) return false;
        for (var i = 0; i < _assistants.Length; i++)
            if (i != target && _assistants[i]?.Path == material.Path) return false;
        return true;
    }

    private string PickerText(MaterialChoice material) =>
        $"{material.Name} {material.Craft.element} {PolarityName(material.Craft.polarity)} {material.Craft.texture} {material.UtilityText}";

    private void ChooseMaterial(MaterialChoice material)
    {
        if (_pickerTarget == -1)
        {
            _main = material;
            _inspectedMaterial = material;
            _inspectedSlot = -1;
            SetDose(-1, _mainDose);
        }
        else
        {
            _assistants[_pickerTarget] = material;
            _inspectedMaterial = material;
            _inspectedSlot = _pickerTarget;
            SetDose(_pickerTarget, _assistantDoses[_pickerTarget]);
        }
        _pickerOverlay.Hide();
        RefreshPage();
    }

    private void RefreshPage()
    {
        _mainName.Text = _main?.Name ?? "点击选药";
        _doseLabels[0].Text = $"{_mainDose}份";
        SetHover(_mainPickButton, _main is null ? "选择君药，君药决定成品识别核心" : MaterialTooltip(_main, -1));
        _mainIcon.Texture = _main is null ? null : MaterialIcon(_main);
        _mainClearButton.Visible = _main is not null;
        for (var i = 0; i < _assistants.Length; i++)
        {
            var material = _assistants[i];
            _assistantNames[i].Text = material?.Name ?? (i == 0 ? "点击选药" : "＋ 添药");
            _doseLabels[i + 1].Text = $"{_assistantDoses[i]}份";
            _doseLabels[i + 1].GetParent<Control>().Visible = material is not null;
            _assistantRows[i].Visible = SlotUnlocked(i);
            _assistantSeals[i].Visible = !SlotUnlocked(i);
            SetHover(_assistantPickButtons[i], material is null ? $"选择{AssistantRoleShort(i)}" : MaterialTooltip(material, i));
            _assistantIcons[i].Texture = material is null ? null : MaterialIcon(material);
            _assistantIcons[i].Visible = material is not null;
            if (i > 0) _assistantRows[i].CustomMinimumSize = new Vector2(0, material is null ? 36 : 70);
            _assistantClearButtons[i].Visible = material is not null;
        }
        if (_inspectedMaterial is null || (_inspectedSlot < 0 && _main is null) || (_inspectedSlot >= 0 && _assistants[_inspectedSlot] is null))
        {
            _inspectedSlot = _main is not null ? -1 : Array.FindIndex(_assistants, material => material is not null);
            _inspectedMaterial = _inspectedSlot < 0 ? null : _assistants[_inspectedSlot];
            if (_inspectedSlot == -1) _inspectedMaterial = _main;
        }
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        var selections = GetSelections();
        var materials = selections.Select(entry => entry.Material).DistinctBy(material => material.Path).ToArray();
        var count = materials.Length;
        var totalDose = selections.Sum(entry => entry.Dose);
        var simulationInputs = GetSimulationInputs();
        if (OwnerCharacter is not null)
        {
            Attainment = OwnerCharacter.AlchemyAttainment;
            _attainmentInput.SetValueNoSignal(Attainment);
            TotalSpirit = OwnerCharacter.max_spirit_perception;
            OtherSpiritOccupation = OwnerCharacter.used_spirit_perception;
        }
        var simulation = AlchemySlotSimulation.Simulate(simulationInputs, Attainment, TotalSpirit, OtherSpiritOccupation);
        var spiritOccupation = AlchemySlotSimulation.SpiritOccupation(simulationInputs);

        var hours = AlchemySlotSimulation.DurationHours(simulationInputs);
        int availableSpirit = AlchemySlotSimulation.AvailableSpirit(TotalSpirit, OtherSpiritOccupation);
        _spiritHeaderValue.Text = $"{availableSpirit}/{TotalSpirit}";
        _spiritHeaderValue.MouseFilter = MouseFilterEnum.Pass;
        _spiritHeaderValue.Modulate = Colors.White;
        SetHover(_spiritHeaderValue, $"剩余可支配灵识 / 人物灵识总值\n人物总值 {TotalSpirit}，其他事务占用 {OtherSpiritOccupation}，剩余可支配 {availableSpirit}。\n当前配伍不提前占用灵识；本炉需求在右侧列出，开炼时只能使用剩余额度。");
        _resourceSummary.Modulate = spiritOccupation > availableSpirit ? new Color(.65f, .22f, .16f) : Colors.White;
        _resourceSummary.MouseFilter = MouseFilterEnum.Pass;
        SetHover(_resourceSummary, "本炉灵识占用明细\n各味保留小数，整炉合计后向上取整。\n\n" +
            string.Join("\n", AlchemySlotSimulation.SpiritLoads(simulationInputs).Select(line =>
                $"{line.Material.Name}　{line.Material.Dose}份　占用{line.Occupation:0.00}")));
        _resourceSummary.Text = $"药材种类　{count}味\n投料总量　{totalDose}份\n\n本炉灵识占用\n{spiritOccupation}\n\n精力消耗\n{simulation.EnergyNeed}\n\n炼制耗时\n{(count == 0 ? "—" : $"{hours:0.##}时辰")}";
        _brewCondition.Text = simulation.CanSimulate ? "" : simulation.BlockingReason;
        _brewButton.Disabled = !simulation.CanSimulate || _brewing || _brewStage is not null;
        if (_energyHeaderValue is not null) _energyHeaderValue.Text = $"{(OwnerCharacter?.current_world_stamina ?? 100):0.#}";
        if (OwnerCharacter is not null)
        {
            var missing = simulationInputs.FirstOrDefault(input => !OwnerCharacter.has_inventory_item(input.Path, input.Dose));
            if (missing is not null) { _brewCondition.Text = $"{missing.Name}数量不足，需要{missing.Dose}份。"; _brewButton.Disabled = true; }
            else if (!OwnerCharacter.can_spend_world_stamina(simulation.EnergyNeed)) { _brewCondition.Text = "当前精力不足。"; _brewButton.Disabled = true; }
        }
    }

    private List<AlchemySlotSimulation.Input> GetSimulationInputs()
    {
        var result = new List<AlchemySlotSimulation.Input>();
        if (_main is not null) result.Add(new(_main.Name, _main.Path, _main.Craft, _mainDose, AlchemySlotSimulation.SlotRole.Jun));
        for (var i = 0; i < _assistants.Length; i++)
            if (_assistants[i] is not null)
            {
                var role = RoleFor(i);
                result.Add(new(_assistants[i]!.Name, _assistants[i]!.Path, _assistants[i]!.Craft, _assistantDoses[i], role));
            }
        return result;
    }

    private void SetDose(int slot, double requested)
    {
        int value = (int)Math.Clamp(Math.Round(requested, MidpointRounding.AwayFromZero), 1, int.MaxValue);
        if (slot < 0)
        {
            _mainDose = value;
            if (Math.Abs(_mainDoseBox.Value - value) > .001) _mainDoseBox.SetValueNoSignal(value);
        }
        else
        {
            _assistantDoses[slot] = value;
            if (Math.Abs(_assistantDoseBoxes[slot].Value - value) > .001) _assistantDoseBoxes[slot].SetValueNoSignal(value);
        }
        _doseLabels[slot + 1].Text = $"{value}份";
        RefreshSummary();
    }

    private List<(MaterialChoice Material, int Dose)> GetSelections()
    {
        var result = new List<(MaterialChoice, int)>();
        if (_main is not null) result.Add((_main, _mainDose));
        for (var i = 0; i < _assistants.Length; i++) if (_assistants[i] is not null) result.Add((_assistants[i]!, _assistantDoses[i]));
        return result;
    }

    private static string FormatElements(List<(MaterialChoice Material, int Dose)> selections)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var entry in selections)
        {
            var element = entry.Material.Craft.element.Trim();
            if (element.Length == 0) continue;
            values[element] = values.GetValueOrDefault(element) + entry.Dose;
        }
        var sum = values.Values.Sum();
        return sum <= 0 ? "—" : string.Join("　", values.OrderByDescending(pair => pair.Value)
            .Select(pair => $"{pair.Key} {pair.Value / sum:P0}"));
    }

    private static string FormatPolarity(List<(MaterialChoice Material, int Dose)> selections)
    {
        var negative = selections.Where(entry => entry.Material.Craft.polarity < 0).Sum(entry => entry.Dose);
        var neutral = selections.Where(entry => entry.Material.Craft.polarity == 0).Sum(entry => entry.Dose);
        var positive = selections.Where(entry => entry.Material.Craft.polarity > 0).Sum(entry => entry.Dose);
        var total = negative + neutral + positive;
        return total <= 0 ? "—" : $"阴 {(double)negative / total:P0}　和 {(double)neutral / total:P0}　阳 {(double)positive / total:P0}";
    }

    private void ShowDetails()
    {
        var rows = new List<string>();
        if (_main is not null) rows.Add($"君药　{_main.Name} × {_mainDose:0}份\n　五行{_main.Craft.element} · {PolarityName(_main.Craft.polarity)} · {_main.Craft.texture}\n　功用：{_main.UtilityText}");
        for (var i = 0; i < _assistants.Length; i++)
        {
            var material = _assistants[i];
            if (material is null) continue;
            rows.Add($"{AssistantRoleShort(i)}　{material.Name} × {_assistantDoses[i]:0}份\n　五行{material.Craft.element} · {PolarityName(material.Craft.polarity)} · {material.Craft.texture}\n　功用：{material.UtilityText}");
        }
        var body = rows.Count == 0 ? "尚未选择药材。" : string.Join("\n\n", rows);
        ShowMessage("本炉药材", body);
    }

    private void ShowBrewSimulation() => _ = BrewWithPresentation();

    private void ClearSelections()
    {
        _main = null;
        Array.Clear(_assistants);
        _mainDose = 1;
        Array.Fill(_assistantDoses, 1);
        _inspectedMaterial = null;
        _inspectedSlot = -1;
        _mainDoseBox.SetValueNoSignal(_mainDose);
        foreach (var doseBox in _assistantDoseBoxes) doseBox.SetValueNoSignal(1);
        RefreshPage();
    }

    private static string MaterialLine(MaterialChoice material) =>
        $"{material.Craft.element} · {PolarityName(material.Craft.polarity)} · {material.Craft.texture}\n{material.UtilityText}";

    private static string PolarityName(int value) => value < 0 ? "阴" : value > 0 ? "阳" : "和";

    private static TextureRect IconRect(int size) => new()
    {
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private static Texture2D? MaterialIcon(MaterialChoice material) =>
        string.IsNullOrWhiteSpace(material.Item.icon_path) || !ResourceLoader.Exists(material.Item.icon_path)
            ? null : GD.Load<Texture2D>(material.Item.icon_path);

    private static SpinBox DoseBox(int value)
    {
        var box = new SpinBox
        {
            MinValue = 1, MaxValue = int.MaxValue, AllowGreater = true, Step = 1,
            Editable = false, Rounded = true,
            Value = Math.Max(value, 1),
            CustomMinimumSize = new Vector2(64, 30)
        };
        var input = box.GetLineEdit();
        input.Alignment = HorizontalAlignment.Center;
        input.AddThemeFontSizeOverride("font_size", 17);
        input.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
        input.AddThemeColorOverride("font_uneditable_color", QuietRoomStyle.Ink);
        var paper = new StyleBoxFlat { BgColor = new Color("d9d5bf66"), ContentMarginLeft = 5, ContentMarginRight = 5 };
        input.AddThemeStyleboxOverride("normal", paper);
        input.AddThemeStyleboxOverride("read_only", paper);
        return box;
    }

    private static Control Heading(string title, string description)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        var heading = QuietRoomStyle.Text(title, 19);
        heading.AddThemeColorOverride("font_color", new Color("3c5e52"));
        box.AddChild(heading);
        var hint = QuietRoomStyle.Text(description, 13);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.AddThemeColorOverride("font_color", new Color("778074"));
        box.AddChild(hint);
        return box;
    }

    private static Control HSeparatorLine() => new HSeparator { CustomMinimumSize = new Vector2(0, 1) };

    private static PanelContainer MakePanel(Color fill, Color border, int padding)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = padding,
            ContentMarginRight = padding,
            ContentMarginTop = padding,
            ContentMarginBottom = padding
        };
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private void BuildHoverCard()
    {
        _hoverCard = MakeInkSlip(); _hoverCard.Name = "AlchemyHerbHoverCard";
        _hoverCard.ZIndex = 300; _hoverCard.MouseFilter = MouseFilterEnum.Ignore;
        _hoverText = QuietRoomStyle.Text("", 17);
        _hoverText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hoverCard.AddChild(_hoverText); _canvas.AddChild(_hoverCard); _hoverCard.Hide();
    }

    private void SetHover(Control control, string text)
    {
        control.TooltipText = "";
        control.SetMeta("alchemy_hover_text", text);
        if (control.HasMeta("alchemy_hover_bound")) return;
        control.SetMeta("alchemy_hover_bound", true);
        control.MouseEntered += () => { _hoverTarget = control; _hoverDelay = 0; };
        control.MouseExited += () => { if (_hoverTarget == control) HideHover(); };
    }

    private void HideHover()
    {
        _hoverTarget = null;
        _hoverCard?.Hide();
    }

    public override void _Process(double delta)
    {
        if (OwnerCharacter is not null && (TotalSpirit != OwnerCharacter.max_spirit_perception ||
            OtherSpiritOccupation != OwnerCharacter.used_spirit_perception || Attainment != OwnerCharacter.AlchemyAttainment)) RefreshPage();
        if (_hoverTarget is null) return;
        if (!GodotObject.IsInstanceValid(_hoverTarget) || !_hoverTarget.IsVisibleInTree()) { HideHover(); return; }
        _hoverDelay += delta;
        if (_hoverDelay < .35 || _hoverCard.Visible) return;
        var text = _hoverTarget.GetMeta("alchemy_hover_text").AsString();
        var desiredSize = new Vector2(330, Math.Min(420, (text.Split('\n').Length + 2) * 24 + 40));
        // Shape wrapped text at its final width before PanelContainer computes its minimum height.
        _hoverText.Size = desiredSize - new Vector2(44, 50);
        _hoverText.Text = text;
        _hoverCard.Size = desiredSize;
        var bounds = _hoverTarget.GetGlobalRect();
        var inverse = _canvas.GetGlobalTransform().AffineInverse();
        var origin = inverse * bounds.Position;
        var end = inverse * bounds.End;
        float x = end.X + 12;
        if (x + _hoverCard.Size.X > 1654) x = origin.X - _hoverCard.Size.X - 12;
        _hoverCard.Position = new Vector2(Math.Clamp(x, 12, Math.Max(12, 1660 - _hoverCard.Size.X)), Math.Clamp(origin.Y, 84, Math.Max(84, 929 - _hoverCard.Size.Y)));
        _hoverCard.Show();
    }

    private static Texture2D LoadUiTexture(string filename) => AlchemyPreviewScene.LoadRoomTexture("res://textures/ui/alchemy_20261019/" + filename);

    private static PanelContainer MakeInkSlip(bool vertical = false)
    {
        var texture = LoadUiTexture(vertical ? "batch_ledger_v3.png" : "role_portrait_v3.png");
        var style = new StyleBoxTexture
        {
            Texture = texture,
            TextureMarginLeft = 0, TextureMarginRight = 0,
            TextureMarginTop = 0, TextureMarginBottom = 0,
            ContentMarginLeft = vertical ? 20 : 22,
            ContentMarginRight = vertical ? 20 : 22,
            ContentMarginTop = 26, ContentMarginBottom = 24
        };
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", style); return panel;
    }

    private static bool FunctionActsInSlot(int slot, string utility) =>
        slot < 0
            ? AlchemyRecipeCatalog.Pills.Any(pill => pill.CoreUtility == utility)
            : AlchemySlotSimulation.CanFunctionAct(RoleFor(slot), utility);

    private string MaterialTooltip(MaterialChoice material, int slot)
    {
        var active = material.Craft.utilities.Where(u => u is not null && FunctionActsInSlot(slot, u.utility_id))
            .Select(u => u.utility_id).Distinct().ToArray();
        var inactive = material.Craft.utilities.Where(u => u is not null && !FunctionActsInSlot(slot, u.utility_id))
            .Select(u => u.utility_id).Distinct().ToArray();
        int dose = slot < 0 ? (_main?.Path == material.Path ? _mainDose : 1)
            : (_assistants[slot]?.Path == material.Path ? _assistantDoses[slot] : 1);
        var load = AlchemySlotSimulation.SpiritLoads([new(material.Name, material.Path, material.Craft, dose,
            slot < 0 ? AlchemySlotSimulation.SlotRole.Jun : RoleFor(slot))]).Single();
        return $"{material.Name} · {material.Craft.material_grade_name()}\n五行　{material.Craft.element}　阴阳　{PolarityName(material.Craft.polarity)}\n材性　{material.Craft.texture}\n功用　{material.UtilityText}\n\n本位发挥　{(active.Length == 0 ? "无对应功用" : string.Join("、", active))}"
            + (inactive.Length == 0 ? "" : $"\n本位不发挥　{string.Join("、", inactive)}")
            + $"\n\n本位{dose}份 · 灵识占用 {load.Occupation:0.00}\n材料负担 {load.BaseLoad:0.00} ＋ 功用负担 {load.FunctionLoad:0.00}\n药位倍率 ×{load.RoleMultiplier:0.0} · 用量倍率 ×{load.DoseMultiplier:0.00}\n各味合计后统一向上取整"
            + (OwnerCharacter is null ? "" : $"\n行囊存量 {OwnerCharacter.get_inventory_total_count(material.Path)}份");
    }

    private static PanelContainer MakePaperPanel(float alpha = .98f, int padding = 18)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(alpha, padding));
        return panel;
    }

    private static void ApplyPaperButtonStyle(Button button)
    {
        var normal = new StyleBoxTexture
        {
            Texture = LoadUiTexture("role_paper_v2.png"),
            TextureMarginLeft = 0,
            TextureMarginRight = 0,
            TextureMarginTop = 0,
            TextureMarginBottom = 0,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        var hover = normal.Duplicate() as StyleBoxTexture ?? normal;
        hover.ModulateColor = new Color(1.02f, 1.02f, .98f, 1);
        var pressed = normal.Duplicate() as StyleBoxTexture ?? normal;
        pressed.ModulateColor = new Color(.94f, .95f, .91f, 1);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeColorOverride("font_color", new Color("344c43"));
        button.AddThemeColorOverride("font_hover_color", new Color("2e6254"));
    }

    private static void ApplyInkBrushButtonStyle(Button button)
    {
        var normal = new StyleBoxTexture
        {
            Texture = GD.Load<Texture2D>("res://textures/ui/alchemy_20261019/brew_brush_v1.png"),
            ContentMarginLeft = 30,
            ContentMarginRight = 30,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        var hover = normal.Duplicate() as StyleBoxTexture ?? normal;
        hover.ModulateColor = new Color(1.08f, 1.08f, 1.04f, 1);
        var pressed = normal.Duplicate() as StyleBoxTexture ?? normal;
        pressed.ModulateColor = new Color(.90f, .93f, .90f, 1);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeColorOverride("font_color", new Color("f1eee0"));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", new Color("e1dfd2"));
        button.AddThemeFontSizeOverride("font_size", 24);
    }

    private Button MakeButton(string text, Action action, int size)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", new Color("344c43"));
        button.AddThemeColorOverride("font_hover_color", new Color("2e6254"));
        button.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(false, 8));
        button.AddThemeStyleboxOverride("hover", QuietRoomStyle.Edge(true, 8));
        button.AddThemeStyleboxOverride("pressed", QuietRoomStyle.Edge(true, 8));
        button.Pressed += () => { HideHover(); action(); };
        return button;
    }

    private static void Place(Control parent, Control control, Vector2 position, Vector2 size)
    {
        parent.AddChild(control);
        control.Position = position;
        control.Size = size;
    }
}
