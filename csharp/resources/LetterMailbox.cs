using Godot;
using Godot.Collections;

/// <summary>独立旅程的信匣。投递事件防重；状态改变随正式旅程保存。</summary>
[GlobalClass]
public partial class LetterMailbox : Resource
{
    [Export] public Array<LetterData> letters = [];
    [Export] public bool closure_pending;
    [Export] public int closure_from_hour;
    [Export] public int closure_until_hour;
    [Export] public string closure_node_id = "";
    [Export] public string closure_subscene_id = "";
    public int UnreadCount => letters.Count(letter => letter is not null && !letter.is_read);
    public LetterData? Find(string id) => letters.FirstOrDefault(letter => letter is not null && letter.letter_id == id);

    public bool Deliver(LetterData letter)
    {
        if (letter is null || string.IsNullOrWhiteSpace(letter.source_event_id) || string.IsNullOrWhiteSpace(letter.subject)
            || string.IsNullOrWhiteSpace(letter.body) || letter.sent_at_hour < 0
            || letter.invitation is { } invite && !invite.IsValid(letter.sent_at_hour)
            || letters.Any(existing => existing is not null && existing.source_event_id == letter.source_event_id)) return false;
        if (letter.Duplicate(true) is not LetterData copy) return false;
        copy.letter_id = Guid.NewGuid().ToString("N"); copy.is_read = copy.is_starred = false;
        if (copy.invitation is not null) copy.invitation.response = "pending";
        letters.Add(copy); EmitChanged(); return true;
    }
    public void SetRead(string id, bool read)
    {
        if (Find(id) is not { } letter || letter.is_read == read) return;
        letter.is_read = read; EmitChanged();
    }
    public void SetStarred(string id, bool starred)
    {
        if (Find(id) is not { } letter || letter.is_starred == starred) return;
        letter.is_starred = starred; EmitChanged();
    }
    public bool Reply(string id, bool accept, int now)
    {
        if (Find(id)?.invitation is not { } invite || !invite.CanReply(now)) return false;
        invite.response = accept ? "accepted" : "declined"; EmitChanged(); return true;
    }
    public bool CompleteInvitation(string id)
    {
        if (Find(id)?.invitation is not { response: "accepted" } invite) return false;
        invite.response = "completed"; EmitChanged(); return true;
    }
    public bool FailInvitation(string id)
    {
        if (Find(id)?.invitation is not { response: "accepted" } invite) return false;
        invite.response = "failed"; EmitChanged(); return true;
    }
    public static string Date(int hour)
    {
        hour = Math.Max(0, hour); var day = hour / 24;
        return $"昭明 {GameTimeMgr.ZhaomingStartYear + day / 360}年{day / 30 % 12 + 1}月{day % 30 + 1}日";
    }
}
