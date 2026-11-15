using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Binary eight-way A* with packed, instance-owned managed storage. Returned
/// points are cell centres; exact endpoints, world bounds and smoothing belong
/// to the caller. No Godot calls occur inside the search loop.
/// </summary>
[GlobalClass]
public partial class PackedWorldNavigation : RefCounted
{
    private const double SqrtTwo = 1.4142135623730951;
    private const double DiagonalExtra = SqrtTwo - 1.0;
    private const double PriorityScale = 1_000_000_000.0;
    private const uint ClosedMask = 0x80000000u;
    private byte[] _solid = [];
    private int[] _parents = [];
    private double[] _scores = [];
    private uint[] _stamps = [];
    private uint _generation;
    private readonly PriorityQueue<int, (long Estimate, double ReverseCost, int Id)> _open = new(256);
    private int _heapCapacity = 256;
    private bool _configured;

    public int columns { get; private set; }
    public float cell_size { get; private set; }
    public float world_size { get; private set; }
    public int last_expanded_cells { get; private set; }
    public int last_heap_peak { get; private set; }
    public long last_search_usec { get; private set; }

    public bool configure(int newColumns, float newCellSize, Godot.Collections.Array rows, float newWorldSize)
    {
        _configured = false;
        columns = 0;
        _open.Clear();
        if (newColumns <= 0 || newColumns > 46340 || rows.Count != newColumns ||
            !float.IsFinite(newCellSize) || newCellSize <= 0 ||
            !float.IsFinite(newWorldSize) || newWorldSize <= 0 ||
            newWorldSize > (double)newColumns * newCellSize + 1e-6) return false;

        var count = newColumns * newColumns;
        var occupancy = new byte[count];
        for (var y = 0; y < newColumns; y++)
        {
            if (rows[y].VariantType != Variant.Type.String) return false;
            var row = rows[y].AsString();
            if (row.Length != newColumns) return false;
            var offset = y * newColumns;
            for (var x = 0; x < newColumns; x++)
            {
                var code = row[x];
                if (code is < '0' or > '2') return false;
                occupancy[offset + x] = code == '0' ? (byte)0 : (byte)1;
            }
        }

        _solid = occupancy;
        if (_parents.Length != count)
        {
            _parents = new int[count];
            _scores = new double[count];
            _stamps = new uint[count];
        }
        else Array.Clear(_stamps);
        _generation = 0;
        columns = newColumns;
        cell_size = newCellSize;
        world_size = newWorldSize;
        _configured = true;
        return true;
    }

    public bool is_in_boundsv(Vector2I point) =>
        _configured && (uint)point.X < (uint)columns && (uint)point.Y < (uint)columns;

    public bool is_point_solid(Vector2I point) =>
        !is_in_boundsv(point) || _solid[point.Y * columns + point.X] != 0;

    /// <summary>Array payload only; excludes instance and array headers.</summary>
    public long get_storage_bytes() =>
        _solid.LongLength + _parents.LongLength * 4 + _scores.LongLength * 8 +
        _stamps.LongLength * 4 + _heapCapacity * 32L;

    public Godot.Collections.Dictionary get_debug_stats() => new()
    {
        ["expanded_cells"] = last_expanded_cells,
        ["heap_peak_entries"] = last_heap_peak,
        ["search_usec"] = last_search_usec,
        ["packed_storage_bytes"] = get_storage_bytes(),
        ["generation"] = (long)_generation,
        ["managed_heap_bytes"] = GC.GetTotalMemory(false),
        ["managed_total_allocated_bytes"] = GC.GetTotalAllocatedBytes(false),
    };

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Vector2[] get_point_path(Vector2I from, Vector2I to)
    {
        var began = Stopwatch.GetTimestamp();
        last_expanded_cells = 0;
        last_heap_peak = 0;
        _open.Clear();
        if (is_point_solid(from) || is_point_solid(to))
        {
            FinishTiming(began);
            return [];
        }

        // High-bit stamps close a cell; epochs avoid clearing the whole map
        // between searches. Every record and heap belongs to this instance.
        if (++_generation >= ClosedMask)
        {
            Array.Clear(_stamps);
            _generation = 1;
        }
        var epoch = _generation;
        var closed = epoch | ClosedMask;
        var n = columns;
        var start = from.Y * n + from.X;
        var finish = to.Y * n + to.X;
        _stamps[start] = epoch;
        _parents[start] = start;
        _scores[start] = 0;
        HeapPush(start, 0, Octile(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y)));

        while (_open.Count > 0)
        {
            var current = HeapPop();
            if (_stamps[current] != epoch) continue;
            _stamps[current] = closed;
            last_expanded_cells++;
            if (current == finish)
            {
                var path = Reconstruct(start, finish);
                FinishTiming(began);
                return path;
            }
            var x = current % n;
            var y = current / n;
            var cost = _scores[current];
            var minY = Math.Max(0, y - 1);
            var maxY = Math.Min(n - 1, y + 1);
            var minX = Math.Max(0, x - 1);
            var maxX = Math.Min(n - 1, x + 1);
            for (var nextY = minY; nextY <= maxY; nextY++)
            {
                var row = nextY * n;
                for (var nextX = minX; nextX <= maxX; nextX++)
                {
                    if (nextX == x && nextY == y) continue;
                    var next = row + nextX;
                    if (_solid[next] != 0 || _stamps[next] == closed) continue;
                    var diagonal = nextX != x && nextY != y;
                    if (diagonal && (_solid[y * n + nextX] != 0 || _solid[row + x] != 0)) continue;
                    var nextCost = cost + (diagonal ? SqrtTwo : 1);
                    if (_stamps[next] == epoch && nextCost >= _scores[next]) continue;
                    _stamps[next] = epoch;
                    _parents[next] = current;
                    _scores[next] = nextCost;
                    var heuristic = Octile(Math.Abs(nextX - to.X), Math.Abs(nextY - to.Y));
                    HeapPush(next, nextCost, nextCost + heuristic);
                }
            }
        }
        FinishTiming(began);
        return [];
    }

    private Vector2[] Reconstruct(int start, int finish)
    {
        var count = 1;
        for (var current = finish; current != start; current = _parents[current]) count++;
        var path = new Vector2[count];
        var at = finish;
        for (var i = count - 1; i >= 0; i--)
        {
            path[i] = new Vector2((at % columns + 0.5f) * cell_size, (at / columns + 0.5f) * cell_size);
            at = _parents[at];
        }
        return path;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FinishTiming(long began) =>
        last_search_usec = (long)((Stopwatch.GetTimestamp() - began) * (1_000_000.0 / Stopwatch.Frequency));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Octile(int dx, int dy) => Math.Max(dx, dy) + DiagonalExtra * Math.Min(dx, dy);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HeapPush(int id, double cost, double priority)
    {
        // Framework PriorityQueue uses an optimized four-way heap even when
        // the game assembly is built in Debug configuration.
        // Integer ranks coalesce only 1e-9 roundoff in equal octile totals.
        // The default tuple comparer keeps a strict order and stays entirely
        // inside the optimized framework heap, including Debug game builds.
        _open.Enqueue(id, ((long)Math.Round(priority * PriorityScale), -cost, id));
        last_heap_peak = Math.Max(last_heap_peak, _open.Count);
        if (_open.Count > _heapCapacity) _heapCapacity = _open.EnsureCapacity(0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int HeapPop() => _open.Dequeue();

}
