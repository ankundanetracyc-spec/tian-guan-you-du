using Godot;

/// <summary>宗门测试法器商品；兑换时生成独立成器快照，不作为普通可堆叠物品入包。</summary>
public partial class SectArtifactOfferData : ItemData
{
    [Export] public int example_index = 1;
    public ArtifactInstanceData CreateArtifact()
    {
        var model = new ProcessForgingTrial();
        var draft = model.ExampleDraft(example_index); draft.Proficiency = 60;
        var result = model.Evaluate(draft);
        if (!result.Success || result.Product is not { Tier: "法器" } snapshot)
            throw new InvalidOperationException("宗门法器样本未达到法器水准，兑换已停止。");
        snapshot.Name = entity_name; snapshot.InstanceId = "sect-artifact-" + Guid.NewGuid().ToString("N");
        var item = ForgedArtifactInventory.Create(snapshot);
        item.layout_preview = layout_preview?.Duplicate(true) as ItemLayoutPreviewData;
        item.control_preview = control_preview?.Duplicate(true) as SwordControlPreviewData;
        return item;
    }
}
