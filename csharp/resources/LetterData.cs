using Godot;

/// <summary>收到时保存署名与内容快照；读信不向人物关系写入相识。</summary>
[GlobalClass]
public partial class LetterData : Resource
{
    [Export] public string letter_id = "";
    [Export] public string source_event_id = "";
    [Export] public string sender_npc_id = "";
    [Export] public string sender_name = "";
    [Export] public bool sender_was_known;
    [Export] public string category = "visit";
    [Export] public string subject = "";
    [Export] public string body = "";
    [Export] public string delivery_place = "";
    [Export] public int sent_at_hour;
    [Export] public bool is_read;
    [Export] public bool is_starred;
    [Export] public LetterInvitationData? invitation;

    public string SenderLabel => string.IsNullOrWhiteSpace(sender_name) ? "未署名" : sender_name;
    public string KindLabel => invitation is not null ? "邀约" : category switch { "notice" => "通知", "receipt" => "回执", _ => "留书" };
    public string ConnectionLabel => string.IsNullOrWhiteSpace(sender_npc_id) ? "事务信函" : sender_was_known ? "相识来信" : "初次来信";
    public string Preview => string.Join(" ", body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
}
