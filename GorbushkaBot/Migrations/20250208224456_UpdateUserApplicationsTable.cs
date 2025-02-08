using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GorbushkaBot.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserApplicationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChatId",
                table: "user_applications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "user_applications");
        }
    }
}
