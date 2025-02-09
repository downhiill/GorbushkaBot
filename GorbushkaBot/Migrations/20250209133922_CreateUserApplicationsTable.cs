using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GorbushkaBot.Migrations
{
    /// <inheritdoc />
    public partial class CreateUserApplicationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_accepts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FacePhoto = table.Column<string>(type: "text", nullable: false),
                    Fio = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    PassportNumber = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    PassportIssueDate = table.Column<string>(type: "text", nullable: false),
                    RegistrationAddress = table.Column<string>(type: "text", nullable: false),
                    PassportPhotos = table.Column<string>(type: "text", nullable: false),
                    PavilionNumber = table.Column<string>(type: "text", nullable: false),
                    RentalContract = table.Column<string>(type: "text", nullable: false),
                    PavilionPhotos = table.Column<string>(type: "text", nullable: false),
                    FolderUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FacePhoto = table.Column<string>(type: "text", nullable: false),
                    Fio = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    PassportNumber = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    PassportIssueDate = table.Column<string>(type: "text", nullable: false),
                    RegistrationAddress = table.Column<string>(type: "text", nullable: false),
                    PassportPhotos = table.Column<string>(type: "text", nullable: false),
                    PavilionNumber = table.Column<string>(type: "text", nullable: false),
                    RentalContract = table.Column<string>(type: "text", nullable: false),
                    PavilionPhotos = table.Column<string>(type: "text", nullable: false),
                    FolderUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_applications", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_accepts");

            migrationBuilder.DropTable(
                name: "user_applications");
        }
    }
}
