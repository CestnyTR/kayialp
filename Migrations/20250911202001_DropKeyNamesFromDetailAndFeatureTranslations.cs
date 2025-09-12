using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class DropKeyNamesFromDetailAndFeatureTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyName",
                table: "ProductDetailsTranslations");

            migrationBuilder.DropColumn(
                name: "KeyName",
                table: "ProductDetailsFeatureTranslations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeyName",
                table: "ProductDetailsTranslations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KeyName",
                table: "ProductDetailsFeatureTranslations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
