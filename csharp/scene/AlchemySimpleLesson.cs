using System;
using System.Linq;

/// <summary>第一炉用预置节点网讲解布料、通路与一次开炉自动结算。</summary>
public sealed class AlchemySimpleLesson
{
    public AlchemyPreviewModel Model { get; } = new();
    public bool WithAssistant { get; }
    public int Stage { get; private set; }
    public string LastAction { get; private set; } = "";
    public const string MainName = "溪纹藻丝", AssistantName = "凝露松脂";
    public string Title => WithAssistant ? "再试一炉 · 辅药稳形" : "第一炉 · 九品通脉丹";

    public AlchemySimpleLesson(bool assistant = false)
    {
        WithAssistant = assistant;
        Model.SelectPill("tongmai");
        Model.SelectFormula("stream_fiber");
        Model.SelectMainHerb(MainName);
        string placed = Model.PlaceHerb(MainName, .8);
        if (!Model.InFurnace.Any(d => d.Herb.Name == MainName)) throw new InvalidOperationException(placed);
        int mainId = Model.InFurnace.Single(d => d.Herb.Name == MainName).Id;
        string route = Model.SignLink(mainId, 0, "通");
        if (!Model.SignedLinks.Any(l => l.SourceDoseId == mainId && l.TargetDoseId == 0 && l.UtilityId == "通")) throw new InvalidOperationException(route);

        if (WithAssistant)
        {
            string support = Model.PlaceHerb(AssistantName, .3);
            if (!Model.InFurnace.Any(d => d.Herb.Name == AssistantName)) throw new InvalidOperationException(support);
            int helperId = Model.InFurnace.Single(d => d.Herb.Name == AssistantName).Id;
            string solid = Model.SignLink(helperId, 0, "固");
            if (!Model.SignedLinks.Any(l => l.SourceDoseId == helperId && l.TargetDoseId == 0 && l.UtilityId == "固")) throw new InvalidOperationException(solid);
        }
        Model.Explanation = "教程炉局已备好：溪纹藻丝的“通”线通向丹炉口。点击【开炉炼丹】，观察炼化、输送、凝丹和实际品质。";
    }

    public void Observe()
    {
        if (Model.Done) Stage = 8;
        else if (Model.AutoRunStarted) Stage = 1;
        else Stage = 0;
    }

    public string Instruction => Stage switch
    {
        0 => "药材与通路已放在节点网上。灵识决定同时工作的线路数；法力供自动火候。现在只需点一次【开炉炼丹】。",
        1 => "自动跑炉中：主药优先按适宜火候炼化，通路把可用药性送往丹炉口，最后自动凝丹并安全收炉。可观察线路状态和资源变化。",
        _ => $"本炉完成：{Model.Result}\n\n{string.Join("\n", Model.Pills.Select(p => $"{p.Score}分 · {AlchemyPreviewModel.Tier(p.Score)} · {p.QualityText}"))}\n\n质量来自实际药性、配伍、阴阳与丹体稳定；加辅药和调整功用线可以继续提高或改变结果。"
    };

    public string ActionLabel => Stage switch
    {
        0 => "开炉炼丹",
        1 => "自动运行中",
        _ => "查看本炉结果"
    };

    public void Step()
    {
        Observe();
        if (Stage == 0)
        {
            string error = Model.BeginAutomaticRun();
            if (error.Length > 0) throw new InvalidOperationException(error);
            LastAction = Model.Explanation;
            Observe();
            return;
        }
        if (Stage != 1) return;
        bool active = Model.AdvanceAutomaticRun();
        LastAction = Model.LastFeedback;
        if (!active && !Model.Done) throw new InvalidOperationException(Model.AutomaticStopReason);
        Observe();
    }
}
