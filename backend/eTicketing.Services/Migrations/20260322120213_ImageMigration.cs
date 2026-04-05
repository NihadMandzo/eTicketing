using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eTicketing.Services.Migrations
{
    /// <inheritdoc />
    public partial class ImageMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<int>(
                name: "ImageId",
                table: "Organizations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageId",
                table: "EventImages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageId",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                });

            // Migrate data from columns to Images table
            migrationBuilder.Sql(@"
                INSERT INTO Images (ImageUrl, CreatedAt) 
                SELECT DISTINCT Url, GETUTCDATE() FROM (
                    SELECT LogoUrl AS Url FROM Organizations WHERE LogoUrl IS NOT NULL AND LogoUrl != ''
                    UNION
                    SELECT ImageUrl AS Url FROM EventImages WHERE ImageUrl IS NOT NULL AND ImageUrl != ''
                    UNION
                    SELECT IconUrl AS Url FROM Categories WHERE IconUrl IS NOT NULL AND IconUrl != ''
                ) t;

                UPDATE o SET o.ImageId = i.Id FROM Organizations o INNER JOIN Images i ON o.LogoUrl = i.ImageUrl;
                UPDATE e SET e.ImageId = i.Id FROM EventImages e INNER JOIN Images i ON e.ImageUrl = i.ImageUrl;
                UPDATE c SET c.ImageId = i.Id FROM Categories c INNER JOIN Images i ON c.IconUrl = i.ImageUrl;
            ");


            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "ImageId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "ImageId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "ImageId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5,
                column: "ImageId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "ImageId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ImageId",
                table: "Organizations",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_EventImages_ImageId",
                table: "EventImages",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ImageId",
                table: "Categories",
                column: "ImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Images_ImageId",
                table: "Categories",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EventImages_Images_ImageId",
                table: "EventImages",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Organizations_Images_ImageId",
                table: "Organizations",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "EventImages");

            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Images_ImageId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_EventImages_Images_ImageId",
                table: "EventImages");

            migrationBuilder.DropForeignKey(
                name: "FK_Organizations_Images_ImageId",
                table: "Organizations");

            // Restore data from Images table back to URL columns
            migrationBuilder.Sql(@"
                UPDATE o SET o.LogoUrl = i.ImageUrl FROM Organizations o INNER JOIN Images i ON o.ImageId = i.Id;
                UPDATE e SET e.ImageUrl = i.ImageUrl FROM EventImages e INNER JOIN Images i ON e.ImageId = i.Id;
                UPDATE c SET c.IconUrl = i.ImageUrl FROM Categories c INNER JOIN Images i ON c.ImageId = i.Id;
            ");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_ImageId",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_EventImages_ImageId",
                table: "EventImages");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ImageId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "EventImages");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "Categories");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Organizations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "EventImages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "IconUrl",
                value: "/icons/music.svg");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "IconUrl",
                value: "/icons/sports.svg");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "IconUrl",
                value: "/icons/theater.svg");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "IconUrl",
                value: "/icons/conference.svg");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5,
                column: "IconUrl",
                value: "/icons/arts.svg");

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "LogoUrl",
                value: "/logos/default.png");

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "LogoUrl",
                value: "/logos/demo.png");
        }
    }
}
