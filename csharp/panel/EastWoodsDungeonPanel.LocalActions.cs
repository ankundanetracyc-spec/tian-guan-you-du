using Godot;
using System;

public partial class EastWoodsDungeonPanel
{
    private string local_action_name(EastWoodsActions.ActionDef action)
    {
        if (action.Battle) return "挑战林地凶狼";
        var item = action.ItemPath.Length > 0 ? EastWoodsActions.Item(action.ItemPath) : null;
        return item is null ? action.Name : action.Name + " " + item.entity_name;
    }

    private string local_action_summary(EastWoodsActions.ActionDef action)
    {
        if (action.Battle) return "查看真实战前信息，再决定是否开战";
        var quantity = action.ItemPath.Length > 0 ? $" · 基础产量 {action.Quantity}份" : "";
        var remaining = action.Stock > 0 ? $" · 余量 {action_remaining(_current, action)}份" : "";
        return $"耗时 {action.Hours:0.#}小时 · 精力 {action.Stamina:0.#}{quantity}{remaining}";
    }

    private void execute_named_action(string node, string id)
    {
        if (GetTree().Paused || _actionModal?.Visible == true || !can_execute_action(node, id)) return;
        // The named button is the explicit confirmation. Existing settlement owns
        // all validation, inventory, time and reward writes; this is presentation only.
        _actionBusy = true;
        try { ActionRequested?.Invoke(node, id); }
        finally { _actionBusy = false; if (IsInsideTree()) refresh_action_bar(); }
    }

    private void position_current_node_actions()
    {
        if (_actionBar is null || _mapRoot is null || !IsInsideTree()) return;
        var visible = Visible && !_isTraveling && _travelPawn is null && _pendingPath.Count < 2 && _actionModal?.Visible != true;
        if (_actionBar.Visible != visible) _actionBar.Visible = visible;
        if (!visible || !_nodeButtons.TryGetValue(_current, out var node)) return;

        var width = Mathf.Min(330, Mathf.Max(180, Size.X - 32));
        if (!Mathf.IsEqualApprox(_actionBar.CustomMinimumSize.X, width))
        {
            _actionBar.CustomMinimumSize = new Vector2(width, 0);
            _actionBar.Size = new Vector2(width, 0);
        }
        var rect = node.GetGlobalRect();
        var inverse = GetGlobalTransform().AffineInverse();
        var start = inverse * rect.Position;
        var end = inverse * rect.End;
        // Wrapped labels settle after the first container layout. Shrink back
        // to their actual height instead of retaining the initial narrow-wrap height.
        var height = _actionBar.GetCombinedMinimumSize().Y;
        var fittedSize = new Vector2(width, height);
        if (!_actionBar.Size.IsEqualApprox(fittedSize)) _actionBar.Size = fittedSize;
        // Prefer the left of the current cell, away from the right-hand quest HUD.
        var x = start.X - width - 12;
        if (x < 16) x = end.X + 12;
        x = Mathf.Clamp(x, 16, Mathf.Max(16, Size.X - width - 16));
        var top = Mathf.Min(96, Mathf.Max(16, Size.Y * .1f));
        var maxY = Mathf.Max(top, Size.Y - 180 - height);
        var y = Mathf.Clamp((start.Y + end.Y - height) * .5f, top, maxY);
        var position = new Vector2(x, y);
        if (!_actionBar.Position.IsEqualApprox(position)) _actionBar.Position = position;
    }
}
