using Godot;
using System;

/// <summary>从首页试演交谈、切磋、交易表现；不进入战斗或执行财物交割。</summary>
public partial class NpcSocialPreviewScene : Control
{
    private Character _player = null!;
    private HumanData _playerData = null!;
    private WorldNpcData _npcTemplate = null!;
    private StoryDialoguePanel _dialogue = null!;
    private VBoxContainer _menu = null!;
    private Control _setupRoot = null!;
    private WorldNpcData? _activeNpc;
    private bool _activeNpcInitiated;
    private Character? _admissionPlayer;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildTestPeople();
        BuildPreviewMenu();
        _dialogue = GD.Load<PackedScene>("res://tscn/panel/StoryDialoguePanel.tscn").Instantiate<StoryDialoguePanel>();
        AddChild(_dialogue);
        _dialogue.VisibilityChanged += () => { if (!_dialogue.Visible && IsInstanceValid(_setupRoot)) _setupRoot.Show(); };
    }

    public override void _ExitTree()
    {
        if (_player is not null && GodotObject.IsInstanceValid(_player)) _player.Free();
        if (_admissionPlayer is not null && GodotObject.IsInstanceValid(_admissionPlayer)) _admissionPlayer.Free();
    }

    private void BuildTestPeople()
    {
        _playerData = new HumanData
        {
            entity_name = "沈照微",
            faction_name = "玉虚仙宗",
            gender = 0,
            current_age = 16,
            cultivation_realm = "云息三层",
            charisma = 68,
            social = 64,
            mood_value = 8,
            sect_affiliation_id = "yuxu_xianzong",
            sect_current_rank = "真传弟子",
            appearance_seed = 684201
        };
        _player = new Character { entity_name = _playerData.entity_name, entity_data = _playerData };
        _npcTemplate = new WorldNpcData
        {
            npc_id = "social_preview_guyuan",
            npc_name = "陆清和",
            faction_id = "yuxu_xianzong",
            formal_identity = "内门弟子",
            cultivation_realm = "云息四层",
            current_age = 19,
            gender = 1,
            appearance_seed = 930614,
            mood_value = 6
        };
        _npcTemplate.social_personality["sociability"] = "外向";
        _npcTemplate.social_personality["risk"] = "谨慎";
        _npcTemplate.social_priorities.Add("宗门");
    }

    private void BuildPreviewMenu()
    {
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/start_menu/title_menu_world_v2.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var veil = new ColorRect { Color = new Color(.025f, .045f, .04f, .48f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var center = new CenterContainer();
        _setupRoot = center;
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var paper = new PanelContainer { CustomMinimumSize = new Vector2(640, 390) };
        paper.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.91f, .87f, .76f, .97f),
            BorderColor = new Color(.25f, .21f, .15f, .9f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 30, ContentMarginRight = 30, ContentMarginTop = 24, ContentMarginBottom = 24
        });
        var menuScroll = new ScrollContainer {
            CustomMinimumSize = new Vector2(660, Mathf.Min(800, GetViewportRect().Size.Y - 48)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        center.AddChild(menuScroll);
        paper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        menuScroll.AddChild(paper);
        _menu = new VBoxContainer();
        _menu.AddThemeConstantOverride("separation", 15);
        paper.AddChild(_menu);

        var title = new Label { Text = "人物交往试演", HorizontalAlignment = HorizontalAlignment.Center };
        Style(title, 30, new Color(.12f, .20f, .17f));
        _menu.AddChild(title);
        var hint = new Label
        {
            Text = "交谈、切磋、交易仅作表现试演。不进入战斗、不交换财物，也不改写正式存档。",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        Style(hint, 17, new Color(.34f, .31f, .25f));
        _menu.AddChild(hint);
        _menu.AddChild(ThinRule());
        AddTestButton("我主动上前搭话", "查看玩家主动结识的判定过程", () => BeginInteraction(npcInitiated: false));
        AddTestButton("同门前来结识", "查看 NPC 主动来访的判定过程", () => BeginInteraction(npcInitiated: true));
        AddTestButton("切磋邀约 · 相识同门", "查看应邀过程，不进入战斗", () => BeginSparring(known: true));
        AddTestButton("切磋邀约 · 尚未相识", "查看谨慎同门的推辞", () => BeginSparring(known: false));
        AddTestButton("药材求购", "查看出售或婉拒，不交换财物", BeginPurchase);
        AddTestButton("旧事回顾", "查看聊过天、交过手、做过买卖后的重逢", BeginRecollection);
        AddTestButton("过月印象", "对照归纳前后，不推进正式时间", BeginImpression);
        AddTestButton("山门迎真", "特殊交互试演，不办理入门", BeginAdmission);
        var back = MakeButton("返回首页");
        back.Pressed += () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
        _menu.AddChild(back);
    }

    private void AddTestButton(string title, string detail, Action start)
    {
        var button = MakeButton(title + "　·　" + detail);
        button.CustomMinimumSize = new Vector2(0, 56);
        button.Pressed += start;
        _menu.AddChild(button);
    }

    private void BeginInteraction(bool npcInitiated)
    {
        _setupRoot.Hide();
        _activeNpcInitiated = npcInitiated;
        _activeNpc = _npcTemplate.Duplicate(true) as WorldNpcData ?? _npcTemplate;
        var opening = npcInitiated ? "陆清和得知你刚入真传，前来问候。" : "你在宗内遇见陆清和，准备上前搭话。";
        _dialogue.show_social_choices(_player, _activeNpc, opening,
            new (string, Action)[]
            {
                (npcInitiated ? "听听来意" : "上前搭话", () => ResolveInteraction(accepted: true)),
                (npcInitiated ? "婉言谢绝" : "作罢", () => ResolveInteraction(accepted: false))
            },
            () => ResolveInteraction(accepted: false),
            npcInitiated ? "来访" : "结识",
            npcInitiated ? "Esc 婉拒" : "Esc 作罢",
            testMode: true);
    }

    private void ResolveInteraction(bool accepted)
    {
        if (_activeNpc is null) return;
        var result = NpcSocialPrototypeRules.Evaluate(_activeNpc, _playerData, _activeNpcInitiated, accepted);
        _dialogue.show_social_result(_activeNpc, result);
    }

    private WorldNpcData FreshPreviewNpc()
    {
        _setupRoot.Hide();
        _activeNpc = (WorldNpcData)_npcTemplate.Duplicate(true);
        return _activeNpc;
    }

    private void BeginSparring(bool known)
    {
        var npc = FreshPreviewNpc();
        npc.social_priorities.Clear();
        npc.social_priorities.Add("修行");
        if (known) npc.player_social_memory["player_name"] = _playerData.entity_name;
        _dialogue.show_social_choices(_player, npc, "你向陆清和提起切磋，希望彼此印证所学。",
            new (string, Action)[] {
                ("提出切磋", () => {
                    var response = NpcSparringRules.Evaluate(npc, _playerData);
                    _dialogue.show_social_result(npc, response with {
                        Result = response.AgreesToSpar ? "对方应下切磋，双方约好点到为止。" : response.Result });
                }),
                ("暂且作罢", () => ShowPreviewCancellation(npc, "切磋"))
            }, () => ShowPreviewCancellation(npc, "切磋"), "切磋", "Esc 作罢", testMode: true);
    }

    private void BeginPurchase()
    {
        var npc = FreshPreviewNpc();
        _dialogue.show_social_choices(_player, npc,
            $"陆清和想补充随身药材，愿以灵石 {NpcPurchaseRules.TestUnitPrice} 枚求购苦苔根一份。",
            new (string, Action)[] {
                ("同意出售", () => {
                    // 只复现已定义的两步表现，不调用库存、付款、体力或月度结算接口。
                    NpcSocialRules.DecisionStep[] steps = [
                        new("purchase_supplied", "你递过药材，对方验看后收好。"),
                        new("purchase_paid", "对方依约递来灵石，双方核对妥当。") ];
                    _dialogue.show_social_result(npc, new("双方核对妥当，这笔买卖就此谈成。", "",
                        new[] { steps[0].Text, steps[1].Text }) { ActionName = "交易", DecisionSteps = steps });
                }),
                ("婉拒求购", () => ShowPreviewCancellation(npc, "交易"))
            }, () => ShowPreviewCancellation(npc, "交易"), "交易", "Esc 婉拒", testMode: true);
    }

    private void ShowPreviewCancellation(WorldNpcData npc, string action)
    {
        _dialogue.show_social_result(npc, new(action == "交易" ? "你没有应下这笔买卖，对方便暂且作罢。" : "你暂且收起了切磋之意。",
            "", []) { ActionName = action });
    }

    private void BeginRecollection()
    {
        var npc = FreshPreviewNpc();
        _dialogue.show_social_choices(_player, npc, "选择一段临时旧事，查看再次相遇时的回顾。",
            new (string, Action)[] {
                ("曾经聊过天", () => ResolveRecollection("player_social_introduce")),
                ("曾经交过手", () => ResolveRecollection("sect_spar_result")),
                ("曾经做过药材买卖", () => ResolveRecollection("purchase_completed")),
                ("返回试演菜单", () => _dialogue.close())
            }, () => _dialogue.close(), "闲谈", "Esc 返回", testMode: true);
    }

    private void ResolveRecollection(string actionId)
    {
        if (_activeNpc is not { } npc) return;
        // 只给本次临时人物放入演示证据；不会登记到正式 NPC 或存档。
        npc.player_social_memory["player_name"] = _playerData.entity_name;
        npc.record(new NpcActionRecord {
            month_index = 0, action_id = actionId, target_npc_id = "player",
            node_id = "yuxu_xianzong_core", subscene_id = "guanfu",
            player_contact_completed = actionId == "player_social_introduce",
            reason = "首页临时旧事试演" });
        var response = NpcSocialRules.Evaluate(npc, _playerData, "introduce") with { ActionName = "闲谈" };
        _dialogue.show_social_result(npc, NpcPersonalRecallRules.Attach(npc, response, 1));
    }

    private void BeginImpression()
    {
        var npc = FreshPreviewNpc();
        _dialogue.show_social_choices(_player, npc, "选择一类临时经历，对照过月归纳前后的表现。",
            new (string, Action)[] {
                ("交谈印象", () => BeginImpressionComparison("contact")),
                ("切磋印象", () => BeginImpressionComparison("spar")),
                ("买卖印象", () => BeginImpressionComparison("trade")),
                ("返回试演菜单", () => _dialogue.close())
            }, () => _dialogue.close(), "闲谈", "Esc 返回", testMode: true);
    }

    private void BeginImpressionComparison(string kind)
    {
        var npc = FreshPreviewNpc();
        _dialogue.show_social_choices(_player, npc, kind == "contact"
                ? "临时人物在两个不同月份都与你聊过。选择查看归纳前或归纳后的交谈。"
                : "临时人物本月与你完成过一次交手或药材买卖。选择查看归纳前或归纳后的交谈。",
            new (string, Action)[] {
                ("过月前", () => ResolveImpression(kind, false)),
                ("过月后", () => ResolveImpression(kind, true)),
                ("返回试演菜单", () => _dialogue.close())
            }, () => _dialogue.close(), "闲谈", "Esc 返回", testMode: true);
    }

    private void ResolveImpression(string kind, bool settled)
    {
        var npc = FreshPreviewNpc();
        npc.player_social_memory["player_name"] = _playerData.entity_name;
        if (kind == "contact")
        {
            foreach (var month in new[] { 0, 1 })
                npc.record(new NpcActionRecord { month_index = month, action_id = "player_social_introduce",
                    target_npc_id = "player", player_contact_completed = true });
        }
        else
        {
            npc.record(new NpcActionRecord { month_index = 1,
                action_id = kind == "spar" ? "sect_spar_result" : "purchase_completed", target_npc_id = "player" });
            if (kind == "spar") { npc.social_priorities.Clear(); npc.social_priorities.Add("修行"); }
            else
            {
                // 临时设定新需求，仅让正式 Offer 检查有真实依据；不实际成交。
                npc.short_term_goal = "补充药材";
                npc.resources["灵石"] = NpcPurchaseRules.TestUnitPrice;
                npc.purchase_request = new NpcPurchaseRequest { request_id = "preview-new-demand",
                    item_path = NpcPurchaseRules.HerbPaths[0], quantity = 1, unit_price = NpcPurchaseRules.TestUnitPrice };
            }
        }
        NpcPlayerImpressionRules.SettleMonth(npc, 1);
        if (settled) NpcPlayerImpressionRules.SettleMonth(npc, 2);
        var response = NpcSocialRules.Evaluate(npc, _playerData, "introduce") with { ActionName = "闲谈" };
        _dialogue.show_social_result(npc, NpcPersonalRecallRules.Attach(npc, response, settled ? 2 : 1));
    }

    private void BeginAdmission()
    {
        _setupRoot.Hide();
        if (_admissionPlayer is null)
        {
            var visitor = (HumanData)_playerData.Duplicate(true);
            visitor.sect_affiliation_id = ""; visitor.sect_current_rank = "";
            visitor.spirit_root_name = "土灵根（天灵根）";
            _admissionPlayer = new Character { entity_name = visitor.entity_name, entity_data = visitor };
        }
        var elder = new WorldNpcData { npc_id = "preview_yuxu_gate_elder", npc_name = "接引长老",
            faction_id = "yuxu_xianzong", formal_identity = "山门接引长老", cultivation_realm = "金丹圆满",
            gender = 0, current_age = 120, appearance_seed = 761230 };
        var visitorData = (HumanData)_admissionPlayer.entity_data!;
        var evidence = new System.Collections.Generic.List<string>(visitorData.get_yuxu_gate_judgment_lines());
        evidence.Add("长老察看灵机，目光微凝，随即颔首：‘土灵根（天灵根）。依宗门规制，入门即列真传。’");
        _dialogue.show_yuxu_admission_review(elder, evidence, () =>
            _dialogue.show_yuxu_admission_question(elder, hasSect =>
                _dialogue.show_yuxu_admission_result(elder, hasSect
                    ? ["长老点了点头：‘既有所属，老夫便不相邀。小友请便。’"]
                    : ["长老唤来接引弟子：‘小友既无宗门，便随他去办入门之礼。’"],
                    () => { }, player: _admissionPlayer, testMode: true),
                player: _admissionPlayer, testMode: true),
            player: _admissionPlayer, lastLineIsAptitude: true, testMode: true);
    }

    private static Button MakeButton(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 46), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Style(button, 19, new Color(.15f, .23f, .19f));
        button.AddThemeColorOverride("font_hover_color", new Color(.08f, .17f, .14f));
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = state == "hover" ? new Color(.84f, .82f, .70f) : new Color(.88f, .84f, .72f),
                BorderColor = new Color(.47f, .45f, .34f, .8f),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8
            });
        return button;
    }

    private static void Style(Control control, int size, Color color)
    {
        var font = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        if (font is not null) control.AddThemeFontOverride("font", font);
        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeColorOverride("font_color", color);
    }

    private static HSeparator ThinRule()
    {
        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(.45f, .43f, .34f, .42f), ContentMarginTop = 1, ContentMarginBottom = 1 });
        return rule;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (_dialogue?.Visible == true) _dialogue.close();
        else GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
        GetViewport().SetInputAsHandled();
    }
}
