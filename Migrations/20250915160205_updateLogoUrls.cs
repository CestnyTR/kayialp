using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class updateLogoUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HeroUrl",
                table: "CompanyInfos",
                newName: "MobilLogoUrl");

            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "CompanyInfos");

            migrationBuilder.RenameColumn(
                name: "MobilLogoUrl",
                table: "CompanyInfos",
                newName: "HeroUrl");
        }
    }
}
