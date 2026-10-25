using Godot;

public partial class GameStartProfile
{
    public static readonly (string Key, string Name)[] StartingProficiencies =
    [
        ("sword_proficiency", "剑道"), ("blade_proficiency", "刀道"), ("spear_proficiency", "枪道"),
        ("body_proficiency", "体道"), ("magic_proficiency", "术法"),
        ("forging_proficiency", "炼器"), ("talisman_proficiency", "符箓"),
        ("formation_proficiency", "阵法"), ("alchemy_proficiency", "炼丹")
    ];
    private readonly System.Collections.Generic.Dictionary<string, float> _startingProficiencies = new();

    public float get_starting_proficiency(string key) => _startingProficiencies.GetValueOrDefault(key, 5f);

    public void set_starting_proficiency(string key, float value)
    {
        if (!float.IsFinite(value) || !StartingProficiencies.Any(item => item.Key == key)) return;
        _startingProficiencies[key] = Mathf.Clamp(Mathf.Snapped(value, .1f), 0, 10);
    }

    public void reset_starting_proficiencies() => _startingProficiencies.Clear();

    private void apply_starting_proficiencies(HumanData data)
    {
        foreach (var (key, _) in StartingProficiencies)
        {
            var value = get_starting_proficiency(key);
            data.Set("base_" + key, value);
            data.Set(key, value);
        }
    }

    private void save_starting_proficiencies(ConfigFile cfg, string section)
    {
        foreach (var (key, _) in StartingProficiencies)
            cfg.SetValue(section, "starting_" + key, get_starting_proficiency(key));
    }

    private void load_starting_proficiencies(ConfigFile cfg, string section)
    {
        reset_starting_proficiencies();
        foreach (var (key, _) in StartingProficiencies)
            set_starting_proficiency(key, cfg.GetValue(section, "starting_" + key, 5f).AsSingle());
    }
}
