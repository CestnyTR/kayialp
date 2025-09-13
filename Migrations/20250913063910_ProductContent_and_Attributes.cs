using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kayialp.Migrations
{
    /// <inheritdoc />
    public partial class ProductContent_and_Attributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductDetailsFeatureTranslations");

            migrationBuilder.DropTable(
                name: "ProductDetailsTranslations");

            migrationBuilder.DropTable(
                name: "ProductDetailsFeatures");

            migrationBuilder.DropTable(
                name: "ProductDetails");

            migrationBuilder.CreateTable(
                name: "ProductAttributeGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributeGroups_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductContents_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    KeyName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributes_ProductAttributeGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ProductAttributeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductContentBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductContentId = table.Column<int>(type: "int", nullable: false),
                    BlockType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductContentBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductContentBlocks_ProductContents_ProductContentId",
                        column: x => x.ProductContentId,
                        principalTable: "ProductContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributeTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttributeId = table.Column<int>(type: "int", nullable: false),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributeTranslations_Langs_LangCodeId",
                        column: x => x.LangCodeId,
                        principalTable: "Langs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductAttributeTranslations_ProductAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "ProductAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductContentBlockTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockId = table.Column<int>(type: "int", nullable: false),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Html = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductContentBlockTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductContentBlockTranslations_Langs_LangCodeId",
                        column: x => x.LangCodeId,
                        principalTable: "Langs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductContentBlockTranslations_ProductContentBlocks_BlockId",
                        column: x => x.BlockId,
                        principalTable: "ProductContentBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeGroups_ProductId",
                table: "ProductAttributeGroups",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributes_GroupId",
                table: "ProductAttributes",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeTranslations_AttributeId_LangCodeId",
                table: "ProductAttributeTranslations",
                columns: new[] { "AttributeId", "LangCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAttributeTranslations_LangCodeId",
                table: "ProductAttributeTranslations",
                column: "LangCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductContentBlocks_ProductContentId",
                table: "ProductContentBlocks",
                column: "ProductContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductContentBlockTranslations_BlockId_LangCodeId",
                table: "ProductContentBlockTranslations",
                columns: new[] { "BlockId", "LangCodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductContentBlockTranslations_LangCodeId",
                table: "ProductContentBlockTranslations",
                column: "LangCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductContents_ProductId",
                table: "ProductContents",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductAttributeTranslations");

            migrationBuilder.DropTable(
                name: "ProductContentBlockTranslations");

            migrationBuilder.DropTable(
                name: "ProductAttributes");

            migrationBuilder.DropTable(
                name: "ProductContentBlocks");

            migrationBuilder.DropTable(
                name: "ProductAttributeGroups");

            migrationBuilder.DropTable(
                name: "ProductContents");

            migrationBuilder.CreateTable(
                name: "ProductDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDetails_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductDetailsFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDetailsFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDetailsFeatures_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductDetailsTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    ProductDetailsId = table.Column<int>(type: "int", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDetailsTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDetailsTranslations_Langs_LangCodeId",
                        column: x => x.LangCodeId,
                        principalTable: "Langs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductDetailsTranslations_ProductDetails_ProductDetailsId",
                        column: x => x.ProductDetailsId,
                        principalTable: "ProductDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductDetailsFeatureTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LangCodeId = table.Column<int>(type: "int", nullable: false),
                    ProductDetailsFeatureId = table.Column<int>(type: "int", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDetailsFeatureTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDetailsFeatureTranslations_Langs_LangCodeId",
                        column: x => x.LangCodeId,
                        principalTable: "Langs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductDetailsFeatureTranslations_ProductDetailsFeatures_ProductDetailsFeatureId",
                        column: x => x.ProductDetailsFeatureId,
                        principalTable: "ProductDetailsFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetails_ProductId",
                table: "ProductDetails",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetailsFeatures_ProductId",
                table: "ProductDetailsFeatures",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetailsFeatureTranslations_LangCodeId",
                table: "ProductDetailsFeatureTranslations",
                column: "LangCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetailsFeatureTranslations_ProductDetailsFeatureId",
                table: "ProductDetailsFeatureTranslations",
                column: "ProductDetailsFeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetailsTranslations_LangCodeId",
                table: "ProductDetailsTranslations",
                column: "LangCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetailsTranslations_ProductDetailsId",
                table: "ProductDetailsTranslations",
                column: "ProductDetailsId");
        }
    }
}
