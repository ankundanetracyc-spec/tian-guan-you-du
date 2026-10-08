using Godot;

public partial class CombatSkillBar
{
    private static void RefreshScaledPips(HBoxContainer? pips, int current, int max)
    {
        if (pips is null) return;
        var total = max <= 0 ? 0 : Mathf.Clamp(current / (float)max, 0, 1) * pips.GetChildCount();
        for (var i = 0; i < pips.GetChildCount(); i++)
            if (pips.GetChild(i) is ProgressBar pip) pip.Value = Mathf.Clamp(total - i, 0, 1);
    }
}
