using Godot;

/// <summary>Two-voice, event-driven combat foley. Cues follow resolved actions and never affect combat state.</summary>
public partial class BattleImpactAudio : Node
{
    private const int VoiceLimit = 2;
    private const string AudioRoot = "res://audio/combat_impact_20261105/";
    private static readonly string[][] Families =
    [
        ["whoosh_01.wav", "whoosh_02.wav"],
        ["light_01.wav", "light_02.wav"],
        ["heavy_01.wav", "heavy_02.wav"],
        ["guard_01.wav", "guard_02.wav"]
    ];
    private static readonly Dictionary<string, AudioStream> Streams = new(StringComparer.Ordinal);

    private readonly List<AudioStreamPlayer2D> _voices = [];
    private readonly int[] _roundRobin = new int[Families.Length];
    private readonly HashSet<(int Family, ulong Subject)> _playedThisFrame = [];
    private ulong _currentFrameBucket = ulong.MaxValue;

    public static void PlayWhoosh(GodotObject source)
    {
        if (source is not Node2D actor || !GodotObject.IsInstanceValid(actor)) return;
        GetOrCreate(actor)?.PlayCue(0, actor.GlobalPosition, actor.GetInstanceId(), -13f);
    }

    public static void PlayContact(GodotObject target, ActiveSkillData skill, bool fullyBlocked)
    {
        if (target is not Node2D actor || !GodotObject.IsInstanceValid(actor)) return;
        var family = fullyBlocked ? 3 : BattleInkCombatVfx.HitWeight(skill) >= 1.35f ? 2 : 1;
        var volume = family switch { 3 => -9f, 2 => -5f, _ => -7f };
        GetOrCreate(actor)?.PlayCue(family, actor.GlobalPosition, actor.GetInstanceId(), volume);
    }

    private static BattleImpactAudio? GetOrCreate(Node context)
    {
        if (!context.IsInsideTree()) return null;
        var tree = context.GetTree();
        var scene = tree.CurrentScene;
        if (scene is null) return null;
        var existing = tree.GetNodesInGroup("battle_impact_audio_router")
            .OfType<BattleImpactAudio>().FirstOrDefault(node => GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion());
        if (existing is not null) return existing;
        var router = new BattleImpactAudio { Name = nameof(BattleImpactAudio), ProcessMode = ProcessModeEnum.Always };
        scene.AddChild(router);
        router.AddToGroup("battle_impact_audio_router");
        return router;
    }

    public override void _Process(double delta)
    {
        var paused = GetTree().GetFirstNodeInGroup("battle_mgr") is BattleManager manager && manager.is_battle_paused();
        foreach (var voice in _voices.ToArray())
        {
            if (!GodotObject.IsInstanceValid(voice) || voice.IsQueuedForDeletion())
            {
                _voices.Remove(voice);
                continue;
            }
            if (voice.IsPlaying()) voice.StreamPaused = paused;
            else
            {
                voice.QueueFree();
                _voices.Remove(voice);
            }
        }
    }

    private void PlayCue(int family, Vector2 position, ulong subject, float volumeDb)
    {
        var bucket = Time.GetTicksMsec() / 16UL;
        if (bucket != _currentFrameBucket)
        {
            _currentFrameBucket = bucket;
            _playedThisFrame.Clear();
        }
        if (!_playedThisFrame.Add((family, subject))) return;

        _voices.RemoveAll(voice => !GodotObject.IsInstanceValid(voice) || voice.IsQueuedForDeletion() || !voice.IsPlaying());
        if (_voices.Count >= VoiceLimit || family < 0 || family >= Families.Length) return;
        var samples = Families[family];
        var sampleIndex = _roundRobin[family]++ % samples.Length;
        var path = AudioRoot + samples[sampleIndex];
        if (!Streams.TryGetValue(path, out var stream))
        {
            if (!ResourceLoader.Exists(path) || GD.Load<AudioStream>(path) is not { } loaded) return;
            stream = loaded;
            Streams[path] = stream;
        }

        var voice = new AudioStreamPlayer2D
        {
            Name = "CombatFoleyVoice",
            Stream = stream,
            Bus = "Master",
            VolumeDb = volumeDb,
            MaxDistance = 1200,
            Attenuation = 0.5f,
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(voice);
        voice.GlobalPosition = position;
        _voices.Add(voice);
        voice.Play();
    }
}
