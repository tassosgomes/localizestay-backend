using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incorporated_properties",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    property_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    destination_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    initial_actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    onboarding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incorporated_properties", x => x.Id);
                });

            BackfillIncorporatedProperties(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "offer_validations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    validated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_validations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "commercial_offers",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    revision_author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentValidationId = table.Column<Guid>(type: "uuid", nullable: true),
                    accommodation_count = table.Column<int>(type: "integer", nullable: false),
                    blocking_issue_count = table.Column<int>(type: "integer", nullable: false),
                    complete_information_received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    target_submission_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pending_issues = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commercial_offers_incorporated_properties_Id",
                        column: x => x.Id,
                        principalSchema: "inventory",
                        principalTable: "incorporated_properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_commercial_offers_offer_validations_CurrentValidationId",
                        column: x => x.CurrentValidationId,
                        principalSchema: "inventory",
                        principalTable: "offer_validations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "accommodations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ever_submitted = table.Column<bool>(type: "boolean", nullable: false),
                    deactivation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    max_adults = table.Column<int>(type: "integer", nullable: true),
                    max_children = table.Column<int>(type: "integer", nullable: true),
                    total_capacity = table.Column<int>(type: "integer", nullable: true),
                    meal_plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    child_age_range_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    child_minimum_age = table.Column<int>(type: "integer", nullable: true),
                    child_maximum_age = table.Column<int>(type: "integer", nullable: true),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bed_configuration = table.Column<string>(type: "jsonb", nullable: false),
                    structural_features = table.Column<string>(type: "jsonb", nullable: false),
                    submission_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accommodations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accommodations_commercial_offers_property_id",
                        column: x => x.property_id,
                        principalSchema: "inventory",
                        principalTable: "commercial_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commercial_offer_idempotency_keys",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    result_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_offer_idempotency_keys", x => x.Id);
                    table.ForeignKey(
                        name: "fk_commercial_offer_idempotency_keys_offer_id",
                        column: x => x.property_id,
                        principalSchema: "inventory",
                        principalTable: "commercial_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commercial_policies",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    ever_submitted = table.Column<bool>(type: "boolean", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    rules_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rule_set_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submission_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commercial_policies_commercial_offers_property_id",
                        column: x => x.property_id,
                        principalSchema: "inventory",
                        principalTable: "commercial_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commercial_rates",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    accommodation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    condition_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    base_price_cents = table.Column<long>(type: "bigint", nullable: true),
                    included_guests = table.Column<int>(type: "integer", nullable: true),
                    additional_adult_price_cents = table.Column<long>(type: "bigint", nullable: true),
                    additional_child_price_cents = table.Column<long>(type: "bigint", nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    minimum_nights = table.Column<int>(type: "integer", nullable: true),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    meal_plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deactivation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ever_submitted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submission_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commercial_rates_commercial_offers_property_id",
                        column: x => x.property_id,
                        principalSchema: "inventory",
                        principalTable: "commercial_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offer_returns",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    returned_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_offer_returns_commercial_offers_property_id",
                        column: x => x.property_id,
                        principalSchema: "inventory",
                        principalTable: "commercial_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offer_submissions",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    submitted_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_offer_submissions_commercial_offers_property_id",
                        column: x => x.property_id,
                        principalSchema: "inventory",
                        principalTable: "commercial_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accommodations_property_status",
                schema: "inventory",
                table: "accommodations",
                columns: new[] { "property_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_commercial_offer_idempotency_keys_property_key_scope",
                schema: "inventory",
                table: "commercial_offer_idempotency_keys",
                columns: new[] { "property_id", "key", "scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_offers_CurrentValidationId",
                schema: "inventory",
                table: "commercial_offers",
                column: "CurrentValidationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_commercial_offers_state",
                schema: "inventory",
                table: "commercial_offers",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_offers_state_target_submission",
                schema: "inventory",
                table: "commercial_offers",
                columns: new[] { "state", "target_submission_at" });

            migrationBuilder.CreateIndex(
                name: "ix_commercial_policies_property_type_status",
                schema: "inventory",
                table: "commercial_policies",
                columns: new[] { "property_id", "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_commercial_rates_accommodation",
                schema: "inventory",
                table: "commercial_rates",
                column: "accommodation_id");

            migrationBuilder.CreateIndex(
                name: "ix_commercial_rates_overlap",
                schema: "inventory",
                table: "commercial_rates",
                columns: new[] { "accommodation_id", "condition_code", "policy_id", "meal_plan", "valid_from", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "ix_commercial_rates_property_status",
                schema: "inventory",
                table: "commercial_rates",
                columns: new[] { "property_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_incorporated_properties_onboarding_id_unique",
                schema: "inventory",
                table: "incorporated_properties",
                column: "onboarding_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offer_returns_property_submission",
                schema: "inventory",
                table: "offer_returns",
                columns: new[] { "property_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "ix_offer_submissions_property_revision",
                schema: "inventory",
                table: "offer_submissions",
                columns: new[] { "property_id", "revision" });

            migrationBuilder.CreateIndex(
                name: "ix_offer_validations_property_revision",
                schema: "inventory",
                table: "offer_validations",
                columns: new[] { "property_id", "revision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accommodations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "commercial_offer_idempotency_keys",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "commercial_policies",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "commercial_rates",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "offer_returns",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "offer_submissions",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "commercial_offers",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "incorporated_properties",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "offer_validations",
                schema: "inventory");
        }

        private static void BackfillIncorporatedProperties(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO inventory.incorporated_properties (
                    "Id",
                    "PartnerId",
                    "property_name",
                    "destination_id",
                    "initial_actor",
                    "onboarding_id",
                    "created_at",
                    "updated_at"
                )
                SELECT
                    po."Id",
                    po."PartnerId",
                    po."property_name",
                    po."property_destination_id",
                    COALESCE(
                        (SELECT cm."created_by"
                         FROM inventory.communication_records cm
                         WHERE cm."PropertyOnboardingId" = po."Id"
                         ORDER BY cm."created_at"
                         LIMIT 1),
                        'system-backfill'
                    ),
                    po."Id",
                    po."CreatedAt",
                    po."UpdatedAt"
                FROM inventory.property_onboardings po
                WHERE po."lifecycle_status" IN ('SubmittedToCuration', 'Closed')
                ON CONFLICT ("Id") DO NOTHING;
                """);
        }
    }
}
