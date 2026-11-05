using OTA.Modules.Inventory.Domain;

namespace OTA.Modules.Inventory.Infrastructure.Database.Seed;

/// <summary>
/// 開發/測試用的假資料。固定的 RoomTypeId 讓測試與之後的 API 手動驗證都能引用同一批資料。
/// 之後 DB 建好後，可由開發環境的 seeder 呼叫 <see cref="CreateRoomInventories"/> 寫入。
/// </summary>
public static class InventoryMockData
{
    /// <summary>熱門房型：只有 5 間，用來模擬搶購與防超賣。</summary>
    public static readonly Guid ScarceRoomTypeId = Guid.Parse("00000000-0000-0000-0000-000000000101");

    /// <summary>一般房型：庫存充足。</summary>
    public static readonly Guid NormalRoomTypeId = Guid.Parse("00000000-0000-0000-0000-000000000102");

    /// <summary>
    /// 產生從 startDate 起連續 days 天、兩種房型的每日庫存。
    /// </summary>
    public static List<RoomInventory> CreateRoomInventories(DateOnly startDate, int days = 30)
    {
        var result = new List<RoomInventory>();

        for (var i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var isWeekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

            result.Add(RoomInventory.Create(ScarceRoomTypeId, date, totalRooms: 5, price: isWeekend ? 6800m : 4800m));
            result.Add(RoomInventory.Create(NormalRoomTypeId, date, totalRooms: 50, price: isWeekend ? 3200m : 2400m));
        }

        return result;
    }
}
