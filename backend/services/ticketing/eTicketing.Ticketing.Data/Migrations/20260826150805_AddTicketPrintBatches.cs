using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Ticketing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPrintBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserEmail",
                table: "Tickets",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PrintBatchId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SerialNumber",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketPrintBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TicketCount = table.Column<int>(type: "int", nullable: false),
                    RenderedCount = table.Column<int>(type: "int", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    SerialFrom = table.Column<int>(type: "int", nullable: false),
                    SerialTo = table.Column<int>(type: "int", nullable: false),
                    NominalValue = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ValidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DownloadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPrintBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketPrintBatchFiles",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPrintBatchFiles", x => x.BatchId);
                    table.ForeignKey(
                        name: "FK_TicketPrintBatchFiles_TicketPrintBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "TicketPrintBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PrintBatchId",
                table: "Tickets",
                column: "PrintBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ProductId_SerialNumber",
                table: "Tickets",
                columns: new[] { "ProductId", "SerialNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketPrintBatches_OrganizationId",
                table: "TicketPrintBatches",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPrintBatches_ProductId",
                table: "TicketPrintBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPrintBatches_Status",
                table: "TicketPrintBatches",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketPrintBatches_PrintBatchId",
                table: "Tickets",
                column: "PrintBatchId",
                principalTable: "TicketPrintBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketPrintBatches_PrintBatchId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketPrintBatchFiles");

            migrationBuilder.DropTable(
                name: "TicketPrintBatches");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_PrintBatchId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ProductId_SerialNumber",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PrintBatchId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Tickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserEmail",
                table: "Tickets",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(320)",
                oldMaxLength: 320,
                oldNullable: true);
        }
    }
}
