using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OTA.Modules.Inventory.Domain;
using OTA.Modules.Inventory.Infrastructure.Database;

namespace OTA.Modules.Inventory.Tests;

/// <summary>
/// 不連資料庫，僅檢查 EF Core 的模型設定。
/// 這類測試能抓到「CHECK constraint 欄位名稱和實際欄位不一致」這種 migration 才會爆的問題。
/// </summary>
public class InventoryModelMappingTests
{
    private static InventoryDbContext CreateContext()
    {
        // 這個連線字串不會被使用 (只讀取 Model，不會真的連線)
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new InventoryDbContext(options);
    }

    [Fact]
    public void RoomInventory_ColumnNames_AreSnakeCase()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(RoomInventory))!;
        var table = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table("room_inventories", "inventory");

        var columns = entity.GetProperties().Select(p => p.GetColumnName(table)).ToHashSet();

        Assert.Contains("available_rooms", columns);
        Assert.Contains("total_rooms", columns);
        Assert.Contains("room_type_id", columns);
    }

    [Fact]
    public void CheckConstraints_ReferenceExistingColumns()
    {
        using var db = CreateContext();
        // CHECK constraint 只存在於 design-time model (migration 產生用的模型)
        var model = db.GetService<IDesignTimeModel>().Model;

        foreach (var type in new[] { typeof(RoomInventory), typeof(InventoryHold) })
        {
            var entity = model.FindEntityType(type)!;
            var table = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Create(entity, Microsoft.EntityFrameworkCore.Metadata.StoreObjectType.Table)!.Value;
            var columns = entity.GetProperties().Select(p => p.GetColumnName(table)!).ToList();

            foreach (var check in entity.GetCheckConstraints())
            {
                Assert.Contains(columns, c => check.Sql.Contains(c));
            }
        }
    }

    [Fact]
    public void RoomInventory_Version_IsConcurrencyToken()
    {
        using var db = CreateContext();
        var version = db.Model.FindEntityType(typeof(RoomInventory))!.FindProperty(nameof(RoomInventory.Version))!;

        Assert.True(version.IsConcurrencyToken);
    }

    [Fact]
    public void RoomInventory_RoomTypeAndDate_IsUnique()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(RoomInventory))!
            .GetIndexes().Single(i => i.GetDatabaseName() == "uq_room_type_date");

        Assert.True(index.IsUnique);
    }
}
