using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AlignOfferWorkflowContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "comment",
                schema: "inventory",
                table: "offer_validations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invalidated_at",
                schema: "inventory",
                table: "offer_validations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invalidation_reason",
                schema: "inventory",
                table: "offer_validations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validation_id",
                schema: "inventory",
                table: "offer_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE inventory.offer_submissions AS submission
                SET validation_id = (
                    SELECT candidate."Id"
                    FROM inventory.offer_validations AS candidate
                    WHERE candidate.property_id = submission.property_id
                      AND candidate.revision = submission.revision
                    ORDER BY candidate.validated_at DESC
                    LIMIT 1
                );
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "validation_id",
                schema: "inventory",
                table: "offer_submissions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_offer_submissions_validation_id",
                schema: "inventory",
                table: "offer_submissions",
                column: "validation_id");

            migrationBuilder.AddForeignKey(
                name: "FK_offer_submissions_offer_validations_validation_id",
                schema: "inventory",
                table: "offer_submissions",
                column: "validation_id",
                principalSchema: "inventory",
                principalTable: "offer_validations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_offer_submissions_offer_validations_validation_id",
                schema: "inventory",
                table: "offer_submissions");

            migrationBuilder.DropIndex(
                name: "ix_offer_submissions_validation_id",
                schema: "inventory",
                table: "offer_submissions");

            migrationBuilder.DropColumn(
                name: "comment",
                schema: "inventory",
                table: "offer_validations");

            migrationBuilder.DropColumn(
                name: "invalidated_at",
                schema: "inventory",
                table: "offer_validations");

            migrationBuilder.DropColumn(
                name: "invalidation_reason",
                schema: "inventory",
                table: "offer_validations");

            migrationBuilder.DropColumn(
                name: "validation_id",
                schema: "inventory",
                table: "offer_submissions");
        }
    }
}
