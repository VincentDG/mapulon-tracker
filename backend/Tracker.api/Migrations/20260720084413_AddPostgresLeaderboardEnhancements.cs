using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tracker.api.Migrations
{
    /// <inheritdoc />
    public partial class AddPostgresLeaderboardEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<string>(type: "text", nullable: false),
                    PlayerPuuid = table.Column<string>(type: "text", nullable: false),
                    Placement = table.Column<int>(type: "integer", nullable: false),
                    MatchEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscordId = table.Column<string>(type: "text", nullable: false),
                    DiscordUsername = table.Column<string>(type: "text", nullable: false),
                    DiscordAvatarHash = table.Column<string>(type: "text", nullable: false),
                    DiscordGlobalName = table.Column<string>(type: "text", nullable: false),
                    RiotName = table.Column<string>(type: "text", nullable: false),
                    RiotTag = table.Column<string>(type: "text", nullable: false),
                    RiotPuuid = table.Column<string>(type: "text", nullable: false),
                    ProfileIconId = table.Column<int>(type: "integer", nullable: false),
                    CurrentTier = table.Column<string>(type: "text", nullable: false),
                    CurrentRank = table.Column<string>(type: "text", nullable: false),
                    CurrentLP = table.Column<int>(type: "integer", nullable: false),
                    HasHitMasterThisSet = table.Column<bool>(type: "boolean", nullable: false),
                    TimestampHitMaster = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsOptedIn = table.Column<bool>(type: "boolean", nullable: false),
                    GamesPlayed = table.Column<int>(type: "integer", nullable: false),
                    AVP = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_MatchId",
                table: "Matches",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlayerPuuid",
                table: "Matches",
                column: "PlayerPuuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
