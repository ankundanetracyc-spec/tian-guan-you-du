using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Authored East Woods encounters. Event outcomes are independent from battle victory.</summary>
public static partial class ForestWolfEvent
{
    public const string NodeId = "root_hollow";
    public const string StateKey = "__forest_wolf_events";
    public const float Hours = .5f, Stamina = 1f;
    public sealed record Choice(string Id, string Text, string Hint = "", string Explanation = "");
    public sealed record Page(string Title, string Body, Choice[] Choices, bool Quiet = false, string Note = "");
    public static bool Supports(string node) => node is NodeId or "outer_08_08" or "outer_13_06";
    public static string Key(EastWoodsActions.ActionDef action, string node = NodeId)
        => node + ":" + (string.IsNullOrEmpty(action.EncounterIdentity) ? "initial" : action.EncounterIdentity);
    public static GDictionary Get(GDictionary actions, string key)
        => actions.GetValueOrDefault(StateKey, new GDictionary()).AsGodotDictionary().GetValueOrDefault(key, new GDictionary()).AsGodotDictionary();
    public static void Store(GDictionary actions, GDictionary record)
    {
        var events = actions.GetValueOrDefault(StateKey, new GDictionary()).AsGodotDictionary();
        var oldStage = Get(actions, record["key"].AsString()).GetValueOrDefault("stage", "").AsString();
        events[record["key"].AsString()] = record;
        actions[StateKey] = events;
        var stage = record["stage"].AsString();
        // Records can share their dictionary with the panel: track publication separately.
        var published = record.GetValueOrDefault("published_stage", oldStage).AsString();
        if (published != stage && (stage is "retreated" or "drive_success" || stage.StartsWith("battle_", StringComparison.Ordinal)))
            actions["__forest_wolf_last_contact"] = new GDictionary { ["node"] = record["node"], ["stage"] = stage };
        record["published_stage"] = stage;
    }
    public static GDictionary Create(EastWoodsActions.ActionDef action, BattleEntityData wolf, string node = NodeId) => new()
    {
        ["key"] = Key(action, node), ["node"] = node, ["action"] = action.Id,
        ["encounter_id"] = action.EncounterIdentity, ["wolf"] = wolf,
        ["stage"] = "opening", ["observation"] = "", ["drive"] = "", ["outcome"] = "active", ["window_open"] = false,
    };
    public static bool CanAttempt(GDictionary record, string choice)
        => record.GetValueOrDefault("outcome", "active").AsString() == "active" &&
            record.GetValueOrDefault("drive", "").AsString() == "" &&
            (choice == "drive" || choice == "observe" && record.GetValueOrDefault("observation", "").AsString() == "");
    public static string ResumeStage(GDictionary record)
        => record.GetValueOrDefault("drive", "").AsString() == "failure" ? "drive_failure"
            : record.GetValueOrDefault("observation", "").AsString() switch
            { "success" => "observe_success", "failure" => "observe_failure", _ => "opening" };
    public static BattleEntityData? Wolf(GDictionary record)
        => record.GetValueOrDefault("wolf", default(Variant)).AsGodotObject() as BattleEntityData;
    public static double Chance(GDictionary record, string choice, BattleEntityData player)
    {
        var wolf = Wolf(record) ?? throw new InvalidOperationException("本次遭遇的凶狼资料尚未就绪。");
        var values = new[] { player.physique, player.soul, player.intelligence, wolf.physique, wolf.soul };
        foreach (var value in values) if (!float.IsFinite(value) || value < 0) throw new InvalidOperationException("本次判定的属性资料无效。");
        double a, b, modifier = 0;
        if (choice == "observe") { a = .7 * player.soul + .3 * player.intelligence; b = wolf.soul; }
        else if (choice == "drive")
        {
            a = .6 * player.physique + .4 * player.soul; b = .6 * wolf.physique + .4 * wolf.soul;
            modifier = record.GetValueOrDefault("observation", "").AsString() switch { "success" => .15, "failure" => -.10, _ => 0 };
        }
        else throw new ArgumentException("未知的事件判定。", nameof(choice));
        if (a + b <= 0) throw new InvalidOperationException("本次判定的属性资料无效。");
        return Math.Clamp(a / (a + b) + modifier, .05, .95);
    }
    public static bool Resolve(GDictionary record, string choice, BattleEntityData player, double sample)
    {
        if (!CanAttempt(record, choice)) return false;
        if (!double.IsFinite(sample) || sample < 0 || sample >= 1) throw new ArgumentOutOfRangeException(nameof(sample));
        var probability = Chance(record, choice, player);
        var success = sample < probability;
        record[choice == "observe" ? "observation" : "drive"] = success ? "success" : "failure";
        record[choice + "_probability"] = probability; record[choice + "_sample"] = sample;
        record[choice + "_player_physique"] = player.physique;
        record[choice + "_player_soul"] = player.soul;
        record[choice + "_player_intelligence"] = player.intelligence;
        record["stage"] = choice + (success ? "_success" : "_failure");
        if (choice == "drive" && success) record["outcome"] = "driven_off";
        return true;
    }
    private static string Hint(GDictionary record, string choice, BattleEntityData player)
    {
        var chance = Chance(record, choice, player);
        var approach = choice == "observe" ? "凝神细察，辨清它的动静与迟疑。" : "站稳身形，以沉稳的声势迫它退让。";
        var effects = choice == "observe" ? "成功后驱赶机会提高15个百分点；失败后降低10个百分点。" : "成功将狼驱离，不获得击杀奖励；失败后仍可迎战或退开。";
        var wolf = Wolf(record)!;
        var a = choice == "observe" ? .7 * player.soul + .3 * player.intelligence : .6 * player.physique + .4 * player.soul;
        var b = choice == "observe" ? wolf.soul : .6 * wolf.physique + .4 * wolf.soul;
        var modifier = choice == "observe" ? 0 : record.GetValueOrDefault("observation", "").AsString() switch { "success" => 15, "failure" => -10, _ => 0 };
        var playerFormula = choice == "observe"
            ? $"神魂[{HintNumber(player.soul)}]×0.7＋灵慧[{HintNumber(player.intelligence)}]×0.3"
            : $"体魄[{HintNumber(player.physique)}]×0.6＋神魂[{HintNumber(player.soul)}]×0.4";
        var wolfFormula = choice == "observe" ? $"凶狼神魂[{HintNumber(wolf.soul)}]"
            : $"凶狼体魄[{HintNumber(wolf.physique)}]×0.6＋凶狼神魂[{HintNumber(wolf.soul)}]×0.4";
        var adjustment = modifier > 0 ? $"＋{modifier}%" : modifier < 0 ? $"－{-modifier}%" : "";
        var percent = Math.Round(chance * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        // Keep the complete source independent of the selected display mode. The
        // shared hover reader hides this block in simple mode and can restore it later.
        return $"{approach}\n耗时0.5小时 · 精力1\n{effects}\n\n计算来源\n"
            + $"人物评分：{HintNumber(a)} = {playerFormula}\n\n"
            + $"凶狼评分：{HintNumber(b)} = {wolfFormula}\n\n"
            + $"成功率：{percent}% = min(95%,max(5%,人物评分[{HintNumber(a)}]÷(人物评分[{HintNumber(a)}]＋凶狼评分[{HintNumber(b)}])×100%{adjustment}))";
    }
    private static string HintNumber(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static Page DescribeBase(GDictionary record, BattleEntityData player)
    {
        var stage = record.GetValueOrDefault("stage", "opening").AsString();
        var fight = new Choice("fight", "迎战", "先查看战前情报，明确开始后才进入正式战斗；取消不额外消耗。");
        var retreat = new Choice("retreat", "缓缓退开", "结束接触，留在当前地格；不清除狼，不额外消耗时间或精力。");
        if (stage == "drive_success") return new("凶狼退去", "你踏稳脚步，朝盘根旁喝出一声。凶狼僵了片刻，终于收回前爪，低吼着退入树影。\n\n枝叶摇晃了几下，便归于沉寂。这一处暂时安静下来。", [new("continue", "继续前行")], true);
        if (stage == "retreated") return new("暂避锋芒", "你收住动作，缓缓拉开与它的距离。凶狼仍伏在盘根旁，目光追着你，却没有扑来。\n\n这场接触暂且告一段落。是否再来，由你自己决定。", [new("continue", "回到林径")]);
        if (stage.StartsWith("battle_", StringComparison.Ordinal)) return stage switch
        {
            "battle_win" => new("林间归静", "凶狼终于倒在盘根旁。你稍稍平复气息，林间只剩下风穿过枝叶的声音。", [new("continue", "回到林径")], true),
            "battle_escape" => new("脱离纠缠", "你摆脱了凶狼的纠缠，重新站稳脚步。树影里仍传来它的低吼，这一处并未平息。", [new("continue", "回到林径")]),
            "battle_draw" => new("胜负未分", "这场交锋未能分出胜负。你重新站稳脚步，盘根旁仍有凶狼活动。", [new("continue", "回到林径")]),
            _ => new("暂避凶锋", "这次交锋没能取胜。你收拾气息，眼下的伤势与消耗仍需自行料理。", [new("continue", "回到林径")]),
        };
        var choices = new List<Choice>();
        if (stage != "observe_success") choices.Add(fight);
        var node = record.GetValueOrDefault("node", NodeId).AsString();
        if (CanAttempt(record, "observe")) choices.Add(new("observe", node switch { "outer_08_08" => "辨听脚步", "outer_13_06" => "观察草动", _ => "屏息观察" }, Hint(record, "observe", player),
            "神魂、灵慧"));
        if (CanAttempt(record, "drive")) choices.Add(new("drive", node switch { "outer_08_08" => "反身喝止", "outer_13_06" => "踏步逼退", _ => "扬声驱赶" }, Hint(record, "drive", player),
            "体魄、神魂"));
        if (stage == "observe_success") choices.Add(fight);
        choices.Add(retreat);
        return stage switch
        {
            "observe_success" => new("看准进退", "你稳住呼吸，留意它每一次抬爪与停顿。凶狼虽然龇着牙，却始终没有离开盘根旁的阴影。\n\n它也在试探你。看准它迟疑的瞬间，也许能将它逼退。", choices.ToArray(), false, "看准了它的迟疑，驱赶机会提高。"),
            "observe_failure" => new("露了声息", "衣袖擦过枝叶，发出一声轻响。凶狼的目光骤然一紧，獠牙间挤出低低的咆哮。\n\n它已留意到你的试探，再想将它吓退，恐怕更难了。", choices.ToArray(), false, "它已留意你的试探，驱赶机会降低。"),
            "drive_failure" => new("逼近的低吼", "你的喝声在林间散开，凶狼却没有退去。它压低头，向前迈出一步，爪尖刮过湿土。\n\n它不肯让步。继续虚张声势已没有意义。", choices.ToArray()),
            _ => new("林中遭遇", "枝叶忽然一晃，一头凶狼从盘根后现出身形。它没有立刻扑来，只伏低身子，目光紧盯着你。\n\n你停下脚步。蕨叶间传来粗重的鼻息，来时的林径仍在身后。", choices.ToArray()),
        };
    }
}
