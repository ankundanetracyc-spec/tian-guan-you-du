using Godot;

public partial class CombatClashTrial
{
    private const float PlayerQueueX = 36, PlayerQueueWidth = 548, EnemyQueueX = 910, EnemyQueueWidth = 334;
    private const float QueueRowY = 375, QueueRowSpacing = 44, QueueRowHeight = 39;
    private static readonly string[] QueueNames = ["一队", "二队"];
    private static readonly Color OpenLine = new("a9b6a0"), PlanLine = new("d6c299");
    private readonly TrialQueueDropTarget[] _playerQueues = new TrialQueueDropTarget[BoardHandState.QueueCount];
    private readonly Button[] _enemyQueues = new Button[BoardHandState.QueueCount];
    private readonly Label[] _capacityLabels = new Label[BoardHandState.QueueCount];
    private readonly Label[] _enemyTargetLabels = new Label[BoardHandState.QueueCount];
    private readonly Control[] _queueContents = new Control[BoardHandState.QueueCount];
    private readonly ColorRect[,] _capacityPips = new ColorRect[BoardHandState.QueueCount, BoardHandState.DefaultQueueCapacity];
    private Control _queueLinks = null!;
    private int? _editingQueue;

    private static int QueueSpeed(bool player, int lane) => (player ? 4 : 3) - lane * 2;

    private void BuildQueues()
    {
        Place(_stage, StageLabel("我方队列", 15, Cream), PlayerQueueX + 2, 354, 142, 19);
        Place(_stage, StageLabel("对手队列", 15, Cream), EnemyQueueX + 2, 354, 142, 19);
        _queueLinks = Place(_stage, new Control { MouseFilter = MouseFilterEnum.Ignore }, 0, 0, StageSize.X, StageSize.Y);
        _queueLinks.Draw += DrawQueueLinks;
        for (var index = 0; index < BoardHandState.QueueCount; index++)
        {
            var queue = index;
            var y = QueueRowY + index * QueueRowSpacing;
            var row = new TrialQueueDropTarget
            {
                Name = $"PlayerQueue{index + 1}", MouseDefaultCursorShape = CursorShape.PointingHand,
                TooltipText = $"选择我方{QueueNames[index]}，也可把手牌拖到这里"
            };
            row.AcceptsCard = data => TryGetDraggedCard(data, out var key) && FitsQueue(key, queue);
            row.ReceiveCard = data =>
            {
                if (TryGetDraggedCard(data, out var key)) TryPlacePlayerCard(key, queue, null);
            };
            row.Pressed += () => SelectQueue(queue);
            _playerQueues[index] = Place(_stage, row, PlayerQueueX, y, PlayerQueueWidth, QueueRowHeight);
            BuildSpeedAndName(row, index, true);
            _capacityLabels[index] = Place(row, StageLabel("", 13, Cream, HorizontalAlignment.Center), 105, 1, 50, 22);
            for (var point = 0; point < BoardHandState.DefaultQueueCapacity; point++)
                _capacityPips[index, point] = Place(row, StagePlate(new Color("5e6552")), 108 + 9 * point, 27, 7, 4);
            Place(row, StagePlate(new Color("827e68", .58f)), 165, 7, 1, 25);
            _queueContents[index] = Place(row, new Control { MouseFilter = MouseFilterEnum.Ignore }, 174, 0, 365, QueueRowHeight);

            var target = new Button { Name = $"EnemyQueue{index + 1}", MouseDefaultCursorShape = CursorShape.PointingHand };
            target.Pressed += () => SelectEnemyQueue(queue);
            _enemyQueues[index] = Place(_stage, target, EnemyQueueX, y, EnemyQueueWidth, QueueRowHeight);
            BuildSpeedAndName(target, index, false);
            Place(target, StageLabel("招式隐藏", 13, LightInk), 115, 0, 91, QueueRowHeight);
            _enemyTargetLabels[index] = Place(target, StageLabel("", 13, Cream, HorizontalAlignment.Right), 207, 0, 112, QueueRowHeight);
        }
    }

    private static void BuildSpeedAndName(Control row, int index, bool player)
    {
        var seal = StagePanel(new Color("455344"), new Color("8b8d70"), 2);
        Place(row, seal, 8, 6, 32, 27);
        Place(seal, StageLabel(QueueSpeed(player, index).ToString(), 18, Cream, HorizontalAlignment.Center), 0, 0, 32, 27);
        seal.MouseFilter = MouseFilterEnum.Pass;
        seal.TooltipText = "队列速度";
        Place(row, StageLabel(QueueNames[index], 16, Cream), 49, 0, 53, QueueRowHeight);
    }

    private void SelectQueue(int queue)
    {
        if (!_planning || _finished) return;
        _editingQueue = queue;
        _planningNotice = "";
        if (_selected is { } key && TryPlacePlayerCard(key, queue, null)) return;
        RefreshQueues();
    }

    private void SelectEnemyQueue(int enemy)
    {
        if (!_planning || _finished || _editingQueue is not { } own || !QueueNeedsTarget(own)) return;
        _queueTargets[own] = enemy;
        _planningNotice = "";
        RefreshQueues();
    }

    private bool QueueNeedsTarget(int queue) => _player.Hand.GetQueue(queue).Any(item => Kind(item.Card) != QueueActionKind.Operation);

    private bool FitsQueue(BoardCardKey key, int queue)
    {
        var card = _player.Hand.Hand.FirstOrDefault(item => item.Key == key);
        return card is not null && _player.Hand.GetQueueUsed(queue) + Capacity(card) <= _player.Hand.QueueCapacity;
    }

    private bool TryGetDraggedCard(Variant data, out BoardCardKey key)
    {
        key = default;
        if (data.VariantType != Variant.Type.Object || data.AsGodotObject() is not TrialTechniqueCard source
            || source.HandKey is not { } dragged || !_handCards.TryGetValue(dragged, out var current) || current.Button != source)
            return false;
        key = dragged;
        return true;
    }

    private void RefreshQueues()
    {
        var editing = _planning && !_finished;
        var canTarget = editing && _editingQueue is { } source && QueueNeedsTarget(source);
        for (var queue = 0; queue < BoardHandState.QueueCount; queue++)
        {
            var row = _playerQueues[queue];
            var used = _player.Hand.GetQueueUsed(queue);
            var highlight = editing && _editingQueue == queue;
            var fits = _selected is { } key && FitsQueue(key, queue);
            row.Disabled = !editing;
            StyleQueue(row, highlight, fits);
            _capacityLabels[queue].Text = $"{used}/{_player.Hand.QueueCapacity}";
            for (var point = 0; point < BoardHandState.DefaultQueueCapacity; point++)
                _capacityPips[queue, point].Color = point < used ? new Color("c7b780") : new Color("575f50");
            RefreshQueueCards(queue, editing);

            var target = _enemyQueues[queue];
            target.Disabled = !canTarget;
            var current = canTarget && _editingQueue is { } own && _queueTargets[own] == queue;
            StyleQueue(target, current && editing, !target.Disabled);
            _enemyTargetLabels[queue].Text = !editing ? "" : current ? "已锁定" : target.Disabled ? "" : "选择目标";
        }
        _queueLinks.QueueRedraw();
        RefreshPlanningHint();
    }

    private void RefreshQueueCards(int queue, bool editing)
    {
        var area = _queueContents[queue];
        foreach (var child in area.GetChildren())
        {
            area.RemoveChild(child);
            child.QueueFree();
        }
        var cards = _player.Hand.GetQueue(queue);
        if (cards.Count == 0)
        {
            Place(area, StageLabel("空队列", 13, LightInk), 9, 0, 150, QueueRowHeight);
            return;
        }

        var width = Mathf.Min(98, (area.Size.X - (cards.Count - 1) * 5) / cards.Count);
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var chip = new Button
            {
                Name = "Queued_" + card.Card.Key.PlacementId, Disabled = !editing,
                TooltipText = editing ? $"点击撤回{card.Card.Skill.skill_name}" : "",
                MouseFilter = MouseFilterEnum.Pass,
                MouseDefaultCursorShape = editing ? CursorShape.PointingHand : CursorShape.Arrow
            };
            chip.AddThemeStyleboxOverride("normal", TrialBattleArt.Frame(new Color("e3d8be"), new Color("9b8d70"), 3));
            chip.AddThemeStyleboxOverride("hover", TrialBattleArt.Frame(new Color("f1e8d4"), new Color("ab9e82"), 3));
            chip.AddThemeStyleboxOverride("pressed", TrialBattleArt.Frame(new Color("c7bca2"), new Color("8d8168"), 3));
            chip.AddThemeStyleboxOverride("disabled", TrialBattleArt.Frame(new Color("bbb39d"), new Color("827b66"), 3));
            chip.AddThemeStyleboxOverride("focus", TrialBattleArt.Frame(Colors.Transparent, PlanLine, 3));
            chip.Pressed += () => WithdrawCard(card.Card.Key, queue);
            Place(area, chip, index * (width + 5), 6, width, 27);
            Place(chip, StageLabel(card.Card.Skill.skill_name, 13, CraftingPaperStyle.Ink), 7, 0, width - 26, 27);
            Place(chip, StageLabel(card.CapacityCost.ToString(), 12, new Color("706957"), HorizontalAlignment.Right), width - 20, 0, 13, 27);
        }
    }

    private static void StyleQueue(Button row, bool selected, bool available)
    {
        var fill = selected ? new Color("39473a") : new Color("29352d");
        var stroke = selected ? PlanLine : available ? OpenLine : Edge;
        row.AddThemeStyleboxOverride("normal", TrialBattleArt.Frame(fill, stroke, 3));
        row.AddThemeStyleboxOverride("hover", TrialBattleArt.Frame(new Color("415044"), PlanLine, 3));
        row.AddThemeStyleboxOverride("pressed", TrialBattleArt.Frame(new Color("303a30"), PlanLine, 3));
        row.AddThemeStyleboxOverride("disabled", TrialBattleArt.Frame(fill, Edge, 3));
        row.AddThemeStyleboxOverride("focus", TrialBattleArt.Frame(Colors.Transparent, PlanLine, 3));
    }

    private void RefreshPlanningHint()
    {
        _selection.Modulate = Colors.White;
        if (!_planning)
            _selection.Text = _finished ? "" : "正在结算已排招式";
        else if (_planningNotice.Length > 0)
        {
            _selection.Text = _planningNotice;
            _selection.Modulate = new Color("efc19c");
        }
        else if (_selected is { } key && _player.Hand.Hand.FirstOrDefault(card => card.Key == key) is { } card)
            _selection.Text = $"已选 {card.Skill.skill_name}　点击或拖到我方队列";
        else if (_editingQueue is { } own && QueueNeedsTarget(own) && _queueTargets[own] is null)
            _selection.Text = $"我方{QueueNames[own]}　点击右侧队列指定交锋目标";
        else if (_editingQueue is { } lane && _player.Hand.GetQueue(lane).Count > 0 && !QueueNeedsTarget(lane))
            _selection.Text = $"我方{QueueNames[lane]}　运转作用于自身";
        else
            _selection.Text = "点击或拖动手牌排招　·　点击队列里的招式可撤回";
    }

    private void DrawQueueLinks()
    {
        for (var own = 0; own < BoardHandState.QueueCount; own++)
        {
            if (_queueTargets[own] is not { } enemy || !QueueNeedsTarget(own)) continue;
            var from = new Vector2(PlayerQueueX + PlayerQueueWidth + 4, QueueRowY + QueueRowHeight / 2 + own * QueueRowSpacing);
            var to = new Vector2(EnemyQueueX - 6, QueueRowY + QueueRowHeight / 2 + enemy * QueueRowSpacing);
            var curve = new Vector2[19];
            for (var i = 0; i < curve.Length; i++)
            {
                var t = i / (float)(curve.Length - 1);
                curve[i] = new Vector2(Mathf.Lerp(from.X, to.X - 6, t), Mathf.Lerp(from.Y, to.Y, t * t * (3f - 2f * t)));
            }
            var color = _editingQueue == own && _planning ? PlanLine : OpenLine;
            _queueLinks.DrawPolyline(curve, color, 1.6f, true);
            _queueLinks.DrawColoredPolygon([to, to + new Vector2(-8, -4), to + new Vector2(-8, 4)], color);
        }
    }
}
