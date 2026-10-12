/// <summary>
/// 行囊物品使用扩展点。具体物品效果尚未定义时不注册处理器，
/// 因而行囊不会显示可执行的“使用”操作。
/// </summary>
public interface IInventoryItemUseHandler
{
    /// <summary>检查该物品在当前角色与数量下是否可以使用。</summary>
    bool CanUse(Character user, ItemData item, int count);

    /// <summary>执行已定义的效果，并由实现方在成功时处理物品消耗。</summary>
    bool TryUse(Character user, ItemData item, int count);
}
