using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Ticketing.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropTicketPdfBlobName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfBlobName",
                table: "Tickets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PdfBlobName",
                table: "Tickets",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
