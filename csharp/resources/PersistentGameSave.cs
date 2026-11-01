using Godot;
using Godot.Collections;

/// <summary>Versioned snapshot for one playable save slot.</summary>
[GlobalClass]
public partial class PersistentGameSave : Resource
{
    public const int CurrentFormatVersion = 12;

    [Export] public int format_version = CurrentFormatVersion;
    [Export] public string slot_id = "";
    [Export] public string save_name = "苏醒者";
    [Export] public long saved_at_unix;

    [Export] public HumanData? player_data;
    [Export] public LetterMailbox letter_mailbox = new();
    [Export] public YuxuSectTreasuryLedger yuxu_sect_treasury = new();
    [Export] public Dictionary battle_reward_ledger = [];
    [Export] public int year = 1;
    [Export] public int month = 1;
    [Export] public int day = 1;
    [Export] public int hour;
    [Export] public float pending_hours;
    [Export] public string weather_type = "";
    [Export] public float weather_intensity;
    [Export] public float weather_wind_speed;
    [Export] public float weather_duration_hours;

    [Export] public Vector2I world_cell = new(38, 166);
    [Export] public string current_node_id = "home_village";
    [Export] public byte[] explored_map_cells = [];
    [Export] public Array<Vector2I> remaining_travel_path = [];
    [Export] public Dictionary node_world_state = [];

    [Export] public Array<WorldNpcData> world_npcs = [];
    [Export] public Array<EastWoodsNpcIncidentRecord> east_woods_npc_incidents = [];
    [Export] public bool npc_simulation_enabled;
    [Export] public int npc_last_processed_month = -1;
    [Export] public int npc_population_version;

    /// <summary>Scene-specific milestone flags, journal entries, and quest presentation state.</summary>
    [Export] public Dictionary scene_progress = [];

    /// <summary>Independent limited-time dungeon instances and their schedule cursor.</summary>
    [Export] public Dictionary dynamic_dungeon_state = [];

    /// <summary>宗门宴席的邀请、排期、对话进度及单次回应记录。</summary>
    [Export] public Array<BanquetEventData> banquet_events = [];

    public bool migrate_to_current_format()
    {
        if (format_version < 1 || format_version > CurrentFormatVersion) return false;
        yuxu_sect_treasury ??= new YuxuSectTreasuryLedger();
        letter_mailbox ??= new LetterMailbox();

        while (format_version < CurrentFormatVersion)
        {
            if (format_version == 1)
            {
                // v1 to v2 retained the same Resource envelope; newly added fields use their defaults.
                format_version = 2;
                continue;
            }
            if (format_version == 2)
            {
                yuxu_sect_treasury.backfill_v2_delivery_records(player_data);
                format_version = 3;
                continue;
            }
            if (format_version == 3)
            {
                // v3 saves have no incident ledger; older NPC action text is not parsed into evidence.
                east_woods_npc_incidents ??= [];
                format_version = 4;
                continue;
            }
            if (format_version == 4)
            {
                // v4 saves have no weather snapshot; an empty weather_type asks WeatherMgr to recalculate it.
                weather_type ??= "";
                format_version = 5;
                continue;
            }
            if (format_version == 5)
            {
                // 旧旅程从空信匣开始，不用旧 NPC 自由文案补造历史来信。
                format_version = 6;
                continue;
            }
            if (format_version == 6)
            {
                migrate_relationships_v6_to_v7();
                format_version = 7;
                continue;
            }
            if (format_version == 7)
            {
                // Old saves start at their saved clock. The first world activation may attempt
                // one new opportunity, but there is no historical schedule or recovery catch-up.
                var currentHour = Math.Max(0, (Math.Max(1, year) - 1) * 360 * 24 +
                    (Math.Clamp(month, 1, 12) - 1) * 30 * 24 +
                    (Math.Clamp(day, 1, 30) - 1) * 24 + Math.Clamp(hour, 0, 23));
                dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(currentHour);
                format_version = 8;
                continue;
            }
            if (format_version == 8)
            {
                // v8 没有宴席实例；已存在的信件、NPC 和副本快照不作推断或改写。
                banquet_events ??= [];
                format_version = 9;
                continue;
            }
            if (format_version == 9)
            {
                if (player_data is not null)
                {
                    player_data.combat_skill_references = new Array<CombatSkillReference>(player_data.combat_skill_slot_paths.Take(10).Select(CombatSkillReference.FromKey));
                    // 未手动装配且没有旧路径的存档仍由角色默认技能池初始化。
                    if (player_data.combat_skill_references.Count > 0 || player_data.combat_skill_slots_configured)
                        while (player_data.combat_skill_references.Count < 10) player_data.combat_skill_references.Add(new());
                    player_data.used_spirit_perception = 0;
                }
                format_version = 10;
                continue;
            }
            if (format_version == 10)
            {
                migrate_removed_agility_v10_to_v11();
                format_version = 11;
                continue;
            }
            if (format_version == 11)
            {
                // v12 stores automatic inspiration refinement thresholds as bottleneck-penalty percentages.
                // Existing tier thresholds remain available for lazy conversion by HumanData.
                format_version = 12;
                continue;
            }
            return false;
        }
        return true;
    }

    private void migrate_removed_agility_v10_to_v11()
    {
        if (player_data is not null)
        {
            var coordinationShare = Math.Max(0f, player_data.base_agility) * .5f;
            player_data.base_physique += coordinationShare;
            player_data.base_soul += coordinationShare;
            player_data.physique += coordinationShare;
            player_data.soul += coordinationShare;
            player_data.base_agility = 0;
            player_data.agility = 0;
        }

        world_npcs ??= [];
        foreach (var npc in world_npcs)
        {
            if (npc is null) continue;
            var coordinationShare = Math.Max(0f, npc.agility) * .5f;
            npc.physique += coordinationShare;
            npc.soul += coordinationShare;
            npc.agility = 0;
        }
    }

    private void migrate_relationships_v6_to_v7()
    {
        var month = (Math.Max(1, year) - 1) * 12 + Math.Clamp(this.month, 1, 12) - 1;
        world_npcs ??= [];
        if (player_data is not null)
        {
            player_data.directed_relationships ??= [];
            player_data.relationship_last_processed_month = month;
            // v6 没有主角的独立关系轴；不从 NPC 单一分数或经历文案推算新数值。
        }
        foreach (var npc in world_npcs)
        {
            if (npc is null) continue;
            npc.directed_relationships ??= [];
            npc.player_social_memory ??= [];
            npc.relation_tags ??= [];
            npc.personal_relationships ??= [];
            npc.relationship_last_processed_month = month;
            var playerMarker = npc.player_social_memory.GetValueOrDefault("relationship", "").AsString();
            if (playerMarker is "friend" or "enemy")
                CharacterRelationshipRules.PreserveLegacyMarker(npc, CharacterRelationshipRules.PlayerCharacterId, playerMarker);
            foreach (var tag in npc.relation_tags)
            {
                if (tag.StartsWith("仇敌:", StringComparison.Ordinal) && tag.Length > 3)
                    CharacterRelationshipRules.PreserveLegacyMarker(npc, tag[3..], "enemy");
                else if (tag.StartsWith("盟友:", StringComparison.Ordinal) && tag.Length > 3)
                    CharacterRelationshipRules.PreserveLegacyMarker(npc, tag[3..], "friend");
            }
            foreach (var relation in npc.personal_relationships)
            {
                var marker = relation.Value.AsString();
                if (marker is "friend" or "enemy")
                    CharacterRelationshipRules.PreserveLegacyMarker(npc, relation.Key.AsString(), marker);
            }
            // action_history 留作可核验旧事，待新规则产生的新事件才影响四项关系。
        }
    }
}
