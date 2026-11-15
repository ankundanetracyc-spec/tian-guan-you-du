using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>Battle hex-grid lookup and occupancy service.</summary>
public partial class BattleMap : Node2D
{
    private static readonly Vector2I[] AxialDirections =
    [
        new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)
    ];
    [Export] public int map_radius = 5;
    [Export] public bool spatial_combat_enabled;
    [Export] public float hex_size = 80;
    [Export] public float hex_aspect_ratio = 1;
    private readonly Array<Vector2I> _cells = [];
    private readonly Dictionary _centers = [], _unitCells = [], _cellUnits = [];

    public override void _Ready() => generate_grid();
    public void generate_grid()
    {
        _cells.Clear(); _centers.Clear();
        for (var q = -map_radius; q <= map_radius; q++) for (var r = Mathf.Max(-map_radius, -q - map_radius); r <= Mathf.Min(map_radius, -q + map_radius); r++)
        { var cell = new Vector2I(q, r); _cells.Add(cell); _centers[cell] = ToGlobal(axial_to_local(cell)); }
    }
    public Vector2 axial_to_local(Vector2I cell) => new(hex_size * 1.5f * cell.X * hex_aspect_ratio, hex_size * Mathf.Sqrt(3) * (cell.Y + cell.X * .5f));
    public Vector2 get_cell_center(Vector2I cell) => _centers.TryGetValue(cell, out var value) ? value.AsVector2() : Vector2.Zero;
    public Vector2I world_to_cell(Vector2 position)
    {
        var p = ToLocal(position); var x = p.X / hex_aspect_ratio; var q = 2f * x / 3f / hex_size; var r = (-x / 3f + Mathf.Sqrt(3) * p.Y / 3f) / hex_size;
        var s = -q - r; var rq = Mathf.RoundToInt(q); var rr = Mathf.RoundToInt(r); var rs = Mathf.RoundToInt(s);
        if (Mathf.Abs(rq - q) > Mathf.Abs(rr - r) && Mathf.Abs(rq - q) > Mathf.Abs(rs - s)) rq = -rr - rs; else if (Mathf.Abs(rr - r) > Mathf.Abs(rs - s)) rr = -rq - rs;
        return new Vector2I(rq, rr);
    }
    public int hex_distance(Vector2I a, Vector2I b) { var q = a.X - b.X; var r = a.Y - b.Y; return (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2; }
    public bool contains_cell(Vector2I cell) => _cells.Contains(cell);
    private static bool BlocksCell(Variant value)
    {
        if (value.VariantType != Variant.Type.Object) return false;
        var unit = value.AsGodotObject();
        if (!GodotObject.IsInstanceValid(unit)) return false;
        if (unit is ArtifactBody body) return body.current_health > 0 && body.DepartureReason.Length == 0;
        if (unit is Creature creature) return creature.current_health > 0;
        return true;
    }
    public bool is_cell_occupied(Vector2I cell, GodotObject? ignore = null) =>
        _cellUnits.TryGetValue(cell, out var value) && BlocksCell(value) && value.AsGodotObject() != ignore;
    public GodotObject? get_unit_at_cell(Vector2I cell) => _cellUnits.TryGetValue(cell, out var value) && GodotObject.IsInstanceValid(value.AsGodotObject()) ? value.AsGodotObject() : null;
    public void clear_occupancy() { _unitCells.Clear(); _cellUnits.Clear(); }
    public void register_unit(GodotObject unit, Vector2I cell) { _unitCells[unit] = cell; _cellUnits[cell] = unit; }
    public void move_unit(GodotObject unit, Vector2I cell) { if (_unitCells.TryGetValue(unit, out var old)) _cellUnits.Remove(old); register_unit(unit, cell); }
    public Variant get_unit_cell(GodotObject unit) => _unitCells.TryGetValue(unit, out var cell) ? cell : default;
    public bool try_get_unit_cell(GodotObject unit, out Vector2I cell)
    {
        if (_unitCells.TryGetValue(unit, out var value)) { cell = value.AsVector2I(); return true; }
        cell = default; return false;
    }
    public bool try_move_unit(GodotObject unit, Vector2I cell)
    {
        if (!contains_cell(cell) || !try_get_unit_cell(unit, out _) || is_cell_occupied(cell, unit)) return false;
        move_unit(unit, cell); return true;
    }
    /// <summary>Returns the shortest legal path, up to maxSteps, toward a chosen range from targetCell.</summary>
    public Array<Vector2I> find_path_toward_distance(GodotObject unit, Vector2I targetCell, int desiredDistance, int maxSteps)
    {
        var result = new Array<Vector2I>();
        if (!spatial_combat_enabled || maxSteps <= 0 || !contains_cell(targetCell) || !try_get_unit_cell(unit, out var start)) return result;
        var steps = new System.Collections.Generic.Dictionary<Vector2I, int> { [start] = 0 };
        var previous = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
        var queue = new Queue<Vector2I>(); queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (steps[current] >= maxSteps) continue;
            foreach (var direction in AxialDirections)
            {
                var next = current + direction;
                if (!contains_cell(next) || steps.ContainsKey(next) || is_cell_occupied(next, unit)) continue;
                steps[next] = steps[current] + 1; previous[next] = current; queue.Enqueue(next);
            }
        }
        var best = steps.Keys
            .Select(cell => (Cell: cell, Steps: steps[cell], Error: Mathf.Abs(hex_distance(cell, targetCell) - desiredDistance)))
            .OrderBy(item => item.Error).ThenBy(item => item.Steps).ThenBy(item => item.Cell.X).ThenBy(item => item.Cell.Y)
            .FirstOrDefault();
        if (best.Cell == start) return result;
        var reversed = new List<Vector2I>();
        var cursor = best.Cell;
        while (cursor != start && previous.TryGetValue(cursor, out var prior)) { reversed.Add(cursor); cursor = prior; }
        reversed.Reverse(); foreach (var cell in reversed) result.Add(cell);
        return result;
    }
    /// <summary>Returns up to distance empty cells in the nearest fixed hex direction away from attackerCell.</summary>
    public Array<Vector2I> get_knockback_path(Vector2I attackerCell, GodotObject target, int distance)
    {
        var result = new Array<Vector2I>();
        if (!spatial_combat_enabled || distance <= 0 || !try_get_unit_cell(target, out var current)) return result;
        var direction = AxialDirections
            .Select((offset, index) => (Offset: offset, Index: index, Distance: hex_distance(attackerCell, current + offset)))
            .OrderByDescending(item => item.Distance).ThenBy(item => item.Index).First().Offset;
        for (var step = 0; step < distance; step++)
        {
            var next = current + direction;
            if (!contains_cell(next) || is_cell_occupied(next, target)) break;
            result.Add(next); current = next;
        }
        return result;
    }
    public Array<Vector2I> get_aoe_cells(Vector2I center, int range) { var result = new Array<Vector2I>(); foreach (var cell in _cells) if (!spatial_combat_enabled || hex_distance(center, cell) <= range) result.Add(cell); return result; }
}
