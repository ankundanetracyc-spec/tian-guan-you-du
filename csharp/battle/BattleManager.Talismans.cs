using Godot;
using System.Threading.Tasks;

public partial class BattleManager
{
    /// <summary>先核验真实来源、目标和费用，再原子扣一张；符痕在效果和演出之前写回。</summary>
    private async Task<bool> ExecuteBoardTalisman(GodotObject owner, CombatBoardPlacement placed)
    {
        if (!_boardMode || placed.kind != CombatBoardPlacement.ObjectKind.Talisman || placed.expended
            || owner is not Character actor || !Alive(owner) || !_boards.TryGetValue(owner, out var board)
            || !board.State.CanUseTalisman(placed)
            || TalismanCombatCatalog.Load(placed.source_key) is not { } product
            || TalismanCombatCatalog.Skill(product) is not { } skill || _executor is null
            || actor.get_inventory_total_count(placed.source_key) < 1
            || owner.Get("current_stamina").AsInt32() < TalismanCombatCatalog.EnergyCost) return false;
        var target = ResolveBoardActionTarget(owner, skill);
        if (!Alive(target) || !IsValidTarget(skill, owner, target)) return false;
        var outgoing = new Dictionary<string, int> { [placed.source_key] = 1 };
        if (!actor.try_exchange_inventory(outgoing, new Dictionary<string, int>(), beforeCommit: () =>
            {
                var stamina = owner.Get("current_stamina").AsInt32();
                if (placed.expended || stamina < TalismanCombatCatalog.EnergyCost) return false;
                owner.Set("current_stamina", stamina - TalismanCombatCatalog.EnergyCost);
                placed.expended = true;
                return true;
            })) return false;
        StoreBoardLayout(owner, board.State.Layout);
        gongfa_combat.Before(owner, skill, recordAsLastCast: false);
        _queuedHandledTargets.Clear();
        BeginBoardPressureAction(owner, skill, 1, TalismanCombatCatalog.EnergyCost);
        ShowBoardActionCue(owner, skill);
        var completion = new TaskCompletionSource<bool>();
        _boardAwaiting[owner] = completion;
        _executor.execute_skill(owner, skill, target!, false, true);
        await completion.Task;
        CompleteBoardPressureAction(owner);
        while (IsInsideTree() && battle_running && (_battlePaused || HasPendingActionAnimations()))
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
    }
}
