using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.CheckConstraint("CK_MenuItems_Price", "[Price] > 0");
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "MenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemDetails",
                columns: table => new
                {
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Ingredients = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WeightGrams = table.Column<int>(type: "int", nullable: true),
                    Calories = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemDetails", x => x.MenuItemId);
                    table.CheckConstraint("CK_MenuItemDetails_Calories", "[Calories] IS NULL OR [Calories] >= 0");
                    table.CheckConstraint("CK_MenuItemDetails_Weight", "[WeightGrams] IS NULL OR [WeightGrams] > 0");
                    table.ForeignKey(
                        name: "FK_MenuItemDetails_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemImages_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemTags",
                columns: table => new
                {
                    MenuItemId = table.Column<long>(type: "bigint", nullable: false),
                    TagId = table.Column<long>(type: "bigint", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemTags", x => new { x.MenuItemId, x.TagId });
                    table.ForeignKey(
                        name: "FK_MenuItemTags_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MenuCategories",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Піци", 1, null, null },
                    { 2L, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Салати", 2, null, null },
                    { 3L, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Десерти", 3, null, null },
                    { 4L, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Напої", 4, null, null }
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "CreatedAt", "Name" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Вегетаріанське" },
                    { 2L, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Хіт" },
                    { 3L, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Гостре" }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_MenuCategories_Name",
                table: "MenuCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemImages_MenuItemId_SortOrder",
                table: "MenuItemImages",
                columns: new[] { "MenuItemId", "SortOrder" })
                .Annotation("SqlServer:Include", new[] { "Url", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "UX_MenuItemImages_OnePrimary",
                table: "MenuItemImages",
                column: "MenuItemId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CategoryId_Name",
                table: "MenuItems",
                columns: new[] { "CategoryId", "Name" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Price", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "UX_MenuItems_Name",
                table: "MenuItems",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemTags_TagId_MenuItemId",
                table: "MenuItemTags",
                columns: new[] { "TagId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemDetails");

            migrationBuilder.DropTable(
                name: "MenuItemImages");

            migrationBuilder.DropTable(
                name: "MenuItemTags");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "MenuCategories");
        }
    }
}
