using Godot;
using System;
using System.Threading.Tasks;

public partial class ActionCharacterAttributesSmoke : Node
{
    private int _checks;
    private void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); _checks++; }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        ActionCharacterBattleSession? session = null;
        try
        {
            var owner = new Character { Visible = false, entity_name = "正式人物属性验证", entity_data = new HumanData
            { entity_name = "正式人物属性验证", cultivation_realm = "云息一层", base_physique = 8, base_intelligence = 10, base_soul = 8 } };
            AddChild(owner);
            owner.current_health = owner.max_health - 17;
            var worldHealth = owner.current_health;
            var worldMana = owner.current_mana;
            var player = owner.get_snapshot_data();
            var enemy = (BattleEntityData)player.Duplicate(true);
            enemy.uid = "attribute-opponent";
            enemy.current_health = enemy.max_health = 1000000;
            var runtime = new ActionBattleAttributeRuntime(); AddChild(runtime);
            runtime.Configure(player, enemy);
            Check(runtime.get_state()["hp"].AsInt32() == worldHealth && runtime.get_state()["max_hp"].AsInt32() == owner.max_health, "读取当前生命与真实上限");
            Check(runtime.get_state()["precision"].AsInt32() == owner.precision && runtime.get_state()["dodge"].AsInt32() == owner.dodge, "精准身法来自真实人物派生值");
            Check(runtime.get_move_speed() == 235 && runtime.get_state()["mana"].AsInt32() == owner.max_mana, "确认移速与开战法力初始化");
            var tempered = (BattleEntityData)player.Duplicate(true);
            tempered.combat_tempering_by_gongfa["属性验收"] = new Godot.Collections.Dictionary
            { ["max_health_flat"] = 5f, ["precision_percent"] = 10f, ["dodge_percent"] = 20f, ["max_mana_yunxi"] = 1f };
            runtime.Configure(tempered, enemy);
            Check(runtime.get_state()["max_hp"].AsInt32() == player.max_health + 5 && runtime.get_state()["hp"].AsInt32() == worldHealth + 5, "开场修持加生命保留受伤绝对缺口");
            Check(runtime.get_state()["precision"].AsInt32() == Mathf.RoundToInt(player.precision * 1.1f) && runtime.get_state()["dodge"].AsInt32() == Mathf.RoundToInt(player.dodge * 1.2f), "开场精准身法修持实际生效");
            runtime.reset_battle();
            Check(runtime.get_state()["max_hp"].AsInt32() == player.max_health + 5 && runtime.get_state()["mana_capacity"].AsInt32() == player.max_mana + 1, "重开不重复累加修持且法力容量修持生效");
            int Hits(int precision, int dodge, int count, bool playerAttacks)
            {
                var a = (BattleEntityData)player.Duplicate(true);
                var b = (BattleEntityData)enemy.Duplicate(true);
                a.current_health = a.max_health = 1000000;
                a.precision = b.precision = precision;
                a.dodge = b.dodge = dodge;
                runtime.Configure(a, b);
                GD.Seed(1703);
                var hits = 0;
                for (var i = 0; i < count; i++)
                {
                    var result = runtime.resolve_attack(playerAttacks, playerAttacks ? "拳掌" : "撕咬", false);
                    if (!result["evaded"].AsBool()) hits++;
                    else Check(result["damage"].AsInt32() == 0 && !result["critical"].AsBool(), "闪避后无伤害无会心");
                }
                return hits;
            }
            var highPrecisionHits = Hits(10000, 10, 300, true);
            var highDodgeHits = Hits(1, 10000, 300, true);
            Check(highPrecisionHits > 290 && highDodgeHits < 15, "玩家精准提高命中、敌人身法提供闪避");
            Check(Hits(1, 10000, 300, false) < 15 && Hits(10000, 10, 300, false) > 290, "玩家身法与敌方精准也实际参与判定");
            Check(player.current_health == worldHealth && owner.current_health == worldHealth && owner.current_mana == worldMana, "连续结算不污染正式角色或输入快照");
            var defended = (BattleEntityData)player.Duplicate(true);
            defended.current_health = defended.max_health = 1000000;
            defended.physical_defense = 5000;
            defended.physical_damage_reduction = .5f;
            enemy.precision = 100000;
            runtime.Configure(defended, enemy);
            for (var i = 0; i < 30; i++)
            {
                var hit = runtime.resolve_attack(false, "撕咬", true);
                if (hit["evaded"].AsBool()) continue;
                Check(hit["guarded"].AsBool() && hit["blocked"].AsInt32() > 0 && hit["damage"].AsInt32() <= 1, "真实架招、物防与减伤结算");
            }
            runtime.QueueFree();

            var previous = GetTree().CurrentScene;
            session = ActionCharacterBattleSession.Open(owner);
            Check(session is not null && GetTree().Paused, "可从当前人物打开演练并暂停世界");
            var arena = GetTree().CurrentScene;
            arena.SetProcess(false);
            await Frame();
            Check(arena.Get("hp").AsInt32() == worldHealth && arena.Get("max_hp").AsInt32() == owner.max_health, "实际动作场景与HUD读取正式人物生命");
            Check(arena.Get("move_speed").AsSingle() == 235 && arena.Get("enemy_max_hp").AsInt32() != 100, "场景读取确认移速和正式兽种上限");
            var start = new Vector2(400, 500);
            arena.Set("player", start); arena.Set("enemy", new Vector2(1000, 400));
            arena.Call("move_character", Vector2.Right, .02);
            Check(Mathf.Abs(arena.Get("player").AsVector2().DistanceTo(start) - 4.7f) < .001f, "实际水平移动距离235乘0.02");
            arena.Set("player", start); arena.Call("move_character", new Vector2(1, -1), .02);
            Check(Mathf.Abs(arena.Get("player").AsVector2().DistanceTo(start) - 4.7f) < .001f, "实际斜向移动不额外加速");
            arena.Set("enemy", new Vector2(450, 500));
            arena.Set("invulnerable", .15);
            arena.Call("receive_wolf_bite");
            Check(arena.Get("hp").AsInt32() == worldHealth, "主动错步无敌独立阻止来袭伤害");
            arena.Set("invulnerable", 0);
            var bites = 0;
            while (arena.Get("hp").AsInt32() == worldHealth && bites++ < 50) arena.Call("receive_wolf_bite");
            Check(arena.Get("hp").AsInt32() < worldHealth, "实际场景命中后扣除正式属性生命");
            Check(owner.current_health == worldHealth && owner.current_mana == worldMana, "实际演练也不回写正式进度");
            arena.Set("pending_attack", "拳掌"); arena.Set("attack_direction", Vector2.Right);
            arena.Set("enemy", new Vector2(1200, 500));
            var enemyHealth = arena.Get("enemy_hp").AsInt32();
            arena.Call("resolve_pending_attack");
            Check(arena.Get("enemy_hp").AsInt32() == enemyHealth && arena.Get("feedback").AsString().Contains("落空"), "空间落空不进入属性伤害判定");
            var attributes = arena.Get("attribute_runtime").AsGodotObject()!;
            var sawHit = false; var sawEvade = false;
            GD.Seed(1703);
            for (var i = 0; i < 100 && !(sawHit && sawEvade); i++)
            {
                attributes.Call("reset_battle"); arena.Call("sync_character_attributes");
                arena.Set("enemy", new Vector2(450, 500));
                arena.Set("pending_attack", "拳掌"); arena.Call("resolve_pending_attack");
                sawHit |= arena.Get("enemy_hp").AsInt32() < arena.Get("enemy_max_hp").AsInt32();
                sawEvade |= arena.Get("feedback").AsString().Contains("属性闪避");
                var afterHit = arena.Get("enemy_hp").AsInt32();
                arena.Call("resolve_pending_attack");
                Check(arena.Get("enemy_hp").AsInt32() == afterHit, "同次攻击清空后不重复判定或扣血");
            }
            Check(sawHit && sawEvade, "实际施放路径同时产生真实命中与属性闪避反馈");
            arena.Set("enemy", new Vector2(620, 500));
            arena.Set("hurt_visual", 0);
            arena.Call("update_skinned_player"); arena.Call("_update_trial_camera");
            ((Node2D)arena).QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using (var image = GetViewport().GetTexture().GetImage()) image.SavePng("res://output/action_character_attributes_20261103.png");
            // Exercise the real Escape branch, including suppression of the global world menu.
            arena.Call("_input", new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await Frame();
            Check(GetTree().CurrentScene == previous && !GetTree().Paused, "Esc恢复原场景和暂停状态，不返回首页或丢失旅程");
            Check(owner.current_health == worldHealth, "返回后角色生命保持");
            session = null;
            GD.Print($"ACTION_CHARACTER_ATTRIBUTES_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error)
        {
            session?.Close(); GD.PushError(error.ToString()); GetTree().Quit(1);
        }
    }
}
