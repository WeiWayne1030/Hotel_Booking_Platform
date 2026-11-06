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

        Assert.True(hold.Confirm(hold.CreatedAt.AddMinutes(1)).IsSuccess);
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

    // ---- 逾期政策：Strict Expiration + Grace Period + Late Payment Revalidation ----
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    [Fact]
    public void Confirm_PaidBeforeExpiry_Succeeds()
    {
        var hold = NewHold();

        Assert.True(hold.Confirm(hold.ExpiresAt.AddSeconds(-1)).IsSuccess);
    }

    [Fact]
    public void Confirm_PaidAtExactExpiry_Succeeds()
    {
        var hold = NewHold();

        Assert.True(hold.Confirm(hold.ExpiresAt).IsSuccess); // 恰好等於期限仍有效
    }

    [Fact]
    public void Confirm_PaidAfterExpiry_FailsEvenWithinGracePeriod()
    {
        // 嚴格過期：寬限期只延後「釋放」，不放寬「付款時間」的判斷
        var hold = NewHold();

        var result = hold.Confirm(hold.ExpiresAt.AddSeconds(30));

        Assert.Equal(InventoryErrors.HoldExpired, result.Error);
        Assert.Equal(InventoryHoldStatus.Held, hold.Status); // 失敗不改狀態，庫存仍由佔位持有
    }

    [Fact]
    public void Confirm_AfterAlreadyReleased_FailsAsNotActive()
    {
        // 寬限期後已被排程釋放，晚到的付款走重新驗證流程 (重新佔位)
        var hold = NewHold();
        hold.MarkReleased();

        var result = hold.Confirm(hold.ExpiresAt.AddMinutes(-1));

        Assert.Equal(InventoryErrors.HoldNotActive, result.Error);
    }

    [Fact]
    public void IsReleasableBySweeper_NotDuringGracePeriod()
    {
        var hold = NewHold();

        Assert.False(hold.IsReleasableBySweeper(hold.ExpiresAt.AddSeconds(-1), Grace)); // 未逾期
        Assert.False(hold.IsReleasableBySweeper(hold.ExpiresAt.AddMinutes(1), Grace));  // 逾期但在寬限期
        Assert.True(hold.IsReleasableBySweeper(hold.ExpiresAt.Add(Grace), Grace));      // 寬限期結束
    }

    [Fact]
    public void IsReleasableBySweeper_ConfirmedHold_IsNeverReleasable()
    {
        var hold = NewHold();
        hold.Confirm(hold.CreatedAt);

        Assert.False(hold.IsReleasableBySweeper(hold.ExpiresAt.AddDays(1), Grace));
    }
}
