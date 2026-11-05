using Microsoft.EntityFrameworkCore;
using OTA.Modules.Inventory.Application;
using OTA.Modules.Inventory.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Infrastructure.Database;

/// <summary><see cref="IInventoryStore"/> 的 EF Core 實作。</summary>
internal sealed class EfInventoryStore(InventoryDbContext db) : IInventoryStore
{
    public Task<List<RoomInventory>> GetRangeAsync(
        Guid roomTypeId, DateOnly from, DateOnly toExclusive, CancellationToken ct) =>
        db.RoomInventories
            .Where(x => x.RoomTypeId == roomTypeId && x.StayDate >= from && x.StayDate < toExclusive)
            .OrderBy(x => x.StayDate)
            .ToListAsync(ct);

    public void AddHold(InventoryHold hold) => db.InventoryHolds.Add(hold);

    public async Task<Result> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            // SaveChanges 預設包在單一交易：全部成功或全部回滾
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            // UPDATE ... WHERE version = @old 影響 0 列：別人先改了。
            // 必須清掉追蹤中的舊物件，否則重試時 EF 會直接回傳舊的快取資料而非 DB 最新值。
            db.ChangeTracker.Clear();
            return Result.Failure(InventoryErrors.ConcurrencyConflict);
        }
    }
}
