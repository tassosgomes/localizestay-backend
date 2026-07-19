using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class ReadinessCommandHandlerTests
{
    [Theory]
    [InlineData("LegalIdentification", "OfficialDocument")]
    [InlineData("CommercialTerms", "OfficialDocument")]
    [InlineData("SignedContract", "Contract")]
    [InlineData("AuthorizedContact", "FormalAuthorization")]
    [InlineData("PropertyBasics", "OfficialDocument")]
    [InlineData("OperationalChannel", "Communication")]
    public void Validate_WithRequiredEvidenceForEveryGate_ShouldValidate(string typeValue, string evidenceKindValue)
    {
        var type = Enum.Parse<ReadinessGateType>(typeValue);
        var evidenceKind = Enum.Parse<EvidenceKind>(evidenceKindValue);
        var gate = ReadinessGate.Create(type, DateTimeOffset.UtcNow);
        var evidence = new[] { new EvidenceReference(evidenceKind, "reference-001", "Validated evidence.") };
        var contract = type == ReadinessGateType.SignedContract ? new ContractReference("reference-001", "LST-001", DateTimeOffset.UtcNow, ["LocalizeStay Ltda."]) : null;

        gate.Validate(evidence, contract, "staff-001", DateTimeOffset.UtcNow);

        gate.Status.Should().Be(ReadinessGateStatus.Validated);
    }

    [Fact]
    public void Validate_AuthorizedContactWithoutFormalReference_ShouldReject()
    {
        var gate = ReadinessGate.Create(ReadinessGateType.AuthorizedContact, DateTimeOffset.UtcNow);

        var action = () => gate.Validate([new EvidenceReference(EvidenceKind.Other, "contact", "Contact details.")], null, "staff-001", DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_CommunicationWithinFourHours_ShouldCalculateSla()
    {
        var receivedAt = DateTimeOffset.Parse("2026-07-20T10:00:00Z");

        var record = CommunicationRecord.Create(Guid.NewGuid(), CommunicationChannel.Email, receivedAt, receivedAt.AddHours(3), "Authorization reference received.", TimeSpan.FromHours(4), "staff-001", receivedAt.AddHours(3));

        record.ProcessedWithinSla.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithResolutionNote_ShouldPreserveIssueHistory()
    {
        var issue = PendingIssue.Create(Guid.NewGuid(), "Confirm formal authorization.", PendingOwnerType.Legal, null, ReadinessGateType.AuthorizedContact, null, DateTimeOffset.UtcNow, "staff-001");

        issue.Resolve("Authorization validated.", DateTimeOffset.UtcNow);

        issue.Status.Should().Be(PendingIssueStatus.Resolved);
        issue.ResolutionNote.Should().Be("Authorization validated.");
    }
}
