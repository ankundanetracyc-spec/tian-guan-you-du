using System;
using System.Collections.Generic;

/// <summary>A single interaction offered by an NPC interaction component.</summary>
public sealed record NpcInteractionAction(string Id, string Label, Action Activate, bool Enabled = true, string UnavailableReason = "");

/// <summary>
/// Supplies actions for a world NPC. Components are registered on MainScene and queried
/// whenever the selected target or its available behavior changes.
/// </summary>
public interface INpcInteractionActionComponent
{
    string ComponentId { get; }
    event Action? ActionsChanged;
    IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target);
}

public abstract class NpcInteractionActionComponent : INpcInteractionActionComponent
{
    public abstract string ComponentId { get; }
    public event Action? ActionsChanged;
    public abstract IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target);

    protected void notify_actions_changed() => ActionsChanged?.Invoke();
}
