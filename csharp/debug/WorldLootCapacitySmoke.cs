using Godot;
using System.Reflection;

/// <summary>Production legacy harvest and loot entry points with full spatial inventory.</summary>
public partial class WorldLootCapacitySmoke : Node
{
    private const string Stone = "res://resources/entities/items/灵石.tres";
    private const string Root = "res://resources/entities/items/苦苔根.tres";
    private const string Hide = "res://resources/entities/items/兽皮.tres";
    private const string Bone = "res://resources/entities/items/兽骨.tres";
    private int _checks;
    private Character _player = null!;
    private WorldLootTestHarness _scene = null!;
    private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private void Check(bool ok, string message) { _checks++; if (!ok) throw new InvalidOperationException(message); }
    private object? Invoke(string name, params object[] args) => typeof(MainScene).GetMethod(name, Flags)!.Invoke(_scene, args);
    private T Field<T>(string name) => (T)typeof(MainScene).GetField(name, Flags)!.GetValue(_scene)!;
    private static void Claim(ItemClaimPanel panel) => typeof(ItemClaimPanel).GetMethod("claim_selected_items", Flags)!.Invoke(panel, null);
    private string Stamp() => string.Join("|", _player.get_spatial_inventory().Select(e => $"{e.InstanceId}:{e.RegionId}:{e.X},{e.Y}:{e.Rotation}"));
    private async Task Frames(int count = 3) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            _scene = new WorldLootTestHarness(); AddChild(_scene);
            _player = new Character { entity_data = new HumanData { entity_name = "旧采集空间验证", current_world_stamina = 100 } };
            AddChild(_player);
            typeof(MainScene).GetField("player", Flags)!.SetValue(_scene, _player);
            typeof(MainScene).GetField("<StartupComplete>k__BackingField", Flags)!.SetValue(_scene, true);
            int initialStones = 120 - _player.get_inventory_occupied_cell_count();
            Check(_player.add_item_to_inventory(Stone, initialStones) && _player.get_inventory_occupied_cell_count() == 120, "满包样本创建失败");
            var before = Stamp(); Invoke("open_qingxi_location", "farmland");
            Check(_player.current_world_stamina == 100 && !Field<bool>("_qingxi_herbs_gathered"), "满包采集扣精力或标记已采集");
            Check(Stamp() == before && _player.get_inventory_total_count(Root) == 0, "失败采集改变摆放");
            _player.remove_item_from_inventory(Stone, 24); Invoke("open_qingxi_location", "farmland");
            Check(Field<bool>("_qingxi_herbs_gathered") && _player.get_inventory_total_count(Root) == 1, "腾空后采集未入包");
            Check(_player.current_world_stamina == 100 - WorldStaminaRules.InteractionCost, "采集精力错误");
            before = Stamp(); Invoke("open_qingxi_location", "farmland");
            Check(Stamp() == before, "重复采集生成了第二份药根");

            _player.remove_item_from_inventory(Root); Check(_player.add_item_to_inventory(Stone, 24), "回填满包失败");
            before = Stamp(); Invoke("grant_back_hill_beast_materials", "wild_boar", 1, false);
            Check(_scene.has_active_item_claim() && GetTree().Paused, "满包战利品未进入领取页");
            Check(Stamp() == before && _player.get_inventory_total_count(Hide) == 0 && _player.get_inventory_total_count(Bone) == 0, "满包战利品部分入包");
            var first = Field<ItemClaimPanel>("_active_item_claim");
            Invoke("collect_back_hill_resource", "spirit_stones", 2);
            Check(ReferenceEquals(first, Field<ItemClaimPanel>("_active_item_claim")), "第二批掉落覆盖领取页");
            Check(_player.remove_item_from_inventory(Stone, 70), "释放领取空间失败");
            // External mutation invalidates the open workspace. First finish refreshes it, then retry safely.
            typeof(ItemClaimPanel).GetMethod("commit_receipt", Flags)!.Invoke(first, null);
            Check(_scene.has_active_item_claim() && _player.get_inventory_total_count(Hide) == 0, "过期预览不应覆盖库存");
            Claim(first); await Frames();
            Check(_player.get_inventory_total_count(Hide) == 1 && _player.get_inventory_total_count(Bone) == 1, "战利品没有完整领取");
            Check(_scene.has_active_item_claim() && !ReferenceEquals(first, Field<ItemClaimPanel>("_active_item_claim")) && GetTree().Paused, "第二批掉落未排队打开或暂停被旧页覆盖");
            Claim(Field<ItemClaimPanel>("_active_item_claim")); await Frames();
            Check(_player.get_inventory_total_count(Stone) == initialStones - 68 && !_scene.has_active_item_claim() && !GetTree().Paused, "第二批掉落丢失或领取后未恢复暂停");
            Invoke("collect_back_hill_resource", "spirit_stones", 1);
            Check(_player.get_inventory_total_count(Stone) == initialStones - 67 && !_scene.has_active_item_claim(), "有空间普通采集未直接入包");
            _scene.QueueFree(); await Frames(); _player.Free();
            GD.Print($"WORLD_LOOT_CAPACITY_SMOKE_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Paused = false; if (GodotObject.IsInstanceValid(_player)) _player.Free(); GetTree().Quit(1); }
    }
}
