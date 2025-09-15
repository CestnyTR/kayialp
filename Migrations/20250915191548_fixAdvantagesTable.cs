using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class fixAdvantagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdvantageTranslations_Langs_LangId",
                table: "AdvantageTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AdvantageTranslations_AdvantageId",
                table: "AdvantageTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AdvantageTranslations_LangId",
                table: "AdvantageTranslations");

            migrationBuilder.DropColumn(
                name: "LangId",
                table: "AdvantageTranslations");

            migrationBuilder.CreateIndex(
                name: "IX_AdvantageTranslations_AdvantageId_LangCodeId",
                table: "AdvantageTranslations",
                columns: new[] { "AdvantageId", "LangCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvantageTranslations_LangCodeId",
                table: "AdvantageTranslations",
                column: "LangCodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdvantageTranslations_Langs_LangCodeId",
                table: "AdvantageTranslations",
                column: "LangCodeId",
                principalTable: "Langs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdvantageTranslations_Langs_LangCodeId",
                table: "AdvantageTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AdvantageTranslations_AdvantageId_LangCodeId",
                table: "AdvantageTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AdvantageTranslations_LangCodeId",
                table: "AdvantageTranslations");

            migrationBuilder.AddColumn<int>(
                name: "LangId",
                table: "AdvantageTranslations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AdvantageTranslations_AdvantageId",
                table: "AdvantageTranslations",
                column: "AdvantageId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvantageTranslations_LangId",
                table: "AdvantageTranslations",
                column: "LangId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdvantageTranslations_Langs_LangId",
                table: "AdvantageTranslations",
                column: "LangId",
                principalTable: "Langs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
