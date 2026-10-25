using Godot;
using System;
using System.Collections.Generic;

/// <summary>Maps container materials to visual container definitions; add future materials here without changing pill rules.</summary>
public static class PillContainerCatalog
{
    public const string Porcelain = "porcelain";
    public const string PorcelainDefinitionPath = "res://resources/entities/items/alchemy/empty_porcelain_bottle.tres";

    private static readonly Dictionary<string, string> ContainerDefinitionPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        [Porcelain] = PorcelainDefinitionPath,
    };

    private static readonly Dictionary<string, PillContainerItemData?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string DefinitionPath(string? materialId) =>
        string.IsNullOrWhiteSpace(materialId) ? "" : ContainerDefinitionPaths.GetValueOrDefault(materialId, "");

    public static PillContainerItemData? LoadContainerDefinition(string? materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId)) return null;
        if (Cache.TryGetValue(materialId, out var cached)) return cached;
        var path = DefinitionPath(materialId);
        var definition = string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path)
            ? null
            : ResourceLoader.Load<PillContainerItemData>(path);
        Cache[materialId] = definition;
        return definition;
    }

}
