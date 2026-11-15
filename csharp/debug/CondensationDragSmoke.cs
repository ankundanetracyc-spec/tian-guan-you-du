using Godot;
using System.Reflection;

/// <summary>用真实界面鼠标事件验证凝核手牌拖拽，不接触正式存档。</summary>
public partial class CondensationDragSmoke : Control
{
    private int _checks;
    private CondensationCardGamePanel _panel = null!;
    private void Check(bool value, string why) { _checks++; if (!value) throw new Exception(why); }
    private static T Read<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(o)!;
    private static void Write(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(o, value);
    private static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(o, null);
    private async Task Frames(int count = 3) { for (var n = 0; n < count; n++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Move(Vector2 p, Vector2 delta, bool held) => GetViewport().PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = delta, ButtonMask = held ? MouseButtonMask.Left : 0 }, true);
    private void Mouse(Vector2 p, bool down) => GetViewport().PushInput(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = down, ButtonMask = down ? MouseButtonMask.Left : 0 }, true);
    private Control Canvas => Read<Control>(_panel, "_canvas");
    private Control Modal => Read<Control>(_panel, "_modal");
    private Vector2 Point(float x, float y) => Canvas.GetGlobalTransformWithCanvas() * new Vector2(x, y);
    private IEnumerable<Button> Buttons() => Modal.FindChildren("*", "Button", true, false).OfType<Button>();
    private IEnumerable<Label> Labels() => Modal.FindChildren("*", "Label", true, false).OfType<Label>();
    private void Press(string text) => Buttons().First(b => b.Text.Contains(text)).EmitSignal(BaseButton.SignalName.Pressed);
    private Button OpeningCard(int index) => (Button)Modal.FindChild($"OpeningCard{index}", true, false)!;
    private async Task Click(Control control)
    {
        var point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
        Move(point, Vector2.Zero, false); await Frames(2);
        Mouse(point, true); Mouse(point, false); await Frames(2);
    }

    private async Task<CondensationCardGame> Fixture(string card, bool testMode = true, bool swap = false)
    {
        var game = new CondensationCardGame(50, 15, 40, CondensationCardGame.Pool.ToDictionary(c => c.Name, c => c.Count), [], 42);
        game.Hand.Clear(); game.Hand.AddRange(["护气", "理气", "蓄劲", "徐压", "息压", card]);
        if (!swap) game.StartActions();
        Write(_panel, "_game", game); Write(_panel, "_testMode", testMode);
        Read<List<int>>(_panel, "_swapSelection").Clear();
        Call(_panel, "HideModal"); Call(_panel, "UpdateModeVisibility"); Call(_panel, "Refresh");
        await Frames(12);
        return game;
    }

    private async Task<Vector2> PressCard(int index)
    {
        var hand = Read<Control>(_panel, "_handLayer");
        var card = hand.GetChild<Control>(index);
        var from = card.GetGlobalTransformWithCanvas() * new Vector2(70, 115);
        Move(from, Vector2.Zero, false); await Frames(3);
        from = card.GetGlobalTransformWithCanvas() * new Vector2(70, 115);
        Move(from, Vector2.Zero, false); Mouse(from, true); await Frames(1);
        var pressed = Read<object?>(_panel, "_pressedHandCard");
        Check(pressed is not null && (int)pressed.GetType().GetField("Index")!.GetValue(pressed)! == index, "Mouse press did not reach the intended hand card.");
        return from;
    }
    private Task<Vector2> PressLastCard() => PressCard(Read<Control>(_panel, "_handLayer").GetChildCount() - 1);

    private async Task<Vector2> StartDrag(Vector2 destination)
    {
        var from = await PressLastCard();
        Move(destination, destination - from, true); await Frames(2);
        Check(Read<bool>(_panel, "_handDragging"), "Mouse movement did not start dragging.");
        Check(!Read<PanelContainer>(GetNode<HoverInfoManager>("/root/HoverInfoManager"), "_card").Visible, "Hover reader remains visible during drag.");
        Check(Canvas.GetNodeOrNull<Line2D>("CondensationDropRing") is null && Canvas.GetNodeOrNull<Panel>("CondensationDragFeedback") is null, "Unwanted drag ring or preview panel remains.");
        var hand = Read<Control>(_panel, "_handLayer");
        Check(hand.GetChild<Control>(hand.GetChildCount() - 1).TooltipText == "", "Card tooltip covers the drag forecast.");
        return destination;
    }
    private async Task Release(Vector2 point) { Mouse(point, false); await Frames(3); }
    private async Task DropAtCenter() { var point = await StartDrag(Point(825, 380)); await Release(point); }
    private async Task WaitForHold()
    {
        for (var n = 0; n < 120 && !Read<bool>(_panel, "_handHoldShown"); n++) await Frames(1);
        Check(Read<bool>(_panel, "_handHoldShown"), "Long press did not reach its display threshold.");
        await Frames(3);
    }
    private void CardTextFits(Control root)
    {
        foreach (var label in root.FindChildren("*", "Label", true, false).OfType<Label>())
        {
            var lines = label.GetLineCount();
            var required = lines * label.GetLineHeight() + Math.Max(0, lines - 1) * label.GetThemeConstant("line_spacing");
            Check(required <= label.Size.Y + 1, "Card text is clipped: " + label.Text);
        }
    }
    private void ButtonTextFits(Button button)
    {
        var width = button.GetThemeFont("font").GetStringSize(button.Text, HorizontalAlignment.Left, -1, button.GetThemeFontSize("font_size")).X;
        Check(width + 8 <= button.Size.X + 1, "Button text does not fit: " + button.Text);
    }
    private async Task Capture(string name, bool modal = false)
    {
        await Frames(3);
        if (modal)
        {
            var box = Modal.GetChild<Control>(1);
            foreach (var label in Labels())
            {
                var lines = label.GetLineCount();
                var required = lines * label.GetLineHeight() + Math.Max(0, lines - 1) * label.GetThemeConstant("line_spacing");
                Check(label.Position.Y + label.Size.Y <= box.Size.Y + 1 && required <= label.Size.Y + 1, "Modal text is clipped: " + label.Text);
            }
        }
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = "res://.godot/test-results/condensation_drag";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng($"{directory}/{name}_{GetWindow().Size.X}.png");
    }

    public override async void _Ready()
    {
        try
        {
            Check(OS.GetEnvironment("APPDATA").Contains("condensation_drag", StringComparison.OrdinalIgnoreCase), "Use isolated APPDATA.");
            Engine.MaxFps = 60;
            if (DisplayServer.GetName() != "headless")
            {
                var size = OS.GetCmdlineUserArgs().Contains("--small-window") ? new Vector2I(1280, 720) : new Vector2I(1920, 1080);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                GetWindow().Unfocusable = true; DisplayServer.WindowSetSize(size);
            }
            _panel = new CondensationCardGamePanel(); AddChild(_panel); _panel.Show();
            await Frames(6);

            // 入局直接询问起手换牌，使用共用悬停栏；跳过不得改变原牌序。
            Call(_panel, "BeginGame"); await Frames(4);
            var game = Read<CondensationCardGame>(_panel, "_game");
            var original = game.Copy();
            Check(Modal.Visible && Buttons().Count(b => b.Name.ToString().StartsWith("OpeningCard")) == 6 && game.Hand.Count == 6, "Opening hand was not shown immediately.");
            var confirmStyle = Buttons().First(b => b.Text == "换牌并开始").GetThemeStylebox("normal") as StyleBoxTexture;
            var skipStyle = Buttons().First(b => b.Text == "不换，直接开始").GetThemeStylebox("normal") as StyleBoxTexture;
            Check(confirmStyle?.Texture == CondensationUiArt.Rest && skipStyle?.Texture == CondensationUiArt.Rest, "Opening buttons do not use the original end-turn artwork.");
            foreach (var button in Buttons().Where(b => b.Text is "玩法说明" or "不换，直接开始" or "换牌并开始")) ButtonTextFits(button);
            Check(!Read<Button>(_panel, "_swap").Visible && !Read<Button>(_panel, "_start").Visible, "First round retained separate swap/start buttons.");
            var hover = GetNode<HoverInfoManager>("/root/HoverInfoManager");
            var first = OpeningCard(0);
            var firstPoint = first.GetGlobalTransformWithCanvas() * (first.Size * .5f);
            Move(firstPoint, Vector2.Zero, false); await Frames(15);
            var hoverCard = Read<PanelContainer>(hover, "_card");
            Check(hoverCard.Visible && Read<Control?>(hover, "_activeAnchor") == first && Read<HoverInfoManager.HoverInfoContent?>(hover, "_visibleContent")?.Title == game.Hand[0], "Opening card did not use the shared hover reader.");
            await Capture("opening_hover", modal: true);
            await Click(first); await Click(OpeningCard(1)); await Click(OpeningCard(2));
            Check(Read<List<int>>(_panel, "_swapSelection").SequenceEqual([0, 1]), "Opening selection did not enforce its two-card limit.");
            await Click(OpeningCard(0));
            Check(Read<List<int>>(_panel, "_swapSelection").SequenceEqual([1]), "Opening card selection could not be reversed.");
            Press("玩法说明"); Check(Labels().Any(l => l.Text.Contains("八回合")), "Opening help did not open.");
            Press("返回换牌");
            Check(Modal.Visible && Read<List<int>>(_panel, "_swapSelection").SequenceEqual([1]), "Returning from help lost opening selection.");
            Call(_panel, "HideModal"); Check(Modal.Visible && game.SwapOpen, "Opening decision was bypassed by closing the modal.");
            await Click(Buttons().First(b => b.Text == "不换，直接开始"));
            Check(!Modal.Visible && !game.SwapOpen && game.Actions == 3 && game.Hand.SequenceEqual(original.Hand) && game.Deck.SequenceEqual(original.Deck), "Skipping changed cards or did not start directly.");
            var topButtons = Canvas.GetChildren().OfType<Button>().Where(b => b.Text is "收益" or "玩法" or "重开" or "返回").ToArray();
            Check(topButtons.Length == 4 && topButtons.All(b => (b.GetThemeStylebox("normal") as StyleBoxTexture)?.Texture == CondensationUiArt.Rest), "Top buttons do not use the original end-turn artwork.");
            Check(topButtons.All(b => new[] { "normal", "hover", "pressed", "disabled", "focus" }.All(state => (b.GetThemeStylebox(state) as StyleBoxTexture)?.Texture == CondensationUiArt.Rest)), "Top-button states do not use the original artwork.");
            foreach (var button in topButtons) ButtonTextFits(button);
            await Click(topButtons.First(b => b.Text == "收益"));
            Check(Modal.Visible && Buttons().Any(b => b.Text == "返回牌局"), "Reward button did not open."); Press("返回牌局");
            await Click(topButtons.First(b => b.Text == "玩法"));
            Check(Modal.Visible && Buttons().Any(b => b.Text == "开始尝试"), "Help button did not open."); Press("开始尝试");
            await Click(topButtons.First(b => b.Text == "重开"));
            Check(Modal.Visible && Buttons().Any(b => b.Text == "继续当前"), "Restart button did not open."); Press("继续当前");

            var hand = Read<Control>(_panel, "_handLayer");
            var handCard = hand.GetChild<Control>(hand.GetChildCount() - 1);
            var handPoint = Vector2.Zero;
            for (var n = 0; n < 20; n++)
            {
                handPoint = handCard.GetGlobalTransformWithCanvas() * new Vector2(70, 115);
                Move(handPoint, Vector2.Zero, false); await Frames(1);
            }
            Check(!hoverCard.Visible, "Hand card showed information without a long press.");
            Mouse(handPoint, true); Check(Read<object?>(_panel, "_pressedHandCard") is not null, "Hand press did not reach the card.");
            var heldStartY = handCard.Position.Y; await WaitForHold();
            Check(hoverCard.Visible && Read<Control?>(hover, "_activeAnchor") == handCard && Read<HoverInfoManager.HoverInfoContent?>(hover, "_visibleContent")?.Title == game.Hand[^1], "Long press did not show the shared reader.");
            Check(handCard.Position.Y < heldStartY - 20, "Held card was not raised into view.");
            await Capture("hand_hold");
            Mouse(handPoint, false); await Frames();
            Check(!hoverCard.Visible && !Modal.Visible && game.Actions == 3 && game.Hand.Count == 6, "Releasing a long press activated or spent the card.");

            // 两张换牌按原牌局规则结算，完成后不再出现第二个开始按钮。
            Call(_panel, "BeginGame"); await Frames(4); game = Read<CondensationCardGame>(_panel, "_game");
            var expected = game.Copy(); Check(expected.Swap([0, 3]) == "", "Could not prepare expected opening swap."); expected.StartActions();
            await Click(OpeningCard(0)); await Click(OpeningCard(3));
            HoverInfoManager.HideAll(); Move(Point(750, 690), Vector2.Zero, false); await Frames(3);
            await Capture("opening_selected", modal: true);
            await Click(Buttons().First(b => b.Text == "换牌并开始"));
            Check(!Modal.Visible && !game.SwapOpen && game.Actions == 3 && game.Hand.SequenceEqual(expected.Hand) && game.Deck.SequenceEqual(expected.Deck), "Two-card swap did not start directly or changed shuffle rules.");

            Call(_panel, "BeginGame"); await Frames(4); game = Read<CondensationCardGame>(_panel, "_game");
            expected = game.Copy(); Check(expected.Swap([5]) == "", "Could not prepare single opening swap."); expected.StartActions();
            await Click(OpeningCard(5)); await Click(Buttons().First(b => b.Text == "换牌并开始"));
            Check(!game.SwapOpen && game.Hand.SequenceEqual(expected.Hand) && game.Deck.SequenceEqual(expected.Deck), "Single opening swap failed.");

            Check(game.EndRound() == "", "Could not reach the later-round fixture."); Call(_panel, "Refresh"); await Frames(12);
            Check(game.Round == 2 && game.SwapOpen && Read<Button>(_panel, "_swap").Visible && Read<Button>(_panel, "_start").Visible, "Later-round swap controls were lost.");

            // 准备阶段点击仍选换牌；拖出目标区松手也不能消耗牌或结束准备。
            game = await Fixture("轻压", testMode: false, swap: true);
            var from = await PressLastCard(); Mouse(from, false); await Frames();
            Check(Read<List<int>>(_panel, "_swapSelection").SequenceEqual([5]), "Click no longer selects a card for swapping.");
            var outside = await StartDrag(Point(350, 370)); await Release(outside);
            Check(game.SwapOpen && game.Actions == 3 && game.Hand.Count == 6 && Read<List<int>>(_panel, "_swapSelection").SequenceEqual([5]), "Outside drop changed game or swap selection.");

            // 同一局直接从准备阶段拖入中心：预览不改变规则，松手只结算一次。
            var center = await StartDrag(Point(825, 380));
            var details = CondensationCardText.Details("轻压", game, 5);
            Check(PlayerInfoText.Detailed(details).Contains("基础10＋灵根1＝11") && PlayerInfoText.Detailed(details).Contains("匹配权重40%") && !PlayerInfoText.Summary(details).Contains("基础10＋灵根1") && !PlayerInfoText.Summary(details).Contains("匹配权重40%"), "Card result or detail-mode calculation is incorrect.");
            Check(game.SwapOpen && game.Score == 0 && game.Actions == 3 && game.Hand.Count == 6, "Preview mutated the actual game.");
            await Capture("drag");
            await Release(center);
            Check(!game.SwapOpen && game.Score == 11 && game.Actions == 2 && game.Hand.Count == 5 && game.Discard.Count == 1 && !Modal.Visible, "Formal safe drop did not play exactly once.");
            Check(Read<List<int>>(_panel, "_swapSelection").Count == 0, "Played card retained swap selection.");

            game = await Fixture("轻压");
            from = await PressLastCard(); await WaitForHold();
            Check(hoverCard.Visible && Read<HoverInfoManager.HoverInfoContent?>(hover, "_visibleContent")?.Body.Contains("基础10＋灵根1＝11") == true, "Long-press reader lacks actual calculation.");
            await Capture("hold_pressure");
            center = Point(825, 380); Move(center, center - from, true); await Frames(2);
            Check(Read<bool>(_panel, "_handDragging") && !hoverCard.Visible && game.Actions == 3, "Dragging after a hold did not hide information before play.");
            await Release(center); Check(game.Actions == 2 && game.Score == 11, "Dragging after a hold failed to play exactly once.");

            // 开始行功后的原点击预览保留，取消不消耗。
            game = await Fixture("徐压");
            from = await PressLastCard(); Mouse(from, false); await Frames();
            Check(Modal.Visible && Buttons().Any(b => b.Text == "确认施展") && game.Actions == 3, "Click preview regressed.");
            Press("取消"); Check(!Modal.Visible && game.Actions == 3, "Canceling click preview changed game.");
            game = await Fixture("徐压");
            from = await PressCard(0); Mouse(from, false); await Frames();
            Check(Modal.Visible && Read<string>(_panel, "_selectionName") == "护气", "First overlapping hand card could not be clicked.");
            Press("取消"); await Frames(8);
            from = await PressCard(2); Mouse(from, false); await Frames();
            Check(Modal.Visible && Read<string>(_panel, "_selectionName") == "蓄劲", "Middle overlapping hand card could not be clicked.");
            Press("取消");

            // 唯一合法目标无需重复选择。
            game = await Fixture("归拢"); game.Recoils.Add(new(100, 3, 4));
            await DropAtCenter();
            Check(!Modal.Visible && game.Actions == 2 && game.Score == 1 && game.Recoils.Count == 0, "Unique recoil target was not applied.");

            game = await Fixture("引回"); game.Hand[0] = "随势";
            game.Recoils.AddRange([new(100, 1, 2), new(101, 1, 4), new(102, 3, 3)]);
            typeof(CondensationCardGame).GetProperty("Score")!.SetValue(game, 20);
            typeof(CondensationCardGame).GetProperty("Disorder")!.SetValue(game, 3);
            Call(_panel, "Refresh"); await Frames(12);
            Check(Read<Label>(_panel, "_recoilInfo").Text.Contains("本回合回弹  6（2股）") && Read<Label>(_panel, "_recoilInfo").Text.Contains("未引回扣除至多  6"), "Current recoil summary is missing or incorrect.");
            Check(Read<Label>(_panel, "_disorder").Text.Contains("回合末预计扣除  2"), "Current disorder penalty is missing or incorrect.");
            var registered = Read<Dictionary<Control, HoverInfoManager.HoverInfoContent>>(hover, "_entries");
            Check(PlayerInfoText.Detailed(registered[Read<Label>(_panel, "_disorder")].Body).Contains("3×3＝9，9÷4＝2余1"), "Disorder calculation is not explained accurately.");
            Check(PlayerInfoText.Detailed(registered[Read<Label>(_panel, "_recoilInfo")].Body).Contains("回弹6－护持0，最低0＝6"), "Recoil calculation is not explained accurately.");
            Check(Read<CondensationTurnTrack>(_panel, "_track").GetChildren().OfType<Label>().Any(l => l.Text == "回弹6·2股"), "Turn track does not show recoil amount and count.");
            await Capture("recoil");
            var disorderLabel = Read<Label>(_panel, "_disorder");
            var statusPoint = disorderLabel.GetGlobalTransformWithCanvas() * (disorderLabel.Size * .5f);
            Move(statusPoint, Vector2.Zero, false); await Frames(15);
            Check(hoverCard.Visible && Read<Control?>(hover, "_activeAnchor") == disorderLabel, "Disorder details could not be opened by hovering.");
            var recoilLabel = Read<Label>(_panel, "_recoilInfo");
            statusPoint = recoilLabel.GetGlobalTransformWithCanvas() * (recoilLabel.Size * .5f);
            Move(statusPoint, Vector2.Zero, false); await Frames(10);
            Check(hoverCard.Visible && Read<Control?>(hover, "_activeAnchor") == recoilLabel, "Recoil details could not be opened by hovering.");
            await Capture("recoil_hover");
            HoverInfoManager.HideAll();
            await DropAtCenter();
            Check(game.ReturnActive && Read<Label>(_panel, "_recoilInfo").Text.Contains("预计引回  +10"), "Current recoil summary missed return bonus.");
            Check(game.Play("随势", 0, []) == "", "Could not arrange follow-up return fixture."); Call(_panel, "Refresh");
            Check(Read<Label>(_panel, "_recoilInfo").Text.Contains("预计引回  +14"), "Current recoil summary missed follow-up bonus.");

            // 有多个目标时必须让玩家选择，取消不得消耗。
            game = await Fixture("缓行"); game.Recoils.AddRange([new(100, 2, 2), new(101, 3, 3)]);
            await DropAtCenter();
            Check(Modal.Visible && game.Actions == 3 && game.Hand.Count == 6 && Buttons().Any(b => b.Text == "确认并施展"), "Multiple targets did not request selection before spending.");
            Press("取消"); Check(!Modal.Visible && game.Actions == 3 && game.Recoils[0].Round == 2, "Canceling target selection spent the card.");
            await Frames(20); await DropAtCenter(); Press("第100股");
            Check(Labels().Any(l => l.Text.Contains("第2回合回弹2 → 第3回合回弹2")), "Target result preview missing.");
            await Capture("targets", modal: true);
            Press("确认并施展"); await Frames();
            Check(game.Actions == 2 && game.Recoils.First(r => r.Id == 100).Round == 3 && game.Recoils.First(r => r.Id == 101).Round == 3, "Selected target changed the wrong recoil.");

            game = await Fixture("并流"); game.Recoils.AddRange([new(100, 2, 2), new(101, 2, 3), new(102, 4, 1)]);
            await DropAtCenter(); Press("第100股"); Press("第102股");
            Check(Labels().Any(l => l.Text.Contains("第2回合回弹2 → 第4回合回弹2")), "Two-target result preview missing.");
            Press("确认并施展"); await Frames();
            Check(game.Actions == 2 && game.Recoils.First(r => r.Id == 100).Round == 4 && game.Recoils.First(r => r.Id == 101).Round == 2, "Two-target play changed the wrong recoil.");

            // 不满足条件的牌不能关闭换牌阶段或扣牌。
            game = await Fixture("叠压", swap: true); await DropAtCenter();
            Check(Modal.Visible && Labels().Any(l => l.Text.Contains("须紧接")) && game.SwapOpen && game.Actions == 3 && game.Hand.Count == 6, "Invalid card was spent or did not explain why.");
            Press("返回");

            // 潜在致败必须在真正打牌前确认，取消后仍可继续。
            game = await Fixture("轻压"); typeof(CondensationCardGame).GetProperty("Disorder")!.SetValue(game, 10);
            Call(_panel, "Refresh"); await Frames(12);
            center = await StartDrag(Point(825, 380));
            Check(CondensationCardText.Details("轻压", game, 5).Contains("立即失败"), "Fatal card detail did not warn before release.");
            await Release(center);
            Check(Modal.Visible && Buttons().Any(b => b.Text == "仍然施展（将失败）") && game.Actions == 3 && !game.Finished, "Fatal play was not held for confirmation.");
            await Capture("risk", modal: true);
            Press("取消"); Check(!game.Finished && game.Disorder == 10 && game.Hand.Count == 6, "Canceling fatal play changed game.");
            await Frames(20); await DropAtCenter(); Press("仍然施展"); await Frames();
            Check(game.Finished && game.Failed && game.Actions == 2 && game.Hand.Count == 5, "Confirmed fatal play was not applied exactly once.");

            // 牌面不增紊乱，但涨—降—涨追加紊乱时也必须拦截致败。
            game = await Fixture("徐压");
            Check(game.Play("护气", 0, []) == "" && game.Play("理气", 0, []) == "", "Could not arrange reversal fixture.");
            typeof(CondensationCardGame).GetProperty("Disorder")!.SetValue(game, 10);
            Call(_panel, "Refresh"); await Frames(12);
            center = await StartDrag(Point(825, 380));
            Check(CondensationCardText.Details("徐压", game, game.Hand.Count - 1).Contains("基础0＋反复涨降1＝1"), "Reversal disorder was missing from the calculation.");
            await Release(center);
            Check(Modal.Visible && Buttons().Any(b => b.Text == "仍然施展（将失败）") && !game.Finished && game.Actions == 1, "Reversal-triggered failure bypassed confirmation.");
            Press("取消");

            // 观气松手前不得泄露牌序，之后正常进入选牌；匀气正常进入弃牌。
            game = await Fixture("观气"); game.Deck.Clear(); game.Deck.AddRange(["催回", "并流", "息压", "轻压"]);
            center = await StartDrag(Point(825, 380)); details = CondensationCardText.Details("观气", game, 5);
            Check(!details.Contains("催回") && !details.Contains("并流") && !details.Contains("息压") && game.Deck.Count == 4 && game.Choices.Count == 0, "Information revealed or modified hidden deck.");
            await Release(center);
            Check(game.Actions == 2 && game.Choices.Count == 3 && Modal.Visible, "Observe card did not open its choice.");
            Press("催回"); await Frames(); Check(!game.Busy && game.Hand.Contains("催回") && game.Actions == 2, "Observe choice failed.");

            game = await Fixture("匀气"); await DropAtCenter();
            Check(game.Actions == 2 && game.ForcedDiscards == 1 && Modal.Visible, "Draw-and-discard did not request a discard.");
            Buttons().First().EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(!game.Busy && game.Actions == 2 && game.Hand.Count == 6, "Forced discard did not resolve correctly.");

            // 每种牌的起手和手牌版式均不得将文字挤出牌面。
            for (var start = 0; start < CondensationCardGame.Pool.Length; start += 6)
            {
                var names = CondensationCardGame.Pool.Skip(start).Take(6).Select(c => c.Name).ToArray();
                Call(_panel, "BeginGame"); game = Read<CondensationCardGame>(_panel, "_game");
                game.Hand.Clear(); game.Hand.AddRange(names); Call(_panel, "ShowOpeningSwap"); await Frames(3);
                foreach (var card in Buttons().Where(b => b.Name.ToString().StartsWith("OpeningCard"))) CardTextFits(card);
                Press("不换，直接开始"); await Frames(3);
                foreach (var card in Read<Control>(_panel, "_handLayer").GetChildren().OfType<Control>()) CardTextFits(card);
            }

            GD.Print($"CONDENSATION_DRAG_SMOKE_PASS: checks={_checks}; viewport={GetWindow().Size}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError($"checks={_checks}: {error}"); GetTree().Quit(1); }
    }
}
