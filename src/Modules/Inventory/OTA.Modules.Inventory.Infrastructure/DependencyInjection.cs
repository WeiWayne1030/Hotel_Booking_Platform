using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OTA.Modules.Inventory.Application;
using OTA.Modules.Inventory.Infrastructure.Database;

namespace OTA.Modules.Inventory.Infrastructure;

/// <summary>
/// Inventory 模組的依賴注入 (DI) 註冊進入點
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' was not found.");

        services.AddDbContext<InventoryDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // 【模組化單體關鍵】：將該模組的 Migration 歷程記錄表放在自己所屬的 "inventory" schema 內，
                // 避免與其他模組 (如 booking、catalog) 互相干擾或衝突
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema);
            })
            // 欄位/索引名稱自動轉 snake_case (HeldRooms -> held_rooms)，
            // 與 CHECK constraint 及 docs/database-design.md 的命名一致
            .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IInventoryStore, EfInventoryStore>();
        services.AddScoped<HoldInventoryHandler>();

        return services;
    }
}
