using Godot;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// Capture on the scene thread, then Materialize on one worker. The snapshot contains
/// managed values only: never Resource, Variant, or a native Godot container. Capture
/// must finish before gameplay resumes; the resulting graph is immutable and can outlive it.
/// </summary>
public sealed class DetachedResourceSnapshot
{
    /// <summary>Main-thread-only optional cache. Old captures remain immutable after a record changes.</summary>
    public sealed class CaptureCache
    {
        private readonly ConditionalWeakTable<NpcActionRecord, ResourceValue> _records = new();
        public void Clear() => _records.Clear();
        internal ResourceValue CaptureRecord(NpcActionRecord record)
        {
            if (_records.TryGetValue(record, out var prior) && ActionMatches(record, prior.Values)) return prior;
            var node = new ResourceValue(GetSchema(typeof(NpcActionRecord)), ActionValues(record));
            _records.Remove(record);
            _records.Add(record, node);
            return node;
        }
    }

    internal sealed record MemberValue(string Name, Type Type, Func<Resource, object?> Get, Action<Resource, object?> Set);
    internal sealed record Schema(Type Type, Func<Resource> New, MemberValue[] Members);
    internal sealed class ResourceValue
    {
        private readonly Schema _schema;
        internal object?[] Values;
        internal ResourceValue(Schema schema, object?[] values) { _schema = schema; Values = values; }
        internal Resource New() => _schema.New();
        internal void Populate(Resource resource, Func<object?, Type, object?> read)
        {
            for (var index = 0; index < Values.Length; index++)
                _schema.Members[index].Set(resource, read(Values[index], _schema.Members[index].Type));
        }
    }
    private sealed record ExternalValue(string Path);
    private sealed record NameValue(string Text, bool IsNodePath);
    private sealed record VariantValue(object? Value);
    private sealed record ArrayValue(Type Type, object?[] Items, Type ElementType, bool Managed);
    private sealed record DictionaryValue(Type Type, Type KeyType, Type ValueType, KeyValuePair<object?, object?>[] Items);
    private static readonly ConcurrentDictionary<Type, Schema> Schemas = new();
    private static readonly ConcurrentDictionary<Type, bool> ExportSchemas = new();
    private static readonly ConcurrentDictionary<Type, Func<object, Variant>> VariantFactories = new();
    private static readonly string[] ActionNames =
    [
        "month_index", "date_label", "action_id", "action_name", "node_id", "subscene_id", "from_node_id", "to_node_id",
        "travel_days", "travel_days_remaining", "target_npc_id", "dungeon_incident_id", "reason", "outcome",
        "player_contact_completed", "resource_delta", "is_death_record"
    ];
    private readonly object _root;
    private DetachedResourceSnapshot(object root) => _root = root;

    public static DetachedResourceSnapshot Capture(Resource source, CaptureCache? cache = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var graph = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        return new DetachedResourceSnapshot(CaptureValue(source, graph, cache)!);
    }

    public Resource Materialize()
    {
        var graph = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        return (Resource)ReadValue(_root, typeof(Resource), graph)!;
    }

    /// <summary>Call during loading to compile reflected accessors before the first autosave.</summary>
    public static void WarmUp()
    {
        foreach (var type in typeof(DetachedResourceSnapshot).Assembly.GetTypes())
            if (!type.IsAbstract && typeof(Resource).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null &&
                (type.GetFields(BindingFlags.Public | BindingFlags.Instance).Any(field => field.IsDefined(typeof(ExportAttribute), true)) ||
                 type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property => property.IsDefined(typeof(ExportAttribute), true))))
                GetSchema(type);
    }

    private static Schema GetSchema(Type type) => Schemas.GetOrAdd(type, static type =>
    {
        var members = new List<MemberValue>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!field.IsDefined(typeof(ExportAttribute), true)) continue;
            if (field.IsInitOnly) throw new NotSupportedException("Readonly Export field: " + type.Name + "." + field.Name);
            var resource = Expression.Parameter(typeof(Resource));
            var value = Expression.Parameter(typeof(object));
            var access = Expression.Field(Expression.Convert(resource, type), field);
            members.Add(new MemberValue(field.Name, field.FieldType,
                Expression.Lambda<Func<Resource, object?>>(Expression.Convert(access, typeof(object)), resource).Compile(),
                Expression.Lambda<Action<Resource, object?>>(Expression.Assign(access, Expression.Convert(value, field.FieldType)), resource, value).Compile()));
        }
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.IsDefined(typeof(ExportAttribute), true)) continue;
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
                throw new NotSupportedException("Unreadable or unwritable Export property: " + type.Name + "." + property.Name);
            var resource = Expression.Parameter(typeof(Resource));
            var value = Expression.Parameter(typeof(object));
            var access = Expression.Property(Expression.Convert(resource, type), property);
            members.Add(new MemberValue(property.Name, property.PropertyType,
                Expression.Lambda<Func<Resource, object?>>(Expression.Convert(access, typeof(object)), resource).Compile(),
                Expression.Lambda<Action<Resource, object?>>(Expression.Assign(access, Expression.Convert(value, property.PropertyType)), resource, value).Compile()));
        }
        if (members.Count == 0 && type != typeof(Resource))
            throw new NotSupportedException("Embedded resource has no Export schema; save by external path or add explicit support: " + type.FullName);
        if (type == typeof(NpcActionRecord))
        {
            if (members.Count != ActionNames.Length || !members.Select(member => member.Name).ToHashSet(StringComparer.Ordinal).SetEquals(ActionNames))
                throw new NotSupportedException("NpcActionRecord Export schema changed; update its detached snapshot value comparison before saving.");
            members = ActionNames.Select(name => members.Single(member => member.Name == name)).ToList();
        }
        return new Schema(type, Expression.Lambda<Func<Resource>>(Expression.Convert(Expression.New(type), typeof(Resource))).Compile(), members.ToArray());
    });

    private static object? CaptureValue(object? value, Dictionary<object, object> graph, CaptureCache? cache)
    {
        if (value is null) return null;
        if (value is Variant variant) return new VariantValue(CaptureValue(variant.Obj, graph, cache));
        if (value is StringName name) return new NameValue(name.ToString(), false);
        if (value is NodePath path) return new NameValue(path.ToString(), true);
        if (value is Resource resource)
        {
            if (graph.TryGetValue(resource, out var existing)) return existing;
            // History records are owned mutable save data, including records restored
            // from subresources. Avoid a native ResourcePath lookup for every old record.
            if (cache is not null && resource is NpcActionRecord action && action.GetType() == typeof(NpcActionRecord))
            {
                var cached = cache.CaptureRecord(action);
                graph.Add(resource, cached);
                return cached;
            }
            var resourcePath = resource.ResourcePath;
            var customSchema = ExportSchemas.GetOrAdd(resource.GetType(), static type =>
                type.Assembly == typeof(DetachedResourceSnapshot).Assembly &&
                (type.GetFields(BindingFlags.Public | BindingFlags.Instance).Any(field => field.IsDefined(typeof(ExportAttribute), true)) ||
                 type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(property => property.IsDefined(typeof(ExportAttribute), true))));
            if (!customSchema && !string.IsNullOrEmpty(resourcePath) && !resourcePath.StartsWith("user://", StringComparison.Ordinal) && !resourcePath.Contains("::", StringComparison.Ordinal))
            {
                var external = new ExternalValue(resourcePath);
                graph.Add(resource, external);
                return external;
            }
            var schema = GetSchema(resource.GetType());
            if (schema.Members.Length == 0 && resource.GetScript().VariantType != Variant.Type.Nil)
                throw new NotSupportedException("Embedded script resource has no supported Export schema: " + resourcePath);
            var node = new ResourceValue(schema, new object?[schema.Members.Length]);
            graph.Add(resource, node);
            for (var index = 0; index < schema.Members.Length; index++)
                node.Values[index] = CaptureValue(schema.Members[index].Get(resource), graph, cache);
            return node;
        }
        if (value is GodotObject) throw new NotSupportedException("Scene object cannot enter a detached snapshot: " + value.GetType());
        var type = value.GetType();
        if (value is Godot.Collections.Dictionary dictionary)
        {
            var items = new KeyValuePair<object?, object?>[dictionary.Count];
            var index = 0;
            foreach (var entry in dictionary)
                items[index++] = new(CaptureValue(entry.Key.Obj, graph, cache), CaptureValue(entry.Value.Obj, graph, cache));
            return new DictionaryValue(type, typeof(Variant), typeof(Variant), items);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Godot.Collections.Dictionary<,>))
        {
            var args = type.GetGenericArguments();
            var entries = new List<KeyValuePair<object?, object?>>();
            var pairType = typeof(KeyValuePair<,>).MakeGenericType(args);
            var key = pairType.GetProperty("Key")!;
            var entryValue = pairType.GetProperty("Value")!;
            foreach (var entry in (IEnumerable)value)
                entries.Add(new(CaptureValue(key.GetValue(entry), graph, cache), CaptureValue(entryValue.GetValue(entry), graph, cache)));
            return new DictionaryValue(type, args[0], args[1], entries.ToArray());
        }
        if (value is Godot.Collections.Array array)
        {
            var items = new object?[array.Count];
            for (var index = 0; index < items.Length; index++) items[index] = CaptureValue(array[index].Obj, graph, cache);
            return new ArrayValue(type, items, typeof(Variant), false);
        }
        if (value is Godot.Collections.Array<NpcActionRecord> records)
        {
            // Read Count once; generic enumeration may query native Count for each step.
            var items = new object?[records.Count];
            for (var index = 0; index < items.Length; index++)
            {
                var entry = records[index];
                if (entry is null) continue;
                if (graph.TryGetValue(entry, out var previous)) items[index] = previous;
                else if (cache is not null && entry.GetType() == typeof(NpcActionRecord))
                {
                    var cached = cache.CaptureRecord(entry);
                    graph.Add(entry, cached);
                    items[index] = cached;
                }
                else items[index] = CaptureValue(entry, graph, cache);
            }
            return new ArrayValue(type, items, typeof(NpcActionRecord), false);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Godot.Collections.Array<>))
        {
            var items = new List<object?>();
            foreach (var entry in (IEnumerable)value) items.Add(CaptureValue(entry, graph, cache));
            return new ArrayValue(type, items.ToArray(), type.GetGenericArguments()[0], false);
        }
        if (value is System.Array managed)
        {
            var items = new object?[managed.Length];
            for (var index = 0; index < items.Length; index++) items[index] = CaptureValue(managed.GetValue(index), graph, cache);
            return new ArrayValue(type, items, type.GetElementType()!, true);
        }
        if (value is Callable or Signal or Rid) throw new NotSupportedException("Native handle cannot enter a detached snapshot: " + type);
        if (type.IsPrimitive || type.IsEnum || value is string or decimal or
            Vector2 or Vector2I or Vector3 or Vector3I or Vector4 or Vector4I or Rect2 or Rect2I or
            Transform2D or Transform3D or Basis or Quaternion or Projection or Plane or Aabb or Color) return value;
        throw new NotSupportedException("Unsupported snapshot value: " + type.FullName);
    }

    private static object? ReadValue(object? value, Type expected, Dictionary<object, object> graph)
    {
        if (value is VariantValue wrapped) return ToVariant(ReadValue(wrapped.Value, typeof(object), graph));
        if (value is null) return expected == typeof(Variant) ? (object)default(Variant) : null;
        object? result;
        switch (value)
        {
            case ResourceValue resource:
                if (!graph.TryGetValue(resource, out var instance))
                {
                    var rebuilt = resource.New();
                    graph.Add(resource, rebuilt);
                    resource.Populate(rebuilt, (item, type) => ReadValue(item, type, graph));
                    instance = rebuilt;
                }
                result = instance;
                break;
            case ExternalValue external:
                if (!graph.TryGetValue(external, out var loaded))
                {
                    loaded = ResourceLoader.Load(external.Path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep)
                        ?? throw new InvalidOperationException("Cannot load snapshot dependency: " + external.Path);
                    graph.Add(external, loaded);
                }
                result = loaded;
                break;
            case NameValue name:
                result = name.IsNodePath ? (object)new NodePath(name.Text) : new StringName(name.Text);
                break;
            case ArrayValue array:
                if (array.Managed)
                {
                    var managed = System.Array.CreateInstance(array.ElementType, array.Items.Length);
                    for (var index = 0; index < array.Items.Length; index++) managed.SetValue(ReadValue(array.Items[index], array.ElementType, graph), index);
                    result = managed;
                }
                else
                {
                    var native = Activator.CreateInstance(array.Type)!;
                    if (native is Godot.Collections.Array untyped)
                        foreach (var item in array.Items) untyped.Add(ToVariant(ReadValue(item, typeof(object), graph)));
                    else
                    {
                        var add = array.Type.GetMethod("Add", [array.ElementType])!;
                        foreach (var item in array.Items) add.Invoke(native, [ReadValue(item, array.ElementType, graph)]);
                    }
                    result = native;
                }
                break;
            case DictionaryValue dictionary:
                var dict = Activator.CreateInstance(dictionary.Type)!;
                if (dict is Godot.Collections.Dictionary untypedDict)
                    foreach (var item in dictionary.Items)
                        untypedDict.Add(ToVariant(ReadValue(item.Key, typeof(object), graph)), ToVariant(ReadValue(item.Value, typeof(object), graph)));
                else
                {
                    var add = dictionary.Type.GetMethod("Add", [dictionary.KeyType, dictionary.ValueType])!;
                    foreach (var item in dictionary.Items)
                        add.Invoke(dict, [ReadValue(item.Key, dictionary.KeyType, graph), ReadValue(item.Value, dictionary.ValueType, graph)]);
                }
                result = dict;
                break;
            default: result = value; break;
        }
        if (expected == typeof(Variant)) return ToVariant(result);
        return result;
    }

    private static Variant ToVariant(object? value)
    {
        if (value is null) return default;
        if (value is Variant variant) return variant;
        if (value is GodotObject resource) return Variant.From(resource);
        if (value.GetType().IsEnum) return Variant.From(Convert.ToInt64(value));
        return VariantFactories.GetOrAdd(value.GetType(), static type =>
            (Func<object, Variant>)typeof(DetachedResourceSnapshot).GetMethod(nameof(CreateVariant), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(type).CreateDelegate(typeof(Func<object, Variant>)))(value);
    }

    private static Variant CreateVariant<[MustBeVariant] T>(object value) => Variant.From((T)value);

    private static object?[] ActionValues(NpcActionRecord record) =>
    [
        record.month_index, record.date_label, record.action_id, record.action_name, record.node_id, record.subscene_id,
        record.from_node_id, record.to_node_id, record.travel_days, record.travel_days_remaining, record.target_npc_id,
        record.dungeon_incident_id, record.reason, record.outcome, record.player_contact_completed, record.resource_delta, record.is_death_record
    ];

    private static bool ActionMatches(NpcActionRecord record, object?[] values) =>
        record.month_index == (int)values[0]! && record.date_label == (string?)values[1] && record.action_id == (string?)values[2] &&
        record.action_name == (string?)values[3] && record.node_id == (string?)values[4] && record.subscene_id == (string?)values[5] &&
        record.from_node_id == (string?)values[6] && record.to_node_id == (string?)values[7] && record.travel_days == (int)values[8]! &&
        record.travel_days_remaining == (int)values[9]! && record.target_npc_id == (string?)values[10] && record.dungeon_incident_id == (string?)values[11] &&
        record.reason == (string?)values[12] && record.outcome == (string?)values[13] && record.player_contact_completed == (bool)values[14]! &&
        record.resource_delta == (int)values[15]! && record.is_death_record == (bool)values[16]!;
}
