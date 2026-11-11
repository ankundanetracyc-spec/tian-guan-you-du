using Godot;
using System;
using System.Linq;

public partial class CondensationCardGamePanel
{
    private void UpdateStatusHover(CondensationCardGame game, CondensationCardGame.Recoil[] current)
    {
        var squared = game.Disorder * game.Disorder;
        var loss = CondensationCardGame.DisorderLoss(game.Disorder);
        var remainder = squared % 4;
        var disorder = $"当前紊乱{game.Disorder} / 10。\n按当前紊乱，本回合结束扣除{loss}点凝核。计算公式：{game.Disorder}×{game.Disorder}＝{squared}，{squared}÷4＝{loss}" +
            (remainder > 0 ? $"余{remainder}" : "") + "。\n随后紊乱降低1；超过10立即失败。";
        HoverInfoManager.SetInfo(_disorder, new HoverInfoManager.HoverInfoContent("气息紊乱", disorder, ShowRules: false));

        var strength = current.Sum(r => r.Strength);
        string result;
        if (game.ReturnActive)
        {
            var multi = current.Length >= 2 ? 4 : 0;
            var follow = game.FollowActive ? Math.Min(6, current.Length * 2) : 0;
            result = $"按当前回弹，回合末引回{strength + multi + follow}点凝核。计算公式：回弹{strength}＋多股奖励{multi}" +
                (game.FollowActive ? $"＋随势{follow}" : "") + $"＝{strength + multi + follow}。";
        }
        else
        {
            var potential = Math.Max(0, strength - game.Protection);
            var actual = Math.Min(game.Score, potential);
            result = $"未引回的基础扣除为{potential}。计算公式：回弹{strength}－护持{game.Protection}，最低0＝{potential}。\n" +
                $"按当前凝核，实际扣除{actual}。计算公式：取基础扣除{potential}和当前凝核{game.Score}中较小值＝{actual}。";
        }
        var recoil = $"本回合到达{current.Length}股回弹，强度合计{strength}。\n{result}\n上方回合轨道显示后续到达的回弹。";
        HoverInfoManager.SetInfo(_recoilInfo, new HoverInfoManager.HoverInfoContent("本回合回弹", recoil, ShowRules: false));
    }
}
