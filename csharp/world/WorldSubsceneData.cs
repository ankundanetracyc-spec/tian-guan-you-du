using Godot;
using Godot.Collections;

/// <summary>地点内部的稳定空间标识；人物位置写在运行时数据中，不写回节点静态定义。</summary>
public partial class WorldSubsceneData : Resource
{
    [Export] public string subscene_id = "";
    [Export] public string subscene_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public bool unlocked = true;
}
