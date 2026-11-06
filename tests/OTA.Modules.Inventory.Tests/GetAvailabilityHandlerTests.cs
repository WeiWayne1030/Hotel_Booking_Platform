using OTA.Modules.Inventory.Application;
using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database.Seed;

namespace OTA.Modules.Inventory.Tests;

public class GetAvailabilityHandlerTests
{
    private static readonly DateOnly Dec1 = FakeInventoryStore.SeedStart;

    [Fact]
    public async Task Returns_PerNightDetail_AndTotalPrice()
    {
        // 12/1(二)、12/2(三)、12/3(四)皆為平日 4800
        var query = new GetAvailabilityQuery(InventoryMockData.ScarceRoomTypeId, Dec1, Dec1.AddDays(3));

        var result = await new GetAvailabilityHandler(new FakeInventoryStore()).HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Nights.Count);
        Assert.Equal(5, result.Value.AvailableRooms);
        Assert.Equal(4800m * 3, result.Value.PricePerRoom);
    }

    [Fact]
    public async Task WeekendNights_AreMoreExpensive()
    {
        // 12/4(五)、12/5(六) 週末 6800
        var query = new GetAvailabilityQuery(InventoryMockData.ScarceRoomTypeId, Dec1.AddDays(3), Dec1.AddDays(5));

        var result = await new GetAvailabilityHandler(new FakeInventoryStore()).HandleAsync(query);

        Assert.Equal(6800m * 2, result.Value.PricePerRoom);
    }

    [Fact]
    public async Task InvalidRange_ReturnsError()
    {
        var query = new GetAvailabilityQuery(InventoryMockData.ScarceRoomTypeId, Dec1, Dec1);

        var result = await new GetAvailabilityHandler(new FakeInventoryStore()).HandleAsync(query);

        Assert.Equal(InventoryErrors.InvalidDateRange, result.Error);
    }
}
