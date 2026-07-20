using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    internal partial class AddPortfolioOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuditType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "partners",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreselectionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    TradeName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    legal_identifier_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    legal_identifier_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    legal_identifier_value = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    legal_identifier_normalized_value = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "property_onboardings",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreselectionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    property_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    property_destination_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    property_address_street = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    property_address_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    property_address_complement = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    property_address_district = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    property_address_city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    property_address_state = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    property_address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    property_address_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DuplicateReviewRequiresDecision = table.Column<bool>(type: "boolean", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TargetSubmissionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    close_reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    close_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    property_similarity_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_onboardings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "communication_records",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    result_summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    processed_within_sla = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PropertyOnboardingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_communication_records_property_onboardings_PropertyOnboardi~",
                        column: x => x.PropertyOnboardingId,
                        principalSchema: "inventory",
                        principalTable: "property_onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "curation_returns",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    curation_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    returned_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PropertyOnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    issues = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curation_returns", x => x.Id);
                    table.ForeignKey(
                        name: "fk_curation_returns_property_onboarding_id",
                        column: x => x.PropertyOnboardingId,
                        principalSchema: "inventory",
                        principalTable: "property_onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "duplicate_reviews",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    existing_property_id = table.Column<Guid>(type: "uuid", nullable: true),
                    justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PropertyOnboardingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_duplicate_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_duplicate_reviews_property_onboardings_PropertyOnboardingId",
                        column: x => x.PropertyOnboardingId,
                        principalSchema: "inventory",
                        principalTable: "property_onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyOnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.Id);
                    table.ForeignKey(
                        name: "fk_idempotency_keys_property_onboarding_id",
                        column: x => x.PropertyOnboardingId,
                        principalSchema: "inventory",
                        principalTable: "property_onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pending_issues",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    owner_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assignee_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    related_gate_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    opened_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PropertyOnboardingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_issues_property_onboardings_PropertyOnboardingId",
                        column: x => x.PropertyOnboardingId,
                        principalSchema: "inventory",
                        principalTable: "property_onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "readiness_gates",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PropertyOnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_readiness_gates", x => x.Id);
                    table.ForeignKey(
                        name: "fk_readiness_gates_property_onboarding_id",
                        column: x => x.PropertyOnboardingId,
                        principalSchema: "inventory",
                        principalTable: "property_onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_aggregate_occurred",
                schema: "inventory",
                table: "audit_entries",
                columns: new[] { "AggregateType", "AggregateId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_correlation_id",
                schema: "inventory",
                table: "audit_entries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "ix_communication_records_property_onboarding_id_processed_within_sla",
                schema: "inventory",
                table: "communication_records",
                columns: new[] { "PropertyOnboardingId", "processed_within_sla" });

            migrationBuilder.CreateIndex(
                name: "ix_curation_returns_property_onboarding_id_returned_at",
                schema: "inventory",
                table: "curation_returns",
                columns: new[] { "PropertyOnboardingId", "returned_at" });

            migrationBuilder.CreateIndex(
                name: "IX_duplicate_reviews_PropertyOnboardingId",
                schema: "inventory",
                table: "duplicate_reviews",
                column: "PropertyOnboardingId");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_property_onboarding_id_key_scope",
                schema: "inventory",
                table: "idempotency_keys",
                columns: new[] { "PropertyOnboardingId", "key", "scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc_OccurredOnUtc",
                schema: "inventory",
                table: "outbox_messages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_partners_legal_identifier_unique",
                schema: "inventory",
                table: "partners",
                columns: new[] { "legal_identifier_country_code", "legal_identifier_type", "legal_identifier_normalized_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partners_legal_name",
                schema: "inventory",
                table: "partners",
                column: "LegalName");

            migrationBuilder.CreateIndex(
                name: "ix_partners_preselection_id",
                schema: "inventory",
                table: "partners",
                column: "PreselectionId");

            migrationBuilder.CreateIndex(
                name: "ix_pending_issues_property_onboarding_id_status",
                schema: "inventory",
                table: "pending_issues",
                columns: new[] { "PropertyOnboardingId", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_property_onboardings_active_similarity_unique",
                schema: "inventory",
                table: "property_onboardings",
                column: "property_similarity_key",
                unique: true,
                filter: "lifecycle_status <> 'Closed'");

            migrationBuilder.CreateIndex(
                name: "ix_property_onboardings_lifecycle_status",
                schema: "inventory",
                table: "property_onboardings",
                column: "lifecycle_status");

            migrationBuilder.CreateIndex(
                name: "ix_property_onboardings_opened_at",
                schema: "inventory",
                table: "property_onboardings",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "ix_property_onboardings_partner_id",
                schema: "inventory",
                table: "property_onboardings",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "ix_property_onboardings_target_submission_at",
                schema: "inventory",
                table: "property_onboardings",
                column: "TargetSubmissionAt");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_gates_property_onboarding_id_type",
                schema: "inventory",
                table: "readiness_gates",
                columns: new[] { "PropertyOnboardingId", "type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "communication_records",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "curation_returns",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "duplicate_reviews",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "partners",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "pending_issues",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "readiness_gates",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "property_onboardings",
                schema: "inventory");
        }
    }
}
