using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainScene
{
    private const string BanquetPlayerId = "player";
    private const string BanquetVenueHall = "gathering_hall";
    private const string BanquetVenuePavilion = "waterside_pavilion";
    private const int BanquetDurationHours = 4;
    private const double BanquetReplySeconds = 12.0;

    private readonly Array<BanquetEventData> _banquetEvents = [];
    private BanquetPanel? _banquetPanel;
    private int _lastNpcBanquetQuarter = -1;
    private double _banquetSchedulePoll;

    private static Array<BanquetEventData> duplicate_banquet_events(IEnumerable<BanquetEventData>? source)
    {
        Array<BanquetEventData> result = [];
        if (source is null) return result;
        foreach (var item in source)
            if (item is not null && item.Duplicate(true) is BanquetEventData copy)
                result.Add(copy);
        return result;
    }

    private void setup_banquets()
    {
        _banquetEvents.Clear();
        foreach (var item in _loaded_game_save?.banquet_events ?? [])
            if (item is not null && item.Duplicate(true) is BanquetEventData copy)
                _banquetEvents.Add(copy);
        normalize_npc_banquet_invitation_windows();

        _banquetPanel = new BanquetPanel { Name = "BanquetPanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(_banquetPanel);
        _banquetPanel.SetupRequested += on_banquet_setup_requested;
        _banquetPanel.ResponseChosen += on_banquet_response_chosen;
        _banquetPanel.AmbientBeatFinished += on_banquet_ambient_finished;
        _banquetPanel.ActivityExitRequested += on_banquet_exit_requested;
        register_letter_activity("banquet", attend_npc_banquet_invitation);
    }

    private void normalize_npc_banquet_invitation_windows()
    {
        var changed = false;
        foreach (var eventData in _banquetEvents.Where(item => !item.player_is_host && item.status == BanquetEventData.Scheduled))
        {
            var dayStart = Math.Max(0, eventData.starts_at_hour) / 24 * 24;
            var letter = !string.IsNullOrWhiteSpace(eventData.source_letter_id)
                ? _letterMailbox.Find(eventData.source_letter_id)
                : _letterMailbox.letters.FirstOrDefault(item => item.invitation?.activity_instance_id == eventData.event_id);
            if (letter?.invitation is not { activity_key: "banquet" } invitation) continue;
            if (eventData.starts_at_hour != dayStart) { eventData.starts_at_hour = dayStart; changed = true; }
            if (invitation.starts_at_hour != dayStart) { invitation.starts_at_hour = dayStart; changed = true; }
            if (invitation.ends_at_hour != dayStart + 24) { invitation.ends_at_hour = dayStart + 24; changed = true; }
            var replyDateEnd = Math.Min(dayStart, Math.Max(0, invitation.reply_by_hour) / 24 * 24 + 24);
            if (invitation.reply_by_hour != replyDateEnd) { invitation.reply_by_hour = replyDateEnd; changed = true; }
            if (string.IsNullOrWhiteSpace(eventData.source_letter_id) && !string.IsNullOrWhiteSpace(letter.letter_id))
            { eventData.source_letter_id = letter.letter_id; changed = true; }
        }
        if (!changed) return;
        mark_save_dirty();
        _letterMailbox.EmitChanged();
    }

    private bool is_at_heguang_banquet_venue()
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        return _player_world_node_id == "yuxu_xianzong_core" && _player_current_yuxu_peak_id == "heguang" &&
            overview?.Visible == true && overview.GetNodeOrNull<Control>("PeakInterior")?.IsVisibleInTree() == true &&
            _east_woods_dungeon?.Visible != true;
    }

    private void open_banquet_setup()
    {
        if (_banquetPanel is null || player is null || player.entity_data is not HumanData human ||
            human.sect_affiliation_id != YuxuSectId || !is_at_heguang_banquet_venue()) return;
        if (_banquetEvents.Any(item => item.status == BanquetEventData.InProgress))
        {
            show_message("同门雅集", "你正在参加另一场雅集，结束后再筹办新席。");
            return;
        }

        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var rosterNpcs = current_heguang_banquet_roster();
        var roster = rosterNpcs.Select(npc => new BanquetRosterEntry
        {
            Id = npc.npc_id,
            Name = npc.npc_name,
            Subtitle = $"{npc.formal_identity} · {npc.cultivation_realm}",
            Npc = npc,
        }).ToList();
        _banquetPanel.ShowSetup(
            new BanquetRosterEntry { Id = BanquetPlayerId, Name = human.entity_name, Subtitle = human.sect_current_rank, PlayerCharacter = player },
            roster,
            [
                new BanquetVenueOption { Id = BanquetVenueHall, Name = "同门雅集堂", CostText = "5 点贡献", CapacityIncludingHost = 12, Description = "雅集堂可容纳十二人，适合多位同门相聚。费用在开席时扣除。" },
                new BanquetVenueOption { Id = BanquetVenuePavilion, Name = "临水水榭", CostText = "2 枚灵石", CapacityIncludingHost = 6, Description = "临水水榭最多六人，适合小范围交谈。费用在开席时扣除。" },
            ],
            [
                new BanquetTimeOption { Label = "现在开席", StartsAtHour = now },
                new BanquetTimeOption { Label = $"明日 · {LetterMailbox.Date(now + 24)}", StartsAtHour = now + 24 },
                new BanquetTimeOption { Label = $"三日后 · {LetterMailbox.Date(now + 72)}", StartsAtHour = now + 72 },
            ]);
    }

    private List<WorldNpcData> current_heguang_banquet_roster()
    {
        if (_npc_world is null) return [];
        return _npc_world.get_subscene_roster("yuxu_xianzong_core", "heguang")
            .Where(is_banquet_guest_available)
            .Where(npc => !CharacterRelationshipRules.AvoidsContact(npc, BanquetPlayerId))
            .OrderBy(npc => npc.npc_name, StringComparer.Ordinal)
            .ThenBy(npc => npc.npc_id, StringComparer.Ordinal)
            .ToList();
    }

    private bool is_banquet_guest_available(WorldNpcData npc) =>
        npc is not null && npc.is_alive && !npc.is_traveling && string.IsNullOrWhiteSpace(npc.dungeon_id) &&
        !is_npc_in_seclusion_now(npc) && npc.faction_id == "yuxu_xianzong" &&
        !npc.is_yuxu_lineage_staff && !string.IsNullOrWhiteSpace(npc.npc_id);

    private void on_banquet_setup_requested(BanquetSetupRequest request)
    {
        if (request is null || _banquetPanel is null || player?.entity_data is not HumanData human ||
            human.sect_affiliation_id != YuxuSectId || !is_at_heguang_banquet_venue() || request.HostId != BanquetPlayerId)
        {
            _banquetPanel?.Close();
            return;
        }

        var venue = venue_for(request.VenueId);
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (venue is null || time is null || request.StartsAtHour < time.get_total_game_hours() ||
            request.StartsAtHour > time.get_total_game_hours() + 72)
        {
            _banquetPanel.Close();
            show_message("宴席未安排", "宴席日期或场地安排已失效，请重新安排。没有扣除费用或时间。");
            return;
        }
        if (_banquetEvents.Any(item => item.player_is_host && item.status == BanquetEventData.Scheduled &&
                request.StartsAtHour < item.starts_at_hour + Math.Max(1, item.duration_hours) &&
                request.StartsAtHour + BanquetDurationHours > item.starts_at_hour))
        {
            _banquetPanel.Close();
            show_message("宴席未安排", "你在这段时间已有一场待办雅集，请另选日期。没有扣除费用或时间。");
            return;
        }

        var currentRoster = current_heguang_banquet_roster().ToDictionary(npc => npc.npc_id, StringComparer.Ordinal);
        var guestIds = request.GuestIds.Where(id => id != BanquetPlayerId && currentRoster.ContainsKey(id))
            .Distinct(StringComparer.Ordinal).Take(venue.CapacityIncludingHost - 1).ToArray();
        if (guestIds.Length == 0)
        {
            _banquetPanel.Close();
            show_message("宴席未安排", "至少需要邀请一位此刻能够赴席的同门；没有扣除费用或时间。");
            return;
        }
        var eventId = Guid.NewGuid().ToString("N");
        var eventData = new BanquetEventData
        {
            event_id = eventId,
            host_id = BanquetPlayerId,
            host_name = human.entity_name,
            player_is_host = true,
            venue_id = venue.Id,
            starts_at_hour = request.StartsAtHour,
            duration_hours = BanquetDurationHours,
            status = BanquetEventData.Scheduled,
            invited_npc_ids = new Array<string>(guestIds),
            attendee_npc_ids = new Array<string>(guestIds),
        };
        _banquetEvents.Add(eventData);
        _banquetPanel.Close();
        mark_save_dirty();
        if (eventData.starts_at_hour <= time.get_total_game_hours())
            begin_banquet_event(eventData, false);
        else
            show_message("宴席已安排", $"已定于 {LetterMailbox.Date(eventData.starts_at_hour)} 在{venue.Name}开席。费用和四个游戏时辰会在开席时结算。");
    }

    private static BanquetVenueOption? venue_for(string venueId) => venueId switch
    {
        BanquetVenueHall => new BanquetVenueOption { Id = BanquetVenueHall, Name = "同门雅集堂", CostText = "5 点贡献", CapacityIncludingHost = 12 },
        BanquetVenuePavilion => new BanquetVenueOption { Id = BanquetVenuePavilion, Name = "临水水榭", CostText = "2 枚灵石", CapacityIncludingHost = 6 },
        _ => null,
    };

    private void process_banquet_activity(double delta)
    {
        _banquetSchedulePoll += delta;
        if (_banquetSchedulePoll < .5) return;
        _banquetSchedulePoll = 0;
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_missed_npc_banquets(now);
        process_npc_banquet_invitation_schedule();

        if (_banquetEvents.FirstOrDefault(item => item.status == BanquetEventData.InProgress) is { } active)
        {
            if (_banquetPanel?.Visible != true) resume_banquet_event(active);
            return;
        }
        var due = _banquetEvents.FirstOrDefault(item => item.status == BanquetEventData.Scheduled && item.player_is_host && item.starts_at_hour <= now);
        if (due is null) return;
        if (!is_at_heguang_banquet_venue())
        {
            due.status = BanquetEventData.Expired;
            due.finished_at_hour = now;
            mark_save_dirty();
            return;
        }
        begin_banquet_event(due, false);
    }

    private void process_npc_banquet_invitation_schedule()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return;
        var month = get_game_month_index(time);
        var quarter = month / 3;
        if (_lastNpcBanquetQuarter < 0)
        {
            _lastNpcBanquetQuarter = quarter;
            mark_save_dirty();
            return;
        }
        if (quarter <= _lastNpcBanquetQuarter) return;
        _lastNpcBanquetQuarter = quarter;
        mark_save_dirty(); // A missed quarter does not accumulate a later duplicate invitation.
        if (_npc_world is null) return;

        var roster = _npc_world.get_subscene_roster("yuxu_xianzong_core", "heguang")
            .Where(npc => is_banquet_guest_available(npc) && npc.get_resource("灵石") >= 2 &&
                !CharacterRelationshipRules.AvoidsContact(npc, BanquetPlayerId))
            .OrderBy(npc => stable_banquet_hash($"host:{quarter}:{npc.npc_id}"))
            .ThenBy(npc => npc.npc_id, StringComparer.Ordinal)
            .ToList();
        if (roster.Count == 0) return;

        var host = roster[0];
        var invitees = roster.Skip(1).Take(4).Select(npc => npc.npc_id).ToArray();
        var now = time.get_total_game_hours();
        var eventId = $"npc-banquet:{quarter}:{host.npc_id}";
        if (_banquetEvents.Any(item => item.event_id == eventId) ||
            _letterMailbox.letters.Any(letter => letter.source_event_id == "banquet_invitation:" + eventId)) return;

        var banquetStartHour = now / 24 * 24 + 72;
        var banquet = new BanquetEventData
        {
            event_id = eventId,
            host_id = host.npc_id,
            host_name = host.npc_name,
            player_is_host = false,
            venue_id = BanquetVenuePavilion,
            starts_at_hour = banquetStartHour,
            duration_hours = BanquetDurationHours,
            status = BanquetEventData.Scheduled,
            invited_npc_ids = new Array<string>(new[] { BanquetPlayerId }.Concat(invitees)),
            attendee_npc_ids = new Array<string>(new[] { host.npc_id }.Concat(invitees)),
        };
        var invite = new LetterInvitationData
        {
            activity_key = "banquet",
            activity_name = "同门雅集",
            activity_instance_id = eventId,
            host_npc_id = host.npc_id,
            location_id = "yuxu_xianzong_core",
            subscene_id = "heguang",
            place_name = "玉虚仙宗·和光峰临水水榭",
            reply_by_hour = Math.Min(banquet.starts_at_hour, (now + 48) / 24 * 24 + 24),
            starts_at_hour = banquet.starts_at_hour,
            ends_at_hour = banquet.starts_at_hour + 24,
        };
        var delivered = deliver_npc_letter(host.npc_id, "banquet_invitation:" + eventId, "邀你赴同门雅集",
            $"道友：\n\n我在和光峰临水水榭略备清茶，邀几位同门小聚。若得闲，欢迎于 {invite.StartsAtDisplay} 当日前来。\n\n席间只叙近况，不谈公事。\n\n{host.npc_name} 敬邀", invite, now);
        if (!delivered) return;
        banquet.source_letter_id = _letterMailbox.letters.FirstOrDefault(letter => letter.source_event_id == "banquet_invitation:" + eventId)?.letter_id ?? "";
        _banquetEvents.Add(banquet);
        mark_save_dirty();
    }

    private void attend_npc_banquet_invitation(LetterData letter)
    {
        if (letter?.invitation is not { activity_key: "banquet", response: "accepted" } invitation ||
            _banquetPanel is null || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time ||
            !invitation.CanAttend(time.get_total_game_hours())) return;
        if (!is_at_heguang_banquet_venue())
        {
            show_message("同门雅集", "赴约地点在和光峰临水水榭。抵达和光峰后，再从信笺中选择赴约。");
            return;
        }
        var eventData = _banquetEvents.FirstOrDefault(item => item.event_id == invitation.activity_instance_id);
        if (eventData is null || eventData.status != BanquetEventData.Scheduled)
        {
            show_message("同门雅集", "这场雅集已结束或邀约记录无法核对。");
            return;
        }
        eventData.source_letter_id = letter.letter_id;
        begin_banquet_event(eventData, true);
    }

    private void expire_missed_npc_banquets(int now)
    {
        var changed = false;
        foreach (var eventData in _banquetEvents.Where(item => !item.player_is_host &&
                     item.status == BanquetEventData.Scheduled && now >= item.starts_at_hour + 24))
        {
            eventData.status = BanquetEventData.Expired;
            eventData.finished_at_hour = now;
            changed = true;
        }
        if (changed) mark_save_dirty();
    }

    private bool begin_banquet_event(BanquetEventData eventData, bool playerAcceptedInvitation)
    {
        if (_banquetPanel is null || eventData.status != BanquetEventData.Scheduled ||
            player?.entity_data is not HumanData human || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is not { } time) return false;
        if (_banquetEvents.Any(item => item.event_id != eventData.event_id && item.status == BanquetEventData.InProgress))
        {
            show_message("同门雅集", "你正在参加另一场雅集，结束后再赴约。");
            return false;
        }

        var allNpcIds = banquet_npc_ids(eventData).Distinct(StringComparer.Ordinal).ToList();
        if (eventData.player_is_host)
            allNpcIds = eventData.invited_npc_ids.Where(id => id != BanquetPlayerId).Distinct(StringComparer.Ordinal).ToList();
        else if (!playerAcceptedInvitation || !eventData.invited_npc_ids.Contains(BanquetPlayerId))
            return false;

        var attendees = allNpcIds.Select(id => _npc_world?.get_npc(id)).Where(npc => npc is not null && is_banquet_guest_available(npc) &&
                npc.current_node_id == "yuxu_xianzong_core" && npc.current_subscene_id == "heguang")
            .Cast<WorldNpcData>().DistinctBy(npc => npc.npc_id).ToList();
        var hostPresent = eventData.player_is_host || attendees.Any(npc => npc.npc_id == eventData.host_id);
        if (attendees.Count == 0 || !hostPresent || eventData.player_is_host && !is_at_heguang_banquet_venue())
        {
            if (eventData.player_is_host)
            {
                eventData.status = BanquetEventData.Expired;
                eventData.finished_at_hour = time.get_total_game_hours();
                mark_save_dirty();
                show_message("宴席未能开席", "预定日期到时，受邀同门已不在和光峰；此次未扣费或耗时。");
            }
            else if (!hostPresent)
            {
                eventData.status = BanquetEventData.Expired;
                eventData.finished_at_hour = time.get_total_game_hours();
                if (!string.IsNullOrWhiteSpace(eventData.source_letter_id)) _letterMailbox.FailInvitation(eventData.source_letter_id);
                mark_save_dirty();
                show_message("同门雅集", "主持者已不在和光峰，这场雅集未能成行。");
            }
            return false;
        }

        if (!eventData.fee_settled && !settle_banquet_fee(eventData, human, attendees))
        {
            eventData.status = BanquetEventData.Declined;
            eventData.finished_at_hour = time.get_total_game_hours();
            mark_save_dirty();
            _banquetPanel.Close();
            show_message("宴席未能开席", "主持人未能承担场地费用；没有推进时间，也没有扣除费用。");
            return false;
        }

        eventData.attendee_npc_ids.Clear();
        foreach (var npc in attendees) eventData.attendee_npc_ids.Add(npc.npc_id);
        eventData.status = BanquetEventData.InProgress;
        if (eventData.started_at_hour < 0) eventData.started_at_hour = time.get_total_game_hours();
        eventData.fee_settled = true;
        mark_save_dirty();

        if (!eventData.time_settled)
        {
            eventData.time_settled = true;
            time.add_hours(BanquetDurationHours);
            mark_save_dirty();
        }
        var presentation = make_banquet_activity_presentation(eventData, attendees);
        _banquetPanel.StartActivity(presentation);
        if (eventData.beats.Count == 0) build_banquet_beats(eventData, human, attendees);
        mark_save_dirty();
        if (!string.IsNullOrEmpty(eventData.active_beat_id))
            show_saved_banquet_beat(eventData, eventData.active_beat_id);
        else
            _ = advance_banquet_after_quiet_gap(eventData.event_id, "宾客已落座，席间寒暄渐起。", 1.6);
        return true;
    }

    private bool settle_banquet_fee(BanquetEventData eventData, HumanData human, IReadOnlyList<WorldNpcData> attendees)
    {
        if (!eventData.player_is_host)
        {
            if (_npc_world?.get_npc(eventData.host_id) is not { } host || host.get_resource("灵石") < 2) return false;
            host.change_resource("灵石", -2);
            eventData.fee_settled = true;
            return true;
        }
        if (eventData.venue_id == BanquetVenueHall)
        {
            const int price = 5;
            var transactionId = "yuxu:banquet:" + eventData.event_id;
            if (human.sect_contribution_records.Any(record => record is not null && record.source_transaction_id == transactionId)) return true;
            if (human.get_yuxu_contribution_total() < price) return false;
            var timestamp = LetterMailbox.Date(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
            var record = new YuxuSectContributionRecord
            {
                record_id = Guid.NewGuid().ToString("N"),
                source_transaction_id = transactionId,
                transaction_kind = YuxuSectContributionRecord.KindSectHandlingFee,
                source_title = "和光峰·同门雅集堂席费",
                occurred_at = timestamp,
                contribution_delta = -price,
                cost_summary = "举办同门雅集，支付雅集堂场地费用。",
                result_summary = "同门雅集堂席费已结清。",
            };
            return human.record_yuxu_contribution(record);
        }
        if (eventData.venue_id == BanquetVenuePavilion)
        {
            const int price = 2;
            if (player is null || player.get_inventory_total_count(SpiritStoneItemPath) < price) return false;
            if (!player.remove_item_from_inventory(SpiritStoneItemPath, price)) return false;
            return true;
        }
        return false;
    }

    private static List<string> banquet_npc_ids(BanquetEventData eventData)
    {
        var ids = eventData.attendee_npc_ids.ToList();
        if (!eventData.player_is_host && !string.IsNullOrWhiteSpace(eventData.host_id) && !ids.Contains(eventData.host_id))
            ids.Insert(0, eventData.host_id);
        return ids;
    }

    private BanquetActivityPresentation make_banquet_activity_presentation(BanquetEventData eventData, IReadOnlyList<WorldNpcData> attendees)
    {
        var participants = new List<BanquetParticipantPresentation>();
        if (player?.entity_data is HumanData human && (eventData.player_is_host || eventData.invited_npc_ids.Contains(BanquetPlayerId)))
            participants.Add(new BanquetParticipantPresentation { Id = BanquetPlayerId, Name = human.entity_name, Subtitle = eventData.player_is_host ? "主持" : "应邀赴席", PlayerCharacter = player });
        foreach (var npc in attendees)
            participants.Add(new BanquetParticipantPresentation { Id = npc.npc_id, Name = npc.npc_name, Subtitle = npc.formal_identity, Npc = npc });
        var host = eventData.player_is_host ? player?.entity_data?.entity_name ?? eventData.host_name : eventData.host_name;
        return new BanquetActivityPresentation
        {
            EventId = eventData.event_id,
            Title = "同门雅集",
            HostName = host,
            VenueName = venue_for(eventData.venue_id)?.Name ?? "和光峰席间",
            Participants = participants,
        };
    }

    private void build_banquet_beats(BanquetEventData eventData, HumanData human, IReadOnlyList<WorldNpcData> attendees)
    {
        if (eventData.beats.Count > 0 || attendees.Count == 0) return;
        var renown = RegionalRenownSystem.get_bai_chuan_total(human);
        var ambientCount = 5 + Math.Min(2, attendees.Count / 2) + (renown >= 200 ? 1 : 0);
        var promptCount = Math.Clamp(1 + (attendees.Count >= 3 ? 1 : 0) + (renown >= 200 ? 1 : 0), 1, 3);
        var pool = attendees.OrderBy(npc => stable_banquet_hash($"speaker:{eventData.event_id}:{npc.npc_id}")).ToList();
        var rankAwareSpeakers = attendees
            .Where(NpcSocialRules.HeardTrueDisciple)
            .OrderBy(npc => stable_banquet_hash($"{eventData.event_id}:rank-speaker:{npc.npc_id}"))
            .ToList();
        var playerHasPublicRank = human.sect_current_rank is "真传" or "真传弟子";
        var prompts = BanquetDialogueLibrary.PlayerPrompts
            .Where(prompt => !prompt.RequiresPublicRank || (playerHasPublicRank && rankAwareSpeakers.Count > 0))
            .OrderBy(prompt => stable_banquet_hash($"{eventData.event_id}:prompt:{prompt.Id}"))
            .Take(promptCount)
            .ToArray();
        promptCount = prompts.Length;
        var promptAfterAmbient = prompts.Select((_, index) => Math.Clamp((index + 1) * ambientCount / (promptCount + 1), 1, ambientCount)).ToArray();
        var speakerOffset = (int)(stable_banquet_hash($"{eventData.event_id}:speaker-offset") % (ulong)pool.Count);
        var beatIndex = 0;
        var promptIndex = 0;
        var usedAmbientExchangeIds = new HashSet<string>(StringComparer.Ordinal);

        WorldNpcData speaker_at(int index) => pool[(speakerOffset + index) % pool.Count];

        void add_ambient_line(string lineId, string lineText, WorldNpcData speaker)
        {
            var displaySeconds = 2.6 + (stable_banquet_hash($"{eventData.event_id}:ambient-duration:{lineId}") % 4) * 0.2;
            eventData.beats.Add(new Dictionary
            {
                ["beat_id"] = $"{eventData.event_id}:beat:{beatIndex}",
                ["kind"] = "ambient",
                ["speaker_id"] = speaker.npc_id,
                ["speaker_name"] = speaker.npc_name,
                ["text"] = lineText,
                ["heard_by"] = new Array<string>(),
                ["choices"] = new Array<Dictionary>(),
                ["display_seconds"] = displaySeconds,
            });
            beatIndex++;
        }

        static string attribute_label(string attribute) => attribute switch
        {
            "charisma" => "魅力",
            "social" => "交际",
            "comprehension" => "悟性",
            "management" => "治事",
            "intelligence" => "灵慧",
            _ => attribute,
        };

        static float attribute_value(HumanData data, string attribute) => attribute switch
        {
            "charisma" => data.charisma,
            "social" => data.social,
            "comprehension" => data.comprehension,
            "management" => data.management,
            "intelligence" => data.intelligence,
            _ => 0,
        };

        string banquet_address(WorldNpcData speaker)
        {
            if (NpcSocialRules.KnowsName(speaker) && !string.IsNullOrWhiteSpace(human.entity_name)) return $"{human.entity_name}道友";
            if (playerHasPublicRank && NpcSocialRules.HeardTrueDisciple(speaker)) return "真传道友";
            return "道友";
        }

        void add_prompt(BanquetPlayerPrompt prompt, int promptNumber)
        {
            var speaker = prompt.RequiresPublicRank
                ? rankAwareSpeakers[promptNumber % rankAwareSpeakers.Count]
                : speaker_at(ambientCount * 2 + promptNumber);
            var choices = new Array<Dictionary>();
            foreach (var reply in prompt.Replies)
            {
                var requiredAttribute = reply.RequiredAttribute ?? "";
                var minimumAttendees = Math.Max(0, reply.MinimumAttendees);
                var currentValue = requiredAttribute.Length == 0 ? 0 : attribute_value(human, requiredAttribute);
                var attributeRequirement = requiredAttribute.Length == 0
                    ? ""
                    : $"需{attribute_label(requiredAttribute)}≥{reply.MinimumAttribute:0.#}（当前{currentValue:0.#}）";
                var attendeeRequirement = minimumAttendees == 0
                    ? ""
                    : $"需至少{minimumAttendees}位同门在席（当前{attendees.Count}位）";
                var requirementText = string.Join("　·　", new[] { attributeRequirement, attendeeRequirement }.Where(text => !string.IsNullOrWhiteSpace(text)));
                choices.Add(new Dictionary
                {
                    ["id"] = reply.Id,
                    ["tone"] = reply.Tone,
                    ["text"] = reply.Text,
                    ["required_attribute"] = requiredAttribute,
                    ["required_value"] = reply.MinimumAttribute,
                    ["minimum_attendees"] = minimumAttendees,
                    ["requirement_text"] = requirementText,
                    ["available"] = (requiredAttribute.Length == 0 || currentValue >= reply.MinimumAttribute) &&
                        (minimumAttendees == 0 || attendees.Count >= minimumAttendees),
                });
            }
            var heardBy = new Array<string>();
            foreach (var npc in attendees) heardBy.Add(npc.npc_id);
            eventData.beats.Add(new Dictionary
            {
                ["beat_id"] = $"{eventData.event_id}:beat:{beatIndex}",
                ["kind"] = "player_prompt",
                ["speaker_id"] = speaker.npc_id,
                ["speaker_name"] = speaker.npc_name,
                ["text"] = prompt.Text.Replace("{address}", banquet_address(speaker), StringComparison.Ordinal),
                ["heard_by"] = heardBy,
                ["choices"] = choices,
                ["display_seconds"] = 0.0,
            });
            beatIndex++;
        }

        for (var index = 0; index < ambientCount; index++)
        {
            var firstSpeaker = speaker_at(index * 2);
            WorldNpcData? replySpeaker = null;
            if (pool.Count > 1)
            {
                replySpeaker = speaker_at(index * 2 + 1);
                if (replySpeaker.npc_id == firstSpeaker.npc_id) replySpeaker = speaker_at(index * 2 + 2);
            }
            var exchange = BanquetDialogueLibrary.SelectAmbientExchange(
                firstSpeaker,
                replySpeaker,
                $"{eventData.event_id}:ambient:{index}");
            for (var attempt = 1; usedAmbientExchangeIds.Contains(exchange.Id) && attempt <= 8; attempt++)
                exchange = BanquetDialogueLibrary.SelectAmbientExchange(
                    firstSpeaker,
                    replySpeaker,
                    $"{eventData.event_id}:ambient:{index}:alternate:{attempt}");
            usedAmbientExchangeIds.Add(exchange.Id);
            add_ambient_line($"{exchange.Id}:opening", exchange.Opening, firstSpeaker);
            if (replySpeaker is not null)
                add_ambient_line($"{exchange.Id}:reply", exchange.Reply, replySpeaker);
            while (promptIndex < prompts.Length && promptAfterAmbient[promptIndex] <= index + 1)
            {
                add_prompt(prompts[promptIndex], promptIndex);
                promptIndex++;
            }
        }
    }

    private void on_banquet_ambient_finished(string eventId, string beatId)
    {
        var eventData = _banquetEvents.FirstOrDefault(item => item.event_id == eventId && item.status == BanquetEventData.InProgress);
        if (eventData is null || eventData.active_beat_id != beatId) return;
        var beat = eventData.beats.FirstOrDefault(entry => entry.GetValueOrDefault("beat_id", "").AsString() == beatId);
        if (beat is null || beat.GetValueOrDefault("choices", new Array<Dictionary>()).AsGodotArray<Dictionary>().Count > 0) return;
        eventData.active_beat_id = "";
        eventData.next_beat_index = Math.Max(eventData.next_beat_index, eventData.beats.IndexOf(beat) + 1);
        mark_save_dirty();
        _ = advance_banquet_after_quiet_gap(eventId, "席间话音渐远，过了片刻，又有人接起新的话头。", 0.9);
    }

    private void on_banquet_response_chosen(BanquetResponseSelection selection)
    {
        if (selection is null || _banquetEvents.FirstOrDefault(item => item.event_id == selection.EventId && item.status == BanquetEventData.InProgress) is not { } eventData ||
            string.IsNullOrWhiteSpace(selection.BeatId) || eventData.responses.Any(entry => entry.GetValueOrDefault("beat_id", "").AsString() == selection.BeatId)) return;
        var beat = eventData.beats.FirstOrDefault(entry => entry.GetValueOrDefault("beat_id", "").AsString() == selection.BeatId);
        if (beat is null) return;
        var choices = beat.GetValueOrDefault("choices", new Array<Dictionary>()).AsGodotArray<Dictionary>().ToList();
        if (choices.Count == 0) return;
        var availableChoices = choices.Where(item => item.GetValueOrDefault("available", true).AsBool()).ToList();
        Dictionary? chosen = null;
        if (!selection.TimedOut)
            chosen = availableChoices.FirstOrDefault(item => item.GetValueOrDefault("id", "").AsString() == selection.ChoiceId);
        if (chosen is null)
        {
            if (availableChoices.Count == 0) return;
            var seed = stable_banquet_hash($"timeout:{eventData.event_id}:{selection.BeatId}");
            chosen = availableChoices[(int)(seed % (ulong)availableChoices.Count)];
        }
        var tone = chosen.GetValueOrDefault("tone", "").AsString();
        var responseText = chosen.GetValueOrDefault("text", "").AsString();
        var heardBy = beat.GetValueOrDefault("heard_by", new Array<string>()).AsGodotArray<string>().ToList();
        eventData.responses.Add(new Dictionary
        {
            ["beat_id"] = selection.BeatId,
            ["choice_id"] = chosen.GetValueOrDefault("id", "").AsString(),
            ["tone"] = tone,
            ["text"] = responseText,
            ["requirement_text"] = chosen.GetValueOrDefault("requirement_text", "").AsString(),
            ["required_attribute"] = chosen.GetValueOrDefault("required_attribute", "").AsString(),
            ["required_value"] = chosen.GetValueOrDefault("required_value", 0.0f).AsSingle(),
            ["minimum_attendees"] = chosen.GetValueOrDefault("minimum_attendees", 0).AsInt32(),
            ["heard_by"] = new Array<string>(heardBy),
            ["timed_out"] = selection.TimedOut,
            ["resolved_at_hour"] = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0,
        });
        record_banquet_response_observations(eventData, selection.BeatId, tone, responseText, heardBy);
        eventData.active_beat_id = "";
        eventData.next_beat_index = Math.Max(eventData.next_beat_index, eventData.beats.IndexOf(beat) + 1);
        mark_save_dirty();
        _banquetPanel?.ShowInterlude("你的回应落在席间，几位同门各自接过话头，片刻后又静下来。");
        if (eventData.next_beat_index >= eventData.beats.Count)
            _ = conclude_banquet_after_quiet_gap(eventData.event_id);
        else
            _ = advance_banquet_after_quiet_gap(eventData.event_id, "茶盏轻响，席间暂歇片刻。", 2.4);
    }

    private void record_banquet_response_observations(BanquetEventData eventData, string beatId, string tone, string text, IEnumerable<string> heardBy)
    {
        if (player?.entity_data is not HumanData human) return;
        var month = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time ? get_game_month_index(time) : 0;
        foreach (var npcId in heardBy.Distinct(StringComparer.Ordinal))
        {
            if (!banquet_npc_ids(eventData).Contains(npcId) || _npc_world?.get_npc(npcId) is not { } npc) continue;
            var actionId = $"banquet-response:{eventData.event_id}:{beatId}:{npcId}";
            if (npc.action_history.Any(record => record is not null && record.dungeon_incident_id == actionId)) continue;
            var date = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } currentTime
                ? LetterMailbox.Date(currentTime.get_total_game_hours())
                : "席间";
            npc.record(new NpcActionRecord
            {
                month_index = month,
                date_label = date,
                action_id = "banquet_response",
                action_name = "宗门宴席回应",
                node_id = "yuxu_xianzong_core",
                subscene_id = "heguang",
                target_npc_id = BanquetPlayerId,
                dungeon_incident_id = actionId,
                reason = tone,
                outcome = text,
            });
            var heardBefore = eventData.responses.Take(Math.Max(0, eventData.responses.Count - 1))
                .Any(response => response.GetValueOrDefault("heard_by", new Array<string>()).AsGodotArray<string>().Contains(npcId));
            if (!heardBefore)
                CharacterRelationshipRules.RecordConversation(human, npc, $"banquet:{eventData.event_id}:{npcId}", month);
        }
    }

    private void show_saved_banquet_beat(BanquetEventData eventData, string beatId)
    {
        var beat = eventData.beats.FirstOrDefault(entry => entry.GetValueOrDefault("beat_id", "").AsString() == beatId);
        if (beat is null) return;
        _banquetPanel?.ShowBeat(make_banquet_beat_presentation(eventData, beat));
    }

    private BanquetBeatPresentation make_banquet_beat_presentation(BanquetEventData eventData, Dictionary beat)
    {
        var choices = beat.GetValueOrDefault("choices", new Array<Dictionary>()).AsGodotArray<Dictionary>()
            .Select(item => new BanquetChoicePresentation
            {
                Id = item.GetValueOrDefault("id", "").AsString(),
                Tone = item.GetValueOrDefault("tone", "").AsString(),
                Text = item.GetValueOrDefault("text", "").AsString(),
                RequirementText = item.GetValueOrDefault("requirement_text", "").AsString(),
                IsAvailable = item.GetValueOrDefault("available", true).AsBool(),
            }).ToArray();
        return new BanquetBeatPresentation
        {
            EventId = eventData.event_id,
            BeatId = beat.GetValueOrDefault("beat_id", "").AsString(),
            SpeakerId = beat.GetValueOrDefault("speaker_id", "").AsString(),
            SpeakerName = beat.GetValueOrDefault("speaker_name", "").AsString(),
            Text = beat.GetValueOrDefault("text", "").AsString(),
            HeardByIds = beat.GetValueOrDefault("heard_by", new Array<string>()).AsGodotArray<string>().ToArray(),
            Choices = choices,
            DisplaySeconds = beat.GetValueOrDefault("display_seconds", 3.4).AsDouble(),
            ResponseSeconds = BanquetReplySeconds,
        };
    }

    private async System.Threading.Tasks.Task advance_banquet_after_quiet_gap(string eventId, string quietText, double seconds)
    {
        if (_banquetPanel is null) return;
        _banquetPanel.ShowInterlude(quietText);
        await ToSignal(GetTree().CreateTimer(seconds, false), SceneTreeTimer.SignalName.Timeout);
        if (_banquetEvents.FirstOrDefault(item => item.event_id == eventId && item.status == BanquetEventData.InProgress) is not { } eventData ||
            _banquetPanel?.IsActivityOpen != true) return;
        if (eventData.next_beat_index >= eventData.beats.Count)
        {
            await conclude_banquet_after_quiet_gap(eventId);
            return;
        }
        var beat = eventData.beats[eventData.next_beat_index];
        eventData.active_beat_id = beat.GetValueOrDefault("beat_id", "").AsString();
        mark_save_dirty();
        _banquetPanel.ShowBeat(make_banquet_beat_presentation(eventData, beat));
    }

    private async System.Threading.Tasks.Task conclude_banquet_after_quiet_gap(string eventId)
    {
        var eventData = _banquetEvents.FirstOrDefault(item => item.event_id == eventId && item.status == BanquetEventData.InProgress);
        if (eventData is null) return;
        eventData.status = BanquetEventData.Completed;
        eventData.active_beat_id = "";
        eventData.finished_at_hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (!string.IsNullOrWhiteSpace(eventData.source_letter_id)) complete_letter_invitation(eventData.source_letter_id);
        mark_save_dirty();
        _banquetPanel?.ShowConclusion("四个游戏时辰的雅集渐近尾声，席间发生的回应与往来已各自记下。");
        await ToSignal(GetTree().CreateTimer(2.2, false), SceneTreeTimer.SignalName.Timeout);
        if (_banquetEvents.Any(item => item.event_id == eventId && item.status == BanquetEventData.Completed)) _banquetPanel?.Close();
    }

    private void on_banquet_exit_requested()
    {
        var eventData = _banquetEvents.FirstOrDefault(item => item.status == BanquetEventData.InProgress);
        if (eventData is null) return;
        eventData.status = BanquetEventData.LeftEarly;
        eventData.active_beat_id = "";
        eventData.finished_at_hour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (!string.IsNullOrWhiteSpace(eventData.source_letter_id)) complete_letter_invitation(eventData.source_letter_id);
        mark_save_dirty();
        _banquetPanel?.ShowConclusion("你提前离席，已发生的回应与交谈仍保留；席面和时辰已在开席时结算。");
        _ = close_concluded_banquet_after_quiet_gap(eventData.event_id);
    }

    private async System.Threading.Tasks.Task close_concluded_banquet_after_quiet_gap(string eventId)
    {
        await ToSignal(GetTree().CreateTimer(1.8, false), SceneTreeTimer.SignalName.Timeout);
        if (_banquetEvents.Any(item => item.event_id == eventId && item.status == BanquetEventData.LeftEarly)) _banquetPanel?.Close();
    }

    private void resume_banquet_event(BanquetEventData eventData)
    {
        if (_banquetPanel is null || player?.entity_data is not HumanData || _banquetPanel.IsActivityOpen) return;
        var attendees = banquet_npc_ids(eventData).Select(id => _npc_world?.get_npc(id)).Where(npc => npc is not null)
            .Cast<WorldNpcData>().DistinctBy(npc => npc.npc_id).ToList();
        _banquetPanel.StartActivity(make_banquet_activity_presentation(eventData, attendees));
        if (!string.IsNullOrWhiteSpace(eventData.active_beat_id)) show_saved_banquet_beat(eventData, eventData.active_beat_id);
        else _ = advance_banquet_after_quiet_gap(eventData.event_id, "宴席从保存处继续，席间众人重新拾起话头。", 1.6);
    }

    private void resume_saved_banquet_if_any()
    {
        if (_banquetEvents.FirstOrDefault(item => item.status == BanquetEventData.InProgress) is { } eventData)
            resume_banquet_event(eventData);
    }

    private static ulong stable_banquet_hash(string value)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (var ch in value) { hash ^= ch; hash *= 1099511628211UL; }
            return hash;
        }
    }
}
