using System.Collections.Generic;

public static partial class NpcSocialPresentationText
{
    // 只记录本次已经展示的举止和话题，不读取隐藏思想，也不参与游戏判定。
    private enum Topic { None, Sect, Entry, Deeds }

    private static void ConnectContext(List<Line> lines, string action, int variation)
    {
        bool? lastReception = null;
        var topics = new HashSet<Topic>();
        var lastTopic = Topic.None;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var cool = line.Favorable == false || line.Rule == "reserved";
            var pick = (int)(Mix(unchecked((uint)variation ^ (uint)index * 0x85ebca6bu)) % 4);
            var topic = TopicOf(line.Rule, action);
            var phrase = ContextPhrase(line.Rule, index == 0, lastReception, cool);
            if (topic != Topic.None && topics.Contains(topic))
                phrase = ContinueTopic(topic, lastTopic == topic);
            else if (topic != Topic.None && lastReception == false)
                phrase = OpenTopicAfterReserve(topic);

            if (phrase != null)
            {
                var selected = phrase[pick % phrase.Length];
                lines[index] = line with
                {
                    Pending = selected.Pending,
                    Result = cool && selected.Cool.Length > 0 ? selected.Cool : selected.Warm
                };
            }

            if (topic != Topic.None)
            {
                topics.Add(topic);
                lastReception = true;
            }
            else if (line.Rule is "player_charisma" or "player_social" or "npc_charisma" or "npc_social" or
                     "npc_mood" or "outgoing" or "outgoing_visit" or "reserved" or "visit_initiative")
                lastReception = !cool;
            lastTopic = topic;
            // 玩家心境不等于对方态度；未知专题也不擅自推断对方已变得热络。
        }
    }

    private static Topic TopicOf(string rule, string action) => rule switch
    {
        "same_faction" or "same_sect" or "sect_priority" or "sect_visit" => Topic.Sect,
        "recognized_identity" or "heard_rank" or "renown_visit" => Topic.Entry,
        "renown_priority" => action == "来访" ? Topic.Entry : Topic.Deeds,
        "public_renown" => Topic.Deeds,
        _ => Topic.None
    };

    private static Phrase[]? ContextPhrase(string rule, bool first, bool? previous, bool cool)
    {
        // 开场只在真正的第一行使用；之后的举止都发生在已经开始的交谈中。
        if (rule == "player_charisma" && !first) return [
            new("对方看向你", "你举止从容，对方也放缓了语气。", "你略显拘束，对方应答也客套了些。"),
            new("你略整衣袖", "你神色自然，对方与你目光相接。", "你显得有些局促，对方只客气应声。"),
            new("你抬眼回应", "你从容应声，对方颔首回应。", "你应声时有些迟疑，对方只简短回应。"),
            new("对方朝你望来", "你应对自然，对方的语气柔和了些。", "你神色拘谨，对方的回应也生分了些。") ];

        if (rule == "player_mood") return [
            new("你稍稍停顿", "你眉间舒展，回应得很从容。", "你眉间愁色未散，回话略有停顿。"),
            new("你轻轻颔首", "你神色平和，语气也舒缓。", "你神色紧绷，回话有些迟缓。"),
            new("你斟酌着开口", "你应声带笑，说得轻松。", "你揉了揉眉心，半晌才应了一句。"),
            new("你抬起眼来", "你神情舒展，言语间也很自在。", "你倦意难掩，说话时几次停顿。") ];

        if (rule == "npc_charisma" && !first) return previous == true && cool ? [
            new("对方略一颔首", "话虽接上了，对方的礼数仍显生分。"),
            new("对方整了整衣袖", "虽有了几句来往，对方应答仍很客套。"),
            new("对方抬眼回应", "交谈间，对方的举止仍透着些疏淡。"),
            new("对方将话接过", "话题虽未断，对方回应却少了些亲切。") ] : [
            new("对方略一颔首", "对方举止温和，应答也亲切了些。", "对方举止疏淡，应答客气而简短。"),
            new("对方整了整衣袖", "对方神色温和，言语少了些客套。", "对方礼数周全，言语却显生分。"),
            new("对方抬眼回应", "对方语气随和，交谈少了些拘束。", "对方回应生硬，仍与你保持着距离。"),
            new("对方将话接过", "对方应答温文，态度也亲切。", "对方只略一颔首，神情疏淡。") ];

        if (rule == "npc_mood" && previous == true && cool) return [
            new("对方稍作停顿", "话虽说得顺，对方眉间却带着倦意。"),
            new("对方缓缓开口", "对方仍在应声，语气却低沉了些。"),
            new("对方抬起头来", "交谈未断，对方眉间的郁色却未散。"),
            new("话声稍歇", "对方接着回话，倦意却难以遮掩。") ];
        if (rule == "npc_mood") return previous == false && !cool ? [
            new("对方稍作停顿", "对方神色倒是轻松，说话也渐渐轻快。"),
            new("对方缓缓开口", "稍歇片刻，对方舒展眉头，语气缓和了些。"),
            new("对方抬起头来", "对方眉间渐松，回应不再那么沉闷。"),
            new("话声稍歇", "对方神情舒缓下来，说话轻快了些。") ] : [
            new("对方稍作停顿", "对方眉间舒展，说得轻松。", "对方眉间带倦，应声有些迟缓。"),
            new("对方缓缓开口", "对方神色轻松，语气也轻快。", "对方语气低沉，回话略有停顿。"),
            new("对方抬起头来", "对方应声带笑，神色和缓。", "对方神色郁郁，半晌才回了一句。"),
            new("话声稍歇", "对方神情舒展，谈起话来颇有精神。", "对方倦意未消，说话时揉了揉眉心。") ];

        if (rule is "player_social" or "npc_social")
        {
            var player = rule == "player_social";
            // 不用“没了下文／就此告辞”描述一次负面表现，以免后续真实判定又凭空重启交谈。
            if (previous == false && !cool) return player ? [
                new("你斟酌着接话", "你换了个妥当的问法，对方这才多说了几句。"),
                new("你留了些余地", "你耐心听完再接话，话头渐渐顺了起来。"),
                new("你略作停顿", "你接话不急不缓，交谈少了些生涩。"),
                new("你将话接过", "你问得恰当，对方的回应渐渐多了起来。") ] : [
                new("对方斟酌着开口", "对方换了个话头，交谈渐渐有了来回。"),
                new("对方略作停顿", "对方添了几句见闻，将生涩的话头接了起来。"),
                new("对方将话接过", "对方问起近况，交谈总算顺畅了些。"),
                new("对方慢慢说道", "对方说得细致，先前的几分生涩渐渐散了。") ];
            return player ? [
                new("你斟酌着接话", "你接话得体，对方愿意多谈几句。", "你接话略显生硬，对方的回应短了些。"),
                new("你留了些余地", "你听完才接话，对方说得更细了些。", "你没接稳话头，交谈略有停顿。"),
                new("你略作停顿", "你问得恰当，话题接得自然。", "你问得有些唐突，对方只简短回应。"),
                new("你将话接过", "你应对得宜，话头顺畅地接了下去。", "你回话不大顺畅，寒暄生涩了些。") ] : [
                new("对方斟酌着开口", "对方接话自然，交谈很快有了来回。", "对方接话有些生硬，交谈略有停顿。"),
                new("对方略作停顿", "对方添了几句见闻，将话题接开。", "对方说话断续，几次没接稳话头。"),
                new("对方将话接过", "对方问起近况，话题自然地展开。", "对方回话很短，没有展开细说。"),
                new("对方慢慢说道", "对方说得细致，与你有问有答。", "对方斟酌许久，回应仍有些生涩。") ];
        }
        if (rule is "outgoing" or "outgoing_visit" or "reserved" or "visit_initiative") return [
            new("对方似要开口", "对方主动添了几句见闻。", "对方不大主动，你问一句才答一句。"),
            new("对方略一停顿", "对方主动问起你的近况。", "对方几次欲言又止，回应颇为拘谨。"),
            new("对方将目光转来", "对方自行接过话头，说得爽快。", "对方应答客气，却很少主动展开话题。"),
            new("对方斟酌着话头", first ? "对方先说起自己的见闻。" : "对方接着说起自己的见闻。", "对方似有话说，末了只简短应声。") ];
        return null;
    }

    private static Phrase[] ContinueTopic(Topic topic, bool immediate) => topic switch
    {
        Topic.Sect => [
            new(immediate ? "话到峰中日常" : "话题又转到师门", immediate ? "顺着宗中的话题，对方问起你平日的修行。" : "话题又回到宗中，对方问起你平日的修行。"),
            new(immediate ? "对方细问了几句" : "对方又问起宗中近况", immediate ? "谈到宗中往来，对方说得比先前详细了些。" : "又聊起宗中往来，对方说得比先前详细了些。"),
            new(immediate ? "说起同门近况" : "又说起同门近况", immediate ? "对方接着问起你在峰中的起居。" : "对方又提到师门，问起你在宗中的起居。"),
            new(immediate ? "对方将话接过" : "对方又提到师门", immediate ? "聊到宗中日常，对方的称呼亲近了些。" : "再谈宗中日常，对方的称呼亲近了些。") ],
        Topic.Entry => [
            new(immediate ? "话到入门之后" : "对方又提起入门之事", immediate ? "说到真传入门，对方接着问起近日的安排。" : "对方又说起真传入门，问起你近日的安排。"),
            new("对方细问了几句", "对方顺着入门的话题，问起你近来的修行。"),
            new("说起近日起居", "谈及入门后的日常，对方听得很认真。"),
            new("对方将话接过", "对方又问了几句迎真之事，语气郑重。") ],
        _ => [
            new(immediate ? "对方细问经过" : "话题又转到那则传闻", "顺着方才的传闻，对方细问了几处经过。"),
            new("对方斟酌片刻", "对方将听来的消息与你核对。"),
            new("对方将话接过", "谈到那些公开事迹，对方又多问了两句。"),
            new("说起传闻中的细节", "对方接着问起事情的经过，颔首称许。") ]
    };

    private static Phrase[] OpenTopicAfterReserve(Topic topic) => topic switch
    {
        Topic.Sect => [
            new("话到师门", "说到同门身份，对方的语气才缓和了些。"),
            new("提及各自来处", "得知同属一宗，对方少了几分生疏。"),
            new("说起宗中近况", "聊到宗中的事，对方才多说了几句。"),
            new("话到峰中往来", "提及师门，对方的应答不再那么客套。") ],
        Topic.Entry => [
            new("话到入门之事", "提到你的真传身份，对方正了正神色。"),
            new("对方稍稍一怔", "说起真传入门，对方这才郑重拱手称贺。"),
            new("对方抬眼看来", "话到真传入门，对方的应答郑重了些。"),
            new("说起近日迎真", "提及你拜入真传，对方认真还了一礼。") ],
        _ => [
            new("话到近日传闻", "说起你在百川的事迹，对方才多问了几句。"),
            new("对方提到一则消息", "谈到你的公开事迹，对方的语气郑重了些。"),
            new("话头转到百川", "说到听来的传闻，对方抬眼向你求证。"),
            new("说起沿途消息", "谈到那些事迹，对方的回应不再那么简短。") ]
    };
}
