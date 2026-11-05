using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>云息六至七层正式凝核与独立试演共用的八回合牌局。</summary>
public partial class CondensationCardGamePanel : Control
{
    private readonly Dictionary<string, int> _counts = CondensationCardGame.Pool.ToDictionary(c => c.Name, c => c.Count);
    private readonly List<string> _items = ["定息丹", "清息丹"];
    private readonly List<int> _swapSelection = [];
    private readonly List<HandVisual> _cards = [];
    private Control _canvas = null!, _handLayer = null!, _modal = null!;
    private Label _score = null!, _disorder = null!, _actions = null!, _status = null!, _forecast = null!, _itemHint = null!;
    private CondensationTurnTrack _track = null!;
    private Button _draw = null!, _discard = null!, _swap = null!, _extra = null!, _start = null!, _end = null!;
    private Button _itemButton = null!, _returnButton = null!, _restartButton = null!;
    private Label _drawCount = null!, _discardCount = null!;
    private RealmQiVisual _qi = null!;
    private CondensationCardGame? _game;
    private Character? _target;
    private int _hover = -1;
    private int _comprehension = 50, _intelligence = 15, _rootWeight = 40;
    private bool _animating;
    private bool _testMode = true, _resultReported, _successAnimationPlayed;
    private bool _resumeResultAfterRealmPresentation;
    private string _selectionName = "";
    private int _selectionIndex = -1;
    private bool _selectionItem;
    private readonly List<int> _targets = [];
    private int _shift = -1;
    public event Action? Closed;
    public event Action<Character, bool, int>? CondensationFinished;
    private static readonly Color Ink = new("28251e"), Cream = new("e5d6b5");
    private sealed class HandVisual
    {
        public Button Node = null!;
        public float X, Y, Angle, Zoom = 1, VX, VY, VA, VZ;
        public int Index;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 600;
        var fill = new ColorRect { Color = new Color("211e18"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(fill); fill.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Size = new Vector2(1672, 941), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_canvas);
        Art(_canvas, Load("res://textures/ui/realm_yunxi_paper_v6.png"), new Rect2(0, 0, 1672, 941));
        _qi = new RealmQiVisual { Position = new Vector2(475, 130), Size = new Vector2(700, 500) };
        _canvas.AddChild(_qi); _qi.ShowCloud(1);
        var poem = new RealmPoemVisual { Position = new Vector2(1240, 120), Size = new Vector2(360, 460) };
        _canvas.AddChild(poem);
        Text(_canvas, "凝 核", 45, new Rect2(690, 22, 290, 62), Cream, true);
        Art(_canvas, CondensationUiArt.Divider, new Rect2(550, 49, 135, 18));
        Art(_canvas, CondensationUiArt.Divider, new Rect2(985, 49, 135, 18));
        _track = new CondensationTurnTrack { Position = new Vector2(435, 88), Size = new Vector2(800, 155) };
        _canvas.AddChild(_track);
        // 用户明确要求靠近左侧边缘；所有文字共用32px安全对齐线。
        Text(_canvas, "凝核程度", 23, new Rect2(32, 195, 250, 34), Cream);
        _score = Text(_canvas, "", 65, new Rect2(32, 235, 220, 85), Cream);
        Text(_canvas, "成核需达到100", 19, new Rect2(32, 322, 250, 30), Cream);
        _disorder = Text(_canvas, "", 21, new Rect2(32, 365, 280, 68), Cream);
        _actions = Text(_canvas, "", 21, new Rect2(32, 448, 270, 68), Cream);
        _status = Text(_canvas, "", 21, new Rect2(450, 475, 740, 50), Cream, true);
        _forecast = Text(_canvas, "", 17, new Rect2(450, 527, 740, 42), new Color("c1b497"), true);
        _itemButton = InkButtonAt(_canvas, "道具", new Rect2(32, 532, 150, 48), ShowItems);
        _itemHint = Text(_canvas, "使用不占施展次数", 16, new Rect2(32, 584, 250, 28), Cream);
        _draw = Pile(new Rect2(110, 650, 132, 152), CondensationUiArt.DrawPile, () => ShowPile(false));
        _discard = Pile(new Rect2(1440, 655, 116, 146), CondensationUiArt.DiscardPile, () => ShowPile(true));
        _drawCount = Text(_draw, "", 23, new Rect2(-25, 157, 185, 36), Cream, true);
        _discardCount = Text(_discard, "", 23, new Rect2(-30, 151, 185, 36), Cream, true);
        var rest = InkButtonAt(_canvas, "调 息", new Rect2(94, 844, 164, 58), () => SelectAction("调息", -1, false));
        _end = InkButtonAt(_canvas, "结束回合", new Rect2(1405, 844, 186, 58), ConfirmEnd);
        // 独立准备操作条留在提示与手牌之间，不遮旋涡；层级高于抬起的手牌。
        _swap = ButtonAt(_canvas, "换起手（选0—2张）", new Rect2(455, 584, 255, 42), DoSwap);
        _extra = ButtonAt(_canvas, "悟性换牌", new Rect2(725, 584, 205, 42), DoExtraSwap);
        _start = ButtonAt(_canvas, "开始行功", new Rect2(945, 584, 230, 42), () => { _game?.StartActions(); _swapSelection.Clear(); Refresh(); });
        _swap.ZIndex = _extra.ZIndex = _start.ZIndex = 45;
        _handLayer = new Control { Size = _canvas.Size, MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_handLayer);
        ButtonAt(_canvas, "收益", new Rect2(1205, 25, 85, 38), () => ShowRewards());
        ButtonAt(_canvas, "玩法", new Rect2(1300, 25, 85, 38), ShowHelp);
        _restartButton = ButtonAt(_canvas, "重开", new Rect2(1395, 25, 85, 38), ConfirmRestart);
        _returnButton = ButtonAt(_canvas, "返回", new Rect2(1490, 25, 85, 38), RequestClose);
        _modal = new Control { Size = _canvas.Size, Visible = false, MouseFilter = MouseFilterEnum.Stop, ZIndex = 100 };
        _canvas.AddChild(_modal);
        var theme = new Theme();
        theme.DefaultFont = GD.Load<Font>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        _canvas.Theme = theme;
        Resized += LayoutCanvas; LayoutCanvas();
        Hide();
    }

    public void ShowFor(Character target, bool testMode = true)
    {
        _target = target;
        _testMode = testMode;
        _game = null;
        _resultReported = false;
        _resumeResultAfterRealmPresentation = false;
        _successAnimationPlayed = false;
        _comprehension = Mathf.RoundToInt(target.comprehension);
        _intelligence = Mathf.RoundToInt(target.intelligence);
        var method = target.main_gongfa;
        var weights = new[] { "METAL", "WOOD", "WATER", "FIRE", "EARTH" }.Select(target.get_effective_root_weight).Where(w => w > 0).ToArray();
        if (method is not null && method.wuxing is not (GongfaResource.Wuxing.NONE or GongfaResource.Wuxing.ALL))
        {
            var required = method.wuxing.ToString().Split('_').Select(target.get_effective_root_weight).ToArray();
            _rootWeight = required.Length == 0 ? 0 : Mathf.RoundToInt(100 * (method.wuxing_requirement_mode == GongfaResource.WuxingRequirementMode.AND ? required.Average() : required.Max()));
        }
        else _rootWeight = weights.Length == 0 ? 0 : Mathf.RoundToInt(weights.Max() * 100);
        if (!_testMode) _items.Clear();
        else if (_items.Count == 0) _items.AddRange(["定息丹", "清息丹"]);
        Show(); ShowSetup();
    }

    private void LayoutCanvas()
    {
        var scale = Mathf.Min(Size.X / 1672f, Size.Y / 941f);
        _canvas.Scale = Vector2.One * scale;
        _canvas.Position = (Size - _canvas.Size * scale) * .5f;
    }
    public override void _Process(double delta)
    {
        // 保存位置速度，升起和回落均经过弹簧，邻牌也随悬停让位。
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Min(delta, .05) / (1.0 / 120)));
        var dt = (float)Math.Min(delta, .05) / steps;
        foreach (var c in _cards)
        {
            var offset = c.Index - (_cards.Count - 1) / 2f;
            var lifted = c.Index == _hover || _swapSelection.Contains(c.Index);
            var x = 836 + offset * 151 - 100;
            if (_hover >= 0 && c.Index != _hover) x += c.Index < _hover ? -22 : 22;
            var y = 652 + Math.Abs(offset) * 7 - (lifted ? 86 : 0);
            var angle = lifted ? 0 : offset * .045f;
            for (var n = 0; n < steps; n++)
            {
                Spring(ref c.X, ref c.VX, x, 190, 19, dt); Spring(ref c.Y, ref c.VY, y, lifted ? 250 : 200, lifted ? 17 : 16, dt);
                Spring(ref c.Angle, ref c.VA, angle, 220, 17, dt); Spring(ref c.Zoom, ref c.VZ, lifted ? 1.1f : 1, 260, 20, dt);
            }
            c.Node.Position = new Vector2(c.X, c.Y); c.Node.Rotation = c.Angle; c.Node.Scale = Vector2.One * c.Zoom;
            c.Node.ZIndex = lifted ? 30 : c.Index;
            c.Node.SelfModulate = _swapSelection.Contains(c.Index) ? new Color("fff1bf") : Colors.White;
        }
    }
    private static void Spring(ref float position, ref float velocity, float target, float stiffness, float damping, float dt)
    { velocity += ((target - position) * stiffness - velocity * damping) * dt; position += velocity * dt; }

    private void BeginGame()
    {
        _game = new CondensationCardGame(_comprehension, _intelligence, _rootWeight, _counts, _items);
        _animating = false; _successAnimationPlayed = false; _swapSelection.Clear(); _qi.ShowCloud(1); UpdateModeVisibility(); HideModal(); Refresh(); ShowHelp();
    }
    private void ShowSetup()
    {
        var box = Modal(_testMode ? "凝核试演 · 入局准备" : "云息六层凝核 · 入局准备", 1290, 825);
        var intro = _testMode
            ? "八回合 · 成核需100 · 同名最多四张 · 牌组须满36张\n独立测试：可调属性与选择虚拟道具，不消耗正式资源或写入角色。"
            : "八回合 · 成核需100 · 同名最多四张 · 牌组须满36张\n正式突破：人物属性固定，不开放道具；失败不扣灵力，重伤休养后可重试。";
        Text(box, intro, 22, new Rect2(25, 65, 1230, 65), Cream);
        var summary = Text(box, "", 22, new Rect2(25, 145, 420, 40), Cream);
        void UpdateSummary() => summary.Text = $"牌组：{_counts.Values.Sum()} / 36 张";
        UpdateSummary();
        ButtonAt(box, "恢复推荐牌组", new Rect2(735, 145, 235, 40), () =>
        {
            foreach (var card in CondensationCardGame.Pool) _counts[card.Name] = card.Count;
            ShowSetup();
        });
        ButtonAt(box, "查看凝核收益", new Rect2(1000, 145, 245, 40), () => ShowRewards(ShowSetup));
        for (var i = 0; i < CondensationCardGame.Pool.Length; i++)
        {
            var card = CondensationCardGame.Pool[i]; var col = i % 3; var row = i / 3; var x = 25 + col * 415; var y = 195 + row * 67;
            Text(box, card.Name, 23, new Rect2(x, y, 115, 42), Cream).TooltipText = card.Description;
            var count = Text(box, _counts[card.Name].ToString(), 23, new Rect2(x + 165, y, 45, 42), Cream, true);
            ButtonAt(box, "−", new Rect2(x + 115, y, 44, 40), () => { _counts[card.Name] = Math.Max(0, _counts[card.Name] - 1); count.Text = _counts[card.Name].ToString(); UpdateSummary(); });
            ButtonAt(box, "+", new Rect2(x + 215, y, 44, 40), () => { _counts[card.Name] = Math.Min(4, _counts[card.Name] + 1); count.Text = _counts[card.Name].ToString(); UpdateSummary(); });
        }
        Text(box, _testMode ? "试演属性（入局后固定）" : "人物属性（按角色当前值锁定）", 22, new Rect2(25, 605, 450, 35), Cream);
        Number(box, "悟性", _comprehension, 0, 200, new Vector2(25, 650), v => _comprehension = v, _testMode);
        Number(box, "灵慧", _intelligence, 0, 100, new Vector2(425, 650), v => _intelligence = v, _testMode);
        Number(box, "匹配权重%", _rootWeight, 1, 100, new Vector2(825, 650), v => _rootWeight = v, _testMode);
        if (_testMode)
        {
            var itemsLabel = Text(box, "", 19, new Rect2(25, 700, 1230, 35), Cream);
            void UpdateItems() => itemsLabel.Text = "携带道具（选两件，同名一件）：" + string.Join("、", _items);
            UpdateItems();
            for (var i = 0; i < CondensationCardGame.Items.Length; i++)
            {
                var item = CondensationCardGame.Items[i];
                ButtonAt(box, item, new Rect2(25 + i * 185, 745, 165, 43), () =>
                { if (_items.Contains(item)) _items.Remove(item); else if (_items.Count < 2) _items.Add(item); UpdateItems(); }).TooltipText = CondensationCardGame.ItemText(item);
            }
        }
        else Text(box, "正式局不开放道具。", 20, new Rect2(25, 710, 500, 40), Cream);
        if (!_testMode) ButtonAt(box, "返回修炼", new Rect2(755, 745, 220, 43), RequestClose);
        ButtonAt(box, "开始凝核", new Rect2(1010, 745, 235, 43), () =>
        { if (_counts.Values.Sum() != 36) { summary.Text = "请将牌组调整为36张。"; return; } BeginGame(); });
        UpdateModeVisibility();
    }
    private static void Number(Control parent, string label, int initial, int min, int max, Vector2 pos, Action<int> changed, bool editable)
    {
        Text(parent, label, 21, new Rect2(pos, new Vector2(150, 40)), Cream);
        var spin = new SpinBox { Position = pos + new Vector2(150, 0), Size = new Vector2(135, 40), MinValue = min, MaxValue = max, Step = 1, Value = initial, Editable = editable };
        parent.AddChild(spin);
        spin.ValueChanged += v =>
        {
            if (editable) changed((int)v);
            else spin.Value = initial;
        };
    }

    private void UpdateModeVisibility()
    {
        if (_itemButton is not null) _itemButton.Visible = _testMode;
        if (_itemHint is not null) _itemHint.Visible = _testMode;
        if (_restartButton is not null) _restartButton.Visible = _testMode;
        if (_returnButton is not null)
        {
            _returnButton.Text = _testMode ? "返回" : "返回修炼";
            _returnButton.Visible = _testMode || _game is null || _game.Finished;
        }
    }

    private void Refresh()
    {
        if (_game is null) return;
        var g = _game; _hover = -1;
        _score.Text = g.Score.ToString();
        _disorder.Text = $"气息紊乱  {g.Disorder} / 10\n本轮凝核损失  {CondensationCardGame.DisorderLoss(g.Disorder)}";
        _actions.Text = "本回合可施展\n" + new string('◆', g.Actions) + new string('◇', 3 - g.Actions);
        _track.SetState(g);
        _status.Text = g.Finished ? g.Disorder > 10 ? "紊乱失控 · 凝核失败" : $"最终凝核程度 {g.Score} · {(g.Failed ? "凝核未成" : "灵核已成")}" : g.SwapOpen ? "准备阶段：可选手牌换牌，再开始行功" : "选择手法，压聚灵气";
        _forecast.Text = g.Finished ? (_testMode ? "本次试演结果不写入正式人物。" : "正式凝核已结算。") : $"灵根：压聚+{g.RootBonus}  ·  灵慧：理气额外降{g.InsightBonus}  ·  悟性换牌余{g.ExtraSwaps}" + (g.ChargeActive ? "  ·  蓄劲待发" : "") + (g.ReturnActive ? "  ·  本轮引回" : "");
        UpdateModeVisibility();
        _drawCount.Text = $"抽牌堆 {g.Deck.Count}"; _discardCount.Text = $"弃牌堆 {g.Discard.Count}";
        _swap.Visible = _start.Visible = g.SwapOpen && !g.Finished;
        _extra.Visible = g.SwapOpen && g.Round > 1 && !g.Finished;
        _swap.Text = g.Round == 1 ? "换起手（选0—2张）" : "换牌（选一张）";
        _swap.Disabled = g.OrdinarySwapUsed;
        _extra.Text = $"悟性换牌 · 余{g.ExtraSwaps}"; _extra.Disabled = g.ExtraSwaps <= 0 || g.Deck.Count == 0;
        _end.Disabled = g.Finished || g.Busy || _animating;
        _end.Text = g.Round == 8 ? "结算并定形" : "结束回合";
        foreach (var child in _handLayer.GetChildren()) { _handLayer.RemoveChild(child); child.QueueFree(); }
        _cards.Clear();
        for (var i = 0; i < g.Hand.Count; i++) BuildHandCard(i, g.Hand[i]);
        if (!_animating && !g.Finished) _qi.ShowCompression(g.Score);
        if (g.Finished)
        {
            // 正式成功交给统一境界演出；试演仍只操作本页，不写正式人物。
            if (_testMode && !g.Failed && !_successAnimationPlayed)
            {
                _successAnimationPlayed = true;
                _animating = true; _end.Disabled = true;
                _qi.PlayCondensation(text => _status.Text = text, () => { _animating = false; Refresh(); });
            }
            else ShowResult();
        }
    }
    private void BuildHandCard(int index, string name)
    {
        var c = CondensationCardGame.Definition(name); var offset = index - (_game!.Hand.Count - 1) / 2f;
        var button = new Button { Size = new Vector2(200, 310), PivotOffset = new Vector2(100, 280), MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
        ClearButton(button);
        button.TooltipText = c.Description + (c.Kind == "压聚" ? $"\n本角色压聚额外凝核+{_game.RootBonus}" : "");
        _handLayer.AddChild(button);
        Art(button, CondensationUiArt.Card, new Rect2(0, 0, 200, 310));
        Text(button, name, 31, new Rect2(12, 13, 176, 48), Ink, true);
        Text(button, c.Kind, 17, new Rect2(12, 58, 176, 28), Ink, true);
        var artIndex = CardIconIndex(name);
        Art(button, CondensationUiArt.Icon(artIndex), new Rect2(22, 83, 156, 117));
        Text(button, c.Description + (c.Kind == "压聚" ? $"\n灵根额外+{_game.RootBonus}" : ""), 19, new Rect2(17, 202, 166, 100), Ink, true);
        var visual = new HandVisual { Node = button, Index = index, X = 736 + offset * 151, Y = 652 + Math.Abs(offset) * 7, Angle = offset * .045f };
        button.Position = new Vector2(visual.X, visual.Y); button.Rotation = visual.Angle; _cards.Add(visual);
        button.MouseEntered += () => _hover = index;
        button.MouseExited += () => { if (_hover == index) _hover = -1; };
        button.Pressed += () =>
        {
            if (_animating || _game.Finished) return;
            if (_game.SwapOpen)
            {
                if (_swapSelection.Contains(index)) _swapSelection.Remove(index);
                else if (_swapSelection.Count < (_game.Round == 1 ? 2 : 1)) _swapSelection.Add(index);
                return;
            }
            SelectAction(name, index, false);
        };
    }
    private void DoSwap() => ApplySwap(false);
    private void DoExtraSwap() => ApplySwap(true);
    private void ApplySwap(bool extra)
    {
        if (_game is null) return;
        var error = _game.Swap(_swapSelection, extra);
        if (error != "") { Notice(error); return; }
        _swapSelection.Clear(); Refresh();
    }

    private void SelectAction(string name, int index, bool item)
    {
        if (_game is null || _animating) return;
        _selectionName = name; _selectionIndex = index; _selectionItem = item; _targets.Clear(); _shift = -1;
        if (_game.TargetCount(name) > 0) ShowTargets(); else ConfirmAction();
    }
    private void ShowTargets()
    {
        var g = _game!; var box = Modal($"{_selectionName} · 选择回弹", 940, 720);
        Text(box, "点击选择目标；未确认前不扣牌或施展次数。", 22, new Rect2(30, 65, 880, 42), Cream);
        var list = new VBoxContainer { Position = new Vector2(30, 120), Size = new Vector2(880, 450) };
        var scroll = new ScrollContainer { Position = list.Position, Size = list.Size }; box.AddChild(scroll);
        list.Position = Vector2.Zero; list.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(list);
        foreach (var r in g.LegalTargets(_selectionName).OrderBy(r => r.Round).ToArray())
        {
            var selected = _targets.Contains(r.Id);
            var b = new Button { Text = $"{(selected ? "◆" : "◇")} 第{r.Round}回合 · 回弹{r.Strength} · 第{r.Id}股", CustomMinimumSize = new Vector2(850, 46) };
            list.AddChild(b); Style(b);
            b.Pressed += () => { if (!_targets.Remove(r.Id) && _targets.Count < g.TargetCount(_selectionName)) _targets.Add(r.Id); ShowTargets(); };
        }
        if (!g.LegalTargets(_selectionName).Any()) Text(box, "目前没有可选回弹。", 24, new Rect2(30, 150, 850, 50), Cream);
        if (_selectionName == "导气符")
        {
            ButtonAt(box, _shift == -1 ? "◆ 提前一回合" : "提前一回合", new Rect2(30, 585, 250, 45), () => { _shift = -1; ShowTargets(); });
            ButtonAt(box, _shift == 1 ? "◆ 延后一回合" : "延后一回合", new Rect2(300, 585, 250, 45), () => { _shift = 1; ShowTargets(); });
        }
        ButtonAt(box, "查看效果", new Rect2(650, 650, 240, 45), ConfirmAction);
        ButtonAt(box, "取消", new Rect2(30, 650, 180, 45), HideModal);
    }
    private void ConfirmAction()
    {
        var g = _game!;
        var error = g.CanPlay(_selectionName, _selectionIndex, _targets, _selectionItem, _shift);
        if (error != "") { Notice(error); return; }
        var copy = g.Copy(); copy.Play(_selectionName, _selectionIndex, _targets, _selectionItem, _shift);
        var box = Modal(_selectionName, 930, 620);
        var description = _selectionItem ? CondensationCardGame.ItemText(_selectionName) : _selectionName == "调息" ? $"紊乱降低{2 + g.InsightBonus}，本轮未引回回弹损失减少1，占一次施展。" : CondensationCardGame.Definition(_selectionName).Description;
        Text(box, description, 24, new Rect2(35, 80, 860, 90), Cream);
        Text(box, $"凝核：{g.Score} → {copy.Score}\n紊乱：{g.Disorder} → {copy.Disorder}\n本轮紊乱损失：{CondensationCardGame.DisorderLoss(g.Disorder)} → {CondensationCardGame.DisorderLoss(copy.Disorder)}", 25, new Rect2(35, 185, 860, 130), Cream);
        var forecast = copy.Finished ? "警告：此操作使紊乱超过10，确认后立即失败！" : copy.SettlementPreview();
        Text(box, forecast, 20, new Rect2(35, 330, 860, 170), copy.Failed ? new Color("edb391") : Cream);
        ButtonAt(box, copy.Failed ? "仍然施展（将失败）" : "确认施展", new Rect2(600, 545, 285, 48), () =>
        {
            var result = g.Play(_selectionName, _selectionIndex, _targets, _selectionItem, _shift);
            if (result != "") { Notice(result); return; }
            HideModal(); Refresh(); ResolvePending();
        });
        ButtonAt(box, "取消", new Rect2(35, 545, 180, 48), HideModal);
    }
    private void ResolvePending()
    {
        var g = _game!;
        if (g.Finished) { ShowResult(); return; }
        if (g.Choices.Count > 0)
        {
            var box = Modal("观气 · 选择一张，其余置底", 1000, 470);
            for (var i = 0; i < g.Choices.Count; i++)
            {
                var index = i; var name = g.Choices[i];
                Text(box, CondensationCardGame.Definition(name).Description, 22, new Rect2(30 + i * 320, 100, 290, 210), Cream);
                ButtonAt(box, name, new Rect2(30 + i * 320, 340, 290, 55), () => { g.Choose(index); HideModal(); Refresh(); ResolvePending(); });
            }
        }
        else if (g.ForcedDiscards > 0)
        {
            var box = Modal($"请弃牌 · 还需弃{g.ForcedDiscards}张", 1120, 630);
            for (var i = 0; i < g.Hand.Count; i++)
            {
                var index = i; var name = g.Hand[i];
                ButtonAt(box, name + "\n" + CondensationCardGame.Definition(name).Description, new Rect2(30 + (i % 4) * 270, 100 + (i / 4) * 145, 250, 125), () => { g.Drop(index); HideModal(); Refresh(); ResolvePending(); });
            }
        }
    }
    private void ConfirmEnd()
    {
        if (_game is null || _game.Finished || _animating) return;
        var g = _game; var box = Modal(g.Round == 8 ? "最后结算 · 确认定形" : "结束本回合", 940, 530);
        Text(box, $"尚余{g.Actions}次施展，结束后不保留。\n\n" + g.SettlementPreview(), 23, new Rect2(35, 85, 870, 325), Cream);
        ButtonAt(box, "返回继续", new Rect2(35, 440, 220, 48), HideModal);
        ButtonAt(box, "确认结算", new Rect2(650, 440, 245, 48), () =>
        {
            g.EndRound(); HideModal(); Refresh();
        });
    }
    private void ShowResult()
    {
        var g = _game!;
        if (!g.Finished) return;
        if (!_testMode && !_resultReported && _target is not null)
        {
            _resultReported = true;
            CondensationFinished?.Invoke(_target, !g.Failed, g.Failed ? 0 : g.Score);
        }
        UpdateModeVisibility();
        var box = Modal(g.Failed ? "凝核未成" : "灵核已成", 1000, 740);
        var outcome = g.Failed
            ? g.Disorder > 10 ? $"紊乱升至 {g.Disorder}，超过10，凝核失败。" : $"最终凝核程度 {g.Score}，未达到100。"
            : $"全部结算后的凝核程度：{g.Score}\n成核标准：100，上不封顶。";
        Text(box, outcome, 32, new Rect2(40, 85, 920, 100), Cream);
        Text(box, string.Join("\n", g.Log.TakeLast(6)), 20, new Rect2(40, 205, 920, 180), Cream);
        var reward = Text(box, !g.Failed ? CondensationOutcomeRules.Describe(g.Score)
            : _testMode ? "试演失败不会影响角色。" : "本次不扣灵力；重伤使正式战斗生命上限暂降15%，实际休息行动累计120小时后可重新凝核。",
            21, new Rect2(40, 395, 920, 135), Cream);
        reward.Name = "CondensationOutcome";
        var applied = !_testMode && _target is { has_spirit_core: true } && _target.condensation_final_score == g.Score;
        var persistence = _testMode ? "试演只展示收益，不写入正式人物或存档。"
            : g.Failed ? "本次失败已结算；重伤恢复后可重试。"
            : applied ? "最终分数已写入人物，三项属性加成已生效；重算与读档不会重复领取。"
            : "本次结果未写入人物，请重新查看突破条件。";
        Text(box, persistence + "\n铸基云气仅保存补正，当前不生成云气，也不改变归元九息。", 19, new Rect2(40, 550, 920, 70), Cream);
        if (_testMode) ButtonAt(box, "重新构筑", new Rect2(40, 650, 240, 45), ShowSetup);
        ButtonAt(box, "查看完整记录", new Rect2(_testMode ? 340 : 210, 650, 280, 45), ShowLog);
        ButtonAt(box, _testMode ? "收起" : "返回修炼", new Rect2(_testMode ? 710 : 690, 650, 240, 45), _testMode ? HideModal : RequestClose);
    }
    private void ShowRewards(Action? returnToSetup = null)
    {
        var box = Modal("凝核分数与后续收益", 1120, 760);
        Text(box, "100分成核；超过100每满5分计一档，分数上不封顶。\n每档：体魄、灵慧、神魂各定额＋0.25；云息后期门槛倍率＋0.0125；铸基云气补正＋2.5%。",
            22, new Rect2(35, 85, 1050, 105), Cream);
        string[] headers = ["最终分数", "三项各定额", "云息后期门槛", "铸基云气补正（待兑现）"];
        float[] columns = [35, 200, 430, 680];
        for (var column = 0; column < headers.Length; column++)
            Text(box, headers[column], 21, new Rect2(columns[column], 205, column == 3 ? 405 : 210, 42), Cream);
        var scores = new[] { 100, 125, 150, 175, 200, 250 };
        for (var row = 0; row < scores.Length; row++)
        {
            var score = scores[row];
            string[] values = [score.ToString(), $"＋{CondensationOutcomeRules.FlatAttributeBonus(score):0.##}",
                $"×{CondensationOutcomeRules.SpiritRequirementMultiplier(score):0.####}", $"＋{CondensationOutcomeRules.FoundationCloudBonus(score) * 100:0.##}%"];
            for (var column = 0; column < values.Length; column++)
                Text(box, values[column], 23, new Rect2(columns[column], 250 + row * 47, column == 3 ? 405 : 210, 40), Cream);
        }
        Text(box, "门槛倍率只乘七→八、八→九、九→十的本层54／60／66缕，并逐次向上取整。\n属性是永久定额，不增加成长倍率、悟性或福缘；云气补正待玄基正式流程兑现，\n不增加归元息、不缩短养息，也不代表已经提高玄基品质。",
            20, new Rect2(35, 555, 1050, 105), Cream);
        ButtonAt(box, returnToSetup is null ? "返回牌局" : "返回准备", new Rect2(835, 680, 235, 48), returnToSetup ?? HideModal);
    }
    private void ShowItems()
    {
        if (_game is null || _animating) return;
        var box = Modal("凝核可用道具", 1030, 560);
        Text(box, "本局最多两件，各用一次；每回合最多一件，不占施展。", 23, new Rect2(35, 75, 960, 55), Cream);
        for (var i = 0; i < _game.EquippedItems.Count; i++)
        {
            var name = _game.EquippedItems[i]; var used = _game.UsedItems.Contains(name);
            var iconRegion = name.Contains("符") ? new Rect2(193, 492, 42, 70) : new Rect2(82, 510, 66, 50);
            Art(box, Reference(iconRegion), new Rect2(35, 175 + i * 145, 72, 72));
            Text(box, name + " · " + (used ? "已使用" : "未使用") + "\n" + CondensationCardGame.ItemText(name), 24, new Rect2(130, 155 + i * 145, 600, 120), Cream);
            ButtonAt(box, "使用", new Rect2(780, 165 + i * 145, 200, 55), () => SelectAction(name, -1, true)).Disabled = used || _game.ItemUsedThisRound || _game.Finished;
        }
        ButtonAt(box, "返回", new Rect2(780, 475, 200, 45), HideModal);
    }
    private void ShowPile(bool discard)
    {
        if (_game is null) return;
        var pile = discard ? _game.Discard : _game.Deck;
        foreach (var child in _modal.GetChildren()) { _modal.RemoveChild(child); child.QueueFree(); }
        _modal.Show(); _modal.MoveToFront(); _hover = -1;
        var page = new Control { Size = _canvas.Size, MouseFilter = MouseFilterEnum.Stop };
        _modal.AddChild(page);
        Art(page, Load("res://textures/ui/realm_yunxi_paper_v6.png"), new Rect2(Vector2.Zero, _canvas.Size));
        Text(page, discard ? $"弃牌堆 · {pile.Count}张" : $"抽牌堆 · {pile.Count}张", 35, new Rect2(62, 30, 1250, 52), Cream);
        Text(page, discard ? "已使用与弃置的牌，逐张展示。" : "按手法归类展示剩余牌面，排列不代表抽取顺序。", 21, new Rect2(62, 92, 1300, 42), Cream);
        InkButtonAt(page, "关 闭", new Rect2(1450, 35, 160, 55), HideModal);
        var scroll = new ScrollContainer
        {
            Position = new Vector2(62, 150), Size = new Vector2(1548, 751),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        page.AddChild(scroll);
        var grid = new GridContainer { Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 35); grid.AddThemeConstantOverride("v_separation", 30);
        scroll.AddChild(grid);
        // 所有实体牌都显示；排序仅为查阅方便，绝不公开牌库真实顺序。
        var ordered = pile.OrderBy(name => Array.FindIndex(CondensationCardGame.Pool, c => c.Name == name));
        foreach (var name in ordered)
        {
            var definition = CondensationCardGame.Definition(name);
            var cell = new Control { CustomMinimumSize = new Vector2(225, 355), MouseFilter = MouseFilterEnum.Pass };
            grid.AddChild(cell);
            var face = new Control { Position = new Vector2(12, 0), Size = new Vector2(200, 340), MouseFilter = MouseFilterEnum.Pass, TooltipText = definition.Description };
            cell.AddChild(face);
            Art(face, CondensationUiArt.Card, new Rect2(0, 0, 200, 340));
            Text(face, name, 30, new Rect2(14, 16, 172, 42), Ink, true);
            Text(face, definition.Kind, 17, new Rect2(14, 61, 172, 26), Ink, true);
            Art(face, CondensationUiArt.Icon(CardIconIndex(name)), new Rect2(22, 86, 156, 110));
            var effect = definition.Description + (definition.Kind == "压聚" ? $"\n本角色额外凝核+{_game.RootBonus}" : "");
            Text(face, effect, 18, new Rect2(17, 201, 166, 126), Ink, true);
        }
        if (pile.Count == 0) Text(page, "这里暂时没有牌。", 30, new Rect2(300, 390, 1070, 60), Cream, true);
    }
    private void ShowLog()
    {
        var box = Modal("行功记录", 1050, 770);
        var scroll = new ScrollContainer { Position = new Vector2(35, 90), Size = new Vector2(980, 585) }; box.AddChild(scroll);
        var label = new Label { Text = string.Join("\n", _game!.Log), CustomMinimumSize = new Vector2(950, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 22); scroll.AddChild(label);
        ButtonAt(box, "返回结果", new Rect2(765, 695, 235, 45), ShowResult);
    }
    private void ShowHelp()
    {
        var box = Modal("初次凝核 · 如何游玩", 1120, 790);
        var itemHelp = _testMode
            ? "② 行功：点开始行功，再点手牌，查看效果并确认。每轮三次施展，\n调息也占一次。道具每轮最多一件，本局各一次，不占施展。\n\n"
            : "② 行功：正式局不开放道具。点开始行功，再点手牌查看效果并确认；\n每轮三次施展，调息也占一次。\n\n";
        Text(box,
            "目标：八回合全部结算后，凝核程度达到100。分数上不封顶。\n\n" +
            "① 准备：起手六张，可统一换零至两张；之后每回合补足六张，\n可弃一抽一。未打的牌保留，牌库不会回洗。\n\n" +
            itemHelp +
            "③ 回弹：压聚立刻增加凝核，却会留下延迟回弹。到达时用引回\n转为凝核收益；没接住就扣分。调度牌可改变到达时间。\n\n" +
            "④ 紊乱：连续压聚会增紊乱；理气、调息可降低。凝核在同回合\n涨—降—涨或降—涨—降，额外紊乱+1。超过10立即失败！\n\n" +
            "⑤ 结算：紊乱0—10分别扣0/0/1/2/4/6/9/12/16/20/25，\n再自然下降1。最后一轮还会扣掉全部未结回弹，之后才判成核。\n\n" +
            (_testMode ? "人物：试演可调悟性、灵慧和匹配权重；" : "人物属性：正式局锁定当前人物的悟性、灵慧和匹配权重；") +
            "悟性追加换牌；灵慧提高理气效率；匹配灵根权重增加\n压聚收益。牌面与确认页会列出补正。可随时点右上玩法重看。",
            23, new Rect2(35, 85, 1050, 610), Cream);
        ButtonAt(box, "开始尝试", new Rect2(835, 715, 235, 48), HideModal);
    }
    private void ConfirmRestart()
    {
        if (!_testMode) return;
        var box = Modal("重新试演", 700, 300);
        Text(box, "结束当前试演，返回牌组与属性准备？", 25, new Rect2(35, 90, 630, 65), Cream);
        ButtonAt(box, "继续当前", new Rect2(35, 215, 240, 45), HideModal);
        ButtonAt(box, "重新构筑", new Rect2(390, 215, 265, 45), () => { _qi.StopTransition(); _animating = false; ShowSetup(); });
    }
    private void Notice(string text)
    {
        var box = Modal("提示", 800, 320);
        Text(box, text, 25, new Rect2(35, 90, 730, 105), Cream);
        ButtonAt(box, "返回", new Rect2(535, 240, 220, 45), HideModal);
    }
    private Control Modal(string title, float width, float height)
    {
        foreach (var child in _modal.GetChildren()) { _modal.RemoveChild(child); child.QueueFree(); }
        _modal.Show(); _modal.MoveToFront(); _hover = -1;
        var shade = new ColorRect { Color = new Color(0.06f, .045f, .025f, .75f), Size = _canvas.Size, MouseFilter = MouseFilterEnum.Stop }; _modal.AddChild(shade);
        var box = new Panel { Position = (_canvas.Size - new Vector2(width, height)) / 2, Size = new Vector2(width, height), MouseFilter = MouseFilterEnum.Stop };
        box.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("443b2e"), BorderColor = new Color("a18d66"), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2 });
        _modal.AddChild(box); Text(box, title, 32, new Rect2(30, 15, width - 60, 45), Cream); return box;
    }
    private void HideModal()
    {
        _modal.Hide(); _hover = -1;
        // 玩法/牌堆等窗口不能使尚未完成的选牌或弃牌消失。
        if (_game?.Busy == true && !_game.Finished) ResolvePending();
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!IsVisibleInTree()) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        if (_game?.Busy == true) return;
        if (_modal.Visible) HideModal();
        else if (_testMode && _game is not null) ConfirmRestart();
        else if (!_testMode) { GetViewport().SetInputAsHandled(); return; }
        else return;
        GetViewport().SetInputAsHandled();
    }

    public bool TryCloseTopModal()
    {
        if (!IsVisibleInTree() || !_modal.Visible || _game?.Busy == true) return false;
        HideModal();
        return true;
    }

    private void RequestClose()
    {
        if (!_testMode && _game is { Finished: false }) return;
        _resumeResultAfterRealmPresentation = false;
        _qi.StopTransition();
        Hide();
        Closed?.Invoke();
    }

    /// <summary>保留已结算的正式结果，避免牌局和高层弹窗盖住统一突破演出。</summary>
    public void SuspendResultForRealmPresentation()
    {
        if (!Visible || _testMode || !_resultReported || _game is not { Finished: true, Failed: false } ||
            _target is not { has_spirit_core: true } || _target.condensation_final_score != _game.Score) return;
        _resumeResultAfterRealmPresentation = true;
        _qi.StopTransition();
        Hide();
    }

    public void RestoreResultAfterRealmPresentation()
    {
        if (!_resumeResultAfterRealmPresentation) return;
        _resumeResultAfterRealmPresentation = false;
        if (_testMode || _game is not { Finished: true, Failed: false } || _target is not { has_spirit_core: true } || _target.condensation_final_score != _game.Score) return;
        Show(); MoveToFront(); ShowResult();
    }
    private Button Pile(Rect2 rect, Texture2D texture, Action click)
    {
        var button = new Button { Position = rect.Position, Size = rect.Size + new Vector2(0, 40) };
        ClearButton(button);
        button.AddThemeConstantOverride("outline_size", 0); button.AddThemeColorOverride("font_color", Cream); button.AddThemeFontSizeOverride("font_size", 23);
        Art(button, texture, new Rect2(Vector2.Zero, rect.Size));
        button.MouseEntered += () => button.Modulate = new Color(1.12f, 1.08f, 1f);
        button.MouseExited += () => button.Modulate = Colors.White;
        button.Pressed += click; _canvas.AddChild(button); return button;
    }
    private static Texture2D? Load(string path) => GD.Load<Texture2D>(path);
    private static Texture2D Reference(Rect2 region) => new AtlasTexture { Atlas = Load("res://docs/prototypes/凝核牌局效果图_v2.png"), Region = region, FilterClip = true };
    private static TextureRect Art(Control parent, Texture2D? texture, Rect2 rect)
    {
        // 必须先解除纹理最小尺寸限制，再装载纹理和设置尺寸。
        // 否则Atlas纸纹先将控件撑到源图大小，之后改IgnoreSize不会主动缩回。
        var node = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(node);
        node.Texture = texture;
        node.Position = rect.Position;
        node.Size = rect.Size;
        return node;
    }
    private static Label Text(Control parent, string text, int size, Rect2 rect, Color color, bool center = false)
    {
        // 先指定换行和文字框，再填内容，避免未换行的整句撑大控件。
        var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, ClipText = true, MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
        label.Position = rect.Position; label.Size = rect.Size; label.Text = text;
        return label;
    }
    private static Button ButtonAt(Control parent, string text, Rect2 rect, Action click)
    {
        var b = new Button { Text = text, Position = rect.Position, Size = rect.Size }; Style(b); b.Pressed += click; parent.AddChild(b); return b;
    }
    private static Button InkButtonAt(Control parent, string text, Rect2 rect, Action click)
    {
        // 固定原素材纵横比，按给定区域等比放入；底纹与按钮文字使用同一中心。
        var texture = CondensationUiArt.Rest;
        var ratio = texture.GetWidth() / (float)texture.GetHeight();
        var width = Mathf.Min(rect.Size.X, rect.Size.Y * ratio);
        var fitted = new Vector2(width, width / ratio);
        var button = new Button { Alignment = HorizontalAlignment.Center };
        ClearButton(button);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxTexture
            {
                Texture = texture,
                ContentMarginLeft = 8, ContentMarginRight = 8,
                ContentMarginTop = 4, ContentMarginBottom = 4,
                ModulateColor = state == "disabled" ? new Color(1, 1, 1, .45f) : state is "hover" or "focus" ? new Color(1.22f, 1.15f, 1f) : state == "pressed" ? new Color(.8f, .8f, .8f) : Colors.White
            });
        }
        button.AddThemeFontSizeOverride("font_size", 23);
        button.AddThemeColorOverride("font_color", Cream);
        button.AddThemeColorOverride("font_hover_color", new Color("fff0c7"));
        button.AddThemeColorOverride("font_disabled_color", new Color(.9f, .84f, .71f, .45f));
        parent.AddChild(button);
        button.Position = rect.Position + (rect.Size - fitted) * .5f;
        button.Size = fitted; button.Text = text;
        button.Pressed += click;
        return button;
    }
    private static int CardIconIndex(string name) => name switch
    {
        "轻压" or "徐压" or "息压" => 0,
        "重压" or "叠压" or "蓄劲" => 1,
        "引回" or "回旋" or "随势" or "分引" => 2,
        "缓行" or "催回" or "并流" => 3,
        "匀气" or "观气" or "理气" => 4,
        _ => 5
    };
    private static void Style(Button b)
    {
        b.AddThemeFontSizeOverride("font_size", 21); b.AddThemeColorOverride("font_color", Cream);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            b.AddThemeStyleboxOverride(state, new StyleBoxFlat { BgColor = new Color(state == "hover" ? "64543b" : "393227"), BorderColor = new Color("a08b64"), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 });
    }
    private static void ClearButton(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
    }
    private static StyleBox CardStyle() => new StyleBoxFlat
    {
        BgColor = new Color("c6b18b"), BorderColor = new Color("594a31"), BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ShadowColor = new Color(0, 0, 0, .35f), ShadowSize = 9, ShadowOffset = new Vector2(0, 5)
    };
}
