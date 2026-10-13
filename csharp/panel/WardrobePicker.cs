using Godot;
using Godot.Collections;
using System;

/// <summary>Try on named portrait clothing without editing facial appearance or equipment.</summary>
public partial class WardrobePicker : Control
{
    public event Action? ClothingApplied;
    private HumanData? _source;
    private HumanData? _draft;
    private string _gender = "male";
    private int _age = 18;
    private string _realm = "凡人";
    private OptionButton? _choices;
    private PortraitRenderer? _preview;
    private Label? _name;
    private readonly Array<Dictionary> _entries = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; ZAsRelative = false; ZIndex = 500;
        var dim = new ColorRect { Color = new Color(0, 0, 0, .5f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(dim); dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer(); AddChild(center); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var paper = new PanelContainer { CustomMinimumSize = new Vector2(700, 720) }; center.AddChild(paper);
        paper.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color(.91f, .88f, .80f), BorderColor = new Color(.30f, .25f, .19f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 22, ContentMarginBottom = 20 });
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 12); paper.AddChild(box);
        var title = new Label { Text = "衣着", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28); title.AddThemeColorOverride("font_color", new Color(.16f,.13f,.10f)); box.AddChild(title);
        _preview = new PortraitRenderer { Name = "WardrobePreview", CustomMinimumSize = new Vector2(360, 500), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        box.AddChild(_preview);
        _name = new Label { Name = "ClothingName", HorizontalAlignment = HorizontalAlignment.Center }; _name.AddThemeFontSizeOverride("font_size", 22); _name.AddThemeColorOverride("font_color", new Color(.16f,.13f,.10f)); box.AddChild(_name);
        _choices = new OptionButton { Name = "ClothingChoice", CustomMinimumSize = new Vector2(0, 44) };
        _choices.AddThemeFontSizeOverride("font_size", 19); _choices.ItemSelected += select_clothing; box.AddChild(_choices);
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; footer.AddThemeConstantOverride("separation", 24); box.AddChild(footer);
        var cancelButton = new Button { Name = "CancelClothing", Text = "取消", CustomMinimumSize = new Vector2(150, 44) }; cancelButton.Pressed += close_draft; footer.AddChild(cancelButton);
        var apply = new Button { Name = "ApplyClothing", Text = "换上", CustomMinimumSize = new Vector2(150, 44) }; apply.Pressed += apply_clothing; footer.AddChild(apply);
        foreach (var button in new Button[] { _choices, cancelButton, apply })
        {
            WorldHudStyle.StyleSmallButton(button, 19);
            button.AddThemeColorOverride("font_hover_pressed_color", WorldHudStyle.Ink);
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) button.AddThemeStyleboxOverride(state, WorldHudStyle.Paper());
            button.MouseEntered += () => button.Modulate = new Color(1.07f, 1.05f, 1.01f);
            button.MouseExited += () => button.Modulate = Colors.White;
        }
        Hide();
    }

    public void open_for(Character character)
    {
        if (character.entity_data is not HumanData data) return;
        _realm = character.cultivation_realm;
        open_for_data(data, character.Get("current_age").AsInt32());
    }

    public void open_for_data(HumanData data, int age = 18)
    {
        _source = data; _draft = data.Duplicate(true) as HumanData ?? new HumanData();
        _gender = data.gender == 1 ? "female" : "male"; _age = age;
        _entries.Clear(); _choices?.Clear();
        var mods = GetNode<AppearanceModCatalog>("/root/AppearanceModCatalog");
        var catalog = GetNode<PortraitCatalog>("/root/PortraitCatalog");
        var context = catalog.get_context(data, _gender, age, data.faction_name, _realm);
        var current = catalog.choose_part(context, "clothes", data.appearance_clothes_style.ToString());
        int selected = 0;
        foreach (Dictionary entry in mods.get_parts(_gender, "clothes"))
        {
            if (!mods.can_wear(entry, data)) continue;
            int index = entry["variant_index"].AsInt32();
            if (current.Count > 0 && current["variant_index"].AsInt32() == index) selected = _entries.Count;
            _entries.Add(entry); _choices?.AddItem(entry["display_name"].AsString(), index);
        }
        if (_entries.Count == 0) return;
        _choices?.Select(selected); select_clothing(selected);
        Show(); MoveToFront();
    }

    private void select_clothing(long selected)
    {
        if (_draft is null || selected < 0 || selected >= _entries.Count) return;
        var entry = _entries[(int)selected];
        _draft.appearance_clothes_style = entry["variant_index"].AsInt32();
        _name!.Text = entry["display_name"].AsString();
        refresh_preview();
    }

    private void refresh_preview()
    {
        if (_draft is null) return;
        var mods = GetNode<AppearanceModCatalog>("/root/AppearanceModCatalog");
        if (!mods.allows_color_tint(_gender, "skin")) _draft.appearance_skin_tone = -1;
        if (!mods.allows_color_tint(_gender, "hair")) _draft.appearance_hair_color = -1;
        var catalog = GetNode<PortraitCatalog>("/root/PortraitCatalog");
        _preview?.configure(_draft, catalog.get_context(_draft, _gender, _age, _draft.faction_name, _realm), catalog.get_requested_appearance(_draft));
    }

    private void apply_clothing()
    {
        if (_source is null || _draft is null || _choices is null || _choices.Selected < 0) return;
        if (!GetNode<AppearanceModCatalog>("/root/AppearanceModCatalog").can_wear(_entries[_choices.Selected], _source)) return;
        _source.appearance_clothes_style = _draft.appearance_clothes_style;
        _source.EmitChanged(); ClothingApplied?.Invoke(); close_draft();
    }

    private void close_draft() { _source = null; _draft = null; Hide(); }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel")) { close_draft(); GetViewport().SetInputAsHandled(); }
    }
}
