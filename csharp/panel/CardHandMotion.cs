using Godot;

/// <summary>Shared fan geometry and spring motion for the condensation and battle hands.</summary>
internal static class CardHandMotion
{
    internal static (Vector2 Position, float Angle) FanPose(int index, int count, Vector2 middle,
        float spacing, float arc, float tilt)
    {
        var offset = index - (count - 1) * .5f;
        return (middle + new Vector2(offset * spacing, Math.Abs(offset) * arc), offset * tilt);
    }

    internal static (int Count, float Delta) Steps(double delta)
    {
        var elapsed = Math.Min(delta, .05);
        var count = Math.Max(1, (int)Math.Ceiling(elapsed / (1.0 / 120)));
        return (count, (float)elapsed / count);
    }

    internal static void Spring(ref float position, ref float velocity, float target,
        float stiffness, float damping, float delta)
    {
        velocity += ((target - position) * stiffness - velocity * damping) * delta;
        position += velocity * delta;
    }
}
