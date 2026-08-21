using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nimbus.Infrastructure.Persistence.EntityFramework.Migrations
{
    public partial class AddVerificationEmailLockout : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationEmailCooldownEnd",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerificationEmailSendCount",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "VerificationEmailLocked",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationEmailCooldownEnd",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationEmailSendCount",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationEmailLocked",
                table: "AspNetUsers");
        }
    }
}
