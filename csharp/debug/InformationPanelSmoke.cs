using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>Isolated UI interaction checks; never reads or writes a real journey.</summary>
public partial class InformationPanelSmoke : Node
{
    private const string Evidence = "res://docs/美术规范/信息栏整理_20261025/";
    private int _checks;
    private void Check(bool ok, string message)
    {
        _checks++;
        if (!ok) throw new InvalidOperationException(message);
    }
    private static object? Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private async Task Settle() { for (int i = 0; i < 16; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static Vector2 Center(Control control) => control.GetGlobalTransformWithCanvas() * (control.Size / 2);
    private static void Move(Vector2 position)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
        Input.FlushBufferedEvents();
    }
    private static void Click(Control control) => Click(Center(control));
    private static void Click(Vector2 position)
    {
        Move(position);
        Input.ParseInputEvent(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true });
        Input.ParseInputEvent(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(Evidence + name + ".png") == Error.Ok, "截图保存 " + name);
    }
    private void Fits(Control control)
    {
        var rect = control.GetGlobalRect();
        var viewport = GetViewport().GetVisibleRect();
        Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= viewport.End.X + 1 && rect.End.Y <= viewport.End.Y + 1, "信息栏完整位于画布内");
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var data = new HumanData { unique_id = "information-panel-isolated", max_health = 100, current_health = 35 };
            var owner = new Character { entity_data = data }; AddChild(owner);
            const string materialPath = "res://resources/entities/items/苦苔根.tres";
            const string swordPath = "res://resources/entities/equipment/铁剑.tres";
            const string pillPath = "res://resources/entities/items/alchemy/huichun_1.tres";
            Check(owner.add_item_to_inventory_silently(materialPath), "材料入测试行囊");
            Check(owner.add_item_to_inventory_silently(swordPath), "装备入测试行囊");
            Check(owner.add_item_to_inventory_silently(pillPath), "丹药入测试行囊");
            var storage = GD.Load<PackedScene>("res://tscn/panel/StoragePanel.tscn").Instantiate<StoragePanel>();
            AddChild(storage); storage.bind_player(owner); storage.show_panel();
            await Settle();
            StorageItemSlot Slot(string path) => storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().First(s => s.item_path == path);
            var popup = storage.FindChild("ItemPopup", true, false) as Control ?? throw new Exception("无菜单节点");
            var hover = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            var material = Slot(materialPath);
            var materialInfo = (HoverInfoManager.HoverInfoContent)Call(storage, "build_hover_content", material)!;
            Check(materialInfo.Sections is { Count: > 0 } && materialInfo.Body.Contains("材性与功用") && !materialInfo.Body.Contains("使用效果"), "材料使用材性栏目");
            Move(Center(material)); HoverInfoManager.SetInfo(material, materialInfo, true); await Settle();
            Check(hover.Visible && !popup.Visible, "悬停有信息，无操作菜单"); Fits(hover);
            Check(hover.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().All(s => s.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled), "详情无内部滚动条");
            await Capture("01_材料悬停");
            Click(material); await Settle(); Check(!popup.Visible, "无操作材料单击不弹窗");
            var sword = Slot(swordPath); Click(sword); await Settle();
            var action = popup.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text != "取消");
            Check(popup.Visible && action.Text == "装备" && !hover.Visible, "装备单击只有操作");
            Check(!popup.FindChildren("*", "Label", true, false).Any() && !popup.FindChildren("*", "RichTextLabel", true, false).Any(), "操作菜单不重复详情文字");
            Fits(popup); await Capture("02_装备操作菜单");
            // Re-registering hovered data must not cover an open operation menu.
            HoverInfoManager.SetInfo(sword, StoragePanel.ItemHover(GD.Load<ItemData>(swordPath), 1, swordPath), true);
            await Settle(); Check(!hover.Visible, "打开操作时暂停该格悬停");
            Click(action); await Settle(); Check(owner.is_item_equipped(swordPath), "真实按钮完成装备");
            sword = Slot(swordPath); Click(sword); await Settle();
            Check(action.Text == "卸下", "装备后刷新卸下操作");
            Click(action); await Settle(); Check(!owner.is_item_equipped(swordPath), "真实按钮完成卸下");
            Click(Slot(swordPath)); await Settle(); Click(Slot(materialPath)); await Settle();
            Check(!popup.Visible, "点击材料关闭旧菜单");
            Click(Slot(swordPath)); await Settle(); Click(Slot(swordPath)); await Settle(); Check(!popup.Visible, "再次单击同格关闭菜单");
            Click(Slot(swordPath)); await Settle(); Click(new Vector2(900, 780)); await Settle(); Check(!popup.Visible, "点击空处关闭菜单");
            var pillSlot = Slot(pillPath); var pillInfo = (HoverInfoManager.HoverInfoContent)Call(storage, "build_hover_content", pillSlot)!;
            Check(pillInfo.Body.Contains("丹毒") && pillInfo.Subtitle.Contains("颗"), "丹药保留代价及颗数");
            Move(Center(pillSlot)); HoverInfoManager.SetInfo(pillSlot, pillInfo, true); await Settle(); Fits(hover); await Capture("03_丹药悬停");
            Click(pillSlot); await Settle(); Check(popup.Visible && action.Text == "使用", "可服用丹药显示使用");
            Click(action); await Settle(); Check(data.current_health > 35, "服丹实际恢复生命");
            Check(!storage.FindChildren("*", "Control", true, false).OfType<StorageItemSlot>().Any(s => s.item_path == pillPath) && !popup.Visible, "最后一颗消耗后关闭菜单");
            storage.hide_panel(); await Settle();
            var anchor = new Button { Text = "技能信息预览", Position = new Vector2(100, 100), Size = new Vector2(150, 60) }; AddChild(anchor);
            foreach (var name in new[] { "应敌", "架招", "痛击" })
            {
                var skill = GD.Load<ActiveSkillData>($"res://resources/activeskills/{name}.tres");
                var expected = CardPresentation.Underline(CardPresentation.CombatEffectText(skill, owner));
                var info = CombatSkillBar.BuildSkillHoverInfo(owner, skill, null);
                Check(info.Sections is { Count: > 0 } && info.Body.Contains(expected), name + "完整保留效果及公式");
                Check(!info.Subtitle.Contains("类型：") && info.Body.Contains("目标：") && info.Body.Contains("使用次数："), name + "保留必要字段并撤下旧分类");
                if (name == "架招") Check(info.Body.Contains("应对触发费用") && info.Body.Contains("未应对费用") && info.Body.Contains("未触发应对"), "应对双分支保留");
                Move(Center(anchor)); HoverInfoManager.SetInfo(anchor, info, true); await Settle(); Fits(hover);
                await Capture("04_技能_" + name);
            }
            foreach (var position in new[] { new Vector2(8, 8), new Vector2(1450, 8), new Vector2(8, 780), new Vector2(1450, 780) })
            {
                anchor.Position = position; Move(Center(anchor));
                HoverInfoManager.SetInfo(anchor, CombatSkillBar.BuildSkillHoverInfo(owner, GD.Load<ActiveSkillData>("res://resources/activeskills/痛击.tres"), null), true);
                await Settle(); Fits(hover);
            }
            HoverInfoManager.HideAll();
            GetWindow().Size = new Vector2I(1280, 720); anchor.Position = new Vector2(100, 100); await Settle();
            Move(Center(anchor)); HoverInfoManager.SetInfo(anchor, CombatSkillBar.BuildSkillHoverInfo(owner, GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres"), null), true);
            await Settle(); Fits(hover); await Capture("05_小窗口应对技能");
            HoverInfoManager.HideAll(); anchor.Hide(); storage.show_panel(); await Settle();
            Click(Slot(swordPath)); await Settle(); Fits(popup); await Capture("06_小窗口操作菜单");
            storage.hide_panel(); Check(!popup.Visible, "关闭行囊收起操作菜单");
            GD.Print($"INFORMATION_PANEL_SMOKE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
