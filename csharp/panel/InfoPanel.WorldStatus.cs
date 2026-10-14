using Godot;

public partial class InfoPanel
{
    private PanelContainer? _overviewWorldStatusSection;
    private ScrollContainer? _overviewWorldStatusScroll;
    private VBoxContainer? _overviewWorldStatusList;

    private void build_overview_world_status_section()
    {
        if (_staticOverviewPage is null) return;
        _overviewWorldStatusSection = new PanelContainer
        {
            Name = "OverviewWorldStatusSection",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(0, 182),
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _overviewWorldStatusSection.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("#eee6d1"),
            BorderColor = new Color("#9a8b6c", .78f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 14, ContentMarginBottom = 14
        });

        var box = new VBoxContainer { Name = "WorldStatusBox", MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 8);
        _overviewWorldStatusSection.AddChild(box);
        var title = new Label { Text = "状态", MouseFilter = MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", 23);
        title.AddThemeColorOverride("font_color", new Color("#4c4436"));
        box.AddChild(title);
        var divider = new HSeparator { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore };
        divider.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color("#918368", .62f) });
        box.AddChild(divider);

        _overviewWorldStatusScroll = new ScrollContainer
        {
            Name = "WorldStatusScroll",
            CustomMinimumSize = new Vector2(0, 104),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Pass
        };
        box.AddChild(_overviewWorldStatusScroll);
        _overviewWorldStatusList = new VBoxContainer { Name = "WorldStatusList", SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _overviewWorldStatusList.AddThemeConstantOverride("separation", 8);
        _overviewWorldStatusScroll.AddChild(_overviewWorldStatusList);
        _staticOverviewPage.AddChild(_overviewWorldStatusSection);
    }

    private void refresh_overview_world_status(HumanData? data, Godot.Collections.Dictionary? bottleneckSummary, long currentGameHour)
    {
        if (_overviewWorldStatusSection is null || _overviewWorldStatusList is null) return;
        if (data is null)
        {
            _overviewWorldStatusSection.Hide();
            return;
        }

        _overviewWorldStatusSection.Show();
        foreach (var child in _overviewWorldStatusList.GetChildren())
        {
            _overviewWorldStatusList.RemoveChild(child);
            child.QueueFree();
        }

        var statuses = WorldStatusRules.active_records(data.world_status_records, currentGameHour);
        var hasBottleneck = CharacterStatusPresentationRules.has_active_bottleneck(bottleneckSummary);
        if (statuses.Count == 0 && !hasBottleneck)
        {
            var empty = new Label
            {
                Text = "暂无特殊状态",
                CustomMinimumSize = new Vector2(0, 34),
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            empty.AddThemeFontSizeOverride("font_size", 18);
            empty.AddThemeColorOverride("font_color", new Color("#756c59"));
            _overviewWorldStatusList.AddChild(empty);
            return;
        }

        if (hasBottleneck)
            _overviewWorldStatusList.AddChild(build_overview_bottleneck_row(bottleneckSummary!));
        foreach (var record in statuses)
            _overviewWorldStatusList.AddChild(build_overview_world_status_row(record, currentGameHour));
    }

    private static Control build_overview_bottleneck_row(Godot.Collections.Dictionary summary)
    {
        var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("#f5eedc"),
            BorderColor = new Color("#a99a7b", .56f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 9, ContentMarginBottom = 9
        });
        var detail = new Label
        {
            Text = CharacterStatusPresentationRules.format_bottleneck_details(summary),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        detail.AddThemeFontSizeOverride("font_size", 17);
        detail.AddThemeColorOverride("font_color", new Color("#4c4436"));
        row.AddChild(detail);
        return row;
    }

    private static Control build_overview_world_status_row(WorldStatusRecord record, long currentGameHour)
    {
        var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("#f5eedc"),
            BorderColor = new Color("#a99a7b", .56f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 9, ContentMarginBottom = 9
        });
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 4);
        row.AddChild(box);

        var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeConstantOverride("separation", 14);
        box.AddChild(heading);
        var name = new Label
        {
            Text = record.stack_count > 1 ? $"{record.display_name} ×{record.stack_count}" : record.display_name,
            CustomMinimumSize = new Vector2(78, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        name.AddThemeFontSizeOverride("font_size", 20);
        name.AddThemeColorOverride("font_color", new Color("#332f25"));
        heading.AddChild(name);
        var effect = new Label
        {
            Text = string.IsNullOrWhiteSpace(record.description) ? "暂无详细说明。" : record.description.Trim(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        effect.AddThemeFontSizeOverride("font_size", 17);
        effect.AddThemeColorOverride("font_color", new Color("#4c4436"));
        heading.AddChild(effect);

        if (record.recovery_hours_required > 0)
        {
            var remaining = Math.Max(0, record.recovery_hours_required - record.recovery_hours_progress);
            AddDetail($"{WorldStatusRules.format_recovery_days(record)} · 尚需休养 {remaining / 24f:0.#} 日后痊愈");
        }
        else if (record.expires_at_total_game_hours != WorldStatusRules.PermanentExpiry)
            AddDetail($"尚余 {record.expires_at_total_game_hours - currentGameHour} 个游戏时辰");
        if (!string.IsNullOrWhiteSpace(record.source_name)) AddDetail($"缘起：{record.source_name.Trim()}");
        return row;

        void AddDetail(string text)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", 15);
            label.AddThemeColorOverride("font_color", new Color("#756c59"));
            box.AddChild(label);
        }
    }

    public void refresh_player_world_status_details(Character player, long currentGameHour)
    {
        if (!ReferenceEquals(current_card, player) || _activeNewPage != 0 || !IsVisibleInTree()) return;
        refresh_overview_world_status(player.entity_data as HumanData, player.get_cultivation_bottleneck_summary(), currentGameHour);
    }
}
