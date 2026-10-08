using Godot;
using Godot.Collections;

/// <summary>古风姓名生成服务，接口与原 GDScript 单例完全一致。</summary>
public partial class NameGenerator : Node
{
    private static readonly string[] Surnames = "李 王 张 刘 陈 杨 赵 黄 周 吴 徐 孙 胡 朱 高 林 何 郭 马 罗 梁 宋 郑 谢 韩 唐 冯 于 董 萧 程 曹 袁 邓 许 傅 沈 曾 彭 吕 苏 卢 蒋 蔡 贾 丁 魏 薛 叶 阎 余 潘 杜 戴 夏 钟 汪 田 任 姜 范 方 石 姚 谭 廖 邹 熊 金 陆 郝 孔 白 崔 康 毛 邱 秦 江 史 顾 侯 邵 孟 龙 万 段 雷 钱 汤 尹 黎 易 常 武 乔 贺 赖 龚 文 欧阳 司马 上官 诸葛 慕容 令狐 皇甫 宇文 长孙 尉迟 公孙 东方 独孤 南宫 夏侯 端木".Split(' ');
    private static readonly string[] MaleGiven = "毅 轩 昊 辰 铭 哲 凯 霖 浩 然 恒 博 文 武 渊 彦 峰 峻 泽 瑞 瑾 瑜 琮 璜 璟 琰 璋 琨 瑀 璠 景 曜 昭 晗 晟 皓 曦 昱 昕 昀 云 霆 霄 震 霖 霈 霆 霓 霁 霄 铮 钧 铎 锵 铿 锟 钰 锋 锐 钦 肃 谨 谦 诚 端 正 直 刚 信 义 逸 逍 遥 翔 飞 腾 驰 骋 骞 昂 柏 松 竹 鹤 麟 鹏 鲲 鸿 鹄 鸾 渊 澜 瀚 溟 沧 潇 洛 淇 渊 泊".Split(' ');
    private static readonly string[] FemaleGiven = "嫣 婉 婷 妍 倩 媛 婵 娟 娥 姣 姝 娉 娴 婳 婧 妤 妃 姬 娃 媚 兰 芷 蕙 萱 薇 蓉 荷 莲 菊 梅 芙 茜 茉 莉 蘅 蘩 芸 薰 蘼 蔷 琴 瑟 箫 笛 笙 筝 璎 珞 珊 瑚 瑾 瑜 瑶 琼 琳 琅 玫 瑰 璐 琪 月 雪 露 霜 冰 清 漪 涟 泓 滢 菲 若 如 初 灵 秀 慧 淑 贞 静 燕 莺 凤 雉 鹊 雁 鸽 鸥 雀 鹂 霞 霓 雯 霁 霏 霖 霄 雪 雾 岚".Split(' ');
    private static readonly string[] MaleMid = "子 之 天 少 长 伯 仲 叔 季 承 继 守 志 怀 思 慕 若 如".Split(' ');
    private static readonly string[] FemaleMid = "若 如 思 梦 晓 映 凝 含 蕴 宛 曼 妙 绮 素 紫 碧 翠 黛".Split(' ');

    public Dictionary generate(int gender = 0)
    {
        var surname = pick(Surnames);
        var given = gender == 1 && GD.Randi() % 100 < 80 ? build_female_name() : build_male_name();
        return new Dictionary { ["surname"] = surname, ["given"] = given, ["full"] = surname + given };
    }

    public string generate_full(int gender = 0) => (string)generate(gender)["full"];
    public string generate_single_given(int gender = 0) => pick(gender == 1 ? FemaleGiven : MaleGiven);
    public string generate_single_full(int gender = 0) => pick(Surnames) + generate_single_given(gender);

    public Array<string> generate_batch(int count, int gender = 0)
    {
        var result = new Array<string>();
        var seen = new HashSet<string>();
        for (var tries = 0; result.Count < count && tries < count * 20; tries++)
        {
            var name = generate_full(gender);
            if (seen.Add(name)) result.Add(name);
        }
        return result;
    }

    private static string build_male_name() => GD.Randi() % 100 < 20
        ? pick(MaleMid) + pick(MaleGiven)
        : build_two_char_name(MaleGiven);

    private static string build_female_name() => GD.Randi() % 100 < 20
        ? pick(FemaleMid) + pick(FemaleGiven)
        : build_two_char_name(FemaleGiven);

    private static string build_two_char_name(string[] pool)
    {
        var first = pick(pool);
        var second = pick(pool);
        while (second == first && pool.Length > 1) second = pick(pool);
        return first + second;
    }

    private static string pick(string[] pool) => pool[GD.Randi() % (uint)pool.Length];
}
