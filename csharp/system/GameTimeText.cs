/// <summary>游戏时长使用 360 日一年、24 小时一日；日期与持续时长分别格式化。</summary>
public static class GameTimeText
{
    public static string Duration(int hours)
    {
        var total = Math.Max(0, hours);
        var days = total / 24;
        var parts = new List<string>(3);
        if (days / 360 > 0) parts.Add($"{days / 360}年");
        if (days % 360 > 0) parts.Add($"{days % 360}日");
        if (total % 24 > 0 || parts.Count == 0) parts.Add($"{total % 24}小时");
        return string.Concat(parts);
    }

    public static string Calendar(int hours)
    {
        var total = Math.Max(0, hours);
        var days = total / 24;
        return $"{days / 360 + 1}年{days % 360 / 30 + 1}月{days % 30 + 1}日 {total % 24:00}时";
    }
}
