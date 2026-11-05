using OTA.Shared.Kernel.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Domain;

/// <summary>
/// 每日房態與庫存，對應 inventory.room_inventories。
/// 以 (RoomTypeId, StayDate) 為唯一key，Version 供opmistic lock使用。
/// </summary>
public sealed class RoomInventory : AggregateRoot<Guid>
{
    public Guid RoomTypeId { get; private set; }
    public DateOnly StayDate { get; private set; }
    public int TotalRooms { get; private set; }
    public int AvailableRooms { get; private set; }
    public decimal Price { get; private set; }

    /// <summary>opmistic lock (由 EF Core 設為 ConcurrencyToken)。</summary>
    public int Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RoomInventory() { }

    public static RoomInventory Create(Guid roomTypeId, DateOnly stayDate, int totalRooms, decimal price)
    {
        if (totalRooms < 0) throw new ArgumentOutOfRangeException(nameof(totalRooms));
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));

        return new RoomInventory
        {
            Id = Guid.NewGuid(),
            RoomTypeId = roomTypeId,
            StayDate = stayDate,
            TotalRooms = totalRooms,
            AvailableRooms = totalRooms,
            Price = price,
            Version = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>扣減庫存 (佔位)。庫存不足回傳失敗，不丟例外。</summary>
    public Result Reserve(int quantity)
    {
        if (quantity <= 0) return Result.Failure(InventoryErrors.InvalidQuantity);
        if (AvailableRooms < quantity) return Result.Failure(InventoryErrors.InsufficientRooms);

        AvailableRooms -= quantity;
        Touch();
        return Result.Success();
    }

    /// <summary>歸還庫存 (取消或佔位逾期)。</summary>
    public Result Release(int quantity)
    {
        if (quantity <= 0) return Result.Failure(InventoryErrors.InvalidQuantity);
        if (AvailableRooms + quantity > TotalRooms) return Result.Failure(InventoryErrors.ExceedsTotalRooms);

        AvailableRooms += quantity;
        Touch();
        return Result.Success();
    }

    private void Touch()
    {
        // 每次狀態發生變更時版號自增 +1。
        // EF Core 會自動以載入時的舊版號發出: UPDATE ... WHERE id = @id AND version = @oldVersion
        // 並將遞增後的新版號寫入資料庫
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
