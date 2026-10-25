using Godot;
using System;
using System.Text.Json;

/// <summary>新成器固定快照接入已有唯一物品行囊。此层不装备、不结算、不重新炼制。</summary>
public static class ForgedArtifactInventory
{
    public static bool IsForged(ArtifactInstanceData item) => !string.IsNullOrWhiteSpace(item.forged_snapshot_json);

    // 从快照派生展示路径，旧存档也能使用新图标，不改写器物数据。
    public static string IconPath(ArtifactInstanceData item) => TryRead(item, out var snapshot)
        ? ForgedArtifactPresentation.IconPath(snapshot) : item.form?.icon_path ?? "";

    public static ArtifactInstanceData Create(ProcessForgingTrial.Artifact snapshot)
    {
        Validate(snapshot);
        return new ArtifactInstanceData
        {
            instance_id = snapshot.InstanceId, artifact_name = snapshot.Name, artifact_tier = snapshot.Tier,
            ritual_mode = "",
            form = new ArtifactFormData { form_id = "forged-snapshot", form_name = snapshot.Shape,
                icon_path = ForgedArtifactPresentation.IconPath(snapshot) },
            forged_snapshot_json = JsonSerializer.Serialize(snapshot)
        };
    }

    public static bool TryRead(ArtifactInstanceData item, out ProcessForgingTrial.Artifact snapshot)
    {
        snapshot = null!;
        if (!IsForged(item)) return false;
        try
        {
            var result = JsonSerializer.Deserialize<ProcessForgingTrial.Artifact>(item.forged_snapshot_json);
            if (result is null || result.InstanceId != item.instance_id) return false;
            Validate(result); snapshot = result; return true;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NotSupportedException) { return false; }
    }

    public static HoverInfoManager.HoverInfoContent Hover(ArtifactInstanceData item)
    {
        if (!TryRead(item, out var snapshot))
            return new(item.artifact_name, "器物信息暂时无法读取。", Subtitle: "器物");
        var content = ForgedArtifactPresentation.Hover(snapshot);
        // 快照可能来自不同生产入口，行囊不将所有物品误称为独立试炼产物。
        return content with { Body = string.Join("\n\n", System.Linq.Enumerable.Select(
            ForgedArtifactPresentation.Sections(snapshot), section => section.Title + "\n" + section.Body)) };
    }

    private static void Validate(ProcessForgingTrial.Artifact snapshot)
    {
        if (snapshot.SchemaVersion != 3 || string.IsNullOrWhiteSpace(snapshot.InstanceId) ||
            string.IsNullOrWhiteSpace(snapshot.Name) || string.IsNullOrWhiteSpace(snapshot.Category) ||
            snapshot.Tier is not ("凡器" or "法器") || !double.IsFinite(snapshot.Polarity) || snapshot.Polarity is < -3 or > 3 ||
            snapshot.Sharpness < 0 || snapshot.Effects is null || snapshot.BondContributions is null ||
            snapshot.Effects.Exists(effect => effect is null))
            throw new ArgumentException("成器快照格式不完整或版本暂不支持");
    }
}
