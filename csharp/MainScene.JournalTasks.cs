using Godot;

public partial class MainScene
{
    private string _trackedJournalTask = "";
    private bool _journalTrackingChosen;
    private long _journalTaskSequence;
    private readonly Dictionary<string, long> _journalTaskOrders = new();

    // 固定标识与显示文案分离；旧存档只在迁移入口按已知旧名称匹配。
    private static string JournalTaskId(JournalPanel.Entry entry) => entry.Title switch
    {
        "行旅与积累" => "phase.travel",
        "师兄师姐带你入门" => "yuxu.lineage.choose_first_volume",
        "研读师门首卷" => "yuxu.lineage.study_first_volume",
        "向带教师兄师姐汇报" => "yuxu.lineage.report_first_volume",
        "同门引路" => "yuxu.lineage.choose_first_volume",
        "静室研读首卷" => "yuxu.lineage.study_first_volume",
        "首卷复命" => "yuxu.lineage.report_first_volume",
        "精进修为" => "cultivation.progress", "凝核准备" => "cultivation.condensation",
        "稳固灵核" => "cultivation.stabilize", "核成阵" => "cultivation.formation",
        "归元养息" => "cultivation.yuanxi", "准备铸玄基" => "cultivation.xuanji_prep", "继续精进" => "cultivation.continue",
        _ => ""
    };
    private JournalPanel.Entry IdentifyJournalTask(JournalPanel.Entry entry)
    {
        var id = string.IsNullOrEmpty(entry.Id) ? JournalTaskId(entry) : entry.Id;
        if (string.IsNullOrEmpty(id)) id = "legacy." + Guid.NewGuid().ToString("N");
        if (!_journalTaskOrders.TryGetValue(id, out var order))
        { order = ++_journalTaskSequence; _journalTaskOrders[id] = order; }
        return entry with { Id = id, AcceptedOrder = order };
    }
    private void RefreshJournalTracker()
    {
        var phase = IdentifyJournalTask(get_phase_goal());
        var duty = get_active_yuxu_journal_task();
        var lecture = get_active_yuxu_true_disciple_lecture_journal_task();
        var lineage = get_active_yuxu_lineage_journal_task();
        var exam = get_yuxu_provisional_exam_tasks().FirstOrDefault(entry => entry.State != "已完成");
        var invitation = get_letter_invitation_tasks().FirstOrDefault(entry => entry.State != "已完成");
        var current = (_baishiLecture is { finished: false } ? IdentifyJournalTask(baishi_current_task()) : null) ?? get_yuxu_registration_journal_task() ?? duty ?? lecture ?? lineage ?? exam ?? invitation ?? IdentifyJournalTask(has_open_world_access() ? get_cultivation_guidance_task() : get_phase_goal());
        var ongoing = get_quest_entries().Where(e => e.State != "已完成").DistinctBy(e => e.Id).ToList();
        if (!_journalTrackingChosen || (!string.IsNullOrEmpty(_trackedJournalTask) && ongoing.All(e => e.Id != _trackedJournalTask)))
            _trackedJournalTask = current.Id;
        var tracked = ongoing.FirstOrDefault(e => e.Id == _trackedJournalTask);
        var visible = new List<JournalPanel.Entry>();
        if (tracked is not null) visible.Add(tracked);
        visible.AddRange(ongoing.Where(e => e.Id != tracked?.Id &&
            (tracked is null || GetJournalTaskTrackerObjective(e) != GetJournalTaskTrackerObjective(tracked)))
            .OrderByDescending(e => e.AcceptedOrder).Take(2 - visible.Count));
        if (journal is not null) journal.TrackedTaskId = _trackedJournalTask;
        var people = _npc_world?.npcs
            .Where(npc => npc is not null && !string.IsNullOrWhiteSpace(npc.npc_name))
            .Select(npc => npc.npc_name.Trim()).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        hud?.set_task_entries(visible.Select(entry =>
        {
            var emphasis = GetJournalTaskTrackerEmphasis(entry, people);
            return entry with
            {
                Title = UnderlineJournalTaskEntities(entry.Title, emphasis),
                Body = UnderlineJournalTaskEntities(GetJournalTaskTrackerObjective(entry), emphasis)
            };
        }), true);
    }

    private static string[] GetJournalTaskTrackerEmphasis(JournalPanel.Entry entry, IEnumerable<string> people)
    {
        var source = $"{entry.Title}\n{entry.Place}\n{entry.Body}";
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var place in new[] { "玉虚仙宗", "和光峰", "观复峰", "玄览峰", "紫府峰", "天问峰", "瑶圃峰", "东林地", "静冠洼地", "百川区域", "青溪里", "静室", "宗门事务处" })
            if (source.Contains(place, StringComparison.Ordinal)) candidates.Add(place);
        foreach (var place in entry.Place.Split(['→', '·', '／', '/', '、', '，', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (place.Length > 1 && source.Contains(place, StringComparison.Ordinal)) candidates.Add(place);
        foreach (var person in people)
            if (person.Length > 1 && source.Contains(person, StringComparison.Ordinal)) candidates.Add(person);

        var start = source.IndexOf('《');
        while (start >= 0)
        {
            var end = source.IndexOf('》', start + 1);
            if (end <= start + 1) break;
            candidates.Add(source[(start + 1)..end]);
            start = source.IndexOf('《', end + 1);
        }
        foreach (var item in new[] { "苦苔根", "兽骨", "雾铃花", "灵石" })
            if (source.Contains(item, StringComparison.Ordinal)) candidates.Add(item);

        return candidates.OrderByDescending(token => token.Length).ToArray();
    }

    private static string UnderlineJournalTaskEntities(string text, IReadOnlyList<string> tokens)
    {
        if (string.IsNullOrEmpty(text) || tokens.Count == 0) return EscapeJournalTaskBbCode(text);
        var output = new System.Text.StringBuilder(text.Length + 24);
        for (var index = 0; index < text.Length;)
        {
            var match = tokens.FirstOrDefault(token => token.Length > 0 && index + token.Length <= text.Length &&
                string.Compare(text, index, token, 0, token.Length, StringComparison.Ordinal) == 0);
            if (match is null)
            {
                output.Append(EscapeJournalTaskBbCode(text[index].ToString()));
                index++;
                continue;
            }
            output.Append("[u]").Append(EscapeJournalTaskBbCode(match)).Append("[/u]");
            index += match.Length;
        }
        return output.ToString();
    }

    private static string EscapeJournalTaskBbCode(string text) => text.Replace("[", "[[", StringComparison.Ordinal).Replace("]", "]]", StringComparison.Ordinal);

    private static string GetJournalTaskTrackerObjective(JournalPanel.Entry entry)
    {
        if (entry.Id.StartsWith("yuxu.true_disciple.lecture.", StringComparison.Ordinal))
            return "前往观复峰开设本期真传讲席";
        if (entry.Id.StartsWith("letter.invitation.", StringComparison.Ordinal))
            return $"前往{entry.Place}赴约";

        if (entry.Title == "行旅与积累") return "在百川区域修炼、历练，积累突破条件";
        if (entry.Title == "精进修为")
            return entry.Body.Contains("云息六层", StringComparison.Ordinal)
                ? "继续修炼，达到云息六层"
                : "继续修炼与历练，提升境界";
        if (entry.Title == "凝核准备") return "查看突破条件，准备进入凝核";
        if (entry.Title == "稳固灵核") return "养护灵核，修至云息九层";
        if (entry.Title == "核成阵") return "准备核成阵，突破云息十层";
        if (entry.Title == "归元养息") return "修习归元九息，直至圆满";
        if (entry.Title == "准备铸玄基") return "云息十层圆满；后续修行尚未开放";
        if (entry.Title == "继续精进") return "提升修为";

        if (entry.Title == "苦苔根交付")
        {
            var progress = entry.Body.Split('。')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            var counts = progress?.Split('/');
            if (counts is { Length: 2 } && int.TryParse(counts[0], out var current) && int.TryParse(counts[1], out var required) && current >= required)
                return "回和光峰事务处交付苦苔根";
            return counts is { Length: 2 } ? $"采集苦苔根 {counts[0]}/{counts[1]}，回和光峰交付" : "采集2份苦苔根，回和光峰交付";
        }
        if (entry.Title == "东林地巡查")
            return entry.Body.Contains("复核完成", StringComparison.Ordinal)
                ? "回和光峰事务处报告巡查结果"
                : "沿东林地路线前行，复核静冠洼地";
        if (entry.Title == "上缴兽骨") return "带1份兽骨回和光峰事务处交付";
        if (entry.Title == "瑶圃药园值守") return "前往瑶圃峰完成药园值守";
        if (entry.Title == "培育雾铃花")
            return entry.Body.Contains("苗株已照料", StringComparison.Ordinal)
                ? "等待生长，回瑶圃峰采收雾铃花"
                : "前往瑶圃峰照料雾铃花";
        if (entry.Title == "静室研读一卷") return "在所属静室研读一卷秘籍";
        if (entry.Title == "同门引路")
        {
            var guide = ExtractJournalTaskName(entry.Body, "与", "相见");
            return string.IsNullOrWhiteSpace(guide) ? "前往玄览峰，拜见带教师兄师姐" : $"前往玄览峰，拜见{guide}";
        }
        if (entry.Title == "静室研读首卷") return "前往紫府峰静室研读师门首卷";
        if (entry.Title == "首卷复命")
        {
            var guide = ExtractJournalTaskName(entry.Body, "向", "复命");
            return string.IsNullOrWhiteSpace(guide) ? "前往紫府峰，向带教师兄师姐复命" : $"前往紫府峰，向{guide}复命";
        }

        var summary = entry.Body.Replace('\n', ' ').Trim();
        var punctuation = summary.IndexOfAny(['。', '；', ';', '！', '？']);
        if (punctuation > 0) summary = summary[..punctuation].Trim();
        if (summary.Length == 0) return string.IsNullOrWhiteSpace(entry.Place) ? "查看任务详情" : $"前往{entry.Place}";
        const int maxSummaryLength = 32;
        return summary.Length <= maxSummaryLength ? summary : summary[..(maxSummaryLength - 1)].TrimEnd() + "…";
    }

    private static string ExtractJournalTaskName(string text, string prefix, string suffix)
    {
        var start = text.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) return "";
        start += prefix.Length;
        var end = text.IndexOf(suffix, start, StringComparison.Ordinal);
        return end <= start ? "" : text[start..end].Trim();
    }

    private JournalPanel.Entry? get_active_yuxu_lineage_journal_task()
        => get_yuxu_lineage_journal_tasks().FirstOrDefault(entry => entry.State != "已完成");

    private JournalPanel.Entry? get_active_yuxu_true_disciple_lecture_journal_task()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            !is_yuxu_true_disciple(human.sect_current_rank))
            return null;
        var dueHour = human.sect_true_disciple_lecture_next_due_hour;
        var now = (long)(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
        if (dueHour < 0 || now < dueHour || now >= dueHour + YuxuTrueDiscipleLecturePeriodHours)
            return null;
        var missedText = human.sect_true_disciple_lecture_missed_count > 0
            ? $"累计缺席记档：{human.sect_true_disciple_lecture_missed_count} 期。"
            : "尚无缺席记档。";
        return IdentifyJournalTask(new JournalPanel.Entry(
            "宗内事务·真传讲席",
            $"本期讲席已开放。前往观复峰开设答疑讲席，面向外门与内门弟子作指点；本项只登记履职到场，不设问答考核。下一期开始前未完成，将记入真传师门履职声誉。{missedText}",
            "玉虚仙宗·宗内事务", "观复峰", "进行中", Id: $"yuxu.true_disciple.lecture.{dueHour}"));
    }

    private List<JournalPanel.Entry> get_yuxu_lineage_journal_tasks()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId)
            return [];

        var tasks = new List<JournalPanel.Entry>();
        var firstCopy = human.sect_library_first_manual_path;
        var hasFirstCopy = !string.IsNullOrWhiteSpace(firstCopy);
        var copiedBook = hasFirstCopy ? ResourceLoader.Load<GongfaResource>(firstCopy) : null;
        tasks.Add(IdentifyJournalTask(new JournalPanel.Entry(
            "玄览藏书", !hasFirstCopy ? "在玄览峰拓印一本秘籍。" : copiedBook is null ? "首本秘籍已领取。" : $"已领取《{copiedBook.gongfa_name}》的拓印本。",
            "玉虚仙宗", "玄览峰", hasFirstCopy ? "已完成" : "进行中", Id: "yuxu.library.first_copy")));
        return tasks;
    }
    private void OnJournalTrackingRequested(string? id)
    {
        _journalTrackingChosen = true; _trackedJournalTask = id ?? "";
        RefreshJournalTracker(); refresh_journal_entries(); mark_save_dirty();
    }
    private Godot.Collections.Dictionary PackJournalTracking()
    {
        var orders = new Godot.Collections.Dictionary();
        foreach (var pair in _journalTaskOrders) orders[pair.Key] = pair.Value;
        return new Godot.Collections.Dictionary { ["selected"] = _trackedJournalTask, ["chosen"] = _journalTrackingChosen, ["sequence"] = _journalTaskSequence, ["orders"] = orders };
    }
    private void RestoreJournalTracking(Godot.Collections.Dictionary state)
    {
        _journalTaskOrders.Clear(); _trackedJournalTask = ""; _journalTrackingChosen = false; _journalTaskSequence = 0;
        if (!state.TryGetValue("journal_tracking", out var value) || value.VariantType != Variant.Type.Dictionary) return;
        var data = value.AsGodotDictionary();
        _trackedJournalTask = data.GetValueOrDefault("selected", "").AsString();
        _journalTrackingChosen = data.GetValueOrDefault("chosen", false).AsBool();
        _journalTaskSequence = data.GetValueOrDefault("sequence", 0L).AsInt64();
        if (data.TryGetValue("orders", out var orders) && orders.VariantType == Variant.Type.Dictionary)
            foreach (var pair in orders.AsGodotDictionary())
            { _journalTaskOrders[pair.Key.AsString()] = pair.Value.AsInt64(); _journalTaskSequence = Math.Max(_journalTaskSequence, pair.Value.AsInt64()); }
    }
}
