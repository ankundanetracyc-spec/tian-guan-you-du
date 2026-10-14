using Godot;

/// <summary>信件美术预览：独立示例信匣，不创建、读取或保存正式旅程。</summary>
public partial class LettersPreviewScene : Control
{
    public override async void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        const int now = (2 * 30 + 11) * 24 + 10;
        var mailbox = new LetterMailbox();
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_banquet", sender_npc_id = "preview_shen", sender_name = "沈听澜", sender_was_known = true,
            subject = "出关后，可来小聚", sent_at_hour = now - 48, delivery_place = "和光峰 · 静室",
            body = "道友：\n\n今日来访，见你仍在闭关，便把邀约留在门前。\n\n我拟在和光峰设一席清茶，请几位同门坐坐。来者有旧识，也有初来峰中的道友，正好相互见礼，聊聊近况。\n\n若你在约期前出关，且有闲暇，盼能同席。倘仍在静修，也不必为此分心。\n\n沈听澜 敬上",
            invitation = new LetterInvitationData { activity_key = "banquet", activity_name = "同门茶叙", location_id = "yuxu_xianzong_core",
                subscene_id = "heguang_peak", place_name = "玉虚仙宗 · 和光峰", reply_by_hour = now + 72, starts_at_hour = now + 96, ends_at_hour = now + 102 }
        });
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_expedition", sender_npc_id = "preview_chen", sender_name = "陈砚", sender_was_known = false,
            subject = "东林同行之约", sent_at_hour = now - 200, delivery_place = "静室门前",
            body = "道友：\n\n我名陈砚，近日欲约几位同门一同外出，故来问询。见道友闭门静修，便留下此信。\n\n若能在所约时日前出关，可再商议同行之事；若已过约期，便留待下回相逢。\n\n陈砚 敬上",
            invitation = new LetterInvitationData { activity_key = "party_dungeon", activity_name = "结伴外出", location_id = "east_woods",
                place_name = "东林地 · 林缘", reply_by_hour = now - 40, starts_at_hour = now - 16, ends_at_hour = now - 10 }
        });
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_notice", sender_name = "宗门执事", category = "notice", subject = "事务回执已收存",
            sent_at_hour = now - 24, body = "此信用于展示事务通知与回执的收存位置。\n\n实际信件将由对应事务投递，正文以该事务的真实结算为准。",
            delivery_place = "玉虚仙宗"
        });
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_greeting", sender_npc_id = "preview_gu", sender_name = "顾远", sender_was_known = true,
            subject = "静室门前留书", sent_at_hour = now - 72,
            body = "道友：\n\n今日前来问候，见静室仍闭，便未叩扰。留此短笺，愿道友清修顺遂。\n\n待你出关，若有闲暇，我们再叙。\n\n顾远 留书"
        });
        var panel = new LetterPanel { Name = "LetterPanel", PreviewHour = now }; AddChild(panel); panel.Bind(mailbox);
        panel.Closed += () => GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn");
        panel.Open(mailbox.letters[0].letter_id);
        var note = WorldHudStyle.Text("界面预览 · 示例信件，不写存档", 16, true); note.Position = new Vector2(420, 30); AddChild(note); note.ZIndex = 251;
        if (!OS.GetCmdlineUserArgs().Contains("--capture-letters")) return;
        await Capture("实际界面_邀约.png");
        panel.SelectLetter(mailbox.letters[1].letter_id); await Capture("实际界面_过期来信.png");
        panel.Bind(new LetterMailbox()); panel.Open(); await Capture("实际界面_空信匣.png");
        GetTree().Quit();
    }
    private async System.Threading.Tasks.Task Capture(string file)
    {
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://docs/美术规范/信件界面_20261014/" + file));
    }
}
