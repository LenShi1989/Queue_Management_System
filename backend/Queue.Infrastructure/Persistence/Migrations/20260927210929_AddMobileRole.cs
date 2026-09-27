using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Queue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_roles",
                columns: new[] { "id", "code", "description", "name" },
                values: new object[] { 6L, "Mobile", "以 QR Token 查詢自己的票據進度", "手機查詢使用者" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_roles",
                keyColumn: "id",
                keyValue: 6L);
        }
    }
}
