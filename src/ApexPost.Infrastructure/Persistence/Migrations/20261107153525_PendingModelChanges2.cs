using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedShipments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    TrackingNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecipientName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    RecipientPhone = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RecipientAddress = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    RecipientCity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RecipientPostalCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    RecipientCountry = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false, defaultValue: "US"),
                    SpecialInstructions = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    DeclaredValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedShipments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedShipmentScans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    TrackedShipmentId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    ScanType = table.Column<int>(type: "int", nullable: false),
                    CourierId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    ScannedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "float(9)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<double>(type: "float(9)", precision: 9, scale: 6, nullable: true),
                    RecipientSignature = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedShipmentScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedShipmentScans_TrackedShipments_TrackedShipmentId",
                        column: x => x.TrackedShipmentId,
                        principalTable: "TrackedShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipments_CreatedAt",
                table: "TrackedShipments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipments_Status",
                table: "TrackedShipments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipments_TrackingNumber",
                table: "TrackedShipments",
                column: "TrackingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipmentScans_CourierId",
                table: "TrackedShipmentScans",
                column: "CourierId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipmentScans_ScannedAt",
                table: "TrackedShipmentScans",
                column: "ScannedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipmentScans_ScanType",
                table: "TrackedShipmentScans",
                column: "ScanType");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipmentScans_ShipmentId",
                table: "TrackedShipmentScans",
                column: "TrackedShipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackedShipmentScans");

            migrationBuilder.DropTable(
                name: "TrackedShipments");
        }
    }
}
