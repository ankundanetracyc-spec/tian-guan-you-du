using System;
using System.Collections.Generic;

/// <summary>只将已执行的分支转为可见举止。每次展示选定措辞，播放中不重抽，也不使用世界随机源。</summary>
public static partial class NpcSocialPresentationText
{
    public sealed record Line(string Pending, string Result, string Rule = "", bool? Favorable = null);
    private sealed record Phrase(string Pending, string Warm, string Cool = "");

    // 同组短句对应相同事实；不会因为更换措辞制造新的认知、过往、道具或私人关系。
    private static readonly Phrase[] Presence = [
        new("对方抬眼望来", "你从容见礼，对方含笑回应。", "你开口有些局促，对方只客气应声。"),
        new("对方听你问候", "你神色自然，对方没有避开目光。", "你问候略显生硬，对方仍隔着几步。"),
        new("对方停下脚步", "见你举止得体，对方语气温和了些。", "你开口略显迟疑，对方只简短回应。"),
        new("你开口问候", "你见礼自然，对方颔首相迎。", "你见礼时有些拘束，对方回应客套。"),
        new("对方朝你看来", "你神色从容，对方也放缓了语气。", "你说话有些紧张，对方移开了目光。"),
        new("你向对方见礼", "你大方地问候，对方也含笑还礼。", "你开口不大自在，对方匆匆应声。") ];
    private static readonly Phrase[] Reply = [
        new("话头渐渐展开", "你接话得体，对方愿意多谈几句。", "你没能接住话头，对方的回应渐短。"),
        new("你接着说下去", "你问得恰当，对方说起了近况。", "你问得有些唐突，对方只简短回应。"),
        new("你们说了几句", "话题接得自然，寒暄渐渐热络。", "你几次换话题没接稳，交谈渐冷。"),
        new("对方听你说着", "你听完才接话，对方说得更细了些。", "你几次打断对方，对方后来只客气应声。"),
        new("寒暄未尽", "你顺势接话，交谈没有冷场。", "你没有接上话，问候后便没了下文。"),
        new("你稍停了停", "你留了些余地，对方自己续起话头。", "你回话略显生硬，对方也收住话头。") ];
    private static readonly Phrase[] PlayerMood = [
        new("你放缓话头", "你眉间舒展，回话也从容。", "你眉间愁色未散，回话几次停顿。"),
        new("你略作停顿", "你应声带笑，话说得自然。", "你揉了揉眉心，回话有些迟缓。"),
        new("你接过话来", "你神色平和，顺势说起了近况。", "你眉头紧锁，几次只应了一声。"),
        new("你停步回应", "你神情舒展，几句问候说得轻松。", "你神色紧绷，回话也短。"),
        new("你轻轻颔首", "你语气舒缓，话头接得从容。", "你倦意难掩，几句寒暄便住了口。"),
        new("你斟酌片刻", "你平静地回应，没有急着收住话头。", "你眉间郁色未消，半晌才接上一句。") ];
    private static readonly Phrase[] Manner = [
        new("对方开口回礼", "对方神情温和，少了些拘束。", "对方神情疏淡，回应客气却简短。"),
        new("对方抬起头来", "对方回礼周全，语气也亲切。", "对方回话冷淡，仍与你保持距离。"),
        new("对方整了整衣袖", "对方举止温文，寒暄少了些客套。", "对方回礼有些生硬，仍显拘束。"),
        new("对方略一颔首", "对方含笑问候，与你攀谈起来。", "对方只点头应声，没再接话。"),
        new("对方与你相对", "对方态度随和，回礼自然。", "对方语气疏冷，几句问候也生分。"),
        new("对方回过身来", "对方侧身相迎，问候得颇为周全。", "对方礼数客气，言语却仍有隔阂。") ];
    private static readonly Phrase[] NpcReply = [
        new("对方续起话头", "对方接话自然，几句寒暄便聊了起来。", "对方没能接住你的话，交谈渐停。"),
        new("对方斟酌着开口", "对方问起近况，交谈很快有了来回。", "对方问候说完，便没再展开话题。"),
        new("对方略作停顿", "对方说起自己的见闻，话题渐渐接开。", "对方来意说完，几次回应都很简短。"),
        new("对方接着说道", "对方不时问上一句，与你谈得颇顺。", "对方说话断续，几次没接上话头。"),
        new("对方将话接过", "对方从问候聊到日常，始终没有冷场。", "对方几次重复先前的话，没再聊开。"),
        new("对方稍稍侧首", "对方添了几句见闻，将话题续了下去。", "对方回话生硬，寒暄后便没了下文。") ];
    private static readonly Phrase[] NpcMood = [
        new("对方稍歇片刻", "对方神色和缓，主动说起了近况。", "对方眉间带倦，回话很简短。"),
        new("对方缓缓开口", "对方语气轻快，又与你多谈了几句。", "对方语气低沉，话到半途便停了。"),
        new("对方停了一停", "对方笑意未收，谈兴正浓。", "对方神色郁郁，迟迟才回了一句。"),
        new("对方轻轻颔首", "对方眉间舒展，说话也少了拘谨。", "对方倦意未消，几句便收住话头。"),
        new("话声稍歇", "对方仍带着笑意，又接起了话头。", "对方揉了揉眉心，没有再接话。"),
        new("对方望了望你", "对方神色轻松，说起了近日的日常。", "对方神色紧绷，没能继续闲谈。") ];
    private static readonly Phrase[] Sect = [
        new("说起峰中往来", "谈及同门身份，对方少了几分生疏。"),
        new("提及各自来处", "得知同属一宗，对方报了自己的峰头。"),
        new("话到师门", "同门身份说开，对方的称呼亲近了些。"),
        new("说起宗中近况", "谈到宗中日常，对方不再一味客套。"),
        new("谈到宗门", "听你说起师门，对方语气熟络了些。"),
        new("你们说起来历", "对方称了声同门，与你多聊了几句。") ];
    private static readonly Phrase[] Identity = [
        new("听到你的名号", "说起真传入门，对方郑重还礼。"),
        new("对方稍稍一怔", "对方提起你的真传身份，拱手称贺。"),
        new("对方又看了你一眼", "对方道了声久闻真传之名，语气郑重。"),
        new("话到入门之事", "对方向你称贺拜入真传，礼数周全。"),
        new("你们谈及初入宗门", "听闻你新入真传，对方认真见了一礼。"),
        new("对方停住话头", "提到你的真传名号，对方正了正神色。") ];
    private static readonly Phrase[] Renown = [
        new("话到近日传闻", "提起你在百川的事迹，对方多问了两句。"),
        new("对方提起一件旧事", "说到你做过的事，对方语气郑重了些。"),
        new("你们说起沿途消息", "对方将传闻与你对上，笑着拱手。"),
        new("对方略一停顿", "对方提起听过的事迹，向你问了几句。"),
        new("话头转到百川", "对方说起听来的消息，向你求证。"),
        new("对方提到一则传闻", "谈到你的公开事迹，对方颔首称许。") ];
    private static readonly Phrase[] Familiar = [
        new("对方细看片刻", "对方叫出你的名字，笑着问起近况。"),
        new("你们打了照面", "对方叫出你的名字，与你打了招呼。"),
        new("对方望向你", "对方认出是你，先问起这些日子的近况。"),
        new("对方停步回望", "对方认出你来，言语间少了客套。"),
        new("你们彼此见礼", "认出是旧识，对方向你问起近况。"),
        new("对方侧身相迎", "对方问起你别来可好，语气熟络。") ];
    private static readonly Phrase[] Friend = [
        new("你们说起近况", "与你已有交情，对方说话自在了许多。"),
        new("对方与你见礼", "对方笑着招呼你，没再讲究客套。"),
        new("你们又见了面", "对方细问你的起居，言语亲近。"),
        new("对方将话头接过", "对方与你说笑起来，语气熟稔。"),
        new("几句问候过后", "略过客套，对方先问起你的近况。"),
        new("对方招呼你停步", "对方笑着打趣一句，少了生客的拘谨。") ];
    private static readonly Phrase[] Initiative = [
        new("对方欲言又止", "对方主动攀谈，几句话便接上话头。", "对方虽先来问候，说完来意仍显拘谨。"),
        new("对方略一停顿", "对方先问起近况，又说了自己的见闻。", "对方问候略显迟疑，几句后便住了口。"),
        new("对方似要开口", "对方主动接过话题，交谈没有僵住。", "对方不大主动接话，你问一句才答一句。"),
        new("对方停了一会儿", "对方先说起日常，谈兴颇浓。", "对方几次欲言又止，没再多说。"),
        new("对方将目光转来", "对方爽快地邀你闲聊，语气热络。", "对方回话客气，却不大主动展开话题。"),
        new("对方斟酌着话头", "对方主动聊起沿途见闻。", "对方回应拘束，寒暄后便沉默下来。") ];
    private static readonly Phrase[] Caution = [
        new("话到彼此来历", "对方问了几句来处，没有接着自报姓名。"),
        new("对方稍作沉吟", "对方只回了一礼，姓名之事轻轻带过。"),
        new("你提及姓名", "对方顿了顿，将话题转向别处。"),
        new("对方斟酌片刻", "对方客气地应声，却没留下姓名。"),
        new("话头暂歇", "对方略拱了拱手，仍有所保留。"),
        new("对方轻抿嘴唇", "对方没有接下问询，回礼后便止住话头。") ];
    private static readonly Phrase[] NoFamiliarBasis = [
        new("彼此还不相熟", "你们此前没有往来，对方一时还不清楚你的来历。"),
        new("话到姓名之前", "此前未曾相识，对方尚在掂量这番交谈从何说起。"),
        new("对方略作打量", "初次照面缺少凭据，对方还不能把你与旧闻对上。"),
        new("你们初次见面", "彼此素无交集，对方先听你说完来意。"),
        new("话头稍稍放缓", "对方对你的来历还不熟悉，交谈仍停在客套上。"),
        new("对方听你提起姓名", "对方尚未听过你的名号，只静静听着，没有贸然接话。") ];
    private static readonly Phrase[] Exchange = [
        new("话到彼此称呼", "对方报上姓名，向你拱了拱手。"),
        new("对方斟酌片刻", "对方随即报了名姓，还了一礼。"),
        new("你们互相见礼", "对方说出姓名，含笑颔首。"),
        new("对方轻轻点头", "对方将姓名告知，接着又说了几句。"),
        new("话头稍歇", "对方道出名姓，礼数很是周全。"),
        new("对方将话接过", "对方自报姓名，与你正式见了礼。") ];
    private static readonly Phrase[] SectInterest = [
        new("话到峰中日常", "说起峰中起居，对方追问了几句。"),
        new("你们说起宗中事务", "对方细问你平日在宗中怎样修行。"),
        new("对方提起峰中见闻", "谈到宗门近况，对方又多聊了几句。"),
        new("话题转到师门", "对方问起你在峰中的日常安排。"),
        new("你们说起同门", "对方聊起峰中日常，谈得比先前更细。"),
        new("对方停步倾听", "提到宗中事务，对方接连问了几句。") ];
    private static readonly Phrase[] RenownInterest = [
        new("对方提起传闻", "对方追问你在百川做过的事。"),
        new("说到近日消息", "谈到公开事迹，对方细问经过。"),
        new("话到百川传闻", "对方说起听来的消息，向你求证。"),
        new("对方略一沉吟", "对方细问事迹，说此前只听过传闻。"),
        new("你们说起见闻", "对方接连问起你的公开事迹。"),
        new("对方续起话头", "对方与你核对听来的传闻。") ];
    private static readonly Phrase[] EntryInterest = [
        new("话到宗中传闻", "对方问起你拜入真传的经过。"),
        new("对方提起迎真消息", "对方细问你入门后的近况。"),
        new("你们说起入门", "对方询问近日的迎真之事。"),
        new("对方续起话头", "对方问起你入门后的修行安排。"),
        new("话到拜入师门", "对方与你聊起真传入门的经历。"),
        new("对方提起宗中近况", "谈到迎真消息，对方追问了几句。") ];
    private static readonly Phrase[] Hostile = [
        new("对方目光微凝", "对方冷冷看了一眼，没有接下话头。"),
        new("你们打了照面", "对方神色沉下，只回了短短一句。"),
        new("对方停住脚步", "对方拱手甚敷衍，随即转过身去。"),
        new("对方缓缓抬眼", "对方避开寒暄，语气生硬。"),
        new("对方略一停顿", "对方脸上笑意尽收，不再接话。"),
        new("你开口问候", "对方没有还礼，侧过身与人拉开距离。") ];
    private static readonly Phrase[] Decline = [
        new("你示意尚有事情", "对方颔首告辞，没有挽留。"),
        new("你婉言辞去", "对方收住话头，还礼后各自离开。"),
        new("你向对方告辞", "对方应了声改日再会，便退开一步。"),
        new("你将话头收住", "对方见你要走，也拱手作别。"),
        new("你轻轻摇头", "对方没有再劝，客气地回了一礼。"),
        new("你示意改日再谈", "对方点了点头，将话留到了下次。") ];
    private static readonly Phrase[] Approach = [
        new("你走近对方", "你上前见礼，向对方问候。"),
        new("你略整衣袖", "你拱手见礼，说明来意。"),
        new("你停下脚步", "你主动开口，问候了几句。"),
        new("你向对方见礼", "你先打了个招呼，提起交谈之意。"),
        new("你斟酌称呼", "你先行问候，试着接起话头。"),
        new("你稍稍走近", "你停步见礼，开口与对方搭话。") ];
    private static readonly Phrase[] Cancelled = [
        new("你稍作停顿", "你暂且收起了交谈的念头。"),
        new("你停住脚步", "你没有上前，决定改日再谈。"),
        new("你略一思量", "你暂未开口，打算稍后再谈。"),
        new("你望了望对方", "你没有上前打扰，暂且作罢。"),
        new("你收住步子", "你将问候留到下次，没有上前搭话。"),
        new("你暂缓片刻", "你打消了此时交谈的念头。") ];
    private static readonly Phrase[] Accepted = [
        new("你应声回礼", "对方颔首，将方才的话续了下去。"),
        new("你示意继续", "对方停步相对，把来意说全。"),
        new("你接过问候", "对方轻轻点头，开口与你攀谈。"),
        new("几句寒暄过后", "对方顺势说起近况，话声未歇。"),
        new("你还了一礼", "对方应声站定，又说了几句。"),
        new("话头接了上来", "对方笑着还礼，接着往下说。") ];
    private static readonly Phrase[] SparStranger = [
        new("话到切磋", "对方客气回礼，只说不妨先认识一番。"),
        new("对方停步相对", "对方没有应下对练，先向你问起称呼。"),
        new("对方略一沉吟", "对方摆了摆手，说先聊上几句也不迟。"),
        new("你将邀约说完", "对方还礼，说不如先结识一番。"),
        new("对方迟迟未应", "对方暂将切磋搁下，先与你互问来处。"),
        new("话声稍歇", "对方没有立即应邀，只客气问起你的来历。") ];
    private static readonly Phrase[] SparGap = [
        new("对方略整衣袖", "对方摇了摇头，只道还需再练些时日。"),
        new("对方斟酌片刻", "对方拱手推辞，说眼下火候尚浅。"),
        new("你将邀约说完", "对方摆手，说待修行再进一步，改日请教。"),
        new("对方望了望你", "对方还了一礼，直言暂时不敢应邀。"),
        new("话到相互印证", "对方苦笑着摇头，只说往后再来领教。"),
        new("对方沉吟了一会儿", "对方拱手，说还要多修炼些日子。") ];
    private static readonly Phrase[] SparReserved = [
        new("对方略一停顿", "对方拱手推辞，将邀约留待改日。"),
        new("你说起对练", "对方只客气还礼，没有接下邀约。"),
        new("对方稍稍垂眼", "对方轻轻摇头，说改日再谈。"),
        new("话声渐歇", "对方委婉推辞，随后与你作别。"),
        new("对方整了整衣襟", "对方还了一礼，暂且谢过你的邀约。"),
        new("对方沉吟片刻", "对方没有应下，只说往后有缘再会。") ];
    private static readonly Phrase[] SparInterest = [
        new("话到所学", "对方笑着应声，约你试上一回。"),
        new("你们说起对练", "对方点头，说正好相互印证一番。"),
        new("对方略一沉吟", "对方问起切磋之法，语气也轻快起来。"),
        new("对方停步相对", "对方拢起衣袖，笑着说愿意领教。"),
        new("话到相互印证", "对方应得爽快，又与你说了几句所学。"),
        new("对方抬眼看来", "对方点了点头，说不妨试试彼此身手。") ];
    private static readonly Phrase[] SparTerms = [
        new("双方略作商议", "你们约定点到为止，随后相互见礼。"),
        new("对方将话接过", "对方言明点到即止，你颔首应下。"),
        new("你们定下规矩", "双方约好只论所学，胜负不作争执。"),
        new("话到对练规矩", "对方与你约定收手之时，互相还礼。"),
        new("双方停步相对", "你们言明点到为止，准备开始对练。"),
        new("你们商议片刻", "双方约定胜负之外不作争执，随即见礼。") ];
    private static readonly Phrase[] Supplied = [
        new("你取出药材", "对方接过药材，低头验看。"),
        new("对方稍作查验", "对方验过药材，点了点头。"),
        new("你将药材递过", "对方逐一看过，收下了药材。"),
        new("双方清点药材", "药材清点妥当，对方向你颔首。"),
        new("对方俯身验看", "对方看过成色，将药材收好。"),
        new("你们核对片刻", "对方收下药材，与你对过数目。") ];
    private static readonly Phrase[] Paid = [
        new("对方取出灵石", "对方付过灵石，与你逐一核对。"),
        new("双方核对价钱", "灵石交到你手中，数目与商定相符。"),
        new("你们清点片刻", "灵石与药材交割妥当，双方还礼。"),
        new("对方将灵石递来", "你点过灵石，对方也收好了货品。"),
        new("双方最后核对", "对方依照谈妥的价钱，付清了灵石。"),
        new("对方将钱款取出", "双方清点妥当，买卖就此了结。") ];
    private static readonly Phrase[] Await = [
        new("对方略一沉吟", ""), new("对方斟酌片刻", ""), new("对方稍作停顿", ""),
        new("话声暂歇", ""), new("对方轻轻颔首", ""), new("对方欲言又止", "") ];
    private static readonly Phrase[] SparAwait = [
        new("话到切磋之事", ""), new("对方略整衣袖", ""), new("对方停步相对", ""),
        new("对方稍稍沉吟", ""), new("对方将话接过", ""), new("对方看了看你", "") ];
    private static readonly Phrase[] TradeAwait = [
        new("对方接过药材", ""), new("对方稍作查验", ""), new("双方核对片刻", ""),
        new("对方将灵石取出", ""), new("对方看了看货品", ""), new("你们清点片刻", "") ];

    private static Phrase[] Bank(string rule, string action) => rule switch
    {
        "player_charisma" => Presence,
        "player_social" => Reply,
        "player_mood" => PlayerMood,
        "npc_charisma" => Manner,
        "npc_social" => NpcReply,
        "npc_mood" => NpcMood,
        "same_faction" or "same_sect" => Sect,
        "recognized_identity" or "heard_rank" or "renown_visit" => Identity,
        "public_renown" => Renown,
        "known_name" or "known_overcomes_caution" => Familiar,
        "friendship" or "attached_visit" or "friend_visit" => Friend,
        "outgoing" or "outgoing_visit" or "reserved" or "visit_initiative" => Initiative,
        "caution" or "caution_veto" => Caution,
        "no_familiar_basis" => NoFamiliarBasis,
        "basis_overcomes_caution" or "no_caution_veto" => Exchange,
        "sect_priority" or "sect_visit" => SectInterest,
        "renown_priority" => action == "来访" ? EntryInterest : RenownInterest,
        "hostile_veto" or "test_hostile" => Hostile,
        "player_declined" => Decline,
        "player_cancelled" => Cancelled,
        "player_initiated" => Approach,
        "player_accepted" => Accepted,
        "spar_hostile" => Hostile,
        "spar_unknown" => SparStranger,
        "spar_known_gap" => SparGap,
        "spar_reserved" => SparReserved,
        "spar_familiar" or "spar_cultivation" => SparInterest,
        "spar_experienced" => SparInterest,
        "spar_terms" => SparTerms,
        "purchase_supplied" => Supplied,
        "purchase_paid" => Paid,
        _ when action.Contains("切磋", StringComparison.Ordinal) => SparAwait,
        _ when action.Contains("交易", StringComparison.Ordinal) => TradeAwait,
        _ => Await
    };

    public static IReadOnlyList<Line> Create(NpcSocialRules.Response response, int variation)
    {
        var visible = new List<NpcSocialRules.DecisionStep>();
        foreach (var step in response.DecisionSteps) if (step.Visible) visible.Add(step);
        var lines = new List<Line>(response.Factors.Count);
        for (var index = 0; index < response.Factors.Count; index++)
        {
            var step = visible.Count == response.Factors.Count ? visible[index] : null;
            var rule = step?.Rule ?? "";
            var bank = Bank(rule, response.ActionName);
            uint hash = 2166136261;
            foreach (var c in rule) hash = unchecked((hash ^ c) * 16777619);
            var mixed = unchecked(hash ^ (uint)variation * 0x9e3779b9u ^ (uint)index * 0x85ebca6bu);
            mixed = Mix(mixed);
            var phrase = bank[(int)(mixed % (uint)bank.Length)];
            var cool = step?.Favorable == false || rule == "reserved";
            var result = cool && phrase.Cool.Length > 0 ? phrase.Cool : phrase.Warm;
            // 不认识的专题规则原文照用；只变换该专题适用的等待语。
            lines.Add(new Line(phrase.Pending, result.Length > 0 ? result : response.Factors[index], rule, step?.Favorable));
        }
        ShuffleIndependentLines(lines, variation);
        ConnectContext(lines, response.ActionName, variation);
        return lines;
    }

    private static bool IsIndependent(string rule) => rule is
        "player_charisma" or "player_social" or "player_mood" or "npc_charisma" or "npc_social" or "npc_mood" or
        "same_faction" or "same_sect" or "recognized_identity" or "heard_rank" or "public_renown" or
        "known_name" or "friendship" or "outgoing" or "outgoing_visit" or "reserved" or "visit_initiative" or
        "sect_priority" or "sect_visit" or "renown_priority" or "renown_visit" or "attached_visit" or "friend_visit";

    private static uint Mix(uint value)
    {
        value ^= value >> 16; value = unchecked(value * 0x7feb352du);
        value ^= value >> 15; value = unchecked(value * 0x846ca68bu);
        return value ^ (value >> 16);
    }

    private static void ShuffleIndependentLines(List<Line> lines, int variation)
    {
        var random = unchecked((uint)variation ^ 0xd1b54a35u);
        for (var start = 0; start < lines.Count;)
        {
            if (!IsIndependent(lines[start].Rule)) { start++; continue; }
            var end = start + 1;
            while (end < lines.Count && IsIndependent(lines[end].Rule)) end++;
            // 只重排相邻的独立表现；接受、阻断、交换姓名、交割等步骤是顺序边界。
            for (var index = end - 1; index > start; index--)
            {
                random = Mix(unchecked(random + 0x9e3779b9u));
                var swap = start + (int)(random % (uint)(index - start + 1));
                (lines[index], lines[swap]) = (lines[swap], lines[index]);
            }
            start = end;
        }
    }
}
