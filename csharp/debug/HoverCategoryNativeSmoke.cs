using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>Read-only representative coverage. A content sample is never counted as its entire screen's entry flow.</summary>
public partial class HoverCategoryNativeSmoke : Control
{
    private const string Evidence = "res://docs/美术规范/全项悬停复核_20261109/";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private sealed record Finding(string Name, bool Passed, string Detail);
    private readonly List<Finding> _checks = [];
    private readonly List<object> _captures = [];
    private readonly List<object> _pageEvidence = [];
    private readonly List<object> _geometry = [];
    private readonly List<string> _limitations = [];
    private Character _owner = null!;
    private Control _card = null!;
    private Button _anchor = null!;
    private Label _description = null!;
    private Vector2 _pointer;
    private string _resolution = "";

    public override void _Ready() => CallDeferred(nameof(Run));
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Private)!.GetValue(value)!;
    private static object? Invoke(object value, string name, params object?[] args) =>
        value.GetType().GetMethod(name, Private)!.Invoke(value, args);
    private static IEnumerable<T> Nodes<T>(Node node) where T : Node => node.FindChildren("*", "", true, false).OfType<T>();
    private static string Plain(string text) => Regex.Replace(text, @"\[/?(?:b|i|u|s|color|font|font_size|url)(?:=[^\]]*)?\]", "");
    private static string Compact(string text) => Regex.Replace(Plain(text), @"\s+", "");
    private static Rect2 Bounds(Control control)
    {
        var transform = control.GetGlobalTransformWithCanvas(); var rect = new Rect2(transform * Vector2.Zero, Vector2.Zero);
        foreach (var point in new[] { new Vector2(control.Size.X, 0), control.Size, new Vector2(0, control.Size.Y) }) rect = rect.Expand(transform * point);
        return rect;
    }
    private string SharedText() => string.Join("\n\n", Nodes<RichTextLabel>(_card).Where(node => node.IsVisibleInTree()).Select(node => node.GetParsedText()));
    private void Check(bool passed, string name, object? detail = null)
    {
        _checks.Add(new(name, passed, detail?.ToString() ?? ""));
        if (!passed) GD.Print("CATEGORY_CHECK_FAILED: " + name + "; " + detail);
    }
    private void Move(Vector2 point)
    {
        _pointer = point;
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
    }
    private async Task Frames(int count = 12)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Move(_pointer);
        }
    }
    private async Task NativeSource(Control source, string id)
    {
        Move(Bounds(source).GetCenter()); await Frames(2);
        var hit = GetViewport().GuiGetHoveredControl();
        Check(source.IsVisibleInTree() && source.MouseFilter != MouseFilterEnum.Ignore && hit is not null
            && (hit == source || source.IsAncestorOf(hit)), id + ": actual viewport pointer hits the production source", hit?.GetPath());
    }
    private async Task Attempt(string name, Func<Task> work)
    {
        try { await work(); }
        catch (Exception error) { Check(false, name + ": representative sample could not complete", error.GetBaseException().Message); }
        finally { HoverInfoManager.HideAll(); Move(new(4, 4)); await Frames(2); }
    }
    private bool Fits(Control card, string name)
    {
        var rect = Bounds(card); var viewport = GetViewport().GetVisibleRect();
        var fits = card.IsVisibleInTree() && card.Modulate.A > .99f && viewport.Grow(1).Encloses(rect);
        Check(fits, name + ": complete native card fits actual viewport", rect);
        return fits;
    }
    private async Task Capture(Control card, string id, string category, string scope, string source, string origin, string text)
    {
        for (var frame = 0; frame < 45 && (!card.IsVisibleInTree() || card.Modulate.A < .99f); frame++) await Frames(1);
        var complete = Fits(card, id);
        if (!card.IsVisibleInTree()) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var full = GetViewport().GetTexture().GetImage();
        var rect = Bounds(card);
        var region = new Rect2I((Vector2I)rect.Position.Floor(), (Vector2I)rect.Size.Ceil()).Intersection(new(Vector2I.Zero, full.GetSize()));
        using var crop = full.GetRegion(region);
        var stem = _resolution + "/" + id;
        Check(full.SavePng(Evidence + stem + "_full.png") == Error.Ok && crop.SavePng(Evidence + stem + "_hover.png") == Error.Ok,
            id + ": saved actual rendered view and card");
        _captures.Add(new { id, category, scope, source, origin, text, complete_in_viewport = complete, width = rect.Size.X, height = rect.Size.Y,
            card = stem + "_hover.png", full = stem + "_full.png" });
    }
    private async Task CapturePage(string id, string source, string boundary)
    {
        HoverInfoManager.HideAll(); await Frames(2);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var full = GetViewport().GetTexture().GetImage();
        var file = _resolution + "/" + id + "_page.png";
        Check(full.SavePng(Evidence + file) == Error.Ok, id + ": actual page evidence saved separately from hover samples");
        _pageEvidence.Add(new { id, source, boundary, image = file });
    }
    private void VerifyShared(string id, HoverInfoManager.HoverInfoContent info)
    {
        var text = SharedText();
        Check(Compact(text) == Compact(PlayerInfoText.Display(info.Body)), id + ": every actual source character appears once", text);
        Check(!Regex.IsMatch(text, @"[，,]\s*计算\s*[：:]|取(?:较|最)小值|\b(?:round|floor|ceil)\s*[(（]"),
            id + ": canonical sources and min/max spelling", text);
        var expectedSources = PlayerInfoText.Mode == PlayerInfoText.InformationMode.Detailed ? PlayerInfoText.CalculationSources(info.Body).Count : 0;
        Check(Regex.Matches(text, "计算来源").Count == (expectedSources > 0 ? 1 : 0), id + ": only the appropriate final source block");
        if (expectedSources > 0)
        {
            var sourceText = text[(text.IndexOf("计算来源", StringComparison.Ordinal) + "计算来源".Length)..];
            Check(!Regex.IsMatch(sourceText, @"四舍五入|向上取整|向下取整"), id + ": final sources do not expose internal rounding instructions", sourceText);
        }
        Check(Nodes<ScrollContainer>(_card).All(scroll => scroll.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled
            && scroll.HorizontalScrollMode == ScrollContainer.ScrollMode.Disabled), id + ": main hover has no scrolling");
        Check(Nodes<InventoryFootprintView>(_card).All(grid => grid.MouseFilter == MouseFilterEnum.Ignore), id + ": occupancy diagrams pass through input");
        Check(Nodes<RichTextLabel>(_card).Where(node => node.IsVisibleInTree()).All(node => node.GetThemeFontSize("normal_font_size") >= 16),
            id + ": source remains readable");
    }
    private async Task ContentSample(string id, string category, string source, string definition, Func<HoverInfoManager.HoverInfoContent> generate)
    {
        await Attempt(id, async () =>
        {
            _description.Text = category + "：真实生成器内容样本；独立来源按钮，不代称正式页面入口已走通。";
            _anchor.Show();
            _anchor.Text = category;
            _anchor.Position = new(GetViewport().GetVisibleRect().Size.X - 180, GetViewport().GetVisibleRect().Size.Y - 70);
            foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
            {
                PlayerInfoText.SetMode(mode, persist: false);
                HoverInfoManager.HideAll();
                Move(_anchor.GetGlobalRect().GetCenter());
                var info = generate(); HoverInfoManager.SetInfo(_anchor, info, true); await Frames();
                var key = id + (mode == PlayerInfoText.InformationMode.Simple ? "_简略" : "_详细");
                VerifyShared(key, info);
                await Capture(_card, key, category, "分类内容样本", source, definition, SharedText());
            }
        });
    }
    private string OwnerStamp() => JsonSerializer.Serialize(new
    {
        health = _owner.current_health, energy = _owner.current_world_stamina,
        inventory = _owner.get_spatial_inventory().Select(entry => new { entry.InstanceId, entry.ItemPath, entry.RegionId, entry.X, entry.Y, entry.Rotation }).ToArray(),
        used_spirit = _owner.used_spirit_perception, soul = _owner.soul, physique = _owner.physique,
    });
    private async void Run()
    {
        var original = PlayerInfoText.Mode;
        try
        {
            var large = OS.GetCmdlineUserArgs().Contains("--large");
            var size = large ? new Vector2I(1920, 1080) : new Vector2I(1280, 720); _resolution = size.X + "x" + size.Y;
            GetWindow().ContentScaleSize = size; GetWindow().Size = size;
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence + _resolution));
            var isolated = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://")).StartsWith(
                System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://tmp/hover_category_20261109/")), StringComparison.OrdinalIgnoreCase);
            if (!isolated) throw new Exception("Only the runner's isolated user data may be used.");
            Check(true, "all autoload/settings/recipe access isolated from player data", ProjectSettings.GlobalizePath("user://"));
            var background = new ColorRect { Color = new("e5ddc7"), MouseFilter = MouseFilterEnum.Ignore }; AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _description = new Label { Position = new(26, 18), Text = "全项悬停分类复核 · 实际来源与覆盖边界分开记录", MouseFilter = MouseFilterEnum.Ignore };
            _description.AddThemeFontSizeOverride("font_size", 17); _description.AddThemeColorOverride("font_color", new("4b5142")); AddChild(_description);
            _anchor = new Button { Size = new(150, 48) }; AddChild(_anchor);
            _owner = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData
            {
                unique_id = "hover-category-isolated", entity_name = "分类复核人物", cultivation_realm = "云息三层",
                physique = 46.51f, soul = 28.65f, intelligence = 20, base_physique = 15, base_intelligence = 15, base_soul = 15,
                alchemy_proficiency = 36, base_alchemy_proficiency = 36, talisman_proficiency = 64, base_talisman_proficiency = 64,
                weapon_path = "res://resources/entities/equipment/素行剑.tres", current_health = 100, max_health = 100,
            } }; AddChild(_owner); _owner.Hide();
            _owner.max_spirit_perception = 100; _owner.current_world_stamina = _owner.max_world_stamina = 100;
            var catalog = TalismanCraftingCatalog.DiscoverMaterials();
            foreach (var material in catalog.Where(material => material.Texture is "纤维" or "液体" or "黏胶").GroupBy(material => material.Texture).SelectMany(group => group.Take(3)))
                _owner.add_item_to_inventory(material.Path, 2);
            var stamp = OwnerStamp();
            _card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            await ItemSamples();
            await Attempt("真实状态徽标", StatusEntries);
            await Attempt("人物与穿戴", CharacterEntries);
            await Attempt("阵法部件", FormationEntries);
            await Attempt("林地资源与事件", WorldEntries);
            await Attempt("正式炼丹独立卡", AlchemyEntries);
            Check(OwnerStamp() == stamp, "read-only hover leaves isolated owner health stamina inventory and attributes unchanged");
            _limitations.Add("分类内容样本只证明生产生成器输出及共用渲染；没有逐一完成交易、坊市、兑换、领取等页面业务流程。");
            _limitations.Add("本轮未遍历全部随机事件、全部地图地点、所有状态组合及所有NPC知识状态；未操作玩家旅程或执行服药、购买、炼制。");
            var failed = _checks.Count(check => !check.Passed);
            var report = new { status = failed == 0 ? "passed_representative_samples" : "partial", resolution = _resolution,
                checks_passed = _checks.Count(check => check.Passed), checks_failed = failed, checks = _checks, captures = _captures, page_evidence = _pageEvidence, geometry = _geometry,
                limitations = _limitations, player_save_read = false, player_save_written = false, trade_or_consumption_executed = false };
            using (var file = Godot.FileAccess.Open(Evidence + _resolution + "/verification.json", Godot.FileAccess.ModeFlags.Write))
                file.StoreString(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            GD.Print($"HOVER_CATEGORY_{(failed == 0 ? "OK" : "PARTIAL")}: {_checks.Count - failed} passed; {failed} failed; {_captures.Count} captures; {_resolution}");
            HoverInfoManager.HideAll(); GetTree().Quit(failed == 0 ? 0 : 1);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(original, persist: false); }
    }
}
