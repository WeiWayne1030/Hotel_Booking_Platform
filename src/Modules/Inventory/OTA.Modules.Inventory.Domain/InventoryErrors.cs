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

    public static readonly Error InvalidDateRange = Error.Validation(
        "Inventory.InvalidDateRange", "退房日必須晚於入住日。");

    public static readonly Error DateNotAvailable = Error.NotFound(
        "Inventory.DateNotAvailable", "區間內有日期尚未開放銷售。");

    /// <summary>樂觀鎖衝突：同一筆庫存被別人先更新了，可重試。</summary>
    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Inventory.ConcurrencyConflict", "庫存同時被其他請求更新，請重試。");

    public static readonly Error HoldNotActive = Error.Conflict(
        "Inventory.HoldNotActive", "佔位狀態不允許此操作。");
}
