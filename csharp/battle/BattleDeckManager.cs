using Godot;
using Godot.Collections;

/// <summary>Per-unit draw, discard, and exhaust piles for a single battle.</summary>
public partial class BattleDeckManager : Node
{
    private const int DefaultInitialHandSize = 4;
    private const int DefaultCardsPerAction = 2;

    [Signal] public delegate void CardDrawnEventHandler(ActiveSkillData skill);
    [Signal] public delegate void CardDiscardedEventHandler(ActiveSkillData skill);
    [Signal] public delegate void PilesChangedEventHandler(int draw_count, int discard_count);

    public Array<ActiveSkillData> draw_pile = [];
    public Array<ActiveSkillData> discard_pile = [];
    public Array<ActiveSkillData> exhausted_pile = [];
    private GodotObject? _unit;

    public void setup(GodotObject? unit)
    {
        _unit = unit;
        draw_pile.Clear(); discard_pile.Clear(); exhausted_pile.Clear();
        foreach (var source in CultivationDeckBuilder.collect_cards(unit))
        {
            if (source is not ActiveSkillData skill || skill.skill_type == ActiveSkillData.SkillType.MOVEMENT) continue;
            var card = skill.Duplicate() as ActiveSkillData ?? skill;
            if (!string.IsNullOrEmpty(skill.ResourcePath))
            {
                card.SetMeta("source_path", skill.ResourcePath);
                CopyMeta(skill, card, "pack_index");
                CopyMeta(skill, card, "pack_item_path");
                CopyMeta(skill, card, "remove_count");
            }
            draw_pile.Add(card);
        }
        shuffle_draw_pile();
        EmitPilesChanged();
    }

    public void shuffle_draw_pile()
    {
        for (var i = draw_pile.Count - 1; i > 0; i--)
        {
            var index = (int)GD.RandRange(0, i);
            (draw_pile[i], draw_pile[index]) = (draw_pile[index], draw_pile[i]);
        }
    }

    public void reshuffle_discard_into_draw()
    {
        draw_pile.AddRange(discard_pile); discard_pile.Clear(); shuffle_draw_pile(); EmitPilesChanged();
    }

    public Array<ActiveSkillData> draw(int count)
    {
        var result = new Array<ActiveSkillData>();
        for (var i = 0; i < count; i++)
        {
            if (draw_pile.Count == 0) reshuffle_discard_into_draw();
            if (draw_pile.Count == 0) break;
            var card = draw_pile[^1]; draw_pile.RemoveAt(draw_pile.Count - 1); result.Add(card);
            EmitSignal(SignalName.CardDrawn, card);
        }
        EmitPilesChanged(); return result;
    }

    public int get_initial_hand_size() => GetUnitValue("initial_hand_size", DefaultInitialHandSize);
    public int get_cards_per_turn() => GetUnitValue("cards_per_turn", DefaultCardsPerAction);
    public Array<ActiveSkillData> draw_initial_hand()
    {
        var innate = new Array<ActiveSkillData>();
        for (var i = draw_pile.Count - 1; i >= 0; i--)
            if (draw_pile[i].keywords.Contains("固有")) { innate.Add(draw_pile[i]); draw_pile.RemoveAt(i); }
        innate.AddRange(draw(Mathf.Max(0, get_initial_hand_size() - innate.Count)));
        EmitPilesChanged(); return innate;
    }
    public Array<ActiveSkillData> draw_for_turn() => draw(get_cards_per_turn());

    public void discard(ActiveSkillData? skill)
    {
        if (skill is null) return;
        if (skill.keywords.Contains("弃顶")) discard_pile.Insert(0, skill); else discard_pile.Add(skill);
        EmitSignal(SignalName.CardDiscarded, skill); EmitPilesChanged();
    }
    public void exhaust_card(ActiveSkillData? skill)
    {
        if (skill is null) return;
        draw_pile.Remove(skill); discard_pile.Remove(skill);
        if (!exhausted_pile.Contains(skill)) exhausted_pile.Add(skill);
        EmitPilesChanged();
    }
    public Array<ActiveSkillData> discard_hand_at_turn_end(Array<ActiveSkillData> hand_skills)
        => new(hand_skills);

    public Array<ActiveSkillData> discard_hand_at_turn_start(Array<ActiveSkillData> hand_skills)
    {
        var kept = new Array<ActiveSkillData>();
        foreach (var skill in hand_skills)
            if (skill.keywords.Contains("虚无")) exhaust_card(skill);
            else if (skill.keywords.Contains("保留")) kept.Add(skill);
            else discard(skill);
        EmitPilesChanged(); return kept;
    }
    public int get_draw_count() => draw_pile.Count;
    public int get_discard_count() => discard_pile.Count;
    public int get_exhaust_count() => exhausted_pile.Count;
    public void retrieve_from_discard(ActiveSkillData? skill) { if (skill is not null) discard_pile.Remove(skill); EmitPilesChanged(); }
    public void remove_skills_from_all_piles(Array<ActiveSkillData> skills)
    {
        var paths = new HashSet<string>(skills.Where(skill => !string.IsNullOrEmpty(skill.ResourcePath)).Select(skill => skill.ResourcePath));
        if (paths.Count == 0) return;
        RemoveWhere(draw_pile, skill => paths.Contains(GetSourcePath(skill)));
        RemoveWhere(discard_pile, skill => paths.Contains(GetSourcePath(skill)));
        RemoveWhere(exhausted_pile, skill => paths.Contains(GetSourcePath(skill))); EmitPilesChanged();
    }
    public void remove_skills_by_weapon_source(string weapon_path)
    {
        if (string.IsNullOrEmpty(weapon_path)) return;
        RemoveWhere(draw_pile, skill => skill.GetMeta("weapon_source", "").AsString() == weapon_path);
        RemoveWhere(discard_pile, skill => skill.GetMeta("weapon_source", "").AsString() == weapon_path);
        RemoveWhere(exhausted_pile, skill => skill.GetMeta("weapon_source", "").AsString() == weapon_path); EmitPilesChanged();
    }
    public void add_skills_to_discard(Array<ActiveSkillData> skills) { discard_pile.AddRange(skills); EmitPilesChanged(); }
    public void remove_skills_by_pack_index(int pack_idx)
    {
        RemoveWhere(draw_pile, skill => skill.HasMeta("pack_index") && skill.GetMeta("pack_index").AsInt32() == pack_idx);
        RemoveWhere(discard_pile, skill => skill.HasMeta("pack_index") && skill.GetMeta("pack_index").AsInt32() == pack_idx);
        RemoveWhere(exhausted_pile, skill => skill.HasMeta("pack_index") && skill.GetMeta("pack_index").AsInt32() == pack_idx); EmitPilesChanged();
    }
    public void shift_pack_indices_after_removal(int removed_idx)
    {
        foreach (var pile in new[] { draw_pile, discard_pile, exhausted_pile }) foreach (var skill in pile)
            if (skill.HasMeta("pack_index") && skill.GetMeta("pack_index").AsInt32() > removed_idx)
                skill.SetMeta("pack_index", skill.GetMeta("pack_index").AsInt32() - 1);
    }

    private int GetUnitValue(string property, int fallback)
    {
        if (!GodotObject.IsInstanceValid(_unit)) return fallback;
        var data = _unit!.Get("entity_data").AsGodotObject();
        return GodotObject.IsInstanceValid(data) ? Mathf.Max(1, data!.Get(property).AsInt32()) : fallback;
    }
    private void EmitPilesChanged() => EmitSignal(SignalName.PilesChanged, draw_pile.Count, discard_pile.Count);
    private static void CopyMeta(GodotObject from, GodotObject to, StringName key) { if (from.HasMeta(key)) to.SetMeta(key, from.GetMeta(key)); }
    private static string GetSourcePath(ActiveSkillData skill) => string.IsNullOrEmpty(skill.ResourcePath) ? skill.GetMeta("source_path", "").AsString() : skill.ResourcePath;
    private static void RemoveWhere(Array<ActiveSkillData> pile, Func<ActiveSkillData, bool> predicate)
    { for (var i = pile.Count - 1; i >= 0; i--) if (predicate(pile[i])) pile.RemoveAt(i); }
}
