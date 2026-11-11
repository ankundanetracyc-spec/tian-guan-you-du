using Godot;
using System.Reflection;
using GArray = Godot.Collections.Array;

/// <summary>真实拓印／空间领取事务、存读档与峰内原生入口验证；仅使用隔离测试存档。</summary>
public partial class YuxuManualLibrarySmoke : Node
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Stone = "res://resources/entities/items/灵石.tres";
    private const string ArtFolder = "res://docs/美术规范/玄览峰拓印接入_20261107/";
    private int _checks;
    private readonly List<Character> _actors = [];
    private void Check(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags)!.Invoke(owner, args);
    private async Task Frames(int count = 4) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private Character Player(string rank = "外门弟子")
    {
        var p = new Character { entity_data = new HumanData { entity_name = "藏经阁验证", sect_affiliation_id = "yuxu_xianzong", sect_current_rank = rank } };
        _actors.Add(p); return p;
    }
    private YuxuManualCopy.Request Prepare(Character p, YuxuManualCopy.Entry e)
    {
        Check(YuxuManualCopy.TryPrepare(p, e.Book.ResourcePath, "1年1月1日", out var request, out var error), error); return request!;
    }
    private ItemClaimPanel Receipt(Character p, YuxuManualCopy.Request request)
    {
        var panel = new ItemClaimPanel(); AddChild(panel);
        Check(panel.Present(p, request.Rewards(), "玄览峰拓印", beforeCommit: request.BeforeCommit), "不能打开真实领取页"); return panel;
    }
    private async Task Commit(ItemClaimPanel panel, bool place = true)
    {
        if (place) Call(panel, "auto_place"); Call(panel, "commit_receipt"); await Frames();
    }
    private static void Seed(HumanData h, int amount, string id) => h.record_yuxu_contribution(new YuxuSectContributionRecord
    {
        record_id = id, source_transaction_id = id, transaction_kind = YuxuSectContributionRecord.KindOfficialDutyReward,
        source_title = "测试履职", occurred_at = "1年1月1日", contribution_delta = amount, cost_summary = "测试", result_summary = "测试"
    });
    private async Task Click(Control control)
    {
        var p = control.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p }, true);
        foreach (var down in new[] { true, false }) GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, Pressed = down, ButtonIndex = MouseButton.Left }, true);
        await Frames(4); await ToSignal(GetTree().CreateTimer(.28), SceneTreeTimer.SignalName.Timeout);
    }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture") || DisplayServer.GetName() == "headless") return;
        await Frames(8); HoverInfoManager.HideAll(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ArtFolder + name + ".png") == Error.Ok, "截图失败");
    }
    private async Task Transactions()
    {
        var books = YuxuManualCopy.Catalog(); Check(books.Count == 5 && books.All(e => e.Item.gongfa?.ResourcePath == e.Book.ResourcePath), "书目必须是五本真实开放秘籍");
        Check(!YuxuExchangeCatalog.Categories.Contains("秘籍") && YuxuExchangeCatalog.LoadItems().All(i => i.gongfa is null), "善功堂仍售秘籍");
        foreach (var rank in new[] { "杂役弟子", "外门弟子", "内门弟子", "真传弟子" })
        {
            var member = Player(rank); var r = Prepare(member, books[0]);
            Check(r.QuotedCost == 0 && member.get_inventory_total_count(books[0].Item.ResourcePath) == 0, rank + "未获首本免费或选书提前发物品");
        }
        var outsider = Player(); ((HumanData)outsider.entity_data!).sect_affiliation_id = "";
        Check(!YuxuManualCopy.TryPrepare(outsider, books[0].Book.ResourcePath, "测试", out _, out _), "非弟子可拓印");
        var p = Player(); var h = (HumanData)p.entity_data!; Seed(h, 4, "seed");
        var first = Prepare(p, books[1]); var panel = Receipt(p, first); Call(panel, "auto_place");
        Check(p.get_inventory_total_count(books[1].Item.ResourcePath) == 0 && h.sect_library_first_manual_path == "" && h.get_yuxu_contribution_total() == 4, "自动整理提前发书或结算");
        int notices = 0; p.ItemAcquired += (_, _, count) => notices += count;
        Action<Character> reenter = _ => Call(panel, "commit_receipt");
        p.InventoryChanged += reenter; // 回调重入也只能结算一次。
        await Commit(panel, false);
        Check(p.get_inventory_total_count(books[1].Item.ResourcePath) == 1 && h.sect_library_first_manual_path == books[1].Book.ResourcePath && h.get_yuxu_contribution_total() == 4 && notices == 1, "首本领取不原子或重入重复");
        // 后续不再触发上面的重入探针。
        p.InventoryChanged -= reenter;
        Check(p.main_gongfa is null && h.learned_gongfa_paths.Count == 0 && p.get_known_gongfas().Any(b => b.ResourcePath == books[1].Book.ResourcePath), "持书未进入研读目录或提前学会／主修");
        Check(first.BeforeCommit(first.Rewards()) is not null && h.get_yuxu_contribution_total() == 4, "同一结算重复执行");
        var paid = Prepare(p, books[1]); Check(paid.QuotedCost == 1, "后续测试价不是1贡献");
        panel = Receipt(p, paid); await Commit(panel);
        Check(p.get_inventory_total_count(books[1].Item.ResourcePath) == 2 && h.get_yuxu_contribution_total() == 3 && h.get_yuxu_official_service_total() == 4, "重复拓印未按实物计价或消费损害累计履职");
        var savePath = "user://manual_copy_roundtrip.tres";
        Check(ResourceSaver.Save((HumanData)h.Duplicate(true), savePath) == Error.Ok, "首本存档写入失败");
        var loaded = ResourceLoader.Load<HumanData>(savePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        Check(loaded.sect_library_first_manual_path == h.sect_library_first_manual_path && loaded.get_yuxu_contribution_total() == 3 && loaded.inventory_item_counts.Sum() == 2, "首本、实物或流水未存读档");
        var abandoned = Player(); panel = Receipt(abandoned, Prepare(abandoned, books[0])); await Commit(panel, false);
        Check(((HumanData)abandoned.entity_data!).sect_library_first_manual_path == "" && abandoned.get_spatial_inventory().Count == 0, "放弃首册仍消费机会");
        var full = Player(); Check(full.add_item_to_inventory(Stone, Character.InventorySlotCapacity), "满包样本失败");
        panel = Receipt(full, Prepare(full, books[0])); Call(panel, "auto_place");
        Check(Read<List<SpatialInventoryEntry>>(panel, "_layout").Count == Character.InventorySlotCapacity && ((HumanData)full.entity_data!).sect_library_first_manual_path == "", "满包覆盖库存或消费机会");
        await Commit(panel, false); Check(full.get_inventory_total_count(Stone) == Character.InventorySlotCapacity, "放弃满包书册改动库存");
        var stale = Player(); var staleRequest = Prepare(stale, books[0]); panel = Receipt(stale, staleRequest); Call(panel, "auto_place"); stale.add_item_to_inventory(Stone);
        Call(panel, "commit_receipt"); Check(!staleRequest.Settled && ((HumanData)stale.entity_data!).sect_library_first_manual_path == "" && stale.get_inventory_total_count(books[0].Item.ResourcePath) == 0, "库存变化仍提交拓印");
        await Commit(panel); Check(staleRequest.Settled && stale.get_inventory_total_count(books[0].Item.ResourcePath) == 1, "库存重读后不能重试领取");
        var poor = Player(); var poorHuman = (HumanData)poor.entity_data!; poorHuman.sect_library_first_manual_path = books[0].Book.ResourcePath; Seed(poorHuman, 1, "poor_seed");
        var poorRequest = Prepare(poor, books[2]); panel = Receipt(poor, poorRequest); Call(panel, "auto_place"); poorHuman.remove_yuxu_contribution("poor_seed"); Call(panel, "commit_receipt");
        Check(!poorRequest.Settled && poor.get_spatial_inventory().Count == 0 && poorHuman.get_yuxu_contribution_total() == 0, "余额变化仍发书或扣成负数");
        await Commit(panel, false); Check(poorRequest.Settled && poorHuman.get_yuxu_contribution_total() == 0, "付费书放弃仍扣费");
        var repriced = Player(); var repricedHuman = (HumanData)repriced.entity_data!; var stalePrice = Prepare(repriced, books[0]); panel = Receipt(repriced, stalePrice); Call(panel, "auto_place"); repricedHuman.sect_library_first_manual_path = books[1].Book.ResourcePath; Call(panel, "commit_receipt");
        Check(!stalePrice.Settled && repriced.get_spatial_inventory().Count == 0, "价格变化仍沿旧价提交"); await Commit(panel, false);
        // 领取成功后丢弃物品不恢复免费记录。
        Check(p.try_exchange_inventory(new Dictionary<string, int> { [books[1].Item.ResourcePath] = 2 }, new Dictionary<string, int>()) && YuxuManualCopy.Cost(h) == 1, "丢弃已领物品恢复免费机会");
    }
    private async Task NativeFlow()
    {
        await Frames(); GetTree().CurrentScene = null;
        var profile = GetNode<GameStartProfile>("/root/GameStartProfile"); profile.roll_fated_aptitude();
        profile.fated_youngest_root_type = 1; profile.fated_youngest_root_name = "金灵根"; profile.fated_youngest_root_weights = new GArray { 100, 0, 0, 0, 0 };
        profile.fated_base_physique = profile.fated_base_intelligence = profile.fated_base_soul = 5;
        GetNode<GameSaveManager>("/root/GameSaveManager").begin_new_game("玄览峰拓印原生验证");
        var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>(); GetTree().Root.AddChild(main); GetTree().CurrentScene = main;
        for (int i = 0; i < 600 && !main.StartupComplete; i++) await Frames(1);
        Check(main.StartupComplete && main.StartupError is null, "正式主场景启动失败"); await Frames(12);
        var actor = Read<Character>(main, "player"); var human = (HumanData)actor.entity_data!;
        var dialogue = Read<StoryDialoguePanel>(main, "story_dialogue"); for (int i = 0; i < 20 && dialogue.Visible; i++) dialogue.close();
        var state = Read<BaishiLectureProgress>(main, "_baishiLecture"); state.finished = true; state.departed_qingxi = true;
        human.sect_affiliation_id = "yuxu_xianzong"; human.sect_current_rank = "外门弟子"; human.sect_entry_rank = "外门弟子";
        typeof(MainScene).GetField("_player_world_node_id", Flags)!.SetValue(main, "yuxu_xianzong_core");
        GetNode<GameTimeMgr>("/root/GameTimeManager").is_paused = true;
        Call(main, "open_yuxu_peak_after_admission", "xuanlan"); await Frames(16);
        var overview = main.GetNode<Control>("SceneLayer/YuxuXianzongOverview"); var peak = overview.GetNode<Control>("PeakInterior");
        var entry = (Button)peak.Get("_action_buttons").AsGodotDictionary()["lineage_book_selection"].AsGodotObject();
        Check(entry.IsVisibleInTree() && entry.Text.Replace("\n", "") == "选书拓印", "外门弟子缺少拓印挂牌");
        await Click(entry); var library = Read<YuxuManualLibraryPanel>(main, "_yuxuManualLibrary");
        Check(library.IsVisibleInTree(), "实际挂牌未打开原生书架"); await Frames(20);
        var spines = library.FindChildren("Manual_*", "Button", true, false).OfType<YuxuManualSpine>().ToArray();
        Check(spines.Length == 5 && spines.All(s => s.Size == YuxuManualSpine.SpineSize && s.GetMeta("title_font_size").AsInt32() <= 18), "书脊尺寸或字号不统一");
        Check(spines.All(s => s.GetNode<Control>("SpineVisual").GetChildren().OfType<TextureRect>().All(t => t.Size == YuxuManualSpine.SpineSize)), "物品图标原尺寸撑破了书脊");
        Check(spines.All(s => s.FindChildren("*", "Label", true, false).OfType<Label>().All(l => l.Size.Y <= 155)), "书名字号适配后仍超出题签");
        var actualRows = YuxuManualRequirements.For(actor, YuxuManualCopy.Catalog()[1].Book, YuxuManualCopy.Catalog()[1].Book.get_sorted_chapters().First(), false);
        Check(actualRows.Any(r => r.Text == "体魄 ≥ 5" && r.Met && r.Current.Contains("5")), "勾叉未读实际角色");
        await Capture("01_藏经阁_1920");
        var before = spines.ToDictionary(s => s, s => s.Position);
        var keyword = library.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "应对"); await Click(keyword);
        Check(spines.Count(s => s.GetNode<Control>("SpineVisual").Modulate.R < .6f) == 4 && spines.All(s => s.Position == before[s]), "关键词未变暗或挪动了书");
        await Capture("02_关键词变暗");
        var dim = spines.First(s => s.GetNode<Control>("SpineVisual").Modulate.R < .6f); await Click(dim);
        Check(dim.GetNode<Control>("SpineVisual").Position.DistanceTo(new(-5, -14)) < .1f && spines.All(s => s.Position == before[s]), "暗项不能抽出或改变占位");
        var all = library.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "全部"); await Click(all);
        var taihe = spines.Single(s => s.GetMeta("book_path").AsString().Contains("太和感物养元宝册")); await Click(taihe);
        var chapters = library.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "卷章"); await Click(chapters); await Capture("03_卷章条件");
        await Click(library.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "简介"));
        await Click(library.FindChild("CopyManual", true, false) as Button ?? throw new Exception("缺少拓印按钮"));
        Check(main.has_active_item_claim() && GetTree().Paused && human.sect_library_first_manual_path == "", "点击拓印没有先进入领取");
        var claim = Read<ItemClaimPanel>(main, "_active_item_claim"); await Capture("04_拓印待领取");
        await Click(claim.FindChild("ClaimAutoPlace", true, false) as Button ?? throw new Exception("缺少自动整理按钮"));
        Check(human.sect_library_first_manual_path == "", "领取预览消耗首本机会"); await Capture("05_秘籍放入背囊");
        await Click(claim.FindChild("ClaimFooterPrimary", true, false) as Button ?? throw new Exception("缺少确认领取按钮"));
        Check(!main.has_active_item_claim() && !GetTree().Paused && human.sect_library_first_manual_path.Contains("太和感物养元宝册") && actor.get_inventory_total_count(YuxuManualCopy.Catalog()[1].Item.ResourcePath) == 1, "主场景领取未提交");
        Check(Read<Label>(library, "_price").Text == "1 贡献", "领取后报价没有更新"); await Capture("06_领取后价格");
        library.Hide();
        var study = Read<CultivationUI>(main, "cultivation_ui"); study.show_teaching_for(actor); await Frames();
        var heldBook = YuxuManualCopy.Catalog()[1].Book;
        Check(Read<List<GongfaResource>>(study, "_studyCandidates").Any(b => b.ResourcePath == heldBook.ResourcePath), "领取的实物没有进入实际研读页面");
        Check(actor.select_gongfa_for_study(heldBook) && !actor.is_gongfa_chapter_learned(heldBook, heldBook.get_sorted_chapters().First().get_id()), "领取后不能正常开始研读，或开读即算学会");
        actor.remove_from_study_slot(0);
        Check(!actor.select_gongfa_for_study(YuxuManualCopy.Catalog()[2].Book), "灵根／属性不足仍绕过首卷门槛");
        study.close_panel(); library.Show();
        var snapshot = main.capture_battle_world_snapshot()!;
        Check(snapshot.player_data?.sect_library_first_manual_path == human.sect_library_first_manual_path, "正式快照未保存首本记录");
        var save = GetNode<GameSaveManager>("/root/GameSaveManager"); Check(save.save_snapshot(snapshot) && save.load_slot(save.active_slot_id), "正式槽位存读失败");
        GetWindow().Size = new(1280, 720); await Frames(12); await Capture("07_藏经阁_1280");
        Check(main.try_return_to_previous_page() && !library.Visible && overview.Visible && peak.Visible, "返回没有先关闭书架");
        var exchange = new YuxuSectExchangePanel(); main.GetNode<CanvasLayer>("CanvasLayer").AddChild(exchange); exchange.open_panel(10); GetWindow().Size = new(1920, 1080); await Frames(12); await Capture("08_善功堂_1920"); exchange.QueueFree();
        main.prepare_return_to_title_from_menu(false); main.QueueFree(); await Frames(8);
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("manual_library", StringComparison.OrdinalIgnoreCase), "必须使用隔离测试存档目录");
            GetWindow().Size = new(1920, 1080); await Frames(); await Transactions(); await NativeFlow();
            foreach (var actor in _actors) actor.Free();
            GD.Print($"YUXU_MANUAL_LIBRARY_OK: {_checks} checks; real scene entry, actual attributes, physical free/paid copy, claim/abandon/full/stale/reentry, save roundtrip, native UI"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
