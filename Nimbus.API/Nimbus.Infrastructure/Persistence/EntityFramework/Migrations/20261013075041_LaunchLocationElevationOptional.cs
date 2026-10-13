using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class LaunchLocationElevationOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ElevationM",
                table: "LaunchLocations",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ElevationM",
                table: "LaunchLocations",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(7,2)",
                oldPrecision: 7,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
