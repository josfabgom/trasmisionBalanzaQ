using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BalanzaQ.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBarcodeFormatToPlu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Balanzas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncStatus = table.Column<string>(type: "TEXT", nullable: false),
                    LastSyncDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balanzas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PluItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PluCode = table.Column<int>(type: "INTEGER", nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Group = table.Column<int>(type: "INTEGER", nullable: false),
                    Section = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    LabelFormat = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", nullable: false),
                    RawType = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncStatus = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncError = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ShelfLife = table.Column<int>(type: "INTEGER", nullable: false),
                    BarcodeFormat = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSyncronized = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluItems", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Balanzas");

            migrationBuilder.DropTable(
                name: "PluItems");
        }
    }
}
