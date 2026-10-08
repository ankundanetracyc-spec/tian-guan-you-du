using Godot;
using Godot.Collections;

/// <summary>一个百川区域首期 NPC 的静态数据包；运行时管理器复制其人物后再写入动态状态。</summary>
public partial class WorldNpcCatalog : Resource
{
    [Export] public string catalog_id = "";
    [Export] public Array<WorldNpcData> npcs = [];
}
