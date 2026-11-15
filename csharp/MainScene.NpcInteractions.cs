using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainScene
{
    private bool is_npc_in_seclusion_now(WorldNpcData npc)
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        return npc is not null && npc.is_in_seclusion_at_hour(now);
    }

    private void explain_npc_seclusion(WorldNpcData npc)
    {
        if (is_npc_in_seclusion_now(npc))
            show_message("对方正在闭关", $"{npc.npc_name}正在静室修行，暂不接待交谈、交易或切磋。你可以留下结识拜帖。");
    }

    private readonly List<INpcInteractionActionComponent> _worldNpcInteractionComponents =
    [
        new AcquaintanceNoteNpcInteractionComponent(),
        new BasicNpcInteractionComponent(),
        new BaishiTownNpcInteractionComponent(),
        new SparNpcInteractionComponent(),
        new AttackNpcInteractionComponent(),
        new ProbeNpcInteractionComponent(),
        new PurchaseNpcInteractionComponent(),
        new YuxuCaseNpcInteractionComponent()
    ];
    private string _activeWorldNpcInteractionId = "";
    private CellCharacterList? _interactionRoster;
    private double _npcInteractionRefreshElapsed;

    public void register_world_npc_interaction_component(INpcInteractionActionComponent component)
    {
        if (component is null || string.IsNullOrWhiteSpace(component.ComponentId)) return;
        foreach (var existing in _worldNpcInteractionComponents.Where(existing => existing.ComponentId == component.ComponentId).ToArray())
        {
            existing.ActionsChanged -= on_interaction_component_actions_changed;
            _worldNpcInteractionComponents.Remove(existing);
        }
        _worldNpcInteractionComponents.Add(component);
        component.ActionsChanged += on_interaction_component_actions_changed;
        refresh_active_world_npc_interaction(true);
    }

    public bool unregister_world_npc_interaction_component(string componentId)
    {
        var removed = false;
        foreach (var component in _worldNpcInteractionComponents.Where(component => component.ComponentId == componentId).ToArray())
        {
            component.ActionsChanged -= on_interaction_component_actions_changed;
            removed |= _worldNpcInteractionComponents.Remove(component);
        }
        if (removed) refresh_active_world_npc_interaction(true);
        return removed;
    }

    private void connect_world_npc_interaction_components()
    {
        foreach (var component in _worldNpcInteractionComponents)
            component.ActionsChanged += on_interaction_component_actions_changed;
    }

    private void disconnect_world_npc_interaction_components()
    {
        foreach (var component in _worldNpcInteractionComponents)
            component.ActionsChanged -= on_interaction_component_actions_changed;
    }

    private void on_interaction_component_actions_changed() => refresh_active_world_npc_interaction(true);

    private IEnumerable<NpcInteractionAction> collect_world_npc_interaction_actions(WorldNpcData npc)
    {
        if (baishi_present_actor(npc.npc_id) is not null)
        {
            foreach (var action in baishi_actor_actions(npc)) yield return action;
            foreach (var action in new SparNpcInteractionComponent().GetActions(this, npc)) yield return action;
            foreach (var action in new AttackNpcInteractionComponent().GetActions(this, npc)) yield return action;
            foreach (var action in new ProbeNpcInteractionComponent().GetActions(this, npc)) yield return action;
            yield break;
        }
        var contactFailure = social_contact_failure_reason(npc);
        if (contactFailure.Length > 0)
        {
            yield return new("npc.unavailable", "当前暂不可交互", static () => { }, false, contactFailure);
            yield break;
        }
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var secluded = npc.is_in_seclusion_at_hour(now);
        foreach (var component in _worldNpcInteractionComponents.ToArray())
        {
            if (secluded && component.ComponentId != "npc.acquaintance_note") continue;
            if (!secluded && component.ComponentId == "npc.acquaintance_note") continue;
            IEnumerable<NpcInteractionAction>? supplied;
            try { supplied = component.GetActions(this, npc); }
            catch (Exception error)
            {
                GD.PushWarning($"NPC 交互组件 {component.ComponentId} 生成选项失败：{error.Message}");
                continue;
            }
            if (supplied is null) continue;
            foreach (var action in supplied)
                if (action is not null && !string.IsNullOrWhiteSpace(action.Id) && !string.IsNullOrWhiteSpace(action.Label))
                    yield return action;
        }
    }

    private void process_active_world_npc_interaction_refresh(double delta)
    {
        if (npc_interactions?.Visible != true)
        {
            _npcInteractionRefreshElapsed = 0;
            return;
        }
        _npcInteractionRefreshElapsed += delta;
        if (_npcInteractionRefreshElapsed < .2) return;
        _npcInteractionRefreshElapsed = 0;
        refresh_active_world_npc_interaction(false);
    }

    private void refresh_active_world_npc_interaction(bool force = true)
    {
        if (string.IsNullOrWhiteSpace(_activeWorldNpcInteractionId) || npc_interactions?.Visible != true) return;
        var npc = baishi_present_actor(_activeWorldNpcInteractionId) ?? _npc_world?.get_npc(_activeWorldNpcInteractionId);
        if (npc is null || !GodotObject.IsInstanceValid(npc) || !npc.is_alive)
        {
            _activeWorldNpcInteractionId = "";
            npc_interactions.close();
            return;
        }
        npc_interactions.refresh_menu_actions(force);
    }

    private void on_world_npcs_changed_for_interactions() => refresh_active_world_npc_interaction();

    private void on_interaction_roster_closed()
    {
        _activeWorldNpcInteractionId = "";
        npc_interactions?.close();
    }

    private sealed class BasicNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.basic";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            yield return new("talk", "交谈", () => owner.open_world_npc_talk(target));
            yield return new("trade", "交易", () => owner.open_world_npc_trade(target));
        }
    }

    private sealed class AcquaintanceNoteNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.acquaintance_note";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            var now = owner.GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
            if (target.is_in_seclusion_at_hour(now))
                yield return new("leave_acquaintance_note", "留下结识拜帖", () => owner.leave_acquaintance_note(target));
        }
    }

    /// <summary>在闭关人物门外留下结识拜帖；仅保存待处理事实，不即时建立关系、交换姓名或刷新窗口。</summary>
    public bool leave_acquaintance_note(WorldNpcData npc)
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (npc is null || !GodotObject.IsInstanceValid(npc) || !npc.is_alive || !can_contact_social_npc(npc) || !npc.is_in_seclusion_at_hour(now)) return false;
        npc.acquaintance_notes ??= [];
        if (npc.acquaintance_notes.Any(note => note.GetValueOrDefault("status", "").AsString() == "pending"))
        {
            show_message(npc.npc_name, "先前留下的拜帖尚待对方出关后处理。 ");
            return false;
        }
        npc.acquaintance_notes.Add(new Godot.Collections.Dictionary
        {
            ["note_id"] = Guid.NewGuid().ToString("N"),
            ["received_hour"] = now,
            ["status"] = "pending",
            ["resolved_month"] = -1,
        });
        mark_save_dirty();
        show_message(npc.npc_name, "你将结识拜帖留在静室门外。待对方出关后，才会决定是否回应。 ");
        refresh_active_world_npc_interaction();
        return true;
    }

    private sealed class SparNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.spar";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            var reason = owner.npc_spar_unavailable_reason(target);
            yield return new("spar", "切磋", () => owner.request_npc_spar(target), reason.Length == 0, reason);
        }
    }

    private sealed class AttackNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.attack";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            var reason = owner.npc_attack_unavailable_reason(target);
            yield return new("attack", "攻击", () => owner.request_npc_attack(target), reason.Length == 0, reason);
        }
    }

    private sealed class PurchaseNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.purchase";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            if (owner.can_npc_purchase(target))
                yield return new("purchase", "求购", () => owner.request_npc_purchase(target));
        }
    }

    private sealed class YuxuCaseNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.yuxu_case";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            var supplied = new List<(string, Action)>();
            owner.add_yuxu_case_npc_actions(target, supplied);
            foreach (var (label, action) in supplied)
                yield return new($"yuxu_case.{label}", label, action);
        }
    }

}
