using OTA.Modules.Inventory.Application;
using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database.Seed;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Tests;

public class HoldInventoryHandlerTests
{
    private static readonly DateOnly Dec1 = new(2026, 12, 1);

    private static HoldInventoryCommand Command(int quantity = 1, int nights = 3, Guid? roomType = null) =>
        new(Guid.NewGuid(), roomType ?? InventoryMockData.ScarceRoomTypeId,
            Dec1, Dec1.AddDays(nights), quantity, TimeSpan.FromMinutes(15));

    [Fact]
    public async Task Hold_CreatesOneHoldPerNight()
    {
        var store = new FakeInventoryStore();

        var result = await new HoldInventoryHandler(store).HandleAsync(Command(quantity: 2, nights: 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, store.SavedHolds.Count);
        Assert.All(store.SavedHolds, h => Assert.Equal(2, h.HeldRooms));
    }

    [Fact]
    public async Task Hold_MoreThanAvailable_FailsWithoutSaving()
    {
        var store = new FakeInventoryStore();

        var result = await new HoldInventoryHandler(store).HandleAsync(Command(quantity: 6)); // 只有 5 間

        Assert.Equal(InventoryErrors.InsufficientRooms, result.Error);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Hold_Conflict_RereadsAndRetries()
    {
        var store = new FakeInventoryStore(conflictsBeforeSuccess: 1);

        var result = await new HoldInventoryHandler(store).HandleAsync(Command(nights: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, store.ReadCount);          // 重試時有重新讀取
        Assert.Equal(2, store.SavedHolds.Count);   // 沒有因為重試而重複建立佔位
    }

    [Fact]
    public async Task Hold_ConflictEveryTime_GivesUpWithConflict()
    {
        var store = new FakeInventoryStore(conflictsBeforeSuccess: 99);

        var result = await new HoldInventoryHandler(store).HandleAsync(Command());

        Assert.Equal(InventoryErrors.ConcurrencyConflict, result.Error);
        Assert.Empty(store.SavedHolds);
    }

    [Fact]
    public async Task Hold_DateRangeOutsideSeededData_Fails()
    {
        var store = new FakeInventoryStore();
        var cmd = Command() with { CheckIn = Dec1.AddDays(40), CheckOut = Dec1.AddDays(42) };

        var result = await new HoldInventoryHandler(store).HandleAsync(cmd);

        Assert.Equal(InventoryErrors.DateNotAvailable, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Hold_InvalidQuantity_Fails(int quantity)
    {
        var result = await new HoldInventoryHandler(new FakeInventoryStore()).HandleAsync(Command(quantity));

        Assert.Equal(InventoryErrors.InvalidQuantity, result.Error);
    }
}
