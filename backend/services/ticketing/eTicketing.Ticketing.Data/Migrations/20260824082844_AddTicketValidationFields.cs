using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Ticketing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketValidationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidatedByUserId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ValidatedByUserId",
                table: "Tickets");
        }
    }
}
