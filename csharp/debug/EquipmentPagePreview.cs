using Godot;
using System.Reflection;

/// <summary>隔离角色验收装备页，不读取或写入正式存档。</summary>
public partial class EquipmentPagePreview : Node
{
    private const string Evidence = "res://docs/美术规范/装备页四槽改版_20261023/";
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); GD.Print("PASS " + message); }
    private static void Call(InfoPanel panel, string name, params object[] args) =>
        typeof(InfoPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(panel, args);
    private async System.Threading.Tasks.Task Settle()
    { for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Settle(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(Evidence + name + ".png") == Error.Ok, "截图 " + name);
    }
    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var owner = new Character(); AddChild(owner); owner.Hide();
            var data = new HumanData { entity_name = "装备页预览", gender = 0, appearance_seed = 121, cultivation_realm = "云息三层" };
            owner.load_from_data(data);
            data = (HumanData)owner.entity_data!;
            var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(panel);
            panel.show_entity(owner); Call(panel, "render_new_page", 4); await Settle();
            var pages = (Dictionary<int, Control>)typeof(InfoPanel).GetField("_characterPages", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
            var page = pages[4];
            Check(page.FindChildren("EquipmentSlot*", "Button", true, false).Count == 4, "主界面四个装备槽");
            Check(!page.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text is "常规穿戴" or "法宝" or "战备"), "撤下旧分栏与战备占位");
            await Capture("01_空槽");
            const string sword = "res://resources/entities/equipment/铁剑.tres";
            Check(owner.add_item_to_inventory_silently(sword) && owner.equip_inventory_item(sword, "weapon"), "测试武器正常入包及装备");
            Call(panel, "build_character_equipment", owner); await Settle();
            Check(page.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("物理防御")), "详情读取真实固定加成");
            await Capture("02_武器详情");
            page.FindChild("EquipmentSlot2", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(page.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "器物一槽"), "点击槽位切换详情");
            page.FindChild("EquipmentChange", true, false)!.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(data.weapon_path == sword, "空候选操作不修改已装备武器");
            Call(panel, "hide_tooltip");
            GetWindow().Size = new Vector2I(1280, 720); await Settle();
            await Capture("03_小窗口");
            data.headwear_path = sword;
            Call(panel, "build_character_equipment", owner); await Settle();
            Check(page.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.Text == "查看旧版装备  ›"), "旧存档额外装备有管理入口");
            Check(data.headwear_path == sword && data.weapon_path == sword, "页面重建不迁移或清除存档字段");
            GD.Print("EQUIPMENT_PAGE_PREVIEW_OK");
            Input.SetCustomMouseCursor(null); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
