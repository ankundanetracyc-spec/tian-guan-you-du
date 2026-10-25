using Godot;
using System.Reflection;

/// <summary>Isolated real-species/status checks; no world or save writes.</summary>
public partial class ForestBeiSmoke : Node2D
{
    private BattleManager _manager = null!;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private Beast Unit(string species, int faction, string name)
    {
        var template = GD.Load<BeastTemplate>(species == "forest_bei" ? "res://resources/beast_templates/林地妖狈.tres" : "res://resources/beast_templates/林地凶狼.tres");
        var unit = new Beast { beast_template = template, entity_name = name, faction = faction };
        AddChild(unit);
        unit.species_id = species;
        if (species == "forest_bei")
        {
            unit.base_physique = unit.physique = template.physique_mean;
            unit.base_intelligence = unit.intelligence = template.intelligence_mean;
            unit.base_soul = unit.soul = template.soul_mean;
            unit.recalculate_beast();
        }
        _manager.all_units.Add(unit);
        return unit;
    }
    public override async void _Ready()
    {
        try
        {
            _manager = new BattleManager { Name = "BattleMgr", isolated_trial_battle = true };
            AddChild(_manager);
            var wolfA = Unit("forest_wolf", 1, "友狼甲");
            var wolfB = Unit("forest_wolf", 1, "友狼乙");
            var foe = Unit("forest_wolf", 2, "敌狼");
            var other = Unit("test_other", 1, "其他兽种");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _manager.trigger_species_attack_talent(wolfA);
            Check(wolfA.get_status_stacks("逐猎") == 0 && wolfB.get_status_stacks("逐猎") == 1, "Original pack hunt changed without a chief");
            wolfA.statuses.Clear(); wolfB.statuses.Clear();
            var beiA = Unit("forest_bei", 1, "林地妖狈甲");
            var beiB = Unit("forest_bei", 1, "林地妖狈乙");
            var cry = GD.Load<ActiveSkillData>("res://resources/activeskills/妖兽/林地妖狈/狈啸.tres");
            var call = GD.Load<ActiveSkillData>("res://resources/activeskills/妖兽/林地妖狈/唤猎.tres");
            Check(beiA.max_health == 176 && beiA.physique == 7 && beiA.intelligence == 8 && beiA.soul == 9, "Mean species attributes mismatch");
            Check(cry.calculate_damage(beiA) == 16 && cry.damage_type == "soul" && cry.attack_style == "ranged", "Cry formula or attack style mismatch");
            Check(cry.energy_cost == 1 && call.energy_cost == 1 && cry.mana_cost == 0 && call.mana_cost == 0, "Skill cost mismatch");
            Check(CardPresentation.CombatTargetText(call, call.description).Contains("全体存活的友方林地凶狼"), "Pack call UI has the wrong target");
            _manager.trigger_species_attack_talent(wolfA);
            Check(wolfA.get_status_stacks("逐猎") == 1 && wolfB.get_status_stacks("逐猎") == 1, "Multiple chiefs duplicated the self grant");
            Check(foe.get_status_stacks("逐猎") == 0 && other.get_status_stacks("逐猎") == 0 && beiA.get_status_stacks("逐猎") == 0, "Chief affected an illegal recipient");
            beiA.current_health = beiB.current_health = 0;
            _manager.trigger_species_attack_talent(wolfA);
            Check(wolfA.get_status_stacks("逐猎") == 1 && wolfB.get_status_stacks("逐猎") == 2, "Chief death did not stop new self grants or erased old stacks");
            beiA.current_health = beiA.max_health;
            wolfA.statuses.Clear(); wolfB.statuses.Clear();
            var executor = _manager.GetChildren().OfType<SkillExecutor>().Single();
            var apply = typeof(SkillExecutor).GetMethod("ApplySelfSpecialEffects", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(ActiveSkillData), typeof(GodotObject), typeof(GodotObject), typeof(bool) }, null)!;
            apply.Invoke(executor, new object[] { call, beiA, beiA, false });
            Check(wolfA.get_status_stacks("逐猎") == 1 && wolfB.get_status_stacks("逐猎") == 1, "Actual utility dispatch failed to affect all allied wolves exactly once");
            Check(foe.get_status_stacks("逐猎") == 0 && other.get_status_stacks("逐猎") == 0 && beiA.get_status_stacks("逐猎") == 0, "Pack call affected an illegal recipient");
            for (var i = 0; i < 6; i++) _manager.grant_pack_hunt_to_wolves(beiA);
            Check(wolfA.get_status_stacks("逐猎") == 3 && wolfB.get_status_stacks("逐猎") == 3 && _manager.grant_pack_hunt_to_wolves(beiA) == 0, "Pack call exceeded the cap or reported a fake gain");
            beiA.current_health = 0;
            var enemyBei = Unit("forest_bei", 2, "敌方妖狈");
            wolfA.statuses.Clear(); wolfB.statuses.Clear();
            _manager.trigger_species_attack_talent(wolfA);
            Check(wolfA.get_status_stacks("逐猎") == 0, "Enemy chief benefited an allied wolf");
            _manager.trigger_species_attack_talent(foe);
            Check(foe.get_status_stacks("逐猎") == 1, "Enemy-faction chief failed to benefit its own wolf");
            Check(_manager.grant_pack_hunt_to_wolves(beiA) == 0, "Dead caster granted pack hunt");
            GD.Print("FOREST_BEI_SMOKE_PASSED: attributes, formula, costs, target text, original pack hunt, multi-chief, death, all-wolf utility dispatch, faction/species filters, cap and zero-gain reporting");
            await ToSignal(GetTree().CreateTimer(.8, false), SceneTreeTimer.SignalName.Timeout);
            GetTree().Quit();
        }
        catch (Exception error) { GD.PrintErr(error); GetTree().Quit(1); }
    }
}
