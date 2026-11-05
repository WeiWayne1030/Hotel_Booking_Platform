using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OTA.Modules.Inventory.Domain;

namespace OTA.Modules.Inventory.Infrastructure.Database.Configurations;

/// <summary>
/// InventoryHold 實體與 PostgreSQL 資料表映射組態
/// </summary>
internal sealed class InventoryHoldConfiguration : IEntityTypeConfiguration<InventoryHold>
{
    public void Configure(EntityTypeBuilder<InventoryHold> builder)
    {
        // 1. 指定資料表名稱
        builder.ToTable("inventory_holds", t =>
        {
            t.HasCheckConstraint("ck_inventory_holds_held_rooms", "held_rooms > 0");
        });

        // 2. 主鍵
        builder.HasKey(x => x.Id);

        // 3. 屬性欄位映射
        builder.Property(x => x.BookingId)
            .IsRequired();

        builder.Property(x => x.RoomTypeId)
            .IsRequired();

        builder.Property(x => x.StayDate)
            .IsRequired();

        builder.Property(x => x.HeldRooms)
            .IsRequired();

        // 列舉轉換為字串存儲，方便資料庫直接檢視狀態 (例如 "Held", "Confirmed", "Released")
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // 4. 索引：為「未付款過期佔位清理排程」建立複合索引，大幅提升背景工作掃描速度
        builder.HasIndex(x => new { x.Status, x.ExpiresAt })
            .HasDatabaseName("idx_holds_status_expires_at");

        // 5. 忽略 DDD 領域事件
        builder.Ignore(x => x.DomainEvents);
    }
}
