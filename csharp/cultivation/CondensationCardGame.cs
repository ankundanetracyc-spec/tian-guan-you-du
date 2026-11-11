using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>正式凝核与试演共用的八回合规则；人物写回由正式入口负责。</summary>
public sealed class CondensationCardGame
{
    public sealed record Card(string Name, string Kind, string Description, int Count);
    public sealed record Recoil(int Id, int Round, int Strength);
    public readonly record struct PressureSpec(int Gain, int Disorder, int Strength, int Delay);
    public static PressureSpec PressureBase(string name) => name switch
    {
        "轻压" => new(10, 1, 2, 2), "重压" => new(18, 3, 4, 1), "叠压" => new(15, 2, 3, 1),
        "徐压" => new(8, 0, 1, 2), _ => new(15, 1, 2, 2)
    };
    public static readonly Card[] Pool =
    [
        new("轻压", "压聚", "凝核+10，紊乱+1；隔两回合回弹2", 4),
        new("重压", "压聚", "凝核+18，紊乱+3；下一回合回弹4", 3),
        new("叠压", "压聚", "上一出手须为压聚；凝核+15，紊乱+2；下一回合回弹3", 3),
        new("徐压", "压聚", "凝核+8，不增紊乱；隔两回合回弹1", 2),
        new("息压", "压聚", "紊乱≤2才能施展；凝核+15，紊乱+1；隔两回合回弹2", 1),
        new("引回", "引回", "本轮末引回全部到达回弹，按强度增加凝核；两股以上额外+4。每轮限一张", 4),
        new("缓行", "调度", "一股回弹延后一回合；末轮仍须结清", 2),
        new("催回", "调度", "一股未来回弹提前一回合，最早到本轮", 2),
        new("并流", "调度", "选到达回合不同的两股回弹，较早者移到较晚者；仍为两股", 1),
        new("蓄劲", "蓄势", "下一张压聚额外凝核+8、回弹强度+1、紊乱+1；最多保留一张", 2),
        new("随势", "蓄势", "本轮先施展引回；本轮末每引回一股额外凝核+2，最多+6。每轮限一张", 2),
        new("匀气", "整理", "凝核+1；抽2弃1，随后检查六张手牌上限", 2),
        new("护气", "护持", "凝核+1；本轮未引回的回弹总损失减少3，可叠加", 2),
        new("归拢", "护持", "消解一股当前或未来回弹，凝核+1；不算引回", 2),
        new("理气", "理气", "凝核-3；紊乱降低4＋灵慧补正，不处理回弹", 2),
        new("回旋", "理气", "立即引回一股本轮回弹，凝核增加其强度；紊乱降低2＋灵慧补正", 1),
        new("分引", "调度", "强度至少2的一股回弹拆成两股，总强度、到达回合不变", 0),
        new("观气", "整理", "确认后查看牌库顶三张，选一张入手，其余按原顺序置底", 1)
    ];
    public static readonly string[] Items = ["定息丹", "聚气丹", "导气符", "观息符", "清息丹"];
    public static string ItemText(string name) => name switch
    {
        "定息丹" => "本轮未引回的回弹损失减少4；不保护末轮未结回弹",
        "聚气丹" => "凝核+10，无回弹；不吃灵根补正，参与涨降判断",
        "导气符" => "选择一股回弹，提前或延后一回合，最早到本轮",
        "观息符" => "确认后查看牌库顶三张，选一入手，其余按原顺序置底",
        _ => "紊乱降低5，不增加凝核，不吃灵慧补正"
    };
    public int Score { get; private set; }
    public int Disorder { get; private set; }
    public int Round { get; private set; } = 1;
    public int Actions { get; private set; } = 3;
    public bool Failed { get; private set; }
    public bool Finished { get; private set; }
    public bool SwapOpen { get; private set; } = true;
    public bool OrdinarySwapUsed { get; private set; }
    public int ExtraSwaps { get; private set; }
    public bool ReturnActive { get; private set; }
    public bool FollowActive { get; private set; }
    public bool ChargeActive { get; private set; }
    public bool LastPressure { get; private set; }
    public bool ItemUsedThisRound { get; private set; }
    public int Protection { get; private set; }
    public int InsightBonus { get; }
    public int RootBonus { get; }
    public int Intelligence { get; }
    public int Comprehension { get; }
    public int RootWeight { get; }
    public int ForcedDiscards { get; private set; }
    public List<string> Hand { get; private set; } = [];
    public List<string> Deck { get; private set; } = [];
    public List<string> Discard { get; private set; } = [];
    public List<string> Choices { get; private set; } = [];
    public List<Recoil> Recoils { get; private set; } = [];
    public List<string> EquippedItems { get; private set; } = [];
    public List<string> UsedItems { get; private set; } = [];
    public List<string> Log { get; private set; } = [];
    private List<int> _directions = [];
    private int _nextRecoil;
    private readonly int _openingShuffleSeed;
    public bool Busy => Choices.Count > 0 || ForcedDiscards > 0;
    public static int DisorderLoss(int value) => value * value / 4;
    public static Card Definition(string name) => Pool.First(c => c.Name == name);

    public CondensationCardGame(int comprehension, int intelligence, int rootWeight,
        IReadOnlyDictionary<string, int> counts, IEnumerable<string> items, int? seed = null)
    {
        Comprehension = comprehension; Intelligence = intelligence; RootWeight = rootWeight;
        ExtraSwaps = comprehension < 40 ? 0 : comprehension < 70 ? 1 : comprehension < 90 ? 2 : 3;
        InsightBonus = intelligence < 10 ? 0 : intelligence < 15 ? 1 : intelligence < 20 ? 2 : intelligence < 25 ? 3 : 4;
        RootBonus = rootWeight < 34 ? 0 : rootWeight < 67 ? 1 : rootWeight < 100 ? 2 : 3;
        if (counts.Sum(c => c.Value) != 36 || counts.Any(c => c.Value < 0 || c.Value > 4 || !Pool.Any(p => p.Name == c.Key)))
            throw new ArgumentException("牌组须为36张，同名最多4张。");
        EquippedItems = items.Distinct().Take(2).ToList();
        if (EquippedItems.Any(i => !Items.Contains(i))) throw new ArgumentException("未定义的凝核道具。");
        foreach (var card in Pool) for (var n = 0; n < counts.GetValueOrDefault(card.Name); n++) Deck.Add(card.Name);
        // 可选种子只供规则回放；正式界面不提供或显示牌序种子。
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        _openingShuffleSeed = random.Next();
        for (var n = Deck.Count - 1; n > 0; n--) { var j = random.Next(n + 1); (Deck[n], Deck[j]) = (Deck[j], Deck[n]); }
        Draw(6);
        Log.Add($"凝核开始：悟性{comprehension}，灵慧{intelligence}，匹配灵根权重{rootWeight}%。");
    }

    public CondensationCardGame Copy()
    {
        var copy = (CondensationCardGame)MemberwiseClone();
        copy.Hand = [.. Hand]; copy.Deck = [.. Deck]; copy.Discard = [.. Discard]; copy.Choices = [.. Choices];
        copy.Recoils = [.. Recoils]; copy.EquippedItems = [.. EquippedItems]; copy.UsedItems = [.. UsedItems];
        copy.Log = [.. Log]; copy._directions = [.. _directions]; return copy;
    }

    public string Swap(IReadOnlyList<int> indices, bool extra = false)
    {
        if (Finished || Busy || !SwapOpen) return "施展或使用道具后不能换牌。";
        if (indices.Count != indices.Distinct().Count() || indices.Any(i => i < 0 || i >= Hand.Count)) return "请选择有效手牌。";
        if (Round == 1)
        {
            if (indices.Count > 2 || OrdinarySwapUsed) return "起手只能统一换零至两张。";
            var removed = indices.OrderDescending().Select(i => { var c = Hand[i]; Hand.RemoveAt(i); return c; }).ToArray();
            Draw(removed.Length);
            Deck.AddRange(removed);
            var rng = new Random(_openingShuffleSeed);
            for (var i = Deck.Count - 1; i > 0; i--) { var j = rng.Next(i + 1); (Deck[i], Deck[j]) = (Deck[j], Deck[i]); }
        }
        else
        {
            if (indices.Count != 1 || Deck.Count == 0) return "换牌需要一张手牌和非空牌库。";
            if (extra ? ExtraSwaps <= 0 : OrdinarySwapUsed) return "本项换牌额度已用尽。";
            var index = indices[0]; Discard.Add(Hand[index]); Hand.RemoveAt(index); Draw(1);
            if (extra) ExtraSwaps--;
        }
        if (Round == 1 || !extra) OrdinarySwapUsed = true;
        Log.Add($"第{Round}回合换牌{indices.Count}张。"); return "";
    }

    public void StartActions() { if (!Finished && !Busy) SwapOpen = false; }
    public int TargetCount(string name) => name == "并流" ? 2 : name is "缓行" or "催回" or "归拢" or "回旋" or "分引" or "导气符" ? 1 : 0;
    public IEnumerable<Recoil> LegalTargets(string name) => Recoils.Where(r => name switch
    {
        "催回" => r.Round > Round,
        "回旋" => r.Round == Round,
        "分引" => r.Strength >= 2,
        _ => true
    });

    public string CanPlay(string name, int handIndex, IReadOnlyList<int> targets, bool item = false, int shift = -1)
    {
        if (Finished) return "本局已经结束。";
        if (Busy) return "请先完成选牌或弃牌。";
        if (item)
        {
            if (!EquippedItems.Contains(name) || UsedItems.Contains(name)) return "本局没有可用的此道具。";
            if (ItemUsedThisRound) return "每回合只能使用一件道具。";
        }
        else
        {
            if (Actions <= 0) return "本回合施展次数已用尽。";
            if (name != "调息" && (handIndex < 0 || handIndex >= Hand.Count || Hand[handIndex] != name)) return "这张牌不在手中。";
        }
        if (name == "叠压" && !LastPressure) return "叠压须紧接一次压聚出手。";
        if (name == "息压" && Disorder > 2) return "息压要求当前紊乱不高于2。";
        if (name == "引回" && (ReturnActive || !Recoils.Any(r => r.Round == Round))) return "需要本轮到达的回弹，且每轮引回限一张。";
        if (name == "随势" && (!ReturnActive || FollowActive)) return "先施展引回，每轮随势限一张。";
        if (name == "蓄劲" && ChargeActive) return "已有尚未触发的蓄劲。";
        if (name is "观气" or "观息符" && Deck.Count == 0) return "牌库已空。";
        if (targets.Count != TargetCount(name) || targets.Distinct().Count() != targets.Count || targets.Any(id => !LegalTargets(name).Any(r => r.Id == id))) return "请选择所需回弹目标。";
        if (name == "并流" && Recoils.First(r => r.Id == targets[0]).Round == Recoils.First(r => r.Id == targets[1]).Round) return "并流须选择到达回合不同的两股。";
        if (name == "导气符" && Recoils.First(r => r.Id == targets[0]).Round + shift < Round) return "不能提前到本回合以前。";
        return "";
    }

    public string Play(string name, int handIndex, IReadOnlyList<int> targets, bool item = false, int shift = -1)
    {
        var error = CanPlay(name, handIndex, targets, item, shift); if (error != "") return error;
        SwapOpen = false;
        if (item) { ItemUsedThisRound = true; UsedItems.Add(name); }
        else { Actions--; if (name != "调息") { Discard.Add(name); Hand.RemoveAt(handIndex); } }
        var before = Score;
        var pressure = !item && name != "调息" && Definition(name).Kind == "压聚";
        if (pressure)
        {
            var (gain, disorder, strength, delay) = PressureBase(name);
            if (ChargeActive) { gain += 8; disorder++; strength++; ChargeActive = false; }
            Score += gain + RootBonus; Disorder += disorder;
            Recoils.Add(new Recoil(++_nextRecoil, Round + delay, strength));
        }
        else
        {
            Recoil? target = targets.Count > 0 ? Recoils.First(r => r.Id == targets[0]) : null;
            switch (name)
            {
                case "引回": ReturnActive = true; break;
                case "缓行": Move(target!, 1); break;
                case "催回": Move(target!, -1); break;
                case "导气符": Move(target!, shift); break;
                case "并流":
                    var other = Recoils.First(r => r.Id == targets[1]);
                    var early = target!.Round < other.Round ? target : other;
                    Move(early, Math.Abs(target.Round - other.Round)); break;
                case "蓄劲": ChargeActive = true; break;
                case "随势": FollowActive = true; break;
                case "匀气": Score++; var drawn = Draw(2); ForcedDiscards = drawn > 0 ? 1 : 0; break;
                case "护气": Score++; Protection += 3; break;
                case "归拢": Score++; Recoils.Remove(target!); break;
                case "理气": Score = Math.Max(0, Score - 3); Disorder = Math.Max(0, Disorder - 4 - InsightBonus); break;
                case "回旋": Score += target!.Strength; Recoils.Remove(target); Disorder = Math.Max(0, Disorder - 2 - InsightBonus); break;
                case "分引":
                    Recoils.Remove(target!);
                    Recoils.Add(new Recoil(++_nextRecoil, target!.Round, target.Strength / 2));
                    Recoils.Add(new Recoil(++_nextRecoil, target.Round, target.Strength - target.Strength / 2)); break;
                case "观气": case "观息符":
                    Choices = Deck.Take(3).ToList(); Deck.RemoveRange(0, Choices.Count); break;
                case "调息": Disorder = Math.Max(0, Disorder - 2 - InsightBonus); Protection++; break;
                case "定息丹": Protection += 4; break;
                case "聚气丹": Score += 10; break;
                case "清息丹": Disorder = Math.Max(0, Disorder - 5); break;
            }
        }
        // 道具不消耗施展，不打断“上一出手为压聚”，但凝核净变化参与反转。
        if (!item) LastPressure = pressure;
        var direction = Math.Sign(Score - before);
        if (direction != 0 && (_directions.Count == 0 || _directions[^1] != direction))
        {
            _directions.Add(direction);
            if (_directions.Count >= 3) { Disorder++; Log.Add("本轮凝核反复涨降，额外紊乱+1。"); }
        }
        Log.Add($"第{Round}回合 · {name}：凝核{Score - before:+0;-0;0}，当前{Score}，紊乱{Disorder}。");
        if (Disorder > 10) { Failed = Finished = true; Log.Add("紊乱超过10，凝核失败。当前程度不是成功结果。"); }
        CheckHandLimit(); return "";
    }

    public void Choose(int index)
    {
        if (Finished || index < 0 || index >= Choices.Count) return;
        Hand.Add(Choices[index]); Choices.RemoveAt(index); Deck.AddRange(Choices); Choices.Clear(); CheckHandLimit();
    }
    public void Drop(int index)
    {
        if (Finished || ForcedDiscards <= 0 || index < 0 || index >= Hand.Count) return;
        Discard.Add(Hand[index]); Hand.RemoveAt(index); ForcedDiscards--; CheckHandLimit();
    }
    private void CheckHandLimit() { if (Choices.Count == 0) ForcedDiscards = Math.Max(ForcedDiscards, Hand.Count - 6); }
    private void Move(Recoil target, int shift) { Recoils.Remove(target); Recoils.Add(target with { Round = Math.Max(Round, target.Round + shift) }); }
    private int Draw(int count)
    {
        var actual = Math.Min(count, Deck.Count); Hand.AddRange(Deck.Take(actual)); Deck.RemoveRange(0, actual); return actual;
    }

    public string SettlementPreview()
    {
        var copy = Copy(); var logStart = copy.Log.Count;
        if (copy.Busy) return "请先完成选牌与弃牌，才能结算。";
        copy.EndRound();
        return string.Join("\n", copy.Log.Skip(logStart));
    }
    public string EndRound()
    {
        if (Finished || Busy) return "请先完成选牌或弃牌。";
        var current = Recoils.Where(r => r.Round == Round).ToArray();
        var strength = current.Sum(r => r.Strength);
        var recoilDelta = ReturnActive ? strength + (current.Length >= 2 ? 4 : 0) + (FollowActive ? Math.Min(6, current.Length * 2) : 0) : -Math.Max(0, strength - Protection);
        Score = Math.Max(0, Score + recoilDelta);
        Log.Add($"第{Round}回合结算：本轮回弹{recoilDelta:+0;-0;0}，凝核{Score}。");
        var loss = DisorderLoss(Disorder); Score = Math.Max(0, Score - loss);
        Log.Add($"紊乱{Disorder}扣除{loss}，凝核{Score}；紊乱随后降至{Math.Max(0, Disorder - 1)}。");
        Recoils.RemoveAll(r => r.Round == Round); Disorder = Math.Max(0, Disorder - 1);
        ReturnActive = FollowActive = LastPressure = false; Protection = 0; _directions.Clear();
        if (Round == 8)
        {
            var remaining = Recoils.Sum(r => r.Strength); Score = Math.Max(0, Score - remaining); Recoils.Clear();
            Finished = true; Failed = Score < 100;
            Log.Add($"未结回弹扣除{remaining}；最终凝核程度{Score}。{(Failed ? "未达到100，未成核。" : "达到100，灵核已成。")}");
        }
        else
        {
            Round++; Actions = 3; SwapOpen = true; OrdinarySwapUsed = ItemUsedThisRound = false;
            Draw(Math.Max(0, 6 - Hand.Count)); Log.Add($"进入第{Round}回合，补足手牌；牌库余{Deck.Count}张。");
        }
        return "";
    }
}
