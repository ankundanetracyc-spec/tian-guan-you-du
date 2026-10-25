using Godot;

public partial class AlchemySlotBrewPreviewScene
{
    private void ShowPillDescription(AlchemyRecipeCatalog.PillType pill)
    {
        if (!_showPills) return;
        _selectedPillId = pill.Id;
        _selectedRoute = null;
        _selectedSaved = null;
        _pendingDelete = "";
        _recipeLoad.Disabled = true;
        _recipeDelete.Visible = false;
        _recipeDelete.Text = "删除";
        ClearNotebookChildren(_notebookDetail);
        _notebookDetailScroll.ScrollVertical = 0;
        HighlightNotebookSelection(AlchemyPillDescriptions.DisplayName(pill));

        if (!AlchemyPillDescriptions.Entries.TryGetValue(pill.Id, out var entry))
        {
            AddPillText(AlchemyPillDescriptions.DisplayName(pill), 26);
            AddPillText("此丹图文尚待补录。", 17);
            return;
        }
        var icon = new TextureRect
        {
            Name = "PillPortrait",
            CustomMinimumSize = new Vector2(0, 170),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        if (ResourceLoader.Exists(entry.TexturePath)) icon.Texture = GD.Load<Texture2D>(entry.TexturePath);
        _notebookDetail.AddChild(icon);
        if (icon.Texture is null) AddPillText("丹药图待补", 15);
        AddPillText(AlchemyPillDescriptions.DisplayName(pill), 27);
        var verse = AddPillText(entry.Verse, 20);
        verse.AddThemeColorOverride("font_color", new Color("657664"));
        _notebookDetail.AddChild(new HSeparator());
        AddPillText("药品简述", 19);
        AddPillText(entry.Description, 18);
        AddPillText("炼制指引", 19);
        AddPillText(entry.Guidance, 18);
    }

    private Label AddPillText(string text, int size)
    {
        var label = QuietRoomStyle.Text(text, size);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _notebookDetail.AddChild(label);
        return label;
    }
}
