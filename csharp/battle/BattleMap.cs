using Godot;
using Godot.Collections;

/// <summary>Battle hex-grid lookup and occupancy service.</summary>
public partial class BattleMap : Node2D
{
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
    public bool is_cell_occupied(Vector2I cell, GodotObject? ignore = null) => _cellUnits.TryGetValue(cell, out var value) && GodotObject.IsInstanceValid(value.AsGodotObject()) && value.AsGodotObject() != ignore;
    public GodotObject? get_unit_at_cell(Vector2I cell) => _cellUnits.TryGetValue(cell, out var value) && GodotObject.IsInstanceValid(value.AsGodotObject()) ? value.AsGodotObject() : null;
    public void clear_occupancy() { _unitCells.Clear(); _cellUnits.Clear(); }
    public void register_unit(GodotObject unit, Vector2I cell) { _unitCells[unit] = cell; _cellUnits[cell] = unit; }
    public void move_unit(GodotObject unit, Vector2I cell) { if (_unitCells.TryGetValue(unit, out var old)) _cellUnits.Remove(old); register_unit(unit, cell); }
    public Variant get_unit_cell(GodotObject unit) => _unitCells.TryGetValue(unit, out var cell) ? cell : default;
    public Array<Vector2I> get_aoe_cells(Vector2I center, int range) { var result = new Array<Vector2I>(); foreach (var cell in _cells) if (!spatial_combat_enabled || hex_distance(center, cell) <= range) result.Add(cell); return result; }
}
