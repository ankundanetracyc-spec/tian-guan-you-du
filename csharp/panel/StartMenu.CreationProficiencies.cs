using Godot;
using System.Globalization;

public partial class StartMenu
{
    private CreationPage build_proficiency_page(GameStartProfile profile)
    {
        var page = new VBoxContainer { Name = "ProficiencyPage", Visible = false };
        page.AddThemeConstantOverride("separation", 16);
        var caption = creation_label("初始造诣 · 每项 0～10，默认 5，不消耗资质点", 19);
        page.AddChild(caption);
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 18);
        page.AddChild(columns);
        var martial = creation_card("武学与术法", "调节角色的初始修习基础。");
        var crafting = creation_card("百工", "调节炼器、符箓、阵法与炼丹的初始造诣。");
        foreach (var card in new[] { martial, crafting })
        {
            card.Panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            columns.AddChild(card.Panel);
        }
        var controls = new List<(string Key, HSlider Slider, LineEdit Input)>();
        var refreshing = false;
        var result = new CreationPage { Root = page };
        foreach (var (key, name) in GameStartProfile.StartingProficiencies)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 58) };
            row.AddThemeConstantOverride("separation", 12);
            var label = creation_label(name, 18);
            label.CustomMinimumSize = new Vector2(62, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(label);
            var slider = new HSlider
            {
                MinValue = 0, MaxValue = 10, Step = .1, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(140, 30)
            };
            slider.AddThemeStyleboxOverride("slider", new StyleBoxFlat { BgColor = new Color("dbdaca"), ContentMarginTop = 2, ContentMarginBottom = 2 });
            slider.AddThemeStyleboxOverride("grabber_area", new StyleBoxFlat { BgColor = CreationInk, ContentMarginTop = 2, ContentMarginBottom = 2 });
            slider.AddThemeStyleboxOverride("grabber_area_highlight", new StyleBoxFlat { BgColor = new Color("537362"), ContentMarginTop = 2, ContentMarginBottom = 2 });
            // Draw a matte ink dot, keeping the thumb clear without the default bright icon.
            var dot = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    if (new Vector2(x - 7.5f, y - 7.5f).Length() <= 6)
                        dot.SetPixel(x, y, CreationInk);
            var thumb = ImageTexture.CreateFromImage(dot);
            slider.AddThemeIconOverride("grabber", thumb);
            slider.AddThemeIconOverride("grabber_highlight", thumb);
            slider.AddThemeIconOverride("grabber_disabled", thumb);
            row.AddChild(slider);
            var input = new LineEdit
            {
                CustomMinimumSize = new Vector2(88, 38), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                Alignment = HorizontalAlignment.Center, SelectAllOnFocus = true,
                TooltipText = "输入 0～10 的造诣，可保留一位小数"
            };
            style_creation_input(input);
            row.AddChild(input);
            slider.ValueChanged += value =>
            {
                if (refreshing) return;
                profile.set_starting_proficiency(key, (float)value);
                input.Text = profile.get_starting_proficiency(key).ToString("0.#", CultureInfo.InvariantCulture);
            };
            void commit()
            {
                if (float.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    profile.set_starting_proficiency(key, value);
                result.Refresh();
            }
            input.TextSubmitted += _ => commit();
            input.FocusExited += commit;
            (controls.Count < 5 ? martial : crafting).Body.AddChild(row);
            controls.Add((key, slider, input));
        }
        result.Refresh = () =>
        {
            refreshing = true;
            foreach (var (key, slider, input) in controls)
            {
                var value = profile.get_starting_proficiency(key);
                slider.Value = value;
                input.Text = value.ToString("0.#", CultureInfo.InvariantCulture);
            }
            refreshing = false;
        };
        var reset = creation_button("全部恢复为 5");
        reset.CustomMinimumSize = new Vector2(200, 44);
        reset.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        reset.Pressed += () => { profile.reset_starting_proficiencies(); result.Refresh(); };
        page.AddChild(reset);
        result.Refresh();
        return result;
    }
}
