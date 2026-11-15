using Godot;

/// <summary>Visual-only preview of the banquet window scene and its ambient/direct dialogue hierarchy.</summary>
public partial class BanquetVisualPreview : Node
{
    public override async void _Ready()
    {
        var panel = new BanquetPanel { Name = "BanquetPanel" };
        AddChild(panel);
        panel.StartActivity(new BanquetActivityPresentation
        {
            EventId = "banquet-visual-preview",
            Title = "同门雅集",
            HostName = "沈照微",
            VenueName = "同门雅集堂",
            Participants = new[]
            {
                new BanquetParticipantPresentation { Id = "player", Name = "沈照微", Subtitle = "真传弟子 · 云息十层" },
                new BanquetParticipantPresentation { Id = "yin-zhen", Name = "尹蓁", Subtitle = "内门弟子 · 云息八层" },
                new BanquetParticipantPresentation { Id = "lu-wan", Name = "陆晚", Subtitle = "外门弟子 · 云息七层" },
            },
        });
        panel.ShowBeat(new BanquetBeatPresentation
        {
            EventId = "banquet-visual-preview", BeatId = "banquet-visual-preview:0",
            SpeakerName = "尹蓁", Text = "你方才说这段旧注读了三遍？",
        });
        panel.ShowBeat(new BanquetBeatPresentation
        {
            EventId = "banquet-visual-preview", BeatId = "banquet-visual-preview:1",
            SpeakerName = "陆晚", Text = "前两遍只顾记字句，第三遍才看明白前后原是一层意思。",
        });
        panel.ShowBeat(new BanquetBeatPresentation
        {
            EventId = "banquet-visual-preview", BeatId = "banquet-visual-preview:2",
            SpeakerName = "陆晚", Text = "沈照微道友，读法门时若遇到一处始终想不明白，你会先自己推敲，还是找人请益？",
            Choices = new[]
            {
                new BanquetChoicePresentation { Id = "ask", Tone = "请益", Text = "找一位走过相似阶段的同门，把各自体会对照一遍。" },
                new BanquetChoicePresentation { Id = "study", Tone = "细察", Text = "把前后几日逐处梳理，看看变化从哪一步开始。", RequirementText = "需悟性≥65（当前48）", IsAvailable = false },
                new BanquetChoicePresentation { Id = "honest", Tone = "坦率", Text = "我也未必一开始就想得明白，先记下疑处再慢慢推敲。" },
                new BanquetChoicePresentation { Id = "ask_all", Tone = "照顾同席", Text = "不妨请席间每位同门各说一件最有用的经验。", RequirementText = "需至少3位同门在席（当前2位）", IsAvailable = false },
            },
            ResponseSeconds = 12,
        });

        await ToSignal(GetTree().CreateTimer(0.65), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var screenshot = GetViewport().GetTexture().GetImage();
        var path = "res://tmp/banquet_window_bubble_preview_20261016.png";
        screenshot.SavePng(path);
        GD.Print($"BANQUET_PREVIEW_SAVED {path}");
    }
}
