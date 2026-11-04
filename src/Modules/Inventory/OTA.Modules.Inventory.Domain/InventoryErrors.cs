using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Domain;

/// <summary>
/// Inventory 模組的業務錯誤定義
/// </summary>
public static class InventoryErrors
{
    public static readonly Error InsufficientRooms = Error.Conflict(
        "Inventory.InsufficientRooms", "該日期可用房數不足。");

    public static readonly Error InvalidQuantity = Error.Validation(
        "Inventory.InvalidQuantity", "數量必須大於 0。");

    public static readonly Error ExceedsTotalRooms = Error.Validation(
        "Inventory.ExceedsTotalRooms", "釋放後的可用房數不可超過總房數。");

    public static readonly Error HoldNotActive = Error.Conflict(
        "Inventory.HoldNotActive", "佔位狀態不允許此操作。");
}
