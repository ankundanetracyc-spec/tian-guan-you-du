using Godot;

/// <summary>百川区域名望计算与初见认知反馈。此类不写入或修改势力态度、NPC 私交。</summary>
public static class RegionalRenownSystem
{
    public const string BaiChuanRegionId = "bai_chuan";
    private const int YuxuBaseRenown = 50;

    public static float get_bai_chuan_yuxu_identity_multiplier(HumanData? data)
    {
        if (data?.sect_affiliation_id != "yuxu_xianzong") return 0f;
        return data.sect_current_rank switch
        {
            "杂役" or "杂役弟子" => 0.6f,
            "外门" or "外门弟子" => 1.0f,
            "内门" or "内门弟子" => 1.5f,
            "真传" or "真传弟子" => 3.0f,
            _ => 0f,
        };
    }

    public static int get_bai_chuan_yuxu_base(HumanData? data)
        => Mathf.RoundToInt(YuxuBaseRenown * get_bai_chuan_yuxu_identity_multiplier(data));

    public static long get_bai_chuan_deed_renown(HumanData? data)
        => data?.get_regional_renown_total(BaiChuanRegionId) ?? 0L;

    public static long get_bai_chuan_total(HumanData? data)
        => Math.Max(0L, get_bai_chuan_yuxu_base(data) + get_bai_chuan_deed_renown(data));

    public static string get_bai_chuan_band(long renown) => renown switch
    {
        < 30 => "名声未显",
        < 60 => "略有耳闻",
        < 100 => "小有名声",
        < 200 => "一方知名",
        < 400 => "名传百川",
        _ => "百川闻名"
    };

    public static string get_bai_chuan_profile_text(HumanData? data)
    {
        var baseRenown = get_bai_chuan_yuxu_base(data);
        var deeds = get_bai_chuan_deed_renown(data);
        var total = get_bai_chuan_total(data);
        return $"{total} · {get_bai_chuan_band(total)}\n宗门基础 {baseRenown}　公开事迹 {deeds}";
    }

    public static string get_bai_chuan_overview_breakdown_text(HumanData? data)
        => $"（玉虚基础声名 {YuxuBaseRenown} × 身份倍率 {get_bai_chuan_yuxu_identity_multiplier(data):0.0} + 公开事迹 {get_bai_chuan_deed_renown(data)}）";

    public static string get_bai_chuan_recognition_line(HumanData? data)
    {
        var total = get_bai_chuan_total(data);
        if (total < 30) return "你在百川尚无广为流传的名声；此人的看法仍取决于你们实际往来。";
        if (total < 100) return "你在百川略有声名；这不代表对方亲近你，态度仍由自身立场与彼此往来决定。";
        if (total < 200) return "你在百川已有一定可识别度；这份名声不代表势力立场或私人交情。";
        return "你的名声已传至百川多处；对方或能认出你，但其态度仍由自身立场与过往往来决定。";
    }
}
