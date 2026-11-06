using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redemption.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server cannot add IDENTITY to an existing column, so the outbox table is recreated.
            // Safe here because outbox rows are transient. In production, drain the outbox first.
            migrationBuilder.DropTable(name: "OutboxMessages");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

            // Filtered index: the relay only looks for pending messages, in order.
            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Sequence",
                table: "OutboxMessages",
                column: "Sequence",
                filter: "[ProcessedAt] IS NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the previous shape: Sequence without IDENTITY and without the filtered index.
            migrationBuilder.DropTable(name: "OutboxMessages");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));
        }
    }
}
