using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ActionSelfSkillsSmoke : Node
{
    private int _checks;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        ActionCharacterBattleSession? session = null;
        try
        {
            var owner = new Character { Visible = false, entity_data = new HumanData
            { entity_name = "即时运功验收", cultivation_realm = "云息一层", base_physique = 8, base_intelligence = 10, base_soul = 8 } };
            AddChild(owner);
            owner.current_health = owner.max_health - 11;
            var health = owner.current_health;
            var mana = owner.current_mana;
            var slots = owner.get_combat_skill_slot_paths().ToArray();
            var player = owner.get_snapshot_data();
            var enemy = (BattleEntityData)player.Duplicate(true);
            enemy.uid = "self-skills-opponent";
            var runtime = new ActionBattleAttributeRuntime();
            AddChild(runtime);
            player.body_qi_blood_bonus = 2;
            player.combat_tempering_by_gongfa["气血修持"] = new Godot.Collections.Dictionary { ["qi_blood_cap"] = 3f };
            runtime.Configure(player, enemy);
            Check(runtime.get_state()["qi_blood_capacity"].AsInt32() == 13, "气血上限沿用基础8、修命2和修持3");
            runtime.reset_battle();
            Check(runtime.get_state()["qi_blood_capacity"].AsInt32() == 13, "重开不重复累计气血上限");
            runtime.QueueFree();

            var previous = GetTree().CurrentScene;
            session = ActionCharacterBattleSession.Open(owner);
            Check(session is not null, "未学会两招的人物仍可进入基础四招演练");
            var arena = GetTree().CurrentScene;
            arena.SetProcess(false);
            await Frame();
            Check(arena.Get("available_self_skills").AsGodotArray().Count == 0, "未参悟节点不授予催血聚炁");
            arena.Set("configuring", true);
            Check(!arena.Call("assign_skill_slot", 2, "催血").AsBool(), "未学会不能通过演练装配绕过资格");
            session!.Close(); session = null;
            await Frame();
            Check(GetTree().CurrentScene == previous && !GetTree().Paused, "退出未学习演练恢复世界");

            owner.active_skills.Add(GD.Load<ActiveSkillData>("res://resources/activeskills/催血.tres"));
            owner.active_skills.Add(GD.Load<ActiveSkillData>("res://resources/activeskills/聚炁.tres"));
            session = ActionCharacterBattleSession.Open(owner);
            Check(session is not null, "已学习人物可再次进入演练");
            arena = GetTree().CurrentScene;
            arena.SetProcess(false);
            await Frame();
            var pool = arena.Get("available_self_skills").AsGodotArray();
            Check(arena.Get("intelligence").AsSingle() == owner.intelligence && arena.Get("dodge").AsSingle() == owner.dodge, "灵慧和身法来自正式人物属性");
            Check(Mathf.IsEqualApprox(arena.Call("skill_cooldown_duration", "错步").AsSingle(), Math.Max(2f, 6f / (1f + owner.dodge / 100f))), "真实身法参与错步独立冷却公式");
            Check(pool.Count == 2 && pool.Any(value => value.AsString() == "催血") && pool.Any(value => value.AsString() == "聚炁"), "技能池读取真实已学技能");
            Check(arena.Get("skill_slots").AsGodotArray()[2].AsString() == "", "学会不等于自动装配");
            arena.Set("configuring", true);
            Check(arena.Call("assign_skill_slot", 2, "催血").AsBool() && arena.Call("assign_skill_slot", 3, "聚炁").AsBool(), "可在五个自由槽内显式装配两招");
            arena.Set("configuring", false);
            arena.Call("use_slot", 2);
            Check(arena.Get("pending_self_skill").AsString() == "催血" && arena.Get("stamina").AsInt32() == 5, "真实角色走统一运功与支付入口");
            Check(arena.Call("skill_cooldown_remaining", "催血").AsSingle() == 0f, "真实人物运功期间尚未开始冷却");
            arena.Set("player", new Vector2(400, 500));
            arena.Call("move_character", Vector2.Right, .5);
            Check(Mathf.Abs(arena.Get("player").AsVector2().X - 458.75f) < .001f, "运功移速为真实235的一半");
            arena.Call("update_skill_timers", 1.0);
            Check(arena.Get("qi_blood").AsInt32() == 2 && arena.Call("movement_multiplier").AsSingle() == 1f, "真实角色完成后获得气血且恢复移速");
            Check(arena.Get("hp").AsInt32() == health, "催血不自动治疗");
            arena.Set("mana", 0);
            var capacity = arena.Get("mana_capacity").AsInt32();
            for (var i = 0; i < capacity; i++)
            {
                arena.Call("use_slot", 3);
                arena.Call("update_skill_timers", 1.0 + arena.Call("skill_cooldown_duration", "聚炁").AsDouble());
            }
            Check(arena.Get("qi").AsInt32() == 0 && arena.Get("mana").AsInt32() == capacity, "真实法力上限决定周天阈值与回复");
            var attributes = arena.Get("attribute_runtime").AsGodotObject()!;
            Check(attributes.Call("get_state").AsGodotDictionary()["mana"].AsInt32() == capacity, "周天法力同时同步到隔离快照");
            Check(owner.current_health == health && owner.current_mana == mana && owner.get_combat_skill_slot_paths().SequenceEqual(slots), "施放与局内装配不回写人物生命、法力或正式技能槽");
            arena.Call("use_slot", 3);
            session!.Close(); session = null;
            await Frame();
            Check(GetTree().CurrentScene == previous && !GetTree().Paused && owner.current_health == health, "运功中离开也能恢复世界且不回写资源");
            GD.Print($"ACTION_SELF_SKILLS_OK {_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            session?.Close();
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
