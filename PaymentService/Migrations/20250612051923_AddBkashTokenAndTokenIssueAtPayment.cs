using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class AddBkashTokenAndTokenIssueAtPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BkashToken",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenIssuedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BkashToken",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TokenIssuedAt",
                table: "Payments");
        }
    }
}
