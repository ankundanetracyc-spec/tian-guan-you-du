public sealed class AlchemyPillUseHandler : IInventoryItemUseHandler
{
    public bool CanUse(Character user, ItemData item, int count) => count > 0 && item is PillItemData pill && user.CanUseAlchemyPill(pill);
    public bool TryUse(Character user, ItemData item, int count) => count > 0 && item is PillItemData pill && user.TryUseAlchemyPill(pill);
}
