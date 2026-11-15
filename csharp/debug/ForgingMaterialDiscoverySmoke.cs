using Godot;
using System.Text.Json;

/// <summary>临时新增材料，走真实发现、正式扣料、装备与快照保存；不使用配方或玩家存档。</summary>
public partial class ForgingMaterialDiscoverySmoke : Node
{
    private const string Folder = "res://resources/entities/items/__forging_material_smoke";
    private const string NewMaterial = "泛化测试金属";
    private const string ItemPath = Folder + "/arbitrary_filename.tres";
    private const string MissingPath = Folder + "/without_processing.tres";
    private const string InvalidPath = Folder + "/invalid_processing.tres";
    private const string SavePath = "user://tests/forging_material_discovery.tres";
    private int _checks;
    private readonly List<Character> _actors = [];
    private void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        GD.Print("PASS " + label); _checks++;
    }
    private static string Data(object value) => JsonSerializer.Serialize(value);
    private static ItemData Alias(ItemData source, string name, string id)
    {
        var item = (ItemData)source.Duplicate(true);
        item.entity_name = name; item.definition_id = id;
        // 材性通常是外部资源，测试变体必须显式复制，不能污染原材料缓存。
        var craft = (MaterialCraftData)((MaterialCraftData)source.craft_data!).Duplicate(true);
        craft.forging = (MaterialForgingData)((MaterialCraftData)source.craft_data!).forging!.Duplicate(true);
        item.craft_data = craft;
        return item;
    }
    private Character Actor(ProcessForgingTrial model, ProcessForgingTrial.Draft draft)
    {
        var actor = ForgingSmokeFixtures.Actor(model, draft);
        _actors.Add(actor); return actor;
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private void Run()
    {
        bool createdFolder = false;
        try
        {
            Check(!DirAccess.DirExistsAbsolute(Folder), "临时材料目录不存在，避免覆盖项目资源");
            var original = new ProcessForgingTrial();
            var originalDraft = ForgingSmokeFixtures.ArtifactDraft(original);
            var originalProduct = original.Evaluate(originalDraft).Product!;
            Check(originalProduct is not null && original.Parameters.Profiles.Count > 0
                && original.Parameters.Profiles.Keys.All(original.Materials.ContainsKey), "现有加工材料从自身档案自动发现");
            Check(!JsonDocument.Parse(Godot.FileAccess.GetFileAsString(ProcessForgingTrial.ConfigPath)).RootElement.TryGetProperty("Profiles", out _),
                "公共公式不再配置材料名字名单");

            // 可选：使用迁移前参数验证真实数值及旧快照，而非只比较两个新模型。
            var legacyArg = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--legacy-forging-config="));
            if (legacyArg is not null)
            {
                var config = JsonSerializer.Deserialize<ProcessForgingTrial.Config>(Godot.FileAccess.GetFileAsString(legacyArg.Split('=', 2)[1]))!;
                var legacy = ProcessForgingTrial.Isolated(config, original.MaterialFacts());
                var currentProfiles = original.Parameters.Profiles;
                Check(config.Profiles.Count == currentProfiles.Count && config.Profiles.All(pair =>
                    currentProfiles.TryGetValue(pair.Key, out var value) && Data(pair.Value) == Data(value)), "迁移前后加工参数逐项一致");
                foreach (var index in Enumerable.Range(0, config.Examples.Count))
                {
                    var before = legacy.Evaluate(legacy.ExampleDraft(index)).Product!;
                    var after = original.Evaluate(original.ExampleDraft(index)).Product!;
                    Check(before.Sharpness == after.Sharpness && before.Tier == after.Tier && Data(before.Effects) == Data(after.Effects)
                        && Data(before.Calculation) == Data(after.Calculation) && Data(before.BondContributions) == Data(after.BondContributions),
                        "原有样方数值及预算保持 " + index);
                    Check(ProcessForgingTrial.Replay(before).Product!.InputHash == before.InputHash, "历史参数原文快照仍可重演 " + index);
                }
            }

            Check(DirAccess.MakeDirRecursiveAbsolute(Folder) == Error.Ok, "创建隔离的临时物品子目录"); createdFolder = true;
            var alias = Alias(original.Materials["铜髓矿"], NewMaterial, "smoke_forging_new_metal");
            Check(ResourceSaver.Save(alias, ItemPath) == Error.Ok, "新材料只保存自身材性与加工属性");
            var missing = Alias(original.Materials["铜髓矿"], "无加工档案测试材", "smoke_forging_missing");
            ((MaterialCraftData)missing.craft_data!).forging = null;
            Check(ResourceSaver.Save(missing, MissingPath) == Error.Ok, "保存未定义炼器用途的材料");
            var model = new ProcessForgingTrial();
            Check(model.Materials.ContainsKey(NewMaterial) && model.Materials[NewMaterial].ResourcePath == ItemPath,
                "自动发现子目录物品，不依赖物品和材性文件同名；实际路径=" + model.Materials.GetValueOrDefault(NewMaterial)?.ResourcePath);
            Check(!model.Materials.ContainsKey("无加工档案测试材"), "无加工属性不猜测材料用途");
            Check(original.Parameters.Profiles["铜髓矿"].Roles.All(role => model.Choices(role).Contains(NewMaterial)),
                "新材料自动进入其合法岗位候选");
            var draft = ProcessForgingTrial.Clone(originalDraft);
            foreach (var input in draft.Inputs.Where(input => input.Material == "铜髓矿")) input.Material = NewMaterial;
            var product = model.Evaluate(draft).Product!;
            Check(product.Sharpness == originalProduct!.Sharpness && Data(product.Effects) == Data(originalProduct.Effects)
                && Data(product.Calculation) == Data(originalProduct.Calculation), "同性质异名材料复用全部成器效果与预算");
            Check(product.InputHash != originalProduct.InputHash && product.Sources.Any(source => source.Material.Name == NewMaterial),
                "新材料身份与来源独立保存");
            var owner = Actor(model, draft); var human = (HumanData)owner.entity_data!;
            Check(owner.GetForgingStock(model).ContainsKey(NewMaterial), "正式人物库存读取新材料的实际物品路径");
            var stock = owner.GetForgingStock(model);
            Check(owner.TryForge(model, draft, out var result) && result.Success, "正式入口重建模型后仍能使用新材料成器");
            Check(result.Required.All(pair => owner.GetForgingStock(model)[pair.Key] == stock[pair.Key] - pair.Value)
                && human.current_world_stamina == 100 - result.Energy, "新材料真实扣料扣精力且同材跨岗合计");
            var item = human.artifact_inventory.Single();
            Check(owner.equip_artifact(item.instance_id) && owner.get_equipped_forged_sword()?.instance_id == item.instance_id
                && owner.get_used_artifact_spirit() == 0, "新材料产物进入现有剑装备接口且装备不占灵识");
            var skills = owner.get_artifact_skills().ToList();
            originalProduct.InstanceId = "material-reference";
            var expectedSkills = ForgedSwordBattleAdapter.Skills(ForgedArtifactInventory.Create(originalProduct)).ToList();
            Check(skills.Count == expectedSkills.Count && skills.Count == ForgedSwordBattleAdapter.Skills(item).Count(),
                "新材与原材按实际成器能力一致生成技能，不凭名称补发主动能力");
            Check(owner.set_combat_skill_slot_paths(new Godot.Collections.Array<string>(skills.Select(ArtifactSkillFactory.Key)))
                && skills.All(skill => owner.resolve_combat_skill(ArtifactSkillFactory.Key(skill)) is not null),
                skills.Count > 0 ? "新材料产物的实际主动能力可装槽并解析" : "本炉无主动能力时不生成或自动占用技能位置");
            Check(result.Product!.Effects.Any(effect => effect.CarrierId == "binding")
                && owner.get_skill_rewrite_choices().Any(choice => choice.Id == "forged:" + item.instance_id && choice.Available),
                "新材料形成的化法进入现有绑定来源");
            Check(owner.get_artifact_bonds() == ArtifactBondRuntime.Aggregate([item]), "新材料产物的器蕴复用现有累计");
            DirAccess.MakeDirRecursiveAbsolute("user://tests");
            Check(ResourceSaver.Save(new PersistentGameSave { player_data = human }, SavePath) == Error.Ok, "新材料成器随正式保存容器写入");
            var saved = ResourceLoader.Load<PersistentGameSave>(SavePath, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            var restored = new Character { entity_data = saved.player_data! }; _actors.Add(restored);
            Check(ForgedArtifactInventory.TryRead(saved.player_data!.artifact_inventory.Single(), out var archived)
                && archived.Sources.Any(source => source.Material.Name == NewMaterial)
                && restored.get_equipped_forged_sword()?.instance_id == item.instance_id, "读回恢复新材料来源与装备实例");
            // 删除 live 资源后，只靠成器自带数据重演，避免未来材料删除或修改使旧器物失效。
            DirAccess.RemoveAbsolute(ItemPath);
            Check(!new ProcessForgingTrial().Materials.ContainsKey(NewMaterial), "移除材料后重新发现会剔除目录缓存，不保留幽灵候选");
            Check(ProcessForgingTrial.Replay(archived).Product!.InputHash == archived.InputHash,
                "材料不再位于当前目录时，成器仍能独立重演");
            var invalid = Alias(original.Materials["铜髓矿"], "非法加工档案测试材", "smoke_forging_invalid");
            ((MaterialCraftData)invalid.craft_data!).forging!.volume = 0;
            ResourceSaver.Save(invalid, InvalidPath);
            bool rejected = false;
            try { _ = new ProcessForgingTrial(); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains("物理加工档案"); }
            Check(rejected, "非法加工属性明确拒绝，不回退到名字或质地默认值");
            GD.Print("FORGING_MATERIAL_DISCOVERY_OK " + _checks);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); return; }
        finally
        {
            foreach (var actor in _actors) actor.Free();
            if (createdFolder)
            {
                foreach (var path in new[] { ItemPath, MissingPath, InvalidPath })
                    if (Godot.FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(path);
                DirAccess.RemoveAbsolute(Folder);
            }
            if (Godot.FileAccess.FileExists(SavePath)) DirAccess.RemoveAbsolute(SavePath);
        }
        GetTree().Quit();
    }
}
