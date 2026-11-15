using Godot;
using System.Collections.Generic;

/// <summary>UI-only venue description. Costs are supplied by the caller so the panel does not mutate game state.</summary>
public sealed class BanquetVenueOption
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string CostText { get; init; } = "";
    public string Description { get; init; } = "";
    public int CapacityIncludingHost { get; init; }
}

/// <summary>Absolute in-game hour chosen by the host; the UI does not advance time.</summary>
public sealed class BanquetTimeOption
{
    public string Label { get; init; } = "";
    public int StartsAtHour { get; init; }
}

public sealed class BanquetRosterEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public Texture2D? Portrait { get; init; }
    public WorldNpcData? Npc { get; init; }
    public Character? PlayerCharacter { get; init; }
}

public sealed class BanquetSetupRequest
{
    public string HostId { get; init; } = "";
    public string VenueId { get; init; } = "";
    public IReadOnlyList<string> GuestIds { get; init; } = [];
    public int StartsAtHour { get; init; }
}

public sealed class BanquetParticipantPresentation
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public Texture2D? Portrait { get; init; }
    public WorldNpcData? Npc { get; init; }
    public Character? PlayerCharacter { get; init; }
}

public sealed class BanquetActivityPresentation
{
    public string EventId { get; init; } = "";
    public string Title { get; init; } = "宗门雅集";
    public string HostName { get; init; } = "";
    public string VenueName { get; init; } = "";
    public IReadOnlyList<BanquetParticipantPresentation> Participants { get; init; } = [];
}

public sealed class BanquetChoicePresentation
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public string Tone { get; init; } = "";
    public string RequirementText { get; init; } = "";
    public bool IsAvailable { get; init; } = true;
}

public sealed class BanquetBeatPresentation
{
    public string EventId { get; init; } = "";
    public string BeatId { get; init; } = "";
    public string SpeakerId { get; init; } = "";
    public string SpeakerName { get; init; } = "";
    public string Text { get; init; } = "";
    public bool IsNotice { get; init; }
    public IReadOnlyList<string> HeardByIds { get; init; } = [];
    public IReadOnlyList<BanquetChoicePresentation> Choices { get; init; } = [];
    public double DisplaySeconds { get; init; } = 3.4;
    public double ResponseSeconds { get; init; } = 12;
}

/// <summary>Returned once when a player chooses or the visible response timer expires.</summary>
public sealed class BanquetResponseSelection
{
    public string EventId { get; init; } = "";
    public string BeatId { get; init; } = "";
    public string ChoiceId { get; init; } = "";
    public bool TimedOut { get; init; }
}
