using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Ticketing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGateDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ValidatedByDeviceId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GateDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AllSectors = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GateDeviceSectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GateDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateDeviceSectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateDeviceSectors_GateDevices_GateDeviceId",
                        column: x => x.GateDeviceId,
                        principalTable: "GateDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GateDeviceSectors_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GateDevices_KeyHash",
                table: "GateDevices",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GateDevices_OrganizationId",
                table: "GateDevices",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_GateDevices_ProductId",
                table: "GateDevices",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_GateDeviceSectors_GateDeviceId_SectorId",
                table: "GateDeviceSectors",
                columns: new[] { "GateDeviceId", "SectorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GateDeviceSectors_SectorId",
                table: "GateDeviceSectors",
                column: "SectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GateDeviceSectors");

            migrationBuilder.DropTable(
                name: "GateDevices");

            migrationBuilder.DropColumn(
                name: "ValidatedByDeviceId",
                table: "Tickets");
        }
    }
}
