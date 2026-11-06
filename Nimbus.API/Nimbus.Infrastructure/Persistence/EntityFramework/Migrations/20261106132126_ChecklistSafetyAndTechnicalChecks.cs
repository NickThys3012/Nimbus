using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistSafetyAndTechnicalChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChecklistCompletedCount",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "ChecklistItemsJson",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "ChecklistTotalCount",
                table: "FlightPreparations");

            migrationBuilder.AddColumn<bool>(
                name: "BalloonVisualInspectionDone",
                table: "FlightPreparations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BurnerTested",
                table: "FlightPreparations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FirstAidAndExtinguisher",
                table: "FlightPreparations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "No");

            migrationBuilder.AddColumn<string>(
                name: "FlightPlanFiled",
                table: "FlightPreparations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "No");

            migrationBuilder.AddColumn<bool>(
                name: "GasCylindersChecked",
                table: "FlightPreparations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InstrumentsChecked",
                table: "FlightPreparations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MooringsChecked",
                table: "FlightPreparations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PassengerListFilled",
                table: "FlightPreparations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "No");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalloonVisualInspectionDone",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "BurnerTested",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "FirstAidAndExtinguisher",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "FlightPlanFiled",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "GasCylindersChecked",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "InstrumentsChecked",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "MooringsChecked",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "PassengerListFilled",
                table: "FlightPreparations");

            migrationBuilder.AddColumn<int>(
                name: "ChecklistCompletedCount",
                table: "FlightPreparations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChecklistItemsJson",
                table: "FlightPreparations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChecklistTotalCount",
                table: "FlightPreparations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
