using Godot;
using GArray = Godot.Collections.Array;

/// <summary>工作台配方槽位的可视化布局。</summary>
public partial class WorkstationSlotPanel : Node2D
{
    [Export] public float slot_radius = 96.0f;

    public void set_slots(GArray labels)
    {
        var slots = GetNodeOrNull<Node2D>("Slots");
        if (slots is null) return;
        for (var i = 0; i < slots.GetChildCount(); i++)
        {
            var slot = slots.GetChild<Control>(i);
            var enabled = i < labels.Count;
            slot.Visible = enabled;
            if (!enabled) continue;
            slot.Position = Vector2.FromAngle(Mathf.Tau * i / Mathf.Max(1, labels.Count)) * slot_radius;
            var label = slot.GetNodeOrNull<Label>("Label");
            if (label is not null) label.Text = labels[i].AsString();
        }
    }
}
