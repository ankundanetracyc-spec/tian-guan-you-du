using Godot;
using System;

public partial class AlchemySlotBrewPreviewScene
{
    /// <summary>Only builds presentation; the existing picker owns availability and filtering.</summary>
    private void BuildPaperPicker()
    {
        _pickerOverlay = CreatePaperDialogOverlay("MaterialPickerOverlay", 100);
        var panel = CreatePaperDialogPanel(_pickerOverlay, "PaperMaterialPicker", new Vector2(1240, 790));
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 16);
        panel.AddChild(body);
        body.AddChild(CreatePaperDialogHeading("拣药", () => _pickerOverlay.Hide(), "CloseMaterialPicker"));

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 24);
        body.AddChild(content);

        var filterScroll = new ScrollContainer
        {
            Name = "PaperPickerFilterScroll",
            CustomMinimumSize = new Vector2(252, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        content.AddChild(filterScroll);
        var filters = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        filters.AddThemeConstantOverride("separation", 12);
        filterScroll.AddChild(filters);

        _pickerSearch = new LineEdit
        {
            Name = "MaterialSearch",
            PlaceholderText = "搜索药材名称、五行或功用",
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        CraftingPaperStyle.ApplyInput(_pickerSearch, fontSize: 20);
        _pickerSearch.TextChanged += _ => RefreshPickerGrid();
        filters.AddChild(_pickerSearch);

        filters.AddChild(CraftingPaperStyle.Text("五行", 19, muted: true));
        _elementFilter = new OptionButton
        {
            Name = "ElementFilter",
            CustomMinimumSize = new Vector2(0, 42),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        // Persist the native filter indices: 2 is 木.
        foreach (var text in new[] { "全部五行", "金", "木", "水", "火", "土" }) _elementFilter.AddItem(text);
        CraftingPaperStyle.ApplyButton(_elementFilter, CraftingPaperStyle.ButtonKind.Input, fontSize: 20);
        _elementFilter.ItemSelected += _ => RefreshPickerGrid();
        filters.AddChild(_elementFilter);

        filters.AddChild(CraftingPaperStyle.Text("阴阳", 19, muted: true));
        _polarityFilter = new OptionButton
        {
            Name = "PolarityFilter",
            CustomMinimumSize = new Vector2(0, 42),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        // Filtering compares these native labels; 3 remains 阳, not 偏阳.
        foreach (var text in new[] { "全部阴阳", "阴", "和", "阳" }) _polarityFilter.AddItem(text);
        CraftingPaperStyle.ApplyButton(_polarityFilter, CraftingPaperStyle.ButtonKind.Input, fontSize: 20);
        _polarityFilter.ItemSelected += _ => RefreshPickerGrid();
        filters.AddChild(_polarityFilter);

        filters.AddChild(CraftingPaperStyle.Text("功用", 19, muted: true));
        var utilities = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        utilities.AddThemeConstantOverride("h_separation", 7);
        utilities.AddThemeConstantOverride("v_separation", 7);
        filters.AddChild(utilities);
        _utilityFilterButtons.Clear();
        foreach (var utility in FilterUtilities)
        {
            var selectedUtility = utility;
            var button = PaperDialogButton(utility, () => { }, CraftingPaperStyle.ButtonKind.Regular, 20);
            button.Name = $"UtilityFilter_{utility}";
            button.ToggleMode = true;
            button.CustomMinimumSize = new Vector2(74, 38);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.SetPressedNoSignal(_utilityFilters.Contains(utility));
            CraftingPaperStyle.ApplyButton(button, CraftingPaperStyle.ButtonKind.Regular, fontSize: 20,
                selected: button.ButtonPressed);
            button.Toggled += pressed =>
            {
                if (pressed) _utilityFilters.Add(selectedUtility);
                else _utilityFilters.Remove(selectedUtility);
                CraftingPaperStyle.ApplyButton(button, CraftingPaperStyle.ButtonKind.Regular, fontSize: 20, selected: pressed);
                RefreshPickerGrid();
            };
            _utilityFilterButtons.Add(button);
            utilities.AddChild(button);
        }

        _effectiveOnly = new CheckBox
        {
            Name = "EffectiveOnlyFilter",
            Text = "仅显示本位功用有效",
            CustomMinimumSize = new Vector2(0, 42),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        CraftingPaperStyle.ApplyButton(_effectiveOnly, CraftingPaperStyle.ButtonKind.Regular, fontSize: 18);
        _effectiveOnly.Toggled += pressed =>
        {
            CraftingPaperStyle.ApplyButton(_effectiveOnly, CraftingPaperStyle.ButtonKind.Regular, fontSize: 18, selected: pressed);
            RefreshPickerGrid();
        };
        filters.AddChild(_effectiveOnly);
        var reset = PaperDialogButton("重置筛选", () =>
        {
            _elementFilter.Select(0);
            _polarityFilter.Select(0);
            _effectiveOnly.ButtonPressed = false;
            _utilityFilters.Clear();
            foreach (var button in _utilityFilterButtons)
            {
                button.SetPressedNoSignal(false);
                CraftingPaperStyle.ApplyButton(button, CraftingPaperStyle.ButtonKind.Regular, fontSize: 20);
            }
            _pickerSearch.Clear();
            RefreshPickerGrid();
        }, fontSize: 20);
        reset.Name = "ResetPickerFilters";
        reset.CustomMinimumSize = new Vector2(0, 42);
        filters.AddChild(reset);

        content.AddChild(PaperDialogRule(vertical: true));
        var results = new VBoxContainer
        {
            Name = "PaperPickerResults",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        results.AddThemeConstantOverride("separation", 10);
        content.AddChild(results);
        _pickerHint = CraftingPaperStyle.Text("", 17, muted: true);
        _pickerHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _pickerHint.MaxLinesVisible = 3;
        results.AddChild(_pickerHint);
        _pickerCount = CraftingPaperStyle.Text("", 18, muted: true);
        _pickerCount.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        results.AddChild(_pickerCount);
        var resultScroll = new ScrollContainer
        {
            Name = "PaperMaterialChoiceScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        results.AddChild(resultScroll);
        _pickerGrid = new GridContainer
        {
            Name = "MaterialChoiceGrid",
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _pickerGrid.AddThemeConstantOverride("h_separation", 14);
        _pickerGrid.AddThemeConstantOverride("v_separation", 14);
        resultScroll.AddChild(_pickerGrid);

        body.AddChild(PaperDialogRule());
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        footer.AddChild(PaperDialogButton("收起", () => _pickerOverlay.Hide(), fontSize: 22));
        body.AddChild(footer);
    }

    /// <summary>Uses all native recipe routes and saved recipes via the existing notebook controller.</summary>
    private void BuildPaperNotebook()
    {
        _recipeOverlay = CreatePaperDialogOverlay("KnownFormulaOverlay", 120);
        var panel = CreatePaperDialogPanel(_recipeOverlay, "PaperRecipeNotebook", new Vector2(1240, 790));
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 16);
        panel.AddChild(body);
        body.AddChild(CreatePaperDialogHeading("丹方册", () => _recipeOverlay.Hide(), "CloseRecipeNotebook"));

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 24);
        body.AddChild(content);
        var sidebar = new VBoxContainer
        {
            Name = "PaperRecipeSidebar",
            CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        sidebar.AddThemeConstantOverride("separation", 12);
        content.AddChild(sidebar);
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        sidebar.AddChild(tabs);
        _builtinTab = PaperDialogButton("内置样方", () => { _showSaved = false; RefreshNotebookList(); },
            CraftingPaperStyle.ButtonKind.Regular, 20);
        _savedTab = PaperDialogButton("我的丹方", () => { _showSaved = true; RefreshNotebookList(); },
            CraftingPaperStyle.ButtonKind.Regular, 20);
        _builtinTab.SizeFlagsHorizontal = _savedTab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tabs.AddChild(_builtinTab);
        tabs.AddChild(_savedTab);
        var listScroll = new ScrollContainer
        {
            Name = "PaperRecipeListScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        sidebar.AddChild(listScroll);
        _notebookList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _notebookList.AddThemeConstantOverride("separation", 9);
        listScroll.AddChild(_notebookList);

        _recipeName = new LineEdit
        {
            Name = "RecipeName",
            PlaceholderText = "为当前丹方起名",
            MaxLength = 40,
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        CraftingPaperStyle.ApplyInput(_recipeName, fontSize: 21);
        sidebar.AddChild(_recipeName);
        var save = PaperDialogButton("保存新方", () => SaveCurrentRecipe(false), fontSize: 20);
        save.CustomMinimumSize = new Vector2(0, 42);
        sidebar.AddChild(save);
        var overwrite = PaperDialogButton("覆盖同名", () => SaveCurrentRecipe(true), fontSize: 20);
        overwrite.CustomMinimumSize = new Vector2(0, 42);
        sidebar.AddChild(overwrite);

        content.AddChild(PaperDialogRule(vertical: true));
        var detail = new VBoxContainer
        {
            Name = "PaperRecipeDetails",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detail.AddThemeConstantOverride("separation", 14);
        content.AddChild(detail);
        var selectedTitle = CraftingPaperStyle.Text("选一张丹方，展开配伍", 30);
        selectedTitle.Name = "PaperRecipeSelectionTitle";
        selectedTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selectedTitle.MaxLinesVisible = 2;
        detail.AddChild(selectedTitle);
        var detailScroll = new ScrollContainer
        {
            Name = "PaperRecipeDetailScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        detail.AddChild(detailScroll);
        _notebookDetail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _notebookDetail.AddThemeConstantOverride("separation", 12);
        detailScroll.AddChild(_notebookDetail);
        // The existing renderer still owns every detail node. Mirror only its first
        // heading outside the scroll view so the selected recipe title stays fixed.
        _notebookDetail.ChildEnteredTree += child =>
        {
            if (_notebookDetail.GetChildCount() == 1 && child is Label heading)
            {
                selectedTitle.Text = heading.Text;
                heading.Hide();
            }
        };

        body.AddChild(PaperDialogRule());
        _notebookStatus = CraftingPaperStyle.Text("", 17, muted: true);
        _notebookStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notebookStatus.MaxLinesVisible = 3;
        body.AddChild(_notebookStatus);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 12);
        _recipeDelete = PaperDialogButton("删除", DeleteNotebookSelection, fontSize: 21);
        actions.AddChild(_recipeDelete);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        actions.AddChild(PaperDialogButton("收起", () => _recipeOverlay.Hide(), fontSize: 22));
        _recipeLoad = PaperDialogButton("载入此方", LoadNotebookSelection, CraftingPaperStyle.ButtonKind.Primary, 23);
        _recipeLoad.CustomMinimumSize = new Vector2(180, 48);
        actions.AddChild(_recipeLoad);
        body.AddChild(actions);
        RefreshNotebookList();
    }

    private Button BuildPaperMaterialChoice(MaterialChoice choice)
    {
        var selected = _pickerTarget < 0
            ? _main?.Path == choice.Path
            : _pickerTarget < _assistants.Length && _assistants[_pickerTarget]?.Path == choice.Path;
        var tile = PaperDialogButton("", () => ChooseMaterial(choice), fontSize: 22);
        tile.Name = $"Material_{choice.Name}";
        tile.CustomMinimumSize = new Vector2(0, 196);
        tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tile.ClipContents = true;
        CraftingPaperStyle.ApplyButton(tile, CraftingPaperStyle.ButtonKind.Regular, fontSize: 22, selected: selected);

        var content = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddThemeConstantOverride("separation", 5);
        tile.AddChild(content);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.OffsetLeft = 12;
        content.OffsetRight = -12;
        content.OffsetTop = 12;
        content.OffsetBottom = -12;
        var icon = IconRect(64);
        icon.Texture = MaterialIcon(choice);
        icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        content.AddChild(icon);
        var name = CraftingPaperStyle.Text(choice.Name, 23);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode = TextServer.AutowrapMode.Off;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.MouseFilter = MouseFilterEnum.Ignore;
        content.AddChild(name);
        var facts = CraftingPaperStyle.Text($"{choice.Craft.element} · {choice.Craft.texture} · {PolarityName(choice.Craft.polarity)}", 17, muted: true);
        facts.HorizontalAlignment = HorizontalAlignment.Center;
        facts.MouseFilter = MouseFilterEnum.Ignore;
        content.AddChild(facts);
        var utilities = CraftingPaperStyle.Text(choice.UtilityText, 16, muted: true);
        utilities.HorizontalAlignment = HorizontalAlignment.Center;
        utilities.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        utilities.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        utilities.MaxLinesVisible = 2;
        utilities.MouseFilter = MouseFilterEnum.Ignore;
        content.AddChild(utilities);
        SetHover(tile, MaterialTooltip(choice, _pickerTarget));
        return tile;
    }

    private Control CreatePaperDialogOverlay(string name, int zIndex)
    {
        var overlay = new Control
        {
            Name = name,
            Visible = false,
            ZIndex = zIndex,
            MouseFilter = MouseFilterEnum.Stop,
            Theme = CraftingPaperStyle.MakeTheme()
        };
        _canvas.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var veil = new ColorRect { Color = new Color(.125f, .208f, .18f, .66f), MouseFilter = MouseFilterEnum.Stop };
        overlay.AddChild(veil);
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return overlay;
    }

    private PanelContainer CreatePaperDialogPanel(Control overlay, string name, Vector2 size)
    {
        var panel = new PanelContainer { Name = name };
        panel.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(padding: 26));
        PlaceCentered(overlay, panel, size);
        return panel;
    }

    private HBoxContainer CreatePaperDialogHeading(string title, Action close, string closeName)
    {
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        var label = CraftingPaperStyle.Text(title, 33);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(label);
        var button = PaperDialogButton("×", close, CraftingPaperStyle.ButtonKind.Icon, 25);
        button.Name = closeName;
        button.TooltipText = "收起";
        button.CustomMinimumSize = new Vector2(42, 42);
        header.AddChild(button);
        return header;
    }

    private Button PaperDialogButton(string text, Action action,
        CraftingPaperStyle.ButtonKind kind = CraftingPaperStyle.ButtonKind.Regular, int fontSize = 22)
    {
        var button = MakeButton(text, action, fontSize);
        CraftingPaperStyle.ApplyButton(button, kind, fontSize: fontSize);
        button.CustomMinimumSize = new Vector2(0, 44);
        return button;
    }

    private static Control PaperDialogRule(bool vertical = false) => new ColorRect
    {
        Color = new Color(.65f, .62f, .52f, .30f),
        CustomMinimumSize = vertical ? new Vector2(1, 0) : new Vector2(0, 1),
        SizeFlagsVertical = vertical ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
        MouseFilter = MouseFilterEnum.Ignore
    };
}
