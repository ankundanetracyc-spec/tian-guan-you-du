using Godot;
using Kind = FormationGridModel.Kind;

public partial class FormationWorkbench
{
    private readonly System.Collections.Generic.List<Button> _categoryButtons = [];
    private readonly System.Collections.Generic.List<Control> _categoryPages = [];
    private ScrollContainer _partsScroll = null!;
    private int _partCategory;
    public int SelectedPartCategory => _partCategory;

    private void BuildPartCategories(VBoxContainer body)
    {
        var tabs = new HBoxContainer { Name = "FormationPartCategories", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tabs.AddThemeConstantOverride("separation", 3); body.AddChild(tabs);
        var names = new[] { "Inputs", "Logic", "Outputs", "Source" };
        var captions = new[] { "输入", "逻辑", "输出", "阵源" };
        for (var index = 0; index < names.Length; index++)
        {
            var chosen = index;
            var button = Button(captions[index], () => SelectPartCategory(chosen));
            button.Name = "FormationCategory" + names[index]; button.ToggleMode = true; button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tabs.AddChild(button); _categoryButtons.Add(button);
        }
        var grouping = new PanelContainer { Name = "FormationCategoryContents", SizeFlagsVertical = SizeFlags.ExpandFill };
        grouping.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(8, false, .96f)); body.AddChild(grouping);
        _partsScroll = new ScrollContainer { Name = "FormationPartsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        grouping.AddChild(_partsScroll);
        var pages = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _partsScroll.AddChild(pages);
        Kind[][] kinds = [
            [Kind.Sensor, Kind.StateInput],
            [Kind.Compare, Kind.Not, Kind.Switch, Kind.Filter, Kind.Counter, Kind.Split, Kind.Merge],
            [Kind.Ward, Kind.Guard, Kind.Fire],
            [Kind.Source]
        ];
        for (var index = 0; index < kinds.Length; index++)
        {
            var page = new VBoxContainer { Name = "FormationParts" + names[index], SizeFlagsHorizontal = SizeFlags.ExpandFill };
            page.AddThemeConstantOverride("separation", 8); pages.AddChild(page); _categoryPages.Add(page);
            if (index == 0)
            {
                page.AddChild(Text("战斗感应", 18, "aeb8aa"));
                AddPart(page, Kind.Sensor);
                page.AddChild(Text("感应成功施展的术法。触发来自真实战斗行为。", 16, "9ca99d"));
                page.AddChild(Text("状态感应", 18, "aeb8aa"));
                AddPart(page, Kind.StateInput, "health", "气血感应");
                AddPart(page, Kind.StateInput, "shield", "护盾感应");
                AddPart(page, Kind.StateInput, "mana", "法力感应");
                AddPart(page, Kind.StateInput, "energy", "阵能感应");
                page.AddChild(Text("拖入哪种感应部件，就读取哪种真实状态。", 16, "9ca99d"));
                continue;
            }
            foreach (var kind in kinds[index]) AddPart(page, kind);
        }
        SelectPartCategory(0);
    }

    public void SelectPartCategory(int index)
    {
        _partCategory = Math.Clamp(index, 0, _categoryPages.Count - 1);
        for (var i = 0; i < _categoryPages.Count; i++)
        {
            _categoryPages[i].Visible = i == _partCategory;
            SetPaperSelected(_categoryButtons[i], i == _partCategory, true);
            _categoryButtons[i].AddThemeFontSizeOverride("font_size", 17);
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                _categoryButtons[i].AddThemeStyleboxOverride(state, CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, i == _partCategory ? CraftingPaperStyle.ButtonState.Selected : CraftingPaperStyle.ButtonState.Normal, 3, 7));
        }
        _partsScroll.ScrollVertical = 0;
        if (Canvas is not null && Canvas.Tool == FormationBoardCanvas.EditTool.Component &&
            !_parts.Values.Any(button => button.ButtonPressed && button.IsVisibleInTree()))
            ChooseTool(FormationBoardCanvas.EditTool.Select);
    }
}
