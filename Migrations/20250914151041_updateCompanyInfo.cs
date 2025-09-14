using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class updateCompanyInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AboutHtml",
                table: "CompanyInfos");

            migrationBuilder.DropColumn(
                name: "MissionHtml",
                table: "CompanyInfos");

            migrationBuilder.DropColumn(
                name: "VisionHtml",
                table: "CompanyInfos");

            migrationBuilder.AlterColumn<string>(
                name: "LogoUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HeroUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YoutubeUrl",
                table: "CompanyInfos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyInfoTranslation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyInfoId = table.Column<int>(type: "int", nullable: false),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    AboutHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissionHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VisionHtml = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInfoTranslation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyInfoTranslation_CompanyInfos_CompanyInfoId",
                        column: x => x.CompanyInfoId,
                        principalTable: "CompanyInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInfoTranslation_CompanyInfoId",
                table: "CompanyInfoTranslation",
                column: "CompanyInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyInfoTranslation");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "CompanyInfos");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "CompanyInfos");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "CompanyInfos");

            migrationBuilder.DropColumn(
                name: "TwitterUrl",
                table: "CompanyInfos");

            migrationBuilder.DropColumn(
                name: "YoutubeUrl",
                table: "CompanyInfos");

            migrationBuilder.AlterColumn<string>(
                name: "LogoUrl",
                table: "CompanyInfos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HeroUrl",
                table: "CompanyInfos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AboutHtml",
                table: "CompanyInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MissionHtml",
                table: "CompanyInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionHtml",
                table: "CompanyInfos",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
