using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class Refactor_DetailTypes_And_FeatureBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductDetailsFeatures_ProductDetails_ProductDetailsId",
                table: "ProductDetailsFeatures");

            migrationBuilder.RenameColumn(
                name: "ProductDetailsId",
                table: "ProductDetailsFeatures",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductDetailsFeatures_ProductDetailsId",
                table: "ProductDetailsFeatures",
                newName: "IX_ProductDetailsFeatures_ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductDetailsFeatures_Products_ProductId",
                table: "ProductDetailsFeatures",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductDetailsFeatures_Products_ProductId",
                table: "ProductDetailsFeatures");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "ProductDetailsFeatures",
                newName: "ProductDetailsId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductDetailsFeatures_ProductId",
                table: "ProductDetailsFeatures",
                newName: "IX_ProductDetailsFeatures_ProductDetailsId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductDetailsFeatures_ProductDetails_ProductDetailsId",
                table: "ProductDetailsFeatures",
                column: "ProductDetailsId",
                principalTable: "ProductDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
