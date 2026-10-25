using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class AlchemySlotBrewPreviewScene
{
    private string? _notebookPathOverride;
    private string NotebookPath => _notebookPathOverride ?? ProjectSettings.GlobalizePath("user://alchemy/recipes_v1.json");
    private List<AlchemyRecipeStore.Recipe> _savedRecipes = [];
    private string _notebookError = "";
    private VBoxContainer _notebookList = null!;
    private VBoxContainer _notebookDetail = null!;
    private LineEdit _recipeName = null!;
    private Label _notebookStatus = null!;
    private Button _recipeLoad = null!;
    private Button _recipeDelete = null!;
    private Button _builtinTab = null!;
    private Button _savedTab = null!;
    private bool _showSaved;
    private bool _showPills;
    private Button _pillTab = null!;
    private HBoxContainer _notebookActions = null!;
    private HBoxContainer _notebookSaveRow = null!;
    private ScrollContainer _notebookDetailScroll = null!;
    private Label _pillRule = null!;
    private string _selectedPillId = "";
    private string _pendingDelete = "";
    private AlchemyRecipeCatalog.SampleRoute? _selectedRoute;
    private AlchemyRecipeStore.Recipe? _selectedSaved;

    private void OpenRecipeBook()
    {
        HideHover();
        _notebookError = "";
        try { _savedRecipes = AlchemyRecipeStore.Load(NotebookPath); }
        catch (Exception error) { _notebookError = "本地丹方读取失败，原文件保留：" + error.Message; }
        RefreshNotebookList();
        _notebookStatus.Text = _notebookError;
        _recipeOverlay.Show();
    }

    private void BuildRecipeOverlay()
    {
        _recipeOverlay = new Control { Name = "KnownFormulaOverlay", Visible = false, ZIndex = 120, MouseFilter = MouseFilterEnum.Stop };
        _recipeOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas.AddChild(_recipeOverlay);
        var veil = new ColorRect { Color = new Color(.10f, .14f, .12f, .58f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _recipeOverlay.AddChild(veil);
        var panel = MakePaperPanel(.99f, 26);
        PlaceCentered(_recipeOverlay, panel, new Vector2(1200, 780));
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        panel.AddChild(body);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        body.AddChild(header);
        var icon = IconRect(76); icon.Texture = LoadUiTexture("formula_notebook_labeled_v2.png"); header.AddChild(icon);
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddChild(QuietRoomStyle.Text("丹方册", 28));
        title.AddChild(QuietRoomStyle.Text("记下配伍，留待下次开炉", 15));
        header.AddChild(title);
        header.AddChild(MakeButton("收起", () => _recipeOverlay.Hide(), 17));
        body.AddChild(new HSeparator());
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 10);
        body.AddChild(tabs);
        _builtinTab = MakeButton("内置样方", () => SetNotebookTab(false, false), 17);
        _savedTab = MakeButton("我的丹方", () => SetNotebookTab(true, false), 17);
        _pillTab = MakeButton("丹药说明", () => SetNotebookTab(false, true), 17);
        tabs.AddChild(_builtinTab); tabs.AddChild(_savedTab);
        tabs.AddChild(_pillTab);
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 20);
        body.AddChild(content);
        var listScroll = new ScrollContainer { CustomMinimumSize = new Vector2(320, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(listScroll);
        _notebookList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _notebookList.AddThemeConstantOverride("separation", 8);
        listScroll.AddChild(_notebookList);
        content.AddChild(new VSeparator());
        var detailScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _notebookDetailScroll = detailScroll;
        content.AddChild(detailScroll);
        _notebookDetail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _notebookDetail.AddThemeConstantOverride("separation", 12);
        detailScroll.AddChild(_notebookDetail);
        body.AddChild(new HSeparator());
        var actions = new HBoxContainer();
        _notebookActions = actions;
        actions.AddThemeConstantOverride("separation", 12);
        body.AddChild(actions);
        _recipeLoad = MakeButton("载入此方", LoadNotebookSelection, 18);
        actions.AddChild(_recipeLoad);
        _recipeDelete = MakeButton("删除", DeleteNotebookSelection, 16);
        actions.AddChild(_recipeDelete);
        var note = QuietRoomStyle.Text("载入只替换投料，开炉后才揭晓结果", 14);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(note);
        var saveRow = new HBoxContainer();
        _notebookSaveRow = saveRow;
        saveRow.AddThemeConstantOverride("separation", 10);
        body.AddChild(saveRow);
        saveRow.AddChild(QuietRoomStyle.Text("保存当前配伍", 17));
        _recipeName = new LineEdit { Name = "RecipeName", PlaceholderText = "为当前丹方起名", MaxLength = 40, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _recipeName.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(false, 10));
        _recipeName.AddThemeStyleboxOverride("focus", QuietRoomStyle.Edge(true, 10));
        _recipeName.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
        saveRow.AddChild(_recipeName);
        saveRow.AddChild(MakeButton("保存新方", () => SaveCurrentRecipe(false), 17));
        saveRow.AddChild(MakeButton("覆盖同名", () => SaveCurrentRecipe(true), 16));
        _notebookStatus = QuietRoomStyle.Text("", 14);
        _notebookStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_notebookStatus);
        _pillRule = QuietRoomStyle.Text("丹方所记为投料配伍，成丹种类仍由君药药性与实际炼制推定。", 15);
        _pillRule.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_pillRule);
        RefreshNotebookList();
    }

    private void SetNotebookTab(bool saved, bool pills)
    {
        _showSaved = saved && !pills;
        _showPills = pills;
        RefreshNotebookList();
    }

    private static void ClearNotebookChildren(Node parent)
    {
        foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }

    private void RefreshNotebookList()
    {
        ClearNotebookChildren(_notebookList);
        _builtinTab.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(!_showSaved && !_showPills, 10));
        _savedTab.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(_showSaved, 10));
        _pillTab.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(_showPills, 10));
        _notebookActions.Visible = !_showPills;
        _notebookSaveRow.Visible = !_showPills;
        _notebookStatus.Visible = !_showPills;
        _pillRule.Visible = _showPills;
        _notebookStatus.Text = _notebookError;
        _notebookDetailScroll.ScrollVertical = 0;
        _savedTab.Text = $"我的丹方 · {_savedRecipes.Count}";
        _selectedRoute = null; _selectedSaved = null; _pendingDelete = "";
        if (_showPills)
        {
            foreach (var pill in AlchemyRecipeCatalog.Pills)
            {
                var current = pill;
                var button = NotebookListButton(AlchemyPillDescriptions.DisplayName(pill), "丹药说明", () => ShowPillDescription(current));
                button.Name = "PillDescription_" + pill.Id;
                _notebookList.AddChild(button);
            }
            var selected = AlchemyRecipeCatalog.Pills.FirstOrDefault(p => p.Id == _selectedPillId)
                ?? AlchemyRecipeCatalog.Pills.FirstOrDefault();
            if (selected is not null) ShowPillDescription(selected);
            else { ClearNotebookChildren(_notebookDetail); _notebookDetail.AddChild(QuietRoomStyle.Text("尚无丹药记载", 24)); }
            return;
        }
        if (_showSaved)
        {
            foreach (var recipe in _savedRecipes)
            {
                var current = recipe;
                var button = NotebookListButton(recipe.Name,
                    $"{recipe.Ingredients.Length}味 · {recipe.Ingredients.Sum(i => i.Dose)}份 · 自拟",
                    () => SelectSavedRecipe(current));
                SetHover(button, recipe.Name);
                _notebookList.AddChild(button);
            }
            if (_savedRecipes.Count == 0) _notebookList.AddChild(QuietRoomStyle.Text("尚无自拟丹方\n配好药材后，在下方命名保存。", 16));
        }
        else
        {
            foreach (var pill in AlchemyRecipeCatalog.Pills)
            {
                _notebookList.AddChild(QuietRoomStyle.Text(pill.Name, 18));
                foreach (var route in pill.SampleRoutes)
                {
                    var current = route;
                    var required = route.Ingredients.Select(i => i.AssistantSlot < 0 ? 8 : UnlockAt(i.AssistantSlot)).Max();
                    var button = NotebookListButton(route.Name,
                        $"{route.Ingredients.Length}味 · {route.Ingredients.Sum(i => i.Dose)}份 · 造诣{required}",
                        () => SelectNotebookRoute(current));
                    button.Name = $"KnownFormula_{pill.Id}_{route.Name}";
                    SetHover(button, route.Direction);
                    _notebookList.AddChild(button);
                }
            }
        }
        ShowNotebookDetail(null);
    }

    private void SelectNotebookRoute(AlchemyRecipeCatalog.SampleRoute route)
    {
        _selectedSaved = null; _selectedRoute = route; _pendingDelete = "";
        _notebookStatus.Text = _notebookError;
        HighlightNotebookSelection(route.Name);
        ShowNotebookDetail(route);
    }

    private void SelectSavedRecipe(AlchemyRecipeStore.Recipe recipe)
    {
        _selectedSaved = recipe; _pendingDelete = "";
        _notebookStatus.Text = _notebookError;
        HighlightNotebookSelection(recipe.Name);
        var resolved = recipe.Ingredients.Select(i => (Entry: i, Material:
            _materials.FirstOrDefault(m => m.Item.definition_id.ToString() == i.MaterialId))).ToArray();
        if (resolved.Any(i => i.Material is null))
        {
            _selectedRoute = null;
            ShowNotebookDetail(null);
            _notebookStatus.Text = "此方有材料已不可用，无法载入；本地记录保留，可删除或另存新方。";
            _recipeDelete.Visible = true;
            return;
        }
        _selectedRoute = new(recipe.Name, "本地保存的自拟配伍，可载入后继续调整。",
            resolved.Select(i => new AlchemyRecipeCatalog.SampleIngredient(i.Material!.Name, i.Entry.Dose, i.Entry.Slot)).ToArray());
        _recipeName.Text = recipe.Name;
        ShowNotebookDetail(_selectedRoute);
    }

    private Button NotebookListButton(string name, string summary, Action action)
    {
        var button = MakeButton("", action, 17);
        button.SetMeta("recipe_name", name);
        button.CustomMinimumSize = new Vector2(0, 66);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.ClipContents = true;
        var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        labels.OffsetLeft = 12; labels.OffsetRight = -12;
        labels.OffsetTop = 9; labels.OffsetBottom = -9;
        labels.AddThemeConstantOverride("separation", 4);
        button.AddChild(labels);
        var title = QuietRoomStyle.Text(name, 17);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.MouseFilter = MouseFilterEnum.Ignore;
        labels.AddChild(title);
        var detail = QuietRoomStyle.Text(summary, 13);
        detail.AddThemeColorOverride("font_color", new Color("737c70"));
        detail.MouseFilter = MouseFilterEnum.Ignore;
        labels.AddChild(detail);
        return button;
    }

    private void HighlightNotebookSelection(string name)
    {
        foreach (var child in _notebookList.GetChildren())
            if (child is Button button)
                button.AddThemeStyleboxOverride("normal", QuietRoomStyle.Edge(button.GetMeta("recipe_name", "").AsString() == name, 10));
    }

    private void ShowNotebookDetail(AlchemyRecipeCatalog.SampleRoute? route)
    {
        ClearNotebookChildren(_notebookDetail);
        _recipeLoad.Disabled = route is null;
        _recipeDelete.Visible = _selectedSaved is not null;
        _recipeDelete.Text = "删除";
        if (route is null)
        {
            _notebookDetail.AddChild(QuietRoomStyle.Text("选一张丹方，展开配伍", 24));
            _notebookDetail.AddChild(QuietRoomStyle.Text("材料、药位与份数会记录在这里。\n也可将炉前当前配伍保存为自己的丹方。", 16));
            return;
        }
        var title = QuietRoomStyle.Text(route.Name, 25);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notebookDetail.AddChild(title);
        var direction = QuietRoomStyle.Text(route.Direction, 15);
        direction.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notebookDetail.AddChild(direction);
        var required = route.Ingredients.Select(i => i.AssistantSlot < 0 ? 8 : UnlockAt(i.AssistantSlot)).Max();
        _notebookDetail.AddChild(QuietRoomStyle.Text($"{route.Ingredients.Length}味 · {route.Ingredients.Sum(i => i.Dose)}份 · 药位需造诣{required}", 15));
        _recipeLoad.Disabled = Attainment < required;
        foreach (var ingredient in route.Ingredients.OrderBy(i => (int)RoleFor(i.AssistantSlot)).ThenBy(i => i.AssistantSlot))
        {
            var material = _materials.FirstOrDefault(m => m.Name == ingredient.HerbName);
            var card = MakePaperPanel(.65f, 10);
            _notebookDetail.AddChild(card);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            card.AddChild(row);
            var role = QuietRoomStyle.Text(ingredient.AssistantSlot < 0 ? "君药" : AssistantRoleShort(ingredient.AssistantSlot), 18);
            role.CustomMinimumSize = new Vector2(54, 0);
            row.AddChild(role);
            var icon = IconRect(42); icon.Texture = material is null ? null : MaterialIcon(material); row.AddChild(icon);
            var name = QuietRoomStyle.Text(ingredient.HerbName, 18); name.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(name);
            row.AddChild(QuietRoomStyle.Text($"{ingredient.Dose}份", 18));
            if (material is not null) SetHover(card, MaterialTooltip(material, ingredient.AssistantSlot));
        }
        if (Attainment < required) _notebookDetail.AddChild(QuietRoomStyle.Text($"当前造诣不足，需要{required}才能载入全部药位。", 15));
    }

    private void LoadNotebookSelection()
    {
        if (_showPills) return;
        if (_selectedRoute is null) return;
        ApplySampleRoute(_selectedRoute);
        _recipeOverlay.Hide();
    }

    private void SaveCurrentRecipe(bool overwrite)
    {
        if (_showPills) return;
        if (_notebookError.Length > 0) { _notebookStatus.Text = _notebookError; return; }
        var name = _recipeName.Text.Trim();
        if (name.Length == 0) { _notebookStatus.Text = "请先填写丹方名称。"; return; }
        if (_main is null) { _notebookStatus.Text = "请先选择君药，再保存当前配伍。"; return; }
        var existing = _savedRecipes.FirstOrDefault(r => r.Name == name);
        if (existing is not null && !overwrite) { _notebookStatus.Text = "同名丹方已存在；如需更新，请点击“覆盖同名”。"; return; }
        var entries = new List<AlchemyRecipeStore.Entry> { new(_main.Item.definition_id.ToString(), _mainDose, -1) };
        for (int i = 0; i < _assistants.Length; i++)
            if (_assistants[i] is { } material) entries.Add(new(material.Item.definition_id.ToString(), _assistantDoses[i], i));
        var recipe = new AlchemyRecipeStore.Recipe(existing?.Id ?? Guid.NewGuid().ToString("N"), name, entries.ToArray());
        var next = _savedRecipes.Where(r => r.Id != recipe.Id).Append(recipe).ToList();
        try
        {
            AlchemyRecipeStore.Save(NotebookPath, next);
            _savedRecipes = next; _showSaved = true;
            RefreshNotebookList(); SelectSavedRecipe(recipe);
            _notebookStatus.Text = $"已保存「{name}」，下次打开仍可载入。";
        }
        catch (Exception error) { _notebookStatus.Text = "保存失败：" + error.Message; }
    }

    private void DeleteNotebookSelection()
    {
        if (_showPills) return;
        if (_selectedSaved is null || _notebookError.Length > 0) return;
        if (_pendingDelete != _selectedSaved.Id)
        {
            _pendingDelete = _selectedSaved.Id; _recipeDelete.Text = "确认删除";
            _notebookStatus.Text = $"再次点击确认删除「{_selectedSaved.Name}」。"; return;
        }
        var next = _savedRecipes.Where(r => r.Id != _selectedSaved.Id).ToList();
        try
        {
            AlchemyRecipeStore.Save(NotebookPath, next);
            _savedRecipes = next; RefreshNotebookList();
            _notebookStatus.Text = "丹方已删除。";
        }
        catch (Exception error) { _notebookStatus.Text = "删除失败：" + error.Message; }
    }
}
