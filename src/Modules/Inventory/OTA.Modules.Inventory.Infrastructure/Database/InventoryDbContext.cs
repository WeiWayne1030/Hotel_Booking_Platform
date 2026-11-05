using Microsoft.EntityFrameworkCore;
using OTA.Modules.Inventory.Domain;

namespace OTA.Modules.Inventory.Infrastructure.Database;

/// <summary>
/// Inventory 模組專屬的資料庫上下文 (DbContext)。
/// 嚴格封裝在 inventory schema 下，只管理庫存與佔位相關的資料表。
/// </summary>
public sealed class InventoryDbContext : DbContext
{
    public const string Schema = "inventory";

    public DbSet<RoomInventory> RoomInventories => Set<RoomInventory>();
    public DbSet<InventoryHold> InventoryHolds => Set<InventoryHold>();

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 指定此 DbContext 內的所有資料表預設建立在 "inventory" schema 下
        modelBuilder.HasDefaultSchema(Schema);

        // 自動掃描並套用當前組件 (Assembly) 裡所有的 IEntityTypeConfiguration 類別
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }
}
