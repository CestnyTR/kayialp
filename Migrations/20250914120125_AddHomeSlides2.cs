using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeSlides2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SliderTranslations");

            migrationBuilder.DropTable(
                name: "Sliders");

            migrationBuilder.CreateTable(
                name: "HomeSlides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Cover1920x900 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverMobile768x1024 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeSlides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeSlideTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeSlideId = table.Column<int>(type: "int", nullable: false),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    Slogan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cta1Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cta1Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cta2Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cta2Url = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeSlideTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeSlideTranslations_HomeSlides_HomeSlideId",
                        column: x => x.HomeSlideId,
                        principalTable: "HomeSlides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeSlideTranslations_HomeSlideId",
                table: "HomeSlideTranslations",
                column: "HomeSlideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeSlideTranslations");

            migrationBuilder.DropTable(
                name: "HomeSlides");

            migrationBuilder.CreateTable(
                name: "Sliders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sliders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SliderTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    SliderId = table.Column<int>(type: "int", nullable: false),
                    ShortContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slogan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SliderTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SliderTranslations_Langs_LangCodeId",
                        column: x => x.LangCodeId,
                        principalTable: "Langs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SliderTranslations_Sliders_SliderId",
                        column: x => x.SliderId,
                        principalTable: "Sliders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SliderTranslations_LangCodeId",
                table: "SliderTranslations",
                column: "LangCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SliderTranslations_SliderId",
                table: "SliderTranslations",
                column: "SliderId");
        }
    }
}
