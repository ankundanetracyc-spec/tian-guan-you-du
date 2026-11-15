using Godot;

public partial class HumanData
{
    /// <summary>山门迎真已完成、观复峰入门报到尚未办理；不等同于宗门身份。</summary>
    [Export] public bool sect_yuxu_registration_pending;
    /// <summary>白石验灵后留存的玉虚起始身份；山门与报到沿用，不重新验灵。</summary>
    [Export] public string sect_yuxu_admission_rank = "";
    [Export] public bool sect_yuxu_admission_rank_checked;

    /// <summary>预备考核起算时辰；-1 表示旧档尚未启用本考核。</summary>
    [Export] public long sect_yuxu_exam_start_hour = -1;
    /// <summary>修为、首本第二卷与指定演法的实际完成时辰；-1 表示尚未完成。</summary>
    [Export] public long sect_yuxu_exam_realm_hour = -1;
    [Export] public long sect_yuxu_exam_book_hour = -1;
    [Export] public long sect_yuxu_exam_trial_hour = -1;
    /// <summary>0 未结算，1 通过，2 超时未完成。</summary>
    [Export] public int sect_yuxu_exam_result;
    [Export] public long sect_yuxu_exam_resolved_hour = -1;
    [Export] public string sect_yuxu_exam_pending_attempt_id = "";
}
