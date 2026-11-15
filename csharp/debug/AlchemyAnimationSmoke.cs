using Godot;
using System.Reflection;

/// <summary>用隔离人物检查完整演出、各阶段跳过与真实领取，不读写旅程存档。</summary>
public partial class AlchemyAnimationSmoke : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _checks, _frame;
    private string _capture = "";
    private ulong _nextCapture;
    private ulong _captureInterval = 80;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden)!.GetValue(target)!;
    private static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Hidden)!.Invoke(target, args);
    private void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        _checks++;
    }
    private async Task Frames(int count = 1)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Until(Func<bool> ready, string label, int timeout = 20000)
    {
        ulong end = Time.GetTicksMsec() + (ulong)timeout;
        while (!ready() && Time.GetTicksMsec() < end) await Frames();
        Check(ready(), "等待超时：" + label);
    }
    private async Task Delay(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static IEnumerable<Control> Children(AlchemySlotBrewPreviewScene room) =>
        Field<Control?>(room, "_brewStage")?.GetChildren().OfType<Control>() ?? [];
    private static IEnumerable<Control> Pills(AlchemySlotBrewPreviewScene room) =>
        Children(room).Where(c => c.HasMeta("pill_item_path"));
    private static Button? Confirm(AlchemySlotBrewPreviewScene room) =>
        Children(room).OfType<Button>().FirstOrDefault(b => b.Text.StartsWith("确定 ·") && b.IsVisibleInTree());
    private static void Skip(AlchemySlotBrewPreviewScene room) =>
        Children(room).OfType<Button>().Single(b => b.Text.StartsWith("跳过演出")).EmitSignal(BaseButton.SignalName.Pressed);

    public override void _Process(double delta)
    {
        if (_capture.Length == 0 || DisplayServer.GetName() == "headless" || Time.GetTicksMsec() < _nextCapture) return;
        _nextCapture = Time.GetTicksMsec() + _captureInterval;
        using var frame = GetViewport().GetTexture().GetImage();
        frame.SaveJpg($"{_capture}/frame{_frame++:D4}.jpg", .92f);
    }

    private async Task Run(string mode)
    {
        var route = AlchemyRecipeCatalog.Pills.Single(p => p.Id == "huichun").SampleRoutes.Single(r => r.Name == "青木清露方");
        var actor = new Character { ProcessMode = ProcessModeEnum.Disabled, entity_data = new HumanData {
            entity_name = "炼丹动画验收", cultivation_realm = "云息三层", base_alchemy_proficiency = 18,
            alchemy_proficiency = 18, main_gongfa_path = GongfaAvailability.DefaultBookPath
        } };
        AddChild(actor); actor.Hide(); actor.current_world_stamina = actor.max_world_stamina = 100;
        actor.max_spirit_perception = 24; actor.used_spirit_perception = 0;
        var inputs = route.Ingredients.Select(i => {
            string path = $"res://resources/entities/items/{i.HerbName}.tres";
            var role = i.AssistantSlot == -1 ? AlchemySlotSimulation.SlotRole.Jun : i.AssistantSlot == 0
                ? AlchemySlotSimulation.SlotRole.Chen : i.AssistantSlot is 1 or 2 or 4
                    ? AlchemySlotSimulation.SlotRole.Zuo : AlchemySlotSimulation.SlotRole.Shi;
            return new AlchemySlotSimulation.Input(i.HerbName, path,
                (MaterialCraftData)GD.Load<ItemData>(path).craft_data!, i.Dose, role);
        }).ToArray();
        if (mode != "rejected") foreach (var input in inputs)
            Check(actor.add_item_to_inventory(input.Path, input.Dose), "验收材料入包失败");
        var room = new AlchemySlotBrewPreviewScene { OwnerCharacter = actor };
        AddChild(room); room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await Frames(3);
        Field<Control>(room, "_tutorialOverlay").Hide();
        Invoke(room, "ApplySampleRoute", route, false);
        room.GetType().GetField("_simpleAlchemyPresentation", Hidden)!.SetValue(room, mode == "simple");
        var expected = AlchemySlotSimulation.Simulate(inputs, actor.AlchemyAttainment, 24);
        var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
        double started = clock.get_precise_game_hours();
        float energy = actor.current_world_stamina;
        if (_capture.Length > 0) await Delay(.35);
        var brew = (Task)Invoke(room, "BrewWithPresentation")!;
        if (mode == "rejected")
        {
            await brew;
            Check(Field<Control?>(room, "_brewStage") is null && energy == actor.current_world_stamina &&
                started == clock.get_precise_game_hours(), "预检拒绝仍播放演出或扣资源");
        }
        else
        {
            await (Task)Invoke(room, "BrewWithPresentation")!;
            var hiddenUi = Field<List<CanvasItem>>(room, "_presentationHiddenUi");
            var originalUi = hiddenUi.ToArray();
            Check(hiddenUi.Count > 0 && hiddenUi.All(c => !c.Visible) &&
                Field<AlchemyFurnaceView>(room, "_furnace").Visible, "演出中周围界面未收起或误隐藏丹炉");
            Check(inputs.All(i => actor.get_inventory_total_count(i.Path) == 0) &&
                actor.current_world_stamina == energy - expected.EnergyNeed, "重复开炉导致材料或精力重复扣除");
            Check(Math.Abs(clock.get_precise_game_hours() - started - 2 * AlchemySlotSimulation.DurationHours(inputs)) < 1e-6,
                "演出改变正式炼制耗时");
            if (mode == "skip-input") { await Delay(.2); Skip(room); }
            else if (mode == "skip-fire")
            {
                await Until(() => !Children(room).Any(c => c.HasMeta("alchemy_flying_ingredient")), "投料融入完成");
                await Delay(.15); Skip(room);
            }
            else if (mode == "skip-melt")
            {
                await Until(() => Children(room).OfType<AlchemyIngredientFlow>().Any(c =>
                    Field<float>(c, "_progress") is > .62f and < .9f), "药材化开中途");
                Skip(room);
            }
            else if (mode == "skip-pill")
            {
                await Until(() => Pills(room).Any(c => c.Scale.X < .95f), "出丹中途"); Skip(room);
            }
            else
            {
                await Until(() => Confirm(room) is not null, "散丹鉴看");
                var revealed = Pills(room).ToArray();
                Check(revealed.Length == expected.OutputCount && revealed.All(c => c.Scale == Vector2.One &&
                    c.Modulate == Colors.White && c.Rotation == 0 && c.Position == c.GetMeta("settled_position").AsVector2()),
                    "散丹数量、透明度或落点错误");
                Check(revealed.Select(c => c.GetMeta("pill_score").AsInt32()).SequenceEqual(mode == "simple"
                    ? expected.Pills.Where(p => p.Quality >= 20).Select(p => p.Quality).OrderDescending()
                    : expected.Pills.Where(p => p.Quality >= 20).Select(p => p.Quality)), "展示模式改变了丹效或简洁排序");
                if (mode == "skip-inspect") Skip(room);
                else
                {
                    if (_capture.Length > 0) await Delay(.8);
                    Confirm(room)!.EmitSignal(BaseButton.SignalName.Pressed);
                    if (mode == "skip-bottle")
                    {
                        await Delay(.5);
                        var bottles = Children(room).OfType<TextureRect>().Where(c => c.HasMeta("animation_bottle_source")).ToArray();
                        Check(bottles.Length > 0 && bottles.All(c => c.Texture.ResourcePath == c.GetMeta("animation_bottle_source").AsString()),
                            "装瓶没有使用当前行囊闭瓶图");
                        Skip(room);
                    }
                    if (mode == "skip-stopper")
                    {
                        await Until(() => Children(room).Any(c => c.HasMeta("bottle_stopper")), "瓶塞下落"); Skip(room);
                    }
                }
            }
            await Until(() => brew.IsCompleted, "进入领取"); await brew; await Frames(2);
            var claim = Field<ItemClaimPanel?>(room, "_alchemyClaim");
            Check(claim is not null, "没有进入通用领取页");
            Check(!Children(room).Any(c => c.HasMeta("alchemy_flying_ingredient") || c.HasMeta("bottle_stopper")) &&
                Field<List<Tween>>(room, "_presentationTweens").Count == 0, "跳过或结束后仍有飞行副本或活动动画");
            foreach (var group in expected.Pills.Where(p => p.Quality >= 20).GroupBy(p => AlchemyQualityRules.Tier(p.Quality)))
                Check(actor.get_inventory_total_count(AlchemyProductCatalog.Path(expected.ProductId, group.Key)) == 0,
                    "领取前已发放丹药");
            if (_capture.Length > 0) await Delay(.7);
            claim!.FindChild("ClaimAutoPlace", true, false)!.Call("emit_signal", "pressed");
            claim.FindChild("ClaimFooterPrimary", true, false)!.Call("emit_signal", "pressed"); await Frames(3);
            foreach (var group in expected.Pills.Where(p => p.Quality >= 20).GroupBy(p => AlchemyQualityRules.Tier(p.Quality)))
                Check(actor.get_inventory_total_count(AlchemyProductCatalog.Path(expected.ProductId, group.Key)) == group.Count(),
                    "正式领取的档位或颗数错误");
            Check(Field<Control?>(room, "_brewStage") is null && Field<TextureRect>(room, "_mainIcon").Modulate == Colors.White &&
                hiddenUi.Count == 0 && originalUi.All(c => IsInstanceValid(c) && c.Visible),
                "领取后未恢复配伍界面");
        }
        room.QueueFree(); actor.QueueFree(); await Frames(3);
        GD.Print($"ALCHEMY_ANIMATION_OK {mode} checks={_checks}");
    }

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            var args = OS.GetCmdlineUserArgs();
            if (DisplayServer.GetName() != "headless")
            {
                GetWindow().Mode = Window.ModeEnum.Windowed;
                GetWindow().Size = new Vector2I(1280, 720);
                GetWindow().Unfocusable = true;
            }
            _capture = args.FirstOrDefault(a => a.StartsWith("--animation-capture="))?.Split('=', 2)[1] ?? "";
            var interval = args.FirstOrDefault(a => a.StartsWith("--animation-capture-interval="))?.Split('=', 2)[1];
            if (ulong.TryParse(interval, out var captureMs)) _captureInterval = Math.Max(20UL, captureMs);
            if (_capture.Length > 0) DirAccess.MakeDirRecursiveAbsolute(_capture);
            string? mode = args.FirstOrDefault(a => a.StartsWith("--animation-mode="))?.Split('=', 2)[1];
            foreach (var scenario in mode is null
                ? new[] { "full", "skip-input", "skip-melt", "skip-fire", "skip-pill", "skip-inspect", "skip-bottle", "skip-stopper", "simple", "rejected" }
                : new[] { mode }) await Run(scenario);
            _capture = ""; GD.Print($"ALCHEMY_ANIMATION_PASS checks={_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }
}
