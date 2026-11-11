using Godot;
using System.Text.Json;
using static TalismanCraftingCatalog;
using Service = TalismanCraftingService;

/// <summary>原生符工隔离验收。仅允许 output 内的用户目录，不读取正式旅程。</summary>
public partial class TalismanCraftingNativeSmoke : Node
{
    private sealed record CheckResult(string Name, bool Passed, string Detail);
    private readonly List<CheckResult> _checks = [];
    private readonly List<string> _captures = [];
    private readonly List<Character> _fixtures = [];
    private readonly List<TalismanCraftingPresentation> _finishedPresentations = [];
    private IReadOnlyList<MaterialEntry> _catalog = [];
    private MaterialEntry _fiber = null!, _liquid = null!, _dense = null!, _particle = null!, _glue = null!;
    private Character _owner = null!;
    private TalismanCraftingScene _room = null!;
    private Control _location = null!;
    private string _output = "";
    private int _width = 1280, _height = 720;
    private bool _headless;

    private void Check(bool passed, string name, object? detail = null)
    {
        string message = detail?.ToString() ?? "";
        _checks.Add(new CheckResult(name, passed, message));
        if (!passed) throw new InvalidOperationException(name + (message.Length == 0 ? "" : ": " + message));
        GD.Print("TALISMAN_NATIVE_OK " + name);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static T Find<T>(Node root, string name) where T : Node => root.FindChild(name, true, false) as T
        ?? throw new InvalidOperationException("找不到控件：" + name);

    private static Rect2 Bounds(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas();
        var bounds = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        foreach (var point in new[] { new Vector2(control.Size.X, 0), control.Size, new Vector2(0, control.Size.Y) })
            bounds = bounds.Expand(transform * point);
        return bounds;
    }

    private static bool IsWithin(string path, string directory)
    {
        string root = System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        string target = System.IO.Path.GetFullPath(path);
        return target.Equals(root, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void Configure()
    {
        var args = OS.GetCmdlineUserArgs();
        string width = args.FirstOrDefault(value => value.StartsWith("--talisman-width="))?.Split('=', 2)[1] ?? "1280";
        if (!int.TryParse(width, out _width) || _width is not (1280 or 1920)) throw new ArgumentException("talisman-width 必须是1280或1920。");
        _height = _width == 1920 ? 1080 : 720;
        string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string allowed = System.IO.Path.Combine(project, "output");
        string supplied = args.FirstOrDefault(value => value.StartsWith("--talisman-output="))?.Split('=', 2)[1]
            ?? "res://output/talisman_native_20261109";
        if (supplied.StartsWith("res://") || supplied.StartsWith("user://")) supplied = ProjectSettings.GlobalizePath(supplied);
        else if (!System.IO.Path.IsPathRooted(supplied)) supplied = System.IO.Path.Combine(project, supplied);
        string candidateOutput = System.IO.Path.GetFullPath(supplied);
        if (!IsWithin(candidateOutput, allowed)) throw new ArgumentException("符工验收输出必须在本项目output内。");
        _output = candidateOutput;
        string user = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        Check(IsWithin(user, allowed), "用户目录隔离于项目output", user);
        System.IO.Directory.CreateDirectory(_output);
        _headless = DisplayServer.GetName() == "headless";
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Borderless = true;
        GetWindow().ContentScaleSize = Vector2I.Zero;
        GetWindow().Size = new Vector2I(_width, _height);
        GetWindow().Unfocusable = true;
    }

    private async Task Frames(int count = 3)
    {
        for (int frame = 0; frame < count; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Until(Func<bool> predicate, string name, ulong milliseconds = 8000)
    {
        ulong deadline = Time.GetTicksMsec() + milliseconds;
        while (!predicate() && Time.GetTicksMsec() < deadline) await Frames();
        Check(predicate(), name);
    }

    private void Pointer(Vector2 point)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        foreach (bool down in new[] { true, false }) GetViewport().PushInput(new InputEventMouseButton
            { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = down }, true);
    }

    private async Task Click(Button button)
    {
        for (Node? parent = button.GetParent(); parent is not null; parent = parent.GetParent())
        {
            if (parent is not ScrollContainer scroll) continue;
            scroll.EnsureControlVisible(button);
            await Frames();
        }
        var rect = Bounds(button);
        Check(button.IsVisibleInTree() && !button.Disabled && button.MouseFilter != Control.MouseFilterEnum.Ignore
            && rect.Size.X > 2 && rect.Size.Y > 2 && GetViewport().GetVisibleRect().Grow(1).Encloses(rect),
            "真实指针可点击 " + button.Name, rect);
        Pointer(rect.GetCenter());
        await Frames();
    }

    private async Task Capture(string state)
    {
        if (_headless) return;
        GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One * 4, GlobalPosition = Vector2.One * 4 }, true);
        await Frames();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _height, "截图为原生窗口尺寸 " + state,
            $"{image.GetWidth()}x{image.GetHeight()}");
        string name = $"talisman_{state}_{_width}.png";
        Check(image.SavePng(System.IO.Path.Combine(_output, name)) == Error.Ok, "保存截图 " + state);
        _captures.Add(name);
    }

    private Character Player(string name, float proficiency = 64, bool inTree = true)
    {
        var actor = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData
        {
            entity_name = name, cultivation_realm = "云息三层", current_health = 100, max_health = 100,
            base_talisman_proficiency = proficiency, talisman_proficiency = proficiency,
            main_gongfa_path = GongfaAvailability.DefaultBookPath
        } };
        _fixtures.Add(actor);
        if (inTree) { AddChild(actor); actor.Hide(); }
        actor.current_world_stamina = actor.max_world_stamina = 100;
        actor.max_spirit_perception = 24; actor.used_spirit_perception = 0;
        return actor;
    }

    private void Stock(Character owner, params (MaterialEntry Material, int Count)[] items)
    {
        foreach (var item in items) Check(owner.add_item_to_inventory(item.Material.Path, item.Count),
            "隔离人物实际入料 " + item.Material.Name + "×" + item.Count);
    }

    private static string Fingerprint(Character owner) => string.Join("|", owner.get_spatial_inventory()
        .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
        .Select(item => $"{item.InstanceId}:{item.ItemPath}:{item.RegionId}:{item.X},{item.Y}:{item.Rotation}"));

    private string WorldState(Character owner)
    {
        var human = (HumanData)owner.entity_data!;
        return JsonSerializer.Serialize(new
        {
            Inventory = Fingerprint(owner), owner.current_world_stamina, owner.TalismanAttainment,
            Hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours(),
            Methods = human.talisman_method_ids.ToArray(), Recipes = human.talisman_recipe_ids.ToArray(),
            Records = human.talisman_records.ToArray(),
            Frozen = human.talisman_product_definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ":" + pair.Value).ToArray()
        });
    }

    private static Service.Request PaperRequest(MaterialEntry fiber, MaterialEntry liquid, int fibers = 2, int liquids = 1) => new()
    { Mode = "paper", TechniqueId = "yangmai", Materials = [new(fiber.Path, fibers), new(liquid.Path, liquids)] };

    private static Service.Request InkRequest(IEnumerable<Selection> materials, params string[] retained) => new()
    { Mode = "ink", TechniqueId = "junyan", Materials = materials.ToList(), RetainedUtilities = retained.ToList() };

    private void LoadMaterials()
    {
        _catalog = DiscoverMaterials(refresh: true);
        Check(_catalog.Count > 11 && _catalog.All(entry => entry.ValidationErrors.Count == 0), "从正式材性目录自动发现全部合法原料", _catalog.Count);
        _fiber = _catalog.First(entry => entry.Texture == "纤维" && entry.UtilityLayers.Any(u => u.Name == "固") && entry.UtilityLayers.Any(u => u.Name == "藏"));
        _liquid = _catalog.Where(entry => entry.Texture == "液体").OrderByDescending(entry => entry.UtilityLayers.Max(u => u.Grade)).First();
        _dense = _catalog.First(entry => entry.Texture == "致密" && entry.UtilityLayers.Any(u => u.Name == "护"));
        _particle = _catalog.First(entry => entry.Texture == "颗粒" && entry.UtilityLayers.Any(u => u.Name == "藏"));
        _glue = _catalog.First(entry => entry.Texture == "黏胶");
        Check(_catalog.All(entry => entry.Stock(null) == 0), "未绑定人物可读目录但不编造库存");
        Check(!IsEligible(_fiber, "ink") && IsEligible(_liquid, "paper") && IsEligible(_liquid, "ink") && IsEligible(_glue, "ink"), "纸墨质地路线分开");
        var invalidCraft = new MaterialCraftData { texture = "液体", element = "水",
            utilities = [new MaterialUtilityGrade { utility_id = "聚", grade = 10 }] };
        var invalid = new MaterialEntry("invalid-fixture", new ItemData { craft_data = invalidCraft }, invalidCraft);
        Check(!IsEligible(invalid, "ink") && invalid.UtilityLayers.Single().Grade == 10
            && invalid.ValidationErrors.Count > 0, "非法品级保留原值报错，不夹取成合法品级");
    }

    private void CheckSources(Service.Request request, Service.Quote quote, string tag)
    {
        Check(quote.CanStart && quote.PlannedProducts.Count > 0, tag + "可实际生成", string.Join("；", quote.Blockers));
        var inputs = request.Materials.Select(selection => (_catalog.Single(entry => entry.Path == selection.Path), selection.Quantity)).ToArray();
        var pairs = inputs.SelectMany(input => input.Item1.UtilityLayers).Select(layer => (layer.Name, layer.Grade)).ToHashSet();
        double totalBudget = inputs.Where(input => input.Item1.UtilityLayers.Count > 0).Sum(input => input.Quantity);
        double accounted = 0;
        foreach (var (snapshot, count) in quote.PlannedProducts)
        {
            Check(Service.ValidateSnapshot(snapshot), tag + "结果合同合法");
            Check(snapshot.Sources.Concat(snapshot.StructureSources).All(source => pairs.Contains((source.Name, source.Grade))), tag + "没有凭空功用或升高原料品级");
            double perUnit = snapshot.Sources.Sum(source => source.Amount) + snapshot.StructureSources.Sum(source => source.Amount)
                + snapshot.ProcessingSpent + snapshot.LostSourceBudget;
            Check(Math.Abs(perUnit - snapshot.InputSourceBudget) < .0001, tag + "逐份来源去向守恒", $"{perUnit}/{snapshot.InputSourceBudget}");
            accounted += snapshot.InputSourceBudget * count;
            if (snapshot.Kind == "ink") Check(snapshot.Sources.Select(source => source.Name).Distinct().Count() <= 4, tag + "成墨最多四项真实保留功用");
        }
        Check(Math.Abs(quote.SourceInputBudget - totalBudget) < .0001, tag + "输入账等于实际原料共享预算",
            $"{quote.SourceInputBudget}/{totalBudget}");
        Check(Math.Abs(accounted - quote.SourceRetainedTotal) < .0001, tag + "分张后结余等于本批保留账",
            $"{accounted}/{quote.SourceRetainedTotal}");
        double batchTotal = accounted + quote.SourceProcessingSpent + quote.SourceLostBudget;
        Check(Math.Abs(batchTotal - totalBudget) < .0002, tag + "保留加工损失与原料完整守恒",
            $"结余{accounted}+加工{quote.SourceProcessingSpent}+损失{quote.SourceLostBudget}={batchTotal}/{totalBudget}");
    }

    private void ExpectNoCommit(Character actor, Service.Request request, string tag)
    {
        string inventory = Fingerprint(actor);
        float energy = actor.current_world_stamina;
        var human = (HumanData)actor.entity_data!;
        int records = human.talisman_records.Count;
        float proficiency = human.talisman_proficiency;
        Check(!actor.TryCraftTalisman(request, out _, out string reason), tag + "拒绝提交", reason);
        Check(Fingerprint(actor) == inventory && actor.current_world_stamina == energy
            && human.talisman_records.Count == records && human.talisman_proficiency == proficiency, tag + "不扣物品精力、不写记录、不加造诣");
    }

    private void CheckModel()
    {
        var actor = Player("符工规则隔离验证");
        Stock(actor, (_fiber, 4), (_liquid, 4), (_dense, 2), (_particle, 1), (_glue, 1));
        var paper = PaperRequest(_fiber, _liquid, 2, 1);
        CheckSources(paper, Service.Evaluate(actor, paper), "纸料");
        var solid = InkRequest([new(_dense.Path, 2), new(_liquid.Path, 1), new(_particle.Path, 1), new(_glue.Path, 1)], "护", "固", "藏", "聚");
        CheckSources(solid, Service.Evaluate(actor, solid), "固液配墨");
        var pure = InkRequest([new(_liquid.Path, 2)], _liquid.UtilityLayers.Select(layer => layer.Name).ToArray());
        CheckSources(pure, Service.Evaluate(actor, pure), "纯液墨");
        ExpectNoCommit(actor, InkRequest([new(_glue.Path, 1)], "固"), "纯胶缺主墨源");
        ExpectNoCommit(actor, InkRequest([new(_dense.Path, 1)], "护"), "固墨缺配液");
        ExpectNoCommit(actor, PaperRequest(_fiber, _liquid, 99, 1), "原料不足");
        actor.current_world_stamina = 0;
        ExpectNoCommit(actor, paper, "精力不足");
        actor.current_world_stamina = 100;
        var human = (HumanData)actor.entity_data!;
        human.talisman_proficiency = 0;
        var low = Service.Evaluate(actor, paper);
        human.talisman_proficiency = 80;
        var high = Service.Evaluate(actor, paper);
        Check(low.CanStart && high.CanStart && high.DurationHours < low.DurationHours && high.EnergyCost <= low.EnergyCost,
            "造诣实际改变加工用度", $"低:{low.EnergyCost}/{low.DurationHours};高:{high.EnergyCost}/{high.DurationHours}");
        double Kept(Service.Quote quote) => quote.PlannedProducts.Sum(product => product.Snapshot.Sources.Sum(source => source.Amount) * product.Count);
        Check(Kept(high) > Kept(low), "造诣影响实际余蕴保留", $"{Kept(low)}/{Kept(high)}");
        CheckSources(paper, high, "高造诣纸料");
        human.talisman_proficiency = 64;
        var split = new Service.Request { Mode = "paper", Materials = [new(_liquid.Path, 1), new(_fiber.Path, 1), new(_fiber.Path, 1)] };
        string Planned(Service.Quote quote) => string.Join("|", quote.PlannedProducts.Select(product =>
            product.Count + ":" + JsonSerializer.Serialize(product.Snapshot)));
        Check(Planned(Service.Evaluate(actor, paper)) == Planned(Service.Evaluate(actor, split)), "同原料拆分及加入顺序不改变结果");
        var aliases = _catalog.Where(entry => entry.Texture == "纤维").GroupBy(Signature).First(group => group.Count() > 1).Take(2).ToArray();
        Stock(actor, (aliases[0], 1), (aliases[1], 1));
        Check(Planned(Service.Evaluate(actor, PaperRequest(aliases[0], _liquid, 1, 1)))
            == Planned(Service.Evaluate(actor, PaperRequest(aliases[1], _liquid, 1, 1))), "异名同材性不按名称给予额外收益");
        actor.max_spirit_perception = 0;
        ExpectNoCommit(actor, paper, "可用灵识不足");
        actor.max_spirit_perception = 24;
        CheckDrawingGuards();
    }

    private string ContractFixture(Character actor, Service.ProductSnapshot snapshot, int count)
    {
        // 仅用于守卫回归的合规合成合同；不放入界面演示人物，不冒充真实制作产物。
        Check(Service.ValidateSnapshot(snapshot) && Service.TrySaveProduct(snapshot, out _, out _), "隔离合同夹具校验 " + snapshot.Kind);
        Check(Service.TrySaveProduct(snapshot, out string path, out var item) && item is not null
            && actor.add_item_to_inventory(path, count), "隔离合同夹具入包 " + snapshot.Kind);
        ((HumanData)actor.entity_data!).talisman_product_definitions[path] = JsonSerializer.Serialize(snapshot);
        return path;
    }

    private void CheckDrawingGuards()
    {
        var actor = Player("画符守卫专用合成合同验证", 900);
        var human = (HumanData)actor.entity_data!;
        human.talisman_method_ids.Add("ninghao"); human.talisman_method_ids.Add("touzhi");
        string paper = ContractFixture(actor, new Service.ProductSnapshot { Kind = "paper", Capacity = 2.5,
            Permeability = .3, Toughness = .9, Connection = .9, Retention = .9 }, 2);
        string brush = ContractFixture(actor, Service.BrushSnapshot("qingzhu"), 1);
        string withoutWritingSources = ContractFixture(actor, new Service.ProductSnapshot { Kind = "ink", SpiritGrade = 1,
            Concentration = .3, Flow = .3, Attachment = .8, Setting = .8, InputSourceBudget = .25,
            Sources = [new("护", 1, .18), new("固", 1, .07)] }, 2);
        Service.Request Drawing(string ink, string writing, int quantity = 1) => new() { Mode = "drawing", PaperPath = paper,
            BrushPath = brush, InkSelections = [new(ink, 2)], WritingId = writing, RecipeId = "hushen", Quantity = quantity };
        Check(Service.Evaluate(actor, Drawing(withoutWritingSources, "common")).CanStart, "无技法来源对照仍可常法成符");
        ExpectNoCommit(actor, Drawing(withoutWritingSources, "ninghao"), "高造诣无聚不能凝毫聚势");
        ExpectNoCommit(actor, Drawing(withoutWritingSources, "touzhi"), "高造诣无通行不能透纸行墨");
        string enoughForTwo = ContractFixture(actor, new Service.ProductSnapshot { Kind = "ink", SpiritGrade = 1,
            Concentration = .3, Flow = .3, Attachment = .8, Setting = .8, InputSourceBudget = .29,
            Sources = [new("护", 1, .18), new("固", 1, .07), new("聚", 1, .04)] }, 2);
        var common = Service.Evaluate(actor, Drawing(enoughForTwo, "common", 2));
        var gathering = Service.Evaluate(actor, Drawing(enoughForTwo, "ninghao", 2));
        Check(common.CanStart && common.ExpectedCount == 2, "两份基础用墨能完成两张常法", string.Join("；", common.Blockers));
        Check(gathering.CanStart && gathering.ExpectedCount == 2 && gathering.Outgoing.GetValueOrDefault(enoughForTwo) == 2,
            "凝毫先保两张基础包，不吞第二张用墨", string.Join("；", gathering.Blockers));
    }

    private void CheckFullInventory()
    {
        // 支付两枚灵石后只空出两处不相邻单格，仍放不下两格符笔。
        var actor = Player("满包原子验证", inTree: false);
        Check(actor.add_item_to_inventory(Service.SpiritStonePath, 2), "满包测试持有真实两枚灵石");
        var stones = actor.get_spatial_inventory().ToArray();
        Check(actor.move_spatial_item(stones[0].InstanceId, "chest", 0, 0, 0)
            && actor.move_spatial_item(stones[1].InstanceId, "back", 7, 9, 0), "两枚灵石分离摆放");
        string fillerPath = System.IO.Path.Combine(_output, "talisman_space_filler_" + _width + ".tres");
        var filler = new ItemData { entity_name = "隔离占位", definition_id = new StringName("talisman_smoke_filler"),
            item_category = "器具", inventory_width = 1, inventory_height = 1 };
        Check(ResourceSaver.Save(filler, fillerPath) == Error.Ok, "仅在output保存隔离占位物");
        filler.TakeOverPath(fillerPath);
        Check(actor.add_item_to_inventory(fillerPath, 198) && actor.get_inventory_occupied_cell_count() == 200, "真实空间库存填满200格");
        string before = Fingerprint(actor);
        float energy = actor.current_world_stamina;
        Check(!actor.TryBuyTalismanBrush(out string reason), "无法安放符笔时拒绝购买", reason);
        Check(actor.get_inventory_total_count(Service.SpiritStonePath) == 2 && Fingerprint(actor) == before
            && actor.current_world_stamina == energy, "容量失败不扣灵石精力，不改变实例位置");
        GC.KeepAlive(filler);
    }

    private void CheckLayout(string mode)
    {
        var viewport = GetViewport().GetVisibleRect().Grow(1);
        foreach (string name in new[] { "CraftTab_paper", "CraftTab_ink", "CraftTab_drawing", "ReturnButton", "StartCraftButton", "CentralArt" })
            Check(viewport.Encloses(Bounds(Find<Control>(_room, name))), mode + "关键控件完整在屏内 " + name);
        var canvasInverse = Find<Control>(_room, "TalismanCanvas").GetGlobalTransformWithCanvas().AffineInverse();
        Rect2 DesignBounds(Control control)
        {
            var global = Bounds(control);
            var topLeft = canvasInverse * global.Position;
            return new Rect2(topLeft, canvasInverse * global.End - topLeft);
        }
        bool Near(float left, float right) => Math.Abs(left - right) <= .05f;
        var headerNames = new[] { "TalismanHelpButton", "PlanBookButton", "KnowledgeButton", "ReturnButton" };
        float headerCenterY = DesignBounds(Find<Button>(_room, headerNames[0])).GetCenter().Y;
        foreach (string name in headerNames)
        {
            var button = Find<Button>(_room, name);
            var rect = DesignBounds(button);
            Check(Near(rect.GetCenter().Y, headerCenterY) && button.GetThemeFontSize("font_size") == 22,
                mode + "顶栏按钮同一水平中心和22字号 " + name, $"{rect}; font={button.GetThemeFontSize("font_size")}");
        }
        var back = DesignBounds(Find<Button>(_room, "ReturnButton"));
        Check(Near(back.Position.Y, 18) && Near(back.Size.Y, 46), mode + "返回沿炼丹炼器顶栏位置与高度", back);
        var header = Find<Control>(_room, "TalismanHeader");
        var returnButton = Find<Button>(_room, "ReturnButton");
        Check(header.GetParent() == returnButton.GetParent() && !header.IsAncestorOf(returnButton)
            && Near(back.Position.X - DesignBounds(header).End.X, 16), mode + "功能横条止于返回前16单位，返回独立置于画布");
        var headerControls = header.GetChildren().OfType<Control>().Where(control => control is Button || control is Label).ToArray();
        Check(headerControls.All(control => control.GetThemeFontSize("font_size") == 22
            && Near(DesignBounds(control).Position.Y, 18) && Near(DesignBounds(control).Size.Y, 46)
            && DesignBounds(header).Grow(.1f).Encloses(DesignBounds(control))), mode + "标题造诣与全部功能同为22字46高且处于横条内");
        foreach (var (name, x) in new[] { ("TalismanHelpButton", 14f), ("CraftingHeaderMetrics", 926f) })
            Check(Near(Find<Control>(header, name).Position.X, x), mode + "说明与资源信息固定列 " + name);
        float spiritGap = DesignBounds(Find<Label>(header, "CraftingHeaderEnergyCaption")).Position.X
            - DesignBounds(Find<Label>(header, "CraftingHeaderSpiritValue")).End.X;
        float energyGap = DesignBounds(Find<Label>(header, "CraftingHeaderAttainmentCaption")).Position.X
            - DesignBounds(Find<Label>(header, "CraftingHeaderEnergyValue")).End.X;
        Check(Near(spiritGap, 32) && Near(energyGap, spiritGap), mode + "三组资源文字之间等距32单位", $"{spiritGap};{energyGap}");
        var modeTabs = Find<Control>(_room, "TalismanModeTabs");
        var tabPanel = Find<Control>(_room, "TalismanLeftPanel");
        Check(DesignBounds(tabPanel).Encloses(DesignBounds(modeTabs)) && !header.IsAncestorOf(modeTabs)
            && Near(DesignBounds(modeTabs).Position.Y, 138), mode + "三模式页签位于左侧纸面最上方");
        Check(DesignBounds(modeTabs).End.Y < DesignBounds(Find<Label>(_room, "TalismanLeftHeading")).Position.Y,
            mode + "模式页签在内容标题上方且不重叠");
        int tabIndex = 0;
        foreach (string name in new[] { "CraftTab_paper", "CraftTab_ink", "CraftTab_drawing" })
        {
            var tab = Find<Button>(_room, name);
            var tabBounds = DesignBounds(tab);
            Check(Near(tabBounds.Position.X, 48 + 120 * tabIndex++) && Near(tabBounds.Position.Y, 138)
                && Near(tabBounds.Size.X, 112) && Near(tabBounds.Size.Y, 46) && tab.GetThemeFontSize("font_size") == 22,
                mode + "左栏模式按钮同宽同高同字号 " + name, tabBounds);
        }
        Check(Find<Label>(header, "CraftingHeaderSpiritValue").Text == $"{Math.Max(0, _owner.max_spirit_perception - _owner.used_spirit_perception)}/{_owner.max_spirit_perception}"
            && Find<Label>(header, "CraftingHeaderEnergyValue").Text == $"{_owner.current_world_stamina:0.#}", mode + "顶栏读取真实可用灵识与当前精力");
        Check(header.GetThemeStylebox("panel") is StyleBoxTexture headerPaper
            && headerPaper.ModulateColor == Colors.White && headerPaper.Texture.ResourceName.Contains("saturation1"), mode + "横条沿用人物面板原纸面暖色");
        Check(!Descendants(header).OfType<Label>().Any(label => label.Text == "符 工"), mode + "顶栏不再重复页面标题");
        Check(Near(Bounds(Find<Label>(header, "CraftingHeaderSpiritValue")).End.X,
            Bounds(Find<Control>(header, "CraftingHeaderSpirit")).End.X), mode + "灵识整组靠资源栏右侧对齐");
        var steppers = Descendants(_room).OfType<LineEdit>().Where(input => input.IsVisibleInTree()
            && (input.Name.ToString().StartsWith("MaterialDose_") || input.Name.ToString().StartsWith("InkDose_")
                || input.Name == "DrawingQuantity")).ToArray();
        foreach (var input in steppers)
        {
            string name = input.Name.ToString();
            var minus = DesignBounds(Find<Button>(_room, name + "Minus"));
            var value = DesignBounds(input);
            var plus = DesignBounds(Find<Button>(_room, name + "Plus"));
            string detail = $"minus={minus}; input={value}; plus={plus}";
            Check(Near(minus.Position.Y, value.Position.Y) && Near(value.Position.Y, plus.Position.Y)
                && Near(minus.End.Y, value.End.Y) && Near(value.End.Y, plus.End.Y)
                && Near(minus.Size.Y, 36) && Near(value.Size.Y, 36) && Near(plus.Size.Y, 36),
                mode + "用量三件组同顶同底且高36 " + name, detail);
            Check(minus.End.X <= value.Position.X + .05f && value.End.X <= plus.Position.X + .05f
                && !minus.Intersects(value) && !value.Intersects(plus) && !minus.Intersects(plus),
                mode + "用量减号数值加号互不交叠 " + name, detail);
        }
        var chips = Descendants(_room).OfType<Button>().Where(button => button.IsVisibleInTree()
            && button.Name.ToString().StartsWith("Retain_")).ToArray();
        for (int left = 0; left < chips.Length; left++)
        for (int right = left + 1; right < chips.Length; right++)
            Check(!DesignBounds(chips[left]).Intersects(DesignBounds(chips[right])),
                mode + "留墨功用按钮互不交叠 " + chips[left].Name + "/" + chips[right].Name,
                $"{DesignBounds(chips[left])}; {DesignBounds(chips[right])}");
        var leftPanel = (Control)Find<Button>(_room, mode == "drawing" ? "ChooseRecipeButton" : "SelectMaterialsButton").GetParent();
        var rightPanel = (Control)Find<Button>(_room, "StartCraftButton").GetParent();
        Check(Near(DesignBounds(leftPanel).End.Y, DesignBounds(rightPanel).End.Y), mode + "左右栏底边对齐",
            $"left={DesignBounds(leftPanel)}; right={DesignBounds(rightPanel)}");
        var methods = Service.Methods(mode).Select(method => Find<Button>(_room, "Technique_" + method.Id)).ToArray();
        void CheckCardLabels(Control card, string kind)
        {
            var cardBounds = Bounds(card);
            foreach (var label in Descendants(card).OfType<Label>().Where(label => label.IsVisibleInTree()))
                Check(cardBounds.Grow(.5f).Encloses(Bounds(label)), mode + kind + "文字位于卡内 " + card.Name + "/" + label.Name,
                    $"text={label.Text.Replace("\n", " / ")}; label={Bounds(label)}; card={cardBounds}");
        }
        for (int i = 0; i < methods.Length; i++)
        {
            Check(viewport.Encloses(Bounds(methods[i])), mode + "技艺竖排完整 " + methods[i].Name);
            if (i > 0) Check(!Bounds(methods[i - 1]).Intersects(Bounds(methods[i])), mode + "技艺卡互不遮挡 " + i);
            CheckCardLabels(methods[i], "技艺卡");
        }
        var materialCards = Descendants(_room).OfType<Control>().Where(card => card.IsVisibleInTree()
            && card.Name.ToString().StartsWith("MaterialCard_")).ToArray();
        if (mode is "paper" or "ink") Check(materialCards.Length == _room.CurrentRequest().Materials.Count,
            mode + "材料文字验收覆盖全部已选卡", $"cards={materialCards.Length}; selected={_room.CurrentRequest().Materials.Count}");
        foreach (var card in materialCards) CheckCardLabels(card, "材料卡");
        string[] labels = Descendants(_room).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
        if (mode is "paper" or "ink")
        {
            string[] forbidden = mode == "paper" ? ["灵性", "承墨量", "渗墨性", "韧性"] : ["灵性", "浓淡", "流动性"];
            Check(!labels.Any(forbidden.Contains), mode + "准备页不展示尚未生产的结果字段");
        }
        Check(!labels.Any(text => text.Contains("成纸后鉴看") || text.Contains("择纸配墨") || text.Contains("化材 · 蕴养")
            || text.Contains("纸墨可用现成品")), mode + "不恢复已删除的说明与静态流程条");
        Check(Find<TextureRect>(_room, "CentralArt").Texture is not null, mode + "中央使用实际意象素材");
    }

    private async Task PickMaterial(MaterialEntry entry)
    {
        await Click(Find<Button>(_room, "SelectMaterialsButton"));
        var search = Find<LineEdit>(_room, "MaterialSearch");
        search.Text = entry.Name;
        search.EmitSignal(LineEdit.SignalName.TextChanged, entry.Name);
        await Frames();
        var picks = Descendants(_room).OfType<Button>().Where(button => button.Name.ToString().StartsWith("PickMaterial_")).ToArray();
        Check(picks.Length == 1 && picks[0].Name == "PickMaterial_" + entry.Id, "选材搜索来自真实目录 " + entry.Name);
        await Click(picks[0]);
        Check(_room.CurrentRequest().Materials.Any(selection => selection.Path == entry.Path && selection.Quantity == 1), "指针选入真实材料 " + entry.Name);
        await Click(Find<Button>(_room, "FinishMaterialSelection"));
    }

    private async Task MakeCurrent(string mode)
    {
        var request = _room.CurrentRequest();
        var quote = Service.Evaluate(_owner, request);
        Check(quote.CanStart, mode + "真实输入可提交", string.Join("；", quote.Blockers));
        // 画符只扣实际开用部分；选中的上限不等于必须耗尽的墨量。
        var outgoing = quote.Outgoing.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var counts = outgoing.ToDictionary(pair => pair.Key, pair => _owner.get_inventory_total_count(pair.Key));
        int brushCount = string.IsNullOrEmpty(request.BrushPath) ? 0 : _owner.get_inventory_total_count(request.BrushPath);
        int records = ((HumanData)_owner.entity_data!).talisman_records.Count;
        float energy = _owner.current_world_stamina;
        double hours = GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours();
        var canvas = Find<Control>(_room, "TalismanCanvas");
        var originalVisibility = canvas.GetChildren().OfType<CanvasItem>().ToDictionary(item => item, item => item.Visible);
        var backdrop = _room.GetChildren().OfType<TextureRect>().Single();
        var backdropTexture = backdrop.Texture;
        var start = Find<Button>(_room, "StartCraftButton");
        var point = Bounds(start).GetCenter();
        await Click(start);
        var presentation = Find<TalismanCraftingPresentation>(_room, "CraftingPresentationView");
        var status = Find<Label>(_room, "CraftingPresentationStatus");
        var skip = Find<Button>(_room, "SkipCraftPresentation");
        Check(presentation.Playing && presentation.CraftMode == mode && status.Text == presentation.PhaseText,
            mode + "自动演出使用本工艺与当前状态");
        Check(Bounds(presentation).HasArea() && GetViewport().GetVisibleRect().Grow(1).Encloses(Bounds(skip))
            && GetViewport().GetVisibleRect().Grow(1).Encloses(Bounds(status)), mode + "演出与跳过控件完整可见");
        Check(!Find<Control>(_room, "CraftingPage").IsVisibleInTree()
            && !Find<Button>(_room, "CraftTab_paper").IsVisibleInTree()
            && !Find<Button>(_room, "ReturnButton").IsVisibleInTree()
            && originalVisibility.Keys.All(item => !item.Visible), mode + "演出隐藏顶栏与其余准备空间");
        var stage = Find<Control>(_room, "CraftingModal");
        Check(backdrop.IsVisibleInTree() && backdrop.Texture == backdropTexture
            && !Descendants(stage).Any(node => node is Panel or PanelContainer),
            mode + "保留原场景背景且中央演出没有纸面弹窗");
        var canvasBounds = Bounds(canvas); var skipBounds = Bounds(skip);
        Check(skipBounds.Position.X > canvasBounds.GetCenter().X
            && skipBounds.Position.Y - canvasBounds.Position.Y < canvasBounds.Size.Y * .03f
            && canvasBounds.End.X - skipBounds.End.X < canvasBounds.Size.X * .03f
            && Math.Abs(Bounds(presentation).GetCenter().X - canvasBounds.GetCenter().X) < 1,
            mode + "中央展示意象且跳过位于右上");
        Pointer(point); await Frames();
        start.EmitSignal(BaseButton.SignalName.Pressed);
        Check(((HumanData)_owner.entity_data!).talisman_records.Count == records + 1
            && Math.Abs(_owner.current_world_stamina - (energy - quote.EnergyCost)) < .001, mode + "重复点击只扣一次精力并记一条生产");
        Check(Math.Abs(GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours() - hours - quote.DurationHours * 2) < .0002,
            mode + "开工只推进一次本批实际游戏工时");
        foreach (var item in outgoing)
            Check(_owner.get_inventory_total_count(item.Key) == counts[item.Key] - item.Value, mode + "仅扣本批实际用料 " + item.Key);
        if (mode == "drawing") Check(_owner.get_inventory_total_count(request.BrushPath) == brushCount, "画符保留真实符笔");
        string committed = WorldState(_owner);
        if (mode == "drawing")
        {
            var faint = Find<TextureRect>(presentation, "PresentationFaintArtwork");
            Check(Bounds(presentation).Grow(.5f).Encloses(Bounds(faint)),
                "画符淡影按显示尺寸收在演出区内，不按原画尺寸溢出", Bounds(faint));
        }
        foreach (var previous in _finishedPresentations) previous.Skip();
        Check(presentation.Playing && Find<TalismanCraftingPresentation>(_room, "CraftingPresentationView") == presentation,
            mode + "旧批次完成回调不影响当前演出");

        foreach (bool pressed in new[] { true, false }) GetViewport().PushInput(new InputEventKey
            { Keycode = Key.Escape, Pressed = pressed }, true);
        await Frames();
        Check(presentation.Playing && _room.FindChild("CraftingModal", true, false) is Control { Visible: true },
            mode + "演出中Escape不撤掉遮罩或提前打开鉴看");
        string otherMode = mode == "paper" ? "ink" : "paper";
        Pointer(Bounds(Find<Button>(_room, "CraftTab_" + otherMode)).GetCenter());
        Pointer(Bounds(Find<Button>(_room, "PlanBookButton")).GetCenter());
        // The hidden return position now belongs to the visible skip action.
        _room.SetMode(otherMode); _room.ReturnToLocation();
        await Frames();
        Check(_room.IsInsideTree() && !_room.IsQueuedForDeletion() && _room.ActiveMode == mode && presentation.Playing
            && _room.FindChild("PlanNameInput", true, false) is null, mode + "演出遮罩与忙碌保护拦截切页配伍册和返回");
        Check(WorldState(_owner) == committed, mode + "播放及被拦截输入不再扣发资源或推进历法");
        await Capture(mode + "_run");
        await Until(() => GodotObject.IsInstanceValid(presentation) && presentation.Playing && presentation.PhaseIndex >= 1,
            mode + "自动演出进入中段");
        Check(status.Text == presentation.PhaseText, mode + "中段状态跟随演出更新");
        await Capture(mode + "_run_middle");
        if (mode == "ink")
        {
            await Click(skip);
            presentation.Skip(); presentation.Skip();
        }
        else
        {
            await Until(() => GodotObject.IsInstanceValid(presentation) && presentation.Playing && presentation.PhaseIndex >= 2,
                mode + "自然演出进入凝定收口");
            Check(status.Text == presentation.PhaseText, mode + "收口状态跟随演出更新");
            await Capture(mode + "_run_finish");
        }
        await Until(() => _room.FindChild("ContinueCraftButton", true, false) is Button { Visible: true },
            mode + (mode == "ink" ? "跳过后进入同一实际鉴看" : "自然结束进入实际鉴看"));
        presentation.Skip();
        Check(!presentation.Playing && Descendants(_room).Count(node => node.Name == "CraftingModal") == 1
            && _room.FindChild("CraftingPresentationView", true, false) is null, mode + "完成与重复跳过只保留一份鉴看");
        Check(WorldState(_owner) == committed, mode + "演出结束只揭示既定结果不重新制作");
        Check(originalVisibility.All(pair => GodotObject.IsInstanceValid(pair.Key) && pair.Key.Visible == pair.Value)
            && Find<Control>(_room, "CraftingPage").IsVisibleInTree()
            && Find<Button>(_room, "CraftTab_paper").IsVisibleInTree(), mode + "自然或跳过结束后恢复原界面可见性");
        Check(Find<Label>(_room, "CraftingHeaderEnergyValue").Text == $"{_owner.current_world_stamina:0.#}"
            && Find<Label>(_room, "CraftingHeaderSpiritValue").Text == $"{Math.Max(0, _owner.max_spirit_perception - _owner.used_spirit_perception)}/{_owner.max_spirit_perception}",
            mode + "制作结束后顶栏显示实际精力与灵识");
        _finishedPresentations.Add(presentation);
        string[] labels = Descendants(_room).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
        if (mode is "paper" or "ink")
        {
            var output = Service.OwnedProducts(_owner, mode).Last();
            var fields = Service.PlayerFields(output.Snapshot);
            Check(fields.Count == 4 && fields.All(field => labels.Contains(field.Key)), mode + "成品后展示四个真实字段");
        }
        await Capture(mode + "_result");
        await Click(Find<Button>(_room, "ContinueCraftButton"));
        presentation.Skip();
        Check(_room.FindChild("CraftingModal", true, false) is null && WorldState(_owner) == committed,
            mode + "返回案前后旧演出回调不再打开结果或重扣发");
    }

    private async Task CheckPresentationUnload()
    {
        var owner = Player("演出移出场景隔离人物");
        Stock(owner, (_fiber, 2), (_liquid, 2));
        var room = new TalismanCraftingScene { OwnerCharacter = owner };
        _location.AddChild(room); await Frames();
        room.ApplyCraftingPlan(PaperRequest(_fiber, _liquid, 1, 1)); await Frames();
        var originalVisibility = Find<Control>(room, "TalismanCanvas").GetChildren().OfType<CanvasItem>()
            .ToDictionary(item => item, item => item.Visible);
        var quote = Service.Evaluate(owner, room.CurrentRequest());
        Check(quote.CanStart, "移出场景用真实纸料配伍可制作");
        await Click(Find<Button>(room, "StartCraftButton"));
        var presentation = Find<TalismanCraftingPresentation>(room, "CraftingPresentationView");
        Check(presentation.Playing && Service.OwnedProducts(owner, "paper").Count > 0,
            "移出演出前成品已经真实入包");
        string committed = WorldState(owner);
        _location.RemoveChild(room); await Frames();
        Check(!room.IsInsideTree() && !presentation.Playing, "场景移出时停止演出并使完成回调失效");
        Check(originalVisibility.All(pair => GodotObject.IsInstanceValid(pair.Key) && pair.Key.Visible == pair.Value),
            "场景移出时恢复演出前各项可见性标记");
        presentation.Skip(); presentation.Stop();
        await Frames();
        Check(room.FindChild("ContinueCraftButton", true, false) is null && WorldState(owner) == committed,
            "移出后旧演出不补开鉴看不退款或重复入包");
        room.Free();
    }

    private async Task PickProduct(string kind)
    {
        await Click(Find<Button>(_room, "ChooseProduct_" + kind));
        var product = Service.OwnedProducts(_owner, kind).First();
        await Click(Find<Button>(_room, "PickProduct_" + product.Item.definition_id));
        if (kind == "ink") await Click(Find<Button>(_room, "FinishProductSelection"));
        var request = _room.CurrentRequest();
        Check(kind switch { "paper" => request.PaperPath == product.Path, "brush" => request.BrushPath == product.Path,
            _ => request.InkSelections.Any(selection => selection.Path == product.Path) }, "画符通过指针选择持有成品 " + kind);
    }

    private async Task CheckHeaderUi()
    {
        _owner = Player("符工顶栏隔离验证");
        Stock(_owner, (_fiber, 4), (_liquid, 5), (_dense, 3), (_particle, 2), (_glue, 2));
        _location = new Control { Name = "IsolatedTalismanHeaderHost" };
        AddChild(_location); _location.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _room = new TalismanCraftingScene { OwnerCharacter = _owner };
        _location.AddChild(_room); await Frames();
        string inventory = Fingerprint(_owner);
        float energy = _owner.current_world_stamina;
        foreach (string mode in new[] { "paper", "ink", "drawing" })
        {
            await Click(Find<Button>(_room, "CraftTab_" + mode));
            Check(_room.ActiveMode == mode, "顶栏真实点击可切换 " + mode);
            CheckLayout(mode); await Capture("header_" + mode);
        }
        foreach (string name in new[] { "PlanBookButton", "KnowledgeButton", "TalismanHelpButton" })
        {
            await Click(Find<Button>(_room, name));
            Check(_room.FindChild("CraftingModal", true, false) is Control { Visible: true }, "横条原功能可打开 " + name);
            await Click(Find<Button>(_room, "CloseCraftingModal"));
        }
        Check(Fingerprint(_owner) == inventory && _owner.current_world_stamina == energy, "顶栏浏览不改变真实库存与精力");
        await Click(Find<Button>(_room, "ReturnButton")); await Frames();
        Check(!GodotObject.IsInstanceValid(_room) || !_room.IsInsideTree(), "横条外的返回按钮保持原地点返回回调");
    }

    private async Task CheckUi()
    {
        _owner = Player("符工原生页面验证");
        Stock(_owner, (_fiber, 4), (_liquid, 5), (_dense, 3), (_particle, 2), (_glue, 2));
        Check(_owner.add_item_to_inventory(Service.SpiritStonePath, 4), "页面人物持有真实购笔灵石");
        _location = new Control { Name = "IsolatedTalismanLocationHost" };
        AddChild(_location); _location.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _room = new TalismanCraftingScene { OwnerCharacter = _owner };
        _location.AddChild(_room); await Frames();
        Check(_room.OwnerCharacter == _owner && _room.ActiveMode == "paper", "正式符工页面绑定实际制作人物");
        CheckLayout("paper"); await Capture("paper_empty");
        string beforeBrowse = Fingerprint(_owner);
        float energyBrowse = _owner.current_world_stamina;
        await Click(Find<Button>(_room, "KnowledgeButton"));
        Check(_owner.get_inventory_total_count(Service.SpiritStonePath) == 4, "打开符工录不扣灵石");
        await Click(Find<Button>(_room, "BuyTalismanBrushButton"));
        Check(_owner.get_inventory_total_count(Service.SpiritStonePath) == 2 && Service.OwnedProducts(_owner, "brush").Count == 1, "购笔真实扣两枚灵石并入行囊");
        await Click(Find<Button>(_room, "Learn_ningwen"));
        Check(Service.IsMethodKnown(_owner, "ningwen"), "研习后实际掌握制纸技艺");
        await Capture("knowledge");
        await Click(Find<Button>(_room, "CloseCraftingModal"));
        await PickMaterial(_fiber); await PickMaterial(_liquid);
        await Click(Find<Button>(_room, "MaterialDose_" + _fiber.Id + "Plus"));
        Check(_room.CurrentRequest().Materials.Single(selection => selection.Path == _fiber.Path).Quantity == 2, "用量加号同步真实草稿");
        await Click(Find<Button>(_room, "MaterialDose_" + _fiber.Id + "Minus"));
        Check(_room.CurrentRequest().Materials.Single(selection => selection.Path == _fiber.Path).Quantity == 1, "用量减号同步真实草稿");
        await Click(Find<Button>(_room, "MaterialDose_" + _fiber.Id + "Plus"));
        await Click(Find<Button>(_room, "Technique_ningwen"));
        Check(_room.CurrentRequest().TechniqueId == "ningwen", "已学技艺可由实际指针切换");
        await Click(Find<Button>(_room, "Technique_yangmai"));
        CheckLayout("paper"); await Capture("paper_ready"); await MakeCurrent("paper");

        await Click(Find<Button>(_room, "CraftTab_ink"));
        Check(_room.ActiveMode == "ink", "实际页签进入研墨");
        await PickMaterial(_dense); await PickMaterial(_particle); await PickMaterial(_liquid); await PickMaterial(_glue);
        await Click(Find<Button>(_room, "MaterialDose_" + _dense.Id + "Plus"));
        // 明确保留真实护、固、藏、聚，其他作用只参与加工，不凭空造源。
        foreach (string name in _room.CurrentRequest().RetainedUtilities.Except(new[] { "护", "固", "藏", "聚" }).ToArray())
            await Click(Find<Button>(_room, "Retain_" + name));
        foreach (string name in new[] { "护", "固", "藏", "聚" }.Except(_room.CurrentRequest().RetainedUtilities).ToArray())
            await Click(Find<Button>(_room, "Retain_" + name));
        Check(_room.CurrentRequest().RetainedUtilities.Count == 4, "留墨功用由玩家选择至多四项");
        CheckLayout("ink"); await Capture("ink_ready"); await MakeCurrent("ink");

        await Click(Find<Button>(_room, "CraftTab_drawing"));
        Check(_room.ActiveMode == "drawing" && string.IsNullOrEmpty(_room.CurrentRequest().PaperPath)
            && _room.CurrentRequest().InkSelections.Count == 0 && string.IsNullOrEmpty(_room.CurrentRequest().BrushPath), "画符不注入虚构默认纸墨笔");
        await PickProduct("paper"); await PickProduct("ink"); await PickProduct("brush");
        var ink = Service.OwnedProducts(_owner, "ink").First();
        for (int dose = 1; dose < ink.Count && !Service.Evaluate(_owner, _room.CurrentRequest()).CanStart; dose++)
            await Click(Find<Button>(_room, "InkDose_" + ink.Item.definition_id + "Plus"));
        await Click(Find<Button>(_room, "Technique_common"));
        CheckLayout("drawing"); await Capture("drawing_ready"); await MakeCurrent("drawing");
        Check(Service.OwnedProducts(_owner, "talisman").Any(entry => entry.Snapshot.RecipeId == "hushen" && entry.Count > 0), "真实纸墨笔经过行纹封符产出护身符");
        Check(Fingerprint(_owner) != beforeBrowse && _owner.current_world_stamina < energyBrowse, "完整生产确实改变库存及用度");
        SaveReload();
        var scene = GetTree().CurrentScene;
        await Click(Find<Button>(_room, "ReturnButton"));
        Check((!GodotObject.IsInstanceValid(_room) || _room.IsQueuedForDeletion()) && GodotObject.IsInstanceValid(_location)
            && _location.IsInsideTree() && GetTree().CurrentScene == scene, "返回只关闭符工页，保留地点宿主和当前旅程场景");
        await CheckPresentationUnload();
    }

    private void SaveReload()
    {
        var before = (HumanData)_owner.entity_data!;
        string path = System.IO.Path.Combine(_output, "talisman_character_" + _width + ".tres");
        Check(ResourceSaver.Save(before, path) == Error.Ok, "隔离人物及纸墨定义保存至output");
        var loaded = ResourceLoader.Load<HumanData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("隔离人物读档失败。");
        Check(loaded.talisman_method_ids.Contains("ningwen")
            && loaded.talisman_records.Count == before.talisman_records.Count && loaded.talisman_product_definitions.Count > 0, "读档保留研习记录与冻结产物合同");
        Check(before.talisman_product_definitions.All(pair =>
            loaded.talisman_product_definitions.TryGetValue(pair.Key, out var value) && value == pair.Value), "读档保留每份纸墨和符笔的完整冻结字段");
        var restored = Player("符工读档验证", inTree: false); restored.entity_data = loaded;
        ((HumanData)restored.entity_data!).talisman_proficiency = 900;
        foreach (string kind in new[] { "paper", "ink", "brush", "talisman" })
        {
            var originals = Service.OwnedProducts(_owner, kind);
            var copies = Service.OwnedProducts(restored, kind);
            Check(originals.Count == copies.Count && originals.All(original => copies.Any(copy => copy.Path == original.Path
                && copy.Count == original.Count && copy.Item.snapshot_json == original.Item.snapshot_json)), "读档不按新造诣重算 " + kind);
        }
        Check(Fingerprint(restored) == Fingerprint(_owner), "读档保留空间物品身份位置与朝向");
    }

    private void WriteReport(bool passed, string failure)
    {
        if (_output.Length == 0 || !System.IO.Directory.Exists(_output)) return;
        var report = new { status = passed ? "passed" : "failed", width = _width, height = _height,
            headless = _headless, passed = _checks.Count(check => check.Passed), failed = _checks.Count(check => !check.Passed), failure,
            materialCatalogCount = _catalog.Count, captures = _captures, checks = _checks,
            inputMethod = "Viewport.PushInput mouse hit testing", ownerIsIsolatedFixture = true, playerJourneyRead = false,
            scope = OS.GetCmdlineUserArgs().Contains("--talisman-header-only") ? "header UI only; no crafting transaction" : "full crafting regression" };
        System.IO.File.WriteAllText(System.IO.Path.Combine(_output, "talisman_checks_" + _width + ".json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        bool passed = false; string failure = "";
        try
        {
            Configure(); await Frames(); LoadMaterials();
            if (OS.GetCmdlineUserArgs().Contains("--talisman-header-only")) await CheckHeaderUi();
            else { CheckModel(); CheckFullInventory(); await CheckUi(); }
            passed = true;
        }
        catch (Exception error) { failure = error.ToString(); GD.PushError(failure); }
        finally
        {
            if (GodotObject.IsInstanceValid(_room) && !_room.IsQueuedForDeletion()) _room.QueueFree();
            foreach (var fixture in _fixtures.Where(GodotObject.IsInstanceValid))
                if (fixture.IsInsideTree()) fixture.QueueFree(); else fixture.Free();
            if (GodotObject.IsInstanceValid(_location) && !_location.IsQueuedForDeletion()) _location.QueueFree();
            await Frames(); WriteReport(passed, failure);
            GD.Print($"TALISMAN_NATIVE_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}");
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
