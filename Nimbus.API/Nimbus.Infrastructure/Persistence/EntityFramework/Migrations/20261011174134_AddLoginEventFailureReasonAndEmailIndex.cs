using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginEventFailureReasonAndEmailIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "LoginEvents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginEvents_Email_OccurredAt",
                table: "LoginEvents",
                columns: new[] { "Email", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoginEvents_Email_OccurredAt",
                table: "LoginEvents");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "LoginEvents");
        }
    }
}
