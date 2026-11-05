using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Domain;

/// <summary>
/// REQ-INV-01：計算一段住宿區間的可售房數。
/// 住 N 晚就要 N 天都有房，所以可售量取「每晚剩餘房數的最小值」(瓶頸)。
/// </summary>
public static class StayAvailability
{
    /// <summary>
    /// 計算 [checkIn, checkOut) 的可售房數。退房日當天不算住宿，不納入計算。
    /// </summary>
    /// <param name="inventories">同一房型的每日庫存 (可包含區間外的日期，會自動忽略)</param>
    public static Result<int> Calculate(
        IEnumerable<RoomInventory> inventories, DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn) return Result.Failure<int>(InventoryErrors.InvalidDateRange);

        var byDate = inventories.ToDictionary(x => x.StayDate);
        var min = int.MaxValue;

        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            // 任何一晚沒有庫存資料 = 該晚不開放，整段都不能訂
            if (!byDate.TryGetValue(date, out var inventory))
                return Result.Failure<int>(InventoryErrors.DateNotAvailable);

            min = Math.Min(min, inventory.AvailableRooms);
        }

        return min;
    }
}
