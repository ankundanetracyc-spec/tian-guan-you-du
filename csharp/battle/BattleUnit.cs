using Godot;
using Godot.Collections;

/// <summary>单个战斗单位的实时队列与临时战斗资源状态。</summary>
public partial class BattleUnit : RefCounted
{
    public GodotObject? character;
    public GodotObject? deck;
    public Array<GodotObject> hand = [];
    public float action_gauge;
    public Array<StatusEffect> status_effects = [];
    public Dictionary intent = [];
    public Dictionary special_resources = [];
    public bool has_drawn_opening_hand;
    public readonly List<BattleQueuedSkill> skill_queue = [];
    // 现行战斗保留整轮槽位，只移动游标；旧牌局仍可使用原先的可变列表。
    public int segment_action_index;
    public BattleQueuedSkill? current_segment_action => segment_action_index >= 0 && segment_action_index < skill_queue.Count ? skill_queue[segment_action_index] : null;
    public bool has_pending_segment_action => current_segment_action is not null;

    public void setup(GodotObject owner, GodotObject? deck_manager)
    {
        character = owner; deck = deck_manager; hand.Clear(); action_gauge = 0;
        status_effects.Clear(); intent.Clear(); special_resources.Clear(); has_drawn_opening_hand = false; skill_queue.Clear();
        segment_action_index = 0;
    }

    public Array<GodotObject> draw_for_action()
    {
        if (!GodotObject.IsInstanceValid(deck)) return hand;
        if (!has_drawn_opening_hand)
        {
            has_drawn_opening_hand = true;
            hand = to_objects(deck!.Call("draw_initial_hand").AsGodotArray());
        }
        else
        {
            hand = to_objects(deck!.Call("discard_hand_at_turn_start", hand).AsGodotArray());
            hand.AddRange(to_objects(deck.Call("draw_for_turn").AsGodotArray()));
        }
        return hand;
    }

    public void discard_action_hand()
    {
        if (GodotObject.IsInstanceValid(deck) && hand.Count > 0)
            hand = to_objects(deck!.Call("discard_hand_at_turn_end", hand).AsGodotArray());
    }

    public int get_special_resource(string id) => special_resources.TryGetValue(id, out var value) ? value.AsInt32() : 0;
    public int get_qi() => get_special_resource("炁");
    public void set_qi(int value) => special_resources["炁"] = Mathf.Max(0, value);
    public int get_skill_nian(string skillKey) => get_special_resource("念:" + skillKey);
    public int add_skill_nian(string skillKey, int amount)
    {
        if (string.IsNullOrWhiteSpace(skillKey) || amount <= 0) return get_skill_nian(skillKey);
        var next = get_skill_nian(skillKey) + amount;
        special_resources["念:" + skillKey] = next;
        return next;
    }
    public int add_special_resource(string id, int amount, int maximum = -1)
    {
        var next = Mathf.Max(0, get_special_resource(id) + amount);
        if (maximum >= 0) next = Mathf.Min(next, maximum);
        special_resources[id] = next;
        return next;
    }

    private static Array<GodotObject> to_objects(Godot.Collections.Array values)
    {
        var result = new Array<GodotObject>();
        foreach (var value in values)
        {
            var item = value.AsGodotObject();
            if (GodotObject.IsInstanceValid(item)) result.Add(item);
        }
        return result;
    }
}
