using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>Isolated real-reader validation. It never loads a player or writes user://.</summary>
public partial class ItemSpatialHoverSmoke : Control
{
    private const string Review = "res://docs/美术规范/物品悬停底部占格与范围_20261108/";
    private readonly List<string> _checks = [];
    private readonly List<object> _captures = [];
    private Control _card = null!;
    private HoverFootprintFooter _footer = null!;
    private InventoryFootprintView _inventory = null!;
    private Button _anchor = null!;
    private Vector2 _pointer;
    private string _resolution = "";

    public override void _Ready() => CallDeferred(nameof(Run));

    private void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        _checks.Add(name);
    }

    private void Move(Vector2 position)
    {
        _pointer = position;
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
    }

    private async Task Settle(int frames = 14)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Move(_pointer);
        }
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node =>
        node.FindChildren("*", "", true, false).OfType<T>();

    private static string NormalizeBody(string text) => Regex.Replace(Regex.Replace(text,
        @"\[/?(?:b|i|u|s|color|font|font_size|url|center|right|left|table|cell|p|indent|list|ol|ul|meta)(?:[=\s][^\]]*)?\]", "", RegexOptions.IgnoreCase), @"\s+", "");

    private string VisibleText() => string.Join("\n\n", Descendants<RichTextLabel>(_card).Where(body => body.IsVisibleInTree()).Select(body => body.GetParsedText()));

    private void CheckSkillDisplay(string text, string name, bool expectApproximation)
    {
        Check(text.Contains("飞斩") && !text.Contains("普通攻击") && !text.Contains("时机：") && !text.Contains("可用"),
            name + ": player text keeps the chosen skill name without common timing or available spirit");
        Check(Regex.IsMatch(text, @"灵识占用[：:]?\s*2(?!\d)"), name + ": recorded item spirit occupation remains visible");
        Check(!Regex.IsMatch(text, @"\d+\.\d{3,}"), name + ": formula values never exceed two decimal places");
        Check(!Regex.IsMatch(text, @"\d+\.\d*0(?!\d)"), name + ": decimal display omits trailing zeros");
        if (expectApproximation) Check(text.Contains("≈"), name + ": shortened formula clearly marks approximation");
    }

    private async Task HoverDelay()
    {
        await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        await Settle(6);
    }

    private async Task Show(HoverInfoManager.HoverInfoContent content, string name, int diagrams, bool right = false, bool capture = false)
    {
        HoverInfoManager.HideAll();
        var viewport = GetViewport().GetVisibleRect().Size;
        _anchor.Position = new(right ? viewport.X - 168 : 16, viewport.Y - 72);
        _anchor.Text = content.Title;
        Move(_anchor.GetGlobalTransformWithCanvas() * (_anchor.Size / 2));
        HoverInfoManager.SetInfo(_anchor, content, true);
        await Settle();
        var bounds = _card.GetGlobalRect();
        if (bounds.End.X > viewport.X + 1 || bounds.End.Y > viewport.Y + 1)
        {
            GD.Print($"SPATIAL_LAYOUT_DIAGNOSTIC: {name}; card={bounds}; footer={_footer.GetGlobalRect()} min={_footer.GetCombinedMinimumSize()} computed={_footer.ContentHeight}");
            foreach (var child in _footer.GetParent().GetChildren().OfType<Control>().Where(child => child.Visible))
                GD.Print($"SPATIAL_COLUMN_CHILD: {child.GetType().Name}; bounds={child.GetGlobalRect()}; min={child.GetCombinedMinimumSize()}; custom={child.CustomMinimumSize}");
            foreach (var rich in Descendants<RichTextLabel>(_card)) GD.Print($"SPATIAL_BODY: size={rich.Size}; height={rich.GetContentHeight()}; font={rich.GetThemeFontSize("normal_font_size")}");
        }
        Check(_card.Visible && bounds.Position.X >= 0 && bounds.Position.Y >= 0
            && bounds.End.X <= viewport.X + 1 && bounds.End.Y <= viewport.Y + 1,
            name + ": full card fits viewport " + bounds + " / " + viewport);
        Check(_footer.GetIndex() == _footer.GetParent().GetChildCount() - 1,
            name + ": diagram footer is last after text and rules");
        var grids = Descendants<InventoryFootprintView>(_footer).Where(grid => grid.IsVisibleInTree()).ToArray();
        var headerGrid = (InventoryFootprintView)_card.FindChild("HeaderInventoryFootprint", true, false);
        Check(diagrams < 0 || grids.Length + (headerGrid.IsVisibleInTree() ? 1 : 0) == diagrams,
            name + ": only configured diagram roles shown across header and footer");
        if (content.Icon is not null)
        {
            var icon = Descendants<TextureRect>(_card).Single(rect => rect.IsVisibleInTree() && rect.Texture == content.Icon);
            Check(icon.Size.IsEqualApprox(new Vector2(content.IconSize, content.IconSize)), name + ": actual icon box keeps its declared square size");
        }
        if (content.InventoryFootprintBelowTitle)
        {
            if (content.Footprint.HasValue || content.GridDiagrams?.Any(diagram => diagram.Kind == HoverGridKind.Inventory) == true)
            {
                var title = Descendants<Label>(_card).Single(label => label.Text == content.Title);
                Check(headerGrid.IsVisibleInTree() && headerGrid.GetGlobalRect().Position.Y >= title.GetGlobalRect().End.Y,
                    name + ": inventory diagram is below the title");
                Check(Descendants<Label>(headerGrid.GetParent()).Count(label => label.IsVisibleInTree()) == 1,
                    name + ": title inventory shape has no side labels or dimensions");
                Check(headerGrid.DisplayCellSize > 16f && Mathf.Max(headerGrid.Size.X, headerGrid.Size.Y) <= 121f,
                    name + ": inventory cells are enlarged while longest shape side stays within 120 pixels");
            }
            else Check(!headerGrid.Visible, name + ": battle-only content has no invented inventory header");
            Check(grids.All(grid => grid.Diagram?.Kind == HoverGridKind.Combat), name + ": footer contains only battle occupancy");
            Check(_footer.Visible == (content.GridDiagrams?.Any(diagram => diagram.Kind == HoverGridKind.Combat) == true),
                name + ": items without battle occupancy have no footer space");
            var footerLabels = Descendants<Label>(_footer).Where(label => label.IsVisibleInTree()).ToArray();
            if (_footer.Visible)
            {
                var caption = footerLabels.Single(label => label.Text == "战斗占格：");
                Check(caption.GetThemeFontSize("font_size") == 18 && footerLabels.All(label => label == caption || label.Text == "未定义"),
                    name + ": battle row has one eighteen-pixel caption and no size or cell-count side notes");
                var divider = _footer.GetNode<ColorRect>("BattleFootprintDivider");
                Check(divider.IsVisibleInTree() && Mathf.IsEqualApprox(divider.Size.Y, 1f), name + ": battle area begins with one-pixel divider");
                Check(divider.GetGlobalRect().Position.Y >= Descendants<ScrollContainer>(_card).Single().GetGlobalRect().End.Y,
                    name + ": battle divider follows the complete body");
                Check(grids.All(grid => grid.DisplayCellSize > 16f), name + ": battle cells are larger than the old miniature");
            }
            else Check(footerLabels.Length == 0, name + ": absent combat layout has no labels or empty bottom band");
        }
        else Check(!headerGrid.Visible, name + ": existing equipment layout keeps inventory outside the title");
        Check(Descendants<Control>(_footer).All(control => control.MouseFilter == MouseFilterEnum.Ignore)
            && _footer.MouseFilter == MouseFilterEnum.Ignore && headerGrid.MouseFilter == MouseFilterEnum.Ignore,
            name + ": all header and footer grid controls ignore mouse");
        Check(Descendants<ScrollContainer>(_card).All(scroll => scroll.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled
            && scroll.HorizontalScrollMode == ScrollContainer.ScrollMode.Disabled), name + ": no internal scrolling");
        Check(grids.All(grid => grid.GetGlobalRect().Position.Y >= _footer.GetGlobalRect().Position.Y
            && grid.GetGlobalRect().End.Y <= _card.GetGlobalRect().End.Y + 1), name + ": grids remain inside footer");
        Check(_card.FindChild("InventoryFootprint", true, false) == _inventory, name + ": inventory view identity persists");
        var visibleBodies = Descendants<RichTextLabel>(_card).Where(body => body.IsVisibleInTree()).ToArray();
        Check(NormalizeBody(string.Join("\n\n", visibleBodies.Select(body => body.Text))) == NormalizeBody(PlayerInfoText.Display(content.Body)),
            name + ": visible columns retain complete body exactly once in original order");
        Check(visibleBodies.All(body => !body.ScrollActive), name + ": every text column remains unscrollable");
        if (capture && DisplayServer.GetName() != "headless") await Capture(name);
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var rect = _card.GetGlobalRect();
        var cropRect = new Rect2I((Vector2I)rect.Position.Floor(), (Vector2I)rect.Size.Ceil());
        cropRect = cropRect.Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
        using var crop = image.GetRegion(cropRect);
        var stem = _resolution + "_" + name;
        Check(image.SavePng(Review + stem + "_full.png") == Error.Ok, name + ": save full render");
        Check(crop.SavePng(Review + stem + "_card.png") == Error.Ok, name + ": save card render");
        var headerGrid = (InventoryFootprintView)_card.FindChild("HeaderInventoryFootprint", true, false);
        var visibleIcon = Descendants<TextureRect>(_card).FirstOrDefault(rect => rect.IsVisibleInTree());
        _captures.Add(new { name, full = stem + "_full.png", card = stem + "_card.png", x = rect.Position.X, y = rect.Position.Y,
            width = rect.Size.X, height = rect.Size.Y, viewport_width = image.GetWidth(), viewport_height = image.GetHeight(),
            icon_size = visibleIcon?.Size.ToString(), header_inventory_visible = headerGrid.IsVisibleInTree(),
            header_inventory_grid = headerGrid.GridDimensions.ToString(), header_inventory_cells = headerGrid.OccupiedCellCount,
            header_inventory_cell_pixels = headerGrid.DisplayCellSize,
            visible_text_columns = Descendants<RichTextLabel>(_card).Count(body => body.IsVisibleInTree()),
            body_font_sizes = Descendants<RichTextLabel>(_card).Where(body => body.IsVisibleInTree()).Select(body => body.GetThemeFontSize("normal_font_size")).ToArray(),
            footer_visible_graphs = Descendants<InventoryFootprintView>(_footer).Count(view => view.IsVisibleInTree()),
            footer_graphs = Descendants<InventoryFootprintView>(_footer).Where(view => view.IsVisibleInTree()).Select(view => new {
                title = view.Diagram?.Title, grid = view.GridDimensions.ToString(), y = view.GetGlobalRect().Position.Y,
                height = view.GetGlobalRect().Size.Y }).ToArray(), footer_height = _footer.ContentHeight });
    }

    private HoverInfoManager.HoverInfoContent NewCard(SectArtifactOfferData offer, Character? user)
    {
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://docs/plans/物品与装备/清和剑属性池修订实算结果_20261108.json"));
        var root = json.RootElement;
        int Effect(string atom) => root.GetProperty("normal_effects").EnumerateArray().First(effect => effect.GetProperty("atom").GetString() == atom)
            .GetProperty("value").GetInt32();
        var critical = Effect("critical"); var precision = Effect("precision"); var defuse = Effect("defuse_flat");
        Check(critical == 11 && precision == 6 && defuse == 3, "new card reads archived fixed v2 item values");
        var profile = GD.Load<SwordControlPreviewData>("res://resources/items_layout/qinghe_control_preview.tres");
        var attack = root.GetProperty("default_attack_parameters");
        var stability = root.GetProperty("stability_parameters");
        Check(profile.IsValid && profile.attack_base == attack.GetProperty("base").GetDouble()
            && profile.attack_soul == attack.GetProperty("soul_coefficient").GetDouble()
            && profile.attack_sword == attack.GetProperty("sword_proficiency_coefficient").GetDouble()
            && profile.stable_base == stability.GetProperty("base").GetDouble()
            && profile.stable_soul == stability.GetProperty("soul_coefficient").GetDouble()
            && profile.stable_sword == stability.GetProperty("sword_proficiency_coefficient").GetDouble()
            && profile.stable_occupation == stability.GetProperty("this_item_spirit_occupation_coefficient").GetDouble()
            && profile.spirit_required == root.GetProperty("spirit_occupation").GetInt32(), "preview uses exact recorded per-item parameter profile");
        var attackSkill = root.GetProperty("skills")[0];
        Check(profile.skill_name == "飞斩" && profile.skill_name == attackSkill.GetProperty("title").GetString()
            && attackSkill.GetProperty("skill_id").GetString() == "weapon.sword.ordinary_attack"
            && profile.energy_cost == attackSkill.GetProperty("energy_cost").GetInt32()
            && profile.mana_cost == attackSkill.GetProperty("mana_cost").GetInt32()
            && !string.IsNullOrWhiteSpace(profile.target_text) && string.IsNullOrWhiteSpace(profile.timing_text)
            && attackSkill.GetProperty("timing").GetString() == "once_on_first_body_scan_each_common_round",
            "skill display name is Feizhan while stable identity and actual timing remain unchanged");
        var info = EquipmentItemPresentation.Build("清和剑", GD.Load<Texture2D>(offer.icon_path), "法器", "剑（修长薄脊剑）",
            benefits: $"会心＋{critical}\n精准＋{precision}",
            binding: $"适用范围：格剑。\n应对物理攻击时，额外获得化解{defuse}层。",
            skills: SwordControlPreview.DescribeSkillForCharacter(profile, user),
            bonds: "惊鸿2、抱朴3", sharpness: root.GetProperty("sharpness").GetProperty("value").GetInt32().ToString());
        CheckSkillDisplay(info.Body, "new card player text", user is not null);
        return ItemLayoutPresentation.Attach(info with { Footprint = offer.get_inventory_size() }, offer.layout_preview);
    }

    private async void Run()
    {
        var originalMode = PlayerInfoText.Mode;
        try
        {
            var width = 1280; var height = 720;
            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                if (argument.StartsWith("--qa-width=")) width = int.Parse(argument[11..], CultureInfo.InvariantCulture);
                if (argument.StartsWith("--qa-height=")) height = int.Parse(argument[12..], CultureInfo.InvariantCulture);
            }
            _resolution = width + "x" + height;
            GetWindow().ContentScaleSize = new(width, height); GetWindow().Size = new(width, height);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            Theme = QuietRoomStyle.MakeTheme();
            var background = new ColorRect { Color = new("e5ddc7"), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var heading = new Label { Text = "清和剑新版悬停布局 · 独立展示预览", Position = new(32, 24), MouseFilter = MouseFilterEnum.Ignore };
            heading.AddThemeFontSizeOverride("font_size", 26); heading.AddThemeColorOverride("font_color", new("4b5142")); AddChild(heading);
            var note = new Label { Text = "原生悬停控件／新版数值只用于布局展示，未写入人物战斗。", Position = new(34, 65), MouseFilter = MouseFilterEnum.Ignore };
            note.AddThemeFontSizeOverride("font_size", 17); note.AddThemeColorOverride("font_color", new("74705e")); AddChild(note);
            _anchor = new Button { Text = "清和剑", Size = new(150, 54) }; AddChild(_anchor);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Review));
            _card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            _footer = Descendants<HoverFootprintFooter>(_card).Single();
            _inventory = _card.FindChild("InventoryFootprint", true, false) as InventoryFootprintView
                ?? throw new InvalidOperationException("Inventory view must exist before first SetInfo");
            var offer = GD.Load<SectArtifactOfferData>("res://resources/entities/equipment/清和剑.tres");
            using var actorA = new Character { entity_data = new HumanData { unique_id = "spatial-preview-actor-a", entity_name = "预览人物甲",
                cultivation_realm = "云息一层", soul = 10, sword_proficiency = 10, max_spirit_perception = 3 } };
            using var actorB = new Character { entity_data = new HumanData { unique_id = "spatial-preview-actor-b", entity_name = "预览人物乙",
                cultivation_realm = "云息一层", soul = 20, sword_proficiency = 20, max_spirit_perception = 6, used_spirit_perception = 4 } };
            actorB.SetMeta("artifact_battle_clone", true);
            var profile = GD.Load<SwordControlPreviewData>("res://resources/items_layout/qinghe_control_preview.tres");
            var contextA = SwordControlPreview.FromCharacter(actorA);
            var contextB = SwordControlPreview.FromCharacter(actorB);
            Check(contextA is { Soul: 10, Sword: 10, SpiritCapacity: 3, UsedSpirit: 0 }
                && contextB is { Soul: 20, Sword: 20, SpiritCapacity: 6, UsedSpirit: 4 }, "preview contexts read actual independent character instances");
            var resultA = SwordControlPreview.Evaluate(profile, contextA);
            var resultB = SwordControlPreview.Evaluate(profile, contextB);
            Check(resultA is { RawPhysicalDamage: 9, StabilityMaximum: 28, SpiritRequired: 2, SpiritAvailable: 3 },
                "character A uses its own soul sword and available spirit");
            Check(resultB is { RawPhysicalDamage: 15, StabilityMaximum: 39, SpiritRequired: 2, SpiritAvailable: 2 },
                "character B changes result and preserves half-up damage floor stability");
            Check(SwordControlPreview.Evaluate(profile, null) is null && SwordControlPreview.Describe(profile, null).Contains("待选择角色"),
                "missing character never falls back to an invented ten-ten actor");
            Check(SwordControlPreview.Evaluate(new SwordControlPreviewData(), contextA) is null
                && SwordControlPreview.Describe(new SwordControlPreviewData(), contextA).Contains("未记录"), "missing per-item coefficients stay explicitly unrecorded");
            var missingCost = (SwordControlPreviewData)profile.Duplicate(true); missingCost.energy_cost = -1;
            Check(SwordControlPreview.DescribeSkillForCharacter(missingCost, actorA).Contains("体力未定"),
                "missing skill cost is not displayed as a free action");
            var specialTiming = (SwordControlPreviewData)profile.Duplicate(true); specialTiming.timing_text = "敌方攻击前";
            Check(SwordControlPreview.DescribeSkillForCharacter(specialTiming, actorA).Contains("时机：敌方攻击前"),
                "declared special timing remains visible without restoring common timing to Qinghe");
            var layout = offer.layout_preview ?? throw new InvalidOperationException("Qinghe needs explicit layout metadata");
            Check(layout.validate_definition().Count == 0, "real Qinghe metadata validates");
            var formal = StoragePanel.ItemHover(offer, user: actorA);
            var otherActorHover = StoragePanel.ItemHover(offer, user: actorB);
            var noActorHover = StoragePanel.ItemHover(offer);
            Check(formal.Body.Contains("预估物理伤害9") && formal.Body.Contains("稳定28")
                && otherActorHover.Body.Contains("预估物理伤害15") && otherActorHover.Body.Contains("稳定39"),
                "formal storage item hover forwards current character context");
            Check(otherActorHover.Body.Contains("神魂[20]") && otherActorHover.Body.Contains("剑道造诣[20]")
                && Regex.IsMatch(otherActorHover.Body, @"灵识占用[：:]?\s*2(?!\d)") && !otherActorHover.Body.Contains("可用"),
                "formal dynamic formulas substitute actual actor while item spirit occupation remains concise");
            CheckSkillDisplay(formal.Body, "formal actor A", true);
            CheckSkillDisplay(otherActorHover.Body, "formal actor B", true);
            CheckSkillDisplay(noActorHover.Body, "formal without actor", false);
            Check(noActorHover.Body.Contains("待选择角色") && !noActorHover.Body.Contains("预估物理伤害9")
                && !noActorHover.Body.Contains("稳定28"), "formal context-free hover does not forge numerical attack or stability");
            actorA.SetMeta("soul", 20f); actorA.SetMeta("sword_proficiency", 30f); actorA.SetMeta("forged_physical_bonus", .5f);
            var modified = StoragePanel.ItemHover(offer, user: actorA);
            Check(SwordControlPreview.FromCharacter(actorA) is { Soul: 20, Sword: 30 }
                && modified.Body.Contains("神魂[20]") && modified.Body.Contains("剑道造诣[30]"),
                "current metadata stat modifiers feed preview rather than unmodified stored attributes");
            Check(modified.Body.Contains("预估物理伤害27") && modified.Body.Contains("稳定42")
                && modified.Body.Contains("自身当前倍率[1.5]"), "current self physical bonus applies after rounded per-item raw damage");
            CheckSkillDisplay(modified.Body, "formal current self modifier", true);
            actorA.SetMeta("soul", 12.3456f); actorA.SetMeta("sword_proficiency", 7.89123f); actorA.SetMeta("forged_physical_bonus", .12345f);
            var fractionalText = SwordControlPreview.DescribeSkillForCharacter(profile, actorA);
            CheckSkillDisplay(fractionalText, "fractional current stats and multiplier", true);
            Check(fractionalText.Contains("神魂[12.35]") && fractionalText.Contains("剑道造诣[7.89]")
                && fractionalText.Contains("自身当前倍率[1.12]"), "current attribute substitution and own multiplier also use at most two decimals");
            actorA.RemoveMeta("soul"); actorA.RemoveMeta("sword_proficiency"); actorA.RemoveMeta("forged_physical_bonus");
            Check(formal.Footprint == new Vector2I(2, 6) && formal.GridDiagrams?.Count == 2, "real item hover exposes separate inventory board and range metadata");
            Check(formal.GridDiagrams?.Single(diagram => diagram.Kind == HoverGridKind.Range).Cells.Count == 3,
                "real item hover keeps explicit three-cell coverage in structured diagram");
            Check(!formal.Body.Contains("锋锐范围："), "main body does not repeat range already shown in diagram");
            for (var turn = 0; turn < 4; turn++)
            {
                var diagrams = ItemLayoutPresentation.Diagrams(layout, turn);
                var board = diagrams.Single(diagram => diagram.Kind == HoverGridKind.Combat);
                var range = diagrams.Single(diagram => diagram.Kind == HoverGridKind.Range);
                var direction = ItemLayoutPresentation.Rotate(Vector2I.Right, turn);
                Check(board.Cells.Count == 2 && board.Cells.Contains(Vector2I.Zero) && board.Cells.Contains(direction), "rotated body cells " + turn);
                Check(range.Origin == direction && range.Direction == direction && range.Cells.Count == 3, "rotated sword-tip anchor and direction " + turn);
                Check(Enumerable.Range(1, 3).All(step => range.Cells.Contains(direction + direction * step))
                    && !range.Cells.Contains(range.Origin!.Value), "three covered cells remain beyond tip " + turn);
            }
            var artifact = offer.CreateArtifact(); var rawSnapshot = artifact.forged_snapshot_json;
            Check(artifact.layout_preview is not null && artifact.layout_preview.GetInstanceId() != layout.GetInstanceId(), "new artifact owns copied display metadata");
            Check(EquipmentItemPresentation.Hover(artifact).GridDiagrams?.Count == 2, "created unique artifact retains new diagrams");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://tmp/"));
            var savePath = "res://tmp/item_spatial_hover_roundtrip_" + _resolution + ".tres";
            Check(ResourceSaver.Save(artifact, savePath) == Error.Ok, "isolated metadata resource save");
            var loaded = ResourceLoader.Load<ArtifactInstanceData>(savePath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
            Check(loaded.layout_preview?.validate_definition().Count == 0 && loaded.forged_snapshot_json == rawSnapshot
                && EquipmentItemPresentation.Hover(loaded).GridDiagrams?.Single(diagram => diagram.Kind == HoverGridKind.Range).Cells.Count == 3,
                "resource roundtrip keeps authored geometry and original forging snapshot");
            var old = new ArtifactInstanceData { instance_id = artifact.instance_id, artifact_name = artifact.artifact_name, forged_snapshot_json = rawSnapshot };
            Check(EquipmentItemPresentation.Hover(old).GridDiagrams is null, "old instance never inferred from Qinghe display name");
            var manifest = Json.ParseString(Godot.FileAccess.GetFileAsString(InventoryIconDensityPack.Manifest)).AsGodotArray();
            var items = manifest.Select(value => GD.Load<ItemData>("res://" + value.AsGodotDictionary()["path"].AsString())).ToArray();
            var material = items.First(MaterialItemPresentation.IsMaterial);
            var pill = items.OfType<PillItemData>().First(item => BattleSupplyRules.Load(item.ResourcePath) is not null);
            var breakthrough = GD.Load<ItemData>(Character.YunxiThreeToFourPillPath);
            Check(StoragePanel.ItemHover(material).GridDiagrams is null, "ordinary material has no invented battle geometry");
            Check(StoragePanel.ItemHover(pill).GridDiagrams?.Single().Kind == HoverGridKind.Combat, "eligible pill reuses actual combat footprint");
            Check(StoragePanel.ItemHover(breakthrough).GridDiagrams is null, "world breakthrough pill has no invented battle slot");
            var malformedPill = (PillItemData)pill.Duplicate(true);
            var priorErrors = malformedPill.validate_definition().ToArray(); var priorEffect = malformedPill.EffectText;
            malformedPill.layout_preview = new ItemLayoutPreviewData { definition_key = "" };
            malformedPill.control_preview = new SwordControlPreviewData();
            Check(malformedPill.validate_definition().SequenceEqual(priorErrors) && malformedPill.EffectText == priorEffect,
                "malformed display metadata never changes real pill eligibility or effect");
            note.Text = $"预览人物甲：神魂{actorA.soul}、剑道{((HumanData)actorA.entity_data!).sword_proficiency}、灵识上限{actorA.max_spirit_perception}。费用与时序仍为候选。";
            await Show(formal, "正式清和剑", 2, capture: true);
            var qingheHeader = (InventoryFootprintView)_card.FindChild("HeaderInventoryFootprint", true, false);
            Check(formal.InventoryFootprintBelowTitle && qingheHeader.Footprint == new Vector2I(2, 6) && qingheHeader.OccupiedCellCount == 12,
                "equipment uses the same title-underneath twelve-cell inventory shape");
            Check(Mathf.IsEqualApprox(qingheHeader.DisplayCellSize, 20f) && qingheHeader.Size.IsEqualApprox(new Vector2(40, 120)),
                "Qinghe inventory retains two-by-six shape at twenty-pixel cells and a 120-pixel long edge");
            Check(!_inventory.Visible && _inventory.Diagram is null && !_footer.GetNode<Control>("InventoryRow").Visible,
                "equipment inventory shape is not repeated at the bottom");
            var composite = _footer.GetNode<InventoryFootprintView>("BattleRow/CombatFootprint");
            Check(composite.OccupiedCellCount == 2 && composite.RangeCellCount == 3 && composite.Overlays.Count == 1,
                "same combat picture contains two body cells and three coverage cells");
            Check(composite.BaseGridDimensions == new Vector2I(2, 1) && composite.GridDimensions == new Vector2I(5, 1)
                && composite.Overlays.Single().Origin == new Vector2I(1, 0), "combined five-cell diagram keeps tip on second body cell");
            Check(_footer.GetNode<Control>("BattleRow").IsVisibleInTree()
                && Descendants<InventoryFootprintView>(_footer).Count(view => view.IsVisibleInTree()) == 1,
                "equipment bottom contains exactly one combined combat and coverage picture");
            Check(!Descendants<PanelContainer>(_footer).Any(), "compact footer contains no separate framed diagram cards");
            var draft = NewCard(offer, actorA);
            await Show(draft, "新版清和剑", 2, right: true, capture: true);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false); await Settle();
            Check(VisibleText().Contains("0.2") && !VisibleText().Contains("0.2045") && VisibleText().Contains("≈"),
                "detailed hover abbreviates four-place coefficients while retaining the actual result");
            CheckSkillDisplay(VisibleText(), "visible detailed actor A", true);
            Check(_footer.IsVisibleInTree(), "detail-mode refresh keeps all footer diagrams");
            await Show(draft, "新版清和剑_详细", 2, capture: true);
            note.Text = "预览人物乙：神魂20、剑道20、灵识上限6、已占用4。费用与时序仍为候选。";
            await Show(NewCard(offer, actorB), "新版清和剑_人物乙", 2, right: true, capture: true);
            note.Text = "未选择角色时保留真实器物参数，不补入示例人物数值。";
            await Show(NewCard(offer, null), "新版清和剑_无角色", 2, capture: true);
            await Show(EquipmentItemPresentation.Hover(old), "旧器物缺战斗布局", 1, right: true);
            Check(!_footer.Visible && ((InventoryFootprintView)_card.FindChild("HeaderInventoryFootprint", true, false)).OccupiedCellCount == 12,
                "old artifact without combat layout keeps its real inventory header and no footer band");
            note.Text = "原生悬停控件／独立人物实例数据；数值未写入正式战斗。";
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            await Show(StoragePanel.ItemHover(material), "材料", 1, right: true, capture: true);
            await Show(StoragePanel.ItemHover(pill), "丹药", 2, capture: true);
            await Show(StoragePanel.ItemHover(breakthrough), "突破丹", 1, right: true);
            var sparse = new HoverInfoManager.HoverInfoContent("异形占格核对", "效果：仅验证已定义的三格形状。", ShowRules: false,
                GridDiagrams: [new("战斗占格", HoverGridKind.Combat, [new(0, 0), new(1, 0), new(0, 1)])], InventoryFootprintBelowTitle: true);
            await Show(sparse, "异形三格", 1, right: true, capture: true);
            Check(Descendants<InventoryFootprintView>(_footer).Single(view => view.IsVisibleInTree()).OccupiedCellCount == 3,
                "sparse L footprint never fills bounding-box corner");
            Check(_inventory.Footprint is null && !_inventory.Visible, "switching to non-inventory diagram clears legacy inventory grid");
            var unknown = ItemLayoutPresentation.Attach(new("未定义占格核对", "效果：仅核对明确缺失的数据。", ShowRules: false, Footprint: new Vector2I(2, 2), InventoryFootprintBelowTitle: true),
                new ItemLayoutPreviewData { definition_key = "fixture.unknown_layout", board_cells = [] });
            await Show(unknown, "未定义占格", -1, right: true);
            Check(Descendants<Label>(_footer).Any(label => label.IsVisibleInTree() && label.Text.Contains("未定义")),
                "explicit empty board metadata stays visibly unknown");
            Check(!Descendants<Label>(_footer).Any(label => label.IsVisibleInTree() && (label.Text.Contains("0×0") || label.Text.Contains("0格") || label.Text.Contains("0 格"))),
                "unknown footprint is never displayed as zero cells");
            HoverInfoManager.HideAll();
            Character currentActor = actorA;
            HoverInfoManager.SetProvider(_anchor, () => StoragePanel.ItemHover(offer, user: currentActor));
            Move(_anchor.GetGlobalTransformWithCanvas() * (_anchor.Size / 2)); await HoverDelay();
            Check(_card.Visible && VisibleText().Contains("预估物理伤害9"), "provider opens with the actual current actor");
            Move(new Vector2(1, 1)); await HoverDelay();
            currentActor = actorB;
            Move(_anchor.GetGlobalTransformWithCanvas() * (_anchor.Size / 2)); await HoverDelay();
            Check(_card.Visible && VisibleText().Contains("预估物理伤害15") && VisibleText().Contains("稳定39"),
                "reopening hover rereads actor instead of freezing list-build values");
            currentActor = actorA;
            actorA.SetMeta("forged_physical_bonus", .5f);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false); await Settle();
            Check(_card.Visible && VisibleText().Contains("预估物理伤害14") && VisibleText().Contains("自身当前倍率[1.5]"),
                "mode change rereads current actor and current self damage modifiers");
            actorA.RemoveMeta("forged_physical_bonus");
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false); await Settle();
            var equipmentRules = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("RulesReader");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.F1, Pressed = true }, true); await Settle(3);
            Check(equipmentRules.Visible && Descendants<RichTextLabel>(equipmentRules).Any(body => body.GetParsedText().Contains("手持或御使器物都可获得其装备效果")
                && body.GetParsedText().Contains("化法须手持") && body.GetParsedText().Contains("不自动改用其他技能")),
                "separate equipment explanation contains shared mode transformation and no-fallback rules");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true); await Settle(3);
            var rules = new HoverInfoManager.HoverInfoContent("规则入口核对", "效果：获得2点。计算公式：1＋1＝2。", ShowRules: true, Footprint: Vector2I.One, InventoryFootprintBelowTitle: true);
            await Show(rules, "规则入口", 1);
            var rulesOverlay = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("RulesReader");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.F1, Pressed = true }, true); await Settle(3);
            Check(rulesOverlay.Visible, "F1 rule reader still opens above item footer");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true); await Settle(3);
            Check(!rulesOverlay.Visible, "escape still closes rule reader");
            await Show(StoragePanel.ItemHover(material), "材料无规则入口", 1);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.F1, Pressed = true }, true); await Settle(3);
            Check(!rulesOverlay.Visible, "materials retain no F1 rule entry");
            HoverInfoManager.SetInfo(_anchor, new("状态", "效果：非物品悬停。", ShowRules: false), true); await Settle();
            Check(!_footer.Visible && _inventory.Footprint is null && !_inventory.Visible, "non-item hover clears and hides all grid roles");
            Check(!_card.FindChild("HeaderInventoryFootprint", true, false).Get("visible").AsBool(), "non-item hover clears the title inventory grid");
            Check(Descendants<InventoryFootprintView>(_footer).All(view => view.Diagram is null && view.OccupiedCellCount == 0
                && view.Overlays.Count == 0 && view.RangeCellCount == 0),
                "non-item hover clears every previously displayed role data");
            Check(Descendants<RichTextLabel>(_card).Count() == 1, "non-item hover restores one actual text node for existing readers");
            Check(artifact.forged_snapshot_json == rawSnapshot, "hover never rewrites original numeric snapshot");
            Check(actorA.soul == 10 && ((HumanData)actorA.entity_data!).sword_proficiency == 10 && actorA.max_spirit_perception == 3
                && actorB.soul == 20 && ((HumanData)actorB.entity_data!).sword_proficiency == 20 && actorB.used_spirit_perception == 4,
                "dynamic preview never mutates either actual character instance");
            var report = new { status = "passed", resolution = _resolution, checks_passed = _checks.Count, checks = _checks, captures = _captures,
                screenshot_renderer = DisplayServer.GetName(), player_save_read = false, player_save_modified = false,
                candidate_numbers_applied_to_character = false, isolated_roundtrip = savePath, current_editor_or_game_replaced = false };
            using (var file = Godot.FileAccess.Open(Review + "验收_" + _resolution + ".json", Godot.FileAccess.ModeFlags.Write))
                file.StoreString(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            GD.Print($"ITEM_SPATIAL_HOVER_OK: {_checks.Count} checks; {_resolution}; {_captures.Count} real native captures; no player state");
            HoverInfoManager.HideAll(); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(originalMode, persist: false); }
    }
}
