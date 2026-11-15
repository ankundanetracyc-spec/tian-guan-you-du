using System.Reflection;
using Godot;
using Godot.Collections;

/// <summary>验证《斫基损契玄经》在战斗测试配置、资源支付与技能结算中的接入。</summary>
public partial class ZhuoJiCombatSmoke : Node
{
    private const string BookPath = "res://resources/gongfa/斫基损契玄经.tres";
    private const string HeartPath = "res://resources/activeskills/斫基损契玄经/斫基损契.tres";
    private const string ZheFengPath = "res://resources/activeskills/斫基损契玄经/折锋.tres";
    private const string FenYuanPath = "res://resources/activeskills/斫基损契玄经/焚元.tres";
    private const string ZheFengC1Path = "res://resources/activeskills/斫基损契玄经/化法/折锋.tres";
    private const string FenYuanC2Path = "res://resources/activeskills/斫基损契玄经/化法/焚元.tres";
    private const string JiaZhaoC3Path = "res://resources/activeskills/斫基损契玄经/化法/架招.tres";
    private const string ZheFengC4Path = "res://resources/activeskills/斫基损契玄经/化法/折锋长明.tres";
    private int _checks;

    public override void _Ready() => CallDeferred(nameof(Run));

    private void Check(bool condition, string label)
    {
        if (!condition) throw new System.Exception(label);
        _checks++;
    }

    private static object? InvokePrivate(Type type, object? instance, string name, params object?[] args)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new System.Exception($"找不到测试入口：{type.Name}.{name}");
        return method.Invoke(instance, args);
    }

    private static bool TupleOptionsContainPath(object? options, string path)
    {
        if (options is not System.Array values) return false;
        foreach (var item in values)
        {
            if (item is null) continue;
            var value = item.GetType().GetField("Item2")?.GetValue(item) as string;
            if (value == path) return true;
        }
        return false;
    }

    private static bool SkillOptionsContainPath(object? options, string path)
    {
        if (options is not System.Array values) return false;
        foreach (var item in values)
            if (item?.GetType().GetProperty("Path")?.GetValue(item) as string == path) return true;
        return false;
    }

    private HumanData MakeData(string name, int faction, int manaCapacity, string mainBookPath = BookPath)
    {
        return new HumanData
        {
            entity_name = name,
            faction = faction,
            main_gongfa_path = mainBookPath,
            max_health = 1000,
            current_health = 1000,
            max_stamina = 20,
            current_stamina = 20,
            max_mana = manaCapacity,
            current_mana = 0,
            physique = 20,
            intelligence = 20,
            soul = 10,
            precision = 10000,
            dodge = 0,
            critical = 0,
            crit_resist = 1000000,
            crit_damage_mult = 1.5f,
            magic_proficiency = 10
        };
    }

    private Creature AddUnit(BattleManager manager, HumanData data, int faction)
    {
        var unit = new Creature { Name = data.entity_name, entity_data = data, faction = faction };
        AddChild(unit);
        manager.all_units.Add(unit);
        if (faction == (int)WorldEntity.Faction.PLAYER) manager.player_units.Add(unit);
        else manager.enemy_units.Add(unit);
        var state = new BattleUnit();
        state.setup(unit, null);
        manager.battle_units[unit] = state;
        return unit;
    }

    private static void ApplyTestLoadoutMetadata(BattleEntityData snapshot, Creature unit)
    {
        unit.SetMeta("combat_passive_paths", snapshot.combat_passive_paths);
        unit.SetMeta("combat_heart_method_upgrades", snapshot.combat_heart_method_upgrades);
        unit.SetMeta("combat_tempering_by_gongfa", snapshot.combat_tempering_by_gongfa);
    }

    private void ApplyEquippedPassives(BattleManager manager, Creature unit) =>
        InvokePrivate(typeof(BattleManager), manager, "ApplyEquippedCombatPassives", unit);

    private static void Pay(BattleManager manager, Creature unit, ActiveSkillData skill) =>
        InvokePrivate(typeof(BattleManager), manager, "Pay", unit, skill);

    private async System.Threading.Tasks.Task Execute(BattleManager manager, Creature unit, ActiveSkillData skill, GodotObject target, System.Action? afterPayment = null)
    {
        var action = new BattleQueuedSkill(skill, target);
        manager.get_battle_unit(unit).skill_queue.Add(action);
        Pay(manager, unit, skill);
        manager.gongfa_combat.Before(unit, skill);
        afterPayment?.Invoke();
        var executor = manager.GetChildren().OfType<SkillExecutor>().Single();
        executor.execute_skill(unit, skill, target);
        for (var frame = 0; frame < 240 && action.phase != BattleQueuedSkill.Phase.Recovering; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(action.phase == BattleQueuedSkill.Phase.Recovering, $"{skill.skill_name}经 SkillExecutor 完整结算");
    }

    private async void Run()
    {
        try
        {
            var menuType = typeof(StartMenu);
            var bookOptions = InvokePrivate(menuType, null, "TestBookOptions");
            var skillOptions = InvokePrivate(menuType, null, "TestSkillOptions");
            Check(TupleOptionsContainPath(bookOptions, BookPath), "战斗测试秘籍列表包含本经");
            Check(SkillOptionsContainPath(skillOptions, ZheFengPath) && SkillOptionsContainPath(skillOptions, FenYuanPath), "战斗测试技能列表包含两项本经技能");
            Check(SkillOptionsContainPath(skillOptions, ZheFengC1Path) && SkillOptionsContainPath(skillOptions, FenYuanC2Path)
                && SkillOptionsContainPath(skillOptions, JiaZhaoC3Path) && SkillOptionsContainPath(skillOptions, ZheFengC4Path), "战斗测试技能列表包含四卷化法");

            var snapshot = new BattleEntityData();
            InvokePrivate(menuType, null, "ApplyTestLoadout", snapshot,
                "res://resources/basic_decks/公共基础技能池.tres", BookPath, "", new[] { ZheFengPath, FenYuanPath });
            Check(snapshot.combat_passive_paths.Contains(HeartPath), "战斗测试装载本经心法被动");
            Check(snapshot.combat_heart_method_upgrades.ContainsKey("斫基损契玄经|zhuo_ji_c3"), "战斗测试装载第三卷心法强化");
            Check(snapshot.combat_skill_slot_paths.Contains(ZheFengPath) && snapshot.combat_skill_slot_paths.Contains(FenYuanPath), "战斗测试按选择装入本经技能槽");

            var manager = new BattleManager { Name = "BattleMgr" };
            AddChild(manager);
            var uninitiated = AddUnit(manager, MakeData("未修斫基者", (int)WorldEntity.Faction.PLAYER, 4, ""), (int)WorldEntity.Faction.PLAYER);
            manager.gongfa_combat.Begin(uninitiated);
            Check(!manager.can_pay_skill_mana_cost(uninitiated, GD.Load<ActiveSkillData>(ZheFengPath)!), "未启用心法时不能燃烧法力上限支付费用");

            var player = AddUnit(manager, MakeData("斫基修士", (int)WorldEntity.Faction.PLAYER, 4), (int)WorldEntity.Faction.PLAYER);
            ApplyTestLoadoutMetadata(snapshot, player);
            manager.gongfa_combat.Begin(player);
            ApplyEquippedPassives(manager, player);
            var heart = GD.Load<ActiveSkillData>(HeartPath) ?? throw new System.Exception("心法资源读取失败");
            Check(manager.gongfa_combat.HasPower(player, "斫基损契"), "战斗初始化实际启用心法");
            Check(manager.can_pay_skill_mana_cost(player, GD.Load<ActiveSkillData>(ZheFengPath)!), "心法允许以法力上限补足费用");

            var qiState = manager.get_battle_unit(player);
            qiState.set_qi(5);
            Check(manager.spend_mana_capacity(player, 2), "法力上限可燃烧至高于1");
            Check(player.max_mana == 2 && player.current_mana == 2 && qiState.get_qi() == 0, "上限降至2后立即按新阈值完成周天");
            Check(manager.spend_mana_capacity(player, 1) && player.max_mana == 1, "法力上限可降至1");
            Check(!manager.spend_mana_capacity(player, 1) && player.max_mana == 1, "法力上限不会降至1以下");

            var attacker = AddUnit(manager, MakeData("折锋修士", (int)WorldEntity.Faction.PLAYER, 4), (int)WorldEntity.Faction.PLAYER);
            ApplyTestLoadoutMetadata(snapshot, attacker);
            manager.gongfa_combat.Begin(attacker);
            ApplyEquippedPassives(manager, attacker);
            var defender = AddUnit(manager, MakeData("试炼对手", (int)WorldEntity.Faction.ENEMY, 0, ""), (int)WorldEntity.Faction.ENEMY);
            defender.physical_defense = 0;
            defender.dodge = 0;
            var zheFeng = GD.Load<ActiveSkillData>(ZheFengC1Path) ?? throw new System.Exception("折锋化法资源读取失败");
            await Execute(manager, attacker, zheFeng, defender, () =>
                Check(attacker.max_mana == 3 && manager.gongfa_combat.PreventsResponse(attacker, zheFeng), "折锋燃烧上限支付后触发不可应对"));
            Check(manager.get_battle_qi(attacker) == 2, "法力为0命中产炁，并触发每回合首次燃费产炁");
            Check(manager.get_skill_mana_cost(attacker, zheFeng) == 0 && manager.get_skill_energy_cost(attacker, zheFeng) == 2, "主修燃费记录使同一技能后续改以体力支付");

            var fenyuanUser = AddUnit(manager, MakeData("焚元修士", (int)WorldEntity.Faction.PLAYER, 4), (int)WorldEntity.Faction.PLAYER);
            fenyuanUser.current_mana = 4;
            ApplyTestLoadoutMetadata(snapshot, fenyuanUser);
            manager.gongfa_combat.Begin(fenyuanUser);
            ApplyEquippedPassives(manager, fenyuanUser);
            var fenyuan = GD.Load<ActiveSkillData>(FenYuanC2Path) ?? throw new System.Exception("焚元化法资源读取失败");
            Check(manager.gongfa_combat.CanPlay(fenyuanUser, fenyuan), "法力上限至少2时焚元可施展");
            await Execute(manager, fenyuanUser, fenyuan, fenyuanUser);
            Check(fenyuanUser.max_mana == 3 && manager.get_battle_qi(fenyuanUser) == 2, "焚元实际燃烧上限并获得炁");
            var convertedAttack = GD.Load<ActiveSkillData>(ZheFengPath)!;
            manager.gongfa_combat.Before(fenyuanUser, convertedAttack);
            Check(manager.get_skill_mana_cost(fenyuanUser, convertedAttack) == 0 && manager.get_skill_energy_cost(fenyuanUser, convertedAttack) == 2,
                "焚元化法将本回合下一项法力费用改为体力");

            var guard = AddUnit(manager, MakeData("架招修士", (int)WorldEntity.Faction.PLAYER, 2), (int)WorldEntity.Faction.PLAYER);
            var shieldedEnemy = AddUnit(manager, MakeData("持盾对手", (int)WorldEntity.Faction.ENEMY, 0, ""), (int)WorldEntity.Faction.ENEMY);
            shieldedEnemy.Set("shield", 6);
            var jiaZhao = GD.Load<ActiveSkillData>(JiaZhaoC3Path) ?? throw new System.Exception("架招化法资源读取失败");
            manager.gongfa_combat.Begin(guard);
            manager.gongfa_combat.Before(guard, jiaZhao);
            manager.gongfa_combat.OnResponseTriggered(guard, jiaZhao);
            manager.gongfa_combat.ResponseResolved(guard, shieldedEnemy, 3, 0, false);
            manager.gongfa_combat.EndIncomingAttack(guard);
            Check(shieldedEnemy.Get("shield").AsInt32() == 3, "架招化法按实际抵消伤害削减攻击者护盾");

            var oneMana = AddUnit(manager, MakeData("长明修士", (int)WorldEntity.Faction.PLAYER, 1), (int)WorldEntity.Faction.PLAYER);
            Check(!manager.gongfa_combat.CanPlay(oneMana, GD.Load<ActiveSkillData>(FenYuanPath)!), "法力上限低于2时焚元不可施展");
            var zheFengC4 = GD.Load<ActiveSkillData>(ZheFengC4Path) ?? throw new System.Exception("长明化法资源读取失败");
            Check(!manager.can_pay_skill_mana_cost(oneMana, GD.Load<ActiveSkillData>(ZheFengPath)!), "法力上限为1时不能再燃烧支付法力费用");
            Check(manager.gongfa_combat.PreventsResponse(oneMana, zheFengC4), "法力上限为1时长明化法阻止应对");

            GD.Print($"ZHUO_JI_COMBAT_SMOKE: {_checks} checks passed");
            GetTree().Quit();
        }
        catch (System.Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
