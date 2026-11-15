using System;
using System.Collections.Generic;
using System.Linq;

public static partial class AlchemyRecipeCatalog
{
    private static PillType Expanded(string id, string core, double yin, string purpose, params SampleRoute[] routes) =>
        new(id, "九品" + AlchemyProductCatalog.Name(id), 9, core,
            routes.Select(r => r.Ingredients.Single(i => i.AssistantSlot == -1).HerbName).Distinct().ToArray(),
            yin, .68, .16, purpose, [purpose], routes, []);

    private static readonly PillType[] ExpansionDefinitions =
    [
        Expanded("ninggang", "护", .15, "土性承载护持，成品用于抵挡伤害。",
            new SampleRoute("甲片泉珠方", "护性君药配合佐化与使通。", [S("玄甲薄片", 2, -1), S("石蜂蜡", 1, 0), S("回泉珠", 2, 1), S("风络藤丝", 3, 3)]),
            new SampleRoute("甲片清音方", "增量君药，以明性压低夹带。", [S("玄甲薄片", 3, -1), S("石蜂蜡", 2, 0), S("清音玉片", 3, 1), S("风络藤丝", 3, 3)])),
        Expanded("jinji", "固", .35, "金土稳定形体，成品短时提高物理防御。",
            new SampleRoute("玄铁金土方", "固性核心配合金土与液质辅药。", [S("玄铁", 2, -1), S("盐纹石膏", 2, 0), S("月露菌汁", 1, 1), S("风窍晶屑", 1, 3)]),
            new SampleRoute("铜髓金肌方", "换用铜髓君药，调整液质与使位用量。", [S("铜髓矿", 2, -1), S("盐纹石膏", 1, 0), S("月露菌汁", 2, 1), S("风窍晶屑", 3, 3)])),
        Expanded("qingshen", "行", -.25, "略阴木金配伍，成品给予身法。",
            new SampleRoute("青羽轻身方", "行性君药配合稳形、提纯与通路。", [S("青羽翎", 1, -1), S("山蚕长丝", 1, 0), S("清音玉片", 2, 1), S("风纹石英", 2, 3)]),
            new SampleRoute("乌羽轻身方", "换用较高品行性君药，加强提纯。", [S("乌羽片", 1, -1), S("山蚕长丝", 1, 0), S("清音玉片", 3, 1), S("风纹石英", 1, 3)])),
        Expanded("xusheng", "生", -.55, "偏阴木土的生机收敛，区别平和回春。",
            new SampleRoute("云菌月露方", "生性君药与阴性化材配伍。", [S("云雾菌", 2, -1), S("石蜂蜡", 1, 0), S("月露菌汁", 1, 1), S("雾铃花", 1, 3)]),
            new SampleRoute("云菌清音方", "少量君药，改用明性降低夹带。", [S("云雾菌", 1, -1), S("石蜂蜡", 1, 0), S("清音玉片", 2, 1), S("雾铃花", 2, 3)]),
            new SampleRoute("赤核平衡方", "原回春样方按真实配伍归续生；第二佐位要求造诣68。", [S("赤瓤木核", 1, -1), S("伏地兽筋", 3, 3), S("凝霜叶", 2, 1), S("灵泉露", 1, 2), S("雾铃花", 1, 0)])),
        Expanded("jiegu", "破", .35, "偏阳土木破瘀，成品处理外伤。",
            new SampleRoute("赤纹接骨方", "破性君药与土木水辅药配伍。", [S("赤纹燧石", 2, -1), S("盐纹石膏", 1, 0), S("回泉珠", 3, 1), S("风络藤丝", 3, 3)]),
            new SampleRoute("裂甲接骨方", "换用金性破材，重看五行与阴阳。", [S("裂甲石", 2, -1), S("盐纹石膏", 1, 0), S("回泉珠", 3, 1), S("风络藤丝", 3, 3)])),
        Expanded("hezhong", "化", -.30, "偏阴水性转换，成品处理内伤。",
            new SampleRoute("月露和中方", "化性君药配合稳形、提纯与通路。", [S("月露菌汁", 1, -1), S("凝露松脂", 1, 0), S("清音玉片", 1, 1), S("溪纹藻丝", 2, 3)]),
            new SampleRoute("月露增量方", "同一君药增加剂量，并相应增加提纯。", [S("月露菌汁", 2, -1), S("凝露松脂", 1, 0), S("清音玉片", 2, 1), S("溪纹藻丝", 2, 3)])),
        Expanded("naling", "聚", 0, "平和木水汇集，成品补入当前层灵力。",
            new SampleRoute("灵藻纳灵方", "聚性君药配合木水与稳形提纯。", [S("灵泉藻", 2, -1), S("山蚕长丝", 1, 0), S("清音玉片", 3, 1), S("风络藤丝", 3, 3)]),
            new SampleRoute("泉珠纳灵方", "改用水性聚材，调整木性通路。", [S("回泉珠", 2, -1), S("山蚕长丝", 1, 0), S("清音玉片", 2, 1), S("风络藤丝", 3, 3)]),
            new SampleRoute("青芽明净方", "青芽生聚双核心按真实配伍归纳灵，不按旧丹方名称锁回春。", [S("青髓芽", 2, -1), S("山蚕长丝", 1, 0), S("清音玉片", 2, 1), S("溪纹藻丝", 2, 3)])),
        Expanded("fanyuan", "聚", .35, "偏阳木火土汇集，成品恢复大世界精力。",
            new SampleRoute("焰炭清音方", "聚性君药配合明性提纯与木性通路。", [S("地脉焰炭", 2, -1), S("山蚕长丝", 1, 0), S("清音玉片", 2, 1), S("溪纹藻丝", 2, 3)]),
            new SampleRoute("焰炭泉珠方", "改由佐化转残，调整通路剂量。", [S("地脉焰炭", 2, -1), S("山蚕长丝", 1, 0), S("回泉珠", 2, 1), S("溪纹藻丝", 1, 3)]))
    ];

    public static Dictionary<string, double> ElementTarget(string id)
    {
        double[] weights = id switch
        {
            "huichun" => [.50, 0, .10, .05, .35], "tongmai" => [.20, .10, .20, 0, .50],
            "ninggang" => [.20, 0, .65, 0, .15], "jinji" => [.10, 0, .40, .40, .10],
            "qingshen" => [.55, 0, 0, .30, .15], "xusheng" => [.60, 0, .20, 0, .20],
            "jiegu" => [.30, .20, .40, 0, .10], "hezhong" => [.30, 0, .10, 0, .60],
            "naling" => [.515, 0, 0, 0, .485], "fanyuan" => [.30, .30, .20, 0, .20],
            "dingmai" or "guiyuan_dingxi" => [.50, 0, 0, 0, .50],
            _ => [.30, .05, .25, .10, .30]
        };
        return new[] { "木", "火", "土", "金", "水" }.Select((e, i) => (e, weights[i])).ToDictionary(p => p.e, p => p.Item2, StringComparer.Ordinal);
    }
    public static string Anchor(string id) => id switch
    {
        "huichun" => "木", "tongmai" => "水", "jingxin" => "土",
        _ => ElementTarget(id).OrderByDescending(p => p.Value).First().Key
    };
}
