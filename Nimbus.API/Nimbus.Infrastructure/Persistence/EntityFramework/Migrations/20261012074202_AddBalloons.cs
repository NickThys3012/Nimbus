using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddBalloons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Balloons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Registration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnvelopeVolumeM3 = table.Column<int>(type: "int", nullable: false),
                    MaxEnvelopeTemperatureC = table.Column<int>(type: "int", nullable: false),
                    EnvelopeWeightKg = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    BasketWeightKg = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    BurnerWeightKg = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    CylindersWeightKg = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balloons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Balloons_IsDeleted",
                table: "Balloons",
                column: "IsDeleted",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Balloons_OwnerId_Registration",
                table: "Balloons",
                columns: new[] { "OwnerId", "Registration" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Balloons");
        }
    }
}
