using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class addCompanyInfoTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInfoTranslation_CompanyInfos_CompanyInfoId",
                table: "CompanyInfoTranslation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyInfoTranslation",
                table: "CompanyInfoTranslation");

            migrationBuilder.RenameTable(
                name: "CompanyInfoTranslation",
                newName: "CompanyInfoTranslations");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyInfoTranslation_CompanyInfoId",
                table: "CompanyInfoTranslations",
                newName: "IX_CompanyInfoTranslations_CompanyInfoId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyInfoTranslations",
                table: "CompanyInfoTranslations",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInfoTranslations_CompanyInfos_CompanyInfoId",
                table: "CompanyInfoTranslations",
                column: "CompanyInfoId",
                principalTable: "CompanyInfos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInfoTranslations_CompanyInfos_CompanyInfoId",
                table: "CompanyInfoTranslations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyInfoTranslations",
                table: "CompanyInfoTranslations");

            migrationBuilder.RenameTable(
                name: "CompanyInfoTranslations",
                newName: "CompanyInfoTranslation");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyInfoTranslations_CompanyInfoId",
                table: "CompanyInfoTranslation",
                newName: "IX_CompanyInfoTranslation_CompanyInfoId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyInfoTranslation",
                table: "CompanyInfoTranslation",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInfoTranslation_CompanyInfos_CompanyInfoId",
                table: "CompanyInfoTranslation",
                column: "CompanyInfoId",
                principalTable: "CompanyInfos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
