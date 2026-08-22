using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudFilm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessGrant : Migration
    {
        private static readonly string[] _accessGrantUniqueIndexColumns =
        [
            "StudyInstanceUid",
            "GranteeUserId",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessGrants",
                schema: "cloudfilm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudyInstanceUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GranteeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessGrants_AspNetUsers_GranteeUserId",
                        column: x => x.GranteeUserId,
                        principalSchema: "cloudfilm",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_GranteeUserId",
                schema: "cloudfilm",
                table: "AccessGrants",
                column: "GranteeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_StudyInstanceUid_GranteeUserId",
                schema: "cloudfilm",
                table: "AccessGrants",
                columns: _accessGrantUniqueIndexColumns,                unique: true,
                filter: "\"RevokedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessGrants",
                schema: "cloudfilm");
        }
    }
}
