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
        var current = duty ?? lineage ?? IdentifyJournalTask(_qingxi_prologue == QingxiPrologueStep.Departed ? get_cultivation_guidance_task() : _active_quest ?? get_phase_goal());
        var ongoing = get_quest_entries().Where(e => e.State != "已完成").DistinctBy(e => e.Id).ToList();
        if (!_journalTrackingChosen || (!string.IsNullOrEmpty(_trackedJournalTask) && ongoing.All(e => e.Id != _trackedJournalTask)))
            _trackedJournalTask = current.Id;
        var tracked = ongoing.FirstOrDefault(e => e.Id == _trackedJournalTask);
        var visible = new List<JournalPanel.Entry>();
        if (tracked is not null) visible.Add(tracked);
        visible.AddRange(ongoing.Where(e => e.Id != tracked?.Id).OrderByDescending(e => e.AcceptedOrder).Take(2 - visible.Count));
        if (journal is not null) journal.TrackedTaskId = _trackedJournalTask;
        estate?.set_compact_task_tracker_visible(false);
        hud?.set_task_entries(visible, true);
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
                $"于紫府峰静室研读《{bookName}》首卷。研读门槛、所需时日与体力消耗均依此卷现行规制；读毕后，再向{guideName}陈报所得。",
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
