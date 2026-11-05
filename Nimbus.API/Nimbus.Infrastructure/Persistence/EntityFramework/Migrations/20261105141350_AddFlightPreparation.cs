using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlightPreparations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BalloonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PilotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LaunchLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FlightDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FlightTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    SunriseTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    SunsetTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    FieldOwnerNotified = table.Column<bool>(type: "bit", nullable: false),
                    WindSpeedKt = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    WindDirectionDeg = table.Column<int>(type: "int", nullable: true),
                    VisibilityKm = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    CapeJPerKg = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    TemperatureC = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DewPointC = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    QnhHpa = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Precipitation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Metar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Taf = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NotamsChecked = table.Column<bool>(type: "bit", nullable: false),
                    AirspaceStructureNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AirspaceRestrictions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Obstacles = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChecklistCompletedCount = table.Column<int>(type: "int", nullable: false),
                    ChecklistTotalCount = table.Column<int>(type: "int", nullable: false),
                    ChecklistItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnvelopeWeightKg = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    MaxAltitudeFt = table.Column<int>(type: "int", nullable: true),
                    LoadRemarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RouteDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClimbRateMetersPerSecond = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    DescentRateMetersPerSecond = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    SimulationDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    SimulationStepSeconds = table.Column<int>(type: "int", nullable: true),
                    CruiseAltitudesFtCsv = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BulletinText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OfpOperatorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OfpPicWeightKg = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    OfpFuelAvailableMinutes = table.Column<int>(type: "int", nullable: true),
                    OfpFuelRequiredMinutes = table.Column<int>(type: "int", nullable: true),
                    OfpLandingLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OfpPlannedLandingTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OfpVisibleDefectsChecked = table.Column<bool>(type: "bit", nullable: false),
                    PaxBriefingText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaxBriefingVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaxBriefingIsCustom = table.Column<bool>(type: "bit", nullable: false),
                    FlownAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    ActualLandingLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FuelConsumedLiters = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    HasVisibleDefects = table.Column<bool>(type: "bit", nullable: true),
                    DefectsDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OutcomeRemarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KmlTrackObjectKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    KmlTrackContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightPreparations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlightPreparationImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlightPreparationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightPreparationImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightPreparationImages_FlightPreparations_FlightPreparationId",
                        column: x => x.FlightPreparationId,
                        principalTable: "FlightPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlightPreparationShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlightPreparationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightPreparationShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightPreparationShares_FlightPreparations_FlightPreparationId",
                        column: x => x.FlightPreparationId,
                        principalTable: "FlightPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Passengers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlightPreparationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    NeedsAssistance = table.Column<bool>(type: "bit", nullable: false),
                    IsChild = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Passengers_FlightPreparations_FlightPreparationId",
                        column: x => x.FlightPreparationId,
                        principalTable: "FlightPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WindLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlightPreparationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AltitudeM = table.Column<int>(type: "int", nullable: false),
                    DirectionDeg = table.Column<int>(type: "int", nullable: false),
                    SpeedKt = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
                    TemperatureC = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindLevels_FlightPreparations_FlightPreparationId",
                        column: x => x.FlightPreparationId,
                        principalTable: "FlightPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparationImages_FlightPreparationId_Section_Order",
                table: "FlightPreparationImages",
                columns: new[] { "FlightPreparationId", "Section", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparationImages_IsDeleted",
                table: "FlightPreparationImages",
                column: "IsDeleted",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparations_IsDeleted",
                table: "FlightPreparations",
                column: "IsDeleted",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparations_OwnerId",
                table: "FlightPreparations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparationShares_FlightPreparationId_SharedWithUserId",
                table: "FlightPreparationShares",
                columns: new[] { "FlightPreparationId", "SharedWithUserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparationShares_IsDeleted",
                table: "FlightPreparationShares",
                column: "IsDeleted",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Passengers_FlightPreparationId_Order",
                table: "Passengers",
                columns: new[] { "FlightPreparationId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Passengers_IsDeleted",
                table: "Passengers",
                column: "IsDeleted",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WindLevels_FlightPreparationId_Order",
                table: "WindLevels",
                columns: new[] { "FlightPreparationId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WindLevels_IsDeleted",
                table: "WindLevels",
                column: "IsDeleted",
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightPreparationImages");

            migrationBuilder.DropTable(
                name: "FlightPreparationShares");

            migrationBuilder.DropTable(
                name: "Passengers");

            migrationBuilder.DropTable(
                name: "WindLevels");

            migrationBuilder.DropTable(
                name: "FlightPreparations");
        }
    }
}
