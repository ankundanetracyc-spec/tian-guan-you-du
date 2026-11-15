using Godot;

/// <summary>Lets a queue row accept native Godot card drags as well as clicks.</summary>
public partial class TrialQueueDropTarget : Button
{
    internal Func<Variant, bool>? AcceptsCard;
    internal Action<Variant>? ReceiveCard;

    public override bool _CanDropData(Vector2 atPosition, Variant data) => !Disabled && AcceptsCard?.Invoke(data) == true;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_CanDropData(atPosition, data)) ReceiveCard?.Invoke(data);
    }
}
