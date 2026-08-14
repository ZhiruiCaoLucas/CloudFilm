using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudFilm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudyReadStates",
                schema: "cloudfilm",
                columns: table => new
                {
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudyInstanceUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MarkedReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyReadStates", x => new { x.ActorUserId, x.StudyInstanceUid });
                    table.ForeignKey(
                        name: "FK_StudyReadStates_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "cloudfilm",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyReadStates",
                schema: "cloudfilm");
        }
    }
}
