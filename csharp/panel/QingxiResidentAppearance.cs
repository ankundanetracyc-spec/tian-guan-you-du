/// <summary>序章村民的固定身份与外观数据；名单头像和脚本对话共用，不加入大世界 NPC 网络。</summary>
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

        var npcId = name switch
        {
            "李守拙" => "qingxi_li_shouzhuo",
            "周满仓" => "qingxi_zhou_mancang",
            "陈素秋" => "qingxi_chen_suqiu",
            "吴承安" => "qingxi_wu_chengan",
            _ => "",
        };
        var appearanceSeed = name switch
        {
            "李守拙" => 860118,
            "周满仓" => 860119,
            "陈素秋" => 860120,
            "吴承安" => 860121,
            _ => 0,
        };
        var age = name switch
        {
            "李守拙" => 54,
            "周满仓" => 43,
            "陈素秋" => 36,
            "吴承安" => 28,
            _ => 30,
        };

        var npc = new WorldNpcData
        {
            // 固定 ID 与种子让名单、对话及重新开局始终选中同一组立绘部件。
            npc_id = npcId,
            npc_name = name,
            formal_identity = identity,
            appearance_seed = appearanceSeed,
            gender = name == "陈素秋" ? 1 : 0,
            current_age = age,
            cultivation_realm = name == "李守拙" ? "云息六层" : "凡人",
            faction_id = "qingxi_village",
        };
        npc.ensure_appearance_identity();
        return npc;
    }
}
