using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiapGames.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SearchVersion",
                table: "Games",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "SearchIndexOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchIndexOutbox_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexOutbox_CreatedAt",
                table: "SearchIndexOutbox",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexOutbox_GameId_Version",
                table: "SearchIndexOutbox",
                columns: new[] { "GameId", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchIndexOutbox");

            migrationBuilder.DropColumn(
                name: "SearchVersion",
                table: "Games");
        }
    }
}
