using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddGoNoGoThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoNoGoThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WindYellowKt = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    WindRedKt = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    VisibilityYellowKm = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    VisibilityRedKm = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    CapeYellow = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    CapeRed = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoNoGoThresholds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoNoGoThresholds_OwnerId",
                table: "GoNoGoThresholds",
                column: "OwnerId",
                unique: true,
                filter: "[OwnerId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoNoGoThresholds");
        }
    }
}
