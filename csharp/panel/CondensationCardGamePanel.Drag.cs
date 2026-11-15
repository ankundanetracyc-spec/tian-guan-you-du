using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>凝核手牌的拖拽与结果预览；实际结算继续由 CondensationCardGame 负责。</summary>
public partial class CondensationCardGamePanel
{
    private const float HandDragThreshold = 12f;
    private const double HandHoldDelay = .35;
    private static readonly Vector2 DropCenter = new(825, 380);
    private static readonly Vector2 DropRadius = new(265, 185);
    private HandVisual? _pressedHandCard;
    private Vector2 _handPressPosition, _handDragOffset, _handDragPosition;
    private bool _handDragging, _handHoldShown, _dragSelectingTargets;
    private double _handHoldElapsed;
    private readonly record struct DragAnalysis(string Error, bool NeedsChoice, int[] Targets, bool WillFail);

    private Vector2 ToCanvas(Vector2 viewportPosition) => _canvas.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
    private static bool InsideDropZone(Vector2 point)
    {
        var offset = (point - DropCenter) / DropRadius;
        return offset.LengthSquared() <= 1f;
    }

    private void HandleHandInput(HandVisual card, InputEvent input)
    {
        if (_game is null || _animating || _game.Finished || _modal.Visible) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            card.Node.AcceptEvent();
            var position = ToCanvas(mouse.GlobalPosition);
            if (mouse.Pressed)
            {
                if (_pressedHandCard is not null) return;
                _pressedHandCard = card;
                _handPressPosition = _handDragPosition = position;
                _handDragOffset = position - card.Node.Position;
                _handHoldElapsed = 0; _handHoldShown = false;
                HoverInfoManager.HideAll();
                HoverInfoManager.SuspendFor(card.Node, true);
                card.Node.GrabFocus();
                return;
            }
            if (_pressedHandCard != card) return;
            var wasDragging = _handDragging;
            var wasHolding = _handHoldShown;
            var inside = InsideDropZone(position);
            var local = card.Node.GetGlobalTransformWithCanvas().AffineInverse() * mouse.GlobalPosition;
            var clicked = new Rect2(Vector2.Zero, card.Node.Size).HasPoint(local);
            CancelHandDrag();
            if (wasDragging && inside) DropHandCard(card);
            else if (!wasDragging && !wasHolding && clicked) ActivateHandCard(card);
            return;
        }
        if (input is InputEventMouseMotion motion && _pressedHandCard == card)
        {
            card.Node.AcceptEvent();
            _handDragPosition = ToCanvas(motion.GlobalPosition);
            if (!_handDragging && _handDragPosition.DistanceTo(_handPressPosition) >= HandDragThreshold)
            {
                _handDragging = true;
                _hover = card.Index;
                HoverInfoManager.SuspendFor(card.Node, true);
                HoverInfoManager.HideAll();
            }
            return;
        }
        if (input is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Enter or Key.Space)
        {
            card.Node.AcceptEvent();
            ActivateHandCard(card);
        }
    }

    private void ActivateHandCard(HandVisual card)
    {
        if (_game is null || _animating || _game.Finished || _game.Busy) return;
        if (_game.SwapOpen)
        {
            if (_swapSelection.Contains(card.Index)) _swapSelection.Remove(card.Index);
            else if (_swapSelection.Count < (_game.Round == 1 ? 2 : 1)) _swapSelection.Add(card.Index);
            return;
        }
        SelectAction(card.Name, card.Index, false);
    }

    private bool MoveDraggedCard(HandVisual card)
    {
        if (!_handDragging || _pressedHandCard != card) return false;
        card.X = _handDragPosition.X - _handDragOffset.X; card.Y = _handDragPosition.Y - _handDragOffset.Y;
        card.Angle = 0; card.Zoom = 1.05f; card.VX = card.VY = card.VA = card.VZ = 0;
        card.Node.Position = new Vector2(card.X, card.Y); card.Node.Rotation = 0;
        card.Node.Scale = Vector2.One * card.Zoom; card.Node.ZIndex = 60; card.Node.SelfModulate = Colors.White;
        return true;
    }

    private void UpdateHandHold(double delta)
    {
        if (_pressedHandCard is not { } card || _handDragging || _handHoldShown) return;
        _handHoldElapsed += delta;
        if (_handHoldElapsed < HandHoldDelay) return;
        _handHoldShown = true;
        LiftHeldCard(card);
        HoverInfoManager.SuspendFor(card.Node, false);
        BindCondensationHover(card.Node, card.Name, true, card.Index, true);
    }

    private void LiftHeldCard(HandVisual card)
    {
        // 留在鼠标下方时适度抬起手牌；边缘按住时只抬到不会失去鼠标的位置。
        var initial = card.Node.Position;
        var pointer = _canvas.GetGlobalTransformWithCanvas() * _handDragPosition;
        var inner = new Rect2(4, 4, card.Node.Size.X - 8, card.Node.Size.Y - 8);
        for (var lift = 60f; lift >= 2f; lift *= .5f)
        {
            card.Node.Position = initial - new Vector2(0, lift);
            var local = card.Node.GetGlobalTransformWithCanvas().AffineInverse() * pointer;
            if (!inner.HasPoint(local)) continue;
            card.X = card.Node.Position.X; card.Y = card.Node.Position.Y; card.VX = card.VY = 0;
            _handDragOffset = _handDragPosition - card.Node.Position;
            return;
        }
        card.Node.Position = initial;
    }

    private void CancelHandDrag()
    {
        if (_pressedHandCard is { } card && GodotObject.IsInstanceValid(card.Node)) HoverInfoManager.SuspendFor(card.Node, true);
        _pressedHandCard = null; _handDragging = _handHoldShown = false; _handHoldElapsed = 0; _hover = -1;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut || what == NotificationApplicationFocusOut) CancelHandDrag();
    }

    private DragAnalysis AnalyzeHandCard(HandVisual card)
    {
        var game = _game!;
        var count = game.TargetCount(card.Name);
        var legal = game.LegalTargets(card.Name).ToArray();
        var options = new List<int[]>();
        if (count == 0) options.Add([]);
        else if (count == 1) options.AddRange(legal.Select(recoil => new[] { recoil.Id }));
        else
        {
            for (var i = 0; i < legal.Length; i++)
                for (var j = i + 1; j < legal.Length; j++)
                    if (legal[i].Round != legal[j].Round) options.Add([legal[i].Id, legal[j].Id]);
        }
        if (options.Count == 0)
        {
            var error = game.CanPlay(card.Name, card.Index, []);
            if (error == "请选择所需回弹目标。") error = count == 2 ? "需要到达回合不同的两股回弹。" : "目前没有可选回弹。";
            return new(error, false, [], false);
        }
        var firstError = game.CanPlay(card.Name, card.Index, options[0]);
        if (firstError != "") return new(firstError, false, [], false);
        if (options.Count > 1)
            return new("", true, [], false);
        var result = game.Copy();
        var copyError = result.Play(card.Name, card.Index, options[0]);
        if (copyError != "") return new(copyError, false, [], false);
        return new("", false, options[0], result.Failed);
    }

    private static string DescribeDragPreview(CondensationCardGame before, CondensationCardGame after, string name)
    {
        var changes = new List<string>();
        foreach (var old in before.Recoils)
        {
            var current = after.Recoils.FirstOrDefault(recoil => recoil.Id == old.Id);
            if (current is null) changes.Add($"移除第{old.Round}回合回弹{old.Strength}");
            else if (current.Round != old.Round || current.Strength != old.Strength)
                changes.Add($"第{old.Round}回合回弹{old.Strength} → 第{current.Round}回合回弹{current.Strength}");
        }
        foreach (var recoil in after.Recoils.Where(recoil => before.Recoils.All(old => old.Id != recoil.Id)))
            changes.Add($"新增第{recoil.Round}回合回弹{recoil.Strength}");
        var recoilText = changes.Count == 0 ? "回弹节点未变" : string.Join("；", changes);
        var settlement = after.Failed ? "警告：紊乱超过10，施展后立即失败。" : after.SettlementPreview();
        string heading = after.Failed ? "风险：" : "若结束本回合：";
        return $"{name}\n凝核 {before.Score} → {after.Score}　紊乱 {before.Disorder} → {after.Disorder}\n{recoilText}\n\n{heading}\n{settlement}";
    }

    private void DropHandCard(HandVisual card)
    {
        if (_game is null || _animating || _game.Finished || _game.Busy) return;
        var analysis = AnalyzeHandCard(card); // Dropping rechecks actual hand, state and targets.
        if (analysis.Error != "") { Notice(analysis.Error); return; }
        _selectionName = card.Name; _selectionIndex = card.Index; _selectionItem = false; _shift = -1;
        _targets.Clear(); _targets.AddRange(analysis.Targets);
        if (analysis.NeedsChoice)
        {
            _dragSelectingTargets = true;
            ShowTargets();
        }
        else if (analysis.WillFail) ConfirmAction();
        else CommitSelectedAction();
    }

    private void ShowSelectedTargetPreview(Control box)
    {
        var game = _game!;
        var error = game.CanPlay(_selectionName, _selectionIndex, _targets, false, _shift);
        if (error != "")
        {
            if (_targets.Count == game.TargetCount(_selectionName)) Text(box, error, 16, new Rect2(30, 410, 880, 225), new Color("edb391"));
            return;
        }
        var copy = game.Copy(); copy.Play(_selectionName, _selectionIndex, _targets, false, _shift);
        Text(box, DescribeDragPreview(game, copy, _selectionName), 19, new Rect2(30, 410, 880, 225), copy.Failed ? new Color("edb391") : Cream);
    }

    private void CommitDraggedTargets()
    {
        if (_game is null) return;
        var error = _game.CanPlay(_selectionName, _selectionIndex, _targets, false, _shift);
        if (error != "") { Notice(error); return; }
        var copy = _game.Copy(); copy.Play(_selectionName, _selectionIndex, _targets, false, _shift);
        if (copy.Failed) ConfirmAction();
        else CommitSelectedAction();
    }

    private void CommitSelectedAction()
    {
        if (_game is null) return;
        var result = _game.Play(_selectionName, _selectionIndex, _targets, _selectionItem, _shift);
        if (result != "") { Notice(result); return; }
        _dragSelectingTargets = false; _swapSelection.Clear();
        _modal.Hide(); _hover = -1;
        Refresh(); ResolvePending();
    }
}
