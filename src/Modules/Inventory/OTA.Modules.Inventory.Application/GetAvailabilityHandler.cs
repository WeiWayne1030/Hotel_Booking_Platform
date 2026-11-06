using OTA.Modules.Inventory.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Application;

public sealed record GetAvailabilityQuery(Guid RoomTypeId, DateOnly CheckIn, DateOnly CheckOut);

public sealed record NightAvailability(DateOnly Date, int AvailableRooms, decimal Price);

/// <param name="AvailableRooms">整段住宿可訂的房數 (每晚剩餘量的最小值)</param>
/// <param name="PricePerRoom">每間房整段住宿的總價 (每晚價格加總)</param>
public sealed record StayAvailabilityResult(
    int AvailableRooms, decimal PricePerRoom, IReadOnlyList<NightAvailability> Nights);

/// <summary>REQ-INV-01：查詢指定房型、指定區間的每日剩餘房數與售價。</summary>
public sealed class GetAvailabilityHandler(IInventoryStore store)
{
    public async Task<Result<StayAvailabilityResult>> HandleAsync(
        GetAvailabilityQuery query, CancellationToken ct = default)
    {
        var rooms = await store.GetRangeAsync(query.RoomTypeId, query.CheckIn, query.CheckOut, ct);

        var available = StayAvailability.Calculate(rooms, query.CheckIn, query.CheckOut);
        if (available.IsFailure) return Result.Failure<StayAvailabilityResult>(available.Error);

        var nights = rooms
            .OrderBy(x => x.StayDate)
            .Select(x => new NightAvailability(x.StayDate, x.AvailableRooms, x.Price))
            .ToList();

        return new StayAvailabilityResult(available.Value, nights.Sum(n => n.Price), nights);
    }
}
