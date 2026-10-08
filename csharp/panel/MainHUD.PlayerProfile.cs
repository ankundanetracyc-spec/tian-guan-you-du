using Godot;

public partial class MainHUD
{
    private Control? _playerProfileOverlay;
    private VBoxContainer? _playerProfileHistory;

    public override void _EnterTree()
    {
        CallDeferred(nameof(fix_clickable_status_layout));
    }

    private void fix_clickable_status_layout()
    {
        if (GetNodeOrNull<Control>("PlayerStatus") is not { } status) return;
        status.TooltipText = "点击查看角色信息";
        status.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        if (status.GetChildCount() > 0 && status.GetChild(0) is MarginContainer margin)
            margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        set_status_descendant_mouse_filter(status);
    }

    private static void set_status_descendant_mouse_filter(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
            set_status_descendant_mouse_filter(child);
        }
    }

    public bool player_profile_visible => _playerProfileOverlay?.Visible == true;

    public void show_player_profile(Character player, string currentLocation)
    {
        if (!GodotObject.IsInstanceValid(player)) return;
        ensure_player_profile();
        if (_playerProfileOverlay is null || _playerProfileHistory is null) return;

        var data = player.entity_data as HumanData;
        set_profile_value("ProfileSubtitle", $"{player.entity_name} · 个人信息");
        set_profile_value("RealmValue", string.IsNullOrWhiteSpace(player.cultivation_realm) ? "未详" : player.cultivation_realm);
        set_profile_value("WorldStaminaValue", $"{player.current_world_stamina:0.#} / {player.max_world_stamina:0.#}");
        set_profile_value("RootValue", string.IsNullOrWhiteSpace(data?.spirit_root_name) ? "未详" : data!.spirit_root_name);
        set_profile_value("LocationValue", string.IsNullOrWhiteSpace(currentLocation) ? "位置未详" : currentLocation);
        set_profile_value("AffiliationValue", get_affiliation_text(data));
        set_profile_value("EntryRankValue", get_registered_text(data?.sect_entry_rank, "尚无入门记录"));
        set_profile_value("CurrentRankValue", get_registered_text(data?.sect_current_rank, "尚无宗门身份"));
        set_profile_value("OfficeValue", get_registered_text(data?.sect_office_title, "暂无职位记录"));
        var yuxuAttitudeStage = data?.get_faction_attitude_stage("yuxu_xianzong") ?? -1;
        var yuxuAttitude = yuxuAttitudeStage >= 0
            ? FactionAttitudeRecord.StageNames[yuxuAttitudeStage]
            : get_registered_text(data?.sect_reputation_summary, "暂无态度记录");
        set_profile_value("FactionAttitudeValue", yuxuAttitude);
        set_profile_value("RegionalRenownValue", RegionalRenownSystem.get_bai_chuan_profile_text(data));
        set_profile_value("GuestValue", get_registered_text(data?.family_guest_affiliation_id, "暂无客卿登记"));
        rebuild_contribution_history(data);
        _playerProfileOverlay.Show();
        _playerProfileOverlay.MoveToFront();
    }

    public bool close_player_profile()
    {
        if (_playerProfileOverlay?.Visible != true) return false;
        _playerProfileOverlay.Hide();
        return true;
    }

    private void ensure_player_profile()
    {
        if (_playerProfileOverlay is not null) return;

        _playerProfileOverlay = new Control { Name = "PlayerProfileOverlay", Visible = false, ZIndex = 180, MouseFilter = Control.MouseFilterEnum.Stop };
        _playerProfileOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_playerProfileOverlay);

        var shade = new ColorRect { Color = new Color(.035f, .045f, .04f, .53f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _playerProfileOverlay.AddChild(shade);

        var window = new Control { Name = "PlayerProfileWindow", MouseFilter = Control.MouseFilterEnum.Stop };
        window.AnchorLeft = .125f; window.AnchorTop = .125f; window.AnchorRight = .875f; window.AnchorBottom = .875f;
        _playerProfileOverlay.AddChild(window);

        var frame = new TextureRect
        {
            Name = "PlayerProfileFrame",
            Texture = GD.Load<Texture2D>("res://textures/ui/player_profile_frame_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        window.AddChild(frame);

        var content = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        content.AddThemeConstantOverride("margin_left", 104);
        content.AddThemeConstantOverride("margin_right", 104);
        content.AddThemeConstantOverride("margin_top", 70);
        content.AddThemeConstantOverride("margin_bottom", 76);
        window.AddChild(content);

        var layout = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 13);
        content.AddChild(layout);

        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 55), MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.AddChild(header);
        var headingBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddChild(headingBox);
        var title = make_profile_label("角色信息", 30, new Color("332d22"));
        title.Name = "ProfileTitle";
        headingBox.AddChild(title);
        var name = make_profile_label("行走百川 · 个人档案", 16, new Color("746c59"));
        name.Name = "ProfileSubtitle";
        headingBox.AddChild(name);

        var close = new Button { Text = "收起", CustomMinimumSize = new Vector2(102, 42), FocusMode = Control.FocusModeEnum.None };
        close.AddThemeFontSizeOverride("font_size", 17);
        close.AddThemeColorOverride("font_color", new Color("f4eedc"));
        close.AddThemeStyleboxOverride("normal", make_profile_button_style(false));
        close.AddThemeStyleboxOverride("hover", make_profile_button_style(true));
        close.Pressed += () => close_player_profile();
        header.AddChild(close);

        var divider = new HSeparator { Modulate = new Color(.53f, .47f, .33f, .72f), MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.AddChild(divider);

        var grid = new GridContainer { Name = "ProfileFields", Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 10);
        layout.AddChild(grid);
        add_profile_card(grid, "境界", "RealmValue");
        add_profile_card(grid, "行旅体力（与战斗精力分开）", "WorldStaminaValue");
        add_profile_card(grid, "灵根", "RootValue");
        add_profile_card(grid, "当前位置", "LocationValue");
        add_profile_card(grid, "正式归属", "AffiliationValue");
        add_profile_card(grid, "入门起始身份", "EntryRankValue");
        add_profile_card(grid, "当前宗门身份", "CurrentRankValue");
        add_profile_card(grid, "宗门职位", "OfficeValue");
        add_profile_card(grid, "玉虚仙宗态度", "FactionAttitudeValue");
        add_profile_card(grid, "百川区域声望", "RegionalRenownValue");
        add_profile_card(grid, "家族客卿", "GuestValue");

        var historyTitle = make_profile_label("宗门贡献流水", 19, new Color("514936"));
        layout.AddChild(historyTitle);
        var historyCard = new PanelContainer { CustomMinimumSize = new Vector2(0, 126), SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        historyCard.AddThemeStyleboxOverride("panel", make_profile_card_style());
        layout.AddChild(historyCard);
        var historyMargin = new MarginContainer();
        historyMargin.AddThemeConstantOverride("margin_left", 14); historyMargin.AddThemeConstantOverride("margin_right", 14);
        historyMargin.AddThemeConstantOverride("margin_top", 9); historyMargin.AddThemeConstantOverride("margin_bottom", 9);
        historyCard.AddChild(historyMargin);
        _playerProfileHistory = new VBoxContainer { Name = "ContributionHistory", MouseFilter = Control.MouseFilterEnum.Ignore };
        _playerProfileHistory.AddThemeConstantOverride("separation", 5);
        historyMargin.AddChild(_playerProfileHistory);

        var note = make_profile_label("区域声望只代表名声；逐势力态度与 NPC 私交各自独立，不会因名声自动改变。", 14, new Color("756d5b"));
        layout.AddChild(note);
    }

    private void add_profile_card(GridContainer grid, string title, string valueNodeName)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 72), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", make_profile_card_style());
        grid.AddChild(card);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14); margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8); margin.AddThemeConstantOverride("margin_bottom", 6);
        card.AddChild(margin);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        margin.AddChild(box);
        box.AddChild(make_profile_label(title, 14, new Color("817660")));
        var value = make_profile_label("—", valueNodeName == "RegionalRenownValue" ? 15 : 18, new Color("332d22"));
        value.Name = valueNodeName;
        value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(value);
    }

    private void rebuild_contribution_history(HumanData? data)
    {
        if (_playerProfileHistory is null) return;
        foreach (var child in _playerProfileHistory.GetChildren()) child.QueueFree();
        var records = data?.sect_contribution_records;
        var availableContribution = Math.Max(0L, data?.get_yuxu_contribution_total() ?? 0L);
        var officialServiceTotal = data?.get_yuxu_official_service_total() ?? 0L;
        if (records is null || records.Count == 0)
        {
            _playerProfileHistory.AddChild(make_profile_label($"可用贡献：{availableContribution}　·　公事履职累计：{officialServiceTotal}", 16, new Color("514936")));
            _playerProfileHistory.AddChild(make_profile_label("暂无贡献流水记录。履职累计只统计已核验的宗门公事奖励，不随兑换支出减少。", 14, new Color("655d4d")));
            return;
        }

        _playerProfileHistory.AddChild(make_profile_label($"可用贡献：{availableContribution}　·　公事履职累计：{officialServiceTotal}\n共 {records.Count} 条可追溯记录（履职累计不随兑换支出减少）", 15, new Color("514936")));
        for (var i = Math.Max(0, records.Count - 2); i < records.Count; i++)
        {
            var record = records[i];
            if (record is null) continue;
            var transactionKind = record.transaction_kind;
            if (string.IsNullOrWhiteSpace(transactionKind) && record.source_transaction_id.StartsWith("yuxu:official:", StringComparison.Ordinal))
                transactionKind = YuxuSectContributionRecord.KindOfficialDutyReward;
            var kindLabel = transactionKind switch
            {
                YuxuSectContributionRecord.KindOfficialDutyReward => "宗门公事奖励",
                YuxuSectContributionRecord.KindPrivateCommissionIncome => "同门私委报酬",
                YuxuSectContributionRecord.KindPrivateCommissionReserve => "私委托管",
                YuxuSectContributionRecord.KindSectHandlingFee => "宗门受理费",
                YuxuSectContributionRecord.KindRefund => "退款",
                YuxuSectContributionRecord.KindExchangeSpend => "善功堂兑换",
                _ => "贡献变动"
            };
            _playerProfileHistory.AddChild(make_profile_label($"{record.occurred_at}　{kindLabel} · {record.source_title}　{record.contribution_delta:+#;-#;0}\n成本：{record.cost_summary}　结果：{record.result_summary}", 14, new Color("655d4d")));
        }
    }

    private void set_profile_value(string nodeName, string value)
    {
        if (_playerProfileOverlay?.FindChild(nodeName, true, false) is Label label) label.Text = value;
    }

    private static string get_registered_text(string? value, string emptyText)
        => string.IsNullOrWhiteSpace(value) ? emptyText : value.Trim();

    private static string get_affiliation_text(HumanData? data)
    {
        if (string.IsNullOrWhiteSpace(data?.sect_affiliation_id)) return "暂无正式宗门归属";
        return data!.sect_affiliation_id == "yuxu_xianzong" ? "玉虚仙宗" : data.sect_affiliation_id;
    }

    private static Label make_profile_label(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat make_profile_card_style() => new()
    {
        BgColor = new Color(.91f, .89f, .81f, .79f),
        BorderColor = new Color(.49f, .54f, .44f, .54f),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7
    };

    private static StyleBoxFlat make_profile_button_style(bool hover) => new()
    {
        BgColor = hover ? new Color(.25f, .29f, .23f, .96f) : new Color(.18f, .21f, .18f, .94f),
        BorderColor = new Color(.68f, .58f, .37f, .88f),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7
    };
}
