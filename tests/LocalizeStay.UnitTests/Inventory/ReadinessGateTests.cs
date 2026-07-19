using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

namespace LocalizeStay.UnitTests.Inventory;

public class ReadinessGateTests
{
    [Fact]
    public void Create_ShouldInitializeAsPending()
    {
        var now = DateTimeOffset.UtcNow;

        var gate = ReadinessGate.Create(ReadinessGateType.LegalIdentification, now);

        gate.Status.Should().Be(ReadinessGateStatus.Pending);
        gate.Type.Should().Be(ReadinessGateType.LegalIdentification);
        gate.Evidence.Should().BeEmpty();
        gate.ValidatedAt.Should().BeNull();
        gate.ValidatedBy.Should().BeNull();
    }

    [Fact]
    public void Validate_WithEvidence_ShouldSetStatusToValidated()
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(ReadinessGateType.LegalIdentification, now);
        var evidence = new[] { new EvidenceReference(EvidenceKind.OfficialDocument, "doc-ref", "Document verified") };

        gate.Validate(evidence, "staff-001", now.AddMinutes(1));

        gate.Status.Should().Be(ReadinessGateStatus.Validated);
        gate.Evidence.Should().HaveCount(1);
        gate.ValidatedBy.Should().Be("staff-001");
        gate.ValidatedAt.Should().Be(now.AddMinutes(1));
    }

    [Fact]
    public void Validate_WithoutEvidence_ShouldThrow()
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(ReadinessGateType.SignedContract, now);

        var act = () => gate.Validate([], "staff-001", now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(ReadinessGateType.SignedContract, EvidenceKind.Contract)]
    [InlineData(ReadinessGateType.AuthorizedContact, EvidenceKind.FormalAuthorization)]
    [InlineData(ReadinessGateType.OperationalChannel, EvidenceKind.Communication)]
    public void Validate_GateWithRequiredEvidenceKind_ShouldRequireSpecificEvidence(ReadinessGateType type, EvidenceKind requiredKind)
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(type, now);
        var wrongEvidence = new[] { new EvidenceReference(EvidenceKind.Other, "other-ref", "Other evidence") };

        var act = () => gate.Validate(wrongEvidence, "staff-001", now);

        act.Should().Throw<ArgumentException>();

        var correctEvidence = new[] { new EvidenceReference(requiredKind, "ref", "Correct evidence") };
        gate.Validate(correctEvidence, "staff-001", now);

        gate.Status.Should().Be(ReadinessGateStatus.Validated);
    }

    [Fact]
    public void Validate_LegalIdentification_WithOfficialDocument_ShouldSucceed()
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(ReadinessGateType.LegalIdentification, now);
        var evidence = new[] { new EvidenceReference(EvidenceKind.OfficialDocument, "doc-ref", "Legal ID verified") };

        gate.Validate(evidence, "staff-001", now);

        gate.Status.Should().Be(ReadinessGateStatus.Validated);
    }

    [Fact]
    public void Reject_ShouldSetStatusToRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(ReadinessGateType.LegalIdentification, now);

        gate.Reject("Missing documentation", now);

        gate.Status.Should().Be(ReadinessGateStatus.Rejected);
        gate.Notes.Should().Be("Missing documentation");
        gate.ValidatedAt.Should().BeNull();
        gate.ValidatedBy.Should().BeNull();
    }

    [Fact]
    public void ResetToPending_ShouldClearValidation()
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(ReadinessGateType.LegalIdentification, now);
        var evidence = new[] { new EvidenceReference(EvidenceKind.OfficialDocument, "doc-ref", "Document verified") };
        gate.Validate(evidence, "staff-001", now);

        gate.ResetToPending(now.AddHours(1));

        gate.Status.Should().Be(ReadinessGateStatus.Pending);
        gate.Evidence.Should().BeEmpty();
        gate.ValidatedAt.Should().BeNull();
        gate.ValidatedBy.Should().BeNull();
    }

    [Fact]
    public void Validate_WithNullValidatedBy_ShouldThrow()
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(ReadinessGateType.LegalIdentification, now);
        var evidence = new[] { new EvidenceReference(EvidenceKind.OfficialDocument, "doc-ref", "Document verified") };

        var act = () => gate.Validate(evidence, null!, now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_WithNullEvidence_ShouldThrow()
    {
        var now = DateTimeOffset.UtcNow;
        var gate = ReadinessGate.Create(ReadinessGateType.LegalIdentification, now);

        var act = () => gate.Validate(null!, "staff-001", now);

        act.Should().Throw<ArgumentNullException>();
    }
}
