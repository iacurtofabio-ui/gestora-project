using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestoraWebApi.Migrations
{
    /// <inheritdoc />
    public partial class PrivacyEAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvvisoInattivitaInviatoIl",
                table: "Utenti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacyAccettataIl",
                table: "Utenti",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyVersione",
                table: "Utenti",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Prenotazioni",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvvisoInattivitaInviatoIl",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "PrivacyAccettataIl",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "PrivacyVersione",
                table: "Utenti");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Prenotazioni",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
