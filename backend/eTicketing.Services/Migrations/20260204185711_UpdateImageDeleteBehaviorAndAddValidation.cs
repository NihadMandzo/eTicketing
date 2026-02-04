using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Services.Migrations
{
    /// <inheritdoc />
    public partial class UpdateImageDeleteBehaviorAndAddValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Categories_CategoryId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Events_EventId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Organizations_OrganizationId",
                table: "Images");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Categories_CategoryId",
                table: "Images",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Events_EventId",
                table: "Images",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Organizations_OrganizationId",
                table: "Images",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Categories_CategoryId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Events_EventId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Organizations_OrganizationId",
                table: "Images");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Categories_CategoryId",
                table: "Images",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Events_EventId",
                table: "Images",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Organizations_OrganizationId",
                table: "Images",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
