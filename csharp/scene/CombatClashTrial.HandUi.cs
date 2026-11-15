using Godot;

public partial class CombatClashTrial
{
    private sealed class HandCardVisual(TrialTechniqueCard button, Vector2 home, float angle)
    {
        public TrialTechniqueCard Button { get; } = button;
        public float X = home.X, Y = home.Y, Angle = angle;
        public float VelocityX, VelocityY, VelocityAngle;
    }

    private Control _handLayer = null!;
    private readonly Dictionary<BoardCardKey, HandCardVisual> _handCards = [];
    private readonly List<BoardCardKey> _handOrder = [];
    private BoardCardKey? _hoverCard;
    private BoardCardKey? _draggingCard;

    private (Vector2 Position, float Angle) HandPose(int index, int count)
    {
        var width = TrialTechniqueCard.CardSize.X;
        var spacing = Mathf.Min(152f, (_handLayer.Size.X - width - 12f) / Math.Max(1, count - 1));
        return CardHandMotion.FanPose(index, count, new Vector2((_handLayer.Size.X - width) / 2f, 5f), spacing, 3f, .024f);
    }

    private void RefreshHand()
    {
        var hand = _player.Hand.Hand;
        var keys = hand.Select(card => card.Key).ToHashSet();
        foreach (var key in _handCards.Keys.Where(key => !keys.Contains(key)).ToArray())
        {
            var button = _handCards[key].Button;
            _handLayer.RemoveChild(button);
            button.QueueFree();
            _handCards.Remove(key);
        }
        if (_hoverCard is { } hover && !keys.Contains(hover)) _hoverCard = null;
        _handOrder.Clear();
        for (var i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            var key = card.Key;
            _handOrder.Add(key);
            if (_handCards.ContainsKey(key)) continue;
            var skill = card.Skill;
            var kind = Kind(card) switch { QueueActionKind.Operation => "运转", QueueActionKind.Ranged => "远攻", _ => "近攻" };
            var effect = card.SourceKey == Qi
                ? $"获得炁 {QiGain(skill)}\n周天时法力回满"
                : $"基础伤害 {Damage(card, _player)}";
            var data = new TrialTechniqueCardData(skill.skill_name, kind, Capacity(card), skill.energy_cost, skill.mana_cost,
                card.SourceKey == Qi ? "" : LingjiRange(card, _player), effect, skill.icon_path);
            var (home, tilt) = HandPose(i, hand.Count);
            var button = new TrialTechniqueCard(data)
            {
                Name = "Hand_" + key.PlacementId, HandKey = key,
                Position = home, Rotation = tilt
            };
            button.Pressed += () => SelectHandCard(key);
            button.DragStarted += BeginHandDrag;
            _handLayer.AddChild(button);
            _handCards.Add(key, new HandCardVisual(button, home, tilt));
        }
        RefreshHandStates();
    }

    private void SelectHandCard(BoardCardKey key)
    {
        if (!_planning || _finished || _draggingCard is not null) return;
        _selected = _selected == key ? null : key;
        _planningNotice = "";
        RefreshHandStates();
        RefreshQueues();
    }

    private void BeginHandDrag(BoardCardKey key)
    {
        _draggingCard = key;
        _selected = key;
        _planningNotice = "";
        RefreshHandStates();
        RefreshQueues();
    }

    private void RefreshHandStates()
    {
        foreach (var (key, visual) in _handCards)
        {
            visual.Button.Disabled = !_planning || _finished;
            visual.Button.SetPressedNoSignal(_selected == key);
            visual.Button.RefreshSelection();
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || _draggingCard is null) return;
        _draggingCard = null;
        if (IsInsideTree()) RefreshHandStates();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false }) return;
        if (_planning && _selected is not null)
        {
            _selected = null;
            _planningNotice = "";
            RefreshHandStates();
            RefreshQueues();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_handOrder.Count == 0) return;
        if (_planning && _draggingCard is null && !Input.IsMouseButtonPressed(MouseButton.Left))
            _hoverCard = FindHoveredCard();
        var (steps, dt) = CardHandMotion.Steps(delta);
        for (var i = 0; i < _handOrder.Count; i++)
        {
            var key = _handOrder[i];
            var visual = _handCards[key];
            var (home, tilt) = HandPose(i, _handOrder.Count);
            var selected = _planning && _selected == key;
            var hovered = _planning && _hoverCard == key;
            var targetY = home.Y - (selected ? 22f : hovered ? 13f : 0f);
            var angle = selected || hovered ? 0f : tilt;
            for (var n = 0; n < steps; n++)
            {
                CardHandMotion.Spring(ref visual.X, ref visual.VelocityX, home.X, 190, 19, dt);
                CardHandMotion.Spring(ref visual.Y, ref visual.VelocityY, targetY, 250, 21, dt);
                CardHandMotion.Spring(ref visual.Angle, ref visual.VelocityAngle, angle, 220, 17, dt);
            }
            visual.Button.Position = new Vector2(visual.X, visual.Y);
            visual.Button.Rotation = visual.Angle;
            visual.Button.ZIndex = selected ? 22 : hovered ? 20 : i;
        }
    }

    private BoardCardKey? FindHoveredCard()
    {
        foreach (var key in _handOrder)
            if (_handCards[key].Button.IsHovered()) return key;
        // Keep the hover while the pointer sits in the part vacated by the lift.
        var index = _hoverCard is { } previous ? _handOrder.IndexOf(previous) : -1;
        if (index < 0) return null;
        var (home, _) = HandPose(index, _handOrder.Count);
        return new Rect2(home, TrialTechniqueCard.CardSize).HasPoint(_handLayer.GetLocalMousePosition()) ? _hoverCard : null;
    }
}
