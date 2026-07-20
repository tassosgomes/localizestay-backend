using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalizeStay.Modules.Inventory.Infrastructure.Migrations;

internal partial class AddReadinessGateContractReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "contract_number", schema: "inventory", table: "readiness_gates", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "contract_repository_reference", schema: "inventory", table: "readiness_gates", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "contract_responsible_parties", schema: "inventory", table: "readiness_gates", type: "jsonb", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "contract_signed_at", schema: "inventory", table: "readiness_gates", type: "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "contract_number", schema: "inventory", table: "readiness_gates");
        migrationBuilder.DropColumn(name: "contract_repository_reference", schema: "inventory", table: "readiness_gates");
        migrationBuilder.DropColumn(name: "contract_responsible_parties", schema: "inventory", table: "readiness_gates");
        migrationBuilder.DropColumn(name: "contract_signed_at", schema: "inventory", table: "readiness_gates");
    }
}
