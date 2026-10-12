using Godot;
using System.Collections.Generic;

/// <summary>副本共用地貌素材；只决定表现，不决定耗时、通行或奖励。</summary>
public static class DungeonTerrainVisuals
{
    public enum Terrain { Grass, Woodland, Rock, Water, Bamboo, Wetland, CaveFloor }
    public const int VariantCount = 4;
    public const string AtlasPath = "res://textures/dungeon/terrain_ink_20261011/terrain_variants_v2.png";
    private static readonly Dictionary<(Terrain, int), AtlasTexture> Cache = new();
    private const string ExpansionPath = "res://textures/dungeon/terrain_ink_20261011/expansion_v1/";
    public static int CountFor(Terrain terrain) => terrain >= Terrain.Bamboo ? 2 : VariantCount;
    public static Texture2D SpringOverlay => GD.Load<Texture2D>(ExpansionPath + "spring_overlay_v1.png");

    public static int VariantFor(string dungeonId, string nodeId)
    {
        // Stable across sessions/builds, unlike string.GetHashCode or a frame-time RNG.
        uint hash = 2166136261;
        foreach (var character in dungeonId + "|" + nodeId)
            hash = unchecked((hash ^ character) * 16777619);
        return (int)(hash % VariantCount);
    }

    public static AtlasTexture Get(Terrain terrain, string dungeonId, string nodeId)
        => GetVariant(terrain, VariantFor(dungeonId, nodeId));

    public static AtlasTexture GetVariant(Terrain terrain, int variant)
    {
        var count = CountFor(terrain);
        variant = ((variant % count) + count) % count;
        var key = (terrain, variant);
        if (Cache.TryGetValue(key, out var texture)) return texture;
        if (terrain >= Terrain.Bamboo)
        {
            var name = terrain switch { Terrain.Bamboo => "bamboo", Terrain.Wetland => "wetland", _ => "cave_floor" };
            var image = GD.Load<Texture2D>(ExpansionPath + name + (variant == 0 ? "_a_v1.png" : "_b_v1.png"));
            texture = new AtlasTexture { Atlas = image, FilterClip = true, Region = new Rect2(Vector2.Zero, image.GetSize()) };
            Cache[key] = texture;
            return texture;
        }
        var atlas = GD.Load<Texture2D>(AtlasPath);
        var cell = atlas.GetSize() / 4;
        texture = new AtlasTexture
        {
            Atlas = atlas, FilterClip = true,
            Region = new Rect2((int)terrain * cell.X + 2, variant * cell.Y + 2, cell.X - 4, cell.Y - 4),
        };
        Cache[key] = texture;
        return texture;
    }
}
