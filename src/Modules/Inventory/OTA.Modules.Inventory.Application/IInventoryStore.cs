using OTA.Modules.Inventory.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Application;

/// <summary>
/// Application 層需要的資料存取最小介面，由 Infrastructure 以 EF Core 實作。
/// 這樣 handler 不必參考 EF，也能用假實作做單元測試。
/// </summary>
public interface IInventoryStore
{
    /// <summary>取得 [from, toExclusive) 的每日庫存 (會被追蹤，修改後可一併存檔)。</summary>
    Task<List<RoomInventory>> GetRangeAsync(
        Guid roomTypeId, DateOnly from, DateOnly toExclusive, CancellationToken ct);

    void AddHold(InventoryHold hold);

    /// <summary>
    /// 一次存檔 (同一交易)。若發生樂觀鎖衝突，回傳 <see cref="InventoryErrors.ConcurrencyConflict"/>，
    /// 並丟棄已追蹤的舊資料，讓下一次重試能讀到最新版本。
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken ct);
}
