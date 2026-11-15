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

    private void BuildRecipeOverlay() => BuildPaperNotebook();

    private static void ClearNotebookChildren(Node parent)
    {
        foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }

    private void RefreshNotebookList()
    {
        ClearNotebookChildren(_notebookList);
        _builtinTab.AddThemeStyleboxOverride("normal", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, !_showSaved ? CraftingPaperStyle.ButtonState.Selected : CraftingPaperStyle.ButtonState.Normal));
        _savedTab.AddThemeStyleboxOverride("normal", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, _showSaved ? CraftingPaperStyle.ButtonState.Selected : CraftingPaperStyle.ButtonState.Normal));
        _savedTab.Text = $"我的丹方 · {_savedRecipes.Count}";
        _selectedRoute = null; _selectedSaved = null; _pendingDelete = "";
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
                    var required = NotebookRequiredAttainment(route);
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
        CraftingPaperStyle.ApplyButton(button, CraftingPaperStyle.ButtonKind.Regular, 22);
        button.CustomMinimumSize = new Vector2(0, 74);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.ClipContents = true;
        var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        labels.OffsetLeft = 12; labels.OffsetRight = -12;
        labels.OffsetTop = 9; labels.OffsetBottom = -9;
        labels.AddThemeConstantOverride("separation", 4);
        button.AddChild(labels);
        var title = PaperText(name, 22);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.MouseFilter = MouseFilterEnum.Ignore;
        labels.AddChild(title);
        var detail = PaperText(summary, 16, true);
        detail.AddThemeColorOverride("font_color", new Color("737c70"));
        detail.MouseFilter = MouseFilterEnum.Ignore;
        labels.AddChild(detail);
        return button;
    }

    private void HighlightNotebookSelection(string name)
    {
        foreach (var child in _notebookList.GetChildren())
            if (child is Button button)
                button.AddThemeStyleboxOverride("normal", CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, button.GetMeta("recipe_name", "").AsString() == name ? CraftingPaperStyle.ButtonState.Selected : CraftingPaperStyle.ButtonState.Normal));
    }

    private static double NotebookRequiredAttainment(AlchemyRecipeCatalog.SampleRoute route)
    {
        int slots = route.Ingredients.Select(i => i.AssistantSlot < 0 ? 8 : UnlockAt(i.AssistantSlot)).DefaultIfEmpty(8).Max();
        // 内置方已有明确丹阶；自拟配伍只检查药位，不提前推断其成品。
        var known = AlchemyRecipeCatalog.Pills.FirstOrDefault(p => p.SampleRoutes.Contains(route));
        return Math.Max(slots, known is null ? 8 : AlchemySlotSimulation.RequiredAttainment(known.Rank));
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
        var required = NotebookRequiredAttainment(route);
        string requirement = _selectedSaved is null ? "开炼与药位" : "药位";
        _notebookDetail.AddChild(QuietRoomStyle.Text($"{route.Ingredients.Length}味 · {route.Ingredients.Sum(i => i.Dose)}份 · 产量随配伍结算 · {requirement}需造诣{required}", 15));
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
        if (Attainment < required) _notebookDetail.AddChild(QuietRoomStyle.Text($"当前造诣不足，需要{required}才能载入此方。", 15));
    }

    private void LoadNotebookSelection()
    {
        if (_selectedRoute is null) return;
        ApplySampleRoute(_selectedRoute);
        _recipeOverlay.Hide();
    }

    private void SaveCurrentRecipe(bool overwrite)
    {
        if (_notebookError.Length > 0) { _notebookStatus.Text = _notebookError; return; }
        var name = _recipeName.Text.Trim();
        if (name.Length == 0) { _notebookStatus.Text = "请先填写丹方名称。"; return; }
        if (_main is null) { _notebookStatus.Text = "请先选择君药，再保存当前配伍。"; return; }
        var existing = _savedRecipes.FirstOrDefault(r => r.Name == name);
        if (existing is not null && !overwrite) { _notebookStatus.Text = "同名丹方已存在；如需更新，请点击“覆盖同名”。"; return; }
        var entries = new List<AlchemyRecipeStore.Entry> { new(_main.Item.definition_id.ToString(), _mainDose, -1) };
        for (int i = 0; i < _assistants.Length; i++)
            if (_assistants[i] is { } material) entries.Add(new(material.Item.definition_id.ToString(), _assistantDoses[i], i));
        var recipe = new AlchemyRecipeStore.Recipe(existing?.Id ?? Guid.NewGuid().ToString("N"), name, entries.ToArray(), 0);
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
