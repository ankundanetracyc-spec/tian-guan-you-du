using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>凝核牌面与长按详情使用同一套现行数值，结算仍由牌局负责。</summary>
public static class CondensationCardText
{
    public static string Wording(string text) => text.Replace("本轮", "本回合").Replace("每轮", "每回合").Replace("末轮", "最后一回合");

    public static string Face(string name, CondensationCardGame? game, int index = -1)
    {
        var card = CondensationCardGame.Definition(name);
        if (card.Kind == "压聚")
        {
            var spec = CondensationCardGame.PressureBase(name);
            var charge = game?.ChargeActive == true ? 1 : 0;
            var gain = spec.Gain + (game?.RootBonus ?? 0) + charge * 8;
            var disorder = spec.Disorder + charge;
            if (game is not null && Simulate(game, name, index, out var copy, out _) && copy is not null)
                disorder = copy.Disorder - game.Disorder;
            var condition = name == "叠压" ? "须接压聚\n" : name == "息压" ? "紊乱不高于2\n" : "";
            return $"{condition}凝核  +{gain}\n紊乱  +{disorder}\n{spec.Delay}回合后  回弹{spec.Strength + charge}";
        }
        var wisdom = game?.InsightBonus ?? 0;
        return name switch
        {
            "引回" => "回合末引回全部回弹\n两股以上额外 +4\n每回合限1张",
            "缓行" => "一股回弹延后1回合\n定形时仍须结清",
            "催回" => "一股未来回弹\n提前1回合",
            "并流" => "较早的回弹移至\n较晚的回合\n须选不同回合的\n两股回弹",
            "蓄劲" => "下一张压聚\n凝核+8\n紊乱+1 · 回弹+1\n最多保留1张",
            "随势" => "须先施展引回\n每股额外凝核+2\n最多+6\n每回合限1张",
            "匀气" => "凝核  +1\n抽2张 · 弃1张",
            "护气" => "凝核  +1\n未引回回弹损失 −3",
            "归拢" => "凝核  +1\n消解1股回弹\n不算引回",
            "理气" => $"凝核  −3\n紊乱  −{4 + wisdom}\n最低降至0",
            "回旋" => $"引回本回合1股回弹\n紊乱  −{2 + wisdom}",
            "分引" => "1股回弹拆为两股\n须强度至少为2",
            "观气" => "查看牌库顶3张\n选1张入手\n其余按原顺序置底",
            _ => Wording(card.Description)
        };
    }

    public static string Details(string name, CondensationCardGame? game, int index = -1)
    {
        var card = CondensationCardGame.Definition(name);
        if (game is null) return "施展消耗1次。\n" + Wording(card.Description);
        var lines = new List<string> { "施展消耗1次。" };
        var canSimulate = Simulate(game, name, index, out var after, out var unavailable);
        var reversal = canSimulate && after is not null && after.Log.Skip(game.Log.Count).Any(line => line.Contains("反复涨降"));
        if (card.Kind == "压聚")
        {
            var spec = CondensationCardGame.PressureBase(name);
            var charged = game.ChargeActive ? 1 : 0;
            var total = spec.Gain + game.RootBonus + charged * 8;
            var gainFormula = $"基础{spec.Gain}＋灵根{game.RootBonus}" + (charged > 0 ? "＋蓄劲8" : "") + $"＝{total}";
            lines.Add($"凝核增加{total}。计算公式：{gainFormula}。");
            var disorder = spec.Disorder + charged + (reversal ? 1 : 0);
            var disorderFormula = $"基础{spec.Disorder}" + (charged > 0 ? "＋蓄劲1" : "") + (reversal ? "＋反复涨降1" : "") + $"＝{disorder}";
            lines.Add(charged > 0 || reversal ? $"紊乱增加{disorder}。计算公式：{disorderFormula}。" : $"紊乱增加{disorder}。");
            if (!canSimulate) lines.Add("若凝核反复涨降，还会额外增加1点紊乱。");
            var strength = spec.Strength + charged;
            lines.Add(charged > 0
                ? $"第{game.Round + spec.Delay}回合回弹{strength}。计算公式：基础{spec.Strength}＋蓄劲1＝{strength}。"
                : $"第{game.Round + spec.Delay}回合回弹{strength}。");
            var tier = game.RootWeight < 34 ? "低于34%" : game.RootWeight < 67 ? "34%—66%" : game.RootWeight < 100 ? "67%—99%" : "100%";
            lines.Add($"灵根补正+{game.RootBonus}。计算方式：匹配权重{game.RootWeight}%，{tier}。");
            if (name == "叠压") lines.Add("须紧接压聚施展。");
            if (name == "息压") lines.Add("当前紊乱不高于2时才能施展。");
        }
        else if (name is "理气" or "回旋")
        {
            if (name == "理气")
            {
                var score = Math.Max(0, game.Score - 3);
                lines.Add($"凝核降至{score}。计算公式：当前{game.Score}－3，最低为0＝{score}。");
            }
            else
            {
                var targets = game.LegalTargets(name).ToArray();
                if (targets.Length == 1) lines.Add($"立即引回本回合回弹，凝核增加{targets[0].Strength}。计算方式：回弹强度{targets[0].Strength}。");
                else lines.Add("立即引回所选的本回合回弹，凝核增加其强度；选择后确定。");
            }
            var basis = name == "理气" ? 4 : 2;
            var reduced = Math.Max(0, game.Disorder - basis - game.InsightBonus);
            lines.Add($"紊乱降至{reduced}。计算公式：当前{game.Disorder}－基础{basis}－灵慧补正{game.InsightBonus}，最低为0＝{reduced}。");
            lines.Add($"灵慧补正+{game.InsightBonus}。计算方式：灵慧{game.Intelligence}对应+{game.InsightBonus}。");
        }
        else
        {
            lines.Add(Wording(card.Description));
            var current = game.Recoils.Where(r => r.Round == game.Round).ToArray();
            var strength = current.Sum(r => r.Strength);
            if (name == "引回")
            {
                var bonus = current.Length >= 2 ? 4 : 0;
                lines.Add($"按当前回弹，引回可得{strength + bonus}。计算公式：回弹强度{strength}＋多股奖励{bonus}＝{strength + bonus}。");
                lines.Add("回弹变化后，回合末收益也会变化。");
            }
            else if (name == "随势")
            {
                var bonus = Math.Min(6, current.Length * 2);
                lines.Add($"当前回弹全部引回可额外获得{bonus}。计算公式：{current.Length}股×2，最高6＝{bonus}。");
                lines.Add("回弹变化后，回合末收益也会变化。");
            }
            else if (name == "匀气")
            {
                var drawn = Math.Min(2, game.Deck.Count);
                lines.Add($"当前可抽{drawn}张。计算公式：牌库{game.Deck.Count}张，最多抽2张＝{drawn}。");
                if (drawn == 0) lines.Add("未抽到牌，无需弃牌。");
            }
            else if (name == "观气") lines.Add($"当前可查看{Math.Min(3, game.Deck.Count)}张。");
        }
        if (card.Kind != "压聚" && after is not null)
        {
            foreach (var old in game.Recoils)
            {
                var current = after.Recoils.FirstOrDefault(r => r.Id == old.Id);
                if (current is not null && current.Round != old.Round)
                    lines.Add($"回弹{old.Strength}：第{old.Round}回合→第{current.Round}回合。");
            }
            if (name == "归拢")
            {
                var removed = game.Recoils.FirstOrDefault(old => after.Recoils.All(r => r.Id != old.Id));
                if (removed is not null) lines.Add($"消解第{removed.Round}回合回弹{removed.Strength}。");
            }
            if (name == "分引")
            {
                var added = after.Recoils.Where(r => game.Recoils.All(old => old.Id != r.Id)).ToArray();
                if (added.Length == 2) lines.Add($"分为回弹{added[0].Strength}和{added[1].Strength}，合计{added.Sum(r => r.Strength)}。");
            }
        }
        if (card.Kind != "压聚" && reversal) lines.Add($"本次凝核反复涨降，额外紊乱+1，实际紊乱{after!.Disorder}。");
        if (after?.Failed == true) lines.Add("警告：施展后紊乱超过10，立即失败。");
        if (!string.IsNullOrEmpty(unavailable)) lines.Add("当前无法施展：" + Wording(unavailable));
        return string.Join("\n", lines);
    }

    // 多目标只核验是否存在合法选择，不预先替玩家选定目标。
    private static bool Simulate(CondensationCardGame game, string name, int index, out CondensationCardGame? after, out string unavailable)
    {
        after = null; unavailable = "";
        if (index < 0 || index >= game.Hand.Count || game.Hand[index] != name) return false;
        var count = game.TargetCount(name);
        var legal = game.LegalTargets(name).ToArray();
        var options = new List<int[]>();
        if (count == 0) options.Add([]);
        else if (count == 1) options.AddRange(legal.Select(r => new[] { r.Id }));
        else
            for (var i = 0; i < legal.Length; i++)
                for (var j = i + 1; j < legal.Length; j++)
                    if (legal[i].Round != legal[j].Round) options.Add([legal[i].Id, legal[j].Id]);
        if (options.Count == 0)
        {
            unavailable = game.CanPlay(name, index, []);
            if (unavailable == "请选择所需回弹目标。") unavailable = count == 2 ? "需要到达回合不同的两股回弹。" : "目前没有可选回弹。";
            return false;
        }
        unavailable = game.CanPlay(name, index, options[0]);
        if (unavailable != "") return false;
        if (options.Count > 1) return false;
        after = game.Copy();
        var error = after.Play(name, index, options[0]);
        if (error == "") return true;
        after = null; unavailable = error; return false;
    }
}
