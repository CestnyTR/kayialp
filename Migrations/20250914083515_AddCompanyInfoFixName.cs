using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInfoFixName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyInfo",
                table: "CompanyInfo");

            migrationBuilder.RenameTable(
                name: "CompanyInfo",
                newName: "CompanyInfos");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyInfos",
                table: "CompanyInfos",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyInfos",
                table: "CompanyInfos");

            migrationBuilder.RenameTable(
                name: "CompanyInfos",
                newName: "CompanyInfo");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyInfo",
                table: "CompanyInfo",
                column: "Id");
        }
    }
}
