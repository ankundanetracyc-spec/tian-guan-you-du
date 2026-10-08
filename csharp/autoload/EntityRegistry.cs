using Godot;
using Godot.Collections;

/// <summary>按 ID、类型与名称索引场景内实体，并在清理时重建索引以避免陈旧引用。</summary>
public partial class EntityRegistry : Node
{
    private readonly Array<GodotObject> all_entities = [];
    private readonly Dictionary entities_by_type = [];
    private readonly Dictionary entities_by_name = [];
    private readonly Dictionary entities_by_id = [];

    public void register(GodotObject entity)
    {
        if (!GodotObject.IsInstanceValid(entity)) return;
        all_entities.Add(entity);
        entities_by_id[get_string(entity, "unique_id")] = entity;
        add_to_bucket(entities_by_type, entity.Get("entity_type"), entity);
        add_to_bucket(entities_by_name, get_string(entity, "entity_name"), entity);
    }

    public void unregister(GodotObject entity)
    {
        if (!GodotObject.IsInstanceValid(entity)) return;
        all_entities.Remove(entity);
        entities_by_id.Remove(get_string(entity, "unique_id"));
        remove_from_bucket(entities_by_type, entity.Get("entity_type"), entity);
        remove_from_bucket(entities_by_name, get_string(entity, "entity_name"), entity);
    }

    public void cleanup()
    {
        var alive = new Array<GodotObject>();
        foreach (var entity in all_entities)
            if (GodotObject.IsInstanceValid(entity)) alive.Add(entity);
        all_entities.Clear();
        all_entities.AddRange(alive);
        rebuild_indexes();
    }

    public GodotObject? get_entity_by_id(string uid)
    {
        if (!entities_by_id.TryGetValue(uid, out var value)) return null;
        var entity = value.AsGodotObject();
        if (GodotObject.IsInstanceValid(entity)) return entity;
        cleanup();
        return null;
    }

    public Array<GodotObject> get_entities_of_type(int entity_type) => get_bucket(entities_by_type, entity_type);
    public Array<GodotObject> get_entities_named(string name) => get_bucket(entities_by_name, name);

    private void rebuild_indexes()
    {
        entities_by_type.Clear();
        entities_by_name.Clear();
        entities_by_id.Clear();
        foreach (var entity in all_entities)
        {
            entities_by_id[get_string(entity, "unique_id")] = entity;
            add_to_bucket(entities_by_type, entity.Get("entity_type"), entity);
            add_to_bucket(entities_by_name, get_string(entity, "entity_name"), entity);
        }
    }

    private static Array<GodotObject> get_bucket(Dictionary index, Variant key)
    {
        var result = new Array<GodotObject>();
        if (!index.TryGetValue(key, out var value) || value.VariantType != Variant.Type.Array) return result;
        foreach (var item in value.AsGodotArray())
        {
            var entity = item.AsGodotObject();
            if (GodotObject.IsInstanceValid(entity)) result.Add(entity);
        }
        return result;
    }

    private static void add_to_bucket(Dictionary index, Variant key, GodotObject entity)
    {
        var bucket = get_bucket(index, key);
        bucket.Add(entity);
        index[key] = bucket;
    }

    private static void remove_from_bucket(Dictionary index, Variant key, GodotObject entity)
    {
        if (!index.TryGetValue(key, out var value) || value.VariantType != Variant.Type.Array) return;
        var bucket = value.AsGodotArray();
        bucket.Remove(entity);
        if (bucket.Count == 0) index.Remove(key); else index[key] = bucket;
    }

    private static string get_string(GodotObject entity, string property) => entity.Get(property).AsString();
}
