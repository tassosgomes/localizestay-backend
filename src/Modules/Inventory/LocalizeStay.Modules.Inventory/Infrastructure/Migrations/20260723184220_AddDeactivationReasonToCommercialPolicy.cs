using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeactivationReasonToCommercialPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deactivation_reason",
                schema: "inventory",
                table: "commercial_policies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deactivation_reason",
                schema: "inventory",
                table: "commercial_policies");
        }
    }
}
