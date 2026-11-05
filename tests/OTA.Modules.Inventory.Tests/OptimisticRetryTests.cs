using OTA.Modules.Inventory.Application;
using OTA.Modules.Inventory.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Tests;

public class OptimisticRetryTests
{
    [Fact]
    public async Task ConflictThenSuccess_RetriesAndSucceeds()
    {
        var calls = 0;

        var result = await OptimisticRetry.ExecuteAsync(() =>
        {
            calls++;
            return Task.FromResult(calls == 1
                ? Result.Failure(InventoryErrors.ConcurrencyConflict)
                : Result.Success());
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AlwaysConflict_StopsAtMaxAttempts()
    {
        var calls = 0;

        var result = await OptimisticRetry.ExecuteAsync(() =>
        {
            calls++;
            return Task.FromResult(Result.Failure(InventoryErrors.ConcurrencyConflict));
        }, maxAttempts: 3);

        Assert.Equal(InventoryErrors.ConcurrencyConflict, result.Error);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task BusinessFailure_IsNotRetried()
    {
        var calls = 0;

        var result = await OptimisticRetry.ExecuteAsync(() =>
        {
            calls++;
            return Task.FromResult(Result.Failure(InventoryErrors.InsufficientRooms));
        });

        Assert.Equal(InventoryErrors.InsufficientRooms, result.Error);
        Assert.Equal(1, calls); // 庫存不足重試也沒用
    }
}
