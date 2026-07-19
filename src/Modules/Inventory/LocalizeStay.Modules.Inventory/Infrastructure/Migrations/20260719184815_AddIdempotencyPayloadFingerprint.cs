using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyPayloadFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payload_fingerprint",
                schema: "inventory",
                table: "idempotency_keys",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payload_fingerprint",
                schema: "inventory",
                table: "idempotency_keys");
        }
    }
}
