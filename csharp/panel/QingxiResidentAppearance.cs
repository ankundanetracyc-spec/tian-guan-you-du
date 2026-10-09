/// <summary>序章村民的固定外观身份，名单头像与对话立绘共用，不加入大世界 NPC 网络。</summary>
public static class QingxiResidentAppearance
{
    public static WorldNpcData? Find(string name)
    {
        var identity = name switch
        {
            "李守拙" => "里正",
            "周满仓" => "掌柜",
            "陈素秋" => "田农",
            "吴承安" => "更夫",
            _ => "",
        };
        if (identity.Length == 0) return null;
        var npc = new WorldNpcData
        {
            // 沿用已有对话外观种子，换场景、重新开局均不重抽。
            npc_id = "dialogue_" + name,
            npc_name = name,
            formal_identity = identity,
            gender = name == "陈素秋" ? 1 : 0,
            current_age = name == "李守拙" ? 54 : 30,
            cultivation_realm = name == "李守拙" ? "云息六层" : "凡人",
        };
        npc.ensure_appearance_identity();
        return npc;
    }
}
