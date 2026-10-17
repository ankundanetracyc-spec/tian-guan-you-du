using System;
using System.Collections.Generic;

/// <summary>Two-line everyday exchanges overheard at a banquet; ambient dialogue carries no relationship evidence.</summary>
public sealed record BanquetAmbientExchange(string Id, string Opening, string Reply);

/// <summary>A player response; existing character attributes may unlock it but are never changed by choosing it.</summary>
public sealed record BanquetDialogueReply(
    string Id,
    string Tone,
    string Text,
    string RequiredAttribute = "",
    float MinimumAttribute = 0,
    int MinimumAttendees = 0);

/// <summary>A direct question addressed to the player, with ordinary and attribute-gated answers.</summary>
public sealed record BanquetPlayerPrompt(
    string Id,
    string Text,
    IReadOnlyList<BanquetDialogueReply> Replies,
    bool RequiresPublicRank = false);

public static class BanquetDialogueLibrary
{
    public static readonly IReadOnlyList<BanquetAmbientExchange> AmbientExchanges = new BanquetAmbientExchange[]
    {
        new("old_annotation", "你方才说这段旧注读了三遍？", "前两遍只顾记字句，第三遍才看明白前后原是一层意思。"),
        new("route_notes", "你每次出门都会把路记下来吗？", "以前不记，回来才发现把去时和归时的岔路弄混了。"),
        new("task_board", "事务板旁那张旧签还在吗？", "还在，边角都磨软了。大概有不少人停下来仔细看过。"),
        new("daily_schedule", "我总把日课排得太满，临时来事就乱了。", "给自己留一点空处吧，事情忽然多起来时就不必全盘推倒。"),
        new("careful_listening", "你方才听得很认真，是对这件事有兴趣？", "是。你说到关键处停了一下，我想等你把后半句说完。"),
        new("sparring_pace", "切磋时收招太急，回来才觉得手腕发紧。", "下次先把分寸说清，彼此都留意些，便不容易伤着。"),
        new("mountain_path", "你说那条山路不好走，是石阶多还是岔路多？", "岔路多些。认准沿途标记再走，回来就不容易绕远。"),
        new("tea_strength", "这盏茶是不是比方才泡得浓？", "我也觉得。聊得投入，倒没留心什么时候添了茶。"),
        new("first_impression", "你刚来门中时，最先记住的是什么？", "是从山门到住处那段路。走错两次以后，倒再也没忘。"),
        new("quiet_evening", "忙完一天以后，你喜欢找人说说话吗？", "看时候。偶尔听别人聊些寻常事，心里也能松快些。"),
        new("study_habit", "你读功法时会一口气读完，还是边读边记？", "先通读一遍，再把不明白的地方记下来回头细看。"),
        new("helpful_question", "问人之前，你会先自己琢磨很久吗？", "会先想一会儿，不过也提醒自己别把简单的事越想越复杂。"),
        new("peak_direction", "我到现在还会在几座峰之间走错方向。", "不只你一个。认路急不得，多走几次自然就熟了。"),
        new("forgotten_errand", "我刚才忽然想不起出门时要办哪件事了。", "先别急，看看随手记下的单子；要是没有，就回去问清再走。"),
        new("shared_experience", "听别人讲自己的经历，比只听结论有意思。", "也更容易记住。遇到相似的事时，还能想起当时的细节。"),
        new("small_mistake", "有回我把两件差事的时限看反了，白跑一趟。", "这种事谁都可能遇上。后来你是不是也养成了再核一遍的习惯？"),
        new("book_return", "那卷书看完以后，你会立刻归回原处吗？", "尽量会。放下时记清位置，下一回想找也方便。"),
        new("quiet_company", "你不说话的时候，我还以为自己讲得太多了。", "没有，只是听得认真。一时没接话，不一定是不想听。"),
        new("new_acquaintance", "刚认识一位同门时，你通常先聊些什么？", "从眼下正在做的事聊起吧，不必一开口就问得太深。"),
        new("time_for_rest", "差事都办完了，你还会给自己留点歇息时辰吗？", "会留一点。缓一缓再做下一件，心里也更有数。"),
        new("different_methods", "同一件事，你觉得该听前辈的还是自己试一遍？", "先弄清缘由，再看哪种做法适合眼前的情形。"),
        new("good_question", "你上回问的那个问题，后来想出答案了吗？", "想出一半。另一半还得找个懂行的人请教，不能装作全明白。"),
        new("travel_preparation", "我出门前总担心漏带东西，收拾起来没完。", "把常用的几样固定下来，临走前照着看一遍，就不必反复惦记。"),
        new("returning_home", "走了一趟回来，才发现熟悉的地方也有不少细节。", "是啊。赶路时只想着到哪儿，慢下来才看见沿途的东西。"),
        new("honest_uncertainty", "你碰到没把握的事，会先应下来再说吗？", "得先看自己能不能做到。答应得太快，最后反而让人失望。"),
        new("conversation_pause", "刚才那句话我还没想好怎么说，话题就过去了。", "不妨下回再提起来。想清楚再说，总比勉强接话稳妥。"),
        new("remembering_faces", "你记同门的名字很快吗？", "有时记得住，有时得多见两回。能记住对方说过的事倒更要紧。"),
        new("practice_log", "你会把每天修习后的感受记下来吗？", "偶尔记几句。隔些时候回看，才知道哪些只是当时的错觉。"),
        new("asking_for_help", "请人帮忙时，你会不会怕给对方添麻烦？", "会先把要问的事说清。对方若正忙，再约别的时候也不迟。"),
        new("small_compliment", "你方才把那件事讲得很明白。", "是诸位听得耐心，我才慢慢理顺了。"),
        new("gentle_teasing", "你是不是又把话说得太周全，反而没人敢接了？", "有这个可能。那我少想两句，听听诸位怎么说。"),
        new("keeping_promises", "你答应的事一般都会记在心里吗？", "会尽量记下来。光靠记性，忙起来就容易疏忽。"),
        new("learning_from_others", "你觉得同门之间最难得的是什么？", "能坦然说自己还不懂，也肯把自己懂的讲清楚。"),
        new("misread_tone", "你刚才是不是在笑我把路走反了？", "没有取笑的意思，只是想起我也走反过，实在太熟悉了。"),
        new("asking_first", "你遇到陌生规矩，会先照着做还是问清楚？", "若不急就先问清。看着相似的规矩，细处未必一样。"),
        new("sharing_notes", "你的笔记写得这么细，愿意借我看看吗？", "可以，不过有几处只记了自己的疑问，别当成定论。"),
        new("learning_together", "两个人一起读书，会比独自读快些吗？", "不一定快，但能及时发现自己漏看的地方。"),
        new("composure", "出了差错以后，你能很快静下来吗？", "未必。先把能补救的地方做好，心绪就会慢慢定下来。"),
        new("plain_farewell", "今夜聊得很舒心，下回再接着说吧。", "好啊。下回若有新见闻，也带来同大家说说。"),
    };

    private static readonly BanquetAmbientExchange[] GuardedExchanges =
    [
        new("guarded_old_matter", "上回那件事，我们各自把理由说清，今日先谈眼前的吧。", "好。旧事暂且放下，席上先听听旁人的见闻。"),
        new("guarded_rules", "这条规矩若不问清，回头怕又要各执一词。", "那便把缘由说透些，免得旁人也听岔了。"),
        new("guarded_distance", "此事我还想再核一遍，暂且不急着照你的法子办。", "也好，各自看清再谈，总比仓促应下强。"),
    ];

    private static readonly BanquetAmbientExchange[] FamiliarExchanges =
    [
        new("familiar_recent_news", "隔些时日再听你说起近况，倒比听旁人传闻踏实。", "近来的事并不复杂，改日有空再慢慢说给你听。"),
        new("familiar_question", "你方才把话留了一半，是等我来问么？", "倒不是卖关子，只是想先听听你怎么想。"),
        new("familiar_old_habit", "你说话还是这样直截了当，一点也没变。", "你倒记得清楚。既然如此，我也不绕弯子了。"),
    ];

    private static readonly BanquetAmbientExchange[] RespectfulExchanges =
    [
        new("respect_clear_reasoning", "你总能先把要紧处理清，我一直觉得这点难得。", "你过誉了。只是多问一句缘由，许多事就不至于绕远。"),
        new("respect_ask_judgment", "这件事我想听听你的判断，尤其是先后次序。", "我先说个粗浅想法，若有遗漏，还得请你一同斟酌。"),
    ];

    private static readonly BanquetAmbientExchange[] TrustedAdviceExchanges =
    [
        new("trusted_advice", "差事和日课撞在一起，我拿不准该先顾哪边。你会怎么排？", "先看时限和眼前轻重，再替日课留个实在的时辰。"),
        new("trusted_second_opinion", "这件事我反复想了几遍，还是想听你给个实在意见。", "我可以说说所见，不过最后还要看你手边的情形。"),
    ];

    private static readonly BanquetAmbientExchange[] TeasingExchanges =
    [
        new("familiar_teasing_order", "你把这点小事也讲得一板一眼，倒像要当场立个章程。", "总比临了才发现漏了一条好。你不也常替我补上？"),
        new("familiar_teasing_options", "你说得这般周全，莫非心里还留了三套备选？", "只留了两套。第三套得等你先把话说完。"),
    ];

    private static readonly BanquetAmbientExchange[] OutgoingExchanges =
    [
        new("outgoing_round_table", "诸位别只顾听我说，最近可有什么新鲜见闻？", "若说新鲜，倒有一桩路上的小事，正好说来听听。"),
        new("outgoing_light_topic", "今夜先不谈功课，说件近来觉得有趣的小事如何？", "这倒好。我前几日遇见一件，想起来还觉得有意思。"),
    ];

    private static readonly BanquetAmbientExchange[] ReservedExchanges =
    [
        new("reserved_shared_topic", "方才说的那处地方，我倒也去过，只是去得不多。", "那你若不嫌我话多，改日可以把路上的细节讲给我听。"),
        new("reserved_slow_question", "你刚才提到的事，有一处我没听明白，想慢慢问清。", "当然可以。不急着答，想清楚了再说也来得及。"),
    ];

    private static readonly BanquetAmbientExchange[] ConsiderateExchanges =
    [
        new("considerate_pause", "席上人多，若有话没说完，尽可慢慢来，不必抢着接。", "多谢。这样说来，倒能把真正想说的讲清楚。"),
        new("considerate_change_topic", "若这话题不合心意，我们换个轻松些的也好。", "无妨，我只是想了一下。说说近来各处的见闻吧。"),
    ];

    private static readonly BanquetAmbientExchange[] CautiousExchanges =
    [
        new("cautious_conditions", "此事还得把前后条件列清，不能只看眼前方便。", "你说得有理。我先把缺的几处记下来，再作打算。"),
        new("cautious_promise", "若时限尚宽，我想再核对一遍，确认后才答应。", "谨慎些总好过误了旁人的安排。"),
    ];

    /// <summary>按发言者对听者的真实私交与双方性情，选择能在席间自然说出口的闲谈。</summary>
    public static BanquetAmbientExchange SelectAmbientExchange(WorldNpcData speaker, WorldNpcData? listener, string stableKey)
    {
        var candidates = new List<BanquetAmbientExchange>();
        if (listener is null || listener.npc_id == speaker.npc_id)
        {
            if (NpcPersonalityRules.Is(speaker, "sociability", "外向")) candidates.AddRange(OutgoingExchanges);
            else candidates.AddRange(ReservedExchanges);
            candidates.AddRange(AmbientExchanges);
            return pick(candidates, stableKey);
        }

        var marker = CharacterRelationshipRules.LegacyMarker(speaker, listener.npc_id);
        var closeness = CharacterRelationshipRules.Value(speaker, listener.npc_id, CharacterRelationshipRules.Closeness);
        var trust = CharacterRelationshipRules.Value(speaker, listener.npc_id, CharacterRelationshipRules.Trust);
        var respect = CharacterRelationshipRules.Value(speaker, listener.npc_id, CharacterRelationshipRules.Respect);
        var resentment = CharacterRelationshipRules.Value(speaker, listener.npc_id, CharacterRelationshipRules.Resentment);
        if (marker == "friend") closeness = Math.Max(closeness, 30);
        if (marker == "enemy") resentment = Math.Max(resentment, 55);

        if (resentment >= 30)
        {
            candidates.AddRange(GuardedExchanges);
        }
        else
        {
            if (closeness >= 30) candidates.AddRange(FamiliarExchanges);
            if (respect >= 30) candidates.AddRange(RespectfulExchanges);
            if (trust >= 30) candidates.AddRange(TrustedAdviceExchanges);

            if (closeness >= 30 &&
                CharacterRelationshipRules.Value(listener, speaker.npc_id, CharacterRelationshipRules.Closeness) >= 30 &&
                NpcPersonalityRules.Is(speaker, "sociability", "外向") &&
                NpcPersonalityRules.Is(listener, "forgiveness", "宽和"))
                candidates.AddRange(TeasingExchanges);

            if (NpcPersonalityRules.Is(speaker, "sociability", "外向")) candidates.AddRange(OutgoingExchanges);
            if (NpcPersonalityRules.Is(speaker, "sociability", "内敛") || NpcPersonalityRules.Is(listener, "sociability", "内敛"))
                candidates.AddRange(ReservedExchanges);
            if (NpcPersonalityRules.Is(speaker, "benevolence", "仁厚")) candidates.AddRange(ConsiderateExchanges);
            if (NpcPersonalityRules.Is(speaker, "risk", "谨慎") || NpcPersonalityRules.Is(speaker, "order", "守序"))
                candidates.AddRange(CautiousExchanges);

            // 关系尚浅时不假称旧识；话题仍从眼前见闻与日常功课开始。
            if (closeness < 30 && marker != "friend") candidates.AddRange(AmbientExchanges);
        }

        if (candidates.Count == 0) candidates.AddRange(AmbientExchanges);
        return pick(candidates, stableKey);
    }

    private static BanquetAmbientExchange pick(IReadOnlyList<BanquetAmbientExchange> candidates, string stableKey)
    {
        if (candidates.Count == 0) return AmbientExchanges[0];
        var hash = 2166136261u;
        foreach (var character in stableKey ?? "") hash = unchecked((hash ^ character) * 16777619u);
        return candidates[(int)(hash % (uint)candidates.Count)];
    }

    public static readonly IReadOnlyList<BanquetPlayerPrompt> PlayerPrompts = new BanquetPlayerPrompt[]
    {
        new("first_steps",
            "{address}，初在门中行走时，事务与功课常挤在一处。你当时是怎样慢慢安排妥当的？",
            new BanquetDialogueReply[]
            {
                new("steady", "稳妥", "先把眼前的日课做扎实，再慢慢认清峰中规矩。"),
                new("ask_senior", "请教", "遇到不懂的地方，我会先找熟悉此事的同门问清。"),
                new("listen_all", "转问", "倒想听听诸位当初是怎样熟悉门中生活的。"),
                new("draw_in_guest", "照顾同席", "不妨请席间每位同门各说一件最有用的经验。", MinimumAttendees: 3),
            }),
        new("cultivation_stall",
            "{address}，修行遇到滞涩时，你通常会先回看功课，还是先找人请益？",
            new BanquetDialogueReply[]
            {
                new("review_routine", "自省", "先回看近来的功课是否做得踏实，不急着归咎法门。"),
                new("seek_comparison", "请益", "找一位走过相似阶段的同门，把各自体会对照一遍。"),
                new("accept_uncertainty", "坦率", "我也未必一开始就想得明白，先记下疑处再慢慢推敲。"),
                new("trace_reasoning", "细察", "把前后几日的修习逐处梳理，看看变化从哪一步开始。", "comprehension", 65),
            }),
        new("sect_duties",
            "{address}，事务板上几桩差事都合心意时，你会怎样拿捏先后？",
            new BanquetDialogueReply[]
            {
                new("finish_one", "先做眼前", "我会先把接下来的这一件办妥，再看后面的安排。"),
                new("keep_balance", "兼顾日课", "差事要办，修行也不能荒废，最好先把时辰排清。"),
                new("ask_peers", "转问同门", "诸位平日怎样安排事务与修行，才能不顾此失彼？"),
                new("make_order", "理出次序", "先把时限、路程和所需准备列清，再决定先后。", "management", 60),
            }),
        new("travel_preparation",
            "{address}，出门历练前，你更习惯把路程都记清，还是留心现场变化再作打算？",
            new BanquetDialogueReply[]
            {
                new("prepare_first", "先作准备", "把路程与要办的事先记清，能备好的东西就不要临时找。"),
                new("adapt_on_route", "留些余地", "先备妥要紧之物，其余留到路上看清情况再定。"),
                new("ask_route", "请教旧识", "先问走过这条路的同门，哪些地方容易耽搁。"),
                new("notice_details", "观察细处", "先记下沿途可能变化的地方，遇事再依实情调整。", "intelligence", 60),
            }),
        new("reputation",
            "{address}，近来有些同门听说过你的名字。你希望旁人先从哪一面认识你？",
            new BanquetDialogueReply[]
            {
                new("actions_first", "看重行事", "称呼只是称呼，日后如何行事，才该由往来慢慢说明。"),
                new("not_hide", "坦然承认", "既然是实情，便不必刻意避开；只是还得继续把本分做好。"),
                new("turn_to_them", "听听旁人", "我倒想先听听，诸位觉得一个人的名声该怎样立得住。"),
                new("speak_gracefully", "从容应对", "先以实际行事回报师门，不必急着追逐旁人的评说。", "charisma", 70),
            }),
        new("listen_and_answer",
            "{address}，若同席之人一时说不下去，你会替他接过话头，还是留些空当？",
            new BanquetDialogueReply[]
            {
                new("let_finish", "耐心听完", "先等他说完。话未出口的部分，有时才是最要紧的。"),
                new("offer_support", "温和接话", "若他一时难开口，我会先问一句是否需要旁人搭把手。"),
                new("ask_guests", "转问众人", "每个人习惯不同，诸位各自会怎样拿捏这个分寸？"),
                new("read_the_room", "体察言外", "先听他停顿的地方，再顺着真正牵挂之事接话。", "social", 60),
            }),
        new("true_disciple_notice",
            "{address}，听闻你已列入真传一脉，恭喜。名声之外，你希望同门先看到你哪一面？",
            new BanquetDialogueReply[]
            {
                new("humble_rank", "谦逊", "名录只是起点，师门所托与每日修习都还得一一做好。"),
                new("own_choice", "坦率", "我会珍惜眼下的机会，也会继续按自己的节奏走稳。"),
                new("ask_peers_rank", "反问", "诸位刚入门时，最希望师长看见的又是什么？"),
                new("answer_diplomatically", "从容作答", "先以实际行事回报师门，旁人的评说便不必急于分辨。", "charisma", 65),
            }, RequiresPublicRank: true),
    };
}
