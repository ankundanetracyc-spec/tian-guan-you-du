/// <summary>
/// 序章·启明的唯一文案源。
/// 编辑叙事、任务卡和开场选书提示时，只改这个文件；触发时机仍由 PrologueGuide 管理。
/// </summary>
public static class PrologueText
{
    // patch probe
    public static (string path, string name, string direction)[] InitialChoices => GongfaAvailability.EnabledBooks()
        .Select(book => (book.ResourcePath, book.gongfa_name, book.description)).ToArray();

    public static readonly string[] OpeningLines =
    [
        "你苏醒之时，已然被困在一面古镜之中。",
        "镜外几度寒暑流转，偶有修士踏空掠过长空，你才知晓此方世界本就不凡。",
        "时日一久，一名男子将这面古镜带回了青溪里。",
        "他便是李家家主，亦是这座村落的里正。",
        "李家幼子跑过镜前的刹那，镜面骤然泛起微光。",
        "你本无心施为，一缕玄光却已然落在那孩童身上。",
        "灵气在他掌心转瞬聚散，数卷入门功法，悄然印入他的识海。",
        "家主未曾声张，只是默默阖上了院门。",
        "一面来历不明的古镜，一个骤然感应灵气的孩儿，万万不可被外人察觉。"
    ];

    public const string SavedChoiceHint = "玄镜将这一卷功法渡予幼子。";
    public const string SavedChoiceButton = "承下此卷";
    public const string ChoiceHint = "为李家幼子择取一卷入门功法";

    public static string InitialTask(string child, string book) => $"首卷未竟\n打开幼子：[u]{child}[/u] 的功法页，在研读位放入《{book}》。玄镜已替他参悟大半。";
    public const string CharacterOpenedTask = "首卷未竟\n进入研读界面，放入玄镜传下的功法，余下要义交由少年自行参悟。";
    public static string StudyStartedTask(string book) => $"首卷研读\n《{book}》尚有两成未曾通透。静待时日，待少年完整读通此卷。";

    public static string ChapterCompletedNarration(string gongfa, string chapter) => "识海中功法铭文归于沉静。少年缓缓吐出一口浊气。";
    public const string ChapterCompletedFather = "经文已然通读。将它定为主修，往后吐纳修行才有根本章法。";
    public static string SetMainTask(string gongfa) => $"定下主修\n在功法页将《{gongfa}》设为主修。";

    public const string FirstQiNarration = "檐下，少年摊开手掌。一丝灵气堪堪凝聚，转眼便自指缝消散。";
    public const string FirstQiFather = "不必强行攥住。循着方才读懂的法门，慢慢吐纳调息。";
    public const string MainSetFather = "功法已定，余下唯有日复一日的苦修。日程切莫排布过满，能够长久坚持，方为正道。";
    public const string ScheduleTask = "安排日程\n为幼子规划每日修炼与休憩的时辰。";
    public const string ScheduleNarration = "晨雾漫落庭院，少年依法门盘膝调息。指尖那一缕微弱灵气，这一次不再溃散。";
    public const string ScheduleFather = "勿求速成。先把这一口内息养得纯熟安稳。";
    public const string CultivationTask = "引灵入云\n持续苦修，攒足十二缕灵力，突破至云息一层。";

    public const string BreakthroughNarration = "灵力循经脉流转周天，少年抬眼睁眼。院落间风吹草木的细碎声响，在此刻骤然清晰分明。";
    public const string BreakthroughFatherPaths = "已是云息一层。修行不再以三条定途分人：诸法皆可研习，修命养形与修性观物也都能并行。你先按眼下所学行功，往后再慢慢体会各门秘籍的相济之处。";
    public const string BreakthroughFatherBeast = "田间异兽损毁庄稼。明日随我一同前去，此战由你独自出手。";
    public const string BeastTask = "砺行知险\n跟随父亲离开村落，操控幼子独自斩杀一头野兽。";
}
