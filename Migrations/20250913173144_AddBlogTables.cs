using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BlogPostContentBlockTranslation",
                table: "BlogPostContentBlockTranslation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BlogPostContentBlock",
                table: "BlogPostContentBlock");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BlogPostContent",
                table: "BlogPostContent");

            migrationBuilder.RenameTable(
                name: "BlogPostContentBlockTranslation",
                newName: "BlogPostContentBlockTranslations");

            migrationBuilder.RenameTable(
                name: "BlogPostContentBlock",
                newName: "BlogPostContentBlocks");

            migrationBuilder.RenameTable(
                name: "BlogPostContent",
                newName: "BlogPostContents");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "BlogPostsTranslations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "BlogCategoriesTranslations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BlogPostContentBlockTranslations",
                table: "BlogPostContentBlockTranslations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BlogPostContentBlocks",
                table: "BlogPostContentBlocks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BlogPostContents",
                table: "BlogPostContents",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPostsTranslations_LangCodeId_Slug",
                table: "BlogPostsTranslations",
                columns: new[] { "LangCodeId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogCategoriesTranslations_LangCodeId_Slug",
                table: "BlogCategoriesTranslations",
                columns: new[] { "LangCodeId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPostsTranslations_LangCodeId_Slug",
                table: "BlogPostsTranslations");

            migrationBuilder.DropIndex(
                name: "IX_BlogCategoriesTranslations_LangCodeId_Slug",
                table: "BlogCategoriesTranslations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BlogPostContents",
                table: "BlogPostContents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BlogPostContentBlockTranslations",
                table: "BlogPostContentBlockTranslations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BlogPostContentBlocks",
                table: "BlogPostContentBlocks");

            migrationBuilder.RenameTable(
                name: "BlogPostContents",
                newName: "BlogPostContent");

            migrationBuilder.RenameTable(
                name: "BlogPostContentBlockTranslations",
                newName: "BlogPostContentBlockTranslation");

            migrationBuilder.RenameTable(
                name: "BlogPostContentBlocks",
                newName: "BlogPostContentBlock");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "BlogPostsTranslations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "BlogCategoriesTranslations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BlogPostContent",
                table: "BlogPostContent",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BlogPostContentBlockTranslation",
                table: "BlogPostContentBlockTranslation",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BlogPostContentBlock",
                table: "BlogPostContentBlock",
                column: "Id");
        }
    }
}
