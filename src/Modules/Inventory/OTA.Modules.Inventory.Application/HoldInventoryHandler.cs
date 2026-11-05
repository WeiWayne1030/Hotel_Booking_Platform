using OTA.Modules.Inventory.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Application;

/// <summary>REQ-INV-02：為一筆預訂暫時佔住 [CheckIn, CheckOut) 每晚的房間。</summary>
public sealed record HoldInventoryCommand(
    Guid BookingId,
    Guid RoomTypeId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Quantity,
    TimeSpan HoldDuration);

/// <summary>
/// Level 1 樂觀鎖版本的佔位用例：
/// 讀取區間庫存 → 檢查每晚都夠 → 每晚扣減 + 建立佔位記錄 → 同一交易存檔。
/// 存檔時若有人搶先改了同一筆庫存 (Version 不符)，整個流程重來。
/// </summary>
public sealed class HoldInventoryHandler(IInventoryStore store)
{
    public Task<Result> HandleAsync(HoldInventoryCommand command, CancellationToken ct = default) =>
        OptimisticRetry.ExecuteAsync(() => TryHoldAsync(command, ct));

    private async Task<Result> TryHoldAsync(HoldInventoryCommand command, CancellationToken ct)
    {
        if (command.Quantity <= 0) return Result.Failure(InventoryErrors.InvalidQuantity);
        if (command.CheckOut <= command.CheckIn) return Result.Failure(InventoryErrors.InvalidDateRange);

        // 每次嘗試都重新讀取，才拿得到最新的庫存與 Version
        var rooms = await store.GetRangeAsync(command.RoomTypeId, command.CheckIn, command.CheckOut, ct);

        // 先整段檢查再動手，避免扣到一半才發現某晚不夠 (記憶體中的物件已被改動)
        var available = StayAvailability.Calculate(rooms, command.CheckIn, command.CheckOut);
        if (available.IsFailure) return Result.Failure(available.Error);
        if (available.Value < command.Quantity) return Result.Failure(InventoryErrors.InsufficientRooms);

        foreach (var room in rooms)
        {
            var reserved = room.Reserve(command.Quantity);
            if (reserved.IsFailure) return reserved;

            store.AddHold(InventoryHold.Create(
                command.BookingId, command.RoomTypeId, room.StayDate, command.Quantity, command.HoldDuration));
        }

        return await store.SaveChangesAsync(ct);
    }
}
