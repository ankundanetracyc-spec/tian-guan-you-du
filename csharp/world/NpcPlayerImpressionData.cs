using Godot;
using Godot.Collections;

/// <summary>单个 NPC 的局部观感及其证据；不是玩家的全局人格或好感数值。</summary>
public partial class NpcPlayerImpressionData : Resource
{
    [Export] public int last_processed_month = -1;
    [Export] public Array<NpcActionRecord> pending = [];
    // 只需保存两个不同月份的完成记录，不累加当天重复交谈的次数。
    [Export] public Array<NpcActionRecord> contact_evidence = [];
    [Export] public NpcActionRecord? spar_evidence;
    [Export] public NpcActionRecord? purchase_evidence;
}
