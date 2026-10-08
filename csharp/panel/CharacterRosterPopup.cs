using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

/// <summary>全屏角色名册：场景承载视觉，脚本处理检索、分类、分页与人物页跳转。</summary>
public partial class CharacterRosterPopup : Control
{
    public event Action<GodotObject>? CharacterSelected;

    private const int PageSize = 8;
    private readonly Button[] _rows = new Button[PageSize];
    private readonly GodotObject?[] _rowCharacters = new GodotObject?[PageSize];
    private readonly List<GodotObject> _source = [];
    private readonly List<GodotObject> _filtered = [];
    private readonly System.Collections.Generic.Dictionary<string, Button> _filterButtons = [];
    private Label? _count, _title, _hint, _page;
    private LineEdit? _search;
    private Button? _close, _prev, _next;
    private Texture2D? _filterIdle, _filterSelected, _filterPlain;
    private string _filter = "all";
    private int _pageIndex;
    private bool _study, _pinned;

    public override void _Ready()
    {
        AddToGroup("map_overlay_blocker"); Hide(); ZIndex = 120; MouseFilter = MouseFilterEnum.Stop;
        _count = GetNodeOrNull<Label>("%CharCountLabel") ?? GetNodeOrNull<Label>("Header/CharCountLabel");
        _title = GetNodeOrNull<Label>("%TitleLabel") ?? GetNodeOrNull<Label>("Header/TitleLabel");
        _hint = GetNodeOrNull<Label>("%SelectionHint") ?? GetNodeOrNull<Label>("RosterPage/Content/SelectionHint");
        _close = GetNodeOrNull<Button>("%CloseBtn") ?? GetNodeOrNull<Button>("Header/CloseBtn");
        _search = GetNodeOrNull<LineEdit>("RosterPage/Content/FilterBar/Search");
        _page = GetNodeOrNull<Label>("RosterPage/Content/Footer/Page");
        _prev = GetNodeOrNull<Button>("RosterPage/Content/Footer/Prev"); _next = GetNodeOrNull<Button>("RosterPage/Content/Footer/Next");
        _filterIdle = GD.Load<Texture2D>("res://textures/ui/roster_filter_idle_v1.png");
        _filterSelected = GD.Load<Texture2D>("res://textures/ui/roster_filter_selected_v1.png");
        _filterPlain = GD.Load<Texture2D>("res://textures/ui/roster_plain_tab_v2.png");
        if (_close is not null) _close.Pressed += dismiss;
        if (_search is not null) _search.TextChanged += _ => { _pageIndex = 0; apply_filter(); };
        if (_prev is not null) _prev.Pressed += () => { _pageIndex--; populate_visible_rows(); };
        if (_next is not null) _next.Pressed += () => { _pageIndex++; populate_visible_rows(); };

        bind_filter("all", "All"); bind_filter("family", "Family"); bind_filter("cultivator", "Cultivator"); bind_filter("visitor", "Visitor");
        for (var i = 0; i < _rows.Length; i++)
        {
            var row = GetNodeOrNull<Button>($"RosterPage/Content/ListPanel/Rows/Row{i + 1}");
            if (row is null) continue;
            _rows[i] = row; var index = i; row.Pressed += () => select(_rowCharacters[index]);
        }
    }

    private void bind_filter(string id, string nodeName)
    {
        var button = GetNodeOrNull<Button>($"RosterPage/Content/FilterBar/{nodeName}");
        if (button is null) return;
        _filterButtons[id] = button;
        button.Pressed += () => { _filter = id; _pageIndex = 0; apply_filter(); update_filter_visuals(); };
    }

    public void dismiss() { if (!_pinned) { Hide(); _study = false; } }
    public void show_pinned(GArray characters, string panel_title = "本节点成员")
    {
        _pinned = true; _study = false; if (_title is not null) _title.Text = panel_title;
        if (_hint is not null) _hint.Text = "此处列出当前地点的成员。点击人物可查看档案。";
        set_source(characters); Show(); MoveToFront();
    }
    public void hide_pinned() { if (_pinned) { _pinned = false; Hide(); } }
    public bool is_pinned() => _pinned;
    public void show_study_candidates(GArray characters, string bookName)
    {
        _pinned = false; _study = true; if (_title is not null) _title.Text = "选择研读者";
        if (_hint is not null) _hint.Text = $"将《{bookName}》交由哪位族人研读？";
        set_source(characters); Show(); MoveToFront();
    }
    public void refresh(GArray characters)
    {
        if (!_study && !_pinned)
        {
            if (_title is not null) _title.Text = "角色名册";
            if (_hint is not null) _hint.Text = "青溪里人物名册 · 点击人物查看根基与功法。";
        }
        set_source(characters); Show(); MoveToFront();
    }

    private void set_source(GArray characters)
    {
        _source.Clear();
        foreach (var value in characters) if (GodotObject.IsInstanceValid(value.AsGodotObject())) _source.Add(value.AsGodotObject()!);
        _pageIndex = 0; apply_filter(); update_filter_visuals();
    }

    private void apply_filter()
    {
        var query = _search?.Text.Trim() ?? "";
        _filtered.Clear();
        foreach (var character in _source)
        {
            var name = character.Get("entity_name").AsString();
            var root = character.Get("spirit_root_name").AsString();
            var realm = character.Get("cultivation_realm").AsString();
            var isFamily = character is WorldEntity world && world.is_family;
            var isCultivator = root != "无灵根" || realm != "凡人";
            if ((_filter == "family" && !isFamily) || (_filter == "cultivator" && !isCultivator) || (_filter == "visitor" && isFamily)) continue;
            if (!string.IsNullOrEmpty(query) && !name.Contains(query) && !root.Contains(query) && !realm.Contains(query)) continue;
            _filtered.Add(character);
        }
        populate_visible_rows();
    }

    private void populate_visible_rows()
    {
        var totalPages = Math.Max(1, Mathf.CeilToInt(_filtered.Count / (float)PageSize));
        _pageIndex = Mathf.Clamp(_pageIndex, 0, totalPages - 1);
        System.Array.Fill(_rowCharacters, null);
        foreach (var row in _rows) if (row is not null) row.Hide();
        var start = _pageIndex * PageSize;
        for (var index = 0; index < PageSize && start + index < _filtered.Count; index++)
        {
            var row = _rows[index]; if (row is null) continue;
            var character = _filtered[start + index]; _rowCharacters[index] = character;
            populate_row(row, character); row.Show();
        }
        if (_count is not null) _count.Text = $"共 {_filtered.Count} 人";
        if (_page is not null) _page.Text = $"第 {_pageIndex + 1} 页 / {totalPages} 页";
        if (_prev is not null) _prev.Disabled = _pageIndex == 0;
        if (_next is not null) _next.Disabled = _pageIndex >= totalPages - 1;
    }

    private void update_filter_visuals()
    {
        foreach (var (id, button) in _filterButtons)
        {
            var active = id == _filter;
            button.AddThemeColorOverride("font_color", active ? new Color(.11f, .09f, .055f) : new Color(.19f, .16f, .11f));
            button.RemoveThemeStyleboxOverride("normal"); button.RemoveThemeStyleboxOverride("hover"); button.RemoveThemeStyleboxOverride("pressed");
            if (button.GetNodeOrNull<TextureRect>("Backdrop") is { } backdrop)
            {
                backdrop.Texture = _filterPlain ?? (active ? _filterSelected : _filterIdle);
                backdrop.Modulate = active ? new Color(1f, .94f, .73f, 1f) : new Color(.86f, .88f, .81f, .9f);
                backdrop.Show();
            }
        }
    }

    private static void populate_row(Button row, GodotObject character)
    {
        var name = character.Get("entity_name").AsString(); if (string.IsNullOrWhiteSpace(name)) name = "无名";
        var root = character.Get("spirit_root_name").AsString(); var age = character.Get("current_age").AsInt32();
        var realm = character.Get("cultivation_realm").AsString();
        row.GetNodeOrNull<Label>("Columns/Person/Name")!.Text = name;
        row.GetNodeOrNull<Label>("Columns/Person/Detail")!.Text = $"{root} · {age}岁";
        var mark = row.GetNodeOrNull<Label>("Columns/Avatar/Mark"); if (mark is not null) { mark.Text = name[..1]; mark.Show(); }
        row.GetNodeOrNull<Label>("Columns/Identity")!.Text = "李家族人\n青溪里";
        row.GetNodeOrNull<Label>("Columns/Realm")!.Text = string.IsNullOrWhiteSpace(realm) ? "凡人" : realm;
        row.GetNodeOrNull<Label>("Columns/Tags")!.Text = string.IsNullOrWhiteSpace(root) || root == "无灵根" ? "未入道" : root;
        var unit = character as Character; var stateLabel = row.GetNodeOrNull<Label>("Columns/State");
        var stateText = unit is null ? "待定" : unit.is_studying() ? "研读中" : "空闲";
        if (stateLabel is null) return;
        stateLabel.Text = stateText;
        stateLabel.Modulate = stateText switch { "研读中" => new Color(.72f,.83f,.66f), _ => Colors.White };
    }

    private void select(GodotObject? character)
    {
        if (!GodotObject.IsInstanceValid(character)) return;
        if (!_study && !_pinned) dismiss(); CharacterSelected?.Invoke(character!);
    }
}
