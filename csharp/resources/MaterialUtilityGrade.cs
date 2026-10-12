using Godot;

/// <summary>一条材料功用的九品强度；九品为 1，一品为 9。</summary>
public partial class MaterialUtilityGrade : Resource
{
    [Export] public string utility_id = "";
    [Export(PropertyHint.Range, "1,9,1")] public int grade = 1;

    public string grade_name() => MaterialCraftData.utility_grade_name(grade);
}
