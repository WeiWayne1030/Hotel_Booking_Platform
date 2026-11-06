using OTA.Modules.Inventory.Application;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Api.Endpoints;

public static class InventoryEndpoints
{
    public sealed record HoldRequest(
        Guid BookingId, Guid RoomTypeId, DateOnly CheckIn, DateOnly CheckOut, int Quantity);

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory");

        // REQ-INV-01：查房態與價格
        group.MapGet("/availability", async (
            Guid roomTypeId, DateOnly checkIn, DateOnly checkOut,
            GetAvailabilityHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetAvailabilityQuery(roomTypeId, checkIn, checkOut), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        // REQ-INV-02：佔位 15 分鐘 (之後 Booking 模組建單時會呼叫，這裡先開放手動測試)
        group.MapPost("/holds", async (
            HoldRequest request, HoldInventoryHandler handler, CancellationToken ct) =>
        {
            var command = new HoldInventoryCommand(
                request.BookingId, request.RoomTypeId, request.CheckIn, request.CheckOut,
                request.Quantity, TimeSpan.FromMinutes(15));

            var result = await handler.HandleAsync(command, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
        });

        return app;
    }

    /// <summary>把業務錯誤對應成 HTTP 狀態碼。</summary>
    private static IResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(title: error.Code, detail: error.Description, statusCode: status);
    }
}
