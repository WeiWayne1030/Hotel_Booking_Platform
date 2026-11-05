using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database.Seed;

namespace OTA.Modules.Inventory.Tests;

public class InventoryHoldTests
{
    private static InventoryHold NewHold(TimeSpan? duration = null) =>
        InventoryHold.Create(
            Guid.NewGuid(), InventoryMockData.ScarceRoomTypeId,
            new DateOnly(2026, 12, 1), heldRooms: 1, duration ?? TimeSpan.FromMinutes(15));

    [Fact]
    public void Create_StartsAsHeld_WithExpiry()
    {
        var hold = NewHold();

        Assert.Equal(InventoryHoldStatus.Held, hold.Status);
        Assert.True(hold.ExpiresAt > hold.CreatedAt);
    }

    [Fact]
    public void Create_ZeroRooms_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InventoryHold.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 12, 1), 0, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void IsExpired_OnlyWhenHeldAndPastExpiry()
    {
        var hold = NewHold(TimeSpan.FromMinutes(15));

        Assert.False(hold.IsExpired(hold.CreatedAt.AddMinutes(14)));
        Assert.True(hold.IsExpired(hold.CreatedAt.AddMinutes(16)));
    }

    [Fact]
    public void Confirm_ThenRelease_Fails()
    {
        var hold = NewHold();

        Assert.True(hold.Confirm().IsSuccess);
        var release = hold.MarkReleased();

        Assert.Equal(InventoryErrors.HoldNotActive, release.Error);
        Assert.Equal(InventoryHoldStatus.Confirmed, hold.Status);
    }

    [Fact]
    public void ExpiredHold_IsNoLongerExpired_AfterRelease()
    {
        // 已釋放的佔位不該被排程重複撈出來釋放 (避免庫存被加回兩次)
        var hold = NewHold();
        hold.MarkReleased();

        Assert.False(hold.IsExpired(hold.ExpiresAt.AddHours(1)));
    }
}
