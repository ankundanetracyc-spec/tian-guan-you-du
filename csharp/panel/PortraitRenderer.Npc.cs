using Godot;

public partial class PortraitRenderer : Control
{
    public void show_generated_npc(WorldNpcData npc)
    {
        if (npc is null) { clear_portrait(); return; }
        int seed = npc.appearance_seed > 0 ? npc.appearance_seed : stable_seed(npc.npc_id + "|" + npc.npc_name);
        int gender = npc.gender is 0 or 1 ? npc.gender : seed % 2;
        var data = new HumanData { gender = gender, appearance_seed = seed, faction_name = npc.faction_id };
        string genderName = gender == 1 ? "female" : "male";
        var catalog = GetNode<PortraitCatalog>("/root/PortraitCatalog");
        var context = catalog.get_context(data, genderName, npc.current_age, data.faction_name, npc.cultivation_realm);
        configure(data, context, catalog.get_requested_appearance(data));
    }
}
