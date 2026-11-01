using Godot;
using Godot.Collections;
using System;
using GArray = Godot.Collections.Array;

/// <summary>游戏内通用物品领取入口，奖励来源自行决定领取与放弃后的结算。</summary>
public partial class MainScene
{
    private ItemClaimPanel? _active_item_claim;
    // Multiple world callbacks can finish in one frame. Keep each receipt until the existing claim page closes.
    private readonly Queue<(string Title, GArray Rewards)> _world_loot_receipts = new();

    private void receive_world_loot(string title, IReadOnlyDictionary<string, int> rewards, string successMessage)
    {
        if (player is null) return;
        if (player.try_exchange_inventory(new System.Collections.Generic.Dictionary<string, int>(), rewards, announceAcquisition: true))
        {
            show_message(title, successMessage);
            return;
        }
        var entries = new GArray();
        foreach (var (path, count) in rewards)
            if (count > 0) entries.Add(new Godot.Collections.Dictionary { ["item_path"] = path, ["count"] = count });
        if (entries.Count == 0) return;
        _world_loot_receipts.Enqueue((title, entries));
        present_next_world_loot();
    }

    private void present_next_world_loot()
    {
        if (!IsInsideTree() || IsQueuedForDeletion() || _world_loot_receipts.Count == 0 || has_active_item_claim()) return;
        var receipt = _world_loot_receipts.Peek();
        if (present_item_claim(receipt.Title, receipt.Rewards)) _world_loot_receipts.Dequeue();
    }

    private void clear_world_loot_receipts() => _world_loot_receipts.Clear();

    public bool has_active_item_claim() =>
        _active_item_claim is not null && GodotObject.IsInstanceValid(_active_item_claim);

    public bool present_item_claim(
        string title,
        GArray rewards,
        Action<GArray>? onClaimed = null,
        Action<GArray>? onAbandoned = null)
    {
        if (!StartupComplete || player is null || has_active_item_claim()) return false;
        var panel = new ItemClaimPanel { Name = "ItemClaimPanel" };
        _active_item_claim = panel;
        AddChild(panel);
        panel.Closed += () =>
        {
            if (ReferenceEquals(_active_item_claim, panel)) _active_item_claim = null;
            CallDeferred(nameof(present_next_world_loot));
        };
        if (panel.Present(player, rewards, title, onClaimed, onAbandoned)) return true;
        _active_item_claim = null;
        panel.QueueFree();
        return false;
    }
}
