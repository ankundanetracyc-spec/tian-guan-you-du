using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>大世界状态记录的通用校验、到期与展示规则；不定义具体状态效果。</summary>
public static class WorldStatusRules
{
    public const long PermanentExpiry = -1;
    public const string SevereInjuryId = "severe_injury";
    public const float SevereInjuryBattleHealthCapRatio = 0.85f;

    public static bool add(Array<WorldStatusRecord> records, WorldStatusRecord? record, long currentGameHour)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.status_id) ||
            string.IsNullOrWhiteSpace(record.display_name) || record.stack_count <= 0 ||
            record.expires_at_total_game_hours < PermanentExpiry ||
            record.recovery_hours_required < 0 || record.recovery_hours_progress < 0 ||
            (record.recovery_hours_required > 0 && record.recovery_hours_progress >= record.recovery_hours_required))
            return false;

        if (record.expires_at_total_game_hours != PermanentExpiry &&
            (record.expires_at_total_game_hours <= currentGameHour ||
             (record.applied_at_total_game_hours >= 0 && record.expires_at_total_game_hours <= record.applied_at_total_game_hours)))
            return false;

        if (string.IsNullOrWhiteSpace(record.instance_id))
            record.instance_id = Guid.NewGuid().ToString("N");
        if (records.Any(existing => existing is not null && existing.instance_id == record.instance_id))
            return false;

        if (record.applied_at_total_game_hours < 0)
            record.applied_at_total_game_hours = currentGameHour;
        records.Add(record);
        return true;
    }

    public static bool remove(Array<WorldStatusRecord> records, string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        for (var i = records.Count - 1; i >= 0; i--)
        {
            if (records[i] is not { } record || record.instance_id != instanceId) continue;
            records.RemoveAt(i);
            return true;
        }
        return false;
    }

    public static int expire_due(Array<WorldStatusRecord> records, long currentGameHour)
    {
        var removed = 0;
        for (var i = records.Count - 1; i >= 0; i--)
        {
            if (records[i] is not { } record) continue;
            var timeExpired = record.expires_at_total_game_hours != PermanentExpiry &&
                record.expires_at_total_game_hours <= currentGameHour;
            var recoveryComplete = record.recovery_hours_required > 0 &&
                record.recovery_hours_progress >= record.recovery_hours_required;
            if (!timeExpired && !recoveryComplete) continue;
            records.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    /// <summary>只累计玩家明确选择的休息行动，不把旅行、闭关或其他时间推进视作休养。</summary>
    public static int advance_rest_hours(Array<WorldStatusRecord> records, int restHours)
    {
        if (restHours <= 0) return 0;
        var changed = 0;
        for (var i = records.Count - 1; i >= 0; i--)
        {
            if (records[i] is not { recovery_hours_required: > 0 } record) continue;
            record.recovery_hours_progress = Math.Min(record.recovery_hours_required,
                Math.Max(0, record.recovery_hours_progress) + restHours);
            changed++;
            if (record.recovery_hours_progress >= record.recovery_hours_required)
                records.RemoveAt(i);
        }
        return changed;
    }

    public static List<WorldStatusRecord> active_records(Array<WorldStatusRecord>? records, long currentGameHour)
    {
        if (records is null) return [];
        return records
            .Where(record => record is not null && !string.IsNullOrWhiteSpace(record.status_id) &&
                !string.IsNullOrWhiteSpace(record.display_name) && record.stack_count > 0 &&
                (record.recovery_hours_required <= 0 || record.recovery_hours_progress < record.recovery_hours_required) &&
                (record.expires_at_total_game_hours == PermanentExpiry || record.expires_at_total_game_hours > currentGameHour))
            .ToList();
    }

    public static string format_recovery_days(WorldStatusRecord record)
    {
        if (record.recovery_hours_required <= 0) return "";
        static string Days(int hours) => (Math.Max(0, hours) / 24f).ToString("0.#");
        return $"休养 {Days(record.recovery_hours_progress)}/{Days(record.recovery_hours_required)}日";
    }

    /// <summary>HUD 只展示最需留意的一项；完整状态与来源在人物概览中查看。</summary>
    public static string format_hud_line(Array<WorldStatusRecord>? records, long currentGameHour)
    {
        var active = active_records(records, currentGameHour)
            .OrderBy(record => record.status_id == SevereInjuryId ? 0 : 1)
            .ThenBy(record => record.applied_at_total_game_hours)
            .ToList();
        if (active.Count == 0) return "";
        var primary = active[0];
        var summary = primary.status_id == SevereInjuryId && primary.recovery_hours_required > 0
            ? $"{primary.display_name} · {format_recovery_days(primary)}"
            : primary.stack_count > 1 ? $"{primary.display_name} ×{primary.stack_count}" : primary.display_name;
        if (active.Count > 1) summary += $"，另有 {active.Count - 1} 项";
        return $"状态　{summary}";
    }

    public static string format_summary(Array<WorldStatusRecord>? records, long currentGameHour)
    {
        var active = active_records(records, currentGameHour);
        if (active.Count == 0) return "暂无特殊状态";
        return string.Join("、", active.Select(record =>
        {
            var name = record.stack_count > 1 ? $"{record.display_name} ×{record.stack_count}" : record.display_name;
            if (record.recovery_hours_required > 0)
                return $"{name}（{format_recovery_days(record)}）";
            return record.expires_at_total_game_hours == PermanentExpiry
                ? name
                : $"{name}（尚余 {record.expires_at_total_game_hours - currentGameHour} 时）";
        }));
    }

    public static string format_details(Array<WorldStatusRecord>? records, long currentGameHour)
    {
        var active = active_records(records, currentGameHour);
        if (active.Count == 0) return "暂无特殊状态。";
        return string.Join("\n\n", active.Select(record =>
        {
            var heading = record.stack_count > 1 ? $"{record.display_name} ×{record.stack_count}" : record.display_name;
            var lines = new List<string> { heading };
            if (!string.IsNullOrWhiteSpace(record.description)) lines.Add(record.description.Trim());
            if (!string.IsNullOrWhiteSpace(record.source_name)) lines.Add($"缘起：{record.source_name.Trim()}");
            if (record.recovery_hours_required > 0)
            {
                var remaining = Math.Max(0, record.recovery_hours_required - record.recovery_hours_progress);
                lines.Add($"{format_recovery_days(record)}；尚需休息 {remaining / 24f:0.#} 日后痊愈");
                if (!string.IsNullOrWhiteSpace(record.recovery_realm)) lines.Add($"施加时境界：{record.recovery_realm}");
            }
            else if (record.expires_at_total_game_hours != PermanentExpiry)
                lines.Add($"尚余时日：{record.expires_at_total_game_hours - currentGameHour} 个游戏时");
            return string.Join("\n", lines);
        }));
    }

    public static int severe_injury_recovery_hours_for_realm(string realm) => realm switch
    {
        var value when value.StartsWith("云息", StringComparison.Ordinal) => 5 * 24,
        var value when value.StartsWith("玄基", StringComparison.Ordinal) => 15 * 24,
        var value when value.StartsWith("金丹", StringComparison.Ordinal) => 75 * 24,
        _ => 0
    };
}
