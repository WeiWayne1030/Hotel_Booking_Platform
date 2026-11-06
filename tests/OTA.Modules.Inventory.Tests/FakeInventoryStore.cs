using OTA.Modules.Inventory.Application;
using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database.Seed;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Tests;

/// <summary>假的 store：每次讀取都產生「剛從 DB 讀出來」的新物件，並可設定前 N 次存檔回報衝突。</summary>
public sealed class FakeInventoryStore(int conflictsBeforeSuccess = 0) : IInventoryStore
{
    public static readonly DateOnly SeedStart = new(2026, 12, 1);

    public int ReadCount { get; private set; }
    public int SaveCount { get; private set; }
    public List<InventoryHold> SavedHolds { get; } = [];
    private readonly List<InventoryHold> _pending = [];

    public Task<List<RoomInventory>> GetRangeAsync(Guid roomTypeId, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        ReadCount++;
        _pending.Clear(); // 重新讀取代表上一輪未存檔的變更已被丟棄
        var rooms = InventoryMockData.CreateRoomInventories(FakeInventoryStore.SeedStart, 30)
            .Where(x => x.RoomTypeId == roomTypeId && x.StayDate >= from && x.StayDate < toExclusive)
            .ToList();
        return Task.FromResult(rooms);
    }

    public void AddHold(InventoryHold hold) => _pending.Add(hold);

    public Task<Result> SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        if (SaveCount <= conflictsBeforeSuccess)
            return Task.FromResult(Result.Failure(InventoryErrors.ConcurrencyConflict));

        SavedHolds.AddRange(_pending);
        return Task.FromResult(Result.Success());
    }
}
