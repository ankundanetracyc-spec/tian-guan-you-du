using Godot;
using Godot.Collections;

/// <summary>突破到指定境界后应用的一次性属性加成。</summary>
public partial class BreakthroughBonusConfig : Resource
{
    [Export] public string realm = "";
    private Dictionary _multipliers = [];
    private Dictionary _fixed = [];

    [Export]
    public Dictionary multipliers
    {
        get => _multipliers;
        set => _multipliers = RealmConfig.normalize(value, RealmConfig.MultiplierKeys, _multipliers);
    }

    [Export]
    public Dictionary @fixed
    {
        get => _fixed;
        set => _fixed = RealmConfig.normalize(value, RealmConfig.FixedBonusKeys, _fixed);
    }

    public void sync_defaults()
    {
        multipliers = multipliers;
        @fixed = @fixed;
    }
}
