using Godot;

/// <summary>青溪里后山首次引导。前五段每次随机生成，第六、第七段为固定剧情节点。</summary>
public partial class BackHillTrialDungeon : DungeonGraph
{
    private readonly record struct RandomNode(NodeType Type, string Title, string Description, string Enemy = "", string EventId = "", string Resource = "", int Amount = 0);
    private static readonly RandomNode[] RandomNodes =
    [
        new(NodeType.BATTLE, "灌木异动", "灌木深处传来急促的踩踏声，有东西正循着山路靠近。", "wild_boar"),
        new(NodeType.BATTLE, "林影低吼", "密叶之后传来低吼，来者显然不愿让出山路。", "forest_wolf"),
        new(NodeType.BATTLE, "湿石伏影", "潮湿的石缝间有什么鼓动了一下，腥气扑面而来。", "giant_toad"),
        new(NodeType.BATTLE, "枯叶游痕", "枯叶无风自移，一道细长的影子从石根间掠过。", "poison_snake"),
        new(NodeType.RESOURCE, "苦苔根", "背阴处的石缝间长着几丛苦苔根。", Resource: "mat_bitter_moss_root", Amount: 1),
        new(NodeType.REST, "溪边歇脚", "溪水清冽，可以洗去尘土、稍作歇息。"),
        new(NodeType.EVENT, "旧猎径", "石缝里留着褪色的猎户刻痕，山路的尽头藏在林影之后。", EventId: "back_hill_path")
    ];

    public BackHillTrialDungeon()
    {
        area_name = "后山外缘";
        area_description = "李守拙带你走一趟后山外围。前路的遭遇各不相同，越往上，山林越显得不安静。";
        Add(0, NodeType.ENTRY, 0, 0, "山脚石阶", "从青溪里沿石阶上山，前路没入疏林。");
        var previous = new List<int> { 0 };
        var nextId = 1;
        for (var layer = 1; layer <= 5; layer++)
        {
            var current = new List<int>();
            for (var index = 0; index < 2; index++)
            {
                var random = RandomNodes[(int)GD.RandRange(0, RandomNodes.Length - 1)];
                Add(nextId, random.Type, layer, index, random.Title, random.Description, random.Enemy, eventId: random.EventId, resource: random.Resource, amount: random.Amount);
                foreach (var parent in previous) connect_nodes(parent, nextId);
                current.Add(nextId++);
            }
            previous = current;
        }
        var spring = nextId++;
        Add(spring, NodeType.RESOURCE, 6, 0, "灵泉坳", "山坳中央有一汪清泉。玄鉴微微发热，指向岩缝边的一丛灵泉藻。", resource: "mat_spirit_spring_algae", amount: 2);
        foreach (var parent in previous) connect_nodes(parent, spring);
        var trace = nextId++;
        Add(trace, NodeType.EVENT, 7, 0, "半山腰爪痕", "岩石上留着一道深重爪痕，前方的山势愈发陡峭。", eventId: "back_hill_high_beast");
        connect_nodes(spring, trace);
        var exit = nextId;
        Add(exit, NodeType.EXIT, 7, 1, "回村小径", "李守拙示意原路返回，沿山坳小径便能回到青溪里。");
        connect_nodes(trace, exit);
    }
    private void Add(int id, NodeType type, int layer, int index, string title, string description, string enemy = "", int count = 1, string eventId = "", string resource = "", int amount = 0) => add_node(new DungeonNode(id, (int)type, layer, index) { title = title, description = description, enemy_id = enemy, enemy_count = count, event_id = eventId, resource_type = resource, resource_amount = amount });
}
