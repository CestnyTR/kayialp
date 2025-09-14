using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalPageTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LegalPageId = table.Column<int>(type: "int", nullable: false),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Html = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalPageTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalPageTranslations_LegalPages_LegalPageId",
                        column: x => x.LegalPageId,
                        principalTable: "LegalPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalPageTranslations_LegalPageId",
                table: "LegalPageTranslations",
                column: "LegalPageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalPageTranslations");

            migrationBuilder.DropTable(
                name: "LegalPages");
        }
    }
}
