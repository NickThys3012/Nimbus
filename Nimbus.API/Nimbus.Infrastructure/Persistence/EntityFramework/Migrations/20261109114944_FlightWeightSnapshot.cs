using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FlightWeightSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasketWeightKg",
                table: "FlightPreparations",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BurnerWeightKg",
                table: "FlightPreparations",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CylindersWeightKg",
                table: "FlightPreparations",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasketWeightKg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "BurnerWeightKg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "CylindersWeightKg",
                table: "FlightPreparations");
        }
    }
}
