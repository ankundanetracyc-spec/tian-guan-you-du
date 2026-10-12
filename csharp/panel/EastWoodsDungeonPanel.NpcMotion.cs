using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class EastWoodsDungeonPanel
{
    private sealed class NpcMotion
    {
        public required WorldNpcData Npc;
        public required Control Pawn;
        public readonly Queue<(string From, string To)> Steps = new();
        public Tween? Tween;
    }
    private NpcWorldManager? _motionManager;
    private readonly Dictionary<string, NpcMotion> _npcMotions = new(StringComparer.Ordinal);

    private void bind_npc_motion()
    {
        _motionManager = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (_motionManager is null) return;
        _motionManager.DungeonNodeStepped += queue_npc_step;
        _motionManager.world_npcs_changed += play_npc_steps;
        VisibilityChanged += () => { if (!IsVisibleInTree()) clear_npc_motion(false); };
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_motionManager))
        {
            _motionManager!.DungeonNodeStepped -= queue_npc_step;
            _motionManager.world_npcs_changed -= play_npc_steps;
        }
        clear_npc_motion(false);
    }

    private Control? npc_token(string node, string id) => _pawnLayers.TryGetValue(node, out var layer)
        ? layer.GetChildren().OfType<Control>().FirstOrDefault(p => p.HasMeta("npc_id") && p.GetMeta("npc_id").AsString() == id) : null;

    private void queue_npc_step(WorldNpcData npc, string from, string to)
    {
        if (_isTraveling || !IsVisibleInTree() || EastWoodsDungeonRules.get_node(from) is null || !EastWoodsDungeonRules.get_neighbors(from).Contains(to)) return;
        if (!is_node_visible(from) && !is_node_visible(to)) return;
        if (!_npcMotions.TryGetValue(npc.npc_id, out var motion))
        {
            var source = npc_token(from, npc.npc_id);
            var size = source?.Size ?? new Vector2(Mathf.Min(80, _cellSize * .6f) * .54f, Mathf.Min(80, _cellSize * .6f));
            var pawn = create_pawn_token(false, npc, size);
            pawn.Name = "NpcTravelPawn";
            pawn.PivotOffset = new Vector2(size.X * .5f, size.Y);
            pawn.Position = source is null ? node_center(EastWoodsDungeonRules.get_node(from)!) - size * .5f : pawn_map_position(source);
            _mapRoot!.AddChild(pawn);
            motion = new NpcMotion { Npc = npc, Pawn = pawn };
            _npcMotions.Add(npc.npc_id, motion);
            source?.Hide();
        }
        motion.Steps.Enqueue((from, to));
    }

    private void play_npc_steps()
    {
        if (!IsVisibleInTree() || _npcEncounterBusy || _isTraveling) return;
        foreach (var pair in _npcMotions.ToArray())
            if (!pair.Value.Npc.is_alive || pair.Value.Npc.dungeon_id != EastWoodsDungeonRules.DungeonId || _motionManager?.npcs.Contains(pair.Value.Npc) != true)
                remove_npc_motion(pair.Key);
        refresh();
        foreach (var motion in _npcMotions.Values.ToArray())
            if (motion.Tween is null) play_next_npc_step(motion);
    }

    private void play_next_npc_step(NpcMotion motion)
    {
        if (motion.Steps.Count == 0)
        {
            remove_npc_motion(motion.Npc.npc_id);
            refresh();
            return;
        }
        var (from, to) = motion.Steps.Dequeue();
        motion.Pawn.Visible = is_node_visible(from) && is_node_visible(to);
        var direction = EastWoodsDungeonRules.get_node(to)!.GridPosition - EastWoodsDungeonRules.get_node(from)!.GridPosition;
        if (direction.X != 0) _npcFacingLeft[motion.Npc.npc_id] = direction.X < 0;
        face_pawn(motion.Pawn, _npcFacingLeft.GetValueOrDefault(motion.Npc.npc_id));
        var start = motion.Pawn.Position;
        var half = motion.Pawn.Size * .5f;
        var a = node_center(EastWoodsDungeonRules.get_node(from)!) - half;
        var b = node_center(EastWoodsDungeonRules.get_node(to)!) - half;
        var destination = motion.Steps.Count == 0 ? npc_token(to, motion.Npc.npc_id) : null;
        var end = destination is null ? b : pawn_map_position(destination) + destination.Size * .5f - half;
        // Preserve every actual edge. Compress long-action playback, never advance simulation here.
        var duration = Math.Min(PawnStepSeconds * _playerDunSpeed / Mathf.Max(.01f, motion.Npc.dun_speed), 1.2 / (motion.Steps.Count + 1));
        var tween = CreateTween(); motion.Tween = tween;
        tween.TweenMethod(Callable.From<float>(t => {
            motion.Pawn.Visible = is_node_visible(from) && is_node_visible(to);
            apply_pawn_gait(motion.Pawn, start, a, b, end, t);
        }), 0f, 1f, duration);
        tween.TweenCallback(Callable.From(() => { motion.Tween = null; play_next_npc_step(motion); }));
    }

    private void remove_npc_motion(string id)
    {
        if (!_npcMotions.Remove(id, out var motion)) return;
        motion.Tween?.Kill(); motion.Pawn.Hide(); motion.Pawn.QueueFree();
        var destination = npc_token(motion.Npc.dungeon_node_id, motion.Npc.npc_id);
        if (destination is not null) face_pawn(destination, _npcFacingLeft.GetValueOrDefault(id));
    }

    private void clear_npc_motion(bool redraw = true)
    {
        if (_npcMotions.Count == 0) return;
        foreach (var id in _npcMotions.Keys.ToArray()) remove_npc_motion(id);
        if (redraw && IsInsideTree()) refresh();
    }

    private static void apply_pawn_gait(Control pawn, Vector2 start, Vector2 a, Vector2 b, Vector2 end, float t)
    {
        var position = t < .15f ? start.Lerp(a, t / .15f) : t > .85f ? b.Lerp(end, (t - .85f) / .15f) : a.Lerp(b, (t - .15f) / .70f);
        pawn.Position = position + new Vector2(0, -Mathf.Abs(Mathf.Sin(t * Mathf.Pi * 4)) * 3);
        pawn.Rotation = Mathf.Sin(t * Mathf.Pi * 4) * .075f;
    }
}
