using Godot;

public partial class MainScene
{
    private JournalPanel.Entry? get_yuxu_registration_journal_task() =>
        player?.entity_data is HumanData { sect_yuxu_registration_pending: true, sect_affiliation_id: "" }
            ? IdentifyJournalTask(new JournalPanel.Entry("玉虚报到", "在观复峰办理入门。", "玉虚仙宗", "观复峰", "进行中", Id: "yuxu.registration"))
            : null;

    /// <summary>报到是观复峰场景中的行动吊牌；对话里的接引人不需要从人物列表寻找。</summary>
    private void request_yuxu_registration()
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        if (_yuxuAdmissionInProgress || story_dialogue is null || story_dialogue.Visible ||
            player?.entity_data is not HumanData human || !human.sect_yuxu_registration_pending ||
            !string.IsNullOrWhiteSpace(human.sect_affiliation_id) || _player_current_yuxu_peak_id != "guanfu" ||
            overview?.Visible != true || overview.GetNodeOrNull<Control>("PeakInterior")?.Visible != true) return;
        var rank = get_recorded_yuxu_admission_rank(human);
        if (string.IsNullOrEmpty(rank)) { show_message("暂不能报到", "山门尚未留下白石验灵名册，暂时无法办理报到。"); return; }
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        var guide = manager?.get_npc(human.sect_admission_guide_npc_id);
        if (!yuxu_npc_is_present(guide, "guanfu"))
            guide = manager is null ? null : choose_yuxu_present_npc(manager.get_active_npcs().Where(n =>
                n.formal_identity.Contains("弟子") && yuxu_npc_is_present(n, "guanfu")));
        if (guide is null) { show_message("暂不能报到", "接引处暂时无人值守，请稍后再来。"); return; }
        human.sect_admission_guide_npc_id = guide.npc_id;
        mark_save_dirty();
        _yuxuAdmissionInProgress = true;
        var speaker = guide.npc_name;
        var rankLine = rank switch
        {
            "预备真传" => "先按预备真传登记。一年内修到云息三层，学会首本秘籍的第二卷，还要通过天问峰演法。考核期间可在观复峰或紫府峰静修、休息。都完成就列真传，逾期则转内门。",
            "内门弟子" => "名册记好了，你列内门。观复峰有静室和歇脚的地方。",
            "外门弟子" => "你先列外门。观复峰有静室和歇脚的地方。",
            _ => "名册上先记杂役。观复峰有静室和歇脚的地方。"
        };
        var bookLine = string.IsNullOrWhiteSpace(human.sect_library_first_manual_path)
            ? "想看书去玄览峰，第一本可以免费拓印；选之前看看自己能不能读。"
            : rank == "预备真传"
                ? "已经领过的书可以接着读；考核期间可在观复峰或紫府峰静修。"
                : "已经领过的书可以接着读，想静修回观复峰。";
        show_dialogue_sequence(() =>
        {
            if (player?.entity_data != human || !human.sect_yuxu_registration_pending)
            { _yuxuAdmissionInProgress = false; return; }
            complete_yuxu_admission(rank, null, guide);
        },
            (DialogueBeatKind.NpcSpeech, speaker, "到了。这边办入门的名册，我先替你登记。"),
            (DialogueBeatKind.Narration, "", $"你依照名册登记了姓名与来历，玉虚门籍记为{rank}。"),
            (DialogueBeatKind.NpcSpeech, speaker, rankLine),
            (DialogueBeatKind.NpcSpeech, speaker, bookLine));
    }
}
