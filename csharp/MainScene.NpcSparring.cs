using Godot;
using System;

public partial class MainScene
{
    private bool can_sect_spar(WorldNpcData npc) => player?.entity_data is HumanData human && !is_npc_in_seclusion_now(npc) &&
        player.current_health > 0 && npc.current_health > 0 && NpcSparringRules.Supports(npc, human) && can_contact_social_npc(npc) &&
        _player_world_node_id == "yuxu_xianzong_core" &&
        GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview/PeakInterior")?.IsVisibleInTree() == true;

    private void request_npc_spar(WorldNpcData npc, bool proactive = false)
    {
        if (is_npc_in_seclusion_now(npc)) { explain_npc_seclusion(npc); return; }
        if (!can_sect_spar(npc) || player?.entity_data is not HumanData human || story_dialogue is null) return;
        info?.hide_panel(); npc_interactions?.close();
        var invitationEventId = Guid.NewGuid().ToString("N");
        var answered = false;
        void Answer(bool accept)
        {
            if (answered) return;
            answered = true;
            if (!can_sect_spar(npc)) { story_dialogue.show_social_result(npc, new("对方已不在此处，切磋暂且作罢。", "", [])); return; }
            if (!accept)
            {
                var declined = new NpcSocialRules.Response("这次切磋暂且作罢。", "", [proactive ? "你示意改日再试，对方便不再相邀。" : "你暂且收起切磋之意，没有发出邀约。"]) { ActionName = "告辞" };
                if (proactive)
                {
                    declined = NpcSocialRules.Remember(npc, human, "spar_declined", get_current_profile_location(),
                        get_game_month_index(GetNode<GameTimeMgr>("/root/GameTimeManager")), declined);
                    mark_save_dirty();
                }
                story_dialogue.show_social_result(npc, declined);
                return;
            }
            if (!player.can_spend_world_stamina(WorldStaminaRules.ConversationCost))
            { story_dialogue.show_social_result(npc, new("你有些疲惫，切磋留待改日。", "", [])
                { RelationshipFeedback = "敬重：切磋未开始，本次没有新增关系记事。" }); return; }
            var response = NpcSparringRules.Evaluate(npc, human);
            player.try_spend_world_stamina(WorldStaminaRules.ConversationCost);
            response = response with { StaminaSpent = WorldStaminaRules.ConversationCost };
            var month = GetNode<GameTimeMgr>("/root/GameTimeManager") is { } clock ? get_game_month_index(clock) : 0;
            response = NpcSocialRules.Remember(npc, human, "spar_invitation", get_current_profile_location(), month, response, invitationEventId);
            mark_save_dirty();
            story_dialogue.show_social_result(npc, response, response.AgreesToSpar ? () => start_npc_spar(npc) : null);
        }
        story_dialogue.show_social_choices(player, npc,
            proactive ? "对方想与你印证所学，邀你切磋一场。" : "你准备邀对方切磋，双方点到为止。",
            new (string, Action)[] {
                ($"{(proactive ? "接受" : "发出邀约")} · 精力 {WorldStaminaRules.ConversationCost:0.#}", () => Answer(true)),
                (proactive ? "婉拒" : "取消", () => Answer(false)) }, () => Answer(false), "同门切磋", proactive ? "右键婉拒" : "右键取消");
    }
    private void start_npc_spar(WorldNpcData npc)
    {
        if (!can_sect_spar(npc) || player is null) return;
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        var playerScene = player.pack_for_battle();
        var playerSnapshot = player.get_snapshot_data();
        var (opponentScene, opponentSnapshot) = create_npc_battle_package(npc, (int)WorldEntity.Faction.ENEMY);
        if (transition is null || playerScene is null || playerSnapshot is null || opponentScene is null || opponentSnapshot is null)
        { show_message("切磋未能开始", "对练配置未能载入，未开始战斗。"); return; }
        transition.set_battle_return_context(new Godot.Collections.Dictionary {
            ["kind"] = "yuxu_npc_spar", ["is_training"] = true, ["title"] = "同门切磋 · 点到为止",
            ["allow_prebattle_cancel"] = true, ["npc_id"] = npc.npc_id,
            ["origin_peak"] = _player_current_yuxu_peak_id });
        transition.start_team_battle(new Godot.Collections.Array<PackedScene> { playerScene },
            new Godot.Collections.Array<PackedScene> { opponentScene },
            new Godot.Collections.Array { playerSnapshot }, new Godot.Collections.Array { opponentSnapshot },
            GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn");
    }
    public void show_npc_spar_result(Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
        var npc = _npc_world?.get_npc(context.GetValueOrDefault("npc_id", "").AsString());
        var peak = context.GetValueOrDefault("origin_peak", "").AsString();
        Callable.From(() => {
            if (_player_world_node_id != "yuxu_xianzong_core" || string.IsNullOrWhiteSpace(peak)) return;
            estate?.Hide(); tile_world_map?.Hide();  location_interior?.Hide();
            open_faction_overview_if_available(_player_world_node_id);
            GetNodeOrNull<Node>("SceneLayer/YuxuXianzongOverview")?.Call("_on_peak_pressed", peak);
        }).CallDeferred();
        if (npc is null || story_dialogue is null) return;
        var outcome = summary.GetValueOrDefault("escaped", false).AsBool() ? "此次切磋提前结束。" :
            summary.GetValueOrDefault("is_draw", false).AsBool() ? "此次切磋不分胜负。" :
            summary.GetValueOrDefault("player_victory", false).AsBool() ? "此次切磋，你略胜一筹。" :
            summary.GetValueOrDefault("player_defeated", false).AsBool() ? "此次切磋，对方更胜一筹。" : "此次切磋已结束，未分胜负。";
        var battleId = summary.GetValueOrDefault("battle_id", "").AsString();
        var relationshipFeedback = summary.GetValueOrDefault("escaped", false).AsBool()
            ? "敬重：切磋提前结束，本次没有增加敬重。"
            : "敬重：本次未形成新的切磋记事。";
        if (battleId.Length > 0 && npc.player_social_memory.GetValueOrDefault("last_spar_battle", "").AsString() != battleId)
        {
            npc.player_social_memory["last_spar_battle"] = battleId;
            npc.player_social_memory["last_spar_outcome"] = outcome;
            var month = get_game_month_index(GetNode<GameTimeMgr>("/root/GameTimeManager"));
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var date = $"{time.year}年{time.month}月{time.day}日";
            NpcPersonalityRules.Observe(npc, "spar_experience", "交手经历：" + outcome, "宗内切磋", month);
            if (!summary.GetValueOrDefault("escaped", false).AsBool() && player?.entity_data is HumanData human && npc.is_alive)
            {
                var eventId = "sect-spar:" + battleId;
                var recorded = CharacterRelationshipRules.RecordCompletedSpar(human, npc, eventId, month);
                relationshipFeedback = recorded
                    ? CharacterRelationshipRules.DescribePlayerEvidence(human, npc.npc_id, eventId,
                        "敬重：本次切磋关系证据已处理。")
                    : "敬重：本次切磋没有新增待月结记事。";
            }
            npc.record(new NpcActionRecord { month_index = month, date_label = date, action_id = "sect_spar_result", action_name = "同门切磋",
                node_id = _player_world_node_id, subscene_id = peak, target_npc_id = "player",
                outcome = outcome + "\n" + relationshipFeedback, reason = "双方约定点到为止。" });
            mark_save_dirty();
        }
        story_dialogue.show_social_result(npc, new(outcome + "双方收手，未结算物品或贡献。", "", [])
            { ActionName = "结束切磋", RelationshipFeedback = relationshipFeedback });
    }
}
