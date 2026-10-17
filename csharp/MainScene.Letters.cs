using Godot;

public partial class MainScene
{
    private LetterMailbox _letterMailbox = new();
    private LetterPanel? _letters;
    private readonly Dictionary<string, Action<LetterData>> _letterActivities = [];

    private void setup_letters()
    {
        _letters = new LetterPanel { Name = "LetterPanel" };
        GetNode<CanvasLayer>("CanvasLayer").AddChild(_letters);
        _letters.Bind(_letterMailbox); _letters.ActivityAvailable = key => _letterActivities.ContainsKey(key);
        _letters.AttendanceRequested += attend_letter_invitation;
        _letterMailbox.Changed += on_letters_changed;
        if (GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time) time.TimeAdvanceStarting += capture_letter_closure;
        on_letters_changed();
    }
    private void open_letters() => _letters?.Open();
    private void on_letters_changed()
    { hud?.set_letter_counts(_letterMailbox.letters.Count, _letterMailbox.UnreadCount); mark_save_dirty(); }

    /// <summary>真实事件使用稳定来源 ID 投递；不猜测活动、附件或奖励。</summary>
    public bool deliver_letter(LetterData letter)
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (letter.sent_at_hour > now || !_letterMailbox.Deliver(letter)) return false;
        if (letter.opens_acquaintance_window && !string.IsNullOrWhiteSpace(letter.sender_npc_id) &&
            _npc_world?.get_npc(letter.sender_npc_id) is { } sender)
        {
            // 以实际收信时刻开启窗口；投递去重成功后才刷新，不读取信件或生成关系证据。
            sender.acquaintance_window_until_hour = now + 5 * 360 * 24;
            mark_save_dirty();
        }
        return true;
    }
    public bool deliver_npc_letter(string npcId, string sourceEventId, string subject, string body, LetterInvitationData? invitation = null, int sentAtHour = -1, bool opensAcquaintanceWindow = false)
    {
        if (_npc_world?.get_npc(npcId) is not { } npc) return false;
        return deliver_letter(new LetterData
        {
            source_event_id = sourceEventId, sender_npc_id = npcId, sender_name = npc.npc_name,
            sender_was_known = NpcSocialRules.KnowsName(npc), subject = subject, body = body,
            sent_at_hour = sentAtHour >= 0 ? sentAtHour : GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours(), invitation = invitation,
            opens_acquaintance_window = opensAcquaintanceWindow,
            delivery_place = get_current_profile_location()
        });
    }
    /// <summary>活动自身注册实际导航／参与流程；框架不会自动传送或判定完成。</summary>
    public void register_letter_activity(string key, Action<LetterData> attend)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _letterActivities[key] = attend;
        if (_letters?.Visible == true) _letters.Open();
    }
    public bool complete_letter_invitation(string id) => _letterMailbox.CompleteInvitation(id);
    private void attend_letter_invitation(LetterData letter)
    {
        var now = GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours();
        if (letter.invitation is not { } invite || !invite.CanAttend(now) || !_letterActivities.TryGetValue(invite.activity_key, out var attend)) return;
        _letters?.Close(); attend(letter);
    }
    private void capture_letter_closure(int fromHour, int untilHour, string reason)
    {
        if (player is null || !(player.is_in_breakthrough_retreat() || reason == "yuanxi")
            || _player_world_node_id != "yuxu_xianzong_core" || string.IsNullOrWhiteSpace(_player_current_yuxu_peak_id)) return;
        // 时间推进前记录地点，等世界月度结算完成后处理；不在闭关中弹窗或猜测历史月份。
        _letterMailbox.closure_pending = true; _letterMailbox.closure_from_hour = fromHour;
        _letterMailbox.closure_until_hour = untilHour; _letterMailbox.closure_node_id = _player_world_node_id;
        _letterMailbox.closure_subscene_id = _player_current_yuxu_peak_id; mark_save_dirty();
    }
    private void process_closed_visit_letters(HumanData human, GameTimeMgr time)
    {
        if (!_letterMailbox.closure_pending) return;
        _letterMailbox.closure_pending = false; mark_save_dirty();
        var month = get_game_month_index(time);
        if (human.sect_affiliation_id != YuxuSectId || _socialMembershipMonth < 0 || month <= _socialMembershipMonth
            || _npc_world is null || social_visit_count(month) >= social_visit_limit(human) || month != _letterMailbox.closure_until_hour / 720
            || _letterMailbox.closure_node_id != "yuxu_xianzong_core" || string.IsNullOrWhiteSpace(_letterMailbox.closure_subscene_id)) return;
        var npc = social_visit_candidate(human, month, _letterMailbox.closure_subscene_id);
        if (npc is null) return;
        var known = NpcSocialRules.KnowsName(npc);
        var source = $"npc_visit:{month}:{npc.npc_id}";
        var letter = new LetterData
        {
            source_event_id = source, sender_npc_id = npc.npc_id, sender_name = npc.npc_name, sender_was_known = known,
            subject = known ? "静室门前留书" : "留笺见礼", sent_at_hour = _letterMailbox.closure_until_hour,
            opens_acquaintance_window = true,
            delivery_place = get_yuxu_peak_name(_letterMailbox.closure_subscene_id) + " · 静室",
            body = known
                ? $"道友：\n\n今日前来问候，见静室仍闭，便未叩扰。留此短笺，愿道友清修顺遂。\n\n待你出关，若有闲暇，我们再叙。\n\n{npc.npc_name} 留书"
                : $"道友：\n\n我名{npc.npc_name}，与道友同在本宗修行。今日登门，见静室闭门，便留此笺，不扰清修。\n\n若出关后有意往来，日后宗内相逢，再当面见礼。\n\n{npc.npc_name} 敬上"
        };
        if (deliver_letter(letter) || _letterMailbox.letters.Any(existing => existing.source_event_id == source))
        {
            NpcSocialRules.HearSectReputation(npc, human, true);
            mark_social_visit_processed(npc, month);
        }
    }
}
