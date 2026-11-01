using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class SpatialInventoryNativeSmoke : Node
{
    private int _checks;
    private void Check(bool ok, string reason) { _checks++; if (!ok) throw new InvalidOperationException(reason); }
    private const string Stone = "res://resources/entities/items/灵石.tres";
    private const string Book = "res://resources/entities/items/玄枢含章经秘籍.tres";
    private const string Sword = "res://resources/entities/equipment/素行剑.tres";
    private static Character Player() => new() { entity_data = new HumanData { entity_name = "空间库存验证" } };
    private static Dictionary<string, int> Batch(string path, int count) => new() { [path] = count };
    private static string Fingerprint(Character p) => string.Join("|", p.get_spatial_inventory().OrderBy(i => i.InstanceId)
        .Select(i => $"{i.InstanceId}:{i.ItemPath}:{i.RegionId}:{i.X},{i.Y}:{i.Rotation}"));
    public override void _Ready()
    {
        try
        {
            using var player = Player();
            Check(Character.InventoryRegions.Sum(r => r.Width*r.Height) == 120, "五区容量错误");
            Check(player.add_item_to_inventory(Stone, 120), "120个灵石应可放入");
            Check(player.get_spatial_inventory().Count == 120 && player.pack_counts.All(n => n == 1), "实际实例必须不堆叠");
            Check(player.get_spatial_inventory().Select(i => i.InstanceId).Distinct().Count() == 120, "实例身份不唯一");
            Check(player.get_inventory_occupied_cell_count() == 120, "满包占格错误");
            string original = Fingerprint(player); int called = 0;
            Check(!player.add_item_to_inventory(Stone), "满包仍可加物品");
            Check(Fingerprint(player) == original, "入包失败改变原库存");
            Check(!player.try_exchange_inventory(Batch(Stone, 1), Batch(Book, 1), beforeCommit: () => { called++; return true; }), "无法摆入的交换不应成功");
            Check(called == 0 && Fingerprint(player) == original, "容量失败不得扣费或改变实例");
            Check(!player.try_exchange_inventory(Batch(Stone, 12), Batch(Book, 1), beforeCommit: () => { called++; return false; }), "扣费回调拒绝应中止");
            Check(called == 1 && Fingerprint(player) == original, $"扣费失败改变原位置（回调{called}次，位置一致{Fingerprint(player) == original}，可交换{player.can_exchange_inventory(Batch(Stone,12),Batch(Book,1))}）");
            int notifications=0; player.InventoryChanged += _ => notifications++;
            Check(player.try_exchange_inventory(Batch(Stone, 12), Batch(Book, 1), beforeCommit: () => { called++; return true; }), "释放空间后交换应成功");
            Check(player.get_inventory_total_count(Stone) == 108 && player.get_inventory_total_count(Book) == 1 && notifications == 1, "原子交换数量或通知错误");
            Check(player.get_inventory_occupied_cell_count() == 114, "交换后占格错误");
            using var moving = Player();
            Check(moving.add_item_to_inventory(Book, 2) && moving.add_item_to_inventory(Sword), "混装入包失败");
            var books = moving.get_spatial_inventory().Where(i => i.ItemPath == Book).ToArray();
            Check(books.Length == 2 && books[0].InstanceId != books[1].InstanceId, "同书独立实例错误");
            var book = books[0]; var sword = moving.get_spatial_inventory().Single(i => i.ItemPath == Sword);
            Check(!moving.move_spatial_item(book.InstanceId, sword.RegionId, sword.X, sword.Y, 0), "碰撞未阻止");
            Check(!moving.move_spatial_item(book.InstanceId, "chest", -1, 0, 0), "负坐标未阻止");
            Check(!moving.move_spatial_item(book.InstanceId, "sleeve-left", 1, 2, 1), "分区边界未阻止");
            Check(moving.move_spatial_item(book.InstanceId, "back", 2, 0, 3), "合法旋转跨区移动失败");
            var rotated = moving.get_spatial_inventory().Single(i => i.InstanceId == book.InstanceId);
            Check(rotated.Width == 3 && rotated.Height == 2 && rotated.Rotation == 3, "旋转尺寸或朝向错误");
            string save = "res://outputs/spatial_inventory_native_smoke.tres";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://outputs"));
            Check(ResourceSaver.Save(moving.entity_data, save) == Error.Ok, "保存失败");
            var loaded = ResourceLoader.Load<HumanData>(save, cacheMode: ResourceLoader.CacheMode.Ignore);
            using var restored = new Character { entity_data = loaded };
            Check(Fingerprint(restored) == Fingerprint(moving), "新空间库存读档身份或朝向未保留");
            var offer = GD.Load<SectArtifactOfferData>("res://resources/entities/equipment/清和剑.tres");
            var artifact = offer.CreateArtifact();
            Check(restored.add_artifact_to_inventory(artifact), "唯一法器未入包");
            var artifactEntry = restored.get_spatial_inventory().Single(i => i.ArtifactId == artifact.instance_id);
            Check(artifactEntry.Width * artifactEntry.Height == 12 && artifactEntry.Artifact == artifact, "法器快照/占地丢失");
            Check(restored.remove_artifact_from_inventory(artifact.instance_id) && restored.get_spatial_inventory().All(i => i.ArtifactId != artifact.instance_id), "法器移除残留占地");
            var unknown = new ArtifactInstanceData { artifact_name = "缺失占地的历史法宝" };
            string unchanged = Fingerprint(restored);
            Check(!restored.add_artifact_to_inventory(unknown) && Fingerprint(restored) == unchanged, "不得猜测未知法宝占地或半入包");
            var initialized = Player(); AddChild(initialized);
            Check(initialized.add_item_to_inventory(Stone) && initialized.get_spatial_inventory().All(i => i.IsPlaced), "真实人物初始化或自带衣物占地失败");
            Check(initialized.get_inventory_occupied_cell_count() <= 120, "新人物初始化溢出行囊");
            initialized.Free(); player.Free(); moving.Free(); restored.Free();
            GD.Print($"SPATIAL_NATIVE_MODEL: {_checks} checks passed"); GetTree().Quit();
        }
        catch(Exception ex) { GD.PushError("SPATIAL_NATIVE_MODEL: " + ex); GetTree().Quit(1); }
    }
}
