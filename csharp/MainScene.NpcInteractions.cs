using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainScene
{
    private readonly List<INpcInteractionActionComponent> _worldNpcInteractionComponents =
    [
        new BasicNpcInteractionComponent(),
        new SparNpcInteractionComponent(),
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
        foreach (var component in _worldNpcInteractionComponents.ToArray())
        {
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
        var npc = _npc_world?.get_npc(_activeWorldNpcInteractionId);
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

    private sealed class SparNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.spar";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            if (owner.can_sect_spar(target))
                yield return new("spar", "切磋", () => owner.request_npc_spar(target));
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
