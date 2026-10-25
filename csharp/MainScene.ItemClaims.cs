using Godot;
using Godot.Collections;
using System;
using GArray = Godot.Collections.Array;

/// <summary>游戏内通用物品领取入口，奖励来源自行决定领取与放弃后的结算。</summary>
public partial class MainScene
{
    private ItemClaimPanel? _active_item_claim;

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
        };
        if (panel.Present(player, rewards, title, onClaimed, onAbandoned)) return true;
        _active_item_claim = null;
        panel.QueueFree();
        return false;
    }
}
