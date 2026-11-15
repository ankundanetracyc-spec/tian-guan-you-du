using Godot;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Isolated owner-bound alchemy UI checks; never starts a brew or a player journey.</summary>
public partial class AlchemyPaperUiSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed record CheckResult(string Name, bool Passed, string Detail);
    private sealed record MaterialEntry(string Path, ItemData Item, MaterialCraftData Craft)
    {
        public string Name => Item.entity_name;
    }

    private readonly List<CheckResult> _checks = [];
    private readonly List<string> _captures = [];
    private readonly List<MaterialEntry> _catalog = [];
    private Character _owner = null!;
    private AlchemySlotBrewPreviewScene _room = null!;
    private string _output = "", _notebookFixture = "";
    private int _width = 1280, _height = 720, _loadedMaterials;
    private bool _headless;

    private static T Field<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, Hidden)?.GetValue(target)
            ?? throw new InvalidOperationException("Missing alchemy field: " + name));
    private static void SetField(object target, string name, object? value) =>
        (target.GetType().GetField(name, Hidden)
            ?? throw new InvalidOperationException("Missing alchemy field: " + name)).SetValue(target, value);
    private static object? Invoke(object target, string name, params object?[] values) =>
        (target.GetType().GetMethod(name, Hidden)
            ?? throw new InvalidOperationException("Missing alchemy method: " + name)).Invoke(target, values);
    private static T Find<T>(Node parent, string name) where T : Node =>
        parent.FindChild(name, true, false) as T
            ?? throw new InvalidOperationException("Missing UI node: " + name);
    private static string ReadStringProperty(object target, string name) =>
        target.GetType().GetProperty(name)?.GetValue(target)?.ToString()
            ?? throw new InvalidOperationException("Missing material property: " + name);
    private static Rect2 Bounds(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas();
        var rect = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        foreach (var point in new[] { new Vector2(control.Size.X, 0), control.Size, new Vector2(0, control.Size.Y) })
            rect = rect.Expand(transform * point);
        return rect;
    }
    private IReadOnlyList<AlchemySlotSimulation.Input> Inputs() =>
        (IReadOnlyList<AlchemySlotSimulation.Input>)Invoke(_room!, "GetSimulationInputs")!;
    private string InventoryStamp() => string.Join("|", _owner!.get_spatial_inventory()
        .Select(item => $"{item.InstanceId}:{item.ItemPath}:{item.RegionId}:{item.X},{item.Y}:{item.Rotation}"));

    private void Check(bool passed, string name, object? detail = null)
    {
        var text = detail?.ToString() ?? "";
        _checks.Add(new(name, passed, text));
        if (!passed) throw new InvalidOperationException(name + (text.Length == 0 ? "" : ": " + text));
        GD.Print("ALCHEMY_PAPER_UI_OK " + name);
    }

    private async Task Frames(int count = 2)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Click(Button button)
    {
        Check(button.IsVisibleInTree() && !button.Disabled && button.MouseFilter != Control.MouseFilterEnum.Ignore
            && button.Size.X > 1 && button.Size.Y > 1, "控件可操作 " + button.Name);
        if (_headless)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        else
        {
            var point = button.GetGlobalTransformWithCanvas() * (button.Size * .5f);
            GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
            GetViewport().PushInput(new InputEventMouseButton
                { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton
                { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        }
        await Frames();
    }

    private async Task Capture(string state)
    {
        if (_headless) return;
        GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(5, 5), GlobalPosition = new Vector2(5, 5) }, true);
        Invoke(_room, "HideHover");
        await Frames();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _height, "截图使用目标原生窗口尺寸 " + state,
            $"{image.GetWidth()}x{image.GetHeight()}");
        string name = $"alchemy_paper_{state}_{_width}.png";
        Check(image.SavePng(System.IO.Path.Combine(_output, name)) == Error.Ok, "保存原生截图 " + state);
        _captures.Add(name);
    }

    private void Configure()
    {
        var arguments = OS.GetCmdlineUserArgs();
        string? width = arguments.FirstOrDefault(argument => argument.StartsWith("--paper-width="))?.Split('=', 2)[1];
        if (width is not null && (!int.TryParse(width, out _width) || _width is not (1280 or 1920)))
            throw new ArgumentException("--paper-width must be 1280 or 1920.");
        _height = _width == 1920 ? 1080 : 720;
        _headless = DisplayServer.GetName() == "headless";
        string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string allowedOutput = System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "output"));
        string supplied = arguments.FirstOrDefault(argument => argument.StartsWith("--paper-output="))?.Split('=', 2)[1]
            ?? "res://output/alchemy_paper_ui_20261108";
        if (supplied.StartsWith("res://") || supplied.StartsWith("user://")) supplied = ProjectSettings.GlobalizePath(supplied);
        else if (!System.IO.Path.IsPathRooted(supplied)) supplied = System.IO.Path.Combine(project, supplied);
        _output = System.IO.Path.GetFullPath(supplied);
        if (!IsWithin(_output, allowedOutput))
            throw new ArgumentException("Paper UI results must remain inside this project's output directory.");

        // The alchemy scene reads its presentation preference during _Ready().
        // Reject a normal profile before creating that scene. Launch with isolated APPDATA.
        string userData = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        Check(IsWithin(userData, allowedOutput)
            || IsWithin(userData, System.IO.Path.Combine(project, "tmp"))
            || IsWithin(userData, System.IO.Path.Combine(project, ".godot", "test-results")),
            "使用项目内隔离用户目录", userData);
        System.IO.Directory.CreateDirectory(_output);
        _notebookFixture = System.IO.Path.Combine(_output, $"notebook_{_width}_{Guid.NewGuid():N}.json");
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Borderless = true;
        GetWindow().ContentScaleSize = Vector2I.Zero;
        GetWindow().Size = new Vector2I(_width, _height);
        GetWindow().Unfocusable = true;
    }

    private static bool IsWithin(string path, string directory)
    {
        string target = System.IO.Path.GetFullPath(path);
        string root = System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);
        return target.Equals(root, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadCatalog()
    {
        using var directory = DirAccess.Open("res://resources/entities/items")
            ?? throw new InvalidOperationException("The real item directory is unavailable.");
        foreach (string file in directory.GetFiles().Where(name => name.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            // Preserve the existing material shelf's bone exclusion.
            if (file.Contains("骨", StringComparison.Ordinal)) continue;
            string path = "res://resources/entities/items/" + file;
            if (GD.Load<ItemData>(path) is { item_category: "材料", craft_data: MaterialCraftData craft } item)
                _catalog.Add(new(path, item, craft));
        }
    }

    private void CreateOwner(AlchemyRecipeCatalog.SampleRoute route)
    {
        _owner = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData
        {
            entity_name = "炼丹纸墨界面验证", cultivation_realm = "云息三层",
            base_alchemy_proficiency = 36, alchemy_proficiency = 36,
            main_gongfa_path = GongfaAvailability.DefaultBookPath
        } };
        AddChild(_owner); _owner.Hide();
        _owner.current_world_stamina = _owner.max_world_stamina = 100;
        _owner.max_spirit_perception = 24; _owner.used_spirit_perception = 0;
        var stock = route.Ingredients.ToDictionary(ingredient => ingredient.HerbName, ingredient => ingredient.Dose + 1);
        foreach (var material in _catalog.OrderBy(item => item.Item.get_inventory_cell_count()).ThenBy(item => item.Name, StringComparer.Ordinal))
        {
            if (stock.Count >= 10) break;
            stock.TryAdd(material.Name, 2);
        }
        foreach (var (name, count) in stock)
        {
            var material = _catalog.Single(item => item.Name == name);
            Check(_owner.add_item_to_inventory(material.Path, count), "真实材料装入隔离人物 " + name);
        }
    }

    private void CheckMaterialsAndStyle()
    {
        var loaded = Field<IEnumerable>(_room!, "_materials").Cast<object>().ToArray();
        var loadedPaths = loaded.Select(material => ReadStringProperty(material, "Path")).ToHashSet(StringComparer.Ordinal);
        _loadedMaterials = loaded.Length;
        Check(loaded.Length > 11 && loadedPaths.SetEquals(_catalog.Select(material => material.Path)),
            "原材料全目录保持，未替换为网页11味夹具", $"actual={loaded.Length}; expected={_catalog.Count}");
        Check(_room!.OwnerCharacter == _owner && _room.Attainment == _owner!.AlchemyAttainment
            && _room.TotalSpirit == 24 && _room.OtherSpiritOccupation == 0,
            "正式Owner绑定真实造诣与灵识");
        var attainmentInput = Field<SpinBox>(_room, "_attainmentInput");
        Check((!attainmentInput.IsVisibleInTree() || !attainmentInput.Editable)
            && Field<Label>(_room, "_energyHeaderValue").Text == "100", "人物造诣只读，精力显示真实100");
        foreach (string name in new[] { "JunHerbCard", "ChenHerbCard", "ZuoHerbGroup", "ShiHerbGroup", "BatchRequirements" })
        {
            var panel = Find<PanelContainer>(_room, name);
            Check(panel.GetThemeStylebox("panel") is StyleBoxTexture { Texture: not null }, "药签用度使用真实素材 " + name);
        }
        foreach (string name in new[] { "FormulaBookButton", "BrewButton", "ReturnButton" })
            Check(Find<Button>(_room, name).GetThemeStylebox("normal") is StyleBoxTexture { Texture: not null },
                "不同控件使用原生按钮素材 " + name);
        var font = Field<Label>(_room, "_mainName").GetThemeFont("font");
        Check(font.ResourcePath.Contains("LXGWZhenKaiSlabGB-Regular", StringComparison.Ordinal), "沿用项目楷体字体", font.ResourcePath);
        var back = Find<Button>(_room, "ReturnButton");
        var rect = Bounds(back);
        var visible = GetViewport().GetVisibleRect();
        Check(back.IsVisibleInTree() && rect.GetCenter().X > visible.Size.X * .8f
            && rect.Position.Y < visible.Size.Y * .18f && visible.Grow(1).Encloses(rect), "统一返回位于右上角", rect);
        var header = Find<Control>(_room, "AlchemyPaperHeader");
        var canvas = Field<Control>(_room, "_canvas");
        float scale = canvas.GetGlobalTransformWithCanvas().Scale.X;
        Check(header.GetParent() == back.GetParent() && !header.IsAncestorOf(back)
            && Math.Abs((rect.Position.X - Bounds(header).End.X) / scale - 16) < .1f,
            "工艺横条在独立返回前收口并保留16设计单位间隔", $"bar={Bounds(header)}; back={rect}");
        var headerText = Descendants(header).OfType<Control>().Where(control => control.IsVisibleInTree()
            && (control is Label || control is Button)).Append(back).ToArray();
        Check(headerText.All(control => control.GetThemeFontSize("font_size") == 22
            && Math.Abs(control.Size.Y - 46) < .1f
            && Math.Abs(Bounds(control).GetCenter().Y - rect.GetCenter().Y) < .1f),
            "炼丹横条资源按钮同为22字46高且同行居中",
            string.Join("; ", headerText.Select(control => $"{control.Name}: font={control.GetThemeFontSize("font_size")}; size={control.Size}; rect={Bounds(control)}")));
        Check(Descendants(header).OfType<Control>().Where(control => control.IsVisibleInTree())
            .All(control => Bounds(header).Grow(1).Encloses(Bounds(control))), "横条内功能和资源均未延伸到返回区域");
        foreach (var (name, x) in new[] { ("AlchemyHelpButton", 14f), ("CraftingHeaderMetrics", 926f) })
            Check(Math.Abs(Find<Control>(header, name).Position.X - x) < .1f, "同类工艺信息使用固定列 " + name);
        float spiritGap = (Bounds(Find<Label>(header, "CraftingHeaderEnergyCaption")).Position.X
            - Bounds(Find<Label>(header, "CraftingHeaderSpiritValue")).End.X) / scale;
        float energyGap = (Bounds(Find<Label>(header, "CraftingHeaderAttainmentCaption")).Position.X
            - Bounds(Find<Label>(header, "CraftingHeaderEnergyValue")).End.X) / scale;
        Check(Math.Abs(spiritGap - 32) < .1f && Math.Abs(energyGap - spiritGap) < .1f,
            "灵识精力造诣三组文字之间均为32单位等距", $"{spiritGap};{energyGap}");
        Check(header.GetThemeStylebox("panel") is StyleBoxTexture headerPaper
            && Find<PanelContainer>(_room, "JunHerbCard").GetThemeStylebox("panel") is StyleBoxTexture bodyPaper
            && headerPaper.ModulateColor == bodyPaper.ModulateColor && headerPaper.Texture == bodyPaper.Texture,
            "炼丹横条与下方阅读纸面使用相同素材和人物面板暖纸色");
        Check(!Descendants(header).OfType<Label>().Any(label => label.Text is "炼 丹" or "天工峰 · 丹房"), "顶栏不再重复页面名和地点字样");
        Check(Math.Abs(Bounds(Find<Label>(header, "CraftingHeaderSpiritValue")).End.X
            - Bounds(Find<Control>(header, "CraftingHeaderSpirit")).End.X) < .1f, "灵识整组在固定资源栏内向右对齐");
        var arrow = back.GetChildren().OfType<Control>().Single(control => control.GetType().Name == "PaperTriangle");
        Check(arrow.IsVisibleInTree() && rect.Encloses(Bounds(arrow))
            && Bounds(arrow).GetCenter().X > rect.GetCenter().X,
            "返回三角位于按钮内部文字右侧", Bounds(arrow));
        Check(ReferenceEquals(Field<AlchemyFurnaceView>(_room, "_furnace").GetParent(), Field<Control>(_room, "_canvas")),
            "丹炉保持演出所需的直接画布父节点");
    }

    private async Task CheckSample(AlchemyRecipeCatalog.SampleRoute route)
    {
        Invoke(_room!, "ApplySampleRoute", route, false);
        await Frames();
        var inputs = Inputs();
        var model = AlchemySlotSimulation.Simulate(inputs, _owner!.AlchemyAttainment, 24);
        Check(inputs.Count == 4 && inputs.Sum(input => input.Dose) == 7
            && AlchemySlotSimulation.SpiritOccupation(inputs) == 19 && model.EnergyNeed == 31
            && Math.Abs(2 * AlchemySlotSimulation.DurationHours(inputs) - 8) < 1e-9,
            "青木清露方四味真实资源为19灵识31精力8小时");
        var spirit = Field<Label>(_room, "_paperSpirit");
        var energy = Field<Label>(_room, "_paperEnergy");
        var hours = Field<Label>(_room, "_paperHours");
        string summary = $"{spirit.Text}/{energy.Text}/{hours.Text}";
        Check(spirit.IsVisibleInTree() && energy.IsVisibleInTree() && hours.IsVisibleInTree()
            && spirit.Text == "19" && energy.Text == "31" && Regex.IsMatch(hours.Text, @"^8(?:\.0+)?\s*小时$"),
            "用度显示真实资源并换算游戏小时", summary);
        var batch = Find<Control>(_room, "BatchRequirements");
        var visibleTexts = Descendants(batch).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text);
        Check(!Regex.IsMatch(string.Join("\n", visibleTexts), "成功率|品质|预计成丹|丹效|时辰"), "开炼前只展示本炉用度");
        Check(!Field<Button>(_room!, "_brewButton").Disabled, "真实材料精力灵识足够时允许开炼");
        Check(_room.FindChild("AlchemyPresentationModes", true, false) is null
            && !Descendants(_room).OfType<Label>().Any(label => label.Text == "样方 · 记方"),
            "移除炼成展示控件与丹方册副标题");
        var bookIcon = Find<TextureRect>(_room, "FormulaBookIcon");
        using (var bookImage = bookIcon.Texture.GetImage())
        {
            if (bookImage.IsCompressed()) bookImage.Decompress();
            Check(bookImage.GetWidth() > 512 && bookImage.GetPixel(0, 0).A == 0
                && bookImage.GetPixel(bookImage.GetWidth() / 2, bookImage.GetHeight() / 2).A > .9f,
                "丹方册使用新生成的透明册本图标");
        }
        var track = (StyleBoxTexture)_room.Theme.GetStylebox("scroll", "VScrollBar");
        var thumb = (StyleBoxTexture)_room.Theme.GetStylebox("grabber", "VScrollBar");
        Check(track.TextureMarginTop == 6 && track.TextureMarginBottom == 6
            && thumb.TextureMarginTop == 6 && thumb.TextureMarginBottom == 6
            && track.Texture.GetHeight() == 13 && thumb.Texture.GetHeight() == 13,
            "滚动条保留固定端帽仅拉伸直段");
        Check(((StyleBoxTexture)_room.Theme.GetStylebox("hover", "PopupMenu")).Texture.ResourceName.Contains("button_regular"),
            "下拉项目不再借用折角页签素材");
        await Capture("main");
        int dose = inputs.Single(input => input.IsMain).Dose;
        await Click(Find<Button>(_room!, "DosePlus_0"));
        Check(Inputs().Single(input => input.IsMain).Dose == dose + 1
            && Field<LineEdit[]>(_room, "_paperDoseInputs")[0].Text == (dose + 1).ToString(),
            "君药加量按钮同步配伍与可见份数");
        await Click(Find<Button>(_room!, "DoseMinus_0"));
        Check(Inputs().Single(input => input.IsMain).Dose == dose
            && Field<LineEdit[]>(_room, "_paperDoseInputs")[0].Text == dose.ToString(),
            "君药减量恢复原份数与显示");
        var assistant = route.Ingredients.First(ingredient => ingredient.AssistantSlot >= 0);
        await Click(Field<Button[]>(_room!, "_assistantClearButtons")[assistant.AssistantSlot]);
        Check(Inputs().Count == 3 && Inputs().All(input => input.Name != assistant.HerbName), "移除按钮只修改草稿药位");
        Invoke(_room!, "ApplySampleRoute", route, false);
        await Frames();
    }

    private async Task CheckPicker(AlchemyRecipeCatalog.SampleRoute route)
    {
        await Click(Find<Button>(_room!, "PickHerb_0"));
        var overlay = Field<Control>(_room!, "_pickerOverlay");
        Check(overlay.Visible && Field<int>(_room!, "_pickerTarget") == -1, "君药按钮打开真实拣药界面");
        CheckPlaceholder(Field<LineEdit>(_room, "_pickerSearch"), "拣药检索提示字可读");
        var utilityButtons = Field<List<Button>>(_room, "_utilityFilterButtons");
        Check(utilityButtons.All(button => ((StyleBoxTexture)button.GetThemeStylebox("normal")).Texture.ResourceName.Contains("button_regular")),
            "功用筛选同用途统一普通齐边纸面");
        await Click(utilityButtons[0]);
        Check(utilityButtons[0].ButtonPressed
            && ((StyleBoxTexture)utilityButtons[0].GetThemeStylebox("normal")).Texture.ResourceName.Contains("button_regular"),
            "功用选中仅换整纸状态不升级主操作或换页签");
        await Capture("picker_utility");
        await Click(Find<Button>(overlay, "ResetPickerFilters"));
        var grid = Field<GridContainer>(_room!, "_pickerGrid");
        var unfiltered = grid.GetChildren().OfType<Button>().Select(button => button.Name.ToString()["Material_".Length..]).ToArray();
        Check(unfiltered.Length > 1 && unfiltered.All(name => _owner!.has_inventory_item(_catalog.Single(item => item.Name == name).Path)),
            "正式拣药仅使用真实持有材料");
        string placedElsewhere = route.Ingredients.First(ingredient => ingredient.AssistantSlot >= 0).HerbName;
        Check(!unfiltered.Contains(placedElsewhere), "同材其他药位禁选保持");
        await Capture("picker");
        string[] selected = Inputs().Select(input => input.Name).ToArray();
        string alternative = unfiltered.First(name => !selected.Contains(name));
        var search = Field<LineEdit>(_room!, "_pickerSearch");
        search.Text = alternative;
        search.EmitSignal(LineEdit.SignalName.TextChanged, alternative);
        await Frames();
        Check(grid.GetChildCount() == 1, "材料名称检索缩小实际目录", alternative);
        await Click(Find<Button>(grid, "Material_" + alternative));
        Check(!overlay.Visible && Inputs().Single(input => input.IsMain).Name == alternative, "拣药点击更换君药草稿");
        Invoke(_room!, "ApplySampleRoute", route, false);
        await Frames();
        await Click(Find<Button>(_room!, "PickHerb_0"));
        await Click(Find<Button>(overlay, "ResetPickerFilters"));
        var wood = _catalog.Single(item => item.Name == route.Ingredients[0].HerbName);
        var element = Field<OptionButton>(_room!, "_elementFilter");
        int index = Enumerable.Range(0, element.ItemCount).First(i => element.GetItemText(i) == wood.Craft.element);
        element.Select(index); element.EmitSignal(OptionButton.SignalName.ItemSelected, index);
        await Frames();
        string[] filtered = grid.GetChildren().OfType<Button>().Select(button => button.Name.ToString()["Material_".Length..]).ToArray();
        string[] expected = unfiltered.Where(name => _catalog.Single(item => item.Name == name).Craft.element == wood.Craft.element).ToArray();
        Check(filtered.Length > 0 && filtered.ToHashSet().SetEquals(expected), "五行筛选作用于实际持有目录");
        await Click(Find<Button>(overlay, "ResetPickerFilters"));
        Check(grid.GetChildCount() == unfiltered.Length && Field<LineEdit>(_room!, "_pickerSearch").Text.Length == 0
            && Field<OptionButton>(_room!, "_elementFilter").Selected == 0, "实际重置按钮恢复材料筛选");
        overlay.Hide();
    }

    private async Task CheckNotebook(AlchemyRecipeCatalog.SampleRoute route)
    {
        var saved = new AlchemyRecipeStore.Recipe("paper-ui", "界面验证留存方", route.Ingredients.Select(ingredient =>
        {
            var material = _catalog.Single(item => item.Name == ingredient.HerbName);
            return new AlchemyRecipeStore.Entry(material.Item.definition_id.ToString(), ingredient.Dose, ingredient.AssistantSlot);
        }).ToArray());
        AlchemyRecipeStore.Save(_notebookFixture, [saved]);
        string original = System.IO.File.ReadAllText(_notebookFixture);
        SetField(_room!, "_notebookPathOverride", _notebookFixture);
        await Click(Find<Button>(_room!, "FormulaBookButton"));
        var overlay = Field<Control>(_room!, "_recipeOverlay");
        Check(overlay.Visible, "丹方册按钮打开原生资料");
        CheckPlaceholder(Field<LineEdit>(_room, "_recipeName"), "丹方题名提示字可读");
        await Click(Field<Button>(_room, "_builtinTab"));
        var list = Field<VBoxContainer>(_room!, "_notebookList");
        var formulas = list.GetChildren().OfType<Button>().ToArray();
        Check(formulas.Length == AlchemyRecipeCatalog.Pills.Sum(pill => pill.SampleRoutes.Length) && formulas.Length > 3,
            "丹方册保留完整原目录，未截断为网页三方", formulas.Length);
        await Click(Find<Button>(list, "KnownFormula_huichun_" + route.Name));
        await Capture("notebook");
        await Click(Field<Button>(_room, "_savedTab"));
        var stored = Field<IEnumerable>(_room!, "_savedRecipes").Cast<AlchemyRecipeStore.Recipe>().ToArray();
        Check(stored.Length == 1 && stored[0].Name == saved.Name, "我的丹方仍读回原保存格式");
        await Click(list.GetChildren().OfType<Button>().Single());
        await Click(Field<Button>(_room!, "_recipeLoad"));
        Check(!overlay.Visible && Inputs().Count == 4
            && System.IO.File.ReadAllText(_notebookFixture) == original, "载入保存丹方不改写记录或丢失投料");
    }

    private async Task CheckEightSlots(AlchemyRecipeCatalog.SampleRoute sample)
    {
        var data = (HumanData)_owner!.entity_data!;
        data.base_alchemy_proficiency = data.alchemy_proficiency = 508;
        _owner.max_spirit_perception = 10000;
        Invoke(_room, "RefreshSummary");
        var names = sample.Ingredients.Select(ingredient => ingredient.HerbName)
            .Concat(_catalog.Where(material => _owner.has_inventory_item(material.Path)).Select(material => material.Name))
            .Distinct().Take(8).ToArray();
        Check(names.Length == 8, "满配夹具使用八种真实持有材料");
        var full = new AlchemyRecipeCatalog.SampleRoute("八味布局验证", "", names.Select((name, index) =>
            new AlchemyRecipeCatalog.SampleIngredient(name, 1, index - 1)).ToArray());
        Invoke(_room!, "ApplySampleRoute", full, false);
        await Frames(3);
        Check(Inputs().Count == 8 && Inputs().Select(input => input.Path).Distinct().Count() == 8,
            "508造诣显示君臣佐使全部八味");
        foreach (string name in new[] { "ZuoHerbGroup", "ShiHerbGroup" })
        {
            var group = Find<Control>(_room!, name);
            Check(Math.Abs(group.Size.Y - 326) <= 2, "第三位开放时药签保持326高 " + name, group.Size);
        }
        for (int index = 0; index < 8; index++)
        {
            var pick = Find<Button>(_room!, "PickHerb_" + index);
            var more = Find<Button>(_room!, "DosePlus_" + index);
            var less = Find<Button>(_room!, "DoseMinus_" + index);
            var group = Find<Control>(_room!, index == 0 ? "JunHerbCard" : index == 1 ? "ChenHerbCard"
                : index is 2 or 3 or 5 ? "ZuoHerbGroup" : "ShiHerbGroup");
            Check(Bounds(group).Grow(2).Encloses(Bounds(pick))
                && Bounds(group).Grow(2).Encloses(Bounds(more))
                && Bounds(group).Grow(2).Encloses(Bounds(less)), "八味控件未越过药签边界 " + index);
            await Click(pick);
            Check(Field<Control>(_room!, "_pickerOverlay").Visible && Field<int>(_room!, "_pickerTarget") == index - 1,
                "八味选药点击到达正确药位 " + index);
            Field<Control>(_room!, "_pickerOverlay").Hide();
            await Click(more);
            Check((index == 0 ? Field<int>(_room!, "_mainDose") : Field<int[]>(_room!, "_assistantDoses")[index - 1]) == 2
                && Field<LineEdit[]>(_room, "_paperDoseInputs")[index].Text == "2" && !less.Disabled,
                "八味份数加量点击有效 " + index);
            await Click(less);
            Check((index == 0 ? Field<int>(_room!, "_mainDose") : Field<int[]>(_room!, "_assistantDoses")[index - 1]) == 1
                && Field<LineEdit[]>(_room, "_paperDoseInputs")[index].Text == "1" && less.Disabled,
                "八味份数减量点击恢复 " + index);
        }
        await Capture("full_eight");
    }

    private void WriteReport(bool passed, string failure)
    {
        if (_output.Length == 0 || !System.IO.Directory.Exists(_output)) return;
        var report = new
        {
            status = passed ? "passed" : "failed", width = _width, height = _height,
            headless = _headless, passed = _checks.Count(check => check.Passed), failed = _checks.Count(check => !check.Passed),
            failure, materialCatalogCount = _loadedMaterials, captures = _captures, checks = _checks,
            inputMethod = _headless ? "button signals with visibility assertions" : "Viewport.PushInput mouse hit testing",
            ownerIsIsolatedFixture = true, playerJourneyRead = false, brewStarted = false
        };
        System.IO.File.WriteAllText(System.IO.Path.Combine(_output, $"alchemy_paper_checks_{_width}.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private void CheckPlaceholder(LineEdit input, string name)
    {
        var color = input.GetThemeColor("font_placeholder_color");
        Check(input.IsVisibleInTree() && color.A >= .65f && (color.R + color.G + color.B) / 3 < .7f,
            name, color);
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(nameof(Run));
    }

    private async void Run()
    {
        bool passed = false; string failure = "";
        try
        {
            Configure(); await Frames(); LoadCatalog();
            var sample = AlchemyRecipeCatalog.Pills.Single(pill => pill.Id == "huichun").SampleRoutes.Single(route => route.Name == "青木清露方");
            CreateOwner(sample);
            _room = GD.Load<PackedScene>("res://tscn/scene/AlchemyPreviewScene.tscn").Instantiate<AlchemySlotBrewPreviewScene>();
            _room.OwnerCharacter = _owner;
            AddChild(_room); _room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Frames(3);
            Field<Control>(_room, "_tutorialOverlay").Hide();
            string originalStock = InventoryStamp();
            float originalEnergy = _owner!.current_world_stamina;
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            double originalTime = time.get_precise_game_hours();
            CheckMaterialsAndStyle();
            await CheckSample(sample);
            await CheckPicker(sample);
            await CheckNotebook(sample);
            await CheckEightSlots(sample);
            Check(InventoryStamp() == originalStock && _owner.current_world_stamina == originalEnergy
                && Math.Abs(time.get_precise_game_hours() - originalTime) < 1e-6
                && ((HumanData)_owner.entity_data!).alchemy_records.Count == 0, "纯配伍浏览不扣料精力、不推进历法、不生成炉次记录");
            SetField(_room, "_brewing", true); _room.ReturnToBaigong();
            Check(!_room.IsQueuedForDeletion(), "开炼标记期间返回保护保持");
            SetField(_room, "_brewing", false);
            var stage = new Control { Name = "PaperUiReturnGuardFixture" }; Field<Control>(_room, "_canvas").AddChild(stage);
            SetField(_room, "_brewStage", stage); _room.ReturnToBaigong();
            Check(!_room.IsQueuedForDeletion(), "鉴看领取阶段返回保护保持");
            SetField(_room, "_brewStage", null); stage.QueueFree(); await Frames();
            var currentScene = GetTree().CurrentScene;
            await Click(Find<Button>(_room, "ReturnButton"));
            Check((!GodotObject.IsInstanceValid(_room) || _room.IsQueuedForDeletion()) && GetTree().CurrentScene == currentScene,
                "右上返回只关闭正式Owner页面，不切换旅程场景");
            passed = true;
        }
        catch (Exception error)
        {
            failure = error.ToString(); GD.PushError(failure);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(_room) && !_room!.IsQueuedForDeletion()) _room.QueueFree();
            if (GodotObject.IsInstanceValid(_owner) && !_owner!.IsQueuedForDeletion()) _owner.QueueFree();
            await Frames(3);
            if (_notebookFixture.Length > 0 && IsWithin(_notebookFixture, _output) && System.IO.File.Exists(_notebookFixture))
                System.IO.File.Delete(_notebookFixture);
            WriteReport(passed, failure);
            GD.Print($"ALCHEMY_PAPER_UI_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}");
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
