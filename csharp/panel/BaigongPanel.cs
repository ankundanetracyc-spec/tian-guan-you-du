using Godot;
using Godot.Collections;

/// <summary>个人百工资料：查阅丹方与炼制记录，实际炼制由地点设施承接。</summary>
public partial class BaigongPanel : Control
{
    private Character? _owner;
    private VBoxContainer? _body;
    private string _page = "阵法";
    private FormationPaperWorkspace? _formationWorkspace;
    private readonly System.Collections.Generic.Dictionary<string, Button> _tabs = [];
    private readonly System.Collections.Generic.Dictionary<string, string> _materialNames = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; Hide(); build();
    }

    public void show_for(Character owner) { _owner = owner; Show(); select_page("阵法"); }
    public void close()
    {
        if (IsInstanceValid(_formationWorkspace)) _formationWorkspace!.Close();
        Hide();
    }

    private void build()
    {
        var shade = new ColorRect { Color = new Color(.015f, .02f, .018f, .82f), MouseFilter = MouseFilterEnum.Stop }; shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(1040, 650) }; frame.SetAnchorsPreset(LayoutPreset.Center); frame.Position = new Vector2(-520, -325); frame.AddThemeStyleboxOverride("panel", panel_style()); AddChild(frame);
        var outer = new HBoxContainer(); outer.AddThemeConstantOverride("separation", 0); frame.AddChild(outer);
        var tabBar = new VBoxContainer { CustomMinimumSize = new Vector2(126, 0) }; tabBar.AddThemeConstantOverride("separation", 8); outer.AddChild(tabBar);
        tabBar.AddChild(title("百工", 25, new Color("f1d77a"))); tabBar.AddChild(muted("传承与器物"));
        foreach (var page in new[] { "法宝", "丹药", "符箓", "阵法" })
        {
            var tab = new Button { Text = page, CustomMinimumSize = new Vector2(118, 48) }; tab.Pressed += () => select_page(page); _tabs[page] = tab; tabBar.AddChild(tab);
        }
        var close = new Button { Text = "关闭", CustomMinimumSize = new Vector2(118, 42) }; close.Pressed += this.close; tabBar.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) }); tabBar.AddChild(close);
        var divider = new ColorRect { Color = new Color(.68f, .56f, .32f, .45f), CustomMinimumSize = new Vector2(1, 0) }; outer.AddChild(divider);
        _body = new VBoxContainer(); _body.SizeFlagsHorizontal = SizeFlags.ExpandFill; _body.AddThemeConstantOverride("separation", 12); outer.AddChild(_body);
    }

    private void select_page(string page)
    {
        _page = page;
        foreach (var (name, tab) in _tabs) { tab.Disabled = name == page; tab.Modulate = name == page ? new Color("f1d77a") : new Color("d8d0b8"); }
        if (_body is null) return; clear(_body);
        if (page == "阵法") build_formation(); else if (page == "法宝") build_forging(); else if (page == "丹药") build_alchemy(); else build_placeholder(page);
    }

    private void build_alchemy()
    {
        _body!.AddChild(title("丹方与炼丹记录", 27, new Color("f4f0df")));
        _body.AddChild(muted("此处查阅配伍与过往炼制。前往玉虚仙宗·天工峰的丹房可开炉炼丹。"));
        var list = archive_list();
        foreach (var pill in AlchemyRecipeCatalog.Pills)
        foreach (var route in pill.SampleRoutes)
            list.AddChild(muted($"{route.Name} · {pill.Name}\n" + string.Join("、", route.Ingredients.Select(i => $"{recipe_role(i.AssistantSlot)}：{i.HerbName}×{i.Dose}"))));
        list.AddChild(title("我的丹方", 20, new Color("f4f0df")));
        try
        {
            var saved = AlchemyRecipeStore.Load(ProjectSettings.GlobalizePath("user://alchemy/recipes_v1.json"));
            foreach (var recipe in saved)
                list.AddChild(muted(recipe.Name + "\n" + string.Join("、", recipe.Ingredients.Select(i =>
                    $"{recipe_role(i.Slot)}：{material_name(i.MaterialId)}×{i.Dose}"))));
            if (saved.Count == 0) list.AddChild(muted("尚未记录自己的丹方。"));
        }
        catch (System.Exception) { list.AddChild(muted("我的丹方暂时无法读取，原有文件已保留。")); }
        list.AddChild(title("炼丹记录", 20, new Color("f4f0df")));
        add_records(list, (_owner?.entity_data as HumanData)?.alchemy_records);
    }

    private void build_forging()
    {
        _body!.AddChild(title("炼器记录", 27, new Color("f4f0df")));
        _body.AddChild(muted("此处查阅过往炼制。前往玉虚仙宗·天工峰的器作可开炉炼器。"));
        var list = archive_list();
        add_records(list, (_owner?.entity_data as HumanData)?.forging_records);
        list.AddChild(title("持有器物的炼制存录", 20, new Color("f4f0df")));
        var found = false;
        if (_owner is not null) foreach (var item in _owner.get_artifact_items())
        {
            if (!ForgedArtifactInventory.TryRead(item, out var snapshot)) continue;
            found = true;
            list.AddChild(muted($"{snapshot.Name} · {snapshot.Tier} · 造诣{snapshot.Proficiency}\n" +
                string.Join("、", snapshot.Sources.Select(s => $"{s.Input.Role.Replace("复合身材", "辅体")}：{s.Input.Material}×{s.Input.Quantity}"))));
        }
        if (!found) list.AddChild(muted("尚无持有器物的炼制存录。"));
    }

    private VBoxContainer archive_list()
    {
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(820, 440), SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _body!.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(list); return list;
    }
    private static string recipe_role(int slot) => slot < 0 ? "君" : slot == 0 ? "臣" : slot is 1 or 2 or 4 ? "佐" : "使";
    private string material_name(string id)
    {
        if (_materialNames.Count == 0)
        {
            using var dir = DirAccess.Open("res://resources/entities/items");
            if (dir is not null) foreach (var file in dir.GetFiles().Where(f => f.EndsWith(".tres")))
            {
                var item = GD.Load<ItemData>("res://resources/entities/items/" + file);
                if (item is not null) _materialNames[item.definition_id.ToString()] = item.entity_name;
            }
        }
        return _materialNames.GetValueOrDefault(id, "未识别药材");
    }
    private static void add_records(VBoxContainer list, Array<string>? records)
    {
        if (records is null || records.Count == 0) { list.AddChild(muted("尚无炼制记录。")); return; }
        foreach (var record in records.AsEnumerable().Reverse()) list.AddChild(muted(record));
    }

    private void build_placeholder(string page)
    {
        _body!.AddChild(title(page, 27, new Color("f4f0df")));
        _body.AddChild(muted("百工 · 传承与器物"));
        var space = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddChild(space);
        var empty = card("尚未开放", 360);
        empty.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        empty.AddChild(muted($"{page}相关内容尚未开放，敬请期待。"));
        space.AddChild(empty);
    }

    private void build_formation()
    {
        _body!.AddChild(title("阵图与布线", 27, new Color("f4f0df")));
        _body.AddChild(muted("在案上摆放部件，连接信号与供能，保存自己的阵图草稿。"));
        _body.AddChild(muted("初阶草图 · 4×4 · 单层接线"));
        var open = new Button { Name = "OpenFormationWorkspace", Text = "绘制阵图", CustomMinimumSize = new Vector2(210, 52), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        CraftingPaperStyle.ApplyButton(open, CraftingPaperStyle.ButtonKind.Primary);
        open.Pressed += OpenFormationWorkspace; _body.AddChild(open);
        _body.AddChild(muted("草图不消耗材料。阵盘内件的摆放、转向与线路将在独立案面中编辑。"));
    }

    private void OpenFormationWorkspace()
    {
        if (IsInstanceValid(_formationWorkspace) && !_formationWorkspace!.IsQueuedForDeletion())
        { _formationWorkspace.Show(); _formationWorkspace.MoveToFront(); return; }
        var workspace = new FormationPaperWorkspace { Name = "FormationPaperWorkspace" };
        _formationWorkspace = workspace;
        workspace.TreeExiting += () => { if (ReferenceEquals(_formationWorkspace, workspace)) _formationWorkspace = null; };
        GetTree().Root.AddChild(workspace);
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_formationWorkspace)) _formationWorkspace!.QueueFree();
    }

    private void install_sample()
    {
        if (_owner?.entity_data is not HumanData data) return;
        var circuit = ResourceLoader.Load<FormationCircuitData>("res://resources/formations/云息周天护持阵.tres");
        if (circuit is null) return;
        if (!data.formation_circuits.Any(existing => existing?.circuit_id == circuit.circuit_id)) data.formation_circuits.Add(circuit);
        select_page("阵法");
    }

    private static IEnumerable<(string name, bool ready, string detail)> unlocks(float p) => new[]
    {
        ("感应枢、脉纹、单阵器", p >= 5, "造诣 5"), ("鉴脉门", p >= 15, "造诣 15"), ("候枢", p >= 30, "造诣 30"),
        ("节律枢", p >= 45, "造诣 45"), ("分脉枢、合脉枢", p >= 60, "造诣 60"), ("基础元件精炼", p >= 75, "造诣 75"),
        ("云息圆满阵图", p >= 90, "造诣 90")
    };
    private static VBoxContainer card(string heading, float width) { var box = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) }; box.AddThemeConstantOverride("separation", 8); var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", card_style()); panel.AddChild(box); var wrap = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; wrap.AddChild(title(heading, 18, new Color("f4f0df"))); wrap.AddChild(panel); return wrap; }
    private static Control item(string name, bool ready, string detail) { var line = new VBoxContainer(); line.AddChild(new Label { Text = ready ? name : $"未解锁 · {name}", Modulate = ready ? new Color("eaf7f2") : new Color("8b908d") }); line.AddChild(muted(detail)); return line; }
    private static Label flow(string text) => new() { Text = text, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color("f1d77a") };
    private static Label title(string text, int size, Color color) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color); return label; }
    private static Label muted(string text) { var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart }; label.AddThemeColorOverride("font_color", new Color("c9d8d2")); label.AddThemeFontSizeOverride("font_size", 14); return label; }
    private static void clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
    private static StyleBoxFlat panel_style() => new() { BgColor = new Color(.055f, .07f, .065f, .98f), BorderColor = new Color(.68f, .56f, .32f, .9f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 16, ContentMarginBottom = 16 };
    private static StyleBoxFlat card_style() => new() { BgColor = new Color(.10f, .13f, .12f, .86f), BorderColor = new Color(.42f, .55f, .49f, .7f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
}
