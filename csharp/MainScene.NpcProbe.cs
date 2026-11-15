using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using D = Godot.Collections.Dictionary;

public partial class MainScene
{
    private D _pendingNpcProbe = [];
    private string _probePresentedEvent = "";
    private double _probePoll;
    private bool _probeLocationRestored;
    private WorldNpcData? probe_target(string id) => baishi_present_actor(id) ?? _npc_world?.get_npc(id);
    private string current_conflict_space() => _east_woods_dungeon?.Visible == true
        ? _east_woods_dungeon.get_current_route_node_id()
        : location_interior?.Visible == true ? location_interior.get_current_subscene_id() : _player_current_yuxu_peak_id;
    private bool current_location_forbids_fighting() => WorldConflictRules.Forbidden(
        _world_node_data?.get_node(_player_world_node_id), current_conflict_space());

    private string npc_probe_unavailable_reason(WorldNpcData npc)
    {
        var contact = social_contact_failure_reason(npc);
        if (contact.Length > 0) return contact;
        if (is_npc_in_seclusion_now(npc)) return "对方正在闭关，无法当面探查。";
        if (player?.entity_data is not HumanData || player.current_health <= 0) return "当前无法探查。";
        if (player.cultivation_realm == "凡人") return "踏入云息后才可运用灵识探查。";
        if (!player.can_spend_world_stamina(WorldStaminaRules.InteractionCost)) return "精力不足。";
        if (GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager") is not { } battle ||
            battle.has_open_flow_view() || battle.is_flow_transition_active()) return "当前还有交手尚未结束。";
        return "";
    }

    private void request_npc_probe(WorldNpcData npc)
    {
        if (_pendingNpcProbe.Count > 0) { present_pending_npc_probe(); return; }
        var failure = npc_probe_unavailable_reason(npc);
        if (failure.Length > 0) { show_message("暂不能探查", failure); return; }
        var id = npc.npc_id; var eventId = Guid.NewGuid().ToString("N");
        info?.hide_panel(); npc_interactions?.close();
        void Cancel() { if (probe_target(id) is { } target) open_world_npc_actions(target); }
        story_dialogue?.show_narration_choices("", "你准备以灵识探察" + npc.npc_name + "。若被察觉，对方可能出言质问，甚至动手。",
            new (string, Action)[] {
                ($"探查 · 精力 {WorldStaminaRules.InteractionCost:0.#}", () => confirm_npc_probe(id, eventId)),
                ("作罢", Cancel) }, Cancel);
    }

    private void confirm_npc_probe(string id, string eventId)
    {
        var npc = probe_target(id);
        if (npc is null) return;
        if (_pendingNpcProbe.Count > 0 || NpcProbeKnowledge.HasAttempt(npc, eventId))
        { if (_pendingNpcProbe.Count > 0) present_pending_npc_probe(); return; }
        var reason = npc_probe_unavailable_reason(npc);
        if (reason.Length > 0) { show_message("未能探查", reason); return; }
        if (player?.can_spend_world_stamina(WorldStaminaRules.InteractionCost) != true)
        { show_message("精力不足", "你有些疲惫，暂且收起了探查的念头。"); return; }
        commit_npc_probe(id, eventId, GD.Randf(), GD.Randf());
    }

    private void commit_npc_probe(string npcId, string eventId, double successRoll, double concealRoll)
    {
        var npc = probe_target(npcId);
        if (npc is null || eventId.Length == 0) return;
        if (NpcProbeKnowledge.HasAttempt(npc, eventId)) { if (_pendingNpcProbe.Count > 0) present_pending_npc_probe(); return; }
        if (_pendingNpcProbe.Count > 0) { present_pending_npc_probe(); return; }
        var failure = npc_probe_unavailable_reason(npc);
        if (failure.Length > 0) { show_message("未能探查", failure); return; }
        if (player?.entity_data is not HumanData || !player.try_spend_world_stamina(WorldStaminaRules.InteractionCost))
        { show_message("精力不足", "你有些疲惫，暂且收起了探查的念头。"); return; }
        var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
        var date = $"{time.year}年{time.month}月{time.day}日{time.hour}时";
        var result = NpcProbeRules.Resolve(player.soul, npc.soul, successRoll, concealRoll);
        NpcProbeKnowledge.RememberAttempt(npc, eventId, result.Success, result.Detected, date, get_game_month_index(time));
        if (result.Success) NpcProbeKnowledge.Capture(npc, eventId, date, time.get_total_game_hours());
        mark_save_dirty(); hud?.refresh_player_status();
        if (!result.Detected)
        {
            npc_interactions?.close();
            story_dialogue?.show_narration("灵识悄然收回。你已探得对方的一些底细，对方没有察觉。", () => info?.show_npc_attributes(npc));
            return;
        }
        var repeated = npc.player_social_memory.GetValueOrDefault("probe_warned", false).AsBool();
        var ratio = Math.Max(1, player.soul) / Math.Max(1, npc.soul);
        var forbidden = current_location_forbids_fighting();
        var protectedRole = is_npc_protected_from_hostility(npc) || _npc_world?.get_npc(npc.npc_id) != npc;
        var reaction = NpcProbeRules.Respond(npc, ratio, repeated, forbidden, protectedRole);
        npc.player_social_memory["probe_warned"] = true;
        npc.player_social_memory["last_detected_probe"] = eventId;
        npc.player_social_memory["probe_observed_pressure"] = ratio >= 2 ? "来势较强" : ratio <= .5 ? "来势较弱" : "来势相近";
        npc.record(new NpcActionRecord { month_index = get_game_month_index(time), date_label = date,
            action_id = "noticed_player_probe", action_name = "察觉窥探", node_id = _player_world_node_id,
            subscene_id = current_conflict_space(), target_npc_id = "player", reason = "察觉到玩家的灵识试探。",
            outcome = reaction == NpcProbeRules.Reaction.Tolerate ? "当面提醒后收手。" : "要求玩家收回灵识并说明来意。" });
        _pendingNpcProbe = new D { ["event_id"] = eventId, ["npc_id"] = npcId, ["success"] = result.Success,
            ["phase"] = "notice", ["reaction_phase"] = reaction == NpcProbeRules.Reaction.Attack ? "threat" : reaction == NpcProbeRules.Reaction.Reprimand ? "question" : "result",
            ["observation"] = (result.Success ? "你已探得对方的一些底细，但灵识尚未收回，对方便转头看了过来。" : "你没能探清对方的底细，反倒惊动了对方。") +
                (forbidden && reaction == NpcProbeRules.Reaction.Reprimand ? "\n此处禁斗，对方没有当场动手。" : ""),
            ["forbidden"] = forbidden, ["speaker"] = npc.npc_name,
            ["repeated"] = repeated, ["ratio"] = ratio, ["protected"] = protectedRole,
            ["origin_node"] = _player_world_node_id, ["body"] = probe_reaction_text(npc, reaction, repeated, forbidden, result.Success) };
        _pendingNpcProbe["return_context"] = npc_interaction_return_context();
        _probeLocationRestored = true;
        _probePresentedEvent = "";
        mark_save_dirty(); present_pending_npc_probe();
    }

    private static string probe_reaction_text(WorldNpcData npc, NpcProbeRules.Reaction reaction, bool repeated, bool forbidden, bool success)
    {
        var speech = reaction switch {
            NpcProbeRules.Reaction.Tolerate => NpcPersonalityRules.Is(npc, "forgiveness", "宽和")
                ? "看够了就收回去。下回先问一声。" : "道友，灵识越界了。收回去吧。",
            NpcProbeRules.Reaction.Attack => repeated ? "已经提醒过你了。还要试？那就别走了。" : "灵识探到我身上，还想当作没事？",
            _ => repeated ? "刚才已经提醒过你，还要再试？" : "道友，探人根底前，总该先打声招呼吧？" };
        return speech;
    }

    private void present_pending_npc_probe()
    {
        if (_pendingNpcProbe.Count == 0 || npc_interactions is null || has_active_item_claim() ||
            _pendingNpcProbe.GetValueOrDefault("phase", "").AsString() == "battle") return;
        if (!_probeLocationRestored)
        {
            _probeLocationRestored = true;
            restore_npc_interaction_location(_pendingNpcProbe.GetValueOrDefault("return_context", new D()).AsGodotDictionary());
            Callable.From(present_pending_npc_probe).CallDeferred(); return;
        }
        var id = _pendingNpcProbe.GetValueOrDefault("npc_id", "").AsString();
        var npc = probe_target(id);
        if (npc is null) { finish_npc_probe(false); return; }
        _activeWorldNpcInteractionId = "";
        info?.hide_panel(); npc_interactions.close();
        _probePresentedEvent = _pendingNpcProbe.GetValueOrDefault("event_id", "").AsString();
        var phase = _pendingNpcProbe.GetValueOrDefault("phase", "").AsString();
        var eventId = _probePresentedEvent;
        if (phase == "notice")
        {
            story_dialogue?.hide_speaker_portrait();
            story_dialogue?.show_narration(_pendingNpcProbe.GetValueOrDefault("observation", "").AsString().Replace("神识", "灵识"),
                () => advance_probe_dialogue(eventId, "notice", "reaction_phase"));
            return;
        }
        if (phase == "reply")
        {
            if (player is not null) story_dialogue?.show_player_speech(player.entity_name,
                _pendingNpcProbe.GetValueOrDefault("reply_player", "").AsString().Replace("神识", "灵识"), player,
                () => advance_probe_dialogue(eventId, "reply", "next_phase"));
            return;
        }
        var speaker = _pendingNpcProbe.GetValueOrDefault("speaker", npc.npc_name).AsString();
        Action cancel = phase == "threat" ? start_npc_probe_retaliation : () => finish_npc_probe(false);
        story_dialogue?.show_bottom_choices(speaker, _pendingNpcProbe.GetValueOrDefault("body", "").AsString().Replace("神识", "灵识"),
            speaker == "旁白" ? null : npc, pending_probe_actions(npc).Select(a => (a.Label, a.Activate)), cancel);
    }

    private void advance_probe_dialogue(string eventId, string phase, string nextKey)
    {
        if (_pendingNpcProbe.GetValueOrDefault("event_id", "").AsString() != eventId ||
            _pendingNpcProbe.GetValueOrDefault("phase", "").AsString() != phase) return;
        _pendingNpcProbe["phase"] = _pendingNpcProbe.GetValueOrDefault(nextKey, "result").AsString();
        mark_save_dirty(); present_pending_npc_probe();
    }

    private IEnumerable<NpcInteractionAction> pending_probe_actions(WorldNpcData npc)
    {
        var phase = _pendingNpcProbe.GetValueOrDefault("phase", "").AsString();
        if (phase == "question")
        {
            yield return new("probe.apologize", "是我唐突", () => answer_npc_probe("apologize"));
            yield return new("probe.explain", "说明来意", () => answer_npc_probe("explain"));
            yield return new("probe.defy", "何必动气", () => answer_npc_probe("defy"));
            yield return new("probe.leave", "收回灵识，告辞", () => finish_npc_probe(false));
        }
        else if (phase == "threat") yield return new("probe.fight", "应战", start_npc_probe_retaliation);
        else
        {
            if (NpcProbeKnowledge.HasSnapshot(npc))
                yield return new("probe.view", _pendingNpcProbe.GetValueOrDefault("success", false).AsBool() ? "查看探得属性" : "查看上次记录", () => finish_npc_probe(true));
            yield return new("probe.done", "告辞", () => finish_npc_probe(false));
        }
    }

    private void answer_npc_probe(string choice)
    {
        if (_pendingNpcProbe.GetValueOrDefault("phase", "").AsString() != "question") return;
        var npc = probe_target(_pendingNpcProbe.GetValueOrDefault("npc_id", "").AsString());
        if (npc is null || !can_contact_social_npc(npc)) { finish_npc_probe(false); return; }
        var defiant = choice == "defy";
        var reaction = defiant ? NpcProbeRules.Respond(npc, _pendingNpcProbe.GetValueOrDefault("ratio", 1d).AsDouble(),
            true, current_location_forbids_fighting(), is_npc_protected_from_hostility(npc), true) : NpcProbeRules.Reaction.Tolerate;
        var playerLine = choice == "apologize" ? "是我唐突，抱歉。" : defiant ? "不过看一眼，何必动气。" : "只是想确认道友的修为，并无别的意思。";
        var response = reaction == NpcProbeRules.Reaction.Attack ? "既然如此，就别只用灵识了。" : defiant
            ? "此事我记下了。把灵识收好。" : NpcPersonalityRules.Is(npc, "forgiveness", "记怨")
                ? "这次不与你计较。别有下一回。" : "那就到此为止。";
        npc.player_social_memory["last_probe_reply"] = choice;
        _pendingNpcProbe["phase"] = "reply";
        _pendingNpcProbe["next_phase"] = reaction == NpcProbeRules.Reaction.Attack ? "threat" : "result";
        _pendingNpcProbe["reply_player"] = playerLine;
        _pendingNpcProbe["speaker"] = npc.npc_name;
        _pendingNpcProbe["body"] = response + (current_location_forbids_fighting() && defiant ? "\n此处禁斗，我不在这里与你动手。" : "");
        mark_save_dirty(); present_pending_npc_probe();
    }

    private void finish_npc_probe(bool view)
    {
        var id = _pendingNpcProbe.GetValueOrDefault("npc_id", "").AsString();
        _pendingNpcProbe.Clear(); _probePresentedEvent = "";
        npc_interactions?.close(); mark_save_dirty();
        if (view && probe_target(id) is { } npc) info?.show_npc_attributes(npc);
    }

    private void start_npc_probe_retaliation()
    {
        if (_pendingNpcProbe.GetValueOrDefault("phase", "").AsString() != "threat") return;
        var id = _pendingNpcProbe.GetValueOrDefault("npc_id", "").AsString();
        var npc = probe_target(id);
        if (npc is null || npc_attack_unavailable_reason(npc).Length > 0)
        {
            _pendingNpcProbe["phase"] = "result";
            _pendingNpcProbe["speaker"] = "旁白";
            _pendingNpcProbe["body"] = "这场争执没有打起来。已经探得的信息仍可查看。";
            mark_save_dirty(); present_pending_npc_probe(); return;
        }
        npc_interactions?.close();
        var eventId = _pendingNpcProbe.GetValueOrDefault("event_id", "").AsString();
        if (start_npc_hostile_battle(id, true, eventId))
        { _pendingNpcProbe["phase"] = "battle"; mark_save_dirty(); }
        else present_pending_npc_probe();
    }

    private void process_npc_probe_interaction(double delta)
    {
        if (_pendingNpcProbe.Count == 0) return;
        _probePoll += delta; if (_probePoll < .2) return; _probePoll = 0;
        var phase = _pendingNpcProbe.GetValueOrDefault("phase", "").AsString();
        if (phase == "battle")
        {
            var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
            if (!_returning_from_battle && transition?.has_open_flow_view() != true && transition?.is_flow_transition_active() != true)
            {
                // 战前/战中使用既有世界快照存档，读档后仍面对本次冲突，不重扣探查费。
                _pendingNpcProbe["phase"] = "threat"; _probePresentedEvent = "";
                mark_save_dirty(); present_pending_npc_probe();
            }
            return;
        }
        if (has_active_item_claim() || story_dialogue?.Visible == true) return;
        var id = _pendingNpcProbe.GetValueOrDefault("npc_id", "").AsString();
        if (_probePresentedEvent != _pendingNpcProbe.GetValueOrDefault("event_id", "").AsString())
        { present_pending_npc_probe(); return; }
        if (phase == "threat") start_npc_probe_retaliation();
        else finish_npc_probe(false);
    }

    private void finish_probe_retaliation(D context, string outcome)
    {
        if (!context.GetValueOrDefault("npc_initiated", false).AsBool() || _pendingNpcProbe.Count == 0) return;
        _pendingNpcProbe["phase"] = "result";
        _pendingNpcProbe["speaker"] = "旁白";
        var npc = _npc_world?.get_npc(context.GetValueOrDefault("npc_id", "").AsString());
        _pendingNpcProbe["body"] = outcome + (npc is { is_alive: false } ? "对方伤重身亡。" : "") +
            "\n这次交手已经结束。探查所得仍是交手前的记录。";
        _probePresentedEvent = "";
    }

    private sealed class ProbeNpcInteractionComponent : NpcInteractionActionComponent
    {
        public override string ComponentId => "npc.probe";
        public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target)
        {
            var reason = owner.npc_probe_unavailable_reason(target);
            yield return new("probe", "探查", () => owner.request_npc_probe(target), reason.Length == 0, reason);
            if (NpcProbeKnowledge.HasSnapshot(target))
                yield return new("probe.cached", "查看探查记录", () => owner.info?.show_npc_attributes(target));
        }
    }
}
