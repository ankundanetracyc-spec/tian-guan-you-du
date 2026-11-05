using System.Linq;

public static partial class AlchemyRecipeCatalog
{
    private static PillType Breakthrough(string id, int rank, string core, double yin, string purpose,
        params SampleRoute[] routes) => new(id, (rank == 7 ? "七品" : "九品") + AlchemyProductCatalog.Name(id),
            rank, core, routes.Select(r => r.Ingredients.Single(i => i.AssistantSlot == -1).HerbName).Distinct().ToArray(),
            yin, .68, .16, purpose, [purpose], routes, []);

    private static readonly PillType[] BreakthroughDefinitions =
    [
        Breakthrough("dingmai", 9, "固", 0, "木水平和承载；只在成功突破至云息四层后补入吐纳进度。",
            new SampleRoute("荒藤定脉方", "固性君药配合臣固、佐化、使通。", [S("荒藤结节", 2, -1), S("山蚕长丝", 1, 0), S("回泉珠", 2, 1), S("溪纹藻丝", 2, 3)]),
            new SampleRoute("松脂定脉方", "阳性固材配合阴性菌汁，调整木水性相。", [S("凝露松脂", 2, -1), S("山蚕长丝", 1, 0), S("月露菌汁", 2, 1), S("溪纹藻丝", 2, 3)])),
        Breakthrough("guiyuan_dingxi", 7, "藏", -.35, "偏阴木水归藏；只在成功突破至云息十层后缩短后续归元九息。",
            new SampleRoute("苦苔定息方", "七品丹需造诣68，九品藏材的量效按真实投料结算。", [S("苦苔根", 3, -1), S("山蚕长丝", 1, 0), S("灵泉露", 3, 1), S("溪纹藻丝", 3, 3)]),
            new SampleRoute("菌核定息方", "换用菌核藏性，佐化、使通调配木水与阴阳。", [S("雾心菌核", 3, -1), S("山蚕长丝", 1, 0), S("灵泉露", 3, 1), S("溪纹藻丝", 3, 3)]))
    ];
}
