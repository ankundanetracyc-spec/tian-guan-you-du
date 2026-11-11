using Godot;
using System.Globalization;
using System.Text.Json;
using static TalismanCraftingCatalog;
using Plans = TalismanCraftingPlans;
using Service = TalismanCraftingService;

/// <summary>配伍只记需求、成材能力冻结；隔离存档和真实鼠标界面回归。</summary>
public partial class TalismanPlansNativeSmoke : Node
{
    private sealed record CheckResult(string Name, bool Passed, string Detail);
    private readonly List<CheckResult> _checks = [];
    private readonly List<string> _captures = [];
    private readonly List<Character> _actors = [];
    private Character _owner = null!;
    private TalismanCraftingScene _room = null!;
    private MaterialEntry _fiber = null!, _liquid = null!, _dense = null!;
    private string _output = "", _paper = "", _ink = "", _brush = "";
    private int _width = 1280, _height = 720;
    private bool _headless;
    private void Check(bool ok, string name, object? detail = null)
    {
        string text = detail?.ToString() ?? "";
        _checks.Add(new(name, ok, text));
        if (!ok) throw new InvalidOperationException(name + (text.Length == 0 ? "" : ": " + text));
        GD.Print("TALISMAN_PLANS_OK " + name);
    }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static T Find<T>(Node node, string name) where T : Node => node.FindChild(name, true, false) as T
        ?? throw new InvalidOperationException("找不到控件：" + name);
    private static bool IsWithin(string path, string directory)
    {
        string root = System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        string target = System.IO.Path.GetFullPath(path);
        return target.Equals(root, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    private static Rect2 Bounds(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas();
        var bounds = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        foreach (var point in new[] { new Vector2(control.Size.X, 0), control.Size, new Vector2(0, control.Size.Y) }) bounds = bounds.Expand(transform * point);
        return bounds;
    }
    private void Configure()
    {
        var args = OS.GetCmdlineUserArgs();
        string width = args.FirstOrDefault(v => v.StartsWith("--talisman-plans-width="))?.Split('=', 2)[1] ?? "1280";
        if (!int.TryParse(width, out _width) || _width is not (1280 or 1920)) throw new ArgumentException("talisman-plans-width 必须是1280或1920。");
        _height = _width == 1920 ? 1080 : 720;
        string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string allowed = System.IO.Path.Combine(project, "output");
        string supplied = args.FirstOrDefault(v => v.StartsWith("--talisman-plans-output="))?.Split('=', 2)[1] ?? "res://output/talisman_plans_native_20261109";
        if (supplied.StartsWith("res://") || supplied.StartsWith("user://")) supplied = ProjectSettings.GlobalizePath(supplied);
        else if (!System.IO.Path.IsPathRooted(supplied)) supplied = System.IO.Path.Combine(project, supplied);
        _output = System.IO.Path.GetFullPath(supplied);
        if (!IsWithin(_output, allowed)) throw new ArgumentException("配伍册验收输出必须在本项目output内。");
        Check(IsWithin(ProjectSettings.GlobalizePath("user://"), allowed), "用户资料隔离于项目output");
        Check(!GetNode<GameSaveManager>("/root/GameSaveManager").has_active_slot, "不打开玩家正式旅程");
        System.IO.Directory.CreateDirectory(_output);
        _headless = DisplayServer.GetName() == "headless";
        GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Borderless = true;
        GetWindow().ContentScaleSize = Vector2I.Zero; GetWindow().Size = new(_width, _height); GetWindow().Unfocusable = true;
    }
    private async Task Frames(int count = 3) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> predicate, string name, int seconds = 8)
    {
        ulong deadline = Time.GetTicksMsec() + (ulong)seconds * 1000;
        while (!predicate() && Time.GetTicksMsec() < deadline) await Frames();
        Check(predicate(), name);
    }
    private async Task Click(Control control)
    {
        for (Node? parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) { scroll.EnsureControlVisible(control); await Frames(); }
        var bounds = Bounds(control);
        Check(control.IsVisibleInTree() && control.MouseFilter != Control.MouseFilterEnum.Ignore && control is not Button { Disabled: true }
            && bounds.Size.X > 2 && bounds.Size.Y > 2 && GetViewport().GetVisibleRect().Grow(1).Encloses(bounds), "真实指针可点击 " + control.Name, bounds);
        var point = bounds.GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        foreach (bool pressed in new[] { true, false }) GetViewport().PushInput(new InputEventMouseButton
            { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
        await Frames();
    }
    private async Task Enter(LineEdit input, string value)
    {
        await Click(input);
        foreach (bool pressed in new[] { true, false }) GetViewport().PushInput(new InputEventKey { Keycode = Key.A, CtrlPressed = true, Pressed = pressed }, true);
        foreach (var rune in value.EnumerateRunes())
            GetViewport().PushInput(new InputEventKey { Unicode = (uint)rune.Value, Pressed = true }, true);
        await Frames();
        Check(input.Text == value, "真实键盘输入 " + input.Name, input.Text);
    }
    private async Task Capture(string name)
    {
        if (_headless) return;
        HoverInfoManager.HideAll();
        GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One * 4, GlobalPosition = Vector2.One * 4 }, true);
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _height, "原生窗口截图 " + name);
        string file = $"talisman_plans_{name}_{_width}.png";
        Check(image.SavePng(System.IO.Path.Combine(_output, file)) == Error.Ok, "保存截图 " + name);
        _captures.Add(file);
    }
    private Character Player(string name)
    {
        var owner = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData
        {
            entity_name = name, cultivation_realm = "云息三层", current_health = 100, max_health = 100,
            base_talisman_proficiency = 64, talisman_proficiency = 64, main_gongfa_path = GongfaAvailability.DefaultBookPath
        } };
        _actors.Add(owner); AddChild(owner); owner.Hide();
        owner.current_world_stamina = owner.max_world_stamina = 100;
        owner.max_spirit_perception = 24; owner.used_spirit_perception = 0;
        return owner;
    }
    private static HumanData Data(Character actor) => (HumanData)actor.entity_data!;
    private static string RequestKey(Service.Request request) => request.Mode == "drawing"
        ? JsonSerializer.Serialize(new { request.Mode, request.RecipeId, request.PaperPath, request.BrushPath, request.Quantity, request.WritingId,
            Inks = request.InkSelections.OrderBy(v => v.Path, StringComparer.Ordinal).ToArray() })
        : JsonSerializer.Serialize(new { request.Mode, request.TechniqueId, Materials = request.Materials.OrderBy(v => v.Path, StringComparer.Ordinal).ToArray(),
            Retained = request.Mode == "ink" ? request.RetainedUtilities.Order(StringComparer.Ordinal).ToArray() : [] });
    private string WorldState(Character actor) => JsonSerializer.Serialize(new
    {
        Inventory = actor.get_spatial_inventory().OrderBy(v => v.InstanceId, StringComparer.Ordinal)
            .Select(v => $"{v.InstanceId}:{v.ItemPath}:{v.RegionId}:{v.X},{v.Y}:{v.Rotation}").ToArray(),
        actor.current_world_stamina, actor.max_world_stamina, actor.TalismanAttainment,
        Hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours(),
        Methods = Data(actor).talisman_method_ids.ToArray(), Recipes = Data(actor).talisman_recipe_ids.ToArray(),
        Records = Data(actor).talisman_records.ToArray(),
        Frozen = Data(actor).talisman_product_definitions.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value).ToArray()
    });
    private static string PlanState(Character actor) => string.Join("\n", new[] { "paper", "ink", "drawing" }.SelectMany(mode => Plans.List(actor, mode))
        .OrderBy(v => v.Id, StringComparer.Ordinal).Select(v => $"{v.Id}|{v.Name}|{v.Mode}|{RequestKey(v.Request)}"));
    private Service.Request Paper(int quantity = 2, string method = "yangmai") => new()
        { Mode = "paper", Materials = [new(_fiber.Path, quantity), new(_liquid.Path, 1)], TechniqueId = method };
    private Service.Request Ink() => new()
        { Mode = "ink", Materials = [new(_dense.Path, 1), new(_liquid.Path, 1)], TechniqueId = "junyan", RetainedUtilities = ["护"] };
    private Service.Request Drawing(string writing = "common") => new()
        { Mode = "drawing", PaperPath = _paper, BrushPath = _brush, InkSelections = [new(_ink, 2)], RecipeId = "hushen", WritingId = writing, Quantity = 1 };
    private string Contract(Character actor, Service.ProductSnapshot snapshot, int count)
    {
        Check(Service.ValidateSnapshot(snapshot) && Service.TrySaveProduct(snapshot, out _, out _), "隔离冻结合同有效 " + snapshot.Kind);
        Check(Service.TrySaveProduct(snapshot, out string path, out var item) && item is not null && actor.add_item_to_inventory(path, count), "隔离冻结合同入包 " + snapshot.Kind);
        Data(actor).talisman_product_definitions[path] = JsonSerializer.Serialize(snapshot);
        return path;
    }
    private void LoadMaterials()
    {
        var catalog = DiscoverMaterials(refresh: true);
        _fiber = catalog.First(v => v.Texture == "纤维");
        _liquid = catalog.First(v => v.Texture == "液体");
        _dense = catalog.First(v => v.Texture == "致密" && v.UtilityLayers.Any(u => u.Name == "护"));
        Check(catalog.All(v => v.ValidationErrors.Count == 0), "计划引用正式材性目录");
    }
    private void CheckModel()
    {
        var actor = Player("配伍册资料模型隔离人物");
        Check(Plans.List(null, "paper").Count == 0 && !Plans.Save(null, "无主", Paper(), false, out _), "无人物不伪造配伍册");
        string untouched = WorldState(actor);
        var ids = new List<string>();
        foreach (var request in new[] { Paper(), Ink(), new Service.Request { Mode = "drawing", PaperPath = "user://missing_saved_paper.tres", RecipeId = "hushen", Quantity = 2 } })
        {
            string key = RequestKey(request);
            Check(Plans.Save(actor, "  同名配伍  ", request, false, out string reason), request.Mode + "允许记下缺料草稿", reason);
            var entry = Plans.List(actor, request.Mode).Single(); ids.Add(entry.Id);
            Check(entry.Name == "同名配伍" && RequestKey(entry.Request) == key, request.Mode + "只保存本工艺完整需求");
            request.Materials.Clear(); request.InkSelections.Clear(); request.RetainedUtilities.Clear(); request.PaperPath = "changed";
            Check(Plans.TryLoad(actor, entry.Id, out var loaded, out _) && RequestKey(loaded) == key, entry.Mode + "保存与调用方深拷贝");
            loaded.Materials.Clear(); loaded.InkSelections.Clear(); loaded.RetainedUtilities.Clear(); loaded.PaperPath = "changed_again";
            Check(Plans.TryLoad(actor, entry.Id, out var loadedAgain, out _) && RequestKey(loadedAgain) == key, entry.Mode + "每次载入深拷贝");
            entry.Request.Materials.Clear(); entry.Request.InkSelections.Clear(); entry.Request.PaperPath = "changed_list";
            Check(Plans.TryLoad(actor, entry.Id, out var fromList, out _) && RequestKey(fromList) == key, entry.Mode + "目录返回也不暴露存储引用");
        }
        Check(ids.Distinct(StringComparer.Ordinal).Count() == 3, "三个模式同名各自独立");
        var paper = Plans.List(actor, "paper").Single(); string original = RequestKey(paper.Request);
        Check(!Plans.Save(actor, "同名配伍", Paper(3), false, out _) && RequestKey(Plans.List(actor, "paper").Single().Request) == original, "同模式重名不静默覆盖");
        Check(Plans.Save(actor, "同名配伍", Paper(3), true, out _) && Plans.List(actor, "paper").Single().Id == paper.Id, "明确覆盖保留原方案身份");
        Check(Plans.Save(actor, "另一纸方", Paper(), false, out _) && !Plans.Rename(actor, paper.Id, "另一纸方", out _), "更名冲突不覆盖其他方案");
        Check(Plans.Rename(actor, paper.Id, "纸方新名", out _) && Plans.List(actor, "paper").Single(v => v.Id == paper.Id).Name == "纸方新名", "更名保留身份及配伍内容");
        Check(!Plans.Save(actor, " ", Paper(), false, out _) && !Plans.Save(actor, new string('长', Plans.NameLimit + 1), Paper(), false, out _)
            && !Plans.Save(actor, "换\n行", Paper(), false, out _), "空白超长及控制字符名称拒绝");
        Check(!Plans.Save(actor, "空纸方", new() { Mode = "paper" }, false, out _)
            && !Plans.Save(actor, "空墨方", new() { Mode = "ink" }, false, out _)
            && !Plans.Save(actor, "空画方", new() { Mode = "drawing" }, false, out _), "空配伍不入册");
        var cross = Paper(); cross.PaperPath = "leftover"; cross.InkSelections.Add(new("leftover_ink", 8)); cross.RetainedUtilities.Add("护");
        Check(Plans.Save(actor, "页面隔离", cross, false, out _), "纸方含其他页面残留仍可保存当前页");
        var normalized = Plans.List(actor, "paper").Single(v => v.Name == "页面隔离").Request;
        Check(normalized.PaperPath.Length == 0 && normalized.InkSelections.Count == 0 && normalized.RetainedUtilities.Count == 0, "纸方剔除画符及研墨残留字段");
        var drawing = new Service.Request { Mode = "drawing", PaperPath = "user://missing_saved_paper.tres", Materials = [new(_dense.Path, 4)], TechniqueId = "juyun" };
        Check(Plans.Save(actor, "画方隔离", drawing, false, out _) && Plans.List(actor, "drawing").Single(v => v.Name == "画方隔离").Request.Materials.Count == 0, "画方不保存此前研墨材料");
        Check(Plans.Save(actor, "待学凝纹", Paper(method: "ningwen"), false, out _), "未学技艺也可记下待用方案");
        var unlearned = Plans.List(actor, "paper").Single(v => v.Name == "待学凝纹");
        Check(Plans.TryLoad(actor, unlearned.Id, out var advanced, out _) && advanced.TechniqueId == "ningwen"
            && !Service.IsMethodKnown(actor, "ningwen") && unlearned.Warnings.Count > 0, "载入保留未学技艺但不授予");
        Data(actor).talisman_plans.Add(new() { version = Plans.Version + 1, id = "broken_version", plan_name = "旧版资料", mode = "paper", request_json = "{}" });
        Data(actor).talisman_plans.Add(new() { version = Plans.Version, id = "broken_json", plan_name = "损坏资料", mode = "paper", request_json = "{invalid}" });
        foreach (string id in new[] { "broken_version", "broken_json" })
        {
            var broken = Plans.List(actor, "paper").Single(v => v.Id == id);
            Check(!broken.IsLoadable && broken.Warnings.Count > 0 && !Plans.TryLoad(actor, id, out _, out _), "坏资料不可载入且保留可见 " + id);
            Check(Plans.Delete(actor, id, out _) && !Plans.List(actor, "paper").Any(v => v.Id == id), "坏资料可明确删除 " + id);
        }
        Check(Plans.Delete(actor, paper.Id, out _) && !Plans.TryLoad(actor, paper.Id, out _, out _) && !Plans.Delete(actor, paper.Id, out _), "删除持久生效且重复删除不造结果");
        Check(WorldState(actor) == untouched, "全部计划操作不改库存精力历法造诣技艺或生产记录");
        SaveReload(actor, "model");
    }
    private void SaveReload(Character actor, string tag)
    {
        string expected = PlanState(actor);
        string path = $"user://talisman_plans_{tag}_{_width}.tres";
        Check(ResourceSaver.Save(new PersistentGameSave { player_data = Data(actor) }, path) == Error.Ok, tag + "独立人物资料保存");
        var save = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        var restored = Player(tag + "重载人物"); restored.load_from_data(save.player_data!);
        Check(PlanState(restored) == expected, tag + "HumanData重载保留三模式方案及身份");
        Check(Data(restored).talisman_method_ids.SequenceEqual(Data(actor).talisman_method_ids), tag + "重载计划不授予技艺");
        foreach (var item in Data(actor).talisman_product_definitions)
            Check(Data(restored).talisman_product_definitions.TryGetValue(item.Key, out var json) && json == item.Value
                && Service.LoadProduct(item.Key)?.snapshot_json == item.Value, tag + "重载保留已冻结纸墨能力");
    }

    private async Task OpenBook(string mode = "")
    {
        await Click(Find<Button>(_room, "PlanBookButton"));
        if (mode.Length > 0) await Click(Find<Button>(_room, "PlanMode_" + mode));
    }
    private async Task CloseBook() => await Click(Find<Button>(_room, "CloseCraftingModal"));
    private async Task SaveUi(string name)
    {
        await Enter(Find<LineEdit>(_room, "PlanNameInput"), name);
        await Click(Find<Button>(_room, "SavePlanButton"));
    }
    private async Task CheckUi()
    {
        _owner = Player("配伍册真实操作隔离人物");
        foreach (var entry in new[] { _fiber, _liquid, _dense }) Check(_owner.add_item_to_inventory(entry.Path, 6), "页面人物实际入料 " + entry.Name);
        _paper = Contract(_owner, new() { Kind = "paper", Capacity = 2.5, Permeability = .3, Toughness = .9, Connection = .9, Retention = .9 }, 3);
        _ink = Contract(_owner, new() { Kind = "ink", SpiritGrade = 1, Concentration = .3, Flow = .3, Attachment = .8, Setting = .8,
            InputSourceBudget = .29, Sources = [new("护", 1, .18), new("固", 1, .07), new("聚", 1, .04)] }, 5);
        _brush = Contract(_owner, Service.BrushSnapshot("qingzhu"), 1);
        _room = new TalismanCraftingScene { OwnerCharacter = _owner }; AddChild(_room); await Frames();
        _room.SelectMaterial(_fiber.Path, 2); _room.SelectMaterial(_liquid.Path, 1); await Frames();
        string before = WorldState(_owner);
        await OpenBook(); await SaveUi("常用纸方"); await Capture("paper_book");
        var first = Plans.List(_owner, "paper").Single(v => v.Name == "常用纸方");
        Check(RequestKey(first.Request) == RequestKey(_room.CurrentRequest()), "从当前纸页保存实际需求");
        await CloseBook(); _room.SelectMaterial(_fiber.Path, 3); await Frames(); await OpenBook(); await SaveUi("常用纸方");
        Check(Find<Button>(_room, "PlanOverwriteConfirm").IsVisibleInTree() && Plans.List(_owner, "paper").Single().Request.Materials.Single(v => v.Path == _fiber.Path).Quantity == 2, "覆盖前保留原纸方");
        await Capture("overwrite_confirm"); await Click(Find<Button>(_room, "PlanOverwriteConfirm"));
        Check(Plans.List(_owner, "paper").Single().Id == first.Id && Plans.List(_owner, "paper").Single().Request.Materials.Single(v => v.Path == _fiber.Path).Quantity == 3, "指针明确覆盖保存新需求并保留身份");
        await SaveUi("备用纸方");
        var second = Plans.List(_owner, "paper").Single(v => v.Name == "备用纸方");
        await Click(Find<Button>(_room, "PlanRename_" + first.Id)); await Enter(Find<LineEdit>(_room, "PlanRenameInput"), "备用纸方");
        await Click(Find<Button>(_room, "PlanRenameConfirm"));
        Check(Plans.List(_owner, "paper").Single(v => v.Id == first.Id).Name == "常用纸方" && Find<Label>(_room, "PlanBookNotice").Text.Length > 0, "更名冲突显示原因且不覆盖");
        await Capture("rename_conflict");
        await Enter(Find<LineEdit>(_room, "PlanRenameInput"), "山居常用"); await Click(Find<Button>(_room, "PlanRenameConfirm"));
        Check(Plans.List(_owner, "paper").Single(v => v.Id == first.Id).Name == "山居常用", "真实指针更名成功");
        await Click(Find<Button>(_room, "PlanDelete_" + second.Id));
        Check(Plans.List(_owner, "paper").Count == 2, "删除确认前不移除方案");
        await Capture("delete_confirm"); await Click(Find<Button>(_room, "PlanDeleteConfirm"));
        Check(Plans.List(_owner, "paper").Count == 1, "明确确认后删除方案");
        await CloseBook();
        Check(WorldState(_owner) == before, "保存覆盖更名删除和浏览不触发生产或用度");

        Check(_owner.remove_item_from_inventory(_liquid.Path, _owner.get_inventory_total_count(_liquid.Path)), "缺料测试只在隔离人物移走液体");
        _room.SelectMaterial(_liquid.Path, 0); await Frames();
        before = WorldState(_owner);
        await OpenBook(); await Click(Find<Button>(_room, "PlanLoad_" + first.Id));
        Check(_room.CurrentRequest().Materials.Any(v => v.Path == _liquid.Path && v.Quantity == 1)
            && _room.CurrentRequest().Materials.Any(v => v.Path == _fiber.Path && v.Quantity == 3), "缺料仍原量载入纸方不夹库存");
        Check(Find<Button>(_room, "StartCraftButton").Disabled && !Service.Evaluate(_owner, _room.CurrentRequest()).CanStart, "缺料纸方开工禁用");
        Check(WorldState(_owner) == before, "缺料载入不补材料不扣精力历法或造诣");
        await Capture("paper_missing");
        Check(_owner.add_item_to_inventory(_liquid.Path, 6), "恢复隔离研墨测试用料");
        await Click(Find<Button>(_room, "CraftTab_ink")); _room.SelectMaterial(_dense.Path, 1); _room.SelectMaterial(_liquid.Path, 1); await Frames();
        string inkKey = RequestKey(_room.CurrentRequest()); before = WorldState(_owner);
        await OpenBook(); await SaveUi("山居常用");
        var inkPlan = Plans.List(_owner, "ink").Single(v => v.Name == "山居常用");
        Check(Plans.List(_owner, "paper").Count == 1 && inkPlan.Id != first.Id, "界面允许不同工艺同名");
        await Capture("ink_book"); await Click(Find<Button>(_room, "PlanMode_paper"));
        await Click(Find<Button>(_room, "PlanLoad_" + first.Id));
        Check(_room.ActiveMode == "paper", "跨册载入切到实际工艺页");
        await OpenBook("ink"); await Click(Find<Button>(_room, "PlanLoad_" + inkPlan.Id));
        Check(_room.ActiveMode == "ink" && RequestKey(_room.CurrentRequest()) == inkKey, "研墨载入保留剂量与留墨功用");
        Check(WorldState(_owner) == before, "跨工艺读取不产生任何物品或资源变更");

        var lostRaw = Paper();
        lostRaw.Materials.Add(new("res://resources/entities/items/配伍册不存在原料.tres", 1));
        Check(Plans.Save(_owner, "失效原料替换", lostRaw, false, out _), "失效原料记录保留原路径供替换");
        var lostRawId = Plans.List(_owner, "paper").Single(p => p.Name == "失效原料替换").Id;
        await OpenBook("paper"); await Click(Find<Button>(_room, "PlanLoad_" + lostRawId));
        Check(Find<Button>(_room, "StartCraftButton").Disabled, "失效原料载入后不能开工");
        await Capture("missing_raw_replace");
        await Click(Descendants(_room).OfType<Button>().Single(b => b.Name.ToString().StartsWith("RemoveMissingMaterial_")));
        Check(_room.CurrentRequest().Materials.All(p => p.Path != "res://resources/entities/items/配伍册不存在原料.tres")
            && _room.CurrentRequest().Materials.Any(p => p.Path == _fiber.Path), "真实点击只移除失效原料并保留有效选择");
        Check(Plans.Delete(_owner, lostRawId, out _), "清理隔离失效原料方案");

        var lostInk = Drawing(); lostInk.InkSelections.Add(new("user://talisman/products/配伍册不存在符墨.tres", 1));
        Check(Plans.Save(_owner, "失效符墨替换", lostInk, false, out _), "失效墨仍按原路径保留");
        var lostInkId = Plans.List(_owner, "drawing").Single(p => p.Name == "失效符墨替换").Id;
        await OpenBook("drawing"); await Click(Find<Button>(_room, "PlanLoad_" + lostInkId));
        Check(Find<Button>(_room, "StartCraftButton").Disabled, "失效墨不能藏在配伍中绕过预检");
        await Capture("missing_ink_replace");
        await Click(Descendants(_room).OfType<Button>().Single(b => b.Name.ToString().StartsWith("RemoveMissingInk_")));
        Check(_room.CurrentRequest().InkSelections.Count == 1 && _room.CurrentRequest().InkSelections[0].Path == _ink
            && !Find<Button>(_room, "StartCraftButton").Disabled, "移除失效墨后有效纸墨笔可继续成符");
        Check(Plans.Delete(_owner, lostInkId, out _) && WorldState(_owner) == before, "替换失效选择全过程不制造补料或消耗资源");
        _room.SetMode("ink"); await Frames();

        Check(Plans.Save(_owner, "现成纸墨", Drawing(), false, out _), "已有纸墨笔记入画符配伍");
        Check(Plans.Save(_owner, "待学写法", Drawing("ninghao"), false, out _), "记下尚未掌握的写法");
        var drawingPlan = Plans.List(_owner, "drawing").Single(v => v.Name == "现成纸墨");
        var waiting = Plans.List(_owner, "drawing").Single(v => v.Name == "待学写法");
        string frozenPaper = Service.LoadProduct(_paper)!.snapshot_json, frozenInk = Service.LoadProduct(_ink)!.snapshot_json;
        Data(_owner).base_talisman_proficiency = Data(_owner).talisman_proficiency = 80;
        before = WorldState(_owner);
        await OpenBook("drawing"); await Capture("drawing_book"); await Click(Find<Button>(_room, "PlanLoad_" + waiting.Id));
        Check(_room.CurrentRequest().WritingId == "ninghao" && !Service.IsMethodKnown(_owner, "ninghao") && Find<Button>(_room, "StartCraftButton").Disabled, "未学写法原样载入且不授技艺或允许开工");
        Check(WorldState(_owner) == before, "未学方法载入不改变本人状态");
        await Capture("drawing_unlearned"); await OpenBook("drawing"); await Click(Find<Button>(_room, "PlanLoad_" + drawingPlan.Id));
        Check(RequestKey(_room.CurrentRequest()) == RequestKey(Drawing()) && !Find<Button>(_room, "StartCraftButton").Disabled, "常法完整加载现成纸墨笔");
        Check(Service.LoadProduct(_paper)!.snapshot_json == frozenPaper && Service.LoadProduct(_ink)!.snapshot_json == frozenInk, "造诣改变后读取仍用冻结纸墨");
        Check(WorldState(_owner) == before, "完整画符载入也不扣材料精力历法或造诣");
        await Capture("drawing_loaded");
        SaveReload(_owner, "ui");
        await CheckEffectsAndOutcome();
    }
    private async Task CheckEffectsAndOutcome()
    {
        string before = WorldState(_owner);
        await Click(Find<Button>(_room, "ChooseRecipeButton"));
        foreach (string recipe in new[] { "hushen", "yanshi" })
        {
            await Click(Find<Button>(_room, "RecipeEffects_" + recipe));
            for (int tier = 0; tier < 5; tier++)
            {
                Check(Find<Label>(_room, "RecipeTier_" + tier).Text == TalismanCombatCatalog.TierName(tier), recipe + "符谱档名" + tier);
                Check(Find<Label>(_room, "RecipeEffect_" + tier).Text == TalismanCombatCatalog.EffectText(recipe, tier), recipe + "符谱固定效果" + tier);
                Check(Math.Abs(Bounds(Find<Label>(_room, "RecipeTier_" + tier)).GetCenter().Y
                    - Bounds(Find<Label>(_room, "RecipeEffect_" + tier)).GetCenter().Y) < .5f, recipe + "档名与效果同行对齐" + tier);
            }
            await Capture("recipe_" + recipe); await Click(Find<Button>(_room, "RecipeEffectsBack"));
        }
        await Click(Find<Button>(_room, "Recipe_hushen"));
        Check(WorldState(_owner) == before, "查阅十档固定符效不扣费不制造");
        var quote = Service.Evaluate(_owner, _room.CurrentRequest());
        Check(quote.CanStart, "已载入配伍通过真正画符预检", string.Join("；", quote.Blockers));
        int records = Data(_owner).talisman_records.Count;
        float energy = _owner.current_world_stamina;
        await Click(Find<Button>(_room, "StartCraftButton"));
        await Until(() => _room.FindChild("ContinueCraftButton", true, false) is Button { Visible: true }, "实际成符进入鉴看");
        var results = Service.OwnedProducts(_owner, "talisman");
        Check(results.Count > 0 && Data(_owner).talisman_records.Count == records + 1 && Math.Abs(_owner.current_world_stamina - (energy - quote.EnergyCost)) < .001, "只有明确开工才制作及扣实际精力");
        foreach (var label in Descendants(_room).OfType<Label>().Where(v => v.Name.ToString().StartsWith("ResultTier_")))
        {
            int index = int.Parse(label.Name.ToString()["ResultTier_".Length..], CultureInfo.InvariantCulture);
            var effect = Find<Label>(_room, "ResultEffect_" + index).Text;
            Check(results.Any(result => label.Text == TalismanCombatCatalog.TierName(result.Snapshot.Tier)
                && effect == TalismanCombatCatalog.EffectText(result.Snapshot.RecipeId, result.Snapshot.Tier)), "鉴看档名和效果来自实际固定成符" + index);
        }
        Check(Descendants(_room).OfType<Label>().Any(v => v.Name.ToString().StartsWith("ResultTier_")), "鉴看显示符效名而非数字占位");
        string summary = Find<Label>(_room, "ResultWorkSummary").Text;
        string number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
        Check(summary.Contains("常法") && summary.Contains("符道" + number(quote.Proficiency, "0.#"))
            && summary.Contains("精力" + number(quote.EnergyCost, "0.#")) && summary.Contains(number(quote.DurationHours, "0.##") + "时辰"), "结果用度读取本批实际造诣技法精力与工时", summary);
        await Capture("actual_outcome");
        string after = WorldState(_owner);
        await Click(Find<Button>(_room, "ContinueCraftButton")); await OpenBook("drawing"); await CloseBook();
        Check(WorldState(_owner) == after, "鉴看返回及再开配伍册不重复扣发");
    }
    private void Report(bool passed, string failure)
    {
        if (_output.Length == 0 || !System.IO.Directory.Exists(_output)) return;
        System.IO.File.WriteAllText(System.IO.Path.Combine(_output, $"talisman_plans_checks_{_width}.json"), JsonSerializer.Serialize(new
        {
            status = passed ? "passed" : "failed", width = _width, height = _height, headless = _headless,
            passed = _checks.Count(v => v.Passed), failed = _checks.Count(v => !v.Passed), failure,
            checks = _checks, captures = _captures, inputMethod = "Viewport.PushInput mouse and keyboard",
            playerJourneyRead = false, syntheticFrozenContracts = true, changedCombatEffects = false
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        bool passed = false; string failure = "";
        try { Configure(); await Frames(); LoadMaterials(); CheckModel(); await CheckUi(); passed = true; }
        catch (Exception error) { failure = error.ToString(); GD.PushError(failure); }
        finally
        {
            foreach (var node in _actors.Cast<Node>().Append(_room).Where(GodotObject.IsInstanceValid))
                if (!node.IsQueuedForDeletion()) node.QueueFree();
            await Frames(); Report(passed, failure);
            GD.Print($"TALISMAN_PLANS_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}");
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
