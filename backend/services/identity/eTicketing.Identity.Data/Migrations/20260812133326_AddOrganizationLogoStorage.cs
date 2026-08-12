using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Identity.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationLogoStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LogoUrl",
                table: "Organizations",
                newName: "LogoContentType");

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoData",
                table: "Organizations",
                type: "varbinary(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoData",
                table: "Organizations");

            migrationBuilder.RenameColumn(
                name: "LogoContentType",
                table: "Organizations",
                newName: "LogoUrl");
        }
    }
}
