using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoggingWayMaster.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    banned = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "characters_claim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    xivauthkey = table.Column<string>(type: "TEXT", nullable: false),
                    claim_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    charname = table.Column<string>(type: "TEXT", nullable: false),
                    datacenter = table.Column<string>(type: "TEXT", nullable: false),
                    homeworld = table.Column<string>(type: "TEXT", nullable: false),
                    lodestone_id = table.Column<int>(type: "INTEGER", nullable: false),
                    avatar_url = table.Column<string>(type: "TEXT", nullable: true),
                    portrait_url = table.Column<string>(type: "TEXT", nullable: true),
                    claim_registered = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_claim", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_claim_users_claim_by",
                        column: x => x.claim_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "encounters",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    cfc_id = table.Column<int>(type: "INTEGER", nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    payload = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounters", x => x.id);
                    table.ForeignKey(
                        name: "FK_encounters_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "encounter_player_stats",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    encounter_id = table.Column<long>(type: "INTEGER", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    character = table.Column<Guid>(type: "TEXT", nullable: true),
                    player_id = table.Column<long>(type: "INTEGER", nullable: false),
                    player_name = table.Column<string>(type: "TEXT", nullable: false),
                    job_id = table.Column<int>(type: "INTEGER", nullable: false),
                    total_damage = table.Column<long>(type: "INTEGER", nullable: false),
                    total_pscore = table.Column<double>(type: "REAL", nullable: false),
                    total_healing = table.Column<long>(type: "INTEGER", nullable: false),
                    total_hits = table.Column<long>(type: "INTEGER", nullable: false),
                    total_crits = table.Column<long>(type: "INTEGER", nullable: false),
                    total_direct_hits = table.Column<long>(type: "INTEGER", nullable: false),
                    first_timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    last_timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    duration_seconds = table.Column<double>(type: "REAL", nullable: false),
                    dps = table.Column<double>(type: "REAL", nullable: false),
                    hps = table.Column<double>(type: "REAL", nullable: false),
                    crit_rate = table.Column<double>(type: "REAL", nullable: false),
                    direct_hit_rate = table.Column<double>(type: "REAL", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter_player_stats", x => x.id);
                    table.ForeignKey(
                        name: "FK_encounter_player_stats_characters_claim_character",
                        column: x => x.character,
                        principalTable: "characters_claim",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_encounter_player_stats_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_encounter_player_stats_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_characters_claim_claim_by",
                table: "characters_claim",
                column: "claim_by");

            migrationBuilder.CreateIndex(
                name: "IX_characters_claim_xivauthkey_charname_datacenter_homeworld",
                table: "characters_claim",
                columns: new[] { "xivauthkey", "charname", "datacenter", "homeworld" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_encounter_player_stats_character",
                table: "encounter_player_stats",
                column: "character");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_player_stats_encounter_id_player_id",
                table: "encounter_player_stats",
                columns: new[] { "encounter_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_encounter_player_stats_uploaded_by",
                table: "encounter_player_stats",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_encounters_uploaded_by",
                table: "encounters",
                column: "uploaded_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encounter_player_stats");

            migrationBuilder.DropTable(
                name: "characters_claim");

            migrationBuilder.DropTable(
                name: "encounters");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
