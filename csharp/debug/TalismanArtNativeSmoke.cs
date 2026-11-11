using Godot;
using System.Text.Json;
using Service = TalismanCraftingService;

/// <summary>仅在隔离用户目录验收符工美术、旧物显示与真实行囊，不读取玩家旅程。</summary>
public partial class TalismanArtNativeSmoke : Node
{
    private sealed record CheckResult(string Name, bool Passed, string Detail);
    private sealed record LegacyProduct(string Key, string Path, TalismanProductItemData Item, string SnapshotJson,
        string LegacyIcon, string ExpectedIcon, int Count);
    private sealed record InventoryTransition(string Stage, string Before, string After, string[] Added, string[] Removed);
    private readonly List<CheckResult> _checks = [];
    private readonly List<string> _captures = [];
    private readonly List<LegacyProduct> _legacy = [];
    private readonly List<InventoryTransition> _inventoryTransitions = [];
    private Character _owner = null!, _maker = null!;
    private StoragePanel _inventory = null!;
    private TalismanCraftingScene _room = null!;
    private string _output = "";
    private int _width = 1280, _height = 720;
    private bool _headless;
    private const string Art = TalismanArtCatalog.PaintedRoot;

    private void Check(bool passed, string name, object? detail = null)
    {
        string message = detail?.ToString() ?? "";
        _checks.Add(new(name, passed, message));
        if (!passed) throw new InvalidOperationException(name + (message.Length == 0 ? "" : ": " + message));
        GD.Print("TALISMAN_ART_OK " + name);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static bool IsWithin(string path, string directory)
    {
        string target = System.IO.Path.GetFullPath(path);
        string root = System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        return target.Equals(root, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void Configure()
    {
        var args = OS.GetCmdlineUserArgs();
        string width = args.FirstOrDefault(value => value.StartsWith("--talisman-art-width="))?.Split('=', 2)[1] ?? "1280";
        if (!int.TryParse(width, out _width) || _width is not (1280 or 1920)) throw new ArgumentException("talisman-art-width 必须是1280或1920。");
        _height = _width == 1920 ? 1080 : 720;
        string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string allowed = System.IO.Path.Combine(project, "output");
        string supplied = args.FirstOrDefault(value => value.StartsWith("--talisman-art-output="))?.Split('=', 2)[1]
            ?? "res://output/talisman_art_native_20261109";
        if (supplied.StartsWith("res://") || supplied.StartsWith("user://")) supplied = ProjectSettings.GlobalizePath(supplied);
        else if (!System.IO.Path.IsPathRooted(supplied)) supplied = System.IO.Path.Combine(project, supplied);
        string output = System.IO.Path.GetFullPath(supplied);
        if (!IsWithin(output, allowed)) throw new ArgumentException("美术验收输出必须在本项目output内。");
        _output = output;
        Check(IsWithin(ProjectSettings.GlobalizePath("user://"), allowed), "用户资料隔离于output", ProjectSettings.GlobalizePath("user://"));
        System.IO.Directory.CreateDirectory(_output);
        _headless = DisplayServer.GetName() == "headless";
        GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Borderless = true;
        GetWindow().ContentScaleSize = Vector2I.Zero; GetWindow().Size = new Vector2I(_width, _height);
        GetWindow().Unfocusable = true;
    }

    private async Task Frames(int count = 4)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Capture(string state)
    {
        if (_headless) return;
        HoverInfoManager.HideAll();
        GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One * 4, GlobalPosition = Vector2.One * 4 }, true);
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _height, "截图使用原生窗口 " + state);
        string name = $"talisman_art_{state}_{_width}.png";
        Check(image.SavePng(System.IO.Path.Combine(_output, name)) == Error.Ok, "保存美术实机 " + state);
        _captures.Add(name);
    }

    private void CheckImages()
    {
        foreach (string name in new[] { "paper", "ink", "hushen", "yanshi", "brush_qingzhu", "brush_jinghao", "brush_cangfeng" })
        {
            string path = Art + name + ".png";
            Check(ResourceLoader.Exists(path) && GD.Load<Texture2D>(path) is not null, "正式PNG已导入 " + name);
            using var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            Check(image.GetWidth() == 128 && image.GetHeight() == (name == "ink" ? 128 : 256), "背包图标每格128源像素 " + name, image.GetSize());
            Check(image.DetectAlpha() != Image.AlphaMode.None, "图标保留真实透明 " + name);
            var corners = new[] { Vector2I.Zero, new Vector2I(image.GetWidth() - 1, 0), image.GetSize() - Vector2I.One,
                new Vector2I(0, image.GetHeight() - 1) };
            Check(corners.All(point => image.GetPixelv(point).A == 0), "图标四角全透明 " + name);
        }
        Check(TalismanArtCatalog.PaperScenePath() == Art + "scene_paper.png", "中央纸张明确命中新PNG");
        Check(new[] { "qingzhu", "jinghao", "cangfeng" }.Select(id => TalismanArtCatalog.IconPath(Service.BrushSnapshot(id)))
            .Distinct(StringComparer.Ordinal).Count() == 3, "三支符笔使用独立图标路径");
        foreach (string recipe in new[] { "hushen", "yanshi" })
        for (int tier = 0; tier < 5; tier++)
        {
            var skill = GD.Load<ActiveSkillData>(TalismanCombatCatalog.SkillPath(recipe, tier));
            Check(skill is not null && skill.icon_path == Art + recipe + ".png"
                && TalismanCombatCatalog.IconPath(recipe) == skill.icon_path, "战斗技能与成符共用新PNG " + recipe + tier);
        }
    }

    private static string Fingerprint(Character actor) => string.Join("|", actor.get_spatial_inventory()
        .OrderBy(entry => entry.InstanceId, StringComparer.Ordinal)
        .Select(entry => $"{entry.InstanceId}:{entry.ItemPath}:{entry.RegionId}:{entry.X},{entry.Y}:{entry.Rotation}"));

    private Character CharacterFixture(string name, bool addToTree)
    {
        var actor = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData
        {
            entity_name = name, current_health = 100, max_health = 100, cultivation_realm = "云息三层",
            base_talisman_proficiency = 64, talisman_proficiency = 64, current_world_stamina = 73, max_world_stamina = 100
        } };
        if (addToTree) { AddChild(actor); actor.Hide(); }
        actor.max_spirit_perception = 24; actor.used_spirit_perception = 0;
        return actor;
    }

    private void AddLegacy(string key, Service.ProductSnapshot snapshot, int count)
    {
        Check(Service.ValidateSnapshot(snapshot), "旧物夹具保持合法能力 " + key);
        string json = JsonSerializer.Serialize(snapshot);
        string legacyIcon = "res://textures/ui/" + (snapshot.Kind == "talisman" ? "talisman_crafting_20261109/" + snapshot.RecipeId
            : "talisman_crafting_20261108/" + snapshot.Kind) + ".svg";
        string path = $"user://talisman/products/art_legacy_{key}_{_width}_{Guid.NewGuid():N}.tres";
        var item = new TalismanProductItemData { entity_name = key switch { "paper" => "符纸", "ink" => "符墨",
            "hushen" => "护身符", "yanshi" => "炎矢符", "qingzhu" => "青竹符笔", "jinghao" => "静毫符笔", _ => "藏锋符笔" },
            definition_id = new StringName("talisman_art_fixture_" + key), snapshot_json = json, icon_path = legacyIcon, description = "",
            item_category = snapshot.Kind == "talisman" ? "符箓" : "器具", item_subcategory = snapshot.Kind,
            inventory_width = 1, inventory_height = snapshot.Kind == "ink" ? 1 : 2, can_stack = snapshot.Kind != "brush",
            battle_skill_path = snapshot.Kind == "talisman" ? TalismanCombatCatalog.SkillPath(snapshot.RecipeId, snapshot.Tier) : "" };
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("user://talisman/products"));
        Check(ResourceSaver.Save(item, path) == Error.Ok, "旧SVG物品夹具仅写隔离资料 " + key);
        item.TakeOverPath(path);
        _legacy.Add(new LegacyProduct(key, path, item, json, legacyIcon, TalismanArtCatalog.IconPath(snapshot), count));
        // 故意不给墨写恢复字典，覆盖只留下库存路径的旧物。
        if (key != "ink") ((HumanData)_owner.entity_data!).talisman_product_definitions[path] = json;
        Check(_owner.add_item_to_inventory(path, count), "旧物进入真实空间库存 " + key);
    }

    private void BuildLegacyInventory()
    {
        _owner = CharacterFixture("符箓美术隔离行囊", false);
        AddLegacy("paper", new Service.ProductSnapshot { Kind = "paper", Capacity = 1.7, Permeability = .45,
            Toughness = .7, Connection = .7, Retention = .7 }, 2);
        AddLegacy("ink", new Service.ProductSnapshot { Kind = "ink", SpiritGrade = 1, Sources = [new("护", 1, .2), new("固", 1, .1)],
            InputSourceBudget = .3, Concentration = .5, Flow = .45, Attachment = .7, Setting = .7 }, 2);
        AddLegacy("hushen", new Service.ProductSnapshot { Kind = "talisman", RecipeId = "hushen" }, 2);
        AddLegacy("yanshi", new Service.ProductSnapshot { Kind = "talisman", RecipeId = "yanshi" }, 2);
        foreach (string id in new[] { "qingzhu", "jinghao", "cangfeng" }) AddLegacy(id, Service.BrushSnapshot(id), 1);
        Check(_owner.add_item_to_inventory(Service.SpiritStonePath, 3), "持有真实灵石以检查纯显示刷新不扣费");
        for (int index = 0; index < _legacy.Count; index++)
        {
            var entries = _owner.get_spatial_inventory().Where(entry => entry.ItemPath == _legacy[index].Path).ToArray();
            for (int n = 0; n < entries.Length; n++)
                Check(_owner.move_spatial_item(entries[n].InstanceId, "back", index, n * 3, 0), "隔离摆放七类物品 " + _legacy[index].Key + n);
        }
        Check(_legacy.All(item => item.Item.icon_path == item.LegacyIcon), "就绪前确实加载旧SVG显示夹具");
    }

    private void CheckLegacyState(string stage, string expectedInventory, float expectedEnergy, int expectedRecords,
        bool allowReadyClothing = false)
    {
        foreach (var product in _legacy)
        {
            var entry = _owner.get_spatial_inventory().First(item => item.ItemPath == product.Path);
            Check(entry.Item is TalismanProductItemData item && item.icon_path == product.ExpectedIcon,
                stage + "真实ItemData更新为PNG " + product.Key, entry.Item?.icon_path);
            Check(product.Item.snapshot_json == product.SnapshotJson && _owner.get_inventory_total_count(product.Path) == product.Count,
                stage + "能力JSON和真实数量不变 " + product.Key);
        }
        string actualInventory = Fingerprint(_owner);
        var beforeRows = expectedInventory.Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var afterRows = actualInventory.Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var added = afterRows.Except(beforeRows, StringComparer.Ordinal).ToArray();
        var removed = beforeRows.Except(afterRows, StringComparer.Ordinal).ToArray();
        _inventoryTransitions.Add(new InventoryTransition(stage, expectedInventory, actualInventory, added, removed));
        string detail = $"added={string.Join(";", added)}; removed={string.Join(";", removed)}";
        if (allowReadyClothing)
        {
            Check(removed.Length == 0, stage + "所有既存实物路径身份位置朝向完全不变", detail);
            var human = (HumanData)_owner.entity_data!;
            var existingIds = beforeRows.Select(row => row[..row.IndexOf(':')]).ToHashSet(StringComparer.Ordinal);
            var initialized = _owner.get_spatial_inventory().Where(entry => !existingIds.Contains(entry.InstanceId)).ToArray();
            Check(initialized.Length <= 1 && initialized.All(entry => entry.Item is PortraitClothingData
                && entry.ItemPath == human.portrait_clothing_item_path), stage + "仅允许正常首次穿戴初始化的一件真实衣着", detail);
            GD.Print("TALISMAN_ART_READY_INVENTORY " + detail);
        }
        else Check(actualInventory == expectedInventory, stage + "路径身份位置朝向完全不变", detail);
        Check(_owner.current_world_stamina == expectedEnergy && _owner.get_inventory_total_count(Service.SpiritStonePath) == 3
            && ((HumanData)_owner.entity_data!).talisman_records.Count == expectedRecords, stage + "不扣精力灵石、不增加制作记录");
    }

    private async Task CheckExistingProducts()
    {
        BuildLegacyInventory();
        string inventory = Fingerprint(_owner); float energy = _owner.current_world_stamina;
        int records = ((HumanData)_owner.entity_data!).talisman_records.Count;
        var time = GetNode<GameTimeMgr>("/root/GameTimeManager"); double clock = time.get_precise_game_hours();
        AddChild(_owner); _owner.Hide(); await Frames();
        CheckLegacyState("Character.Ready", inventory, energy, records, allowReadyClothing: true);
        // 衣着是正常首次初始化的新增实物；自此后纯显示刷新必须保持整份行囊完全不变。
        inventory = Fingerprint(_owner);
        foreach (var product in _legacy)
        {
            product.Item.icon_path = product.LegacyIcon;
            var loaded = Service.LoadProduct(product.Path);
            Check(loaded is not null && loaded.icon_path == product.ExpectedIcon && loaded.snapshot_json == product.SnapshotJson,
                "缓存命中也派生更新PNG " + product.Key);
            product.Item.icon_path = product.LegacyIcon;
        }
        Service.RestoreDefinitions((HumanData)_owner.entity_data!);
        CheckLegacyState("RestoreDefinitions已有文件", inventory, energy, records);
        _inventory = new StoragePanel(); AddChild(_inventory); _inventory.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _inventory.bind_player(_owner); _inventory.show_panel(); await Frames(10);
        Check(Character.InventoryRegions.Sum(region => region.Width * region.Height) == 200, "使用正式200格原生行囊");
        foreach (var product in _legacy)
        {
            var slots = Descendants(_inventory).OfType<StorageItemSlot>().Where(slot => slot.item_path == product.Path).ToArray();
            Check(slots.Length == product.Count && slots.All(slot => slot.IsVisibleInTree()
                && slot.GetNode<TextureRect>("Frame/ItemIcon").Texture?.ResourcePath == product.ExpectedIcon),
                "普通行囊实际图像控件加载新PNG " + product.Key);
        }
        await Capture("inventory_seven_products");
        await CheckInventoryCraftHovers();
        CheckLegacyState("首次打开普通行囊", inventory, energy, records);
        Check(Math.Abs(time.get_precise_game_hours() - clock) < .000001, "显示更新与行囊浏览不推进历法");
        _inventory.hide_panel();
    }

    private void CheckNewProducts()
    {
        _maker = CharacterFixture("新制品图标隔离验证", true);
        var catalog = TalismanCraftingCatalog.DiscoverMaterials();
        var fiber = catalog.First(item => item.Texture == "纤维");
        var liquid = catalog.First(item => item.Texture == "液体" && item.UtilityLayers.Count > 0);
        Check(_maker.add_item_to_inventory(fiber.Path, 2) && _maker.add_item_to_inventory(liquid.Path, 3), "新制品使用正式原料实际库存");
        foreach (var request in new[]
        {
            new Service.Request { Mode = "paper", Materials = [new(fiber.Path, 2), new(liquid.Path, 1)] },
            new Service.Request { Mode = "ink", TechniqueId = "junyan", Materials = [new(liquid.Path, 2)],
                RetainedUtilities = liquid.UtilityLayers.Select(layer => layer.Name).Take(4).ToList() }
        })
        {
            Check(_maker.TryCraftTalisman(request, out var outcome, out string reason), "真实制作成功 " + request.Mode, reason);
            Check(outcome.Products.Count > 0 && outcome.Products.All(product => product.Item.icon_path == Art + request.Mode + ".png"
                && product.Item.snapshot_json == JsonSerializer.Serialize(product.Snapshot)), "新制纸墨绑定PNG且能力独立保存 " + request.Mode);
        }
    }

    private async Task CheckPaperScene()
    {
        _room = new TalismanCraftingScene { OwnerCharacter = _owner }; AddChild(_room); await Frames(6);
        Check(_room.ActiveMode == "paper" && Descendants(_room).OfType<TextureRect>().Any(image => image.IsVisibleInTree()
            && image.Texture?.ResourcePath == Art + "scene_paper.png"), "制纸界面中央实际加载新宣纸PNG而非SVG回退");
        await Capture("paper_painted_scene");
        await CheckCraftPickerHovers();
    }

    private void Report(bool passed, string failure)
    {
        if (_output.Length == 0 || !System.IO.Directory.Exists(_output)) return;
        System.IO.File.WriteAllText(System.IO.Path.Combine(_output, $"talisman_art_checks_{_width}.json"), JsonSerializer.Serialize(new
        {
            status = passed ? "passed" : "failed", width = _width, height = _height, headless = _headless,
            passed = _checks.Count(check => check.Passed), failed = _checks.Count(check => !check.Passed), failure,
            captures = _captures, checks = _checks, inventoryTransitions = _inventoryTransitions,
            usesOrdinaryStoragePanel = true, playerJourneyRead = false,
            oldProductsAreIsolatedFixtures = true
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        bool passed = false; string failure = "";
        try
        {
            Configure(); await Frames(); CheckImages(); await CheckExistingProducts(); CheckNewProducts(); await CheckPaperScene(); passed = true;
        }
        catch (Exception error) { failure = error.ToString(); GD.PushError(failure); }
        finally
        {
            foreach (var node in new Node[] { _room, _inventory, _owner, _maker })
                if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
                { if (node.IsInsideTree()) node.QueueFree(); else node.Free(); }
            await Frames(); Report(passed, failure);
            GD.Print($"TALISMAN_ART_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}");
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
