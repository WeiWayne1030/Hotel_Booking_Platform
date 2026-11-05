using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database.Seed;

namespace OTA.Modules.Inventory.Tests;

public class RoomInventoryTests
{
    private static RoomInventory NewScarceRoom() =>
        InventoryMockData.CreateRoomInventories(new DateOnly(2026, 12, 1), days: 1)
            .First(x => x.RoomTypeId == InventoryMockData.ScarceRoomTypeId);

    [Fact]
    public void Create_SetsAvailableEqualToTotal_AndVersionZero()
    {
        var room = NewScarceRoom();

        Assert.Equal(5, room.TotalRooms);
        Assert.Equal(5, room.AvailableRooms);
        Assert.Equal(0, room.Version);
    }

    [Fact]
    public void Reserve_ReducesAvailable_AndBumpsVersion()
    {
        var room = NewScarceRoom();

        var result = room.Reserve(2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, room.AvailableRooms);
        Assert.Equal(1, room.Version);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_FailsAndKeepsStateUnchanged()
    {
        var room = NewScarceRoom();

        var result = room.Reserve(6);

        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientRooms, result.Error);
        Assert.Equal(5, room.AvailableRooms);
        Assert.Equal(0, room.Version); // 失敗不應該動到版號
    }

    [Fact]
    public void Reserve_LastRoomThenOneMore_SecondFails()
    {
        // 模擬「只剩最後一間」：第一個成功、第二個失敗，庫存不會變負數
        var room = NewScarceRoom();
        room.Reserve(4);

        Assert.True(room.Reserve(1).IsSuccess);
        var second = room.Reserve(1);

        Assert.True(second.IsFailure);
        Assert.Equal(0, room.AvailableRooms);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_NonPositiveQuantity_Fails(int quantity)
    {
        var result = NewScarceRoom().Reserve(quantity);

        Assert.Equal(InventoryErrors.InvalidQuantity, result.Error);
    }

    [Fact]
    public void Release_RestoresAvailable()
    {
        var room = NewScarceRoom();
        room.Reserve(3);

        var result = room.Release(2);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, room.AvailableRooms);
    }

    [Fact]
    public void Release_BeyondTotal_Fails()
    {
        var room = NewScarceRoom();

        var result = room.Release(1); // 還沒扣就釋放，會超過總房數

        Assert.Equal(InventoryErrors.ExceedsTotalRooms, result.Error);
        Assert.Equal(5, room.AvailableRooms);
    }
}
