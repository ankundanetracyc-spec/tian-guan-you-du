using Godot;
using System.Collections.Generic;

public enum HoverGridKind { Inventory, Combat, Range }

/// <summary>Display metadata in real local cells; range Cells exclude the origin's benefit.</summary>
public sealed record HoverGridDiagram(
    string Title,
    HoverGridKind Kind,
    IReadOnlyList<Vector2I> Cells,
    string Caption = "",
    Vector2I? Origin = null,
    Vector2I? Direction = null,
    bool RotationAllowed = false,
    string SourceLabel = "");
