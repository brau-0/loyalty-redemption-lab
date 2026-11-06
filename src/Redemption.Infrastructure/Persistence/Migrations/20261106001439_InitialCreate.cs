using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redemption.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PointsReservations",
                columns: table => new
                {
                    RedemptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsReservations", x => x.RedemptionId);
                });

            migrationBuilder.CreateTable(
                name: "Redemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastDeliveryStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TotalPoints = table.Column<int>(type: "int", nullable: false),
                    PointsToReverse = table.Column<int>(type: "int", nullable: false),
                    StatusAfterReversal = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    History = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lines = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Redemptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointsReservations_CustomerId_State",
                table: "PointsReservations",
                columns: new[] { "CustomerId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_CustomerId_Status",
                table: "Redemptions",
                columns: new[] { "CustomerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PointsReservations");

            migrationBuilder.DropTable(
                name: "Redemptions");
        }
    }
}
