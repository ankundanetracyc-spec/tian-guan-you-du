using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class MainScene
{
    private YuxuMarketPanel? _yuxuMarket;
    private bool is_at_yuxu_market() => is_yuxu_member_at_peak_interior() && _player_current_yuxu_peak_id == "heguang" && !has_active_item_claim() &&
        _npc_world?.HasPendingSettlement != true && GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.IsSmoothAdvanceBusy != true;
    private void open_yuxu_market()
    {
        if (!is_at_yuxu_market() || player is null || _npc_world is null) return;
        if (_yuxuMarket is null || !GodotObject.IsInstanceValid(_yuxuMarket))
        {
            _yuxuMarket = new YuxuMarketPanel { Name = "YuxuMarketPanel" };
            GetNode<CanvasLayer>("CanvasLayer").AddChild(_yuxuMarket);
            _yuxuMarket.BackRequested += () => _yuxuMarket?.Hide();
            _yuxuMarket.Changed += mark_save_dirty;
            _yuxuMarket.ClaimRequested += claim_yuxu_market_proceeds;
        }
        _yuxuMarket.Open(player, _npc_world, is_at_yuxu_market);
    }
    private void claim_yuxu_market_proceeds()
    {
        if (!is_at_yuxu_market() || player?.entity_data is not HumanData human || human.yuxu_market_pending_stones <= 0) return;
        int offered = human.yuxu_market_pending_stones;
        var rewards = new GArray { new GDictionary { ["item_path"] = YuxuMarketRules.StonePath, ["count"] = offered, ["source"] = "坊市所得" } };
        string? BeforeCommit(GArray claimed)
        {
            if (player?.entity_data != human || human.sect_affiliation_id != YuxuSectId) return "坊市状态已变化，请重新领取。";
            int count = 0;
            foreach (var value in claimed)
            {
                if (value.VariantType != Variant.Type.Dictionary) continue;
                var entry = value.AsGodotDictionary();
                if (entry.GetValueOrDefault("item_path", "").AsString() != YuxuMarketRules.StonePath) return "领取物品不符。";
                count += entry.GetValueOrDefault("count", 0).AsInt32();
            }
            if (count < 0 || count > offered || count > human.yuxu_market_pending_stones) return "待领所得已变化，请重新领取。";
            human.yuxu_market_pending_stones -= count; offered -= count; return null;
        }
        void Refreshed(GArray _)
        {
            mark_save_dirty(); _yuxuMarket?.Refresh();
        }
        if (!present_item_claim("领取坊市所得", rewards, Refreshed, null, BeforeCommit))
            _yuxuMarket?.ShowStatus("领取界面暂未打开，请稍后再试。");
    }
}
