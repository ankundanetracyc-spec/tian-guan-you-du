using Godot;

public partial class EastWoodsDungeon : DungeonGraph
{
    public EastWoodsDungeon()
    {
        area_name = "东林地"; area_description = "镜月泽以东的密林。要真正探明这片林地，需要找到几处关键线索与核心区域。";
        Add(0, NodeType.ENTRY, 0, 0, "林地入口", "刚踏入东林地，脚下泥土还带着潮气，前方分出几条小径。");
        Add(1, NodeType.BATTLE, 1, 0, "灌木踪迹", "翻开的灌木丛里，有野猪刚刚冲过的痕迹。", "wild_boar"); Add(2, NodeType.EVENT, 1, 1, "猎人陷坑", "这里有一处废弃陷坑。", eventId:"hunter_trap", progress:1); Add(3, NodeType.REST, 1, 2, "溪边歇脚", "溪水清凉，适合短暂整备。");
        Add(5, NodeType.BATTLE, 2, 1, "兽穴", "前方是一处狼群频繁活动的洞穴。", "forest_wolf", 2); Add(6, NodeType.RESOURCE, 2, 2, "苦苔根坡", "坡地上散着几丛苦苔根。", resource:"mat_bitter_moss_root", amount:12);
        Add(7, NodeType.ELITE, 3, 0, "巨蜂巢", "树冠深处挂着巨大蜂巢。", "giant_bee", 3, progress:1); Add(9, NodeType.EVENT, 3, 2, "迷途修士", "低阶修士指认了安全通路。", eventId:"lost_cultivator"); Add(10, NodeType.EXIT, 4, 0, "林地边缘", "前方已经能看到林外的光亮。");
        foreach (var pair in new[]{(0,1),(0,2),(0,3),(1,5),(2,5),(2,6),(3,6),(5,7),(5,9),(6,9),(7,10),(9,10)}) connect_nodes(pair.Item1,pair.Item2);
    }
    private void Add(int id, NodeType type, int layer, int index, string title, string description, string enemy="", int count=1, string eventId="", string resource="", int amount=0, int progress=0) { var node = new DungeonNode(id,(int)type,layer,index){title=title,description=description,enemy_id=enemy,enemy_count=count,event_id=eventId,resource_type=resource,resource_amount=amount,exploration_progress_amount=progress}; add_node(node); }
}
