using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database.Seed;

namespace OTA.Modules.Inventory.Tests;

public class StayAvailabilityTests
{
    private static readonly DateOnly Dec1 = new(2026, 12, 1);

    private static List<RoomInventory> ScarceRoom30Days() =>
        InventoryMockData.CreateRoomInventories(Dec1, 30)
            .Where(x => x.RoomTypeId == InventoryMockData.ScarceRoomTypeId).ToList();

    [Fact]
    public void Calculate_TakesMinimumAcrossNights()
    {
        var rooms = ScarceRoom30Days();
        // 12/1=5, 12/2=2, 12/3=4 → 瓶頸是 12/2
        rooms.Single(x => x.StayDate == Dec1.AddDays(1)).Reserve(3);
        rooms.Single(x => x.StayDate == Dec1.AddDays(2)).Reserve(1);

        var result = StayAvailability.Calculate(rooms, Dec1, Dec1.AddDays(3));

        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void Calculate_CheckOutDay_IsNotCounted()
    {
        var rooms = ScarceRoom30Days();
        rooms.Single(x => x.StayDate == Dec1.AddDays(3)).Reserve(5); // 退房日全滿，不影響

        var result = StayAvailability.Calculate(rooms, Dec1, Dec1.AddDays(3));

        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Calculate_MissingNight_Fails()
    {
        var rooms = ScarceRoom30Days();

        var result = StayAvailability.Calculate(rooms, Dec1.AddDays(28), Dec1.AddDays(32)); // 超出已建立的 30 天

        Assert.Equal(InventoryErrors.DateNotAvailable, result.Error);
    }

    [Theory]
    [InlineData(0)]  // 入住 = 退房
    [InlineData(-1)] // 退房早於入住
    public void Calculate_InvalidRange_Fails(int nights)
    {
        var result = StayAvailability.Calculate(ScarceRoom30Days(), Dec1, Dec1.AddDays(nights));

        Assert.Equal(InventoryErrors.InvalidDateRange, result.Error);
    }
}
