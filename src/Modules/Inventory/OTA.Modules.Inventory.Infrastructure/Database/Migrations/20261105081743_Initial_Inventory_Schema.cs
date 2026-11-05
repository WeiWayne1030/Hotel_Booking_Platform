using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OTA.Modules.Inventory.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Inventory_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "inventory_holds",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_date = table.Column<DateOnly>(type: "date", nullable: false),
                    held_rooms = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_holds", x => x.id);
                    table.CheckConstraint("ck_inventory_holds_held_rooms", "held_rooms > 0");
                });

            migrationBuilder.CreateTable(
                name: "room_inventories",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_rooms = table.Column<int>(type: "integer", nullable: false),
                    available_rooms = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_inventories", x => x.id);
                    table.CheckConstraint("ck_room_inventories_available_rooms", "available_rooms >= 0");
                    table.CheckConstraint("ck_room_inventories_total_rooms", "total_rooms >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "idx_holds_status_expires_at",
                schema: "inventory",
                table: "inventory_holds",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_room_type_date",
                schema: "inventory",
                table: "room_inventories",
                columns: new[] { "room_type_id", "stay_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_holds",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "room_inventories",
                schema: "inventory");
        }
    }
}
