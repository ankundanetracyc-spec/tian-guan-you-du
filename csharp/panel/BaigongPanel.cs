using Godot;
using Godot.Collections;

/// <summary>百工总入口。四个工艺页共用外置页签；首期仅阵法页可操作，其他页不提前固化其制作规则。</summary>
public partial class BaigongPanel : Control
{
    private Character? _owner;
    private VBoxContainer? _body;
    private string _page = "阵法";
    private readonly System.Collections.Generic.Dictionary<string, Button> _tabs = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; Hide(); build();
    }

    public void show_for(Character owner) { _owner = owner; Show(); select_page("阵法"); }
    public void close() => Hide();

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
        if (page == "阵法") build_formation(); else if (page == "法宝") build_forging(); else build_placeholder(page);
    }

    private void build_forging()
    {
        _body!.AddChild(title("炼器 · 剑类工坊", 27, new Color("f4f0df")));
        _body.AddChild(muted("细身剑、阔刃剑、厚脊剑：按部件投料，配置功用与方法。当前开放独立试炼，候选参数不进入正式装备。"));
        var open = new Button { Text = "进入炼器工坊 · 试炼", CustomMinimumSize = new Vector2(360, 58) };
        open.Pressed += () => { var scene = GD.Load<PackedScene>("res://tscn/scene/SwordCraftingScene.tscn").Instantiate<Control>(); scene.ZIndex = 100; AddChild(scene); };
        _body.AddChild(open);
    }

    private void build_placeholder(string page)
    {
        _body!.AddChild(title(page, 27, new Color("f4f0df"))); _body.AddChild(muted($"{page}的具体制作规则尚未定稿。\n本页已保留在百工入口中，待对应专题确认后接入，不在阵法任务中擅自补全。"));
    }

    private void build_formation()
    {
        var proficiency = _owner?.entity_data?.Get("formation_proficiency").AsSingle() ?? 0;
        _body!.AddChild(title("阵法", 27, new Color("8fe5d6")));
        _body.AddChild(muted($"阵道造诣 {proficiency:0}　·　阵脉传递信息，阵源提供能量。"));
        var columns = new HBoxContainer(); columns.SizeFlagsVertical = SizeFlags.ExpandFill; columns.AddThemeConstantOverride("separation", 12); _body.AddChild(columns);
        var library = card("阵法录与元件", 260); columns.AddChild(library);
        var manuals = _owner?.entity_data is HumanData human ? human.formation_manual_ids : [];
        library.AddChild(muted("阵法录端口"));
        library.AddChild(item("术法感应接口", proficiency >= 5 && manuals.Contains("manual_small_circulation_input"), "《小周天聚灵阵录》"));
        library.AddChild(item("护持阵器接口", proficiency >= 5 && manuals.Contains("manual_guard_output"), "《护元阵录》"));
        library.AddChild(muted("阵道元件"));
        foreach (var node in unlocks(proficiency)) library.AddChild(item(node.name, node.ready, node.detail));
        var board = card("阵盘 · 云息示范阵", 410); board.SizeFlagsHorizontal = SizeFlags.ExpandFill; columns.AddChild(board);
        board.AddChild(muted("细线为脉纹：传递触发信号。粗线为灵络：从阵源向阵器供能。"));
        board.AddChild(title("术法感应枢", 16, new Color("eaf7f2")));
        board.AddChild(flow("↓  脉纹")); board.AddChild(title("三候枢", 16, new Color("f1d77a")));
        board.AddChild(muted("每 3 次术法脉冲，放行 1 次控制信号。")); board.AddChild(flow("↓  脉纹"));
        board.AddChild(title("护持阵器", 16, new Color("8fe5d6")));
        board.AddChild(flow("阵源 ━━━ 灵络 ━━━ 护持阵器"));
        var hasManuals = _owner?.entity_data is HumanData ownerData && ownerData.formation_manual_ids.Contains("manual_small_circulation_input") && ownerData.formation_manual_ids.Contains("manual_guard_output");
        var install = new Button { Text = !hasManuals ? "需要两本阵法录" : proficiency < 15 ? "需要阵道造诣 15" : "装配此示范阵图", Disabled = _owner is null || proficiency < 15 || !hasManuals }; install.Pressed += install_sample; board.AddChild(install);
        var status = card("硬件校验", 250); columns.AddChild(status);
        status.AddChild(item("阵盘格位", true, "3 / 4")); status.AddChild(item("阵眼复杂度", true, "4 / 5"));
        status.AddChild(item("阵源", true, "4 容量 · 每行动回复 1")); status.AddChild(item("灵络 / 负荷", true, "峰值 2 / 2"));
        status.AddChild(item("运行状态", proficiency >= 15, proficiency >= 15 ? "可运行" : "需要阵道造诣 15：解锁候枢"));
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
    private static void clear(Node node) { foreach (var child in node.GetChildren()) child.QueueFree(); }
    private static StyleBoxFlat panel_style() => new() { BgColor = new Color(.055f, .07f, .065f, .98f), BorderColor = new Color(.68f, .56f, .32f, .9f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 16, ContentMarginBottom = 16 };
    private static StyleBoxFlat card_style() => new() { BgColor = new Color(.10f, .13f, .12f, .86f), BorderColor = new Color(.42f, .55f, .49f, .7f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
}
