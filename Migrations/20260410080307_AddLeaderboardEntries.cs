using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoggingWayMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaderboardEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CfcId = table.Column<int>(type: "INTEGER", nullable: false),
                    JobId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: false),
                    Character = table.Column<Guid>(type: "TEXT", nullable: true),
                    BestDps = table.Column<double>(type: "REAL", nullable: false),
                    BestDpsEncounterId = table.Column<long>(type: "INTEGER", nullable: false),
                    BestHps = table.Column<double>(type: "REAL", nullable: false),
                    BestHpsEncounterId = table.Column<long>(type: "INTEGER", nullable: false),
                    BestPScore = table.Column<double>(type: "REAL", nullable: false),
                    BestPScoreEncounterId = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalKills = table.Column<int>(type: "INTEGER", nullable: false),
                    MedianDps = table.Column<double>(type: "REAL", nullable: false),
                    DpsRank = table.Column<int>(type: "INTEGER", nullable: false),
                    HpsRank = table.Column<int>(type: "INTEGER", nullable: false),
                    PScoreRank = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CharacterClaimId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardEntries_characters_claim_CharacterClaimId",
                        column: x => x.CharacterClaimId,
                        principalTable: "characters_claim",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_LeaderboardEntries_encounters_BestDpsEncounterId",
                        column: x => x.BestDpsEncounterId,
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaderboardEntries_encounters_BestHpsEncounterId",
                        column: x => x.BestHpsEncounterId,
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaderboardEntries_encounters_BestPScoreEncounterId",
                        column: x => x.BestPScoreEncounterId,
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_BestDpsEncounterId",
                table: "LeaderboardEntries",
                column: "BestDpsEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_BestHpsEncounterId",
                table: "LeaderboardEntries",
                column: "BestHpsEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_BestPScoreEncounterId",
                table: "LeaderboardEntries",
                column: "BestPScoreEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_CharacterClaimId",
                table: "LeaderboardEntries",
                column: "CharacterClaimId");

            migrationBuilder.CreateIndex(
                name: "Leaderboard_CfcJob_DpsRank",
                table: "LeaderboardEntries",
                columns: new[] { "CfcId", "JobId", "DpsRank" });

            migrationBuilder.CreateIndex(
                name: "Leaderboard_Player_Job_Cfc",
                table: "LeaderboardEntries",
                columns: new[] { "PlayerId", "JobId", "CfcId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardEntries");
        }
    }
}
