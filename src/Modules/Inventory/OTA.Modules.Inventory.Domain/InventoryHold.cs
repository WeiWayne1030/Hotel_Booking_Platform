using OTA.Shared.Kernel.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Domain;

public enum InventoryHoldStatus
{
    Held = 0,
    Confirmed = 1,
    Released = 2
}

/// <summary>
/// 庫存佔位記錄，對應 inventory.inventory_holds。
/// 旅客進入結帳時建立，逾期未付款需釋放。
/// </summary>
public sealed class InventoryHold : AggregateRoot<Guid>
{
    public Guid BookingId { get; private set; }
    public Guid RoomTypeId { get; private set; }
    public DateOnly StayDate { get; private set; }
    public int HeldRooms { get; private set; }
    public InventoryHoldStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private InventoryHold() { }

    public static InventoryHold Create(
        Guid bookingId, Guid roomTypeId, DateOnly stayDate, int heldRooms, TimeSpan holdDuration)
    {
        if (heldRooms <= 0) throw new ArgumentOutOfRangeException(nameof(heldRooms));

        var now = DateTimeOffset.UtcNow;
        return new InventoryHold
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            RoomTypeId = roomTypeId,
            StayDate = stayDate,
            HeldRooms = heldRooms,
            Status = InventoryHoldStatus.Held,
            ExpiresAt = now.Add(holdDuration),
            CreatedAt = now
        };
    }

    public bool IsExpired(DateTimeOffset now) =>
        Status == InventoryHoldStatus.Held && ExpiresAt <= now;

    /// <summary>付款成功，佔位轉為永久確認。</summary>
    public Result Confirm()
    {
        if (Status != InventoryHoldStatus.Held) return Result.Failure(InventoryErrors.HoldNotActive);
        Status = InventoryHoldStatus.Confirmed;
        return Result.Success();
    }

    /// <summary>取消或逾期，標記為已釋放 (庫存回補由呼叫端對 RoomInventory 執行)。</summary>
    public Result MarkReleased()
    {
        if (Status != InventoryHoldStatus.Held) return Result.Failure(InventoryErrors.HoldNotActive);
        Status = InventoryHoldStatus.Released;
        return Result.Success();
    }
}
