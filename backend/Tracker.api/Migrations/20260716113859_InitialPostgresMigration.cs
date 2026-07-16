using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tracker.api.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscordId = table.Column<string>(type: "text", nullable: false),
                    DiscordUsername = table.Column<string>(type: "text", nullable: false),
                    RiotName = table.Column<string>(type: "text", nullable: false),
                    RiotTag = table.Column<string>(type: "text", nullable: false),
                    RiotPuuid = table.Column<string>(type: "text", nullable: false),
                    CurrentTier = table.Column<string>(type: "text", nullable: false),
                    CurrentRank = table.Column<string>(type: "text", nullable: false),
                    CurrentLP = table.Column<int>(type: "integer", nullable: false),
                    HasHitMasterThisSet = table.Column<bool>(type: "boolean", nullable: false),
                    TimestampHitMaster = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
