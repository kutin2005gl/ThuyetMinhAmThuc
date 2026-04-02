using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pois",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    RadiusMeters = table.Column<double>(type: "REAL", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pois", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioFiles_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Translations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoiId = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Translations_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Pois",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Latitude", "Longitude", "Name", "RadiusMeters" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Phở bò truyền thống từ 1970", true, 10.776899999999999, 106.7009, "Quán Phở Bà Dậu", 30.0 },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Bánh mì đặc sản nổi tiếng nhất phố", true, 10.7775, 106.7015, "Bánh Mì Hùng", 30.0 },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Chè truyền thống Nam Bộ", true, 10.778, 106.702, "Chè Bà Ba", 30.0 }
                });

            migrationBuilder.InsertData(
                table: "Translations",
                columns: new[] { "Id", "Language", "PoiId", "Text" },
                values: new object[,]
                {
                    { 1, "vi", 1, "Chào mừng đến với Quán Phở Bà Dậu. Quán được thành lập từ năm 1970 với công thức phở bò truyền thống." },
                    { 2, "en", 1, "Welcome to Pho Ba Dau restaurant, established in 1970 with traditional beef pho recipe." },
                    { 3, "vi", 2, "Bánh Mì Hùng là địa điểm nổi tiếng nhất phố với hơn 30 năm kinh nghiệm làm bánh mì đặc sản." },
                    { 4, "en", 2, "Banh Mi Hung is the most famous spot on the street with over 30 years of experience." },
                    { 5, "vi", 3, "Chè Bà Ba phục vụ các loại chè truyền thống Nam Bộ được nấu theo công thức gia truyền." },
                    { 6, "en", 3, "Che Ba Ba serves traditional Southern Vietnamese sweet desserts cooked with family recipes." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioFiles_PoiId",
                table: "AudioFiles",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_Translations_PoiId",
                table: "Translations",
                column: "PoiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioFiles");

            migrationBuilder.DropTable(
                name: "Translations");

            migrationBuilder.DropTable(
                name: "Pois");
        }
    }
}
