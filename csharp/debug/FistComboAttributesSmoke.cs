using Godot;
using System;

public partial class FistComboAttributesSmoke : Node
{
    private int _checks;
    private void Check(bool value, string message)
    { if (!value) throw new Exception(message); _checks++; }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        ActionCharacterBattleSession? session = null;
        try
        {
            var owner = new Character { Visible = false, entity_data = new HumanData
            { entity_name = "四段实人验收", cultivation_realm = "云息一层", base_physique = 8, base_intelligence = 10, base_soul = 8 } };
            AddChild(owner);
            var worldHealth = owner.current_health;
            var worldMana = owner.current_mana;
            var original = GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres");
            var baseDamage = original.base_damage;
            var formula = original.damage_formula;
            var bonusCount = original.damage_bonuses.Count;
            var player = owner.get_snapshot_data();
            player.precision = 1000000;
            var enemy = (BattleEntityData)player.Duplicate(true);
            enemy.uid = "combo-fixture-enemy";
            enemy.current_health = enemy.max_health = 100000;
            enemy.dodge = enemy.physical_defense = 0;
            enemy.physical_damage_reduction = 0;
            enemy.crit_resist = 1000000;
            session = ActionCharacterBattleSession.Open(owner);
            Check(session is not null, "可从正式人物打开四段演练");
            var arena = GetTree().CurrentScene;
            arena.SetProcess(false);
            var attributes = (ActionBattleAttributeRuntime)arena.Get("attribute_runtime").AsGodotObject()!;
            attributes.Configure(player, enemy);
            arena.Call("sync_character_attributes");
            var expectedCooldown = Math.Max(.35f, .8f - player.physique * .002f);
            arena.Set("player", new Vector2(1200, 900));
            arena.Set("held_basic_attack", true);
            arena.Call("use_skill", "拳掌");
            var combo = arena.Get("fist_combo").AsGodotObject()!;
            var durations = new[] { .45, .50, .55, .70 };
            var expectedDamage = new[] { 4, 4, 6, 8 };
            var lastHp = arena.Get("enemy_hp").AsInt32();
            GD.Seed(1703);
            for (var i = 0; i < 4; i++)
            {
                Check(combo.Get("stage").AsInt32() == i && combo.Get("damage").AsInt32() == expectedDamage[i], $"第{i + 1}段读取真实体魄并按份额取整");
                var before = arena.Get("player").AsVector2();
                var direction = combo.Get("direction").AsVector2();
                arena.Set("enemy", before + direction * 40);
                arena.Call("update_skill_timers", durations[i] * .4);
                Check(lastHp - arena.Get("enemy_hp").AsInt32() == expectedDamage[i], $"第{i + 1}段进入真实属性命中与伤害结算且没有重复缩放");
                lastHp = arena.Get("enemy_hp").AsInt32();
                var travel = arena.Get("player").AsVector2().DistanceTo(before);
                Check(Mathf.Abs(travel - (i == 1 ? 8f : i == 3 ? 12f : 0f)) < .001f, $"第{i + 1}段真实贴步正确");
                Check(arena.Call("skill_cooldown_remaining", "拳掌").AsSingle() == 0, "段内与段间没有提前进入拳掌CD");
                arena.Call("update_skill_timers", durations[i] * .6);
            }
            var finalCooldown = arena.Call("skill_cooldown_remaining", "拳掌").AsSingle();
            Check(!combo.Get("active").AsBool() && Mathf.IsEqualApprox(finalCooldown, expectedCooldown), $"整套2.2秒结束才开始一次完整最终属性CD：CD={finalCooldown}，预期={expectedCooldown}");
            Check(arena.Get("stamina").AsInt32() == 6 && owner.current_health == worldHealth && owner.current_mana == worldMana, "四段0费且不污染世界人物");
            Check(original.base_damage == baseDamage && original.damage_formula == formula && original.damage_bonuses.Count == bonusCount, "逐段派生伤害不修改共享原技能资源");
            arena.Call("update_skill_timers", expectedCooldown);
            arena.Call("use_skill", "拳掌");
            arena.Call("update_skill_timers", .10);
            var beforeCancel = arena.Get("enemy_hp").AsInt32();
            arena.Call("use_skill", "错步");
            arena.Call("update_skill_timers", 3.0);
            Check(arena.Get("enemy_hp").AsInt32() == beforeCancel && !combo.Get("active").AsBool(), "真实人物中断后不补发剩余伤害");
            session!.Close(); session = null;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!GetTree().Paused && owner.current_health == worldHealth, "退出四段演练恢复世界且不回写生命");
            GD.Print($"FIST_COMBO_ATTRIBUTES_OK {_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        { session?.Close(); GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
