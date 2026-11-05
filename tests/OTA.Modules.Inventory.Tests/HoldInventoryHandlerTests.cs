using OTA.Modules.Inventory.Application;
using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database.Seed;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Tests;

public class HoldInventoryHandlerTests
{
    private static readonly DateOnly Dec1 = new(2026, 12, 1);

    /// <summary>假的 store：每次讀取都產生「剛從 DB 讀出來」的新物件，並可設定前 N 次存檔回報衝突。</summary>
    private sealed class FakeStore(int conflictsBeforeSuccess = 0) : IInventoryStore
    {
        public int ReadCount { get; private set; }
        public int SaveCount { get; private set; }
        public List<InventoryHold> SavedHolds { get; } = [];
        private readonly List<InventoryHold> _pending = [];

        public Task<List<RoomInventory>> GetRangeAsync(Guid roomTypeId, DateOnly from, DateOnly toExclusive, CancellationToken ct)
        {
            ReadCount++;
            _pending.Clear(); // 重新讀取代表上一輪未存檔的變更已被丟棄
            var rooms = InventoryMockData.CreateRoomInventories(Dec1, 30)
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

    private static HoldInventoryCommand Command(int quantity = 1, int nights = 3, Guid? roomType = null) =>
        new(Guid.NewGuid(), roomType ?? InventoryMockData.ScarceRoomTypeId,
            Dec1, Dec1.AddDays(nights), quantity, TimeSpan.FromMinutes(15));

    [Fact]
    public async Task Hold_CreatesOneHoldPerNight()
    {
        var store = new FakeStore();

        var result = await new HoldInventoryHandler(store).HandleAsync(Command(quantity: 2, nights: 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, store.SavedHolds.Count);
        Assert.All(store.SavedHolds, h => Assert.Equal(2, h.HeldRooms));
    }

    [Fact]
    public async Task Hold_MoreThanAvailable_FailsWithoutSaving()
    {
        var store = new FakeStore();

        var result = await new HoldInventoryHandler(store).HandleAsync(Command(quantity: 6)); // 只有 5 間

        Assert.Equal(InventoryErrors.InsufficientRooms, result.Error);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Hold_Conflict_RereadsAndRetries()
    {
        var store = new FakeStore(conflictsBeforeSuccess: 1);

        var result = await new HoldInventoryHandler(store).HandleAsync(Command(nights: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, store.ReadCount);          // 重試時有重新讀取
        Assert.Equal(2, store.SavedHolds.Count);   // 沒有因為重試而重複建立佔位
    }

    [Fact]
    public async Task Hold_ConflictEveryTime_GivesUpWithConflict()
    {
        var store = new FakeStore(conflictsBeforeSuccess: 99);

        var result = await new HoldInventoryHandler(store).HandleAsync(Command());

        Assert.Equal(InventoryErrors.ConcurrencyConflict, result.Error);
        Assert.Empty(store.SavedHolds);
    }

    [Fact]
    public async Task Hold_DateRangeOutsideSeededData_Fails()
    {
        var store = new FakeStore();
        var cmd = Command() with { CheckIn = Dec1.AddDays(40), CheckOut = Dec1.AddDays(42) };

        var result = await new HoldInventoryHandler(store).HandleAsync(cmd);

        Assert.Equal(InventoryErrors.DateNotAvailable, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Hold_InvalidQuantity_Fails(int quantity)
    {
        var result = await new HoldInventoryHandler(new FakeStore()).HandleAsync(Command(quantity));

        Assert.Equal(InventoryErrors.InvalidQuantity, result.Error);
    }
}
