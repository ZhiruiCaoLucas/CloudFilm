using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudFilm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShareLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShareLinks",
                schema: "cloudfilm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudyInstanceUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Permission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_StudyInstanceUid",
                schema: "cloudfilm",
                table: "ShareLinks",
                column: "StudyInstanceUid");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_TokenHash",
                schema: "cloudfilm",
                table: "ShareLinks",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShareLinks",
                schema: "cloudfilm");
        }
    }
}
