using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OTA.Modules.Inventory.Domain;

namespace OTA.Modules.Inventory.Infrastructure.Database.Configurations;

/// <summary>
/// RoomInventory 實體與 PostgreSQL 資料表映射組態
/// </summary>
internal sealed class RoomInventoryConfiguration : IEntityTypeConfiguration<RoomInventory>
{
    public void Configure(EntityTypeBuilder<RoomInventory> builder)
    {
        // 1. 指定資料表名稱與 CHECK 約束（防超賣雙保險）
        builder.ToTable("room_inventories", t =>
        {
            t.HasCheckConstraint("ck_room_inventories_available_rooms", "available_rooms >= 0");
            t.HasCheckConstraint("ck_room_inventories_total_rooms", "total_rooms >= 0");
        });

        // 2. 主鍵
        builder.HasKey(x => x.Id);

        // 3. 業務唯一索引：同一房型在同一天只能有一筆庫存記錄
        builder.HasIndex(x => new { x.RoomTypeId, x.StayDate })
            .IsUnique()
            .HasDatabaseName("uq_room_type_date");

        // 4. 屬性欄位映射
        builder.Property(x => x.RoomTypeId)
            .IsRequired();

        builder.Property(x => x.StayDate)
            .IsRequired();

        builder.Property(x => x.TotalRooms)
            .IsRequired();

        builder.Property(x => x.AvailableRooms)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasPrecision(10, 2)
            .IsRequired();

        // 5. 【高併發核心】：將 Version 標記為樂觀鎖併發 Token
        // EF Core 產生 UPDATE 時會自動附加條件：WHERE version = @oldVersion
        //IsConcurrencyToken為高併發標記
        builder.Property(x => x.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        // 6. 忽略 DDD 領域事件（不需要存進資料庫欄位）
        builder.Ignore(x => x.DomainEvents);
    }
}
