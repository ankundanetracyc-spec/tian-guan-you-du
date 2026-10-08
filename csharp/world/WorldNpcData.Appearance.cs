using Godot;

/// <summary>NPC 的立绘属性；未显式设定时按人物 ID 稳定派生随机性别与组合。</summary>
public partial class WorldNpcData : Resource
{
    [Export] public int gender = -1;
    [Export] public int appearance_seed;

    /// <summary>补齐旧 NPC 的外观身份，并确保性别和随机立绘由稳定人物 ID 决定。</summary>
    public void ensure_appearance_identity()
    {
        if (appearance_seed <= 0)
            appearance_seed = stable_seed(npc_id + "|" + npc_name);
        if (gender is not (0 or 1))
            gender = appearance_seed & 1;
    }

    private static int stable_seed(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value) { hash ^= character; hash *= 16777619; }
            int seed = (int)(hash & 0x7fffffff);
            return seed == 0 ? 1 : seed;
        }
    }
}
