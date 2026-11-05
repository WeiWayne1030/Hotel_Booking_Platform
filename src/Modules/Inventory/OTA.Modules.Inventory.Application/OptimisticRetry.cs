using OTA.Modules.Inventory.Domain;
using OTA.Shared.Kernel.Primitives;

namespace OTA.Modules.Inventory.Application;

/// <summary>
/// 樂觀鎖重試：遇到 <see cref="InventoryErrors.ConcurrencyConflict"/> 時重新執行整個操作
/// (重新讀取最新資料 → 重新計算 → 再嘗試寫入)，其他結果 (成功或業務失敗) 直接回傳。
/// 本類別不依賴 DB；由呼叫端把 DbUpdateConcurrencyException 轉成 ConcurrencyConflict。
/// </summary>
public static class OptimisticRetry
{
    public static async Task<Result> ExecuteAsync(
        Func<Task<Result>> attempt, int maxAttempts = 3)
    {
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        var result = Result.Failure(InventoryErrors.ConcurrencyConflict);

        for (var i = 0; i < maxAttempts; i++)
        {
            result = await attempt();

            // 只有「衝突」值得重試；庫存不足重試也不會變多
            if (result.IsSuccess || result.Error != InventoryErrors.ConcurrencyConflict)
                return result;
        }

        return result; // 用完次數仍衝突，回傳衝突讓上層決定怎麼回應使用者
    }
}
