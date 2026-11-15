using Godot;
using System.Reflection;
using System.Text.Json;

/// <summary>Isolated, constructed characters; never loads or saves a player slot.</summary>
public partial class CloudCombatSnapshotProbe : Node
{
    public override void _Ready() => CallDeferred(nameof(Run));

    private void Run()
    {
        try
        {
            var realms = GetNode<RealmManager>("/root/RealmManager");
            var board = new BattleManager();
            var resolveInitializer = typeof(BattleManager).GetMethod("InitializeBoardResolve", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The live board resolve initializer is unavailable.");
            var action = new ActionBattleAttributeRuntime();
            var rows = new List<object>();
            var names = new[] { "云息一层", "云息五层", "云息七层", "云息十层" };
            foreach (var realm in names)
            {
                if (realms.get_realm_config(realm) is null) throw new InvalidOperationException($"Missing live realm config: {realm}");
                var hasCore = realm is "云息七层" or "云息十层";
                var owner = new Character
                {
                    Visible = false,
                    ProcessMode = ProcessModeEnum.Disabled,
                    entity_data = new HumanData
                    {
                        entity_name = "临时快照角色", cultivation_realm = realm,
                        base_physique = 5, base_intelligence = 5, base_soul = 5,
                        has_spirit_core = hasCore, condensation_final_score = hasCore ? 100 : 0,
                        core_count = hasCore ? 1 : 0,
                    }
                };
                AddChild(owner);
                if (!owner.IsInsideTree()) throw new InvalidOperationException("Character must enter the tree before reading realm bonuses.");
                owner.recalculate_attributes();
                owner.current_health = owner.max_health;
                resolveInitializer.Invoke(board, [owner]);
                var snap = owner.get_snapshot_data();
                ActionBattleAttributeRuntime.InitializeActionSnapshot(snap);
                rows.Add(new {
                    realm, has_core = hasCore, condensation_score = hasCore ? 100 : 0,
                    physique = owner.physique, intelligence = owner.intelligence, soul = owner.soul,
                    hp = owner.max_health, board_resolve = board.get_board_resolve_max(owner),
                    action_start_stamina = snap.current_stamina, action_max_stamina = snap.max_stamina,
                    action_stamina_recovery_seconds = 1f / action.get_stamina_recovery_base(),
                    legacy_energy_recovery_frames = owner.get_energy_recovery_interval(),
                    legacy_energy_recovery_seconds = owner.get_energy_recovery_interval() / StatusEffect.CombatFramesPerSecond,
                    mana_max = snap.max_mana,
                });
                owner.Free();
            }
            var bookRows = new List<object>();
            var xuan = GD.Load<GongfaResource>("res://resources/gongfa/玄枢含章经.tres");
            var zhuo = GD.Load<GongfaResource>("res://resources/gongfa/斫基损契玄经.tres");
            if (!GongfaAvailability.IsEnabled(xuan) || !GongfaAvailability.IsEnabled(zhuo))
                throw new InvalidOperationException("Required sample books are not enabled.");
            foreach (var realm in new[] { "云息五层", "云息七层" })
            {
                var hasCore = realm == "云息七层";
                var data = new HumanData {
                    entity_name = "临时功法快照角色", cultivation_realm = realm,
                    base_physique = 5, base_intelligence = 5, base_soul = 5,
                    has_spirit_core = hasCore, condensation_final_score = hasCore ? 100 : 0,
                    core_count = hasCore ? 1 : 0,
                };
                // A constructed learned-first-chapter state, not completed study or a player save.
                data.learned_gongfa_paths.Add(xuan.ResourcePath);
                data.learned_gongfa_paths.Add(zhuo.ResourcePath);
                data.learned_gongfa_chapters[xuan.ResourcePath] = new Godot.Collections.Array<string> { "shou_shu_1" };
                data.learned_gongfa_chapters[zhuo.ResourcePath] = new Godot.Collections.Array<string> { "zhuo_ji_1" };
                var owner = new Character { Visible = false, ProcessMode = ProcessModeEnum.Disabled, entity_data = data };
                AddChild(owner);
                if (!owner.equip_main_gongfa(xuan) || !owner.add_sub_gongfa(zhuo))
                    throw new InvalidOperationException($"Could not equip the actual sample books: {realm}");
                var skill = owner.get_available_battle_skills().FirstOrDefault(s => s.skill_name == "惊蛰")
                    ?? throw new InvalidOperationException("The learned first chapter did not unlock 惊蛰.");
                owner.current_health = owner.max_health;
                resolveInitializer.Invoke(board, [owner]);
                var snap = owner.get_snapshot_data();
                ActionBattleAttributeRuntime.InitializeActionSnapshot(snap);
                var body = new Creature { entity_data = new LivingData() };
                body.apply_battle_data(snap);
                var unit = new BattleUnit(); unit.setup(body, null); board.battle_units[body] = unit;
                var qi = board.get_battle_qi(body);
                var canPay = board.can_pay_skill_mana_cost(body, skill);
                if (snap.max_mana <= 0 || snap.current_mana < skill.mana_cost || !canPay || qi != 0)
                    throw new InvalidOperationException("Expected actual equipped-book snapshot to afford 惊蛰 at battle start.");
                bookRows.Add(new {
                    realm, main_book = xuan.gongfa_name, sub_book = zhuo.gongfa_name,
                    assumed_learned_chapters = new[] { "shou_shu_1", "zhuo_ji_1" },
                    physique = owner.physique, intelligence = owner.intelligence, soul = owner.soul,
                    body_proficiency = snap.body_proficiency, sword_proficiency = snap.sword_proficiency,
                    magic_proficiency = snap.magic_proficiency, hp = snap.max_health,
                    board_resolve = board.get_board_resolve_max(owner),
                    action_start_stamina = snap.current_stamina, action_max_stamina = snap.max_stamina,
                    mana_start = snap.current_mana, mana_max = snap.max_mana, qi_start = qi,
                    jingzhe_unlocked = true, jingzhe_mana_cost = skill.mana_cost, jingzhe_can_pay = canPay,
                });
                board.battle_units.Remove(body); body.Free(); owner.Free();
            }
            board.Free(); action.Free();
            GD.Print("CLOUD_COMBAT_SNAPSHOT_JSON=" + JsonSerializer.Serialize(new {
                provenance = "constructed temporary characters, not player saves; rows have no equipment or books; equipped_book_rows assume two equipped books and learned first chapters",
                base_physique = 5, base_intelligence = 5, base_soul = 5,
                core_note = "7/10 use a constructed 100-point core (zero flat bonus); not a claim about saved characters",
                resolve_note = "actual private live board initializer; board currently restores full resolve each round",
                action_stamina_note = "actual action battle initializes 6 stamina and recovers 1 per 2 seconds",
                legacy_recovery_note = "separate legacy battle interval, 60 simulation frames per second; not the action or proposed turn-based recovery",
                rows, equipped_book_rows = bookRows,
            }));
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
