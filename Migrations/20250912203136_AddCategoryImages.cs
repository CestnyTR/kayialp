using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageAlts",
                table: "ProductDetailsTranslations");

            migrationBuilder.AddColumn<string>(
                name: "ImageCard312x240",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageShowcase423x636",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageCard312x240",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ImageShowcase423x636",
                table: "Categories");

            migrationBuilder.AddColumn<string>(
                name: "ImageAlts",
                table: "ProductDetailsTranslations",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
