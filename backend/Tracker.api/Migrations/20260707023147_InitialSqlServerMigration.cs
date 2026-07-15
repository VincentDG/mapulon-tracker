using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServerMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscordUsername = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiotName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiotTag = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiotPuuid = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentTier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentRank = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentLP = table.Column<int>(type: "int", nullable: false),
                    HasHitMasterThisSet = table.Column<bool>(type: "bit", nullable: false),
                    TimestampHitMaster = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
