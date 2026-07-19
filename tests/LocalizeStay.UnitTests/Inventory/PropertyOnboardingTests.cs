using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;
using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.UnitTests.Inventory;

public class PropertyOnboardingTests
{
    private static Property CreateProperty()
    {
        return new Property(
            "Pousada Mar do Sol",
            "dest-porto-de-galinhas",
            new Address(
                "Avenida Beira Mar",
                "250",
                null,
                "Centro",
                "Ipojuca",
                "PE",
                "55590-000",
                "BR"));
    }

    private static PropertyOnboarding CreateOnboarding()
    {
        return PropertyOnboarding.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "preselection-2026-0042",
            CreateProperty(),
            DateTimeOffset.Parse("2026-07-18T13:00:00Z"),
            TimeSpan.FromDays(10));
    }

    private static EvidenceReference[] GetEvidenceForGate(ReadinessGateType gateType)
    {
        return gateType switch
        {
            ReadinessGateType.SignedContract => new[] { new EvidenceReference(EvidenceKind.Contract, "contract-ref", "Contract signed") },
            ReadinessGateType.AuthorizedContact => new[] { new EvidenceReference(EvidenceKind.FormalAuthorization, "auth-ref", "Authorization verified") },
            ReadinessGateType.OperationalChannel => new[] { new EvidenceReference(EvidenceKind.Communication, "comm-ref", "Channel tested") },
            _ => new[] { new EvidenceReference(EvidenceKind.OfficialDocument, "doc-ref", "Document verified") },
        };
    }

    private static void FillAllGates(PropertyOnboarding onboarding)
    {
        foreach (var gateType in Enum.GetValues<ReadinessGateType>())
        {
            onboarding.ValidateGate(gateType, GetEvidenceForGate(gateType), "staff-001", DateTimeOffset.UtcNow);
        }
    }

    private static void ResolveAllIssues(PropertyOnboarding onboarding)
    {
        foreach (var issue in onboarding.PendingIssues.Where(i => i.Status == PendingIssueStatus.Open).ToList())
        {
            onboarding.ResolvePendingIssue(issue.Id, "Resolved", DateTimeOffset.UtcNow);
        }
    }

    [Fact]
    public void Create_ShouldInitializeWithSixPendingGates()
    {
        var onboarding = CreateOnboarding();

        onboarding.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.InProgress);
        onboarding.ReadinessStatus.Should().Be(ReadinessStatus.Blocked);
        onboarding.ReadinessGates.Should().HaveCount(6);
        onboarding.ReadinessGates.Should().AllSatisfy(g => g.Status.Should().Be(ReadinessGateStatus.Pending));
        onboarding.PendingIssues.Should().BeEmpty();
        onboarding.DuplicateReviewRequiresDecision.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetTargetSubmissionAt()
    {
        var openedAt = DateTimeOffset.Parse("2026-07-18T13:00:00Z");
        var onboarding = PropertyOnboarding.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "preselection-2026-0042",
            CreateProperty(),
            openedAt,
            TimeSpan.FromDays(10));

        onboarding.OpenedAt.Should().Be(openedAt);
        onboarding.TargetSubmissionAt.Should().Be(openedAt.AddDays(10));
    }

    [Fact]
    public void ValidateGate_ShouldUpdateGateStatus()
    {
        var onboarding = CreateOnboarding();
        var evidence = GetEvidenceForGate(ReadinessGateType.LegalIdentification);

        onboarding.ValidateGate(ReadinessGateType.LegalIdentification, evidence, "staff-001", DateTimeOffset.UtcNow);

        var gate = onboarding.ReadinessGates.Single(g => g.Type == ReadinessGateType.LegalIdentification);
        gate.Status.Should().Be(ReadinessGateStatus.Validated);
    }

    [Fact]
    public void RejectGate_ShouldSetStatusToRejected()
    {
        var onboarding = CreateOnboarding();

        onboarding.RejectGate(ReadinessGateType.LegalIdentification, "Missing documents", DateTimeOffset.UtcNow);

        var gate = onboarding.ReadinessGates.Single(g => g.Type == ReadinessGateType.LegalIdentification);
        gate.Status.Should().Be(ReadinessGateStatus.Rejected);
    }

    [Fact]
    public void ResetGateToPending_ShouldSetStatusToPending()
    {
        var onboarding = CreateOnboarding();
        var gateType = ReadinessGateType.LegalIdentification;
        onboarding.ValidateGate(gateType, GetEvidenceForGate(gateType), "staff-001", DateTimeOffset.UtcNow);

        onboarding.ResetGateToPending(gateType, DateTimeOffset.UtcNow);

        var gate = onboarding.ReadinessGates.Single(g => g.Type == gateType);
        gate.Status.Should().Be(ReadinessGateStatus.Pending);
    }

    [Fact]
    public void FlagDuplicateReviewRequired_ShouldUpdateUpdatedAt()
    {
        var onboarding = CreateOnboarding();
        var flagAt = DateTimeOffset.Parse("2026-07-19T14:00:00Z");

        onboarding.FlagDuplicateReviewRequired(flagAt);

        onboarding.DuplicateReviewRequiresDecision.Should().BeTrue();
        onboarding.UpdatedAt.Should().Be(flagAt);
    }

    [Fact]
    public void GetBlockingReasons_WhenSubmittedToCuration_ShouldReturnAlreadySubmitted()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        onboarding.SubmitToCuration(Guid.NewGuid(), "Ready", DateTimeOffset.UtcNow, "staff-001");

        var reasons = onboarding.GetBlockingReasons();

        reasons.Should().ContainSingle(r => r.Code == BlockingReasonCode.AlreadySubmitted);
    }

    [Fact]
    public void IsReady_WhenAllGatesValidatedAndNoIssues_ShouldBeTrue()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);

        onboarding.IsReady.Should().BeTrue();
        onboarding.ReadinessStatus.Should().Be(ReadinessStatus.Ready);
    }

    [Fact]
    public void IsReady_WhenGatePending_ShouldBeFalse()
    {
        var onboarding = CreateOnboarding();
        onboarding.ValidateGate(ReadinessGateType.LegalIdentification, GetEvidenceForGate(ReadinessGateType.LegalIdentification), "staff-001", DateTimeOffset.UtcNow);

        onboarding.IsReady.Should().BeFalse();
        onboarding.ReadinessStatus.Should().Be(ReadinessStatus.Blocked);
    }

    [Fact]
    public void IsReady_WhenOpenIssueExists_ShouldBeFalse()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        onboarding.AddPendingIssue(
            Guid.NewGuid(),
            "Confirm contract",
            PendingOwnerType.Legal,
            null,
            ReadinessGateType.SignedContract,
            null,
            DateTimeOffset.UtcNow,
            "staff-001");

        onboarding.IsReady.Should().BeFalse();
    }

    [Fact]
    public void IsReady_WhenDuplicateReviewRequired_ShouldBeFalse()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        onboarding.FlagDuplicateReviewRequired();

        onboarding.IsReady.Should().BeFalse();
    }

    [Fact]
    public void AddPendingIssue_ShouldCreateOpenIssue()
    {
        var onboarding = CreateOnboarding();
        var issueId = Guid.NewGuid();

        var issue = onboarding.AddPendingIssue(
            issueId,
            "Confirm contract",
            PendingOwnerType.Legal,
            "staff-002",
            ReadinessGateType.SignedContract,
            DateTimeOffset.UtcNow.AddDays(2),
            DateTimeOffset.UtcNow,
            "staff-001");

        issue.Id.Should().Be(issueId);
        issue.Status.Should().Be(PendingIssueStatus.Open);
        issue.RelatedGateType.Should().Be(ReadinessGateType.SignedContract);
        onboarding.PendingIssues.Should().ContainSingle();
    }

    [Fact]
    public void UpdatePendingIssue_ShouldUpdateDetails()
    {
        var onboarding = CreateOnboarding();
        var issue = onboarding.AddPendingIssue(
            Guid.NewGuid(),
            "Confirm contract",
            PendingOwnerType.Legal,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            "staff-001");

        onboarding.UpdatePendingIssue(
            issue.Id,
            "Confirm contract and authorization",
            PendingOwnerType.Partner,
            "staff-003",
            DateTimeOffset.UtcNow.AddDays(3),
            DateTimeOffset.UtcNow);

        var updated = onboarding.PendingIssues.Single(i => i.Id == issue.Id);
        updated.Description.Should().Be("Confirm contract and authorization");
        updated.OwnerType.Should().Be(PendingOwnerType.Partner);
        updated.AssigneeId.Should().Be("staff-003");
    }

    [Fact]
    public void ResolvePendingIssue_ShouldMarkAsResolved()
    {
        var onboarding = CreateOnboarding();
        var issue = onboarding.AddPendingIssue(
            Guid.NewGuid(),
            "Confirm contract",
            PendingOwnerType.Legal,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            "staff-001");

        onboarding.ResolvePendingIssue(issue.Id, "Validated with Legal", DateTimeOffset.UtcNow);

        var resolved = onboarding.PendingIssues.Single(i => i.Id == issue.Id);
        resolved.Status.Should().Be(PendingIssueStatus.Resolved);
        resolved.ResolutionNote.Should().Be("Validated with Legal");
    }

    [Fact]
    public void CancelPendingIssue_ShouldMarkAsCancelled()
    {
        var onboarding = CreateOnboarding();
        var issue = onboarding.AddPendingIssue(
            Guid.NewGuid(),
            "Confirm contract",
            PendingOwnerType.Legal,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            "staff-001");

        onboarding.CancelPendingIssue(issue.Id, "No longer needed", DateTimeOffset.UtcNow);

        var cancelled = onboarding.PendingIssues.Single(i => i.Id == issue.Id);
        cancelled.Status.Should().Be(PendingIssueStatus.Cancelled);
        cancelled.ResolutionNote.Should().Be("No longer needed");
    }

    [Fact]
    public void ResolvePendingIssue_WhenNotOpen_ShouldThrow()
    {
        var onboarding = CreateOnboarding();
        var issue = onboarding.AddPendingIssue(
            Guid.NewGuid(),
            "Confirm contract",
            PendingOwnerType.Legal,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            "staff-001");
        onboarding.ResolvePendingIssue(issue.Id, "Resolved", DateTimeOffset.UtcNow);

        var act = () => onboarding.ResolvePendingIssue(issue.Id, "Resolved again", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RecordCommunication_ShouldAddRecord()
    {
        var onboarding = CreateOnboarding();
        var receivedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var processedAt = DateTimeOffset.UtcNow;

        var record = onboarding.RecordCommunication(
            Guid.NewGuid(),
            CommunicationChannel.Whatsapp,
            receivedAt,
            processedAt,
            "Partner sent contract reference",
            TimeSpan.FromHours(4),
            "staff-001",
            DateTimeOffset.UtcNow);

        record.Channel.Should().Be(CommunicationChannel.Whatsapp);
        record.ProcessedWithinSla.Should().BeTrue();
        onboarding.CommunicationRecords.Should().ContainSingle();
    }

    [Fact]
    public void RecordCommunication_ProcessedLate_ShouldSetWithinSlaToFalse()
    {
        var onboarding = CreateOnboarding();
        var receivedAt = DateTimeOffset.UtcNow.AddHours(-5);
        var processedAt = DateTimeOffset.UtcNow;

        var record = onboarding.RecordCommunication(
            Guid.NewGuid(),
            CommunicationChannel.Email,
            receivedAt,
            processedAt,
            "Partner sent contract reference",
            TimeSpan.FromHours(4),
            "staff-001",
            DateTimeOffset.UtcNow);

        record.ProcessedWithinSla.Should().BeFalse();
    }

    [Fact]
    public void SubmitToCuration_WhenReady_ShouldSetSubmitted()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        var idempotencyKey = Guid.NewGuid();

        onboarding.SubmitToCuration(
            idempotencyKey,
            "Ready for curation",
            DateTimeOffset.Parse("2026-07-20T10:00:00Z"),
            "staff-001");

        onboarding.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.SubmittedToCuration);
        onboarding.SubmittedAt.Should().Be(DateTimeOffset.Parse("2026-07-20T10:00:00Z"));
    }

    [Fact]
    public void SubmitToCuration_WhenNotReady_ShouldThrow()
    {
        var onboarding = CreateOnboarding();
        var idempotencyKey = Guid.NewGuid();

        var act = () => onboarding.SubmitToCuration(
            idempotencyKey,
            "Ready for curation",
            DateTimeOffset.UtcNow,
            "staff-001");

        act.Should().Throw<BusinessRuleViolationException>()
            .Where(ex => ex.ErrorCode == "ONBOARDING_NOT_READY");
    }

    [Fact]
    public void SubmitToCuration_WithSameIdempotencyKey_ShouldBeIdempotent()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        var idempotencyKey = Guid.NewGuid();

        onboarding.SubmitToCuration(
            idempotencyKey,
            "Ready for curation",
            DateTimeOffset.Parse("2026-07-20T10:00:00Z"),
            "staff-001");

        var act = () => onboarding.SubmitToCuration(
            idempotencyKey,
            "Ready for curation",
            DateTimeOffset.Parse("2026-07-20T10:00:00Z"),
            "staff-001");

        act.Should().Throw<IdempotentReplayException>();
    }

    [Fact]
    public void RecordCurationReturn_ShouldReopenAndCreateIssues()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        onboarding.SubmitToCuration(Guid.NewGuid(), "Ready", DateTimeOffset.UtcNow, "staff-001");

        var curationReturn = onboarding.RecordCurationReturn(
            Guid.NewGuid(),
            "curation-001",
            CurationReturnReasonCode.InconsistentData,
            "Address mismatch",
            new[]
            {
                new CurationReturnIssue("Fix property address", PendingOwnerType.Staff, ReadinessGateType.PropertyBasics),
            },
            DateTimeOffset.Parse("2026-07-21T10:00:00Z"),
            "staff-002",
            Guid.NewGuid());

        onboarding.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.ReturnedByCuration);
        onboarding.SubmittedAt.Should().BeNull();
        curationReturn.Issues.Should().HaveCount(1);
        onboarding.PendingIssues.Should().HaveCount(1);
        onboarding.PendingIssues.Single().Description.Should().Be("Fix property address");
    }

    [Fact]
    public void RecordCurationReturn_WhenNotSubmitted_ShouldThrow()
    {
        var onboarding = CreateOnboarding();

        var act = () => onboarding.RecordCurationReturn(
            Guid.NewGuid(),
            null,
            CurationReturnReasonCode.MissingData,
            "Missing data",
            new[] { new CurationReturnIssue("Add contract", PendingOwnerType.Partner, ReadinessGateType.SignedContract) },
            DateTimeOffset.UtcNow,
            "staff-002",
            Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Close_WhenInProgress_ShouldSetClosed()
    {
        var onboarding = CreateOnboarding();

        onboarding.Close(
            CloseReasonCode.PartnerWithdrawal,
            "Partner requested to stop the evaluation process.",
            DateTimeOffset.Parse("2026-07-20T10:00:00Z"),
            "staff-001");

        onboarding.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.Closed);
        onboarding.ReasonCode.Should().Be(CloseReasonCode.PartnerWithdrawal);
        onboarding.ClosedAt.Should().Be(DateTimeOffset.Parse("2026-07-20T10:00:00Z"));
    }

    [Fact]
    public void Close_WhenAlreadyClosed_ShouldThrow()
    {
        var onboarding = CreateOnboarding();
        onboarding.Close(
            CloseReasonCode.PartnerWithdrawal,
            "Partner requested to stop the evaluation process.",
            DateTimeOffset.UtcNow,
            "staff-001");

        var act = () => onboarding.Close(
            CloseReasonCode.Other,
            "Second close attempt.",
            DateTimeOffset.UtcNow,
            "staff-001");

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Close_WithShortReason_ShouldThrow()
    {
        var onboarding = CreateOnboarding();

        var act = () => onboarding.Close(
            CloseReasonCode.Other,
            "Too short",
            DateTimeOffset.UtcNow,
            "staff-001");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProperty_ShouldUpdatePropertyDetails()
    {
        var onboarding = CreateOnboarding();
        var newAddress = new Address(
            "Rua das Palmeiras",
            "100",
            "Apto 1",
            "Vila Nova",
            "Ipojuca",
            "PE",
            "55590-001",
            "BR");

        onboarding.UpdateProperty("Pousada Mar do Sol Boutique", newAddress, DateTimeOffset.UtcNow);

        onboarding.Property.Name.Should().Be("Pousada Mar do Sol Boutique");
        onboarding.Property.Address.Street.Should().Be("Rua das Palmeiras");
    }

    [Fact]
    public void GetBlockingReasons_WhenNotReady_ShouldReturnGateReasons()
    {
        var onboarding = CreateOnboarding();

        var reasons = onboarding.GetBlockingReasons();

        reasons.Should().Contain(r => r.Code == BlockingReasonCode.GateNotValidated);
    }

    [Fact]
    public void GetBlockingReasons_WhenClosed_ShouldReturnClosedReason()
    {
        var onboarding = CreateOnboarding();
        onboarding.Close(
            CloseReasonCode.PartnerWithdrawal,
            "Partner requested to stop the evaluation process.",
            DateTimeOffset.UtcNow,
            "staff-001");

        var reasons = onboarding.GetBlockingReasons();

        reasons.Should().ContainSingle(r => r.Code == BlockingReasonCode.OnboardingClosed);
    }

    [Fact]
    public void Operations_WhenClosed_ShouldThrow()
    {
        var onboarding = CreateOnboarding();
        onboarding.Close(
            CloseReasonCode.PartnerWithdrawal,
            "Partner requested to stop the evaluation process.",
            DateTimeOffset.UtcNow,
            "staff-001");

        var act = () => onboarding.AddPendingIssue(
            Guid.NewGuid(),
            "Confirm contract",
            PendingOwnerType.Legal,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            "staff-001");

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void ValidateGate_WhenClosed_ShouldThrow()
    {
        var onboarding = CreateOnboarding();
        onboarding.Close(
            CloseReasonCode.PartnerWithdrawal,
            "Partner requested to stop the evaluation process.",
            DateTimeOffset.UtcNow,
            "staff-001");

        var act = () => onboarding.ValidateGate(
            ReadinessGateType.LegalIdentification,
            GetEvidenceForGate(ReadinessGateType.LegalIdentification),
            "staff-001",
            DateTimeOffset.UtcNow);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void UpdateProperty_WithInvalidName_ShouldThrow()
    {
        var onboarding = CreateOnboarding();

        var act = () => onboarding.UpdateProperty("A", new Address(
            "Rua",
            "1",
            null,
            "Bairro",
            "Cidade",
            "PE",
            "12345",
            "BR"), DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordCurationReturn_WithSameIdempotencyKey_ShouldBeIdempotent()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        onboarding.SubmitToCuration(Guid.NewGuid(), "Ready", DateTimeOffset.UtcNow, "staff-001");
        var idempotencyKey = Guid.NewGuid();

        onboarding.RecordCurationReturn(
            Guid.NewGuid(),
            null,
            CurationReturnReasonCode.MissingData,
            "Missing data",
            new[] { new CurationReturnIssue("Add contract", PendingOwnerType.Partner, ReadinessGateType.SignedContract) },
            DateTimeOffset.UtcNow,
            "staff-002",
            idempotencyKey);

        var act = () => onboarding.RecordCurationReturn(
            Guid.NewGuid(),
            null,
            CurationReturnReasonCode.MissingData,
            "Missing data",
            new[] { new CurationReturnIssue("Add contract", PendingOwnerType.Partner, ReadinessGateType.SignedContract) },
            DateTimeOffset.UtcNow,
            "staff-002",
            idempotencyKey);

        act.Should().Throw<IdempotentReplayException>();
    }

    [Theory]
    [InlineData("Avenida", "1", "Bairro", "Cidade", "P", "12345", "BR")]
    [InlineData("Avenida", "1", "Bairro", "Cidade", "PE", "12345", "bra")]
    public void CreateAddress_WithInvalidData_ShouldThrow(
        string street, string number, string district, string city, string state, string postalCode, string countryCode)
    {
        var act = () => new Address(street, number, null, district, city, state, postalCode, countryCode);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateAddress_WithComplementTooLong_ShouldThrow()
    {
        var complement = new string('a', 121);

        var act = () => new Address("Avenida", "1", complement, "Bairro", "Cidade", "PE", "12345", "BR");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateAddress_WithValidData_ShouldExposeAllProperties()
    {
        var address = new Address("Avenida Beira Mar", "250", "Próximo à praça", "Centro", "Ipojuca", "PE", "55590-000", "BR");

        address.Street.Should().Be("Avenida Beira Mar");
        address.Number.Should().Be("250");
        address.Complement.Should().Be("Próximo à praça");
        address.District.Should().Be("Centro");
        address.City.Should().Be("Ipojuca");
        address.State.Should().Be("PE");
        address.PostalCode.Should().Be("55590-000");
        address.CountryCode.Should().Be("BR");
    }

    [Fact]
    public void CreateProperty_WithDestinationIdTooLong_ShouldThrow()
    {
        var destinationId = new string('a', 121);
        var address = new Address("Avenida", "1", null, "Bairro", "Cidade", "PE", "12345", "BR");

        var act = () => new Property("Valid Name", destinationId, address);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateProperty_WithNullAddress_ShouldThrow()
    {
        var act = () => new Property("Valid Name", "dest-id", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateEvidenceReference_WithDescriptionTooLong_ShouldThrow()
    {
        var description = new string('a', 301);

        var act = () => new EvidenceReference(EvidenceKind.Contract, "ref", description);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateEvidenceReference_WithReferenceTooLong_ShouldThrow()
    {
        var reference = new string('a', 501);

        var act = () => new EvidenceReference(EvidenceKind.Contract, reference, "Description");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SubmitToCuration_AfterReturn_WhenReady_ShouldSubmitAgain()
    {
        var onboarding = CreateOnboarding();
        FillAllGates(onboarding);
        onboarding.SubmitToCuration(Guid.NewGuid(), "Ready", DateTimeOffset.UtcNow, "staff-001");
        onboarding.RecordCurationReturn(
            Guid.NewGuid(),
            null,
            CurationReturnReasonCode.MissingData,
            "Missing data",
            new[] { new CurationReturnIssue("Add contract", PendingOwnerType.Partner, ReadinessGateType.SignedContract) },
            DateTimeOffset.UtcNow,
            "staff-002",
            Guid.NewGuid());
        onboarding.ValidateGate(ReadinessGateType.SignedContract, GetEvidenceForGate(ReadinessGateType.SignedContract), "staff-001", DateTimeOffset.UtcNow);
        ResolveAllIssues(onboarding);

        onboarding.SubmitToCuration(Guid.NewGuid(), "Ready again", DateTimeOffset.UtcNow, "staff-001");

        onboarding.LifecycleStatus.Should().Be(OnboardingLifecycleStatus.SubmittedToCuration);
    }
}
