using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>自动跑炉：布置时预签原料节点，开炉后由调度器处理火候与线路并行。</summary>
public sealed partial class AlchemyPreviewModel
{
    private bool _automaticRunActive;
    private bool _automaticCooling;
    private bool _automaticGathering;
    private int _automaticLineCursor;
    private int _automaticIdleBreaths;
    private int _automaticStartBreath;
    private int _automaticStartingMana;
    private int _automaticPeakQueuedLines;
    private int _automaticLessonBreathLimit;
    private string _automaticStopReason = "";
    private double _automaticLastProgress;
    private bool _automaticLessonMode;

    public bool AutoRunStarted { get; private set; }
    public bool AutoRunActive => AutoRunStarted && !Done;
    public int AutomaticBreathsElapsed => Math.Max(0, Breath - _automaticStartBreath);
    public bool CanPrepare => !AutoRunStarted && !Done;
    public int ActiveAutomaticLines { get; private set; }
    public int QueuedAutomaticLines => Math.Max(0, SignedLinks.Count(l => !l.Paused) - ActiveAutomaticLines);
    public int AutomaticManaSpent => Math.Max(0, _automaticStartingMana - (Helper ? HelperMana : Mana));
    public string AutomaticStopReason => _automaticStopReason;

    public string PlaceHerb(string name, double portions = 1)
    {
        if (!CanPrepare) return "開爐后不能再投料；请重新开炉布置。";
        var herb = Herbs.FirstOrDefault(h => h.Name == name);
        if (herb is null) return "药材定义不存在。";
        if (OperatorAttainment < AlchemyProcessConfig.MinimumAttainment) return "九品丹需要当前操作者造诣至少8。";
        if (portions < .1 || Math.Abs(portions * 10 - Math.Round(portions * 10)) > 1e-7) return "投料以0.1份为单位。";
        if (FurnaceMass + portions > FurnaceCapacity + 1e-8) return $"超过丹炉容量；当前最多还能放{Math.Max(0, FurnaceCapacity - FurnaceMass):0.0}份。";

        EnsureInventory();
        while (InFurnace.Any(d => d.Id == _nextId)) _nextId++;
        var dose = new Dose(herb, name == MainName ? "主药" : "辅药", 0)
        {
            Id = _nextId++,
            Original = portions * AlchemyProcessConfig.Unit,
            Raw = portions * AlchemyProcessConfig.Unit,
            Open = herb.Processing.Dense ? 0 : 1
        };
        InFurnace.Add(dose);
        _seeded.Add(dose.Id);
        Explanation = $"已将{herb.Name}×{portions:0.0}份放入布置区；现在可在节点网上预设功用线。";
        return Explanation;
    }

    private void RefreshDoseRoles()
    {
        foreach (var dose in InFurnace)
            dose.Kind = dose.Herb.Name == MainName ? "主药" : "辅药";
    }

    public string AutomaticPreflight()
    {
        if (Done) return "本炉已结束。";
        if (AutoRunStarted) return "本炉已经开炉。";
        if (OperatorAttainment < AlchemyProcessConfig.MinimumAttainment) return "当前操作者造诣不足8，无法炼制九品丹。";
        var main = InFurnace.FirstOrDefault(d => d.Kind == "主药" && d.Herb.Name == MainName);
        if (main is null) return $"请先将主药{MainName}放入丹炉。";
        if (main.Herb.Grade(CurrentPill.CoreUtility) <= 0) return $"主药没有丹种要求的{CurrentPill.CoreUtility}功用。";
        if (InFurnace.Sum(d => d.Raw + d.Mass - d.Raw) <= 0) return "炉内没有可处理的药材。";
        if (main.Herb.Processing.Open > FireLimit && !SignedLinks.Any(l => !l.Paused && l.UtilityId == "破" && l.TargetDoseId == main.Id))
            return $"主药需要{main.Herb.Processing.Open}档才能开炼，当前最高只能控制到{FireLimit}档；请接入“破”或提升造诣／炉具。";
        if (!HasDirectedPassage(main.Id, 0, true)) return $"主药{main.Herb.Name}还没有通往丹炉口的“通”路线。";
        if ((Helper ? HelperSpirit : MainSpirit) <= 0) return "灵识不足以维持任何一条功用线。";
        return "";
    }

    public string BeginAutomaticRun()
    {
        string error = AutomaticPreflight();
        if (error.Length > 0) return error;
        AutoRunStarted = true;
        _automaticRunActive = true;
        _automaticCooling = false;
        _automaticGathering = false;
        _automaticLessonMode = false;
        _automaticLessonBreathLimit = 0;
        _automaticLineCursor = 0;
        _automaticPeakQueuedLines = 0;
        _automaticIdleBreaths = 0;
        _automaticStartBreath = Breath;
        _automaticStartingMana = Helper ? HelperMana : Mana;
        _automaticStopReason = "运行中：灵识调度功用线，法力维持自动火候。";
        _automaticLastProgress = ProgressMark();
        Wanted = Math.Clamp(Wanted, 1, Math.Min(MaxPills, Math.Max(1, Count)));
        Explanation = $"已开炉：目标{CurrentPill.Name}，计划{Wanted}颗。灵识决定并行线路数，超出的线路轮候；法力按实际火候炉令支出。";
        return "";
    }

    /// <summary>教程使用同一套自动炉令和资源结算，但允许无主药运输路线的单功用对照场景。</summary>
    public void BeginAutomaticLesson(int breathLimit = 1)
    {
        if (Done) return;
        AutoRunStarted = true;
        _automaticRunActive = true;
        _automaticLessonMode = true;
        _automaticLessonBreathLimit = Math.Clamp(breathLimit, 1, 8);
        _automaticCooling = false;
        _automaticGathering = false;
        _automaticLineCursor = 0;
        _automaticPeakQueuedLines = 0;
        _automaticIdleBreaths = 0;
        _automaticStartBreath = Breath;
        _automaticStartingMana = Helper ? HelperMana : Mana;
        _automaticStopReason = $"教程观察中：本课共运行{_automaticLessonBreathLimit}息。";
        _automaticLastProgress = ProgressMark();
    }

    public bool AdvanceAutomaticRun()
    {
        if (!AutoRunStarted || Done || !_automaticRunActive) return false;
        if (Breath - _automaticStartBreath >= 80)
        {
            StopAutomatic("达到80息安全上限，停止空转并保留当前药材与丹胚。", true);
            return false;
        }

        PlannedLinks.Clear();
        if (_automaticCooling)
        {
            ActiveAutomaticLines = 0;
            PendingOperations.Clear();
            if (Heat <= 1)
            {
                FinishAutomatic();
                return false;
            }
            Action = Hand.Lower;
        }
        else
        {
            ScheduleAutomaticLines();
            if (_automaticGathering)
                PendingOperations.Add(new(Hand.Gather));
            else
                PendingOperations.RemoveAll(o => o.Kind == Hand.Gather);
            Action = SelectAutomaticFireOrder();
        }

        string error = Advance();
        if (error.Length > 0)
        {
            if (_automaticCooling)
            {
                _automaticStopReason += "；法力不足以继续敛焰，按现有药团结算。";
                FinishAutomatic();
            }
            else StopAutomatic(error, true);
            return false;
        }

        if (_automaticLessonMode && AutomaticBreathsElapsed >= _automaticLessonBreathLimit)
        {
            _automaticStopReason = $"完成{_automaticLessonBreathLimit}息的同炉况观察；结算用于本课对照。";
            FinishAutomatic();
            return false;
        }

        if (_automaticCooling)
        {
            if (Heat <= 1) FinishAutomatic();
            return AutoRunActive;
        }

        if (_automaticLessonMode)
        {
            _automaticStopReason = $"教程观察中：已运行{AutomaticBreathsElapsed}/{_automaticLessonBreathLimit}息。";
        }
        else
        {
            double progress = ProgressMark();
            if (progress > _automaticLastProgress + 1e-6)
            {
                _automaticIdleBreaths = 0;
                _automaticLastProgress = progress;
            }
            else
            {
                _automaticIdleBreaths++;
            }

            int lines = SignedLinks.Count(l => !l.Paused);
            int idleLimit = Math.Max(2, (int)Math.Ceiling(lines / (double)Math.Max(1, Helper ? HelperSpirit : MainSpirit)) + 1);
            bool rawRemains = InFurnace.Any(d => d.Raw > 1e-6);
            if (_automaticIdleBreaths >= idleLimit)
            {
                if (NodeAvailable(0) >= 1e-6 && !_automaticGathering)
                {
                    _automaticGathering = true;
                    _automaticIdleBreaths = 0;
                    _automaticStopReason = "药性流转已停稳，自动进行基础凝丹。";
                }
                else if (_automaticGathering && Pills.Count > 0)
                {
                    _automaticStopReason = NodeAvailable(0) >= 1e-6
                        ? "丹体已成形，余药暂不能继续分配；保留实际余料。"
                        : "药流已尽，完成现有丹体的自动收束。";
                    BeginCooling();
                }
                else
                {
                    _automaticStopReason = rawRemains
                        ? "连续多个呼吸没有有效加工或输送；按当前材料与布网瓶颈收炉。"
                        : Pills.Count > 0
                            ? "当前节点网已无有效工作，按现有丹体与余料收炉。"
                            : "当前节点网已无有效工作，停止空转。";
                    BeginCooling();
                }
            }
        }

        LastFeedback = string.Join("\n", Changes.TakeLast(5));
        Explanation = $"自动炼制中 · 第{Breath - _automaticStartBreath}息 · 火候{Heat}档 · 并行{ActiveAutomaticLines}/{(Helper ? HelperSpirit : MainSpirit)} · 等待{QueuedAutomaticLines}线";
        if (_automaticCooling && Heat <= 1) FinishAutomatic();
        return AutoRunActive;
    }

    public void RunAutomaticLesson(int breathLimit = 8)
    {
        int fixedWindow = Math.Clamp(breathLimit, 1, 8);
        BeginAutomaticLesson(fixedWindow);
        int end = Breath + fixedWindow;
        while (AutoRunActive && Breath < end) AdvanceAutomaticRun();
        if (AutoRunActive) StopAutomatic("本课演练到达观察上限；可查看已产生的真实变化。", false);
    }

    private void ScheduleAutomaticLines()
    {
        var lines = SignedLinks.Where(l => !l.Paused).OrderBy(l => AutomaticPriority(l.UtilityId))
            .ThenBy(l => l.SourceDoseId).ThenBy(l => l.TargetDoseId).ToArray();
        int capacity = Math.Clamp(Helper ? HelperSpirit : MainSpirit, 0, Math.Max(0, lines.Length));
        ActiveAutomaticLines = Math.Min(capacity, lines.Length);
        _automaticPeakQueuedLines = Math.Max(_automaticPeakQueuedLines, Math.Max(0, lines.Length - ActiveAutomaticLines));
        if (ActiveAutomaticLines == 0) return;

        int start = lines.Length <= capacity ? 0 : _automaticLineCursor % lines.Length;
        var chosen = new HashSet<SignedLink>();
        for (int i = 0; i < ActiveAutomaticLines; i++) chosen.Add(lines[(start + i) % lines.Length]);
        foreach (var line in lines)
        {
            if (chosen.Contains(line))
            {
                PlannedLinks.Add(line);
                line.Status = "灵识并行运行";
            }
            else line.Status = "灵识不足，自动轮候";
        }
        _automaticLineCursor = (start + ActiveAutomaticLines) % Math.Max(1, lines.Length);
    }

    private static int AutomaticPriority(string utility) => utility switch
    {
        "通" => 0, "行" => 1, "引" => 2, "破" => 3, "化" => 4, "聚" => 5,
        "固" => 6, "镇" => 7, "护" => 8, "生" => 9, "明" => 10, "藏" => 11, _ => 12
    };

    private Hand SelectAutomaticFireOrder()
    {
        var main = InFurnace.FirstOrDefault(d => d.Kind == "主药");
        var raw = InFurnace.Where(d => d.Raw > 1e-6).ToArray();
        int target = 0;
        if (main is { Raw: > 1e-6 })
            target = main.Herb.Processing.OptimalLow;
        else if (raw.Length > 0)
            target = raw.Max(d => d.Herb.Processing.OptimalLow);
        target = Math.Clamp(target, 0, FireLimit);
        if (Heat < target) return Hand.Raise;
        if (Heat > target) return Hand.Lower;
        return Hand.Hold;
    }

    private double ProgressMark()
    {
        return InFurnace.Sum(d => d.Extracted + d.Spent + d.Open + d.Separation + d.Diagnosis + d.Sealed + d.Agitation)
            + SignedLinks.Sum(l => l.BuildProgress + l.Transported + l.Structures.Sum(p => p.Amount))
            + Inventory.Sum(p => p.Amount * (1 + p.Mix + p.Clustered + p.Sealed) + p.Slag)
            + Pills.Sum(p => p.Parts.Sum(x => x.Amount + x.Slag) + p.Form);
    }

    private void BeginCooling()
    {
        _automaticCooling = true;
        _automaticGathering = false;
        PlannedLinks.Clear();
        PendingOperations.Clear();
        ActiveAutomaticLines = 0;
        if (Heat <= 1) FinishAutomatic();
    }

    private void StopAutomatic(string reason, bool coolIfPossible)
    {
        _automaticStopReason = reason;
        if (coolIfPossible && Heat > 1)
        {
            BeginCooling();
            return;
        }
        FinishAutomatic();
    }

    private void FinishAutomatic()
    {
        if (Done) return;
        PlannedLinks.Clear();
        PendingOperations.Clear();
        RepeatingOperations.Clear();
        ActiveAutomaticLines = 0;
        _automaticRunActive = true; // 完炉后仍按新规则显示灵识容量，不回到旧超载百分比。
        Finish();
        string summary = $"\n\n{(_automaticLessonMode ? "教程对照" : "自动跑炉" )}：{Breath - _automaticStartBreath}息 · 法力实耗{AutomaticManaSpent} · 灵识并行上限{(Helper ? HelperSpirit : MainSpirit)} · 最多同时等待{_automaticPeakQueuedLines}条线。\n收炉缘由：{_automaticStopReason}";
        Result += summary;
        Explanation = Result;
        LastFeedback = Result;
    }
}
