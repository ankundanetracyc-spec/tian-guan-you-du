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
        "初醒青溪" => "phase.qingxi", "行旅与积累" => "phase.travel",
        "初醒" => "qingxi.awakening", "与李守拙道别" => "qingxi.farewell",
        "村中岁月" => "qingxi.village_life", "明日上山" => "qingxi.wait_for_morning",
        "后山初探" => "qingxi.back_hill", "夜深" => "qingxi.first_rest", "问道" => "qingxi.ask_for_room",
        "玄鉴初读" => "qingxi.first_volume", "定为主修" => "qingxi.equip_gongfa",
        "初入云息" => "qingxi.report_breakthrough", "静室引气" => "qingxi.cultivate",
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
        var lineage = get_active_yuxu_lineage_journal_task();
        var invitation = get_letter_invitation_tasks().FirstOrDefault(entry => entry.State != "已完成");
        var current = duty ?? lineage ?? invitation ?? IdentifyJournalTask(has_open_world_access() ? get_cultivation_guidance_task() : _active_quest ?? get_phase_goal());
        var ongoing = get_quest_entries().Where(e => e.State != "已完成").DistinctBy(e => e.Id).ToList();
        if (!_journalTrackingChosen || (!string.IsNullOrEmpty(_trackedJournalTask) && ongoing.All(e => e.Id != _trackedJournalTask)))
            _trackedJournalTask = current.Id;
        var tracked = ongoing.FirstOrDefault(e => e.Id == _trackedJournalTask);
        var visible = new List<JournalPanel.Entry>();
        if (tracked is not null) visible.Add(tracked);
        visible.AddRange(ongoing.Where(e => e.Id != tracked?.Id).OrderByDescending(e => e.AcceptedOrder).Take(2 - visible.Count));
        if (journal is not null) journal.TrackedTaskId = _trackedJournalTask;
        estate?.set_compact_task_tracker_visible(false);
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
        foreach (var place in new[] { "玉虚仙宗", "和光峰", "玄览峰", "紫府峰", "瑶圃峰", "东林地", "静冠洼地", "百川区域", "青溪里", "后山", "李宅", "偏院旧屋", "静室", "宗门事务处" })
            if (source.Contains(place, StringComparison.Ordinal)) candidates.Add(place);
        if (source.Contains("李守拙", StringComparison.Ordinal)) candidates.Add("李守拙");
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
        if (entry.Id.StartsWith("letter.invitation.", StringComparison.Ordinal))
            return $"前往{entry.Place}赴约";

        if (entry.Title == "与李守拙道别") return "回李宅向李守拙道别";
        if (entry.Title == "村中岁月") return "留在青溪里修行、采集并积蓄力量";
        if (entry.Title == "明日上山") return "休息至次日清晨，再去后山";
        if (entry.Title == "后山初探") return "前往后山完成外围探查";
        if (entry.Title == "夜深") return "休息至明日，再作打算";
        if (entry.Title == "问道") return "向李守拙问起偏院旧屋";
        if (entry.Title == "玄鉴初读") return "前往静室研读玄鉴第一卷";
        if (entry.Title == "定为主修") return "在角色页装配已研读功法";
        if (entry.Title == "初入云息") return "回李宅向李守拙报告突破";
        if (entry.Title == "静室引气") return "前往静室继续引气修行";
        if (entry.Title == "初醒") return "与李守拙交谈，确认身在何处";
        if (entry.Title == "行旅与积累") return "在百川区域修炼、历练，积累突破条件";
        if (entry.Title == "初醒青溪") return "弄清身在何处，踏入云息后离村";
        if (entry.Title == "精进修为")
            return entry.Body.Contains("云息六层", StringComparison.Ordinal)
                ? "继续修炼，达到云息六层"
                : "继续修炼与历练，提升境界";
        if (entry.Title == "凝核准备") return "查看突破条件，准备进入凝核";
        if (entry.Title == "稳固灵核") return "养护灵核，修至云息九层";
        if (entry.Title == "核成阵") return "准备核成阵，突破云息十层";
        if (entry.Title == "归元养息") return "修习归元九息，直至圆满";
        if (entry.Title == "准备铸玄基") return "云息十层圆满；后续修行尚未开放";
        if (entry.Title == "继续精进") return "继续修行，查看当前境界目标";

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

    private List<JournalPanel.Entry> get_yuxu_lineage_journal_tasks()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            !is_yuxu_true_disciple(human.sect_current_rank))
            return [];

        var tasks = new List<JournalPanel.Entry>();
        if (string.IsNullOrWhiteSpace(human.sect_teacher_npc_id) || string.IsNullOrWhiteSpace(human.sect_guide_npc_id))
            return tasks;

        var guide = (_npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager"))?.get_npc(human.sect_guide_npc_id);
        var guideName = string.IsNullOrWhiteSpace(guide?.npc_name) ? "带教师兄师姐" : guide.npc_name;
        var bookPath = human.sect_lineage_gongfa_path;
        var book = string.IsNullOrWhiteSpace(bookPath) ? null : ResourceLoader.Load<GongfaResource>(bookPath);
        var bookName = string.IsNullOrWhiteSpace(book?.gongfa_name) ? "师门首卷" : book.gongfa_name;

        if (string.IsNullOrWhiteSpace(bookPath))
        {
            tasks.Add(IdentifyJournalTask(new JournalPanel.Entry(
                "同门引路",
                $"赴玄览峰与{guideName}相见，择取一部符合当前条件的云息秘籍为师门首卷；择定后，返紫府峰静室研读。",
                "玉虚仙宗·真传带教", "玄览峰", "进行中", Id: "yuxu.lineage.choose_first_volume")));
            return tasks;
        }

        tasks.Add(IdentifyJournalTask(new JournalPanel.Entry(
            "同门引路",
            $"已与{guideName}在玄览峰择定《{bookName}》为师门首卷。",
            "玉虚仙宗·真传带教", "玄览峰", "已完成", Id: "yuxu.lineage.choose_first_volume")));

        var firstChapter = book?.get_sorted_chapters().FirstOrDefault();
        var firstVolumeComplete = firstChapter is not null && player is not null &&
            player.is_gongfa_chapter_learned(book!, firstChapter.get_id());
        if (!firstVolumeComplete)
        {
            tasks.Add(IdentifyJournalTask(new JournalPanel.Entry(
                "静室研读首卷",
                $"于紫府峰静室研读《{bookName}》首卷。研读门槛、所需时日与精力消耗均依此卷现行规制；读毕后，再向{guideName}陈报所得。",
                "玉虚仙宗·真传带教", "紫府峰·静室", "进行中", Id: "yuxu.lineage.study_first_volume")));
            return tasks;
        }

        tasks.Add(IdentifyJournalTask(new JournalPanel.Entry(
            "静室研读首卷",
            $"《{bookName}》首卷已依原有章节规则研读完成。",
            "玉虚仙宗·真传带教", "紫府峰·静室", "已完成", Id: "yuxu.lineage.study_first_volume")));

        var reportBody = $"返紫府峰向{guideName}复命，陈述《{bookName}》首卷研读所得。是否完成，以该卷首章的既有完成记录为准。";
        tasks.Add(IdentifyJournalTask(new JournalPanel.Entry(
            "首卷复命",
            human.sect_lineage_reported_first_volume
                ? $"已向{guideName}复命，呈报《{bookName}》首卷研读所得。"
                : reportBody,
            "玉虚仙宗·真传带教", "紫府峰", human.sect_lineage_reported_first_volume ? "已完成" : "进行中",
            Id: "yuxu.lineage.report_first_volume")));
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
