using Godot;

/// <summary>Independent planning UI: real card resources, controls and output plan.</summary>
public partial class BoardHandPlanningSmoke : Node
{
    private int _checks;
    private void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        _checks++;
    }
    private static void Press(Button button) => button.EmitSignal(BaseButton.SignalName.Pressed);
    private static void Choose(OptionButton option, int index)
    {
        option.Select(index);
        option.EmitSignal(OptionButton.SignalName.ItemSelected, index);
    }
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        try
        {
            var paths = new[] { "res://resources/activeskills/应敌.tres", "res://resources/activeskills/痛击.tres",
                "res://resources/activeskills/剑修基础/格剑.tres", "res://resources/activeskills/聚炁.tres" };
            var cards = paths.Select((path, index) => new BoardCard(new BoardCardKey(123, "placement-" + index),
                path, GD.Load<ActiveSkillData>(path) ?? throw new InvalidOperationException("missing skill " + path))).ToArray();
            var state = new BoardHandState(cards, new Random(7), queueCapacity: 3);
            Check(state.DrawOpeningHand() == 4, "real resources form the opening hand");
            var first = state.Hand[0]; var second = state.Hand[1]; var third = state.Hand[2]; var missing = state.Hand[3];
            var points = new Dictionary<BoardCardKey, int?>
            {
                [first.Key] = 2, [second.Key] = 2, [third.Key] = 1, [missing.Key] = null
            };
            var enemies = new[] { new BoardPublicEnemyQueue("enemy-a", "赤焰 · 一路", 11),
                new BoardPublicEnemyQueue("enemy-b", "赤焰 · 二路", 8) };
            var view = new BoardHandPlanningView { Name = "PlanningView" };
            view.Configure(state, enemies, 4, 6, 1, 2, null);
            AddChild(view);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            const string root = "Content/Layout/";
            Button Hand(int index) => view.GetNode<Button>(root + "Columns/HandPanel/Body/HandScroll/HandCards/HandCard" + index);
            Button Queue(int index) => view.GetNode<Button>(root + "Columns/QueuePanel" + index + "/Body/SelectQueue" + index);
            Button Queued(int queue, int index) => view.GetNode<Button>(root + "Columns/QueuePanel" + queue + "/Body/QueueScroll/QueueCards/QueuedCard" + index);
            OptionButton Target(int index) => view.GetNode<OptionButton>(root + "Columns/QueuePanel" + index + "/Body/Target" + index);
            async Task ClickActual(Button button)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var position = button.GetGlobalRect().GetCenter();
                GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
                GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
                    ButtonIndex = MouseButton.Left, Pressed = true }, true);
                GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
                    ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var notice = view.GetNode<Label>(root + "Notice");
            var confirm = view.GetNode<Button>(root + "Confirm");
            Check(view.GetNode<Label>(root + "Resources").Text.Contains("4/6")
                && view.GetNode<Label>(root + "Resources").Text.Contains("1/2"), "actual provided resources shown");
            Check(Hand(0).Disabled && confirm.Disabled && notice.Text.Contains("未配置队列点数"), "missing point resolver disables planning with explanation");
            if (OS.GetCmdlineUserArgs().Contains("--native-input"))
                await ClickActual(Hand(0));
            else Press(Hand(0));
            Check(state.Hand.Count == 4 && state.GetQueue(0).Count == 0, "disabled hand cannot bypass missing configuration");

            view.Configure(state, enemies, 4, 6, 1, 2, card => points[card.Key]);
            Check(!Hand(0).Disabled && Hand(3).Disabled && Hand(3).Text.Contains("未配置"),
                "per-card point configuration controls only eligible cards");
            Check(Target(0).ItemCount == 3 && Target(0).GetItemText(1) == "赤焰 · 一路  ·  行速 11"
                && Target(0).GetItemText(2) == "赤焰 · 二路  ·  行速 8", "targets show only public queue and speed");
            if (OS.GetCmdlineUserArgs().Contains("--native-input"))
                await ClickActual(Hand(0));
            else Press(Hand(0));
            Check(state.GetQueue(0).Count == 1 && state.GetQueue(0)[0].Card.Key == first.Key
                && state.GetQueueUsed(0) == 2 && state.Hand.Count == 3, "click hand queues original card with external point cost");
            Check(Hand(0).Disabled && !Hand(1).Disabled, "queue capacity disables cards that do not fit");
            Press(Hand(1));
            Check(state.GetQueueUsed(0) == 3 && state.GetQueue(0)[1].Card.Key == third.Key, "second card fills queue in order");
            Press(Queued(0, 0));
            Check(state.GetQueueUsed(0) == 1 && state.Hand.Any(card => card.Key == first.Key), "click queue card withdraws it and frees points");
            Press(Queued(0, 0));
            Check(state.GetQueue(0).Count == 0 && state.Hand.Count == 4, "withdrawing all returns both cards");
            var firstIndex = state.Hand.ToList().FindIndex(card => card.Key == first.Key);
            Press(Hand(firstIndex));
            Press(confirm);
            Check(notice.Text.Contains("选择交互目标") && !view.IsConfirmed, "nonempty queue needs explicit target");
            Choose(Target(0), 1);
            Press(Queue(1));
            var secondIndex = state.Hand.ToList().FindIndex(card => card.Key == second.Key);
            Press(Hand(secondIndex));
            Check(state.GetQueue(1).Count == 1 && state.GetQueueUsed(1) == 2, "selected second queue has independent capacity");
            Press(confirm);
            Check(notice.Text.Contains("队列 2") && !view.IsConfirmed, "each nonempty queue needs its own target");
            Choose(Target(1), 2);
            if (OS.GetCmdlineUserArgs().Contains("--capture-planning"))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                using var capture = GetViewport().GetTexture().GetImage();
                Check(capture is not null && !capture.IsEmpty()
                    && capture.SavePng("res://tmp/board_hand_planning_review.png") == Error.Ok, "native planning screenshot saved");
            }
            points[first.Key] = null;
            Press(confirm);
            Check(notice.Text.Contains("未配置或已变化") && !view.IsConfirmed, "stale queued points cannot be silently submitted");
            points[first.Key] = 2;
            BoardHandPlan? submitted = null; var count = 0;
            view.PlanConfirmed += plan => { submitted = plan; count++; };
            Press(confirm);
            Check(view.IsConfirmed && count == 1 && submitted is not null, "valid plan emits once");
            Check(submitted!.Queues.Count == 2 && submitted.Queues[0].TargetQueueId == "enemy-a"
                && submitted.Queues[1].TargetQueueId == "enemy-b", "submitted targets match selected public queues");
            Check(submitted.Queues[0].Cards.Single().Card.Key == first.Key && submitted.Queues[1].Cards.Single().Card.Key == second.Key,
                "submitted cards preserve placement identities");
            Check(submitted.Queues[0].Cards[0].CapacityCost == 2 && submitted.Queues[1].Cards[0].CapacityCost == 2,
                "submitted plan includes externally supplied points");
            Press(confirm); Press(Queued(0, 0));
            Check(count == 1 && state.GetQueue(0).Count == 1, "confirmation and queue editing cannot repeat after submit");
            view.UpdateResources(2, 6, 0, 2);
            Check(view.GetNode<Label>(root + "Resources").Text.Contains("2/6") && view.GetNode<Label>(root + "Resources").Text.Contains("0/2"),
                "resource display accepts live external update without spending");
            GD.Print($"BOARD_HAND_PLANNING_SMOKE_OK checks={_checks}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("BOARD_HAND_PLANNING_SMOKE_FAILED " + error);
            GetTree().Quit(1);
        }
    }
}
